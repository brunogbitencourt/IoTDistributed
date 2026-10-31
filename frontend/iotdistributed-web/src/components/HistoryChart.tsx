import type { SensorReading } from '../types/SensorReading'
import { formatTime, formatValue } from '../utils/format'

type Props = { readings: SensorReading[]; unit: string; limit: number }

export default function HistoryChart({ readings, unit, limit }: Props) {
  const data = [...readings]
    .filter(item => Number.isFinite(item.value) && Number.isFinite(Date.parse(item.collectedAtUtc)))
    .sort((a, b) => Date.parse(a.collectedAtUtc) - Date.parse(b.collectedAtUtc))
    .slice(-limit)

  if (data.length === 0) {
    return <div className="chart-empty"><span className="chart-empty-icon">∿</span><strong>Sem medições para exibir</strong><span>As leituras aparecerão quando os sensores enviarem dados.</span></div>
  }

  const width = 900, height = 286
  const left = 60, right = 24, top = 25, bottom = 40
  const plotWidth = width - left - right
  const plotHeight = height - top - bottom
  const values = data.map(item => item.value)
  const minValue = Math.min(...values)
  const maxValue = Math.max(...values)
  const padding = Math.max((maxValue - minValue) * 0.19, Math.abs(maxValue) * 0.015, 0.15)
  const minY = minValue - padding
  const maxY = maxValue + padding
  const t0 = Date.parse(data[0].collectedAtUtc)
  const t1 = Date.parse(data[data.length - 1].collectedAtUtc)
  const getX = (item: SensorReading, index: number) => t0 === t1
    ? left + (data.length === 1 ? plotWidth / 2 : (index / (data.length - 1)) * plotWidth)
    : left + ((Date.parse(item.collectedAtUtc) - t0) / (t1 - t0)) * plotWidth
  const getY = (value: number) => top + ((maxY - value) / (maxY - minY)) * plotHeight
  const points = data.map((item, index) => ({ x: getX(item, index), y: getY(item.value), item }))
  const path = points.map((p, index) => `${index === 0 ? 'M' : 'L'} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(' ')
  const area = points.length ? `${path} L ${points[points.length - 1].x} ${top + plotHeight} L ${points[0].x} ${top + plotHeight} Z` : ''
  const latest = data[data.length - 1]
  const yTicks = Array.from({ length: 4 }, (_, index) => minY + ((maxY - minY) * index) / 3)
  const xTickIndexes = [...new Set([0, Math.floor((data.length - 1) / 2), data.length - 1])]

  return (
    <div className="history-chart">
      <div className="chart-metrics">
        <div><span>Última leitura</span><strong>{formatValue(latest.value)} <small>{unit}</small></strong></div>
        <div><span>Mínimo</span><strong>{formatValue(minValue)} <small>{unit}</small></strong></div>
        <div><span>Máximo</span><strong>{formatValue(maxValue)} <small>{unit}</small></strong></div>
        <div><span>Amostras</span><strong>{data.length}</strong></div>
      </div>
      <svg className="chart-svg" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Histórico das últimas ${data.length} medições, mínimo ${formatValue(minValue)} e máximo ${formatValue(maxValue)} ${unit}`} preserveAspectRatio="xMidYMid meet">
        <defs>
          <linearGradient id="historyArea" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#5d83ff" stopOpacity=".30"/>
            <stop offset="100%" stopColor="#5d83ff" stopOpacity="0"/>
          </linearGradient>
        </defs>
        {yTicks.map((tick, index) => (
          <g key={index}>
            <line x1={left} y1={getY(tick)} x2={width - right} y2={getY(tick)} className="chart-gridline"/>
            <text x={left - 12} y={getY(tick) + 4} textAnchor="end" className="chart-label">{Number(tick.toFixed(2)).toLocaleString('pt-BR')}</text>
          </g>
        ))}
        {xTickIndexes.map(index => (
          <text key={index} x={points[index].x} y={height - 11} textAnchor={index === 0 ? 'start' : index === data.length - 1 ? 'end' : 'middle'} className="chart-label">
            {formatTime(data[index].collectedAtUtc)}
          </text>
        ))}
        <path d={area} fill="url(#historyArea)"/>
        <path d={path} className="chart-line"/>
        {points.length <= 35 && points.map((point, index) => (
          <circle key={index} cx={point.x} cy={point.y} r="3.5" className="chart-point">
            <title>{`${formatValue(point.item.value)} ${unit} • ${formatTime(point.item.collectedAtUtc)}`}</title>
          </circle>
        ))}
        <circle cx={points[points.length - 1].x} cy={points[points.length - 1].y} r="5" className="chart-last-point">
          <title>{`Última leitura: ${formatValue(latest.value)} ${unit}`}</title>
        </circle>
      </svg>
    </div>
  )
}
