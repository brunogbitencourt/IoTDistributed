#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <time.h>

#include "esp_err.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_netif.h"
#include "esp_netif_sntp.h"
#include "esp_random.h"
#include "esp_wifi.h"
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/task.h"
#include "mqtt_client.h"
#include "nvs_flash.h"

#define WIFI_READY_BIT BIT0
#define MQTT_READY_BIT BIT1

static const char *TAG = "iot_sensor_sim";
static EventGroupHandle_t s_connection_events;
static esp_mqtt_client_handle_t s_mqtt_client;
static char s_client_id[32];

typedef struct {
    const char *sensor_id;
    const char *topic;
    double minimum;
    double maximum;
} simulated_sensor_t;

static const simulated_sensor_t SENSORS[] = {
    {"TEMP-001",  "iot/sensors/TEMP-001/readings",  20.0, 35.0},
    {"PRESS-001", "iot/sensors/PRESS-001/readings", 0.80, 1.60},
    {"LEVEL-001", "iot/sensors/LEVEL-001/readings", 15.0, 95.0},
};

static void wifi_event_handler(void *arg, esp_event_base_t event_base,
                               int32_t event_id, void *event_data)
{
    (void)arg;
    (void)event_data;

    if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_START) {
        ESP_ERROR_CHECK(esp_wifi_connect());
    } else if (event_base == WIFI_EVENT &&
               event_id == WIFI_EVENT_STA_DISCONNECTED) {
        xEventGroupClearBits(s_connection_events, WIFI_READY_BIT | MQTT_READY_BIT);
        ESP_LOGW(TAG, "Wi-Fi desconectado; tentando reconectar...");
        esp_err_t err = esp_wifi_connect();
        if (err != ESP_OK) {
            ESP_LOGE(TAG, "Falha ao solicitar reconexao Wi-Fi: %s",
                     esp_err_to_name(err));
        }
    } else if (event_base == IP_EVENT && event_id == IP_EVENT_STA_GOT_IP) {
        ip_event_got_ip_t *event = (ip_event_got_ip_t *)event_data;
        ESP_LOGI(TAG, "Wi-Fi conectado. IP: " IPSTR,
                 IP2STR(&event->ip_info.ip));
        xEventGroupSetBits(s_connection_events, WIFI_READY_BIT);
    }
}

static void wifi_start(void)
{
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t init_cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&init_cfg));
    ESP_ERROR_CHECK(esp_wifi_set_storage(WIFI_STORAGE_RAM));

    ESP_ERROR_CHECK(esp_event_handler_register(
        WIFI_EVENT, ESP_EVENT_ANY_ID, wifi_event_handler, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(
        IP_EVENT, IP_EVENT_STA_GOT_IP, wifi_event_handler, NULL));

    wifi_config_t wifi_cfg = {0};
    snprintf((char *)wifi_cfg.sta.ssid, sizeof(wifi_cfg.sta.ssid),
             "%s", CONFIG_SIM_WIFI_SSID);
    snprintf((char *)wifi_cfg.sta.password, sizeof(wifi_cfg.sta.password),
             "%s", CONFIG_SIM_WIFI_PASSWORD);
    wifi_cfg.sta.threshold.authmode =
        strlen(CONFIG_SIM_WIFI_PASSWORD) == 0
            ? WIFI_AUTH_OPEN
            : WIFI_AUTH_WPA2_PSK;

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_cfg));
    ESP_ERROR_CHECK(esp_wifi_start());
}

static bool clock_is_synchronized(void)
{
    // Evita publicar datas invalidas (ex.: 1970) antes do SNTP.
    return time(NULL) >= 1704067200; // 2024-01-01 UTC
}

static void synchronize_clock(void)
{
    esp_sntp_config_t cfg =
        ESP_NETIF_SNTP_DEFAULT_CONFIG(CONFIG_SIM_SNTP_SERVER);
    ESP_ERROR_CHECK(esp_netif_sntp_init(&cfg));

    while (!clock_is_synchronized()) {
        esp_err_t err = esp_netif_sntp_sync_wait(pdMS_TO_TICKS(15000));
        if (err != ESP_OK) {
            ESP_LOGW(TAG, "Aguardando NTP (%s). Verifique acesso ao servidor.",
                     esp_err_to_name(err));
            vTaskDelay(pdMS_TO_TICKS(2000));
        }
    }
    ESP_LOGI(TAG, "Relogio sincronizado por NTP.");
}

static void mqtt_event_handler(void *args, esp_event_base_t base,
                               int32_t event_id, void *event_data)
{
    (void)args;
    (void)base;
    esp_mqtt_event_handle_t event = (esp_mqtt_event_handle_t)event_data;

    switch ((esp_mqtt_event_id_t)event_id) {
    case MQTT_EVENT_CONNECTED:
        xEventGroupSetBits(s_connection_events, MQTT_READY_BIT);
        ESP_LOGI(TAG, "MQTT conectado ao broker.");
        break;
    case MQTT_EVENT_DISCONNECTED:
        xEventGroupClearBits(s_connection_events, MQTT_READY_BIT);
        ESP_LOGW(TAG, "MQTT desconectado; o cliente tentara reconectar.");
        break;
    case MQTT_EVENT_PUBLISHED:
        ESP_LOGD(TAG, "MQTT QoS1 confirmado: message_id=%d", event->msg_id);
        break;
    case MQTT_EVENT_ERROR:
        ESP_LOGW(TAG, "Evento de erro MQTT; confira o broker e a rede.");
        break;
    default:
        break;
    }
}

static void mqtt_start(void)
{
    uint8_t mac[6] = {0};
    ESP_ERROR_CHECK(esp_wifi_get_mac(WIFI_IF_STA, mac));
    snprintf(s_client_id, sizeof(s_client_id), "iot-sim-%02X%02X%02X",
             mac[3], mac[4], mac[5]);

    esp_mqtt_client_config_t cfg = {
        .broker.address.uri = CONFIG_SIM_MQTT_BROKER_URI,
        .credentials.client_id = s_client_id,
        .credentials.username = CONFIG_SIM_MQTT_USERNAME[0] ?
                                CONFIG_SIM_MQTT_USERNAME : NULL,
        .credentials.authentication.password = CONFIG_SIM_MQTT_PASSWORD[0] ?
                                                CONFIG_SIM_MQTT_PASSWORD : NULL,
    };

    s_mqtt_client = esp_mqtt_client_init(&cfg);
    if (!s_mqtt_client) {
        ESP_LOGE(TAG, "Nao foi possivel criar o cliente MQTT.");
        return;
    }

    ESP_ERROR_CHECK(esp_mqtt_client_register_event(
        s_mqtt_client, ESP_EVENT_ANY_ID, mqtt_event_handler, NULL));
    ESP_ERROR_CHECK(esp_mqtt_client_start(s_mqtt_client));
    ESP_LOGI(TAG, "MQTT inicializado. ClientId=%s Broker=%s",
             s_client_id, CONFIG_SIM_MQTT_BROKER_URI);
}

static void create_uuid_v4(char out[37])
{
    uint8_t bytes[16];
    esp_fill_random(bytes, sizeof(bytes));

    bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
    bytes[8] = (bytes[8] & 0x3f) | 0x80; // RFC 9562 variant

    snprintf(out, 37,
             "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-"
             "%02x%02x%02x%02x%02x%02x",
             bytes[0], bytes[1], bytes[2], bytes[3],
             bytes[4], bytes[5], bytes[6], bytes[7],
             bytes[8], bytes[9], bytes[10], bytes[11],
             bytes[12], bytes[13], bytes[14], bytes[15]);
}

static double random_value(double minimum, double maximum)
{
    double fraction = (double)(esp_random() % 10001) / 10000.0;
    return minimum + (maximum - minimum) * fraction;
}

static void publish_measurement(const simulated_sensor_t *sensor)
{
    char event_id[37];
    char timestamp[32];
    char payload[256];

    create_uuid_v4(event_id);

    time_t now = time(NULL);
    struct tm utc;
    if (gmtime_r(&now, &utc) == NULL ||
        strftime(timestamp, sizeof(timestamp), "%Y-%m-%dT%H:%M:%SZ", &utc) == 0) {
        ESP_LOGE(TAG, "Falha ao formatar horario UTC.");
        return;
    }

    double value = random_value(sensor->minimum, sensor->maximum);
    int written = snprintf(payload, sizeof(payload),
                           "{\"event_id\":\"%s\",\"sensor_id\":\"%s\","
                           "\"value\":%.2f,\"collected_at_utc\":\"%s\"}",
                           event_id, sensor->sensor_id, value, timestamp);

    if (written < 0 || written >= (int)sizeof(payload)) {
        ESP_LOGE(TAG, "Payload MQTT excedeu o tamanho do buffer.");
        return;
    }

    int message_id = esp_mqtt_client_publish(
        s_mqtt_client, sensor->topic, payload, 0, 1, 0);

    if (message_id < 0) {
        ESP_LOGW(TAG, "Falha ao enfileirar %s para publicacao MQTT.",
                 sensor->sensor_id);
        return;
    }

    ESP_LOGI(TAG, "MQTT enviado: topic=%s value=%.2f event_id=%s msg_id=%d",
             sensor->topic, value, event_id, message_id);
}

void app_main(void)
{
    if (strlen(CONFIG_SIM_WIFI_SSID) == 0 ||
        strlen(CONFIG_SIM_MQTT_BROKER_URI) == 0) {
        ESP_LOGE(TAG, "Configure Wi-Fi e MQTT em idf.py menuconfig.");
        return;
    }

    esp_err_t result = nvs_flash_init();
    if (result == ESP_ERR_NVS_NO_FREE_PAGES ||
        result == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        result = nvs_flash_init();
    }
    ESP_ERROR_CHECK(result);

    s_connection_events = xEventGroupCreate();
    if (!s_connection_events) {
        ESP_LOGE(TAG, "Nao foi possivel criar EventGroup.");
        return;
    }

    wifi_start();
    ESP_LOGI(TAG, "Aguardando conexao Wi-Fi...");
    xEventGroupWaitBits(s_connection_events, WIFI_READY_BIT,
                       pdFALSE, pdTRUE, portMAX_DELAY);

    synchronize_clock();
    mqtt_start();

    while (true) {
        EventBits_t state = xEventGroupGetBits(s_connection_events);

        if ((state & (WIFI_READY_BIT | MQTT_READY_BIT)) ==
                (WIFI_READY_BIT | MQTT_READY_BIT) &&
            clock_is_synchronized()) {
            for (size_t i = 0; i < sizeof(SENSORS) / sizeof(SENSORS[0]); i++) {
                publish_measurement(&SENSORS[i]);
            }
        } else {
            ESP_LOGW(TAG, "Aguardando Wi-Fi, MQTT e relogio validos.");
        }

        vTaskDelay(pdMS_TO_TICKS(CONFIG_SIM_PUBLISH_INTERVAL_SECONDS * 1000));
    }
}
