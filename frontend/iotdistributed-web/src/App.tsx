
import { useEffect, useState } from 'react'
import SensorCard from './components/SensorCard'
import type { Sensor } from './types/Sensor'
import type { SensorReading } from './types/SensorReading'
import './App.css'

const sensorNames: Record<string, string> = {
  temperature: 'Temperatura',
  pressure: 'Pressão',
  level: 'Nível',
}

const sensorUnits: Record<string, string> = {
  celsius: '°C',
  bar: 'bar',
  percent: '%',
}

function App() {
  const [sensors, setSensors] = useState<Sensor[]>([])

  const [selectedSensorId, setSelectedSensorId] =
    useState<string | null>(null)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Última medição de cada sensor
  const [latestReadings, setLatestReadings] =
    useState<Record<string, SensorReading | null>>({})

  const [readingsLoading, setReadingsLoading] = useState(false)
  const [readingsError, setReadingsError] =
    useState<string | null>(null)

  // Efeito 1: carregar sensores cadastrados
  useEffect(() => {
    const controller = new AbortController()

    async function loadSensors() {
      try {
        const response = await fetch('/api/sensors', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(`Erro HTTP ${response.status}`)
        }

        const data = (await response.json()) as Sensor[]

        if (controller.signal.aborted) return

        setSensors(data)

        setSelectedSensorId(current =>
          current ?? data[0]?.sensorId ?? null
        )
      } catch (err) {
        if (controller.signal.aborted) return

        setError(
          err instanceof Error
            ? err.message
            : 'Erro desconhecido'
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadSensors()

    return () => controller.abort()
  }, [])

  // Efeito 2: carregar a última medição de cada sensor
  useEffect(() => {
    if (sensors.length === 0) return

    const controller = new AbortController()

    async function loadLatestReadings() {
      setReadingsLoading(true)
      setReadingsError(null)

      try {
        const entries = await Promise.all(
          sensors.map(async sensor => {
            const params = new URLSearchParams({
              node: String(sensor.ownerNodeId),
              sensorId: sensor.sensorId,
              limit: '1',
            })

            const response = await fetch(
              `/api/readings?${params}`,
              { signal: controller.signal }
            )

            if (!response.ok) {
              throw new Error(
                `Erro ao consultar ${sensor.sensorId}: HTTP ${response.status}`
              )
            }

            const readings =
              (await response.json()) as SensorReading[]

            return [
              sensor.sensorId,
              readings[0] ?? null,
            ] as const
          })
        )

        if (controller.signal.aborted) return

        setLatestReadings(Object.fromEntries(entries))
      } catch (err) {
        if (controller.signal.aborted) return

        setReadingsError(
          err instanceof Error
            ? err.message
            : 'Erro ao consultar medições'
        )
      } finally {
        if (!controller.signal.aborted) {
          setReadingsLoading(false)
        }
      }
    }

    void loadLatestReadings()

    return () => controller.abort()
  }, [sensors])

  return (
    <main className="dashboard">
      <header>
        <h1>IoTDistributed Dashboard</h1>
        <p>Monitoramento de sensores distribuídos</p>

        <p>
          Sensor selecionado: {selectedSensorId ?? 'Nenhum'}
        </p>
      </header>

      {loading && <p>Carregando sensores...</p>}

      {error && (
        <p role="alert">Erro ao carregar sensores: {error}</p>
      )}

      {readingsLoading && <p>Carregando medições...</p>}

      {readingsError && (
        <p role="alert">{readingsError}</p>
      )}

      {!loading && !error && sensors.length === 0 && (
        <p>Nenhum sensor cadastrado.</p>
      )}

      <section className="sensor-grid">
        {sensors.map(sensor => (
          <SensorCard
            key={sensor.sensorId}
            name={
              sensorNames[sensor.sensorType] ??
              sensor.sensorType
            }
            sensorId={sensor.sensorId}
            unit={sensorUnits[sensor.unit] ?? sensor.unit}
            nodeId={sensor.ownerNodeId}
            value={
              latestReadings[sensor.sensorId]?.value ?? null
            }
            selected={selectedSensorId === sensor.sensorId}
            onSelect={() =>
              setSelectedSensorId(sensor.sensorId)
            }
          />
        ))}
      </section>
    </main>
  )
}

export default App
