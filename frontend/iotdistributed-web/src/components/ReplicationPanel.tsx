import { useEffect, useState } from 'react'
import Icon from './Icon'
import { compareReplication } from '../services/replication'
import type { DivergentEvent, ReplicationComparison } from '../types/Replication'
import { formatDate } from '../utils/format'

type Props = { autoRefresh: boolean; refreshKey: number }
type Filter = 'all' | 'exclusive' | 'db1' | 'db2' | 'db3'
const PAGE_SIZE = 25
const PERIODS = [5, 15, 30, 60] as const

function stateForNode(event: DivergentEvent, nodeId: number, onlineNodes: number[]): 'present' | 'missing' | 'unavailable' {
  if (!onlineNodes.includes(nodeId)) return 'unavailable'
  return event.missingInNodes.includes(nodeId) ? 'missing' : 'present'
}

function availableNodesFor(event: DivergentEvent, onlineNodes: number[]) {
  return onlineNodes.filter(node => !event.missingInNodes.includes(node))
}

function exportDivergences(rows: DivergentEvent[], onlineNodes: number[]) {
  const values = [
    ['Event ID', 'DB1', 'DB2', 'DB3', 'Presente nos bancos', 'Ausente nos bancos'],
    ...rows.map(item => [
      item.eventId,
      ...[1, 2, 3].map(nodeId => stateForNode(item, nodeId, onlineNodes)),
      availableNodesFor(item, onlineNodes).join(', '),
      item.missingInNodes.join(', '),
    ]),
  ]
  const csv = '\uFEFF' + values.map(row => row.map(value => `"${value.replaceAll('"', '""')}"`).join(';')).join('\r\n')
  const link = document.createElement('a')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  link.href = url
  link.download = 'iotdistributed-divergencias.csv'
  link.click()
  URL.revokeObjectURL(url)
}

function DatabasePresence({ nodeId, status }: { nodeId: number; status: 'present' | 'missing' | 'unavailable' }) {
  const label = status === 'present' ? 'Presente' : status === 'missing' ? 'Ausente' : 'Não consultado'
  return <span className={`replica-state replica-state--${status}`} title={`DB${nodeId}: ${label}`} aria-label={`DB${nodeId}: ${label}`}>
    {status === 'present' ? <Icon name="check" size={15}/> : status === 'missing' ? <Icon name="alert" size={15}/> : <span>?</span>}
    <span>{label}</span>
  </span>
}

export default function ReplicationPanel({ autoRefresh, refreshKey }: Props) {
  const [minutes, setMinutes] = useState<number>(15)
  const [snapshot, setSnapshot] = useState<ReplicationComparison | null>(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [localRefresh, setLocalRefresh] = useState(0)
  const [filter, setFilter] = useState<Filter>('all')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  useEffect(() => {
    const controller = new AbortController()
    let inProgress = false

    async function refreshComparison() {
      if (inProgress || controller.signal.aborted) return
      inProgress = true
      setRefreshing(true)
      setError(null)
      try {
        const data = await compareReplication(minutes, controller.signal)
        if (controller.signal.aborted) return
        setSnapshot(data)
      } catch (cause) {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : 'Não foi possível comparar os bancos.')
        }
      } finally {
        inProgress = false
        if (!controller.signal.aborted) {
          setLoading(false)
          setRefreshing(false)
        }
      }
    }

    void refreshComparison()
    // A comparação envolve varredura de GUIDs nos três bancos: período maior reduz a frequência.
    const timer = autoRefresh ? window.setInterval(() => void refreshComparison(), 30_000) : undefined
    return () => {
      if (timer !== undefined) window.clearInterval(timer)
      controller.abort()
    }
  }, [minutes, localRefresh, refreshKey, autoRefresh])

  const onlineNodes = snapshot?.databases
    .filter(database => database.status.toLowerCase() === 'online')
    .map(database => database.nodeId) ?? []
  const availableCount = onlineNodes.length
  const divergences = snapshot?.divergences ?? []
  const exclusive = availableCount > 1
    ? divergences.filter(item => availableNodesFor(item, onlineNodes).length === 1)
    : []

  // Cada GUID da união está presente ou ausente em cada banco acessível.
  // Por isso: |união| = (soma dos eventos presentes + ausências) / bancos acessíveis.
  const sumCounts = snapshot?.databases.reduce((sum, database) => sum + (database.eventCount ?? 0), 0) ?? 0
  const missingOccurrences = divergences.reduce((sum, event) => sum + event.missingInNodes.length, 0)
  const distinctUnion = availableCount > 0
    ? Math.round((sumCounts + missingOccurrences) / availableCount)
    : null
  const hasFullComparison = snapshot?.allDatabasesAvailable === true && availableCount === 3

  const filtered = divergences.filter(item => {
    const matchesSearch = item.eventId.toLowerCase().includes(search.trim().toLowerCase())
    if (!matchesSearch) return false
    if (filter === 'exclusive') return availableNodesFor(item, onlineNodes).length === 1 && availableCount > 1
    if (filter.startsWith('db')) return item.missingInNodes.includes(Number(filter.slice(2)))
    return true
  })
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const currentPage = Math.min(page, totalPages)
  const visibleRows = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE)

  return (
    <div className="replication-layout">
      <div className="readings-intro">
        <span className="info-icon"><Icon name="layers" size={20}/></span>
        <div>
          <strong>Comparação por identificador único (GUID)</strong>
          <p>A API consulta os três PostgreSQL e compara os conjuntos de event_id pela data de coleta. Um evento divergente está presente em alguns bancos, mas ausente em outros.</p>
        </div>
      </div>

      <div className="replication-toolbar">
        <div><span className="replication-control-label">JANELA DE ANÁLISE</span>
          <div className="period-options" role="group" aria-label="Período da comparação">
            {PERIODS.map(value => <button key={value} type="button" aria-pressed={minutes === value} className={minutes === value ? 'period-option period-option--active' : 'period-option'} onClick={() => { setMinutes(value); setPage(1); setLoading(!snapshot) }}>{value} min</button>)}
          </div>
        </div>
        <div className="replication-refresh">
          <span className="replication-control-label">{autoRefresh ? 'VERIFICAÇÃO AUTOMÁTICA · 30S' : 'VERIFICAÇÃO PAUSADA'}</span>
          <button className="export-button" type="button" onClick={() => setLocalRefresh(key => key + 1)} disabled={refreshing}>
            <Icon name="refresh" size={16} className={refreshing ? 'spin' : ''}/> Comparar agora
          </button>
        </div>
      </div>

      {error && <div className="alert-banner" role="alert"><Icon name="alert" size={20}/><div><strong>Falha ao comparar as réplicas</strong><span>{error}{snapshot ? ' · Exibindo a última comparação concluída.' : ''}</span></div></div>}
      {loading && !snapshot && <p className="loading-line" role="status">Comparando os GUIDs nos bancos PostgreSQL...</p>}

      {snapshot && <>
        {!hasFullComparison && <div className="replication-notice" role="status"><Icon name="alert" size={19}/><div><strong>Comparação parcial — bancos indisponíveis</strong><p>Somente {availableCount} dos 3 bancos responderam. O sistema não consegue confirmar a presença ou ausência de GUIDs nos bancos indisponíveis. Zero divergências aqui não significa que as três réplicas estejam iguais.</p></div></div>}

        <section className="replication-summary" aria-label="Resumo de consistência">
          <div className="replication-stat"><span>GUIDs distintos encontrados</span><strong>{distinctUnion === null ? '—' : distinctUnion.toLocaleString('pt-BR')}</strong><small>União dos bancos acessíveis</small></div>
          <div className="replication-stat"><span>Eventos divergentes</span><strong className={divergences.length ? 'replication-danger' : 'replication-success'}>{divergences.length.toLocaleString('pt-BR')}</strong><small>Ausentes em pelo menos 1 réplica acessível</small></div>
          <div className="replication-stat"><span>Presentes em apenas 1 banco</span><strong className={exclusive.length ? 'replication-amber' : ''}>{availableCount < 2 ? '—' : exclusive.length.toLocaleString('pt-BR')}</strong><small>Entre os bancos que responderam</small></div>
          <div className="replication-stat"><span>Comparação dos bancos</span><strong>{availableCount} <em>/ 3</em></strong><small>{hasFullComparison ? 'Todos os bancos consultados' : 'Resultado parcial'}</small></div>
        </section>

        <section className="panel replication-databases">
          <div className="panel-heading"><div><div className="eyebrow">DISTRIBUIÇÃO DE EVENTOS</div><h2>GUIDs por PostgreSQL</h2><p>Contagem de identificadores distintos dentro da janela selecionada</p></div></div>
          <div className="replication-db-list">
            {[1, 2, 3].map(nodeId => {
              const database = snapshot.databases.find(item => item.nodeId === nodeId)
              const online = database?.status.toLowerCase() === 'online'
              const count = online ? database?.eventCount ?? 0 : null
              const missing = divergences.filter(item => item.missingInNodes.includes(nodeId)).length
              const percent = distinctUnion && count !== null ? Math.min(100, (count / distinctUnion) * 100) : 0
              return <div className="replication-db-row" key={nodeId}>
                <div className="replication-db-name"><span className="replication-db-icon"><Icon name="database" size={20}/></span><div><strong>PostgreSQL {nodeId}</strong><small>{online ? 'Consulta concluída' : 'Não foi possível consultar'}</small></div></div>
                <div className="replication-db-progress"><div className="replication-track"><span style={{ width: `${percent}%` }} /></div><small>{online ? `${percent.toFixed(1)}% dos GUIDs observados` : 'Sem informação'}</small></div>
                <div className="replication-db-count"><strong>{count === null ? '—' : count.toLocaleString('pt-BR')}</strong><small>{online ? `${missing} ausentes` : 'Indisponível'}</small></div>
              </div>
            })}
          </div>
        </section>

        <section className="panel replication-events">
          <div className="panel-heading replication-events-heading">
            <div><div className="eyebrow">CONSISTÊNCIA ENTRE RÉPLICAS</div><h2>Eventos não replicados integralmente</h2><p>GUIDs presentes em um ou dois bancos e ausentes em pelo menos outro banco acessível</p></div>
            <button type="button" className="export-button" disabled={!filtered.length} onClick={() => exportDivergences(filtered, onlineNodes)}><Icon name="download" size={16}/> Exportar CSV</button>
          </div>
          <div className="replication-filterbar">
            <div className="replication-filter-group">
              <button type="button" className={filter === 'all' ? 'replication-filter--active' : ''} aria-pressed={filter === 'all'} onClick={() => { setFilter('all'); setPage(1) }}>Todas ({divergences.length})</button>
              <button type="button" className={filter === 'exclusive' ? 'replication-filter--active' : ''} aria-pressed={filter === 'exclusive'} onClick={() => { setFilter('exclusive'); setPage(1) }}>Somente exclusivos ({exclusive.length})</button>
              {[1, 2, 3].map(nodeId => <button type="button" key={nodeId} className={filter === `db${nodeId}` ? 'replication-filter--active' : ''} aria-pressed={filter === `db${nodeId}`} onClick={() => { setFilter(`db${nodeId}` as Filter); setPage(1) }}>Ausentes DB{nodeId}</button>)}
            </div>
            <label className="sr-only" htmlFor="replication-search">Pesquisar GUID</label>
            <input id="replication-search" value={search} onChange={event => { setSearch(event.target.value); setPage(1) }} placeholder="Buscar por GUID..." className="replication-search"/>
          </div>
          <div className="table-scroll">
            <table className="readings-table replication-table">
              <thead><tr><th>Identificador do evento (GUID)</th><th>DB1</th><th>DB2</th><th>DB3</th><th>Classificação</th></tr></thead>
              <tbody>
                {visibleRows.map(item => {
                  const presentCount = availableNodesFor(item, onlineNodes).length
                  return <tr key={item.eventId}>
                    <td className="replication-event-id"><code title={item.eventId}>{item.eventId}</code></td>
                    {[1,2,3].map(nodeId => <td key={nodeId}><DatabasePresence nodeId={nodeId} status={stateForNode(item, nodeId, onlineNodes)}/></td>)}
                    <td><span className={presentCount === 1 ? 'replication-type replication-type--exclusive' : 'replication-type'}>{presentCount === 1 ? 'Presente em 1 banco' : `Presente em ${presentCount} bancos`}</span></td>
                  </tr>
                })}
                {!visibleRows.length && <tr><td colSpan={5}>
                  <div className="replication-empty">
                    <Icon name={divergences.length === 0 && hasFullComparison ? 'check' : 'activity'} size={26}/>
                    <strong>{!divergences.length && hasFullComparison ? 'Nenhuma divergência encontrada' : !divergences.length ? 'Nenhuma divergência detectada nos bancos acessíveis' : 'Nenhum GUID corresponde aos filtros'}</strong>
                    <span>{!divergences.length ? `A análise cobre de ${formatDate(snapshot.fromUtc)} até ${formatDate(snapshot.toUtc)}. Isso não garante ausência de divergências fora desse período.` : 'Altere o filtro ou o termo de busca para visualizar outros registros.'}</span>
                  </div>
                </td></tr>}
              </tbody>
            </table>
          </div>
          {filtered.length > PAGE_SIZE && <div className="replication-pagination">
            <span>Exibindo {(currentPage - 1) * PAGE_SIZE + 1}–{Math.min(currentPage * PAGE_SIZE, filtered.length)} de {filtered.length}</span>
            <div><button type="button" disabled={currentPage === 1} onClick={() => setPage(value => Math.max(1, value - 1))}>Anterior</button><span>{currentPage} / {totalPages}</span><button type="button" disabled={currentPage === totalPages} onClick={() => setPage(value => Math.min(totalPages, value + 1))}>Próxima</button></div>
          </div>}
        </section>

        <div className="replication-footnote">
          <Icon name="clock" size={16}/>
          <span>Janela consultada: <strong>{formatDate(snapshot.fromUtc)} até {formatDate(snapshot.toUtc)}</strong> (horário local). Cada comparação verifica GUIDs distintos em cada banco, com base em <code>collected_at</code>. Não identifica linhas duplicadas dentro de um mesmo banco.</span>
        </div>
      </>}
    </div>
  )
}
