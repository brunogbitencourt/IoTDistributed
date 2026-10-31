import { useCallback, useEffect, useState } from 'react'
import { fetchDatabases, fetchSensorReadings, fetchSensors } from '../services/api'
import type { Sensor } from '../types/Sensor'
import type { SensorReading } from '../types/SensorReading'
import type { DatabaseStatus } from '../types/Cluster'

const POLL_INTERVAL_MS = 10_000

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : 'Erro inesperado ao consultar a API'
}

export function useDashboard() {
  const [sensors, setSensors] = useState<Sensor[]>([])
  const [sensorsLoading, setSensorsLoading] = useState(true)
  const [sensorsError, setSensorsError] = useState<string | null>(null)
  const [sensorFetchKey, setSensorFetchKey] = useState(0)

  const [readingsBySensor, setReadingsBySensor] = useState<Record<string, SensorReading[]>>({})
  const [readingErrors, setReadingErrors] = useState<Record<string, string>>({})
  const [databases, setDatabases] = useState<DatabaseStatus[]>([])
  const [databaseError, setDatabaseError] = useState<string | null>(null)
  const [lastCheckedAt, setLastCheckedAt] = useState<Date | null>(null)
  const [updating, setUpdating] = useState(false)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [refreshKey, setRefreshKey] = useState(0)

  const refresh = useCallback(() => {
    setRefreshKey(value => value + 1)
    if (sensorsError) setSensorFetchKey(value => value + 1)
  }, [sensorsError])

  useEffect(() => {
    const controller = new AbortController()
    async function loadSensors() {
      setSensorsLoading(true)
      setSensorsError(null)
      try {
        const data = await fetchSensors(controller.signal)
        if (!controller.signal.aborted) setSensors(data)
      } catch (error) {
        if (!controller.signal.aborted) setSensorsError(describeError(error))
      } finally {
        if (!controller.signal.aborted) setSensorsLoading(false)
      }
    }
    void loadSensors()
    return () => controller.abort()
  }, [sensorFetchKey])

  useEffect(() => {
    if (sensorsLoading || sensorsError) return

    const controller = new AbortController()
    let inProgress = false

    async function loadSnapshot() {
      if (inProgress || controller.signal.aborted) return
      inProgress = true
      setUpdating(true)
      try {
        const [databaseResult, ...sensorResults] = await Promise.allSettled([
          fetchDatabases(controller.signal),
          ...sensors.map(sensor => fetchSensorReadings(sensor, controller.signal)),
        ])
        if (controller.signal.aborted) return

        if (databaseResult.status === 'fulfilled') {
          setDatabases(databaseResult.value as DatabaseStatus[])
          setDatabaseError(null)
        } else {
          setDatabaseError(describeError(databaseResult.reason))
        }

        const successfulReadings: Record<string, SensorReading[]> = {}
        const failedReadings: Record<string, string> = {}

        sensorResults.forEach((result, index) => {
          const sensor = sensors[index]
          if (result.status === 'fulfilled') {
            successfulReadings[sensor.sensorId] = result.value as SensorReading[]
          } else {
            failedReadings[sensor.sensorId] = describeError(result.reason)
          }
        })

        // Preserve the last successful data for failed sensors; display the error separately.
        setReadingsBySensor(previous => ({ ...previous, ...successfulReadings }))
        setReadingErrors(failedReadings)
        setLastCheckedAt(new Date())
      } finally {
        inProgress = false
        if (!controller.signal.aborted) setUpdating(false)
      }
    }

    void loadSnapshot()
    const timer = autoRefresh ? window.setInterval(() => void loadSnapshot(), POLL_INTERVAL_MS) : undefined

    return () => {
      if (timer !== undefined) window.clearInterval(timer)
      controller.abort()
    }
  }, [sensors, sensorsLoading, sensorsError, autoRefresh, refreshKey])

  return {
    sensors, sensorsLoading, sensorsError, readingsBySensor, readingErrors,
    databases, databaseError, lastCheckedAt, updating,
    autoRefresh, setAutoRefresh, refresh, refreshKey, intervalSeconds: POLL_INTERVAL_MS / 1000,
  }
}
