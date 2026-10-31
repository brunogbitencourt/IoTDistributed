import type { Sensor } from '../types/Sensor'

const sensorNames: Record<string, string> = {
  temperature: 'Temperatura', pressure: 'Pressão', level: 'Nível',
}
const sensorUnits: Record<string, string> = {
  celsius: '°C', pressure: 'bar', bar: 'bar', percent: '%',
}
export function sensorName(sensor: Sensor): string {
  return sensorNames[sensor.sensorType.toLowerCase()] ?? sensor.sensorType
}
export function sensorUnit(sensor: Sensor): string {
  return sensorUnits[sensor.unit.toLowerCase()] ?? sensor.unit
}
export function formatValue(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value)
    ? '—'
    : new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)
}
export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? '—'
    : date.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
export function formatTime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
export function isRecent(timestamp: string | undefined): boolean {
  if (!timestamp) return false
  const elapsed = Date.now() - new Date(timestamp).getTime()
  return elapsed >= 0 && elapsed <= 120_000
}
