# IoTDistributed.Simulator — Gerador MQTT para demonstração

Worker Service .NET 10, opcional, que publica medições artificiais nos **mesmos tópicos e no mesmo JSON do firmware ESP32**. Permite apresentar a aplicação sem hardware físico.

## Como funciona

A cada 5 segundos (configurável), o simulador publica um novo evento por sensor:

| Sensor | Tópico | Faixa |
| --- | --- | --- |
| TEMP-001 | `iot/sensors/TEMP-001/readings` | 20,00 a 35,00 °C |
| PRESS-001 | `iot/sensors/PRESS-001/readings` | 0,80 a 1,60 bar |
| LEVEL-001 | `iot/sensors/LEVEL-001/readings` | 15,00 a 95,00 % |

Cada evento tem um GUID v4 novo, horário UTC e QoS 1. O sinalizador MQTT `retain` permanece desativado, e o serviço não grava diretamente nos bancos. O fluxo de ingestão e replicação continua o mesmo dos eventos do ESP32.

Exemplo:

```json
{
  "event_id": "64823b46-8334-4d4f-a2df-787393885e33",
  "sensor_id": "TEMP-001",
  "value": 26.87,
  "collected_at_utc": "2026-10-31T19:30:00Z"
}
```

## Rodar pelo Docker Compose (recomendado)

Na pasta `src/IoTDistributed.Node`:

```powershell
# Aplicação normal: NÃO inicia o simulador
docker compose up -d --build

# Demonstração: ativa o simulador além dos serviços existentes
docker compose --profile demo up -d --build

# Acompanhar os eventos que estão sendo gerados
docker compose logs -f simulator

# Parar apenas o simulador, mantendo ESP32/broker/nós ativos
docker compose --profile demo stop simulator

# Remover o contêiner parado, se desejar:
docker compose --profile demo rm -f simulator
```

Frontend: http://localhost:5173

EMQX: http://localhost:18083

O serviço não inicia no comando `docker compose up -d --build` porque possui o perfil `demo`.

### Configurações

| Variável de ambiente | Padrão local | Valor no Compose |
| --- | --- | --- |
| `MQTT__Host` | `localhost` | `emqx` |
| `MQTT__Port` | `1883` | `1883` |
| `Simulator__IntervalSeconds` | `5` | `5` |
| `MQTT__Username` | vazio | não definido |
| `MQTT__Password` | vazio | não definido |

Os intervalos permitidos são de 1 a 3600 segundos.

## ESP32 real e simulador

São **produtores independentes**, com Client IDs MQTT distintos, portanto podem se conectar simultaneamente sem derrubar a sessão um do outro. No entanto, se ambos publicarem ao mesmo tempo, o sistema armazenará as medições de ambos para os mesmos sensores. Para uma demonstração limpa, pare o simulador antes de ligar o ESP32 real.

## Executar fora do Docker

O Worker utiliza `localhost:1883` por padrão (broker EMQX com porta exposta):

```powershell
dotnet run --project src/IoTDistributed.Simulator
```

Nesse comando, execute a partir da raiz do repositório. Não há necessidade de configurar banco nem API REST.

## Diagnóstico

```powershell
docker compose logs -f simulator
docker compose logs --tail 40 node-1 node-2 node-3
```

A primeira publicação ocorre após o primeiro intervalo configurado, para permitir que os nós terminem suas inscrições MQTT. Se o broker estiver indisponível, o Worker registra a falha e tenta novamente a cada ciclo.
