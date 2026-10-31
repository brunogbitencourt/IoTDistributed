
type SensorCardProps = {
  name: string
  sensorId: string
  unit: string
  nodeId: number
  value: number | null
  selected: boolean
  onSelect: () => void
}

function SensorCard({
  name,
  sensorId,
  unit,
  nodeId,
  value,
  selected,
  onSelect
}: SensorCardProps) {
  return (
    <article
      className={`sensor-card ${selected ? 'sensor-card--selected' : ''}`}
    >
      <h2>{name}</h2>

      <p className="sensor-value">
        {value === null ? '--' : value.toFixed(2)}
        <span> {unit}</span>
      </p>

      <p>{sensorId}</p>

      <small>NODE {nodeId}</small>

      <div>
        <button type="button" onClick={onSelect}>
          {selected ? 'Selecionado' : 'Selecionar sensor'}
        </button>
      </div>
    </article>
  )
}

export default SensorCard
