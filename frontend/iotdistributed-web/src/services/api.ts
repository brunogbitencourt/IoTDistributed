import type { Sensor } from '../types/Sensor'
import type { SensorReading } from '../types/SensorReading'
import type { DatabaseStatus } from '../types/Cluster'

async function getJson<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(path, { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ao consultar ${path.split('?')[0]}`)
  }
  return (await response.json()) as T
}

export function fetchSensors(signal: AbortSignal) {
  return getJson<Sensor[]>('/api/sensors', signal)
}

export function fetchDatabases(signal: AbortSignal) {
  return getJson<DatabaseStatus[]>('/api/cluster/databases', signal)
}

export function fetchSensorReadings(sensor: Sensor, signal: AbortSignal) {
  const query = new URLSearchParams({
    node: String(sensor.ownerNodeId),
    sensorId: sensor.sensorId,
    limit: '100',
  })
  return getJson<SensorReading[]>(`/api/readings?${query.toString()}`, signal)
}
