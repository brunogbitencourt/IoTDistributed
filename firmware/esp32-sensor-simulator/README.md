# Simulador de sensores ESP32 (ESP-IDF)

Firmware para um ESP32 que gera leituras **simuladas** de temperatura, pressao e nivel e publica um JSON por sensor em um broker MQTT (EMQX). O firmware **nao necessita de sensores fisicos**.

Compatibilidade prevista: ESP-IDF 6.1, alvo `esp32`, componente `espressif/mqtt` 1.1.0.

## Topicos e intervalos

| Sensor | Topico MQTT | Intervalo de valores |
|---|---|---|
| TEMP-001 | `iot/sensors/TEMP-001/readings` | 20.00 a 35.00 C |
| PRESS-001 | `iot/sensors/PRESS-001/readings` | 0.80 a 1.60 bar |
| LEVEL-001 | `iot/sensors/LEVEL-001/readings` | 15.00 a 95.00 % |

O intervalo entre lotes e configuravel (padrao: **10 segundos**). Cada lote publica tres mensagens, uma em cada topico, com **QoS 1** e **retain=false**.

Exemplo de mensagem:

```json
{
  "event_id": "f6d87657-5b0c-4c47-b9a9-fca7b8bb2345",
  "sensor_id": "TEMP-001",
  "value": 28.50,
  "collected_at_utc": "2026-10-24T21:00:00Z"
}
```

O ESP32 gera UUID v4 com `esp_fill_random()`. Cada **nova** leitura recebe um UUID novo. A data UTC vem do servidor NTP configurado. Sem hora sincronizada, o firmware nao publica datas invalidas.

## Pre-requisito importante: broker acessivel pelo ESP32

O endereco `emqx:1883` so funciona **dentro da rede Docker**. O endereco `localhost:1883` so funciona **no proprio computador**. O ESP32 precisa do endereco IPv4 do Windows na rede Wi-Fi.

1. No Windows, execute `ipconfig` e encontre o IPv4 do adaptador Wi-Fi em uso (ex.: `192.168.1.100`).
2. No `src/IoTDistributed.Node/compose.yml`, altere **somente a publicacao MQTT do EMQX** de `127.0.0.1:1883:1883` para `1883:1883` (ou use `IP_DO_PC:1883:1883`).
3. Preserve o mapeamento do Dashboard em `127.0.0.1:18083:18083`.
4. Recrie o broker com `docker compose up -d --force-recreate emqx` a partir da pasta do Compose.
5. Permita conexoes de entrada **TCP 1883** no Firewall do Windows **apenas em rede privada confiavel**. Evite expor essa porta na Internet.
6. Conecte ESP32 e computador a uma rede com comunicacao local liberada; no ESP32 classico, use Wi-Fi 2.4 GHz.
7. Em menuconfig, use o broker `mqtt://192.168.1.100:1883` substituindo pelo IP correto do seu PC.

A configuracao `mqtt://` sem TLS e adequada somente para laboratorio confiavel. Para producao, configurar autenticacao e TLS.

## Como abrir, configurar e gravar (Windows / VS Code ESP-IDF)

Abra a pasta `firmware/esp32-sensor-simulator` no VS Code com a extensao ESP-IDF, ou use o terminal ESP-IDF inicializado:

```powershell
cd C:\Projetos\IotDistributed\firmware\esp32-sensor-simulator
idf.py --version
idf.py set-target esp32
idf.py menuconfig
```

Em **IoT Sensor Simulator** configure:

- `Wi-Fi SSID` e `Wi-Fi password`.
- `MQTT broker URI`: `mqtt://IP_DO_PC:1883`.
- `MQTT username/password`: somente se houver autenticacao configurada no EMQX.
- `Servidor NTP` (padrao `pool.ntp.org`), acessivel pela rede.
- `Intervalo entre lotes` (padrao: 10 segundos).

Compile e grave:

```powershell
idf.py build
idf.py -p COM4 flash monitor
```

Troque `COM4` pela porta serial real do dispositivo. Caso a porta esteja ocupada, feche outros monitores seriais (incluindo outra instancia do VS Code). Para sair do monitor: `Ctrl + ]`.

O ESP-IDF Component Manager baixa `espressif/mqtt` automaticamente, conforme `main/idf_component.yml`; nao precisa copiar bibliotecas manualmente.

**Seguranca:** o arquivo local `sdkconfig` contem o SSID, senha e broker e esta ignorado pelo Git. Nao envie `sdkconfig`, firmware binario com credenciais, ou capturas das configuracoes.

## Como validar

1. Acompanhe o monitor serial do ESP32: deve mostrar conexao Wi-Fi, sincronizacao NTP, conexao MQTT e publicacoes dos tres sensores.
2. No Dashboard do EMQX, confira o cliente `iot-sim-XXXXXX` conectado.
3. Acompanhe os tres consumidores MQTT/.NET na pasta do Compose:

   ```powershell
   docker compose logs -f node-1 node-2 node-3
   ```

4. Consulte a replicacao em qualquer um dos tres bancos:

   ```powershell
   docker compose exec db1 psql -U iot -d iotdb -c "SELECT sensor_id, COUNT(*) FROM sensor_readings WHERE created_at > NOW() - INTERVAL '3 minutes' GROUP BY sensor_id ORDER BY sensor_id;"
   ```

   Esperado: `TEMP-001`, `PRESS-001` e `LEVEL-001` com novas leituras em cada banco, pois as medicoes recebidas pelo MQTT sao replicadas via gRPC.

## Alcance desta versao

- Dados **simulados**; nenhum sensor fisico e lido.
- UUID novo por amostra e QoS 1 para o envio; o retorno de `esp_mqtt_client_publish` significa que o cliente aceitou/enfileirou a publicacao, **nao** que os tres PostgreSQL confirmaram gravacao.
- O ESP-MQTT faz reconexoes; mensagens geradas enquanto o dispositivo estiver desconectado **nao** ficam guardadas de forma duravel.
- Um sensor real exigiria substituir `random_value` pela leitura do hardware, mantendo o topico e o JSON atuais.
