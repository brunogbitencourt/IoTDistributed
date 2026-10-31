import type { Sensor } from '../types/Sensor'
import type { SensorReading } from '../types/SensorReading'
import Icon from './Icon'
import { formatTime, formatValue, isRecent, sensorName, sensorUnit } from '../utils/format'

type Props = {
  sensor: Sensor
  reading: SensorReading | undefined
  selected: boolean
  error?: string
  onSelect: () => void
}

export default function SensorCard({ sensor, reading, selected, error, onSelect }: Props) {
  const name = sensorName(sensor)
  const kind = sensor.sensorType.toLowerCase()
  const icon = kind === 'temperature' ? 'thermometer' : kind === 'pressure' ? 'gauge' : 'waves'
  const fresh = isRecent(reading?.collectedAtUtc)
  const status = error ? 'Falha na consulta' : !reading ? 'Sem leituras' : fresh ? 'Leitura recente' : 'Leitura histórica'

  return (
    <button type="button" className={`sensor-card ${selected ? 'sensor-card--selected' : ''}`} onClick={onSelect} aria-pressed={selected} aria-label={`Selecionar sensor ${name}`}>
      <span className="sensor-card-top">
        <span className={`sensor-icon sensor-icon--${kind}`}><Icon name={icon} size={23}/></span>
        <span className="sensor-node">NODE {sensor.ownerNodeId.toString().padStart(2,'0')}</span>
      </span>
      <span className="sensor-name">{name}</span>
      <span className="sensor-reading"><strong>{formatValue(reading?.value)}</strong><small>{sensorUnit(sensor)}</small></span>
      <span className="sensor-footer">
        <span className={`dot-status ${error ? 'dot-status--error' : fresh ? 'dot-status--success' : 'dot-status--muted'}`}><span/>{status}</span>
        <span className="sensor-time">{formatTime(reading?.collectedAtUtc)}</span>
      </span>
    </button>
  )
}
