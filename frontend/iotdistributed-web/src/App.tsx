import { useEffect, useState } from 'react'
import Icon from './components/Icon'
import type { IconName } from './components/Icon'
import SensorCard from './components/SensorCard'
import HistoryChart from './components/HistoryChart'
import ReplicationPanel from './components/ReplicationPanel'
import { useDashboard } from './hooks/useDashboard'
import type { Sensor } from './types/Sensor'
import type { SensorReading } from './types/SensorReading'
import { formatDate, formatTime, formatValue, sensorName, sensorUnit } from './utils/format'
import './App.css'

type Page = 'overview' | 'readings' | 'cluster' | 'replication'

const navigation: { id: Page; label: string; icon: IconName }[] = [
  { id: 'overview', label: 'Visão geral', icon: 'grid' },
  { id: 'readings', label: 'Medições', icon: 'activity' },
  { id: 'cluster', label: 'Infraestrutura', icon: 'database' },
  { id: 'replication', label: 'Replicação', icon: 'layers' },
]

const pageTitles: Record<Page, { title: string; description: string }> = {
  overview: { title: 'Visão geral', description: 'Acompanhe os sensores e as últimas leituras da rede.' },
  readings: { title: 'Medições', description: 'Explore o histórico e os registros coletados.' },
  cluster: { title: 'Infraestrutura', description: 'Disponibilidade dos bancos PostgreSQL do cluster.' },
  replication: { title: 'Consistência da replicação', description: 'Compare GUIDs entre os PostgreSQL e identifique registros que não chegaram a todas as réplicas.' },
}

function newest(readings: SensorReading[] | undefined): SensorReading | undefined {
  return readings?.reduce<SensorReading | undefined>((latest, current) =>
    !latest || Date.parse(current.collectedAtUtc) > Date.parse(latest.collectedAtUtc) ? current : latest, undefined)
}

function exportCsv(rows: SensorReading[]) {
  const columns = ['Evento', 'Sensor', 'Valor', 'Coletado em (UTC)', 'Nó de origem']
  const escapeCell = (value: string | number) => `"${String(value).replaceAll('"', '""')}"`
  const lines = [columns, ...rows.map(reading => [
    reading.eventId, reading.sensorId, reading.value, reading.collectedAtUtc, reading.originNodeId,
  ])]
  const csv = '\uFEFF' + lines.map(row => row.map(escapeCell).join(';')).join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'iotdistributed-medicoes.csv'
  link.click()
  URL.revokeObjectURL(url)
}

function App() {
  const {
    sensors, sensorsLoading, sensorsError, readingsBySensor, readingErrors,
    databases, databaseError, lastCheckedAt, updating, autoRefresh,
    setAutoRefresh, refresh, refreshKey, intervalSeconds,
  } = useDashboard()

  const [page, setPage] = useState<Page>('overview')
  const [selectedSensorId, setSelectedSensorId] = useState<string | null>(null)
  const [historyLimit, setHistoryLimit] = useState(30)
  const [tableSensor, setTableSensor] = useState('all')

  useEffect(() => {
    if (sensors.length && !sensors.some(sensor => sensor.sensorId === selectedSensorId)) {
      setSelectedSensorId(sensors[0].sensorId)
    }
  }, [sensors, selectedSensorId])

  const selectedSensor: Sensor | undefined = sensors.find(sensor => sensor.sensorId === selectedSensorId) ?? sensors[0]
  const selectedReadings = selectedSensor ? readingsBySensor[selectedSensor.sensorId] ?? [] : []
  const allReadings = Object.values(readingsBySensor)
    .flat()
    .sort((a, b) => Date.parse(b.collectedAtUtc) - Date.parse(a.collectedAtUtc))
  const tableRows = (tableSensor === 'all' ? allReadings : allReadings.filter(item => item.sensorId === tableSensor)).slice(0, 30)
  const onlineCount = databases.filter(item => item.databaseStatus.toLowerCase() === 'online').length
  const clusterOk = databases.length === 3 && onlineCount === 3 && !databaseError
  const hasAnyErrors = Boolean(sensorsError || databaseError || Object.keys(readingErrors).length)
  const clusterLabel = databaseError ? 'Consulta indisponível' : databases.length === 0 ? 'Verificando...' : clusterOk ? '3 bancos disponíveis' : 'Atenção ao cluster'

  const renderTable = (rows: SensorReading[], compact = false) => (
    <div className="table-scroll">
      <table className="readings-table">
        <thead><tr><th>Sensor</th><th>Valor</th><th>Data da coleta</th><th>Origem</th>{!compact && <th>Evento</th>}</tr></thead>
        <tbody>
          {rows.length ? rows.map(reading => {
            const sensor = sensors.find(item => item.sensorId === reading.sensorId)
            return (
              <tr key={reading.eventId}>
                <td><div className="table-sensor"><span className="table-sensor-mark"/><div><strong>{sensor ? sensorName(sensor) : reading.sensorId}</strong><small>{reading.sensorId}</small></div></div></td>
                <td className="table-value">{formatValue(reading.value)} <small>{sensor ? sensorUnit(sensor) : ''}</small></td>
                <td className="muted-cell">{formatDate(reading.collectedAtUtc)}</td>
                <td><span className="node-pill">NODE {reading.originNodeId.toString().padStart(2, '0')}</span></td>
                {!compact && <td className="event-id" title={reading.eventId}>{reading.eventId.slice(0, 8)}…</td>}
              </tr>
            )
          }) : <tr><td colSpan={compact ? 4 : 5}><div className="table-empty">Nenhuma medição encontrada. Inicie o simulador ESP32 para receber novas leituras.</div></td></tr>}
        </tbody>
      </table>
    </div>
  )

  const renderChart = () => (
    <section className="panel panel-chart">
      <div className="panel-heading">
        <div><div className="eyebrow">ANÁLISE DE DADOS</div><h2>Histórico de medições</h2><p>Valores ordenados pelo horário de coleta</p></div>
        <div className="chart-controls">
          <label className="sr-only" htmlFor="chart-sensor">Sensor do gráfico</label>
          <select id="chart-sensor" value={selectedSensor?.sensorId ?? ''} onChange={event => setSelectedSensorId(event.target.value)} disabled={!sensors.length}>
            {sensors.map(sensor => <option key={sensor.sensorId} value={sensor.sensorId}>{sensorName(sensor)}</option>)}
          </select>
          <label className="sr-only" htmlFor="chart-limit">Quantidade de amostras</label>
          <select id="chart-limit" value={historyLimit} onChange={event => setHistoryLimit(Number(event.target.value))}>
            <option value={20}>20 leituras</option><option value={30}>30 leituras</option><option value={60}>60 leituras</option><option value={100}>100 leituras</option>
          </select>
        </div>
      </div>
      {selectedSensor && readingErrors[selectedSensor.sensorId] && <p className="inline-warning"><Icon name="alert" size={15}/> Falha na última consulta. Exibindo dados disponíveis anteriormente.</p>}
      <HistoryChart readings={selectedReadings} unit={selectedSensor ? sensorUnit(selectedSensor) : ''} limit={historyLimit}/>
    </section>
  )

  return (
    <div className="app-shell">
      <aside className="sidebar" aria-label="Navegação principal">
        <div className="brand"><span className="brand-symbol"><Icon name="layers" size={25}/></span><div><strong>IoT<span>Distributed</span></strong><small>SENSOR NETWORK</small></div></div>
        <div className="sidebar-section-label">WORKSPACE</div>
        <nav className="nav-links">
          {navigation.map(item => <button key={item.id} type="button" className={`nav-link ${page === item.id ? 'nav-link--active' : ''}`} onClick={() => setPage(item.id)} aria-current={page === item.id ? 'page' : undefined}><Icon name={item.icon} size={19}/>{item.label}{page === item.id && <span className="nav-active-bar"/>}</button>)}
        </nav>
        <div className="sidebar-bottom"><div className="sidebar-info"><Icon name="radio" size={19}/><div><strong>Ambiente de laboratório</strong><span>Computação Distribuída</span></div></div><div className="sidebar-version">PROJETO ACADÊMICO · V1.0</div></div>
      </aside>

      <div className="main-area">
        <header className="topbar">
          <div className="breadcrumb"><span>Plataforma IoT</span><Icon name="chevron" size={14}/><strong>{pageTitles[page].title}</strong></div>
          <div className="topbar-actions">
            <span className="topbar-time"><Icon name="clock" size={16}/>{lastCheckedAt ? `Consulta: ${formatTime(lastCheckedAt)}` : 'Aguardando consulta'}</span>
            <button className={`auto-toggle ${autoRefresh ? 'auto-toggle--active' : ''}`} type="button" onClick={() => setAutoRefresh(!autoRefresh)} aria-pressed={autoRefresh} title={autoRefresh ? 'Pausar atualização automática' : 'Retomar atualização automática'}><span className="live-dot"/>{autoRefresh ? `Auto · ${intervalSeconds}s` : 'Pausado'}</button>
            <button className="refresh-button" type="button" onClick={refresh} disabled={updating} title="Atualizar dados agora" aria-label="Atualizar dados agora"><Icon name="refresh" size={18} className={updating ? 'spin' : ''}/></button>
          </div>
        </header>

        <main className="page-content">
          <div className="page-title-row"><div><span className="page-kicker">CENTRO DE MONITORAMENTO</span><h1>{pageTitles[page].title}</h1><p>{pageTitles[page].description}</p></div><span className={`health-badge ${clusterOk ? 'health-badge--ok' : hasAnyErrors || databases.length ? 'health-badge--warning' : ''}`}><span className="live-dot"/>{clusterLabel}</span></div>

          {sensorsError && <div className="alert-banner" role="alert"><Icon name="alert" size={20}/><div><strong>Não foi possível carregar os sensores</strong><span>{sensorsError}</span></div><button type="button" onClick={refresh}>Tentar novamente</button></div>}
          {!sensorsError && sensorsLoading && <div className="loading-line" role="status">Carregando sensores do PostgreSQL...</div>}

          {page === 'overview' && <>
            <section className="stats-grid" aria-label="Indicadores gerais">
              <div className="stat-card"><span className="stat-icon stat-icon--blue"><Icon name="activity" size={21}/></span><span className="stat-caption">Sensores cadastrados</span><strong>{sensors.length.toString().padStart(2,'0')}</strong><span className="stat-foot">Registro consultado na API</span></div>
              <div className="stat-card"><span className="stat-icon stat-icon--green"><Icon name="database" size={21}/></span><span className="stat-caption">Bancos disponíveis</span><strong>{onlineCount}<span className="stat-denominator"> / 3</span></strong><span className="stat-foot">{databaseError ? 'Falha na verificação' : 'PostgreSQL · verificação ativa'}</span></div>
              <div className="stat-card"><span className="stat-icon stat-icon--purple"><Icon name="layers" size={21}/></span><span className="stat-caption">Leituras em memória</span><strong>{allReadings.length.toLocaleString('pt-BR')}</strong><span className="stat-foot">Até 100 registros por sensor</span></div>
              <div className="stat-card"><span className="stat-icon stat-icon--amber"><Icon name="clock" size={21}/></span><span className="stat-caption">Última consulta</span><strong className="stat-time">{formatTime(lastCheckedAt)}</strong><span className="stat-foot">{autoRefresh ? `Atualiza a cada ${intervalSeconds}s` : 'Atualização automática pausada'}</span></div>
            </section>

            <div className="section-heading"><div><h2>Sensores da rede</h2><p>Selecione um sensor para analisar seu histórico</p></div><span className="count-tag">{sensors.length} dispositivos</span></div>
            <section className="sensor-grid" aria-label="Sensores">
              {sensors.map(sensor => <SensorCard key={sensor.sensorId} sensor={sensor} reading={newest(readingsBySensor[sensor.sensorId])} selected={selectedSensor?.sensorId === sensor.sensorId} error={readingErrors[sensor.sensorId]} onSelect={() => setSelectedSensorId(sensor.sensorId)}/>)}
              {!sensorsLoading && !sensorsError && !sensors.length && <p className="empty-state">Nenhum sensor cadastrado.</p>}
            </section>

            <div className="dashboard-lower">{renderChart()}
              <section className="panel recent-panel"><div className="panel-heading"><div><div className="eyebrow">ATIVIDADE RECENTE</div><h2>Últimas leituras</h2><p>Dados de todos os sensores</p></div><button className="text-action" type="button" onClick={() => setPage('readings')}>Ver todas <Icon name="arrow" size={15}/></button></div>
                {renderTable(allReadings.slice(0, 5), true)}
              </section>
            </div>
          </>}

          {page === 'readings' && <>
            <div className="readings-intro"><span className="info-icon"><Icon name="activity" size={20}/></span><div><strong>Explorador de medições</strong><p>Consulte as últimas 100 leituras de cada sensor. A tabela reflete os dados retornados pela API.</p></div></div>
            <section className="panel readings-panel">
              <div className="panel-heading"><div><div className="eyebrow">REGISTROS</div><h2>Histórico de eventos</h2><p>{allReadings.length} leituras carregadas · exibindo até 30</p></div><div className="chart-controls"><label className="sr-only" htmlFor="table-sensor">Filtrar sensor</label><select id="table-sensor" value={tableSensor} onChange={event => setTableSensor(event.target.value)}><option value="all">Todos os sensores</option>{sensors.map(sensor => <option key={sensor.sensorId} value={sensor.sensorId}>{sensorName(sensor)}</option>)}</select><button className="export-button" type="button" onClick={() => exportCsv(tableSensor === 'all' ? allReadings : allReadings.filter(item => item.sensorId === tableSensor))} disabled={!allReadings.length}><Icon name="download" size={16}/> Exportar CSV</button></div></div>
              {renderTable(tableRows)}
            </section>
            {renderChart()}
          </>}

          {page === 'replication' && <ReplicationPanel autoRefresh={autoRefresh} refreshKey={refreshKey}/>}

          {page === 'cluster' && <>
            <div className="readings-intro"><span className="info-icon"><Icon name="server" size={20}/></span><div><strong>Arquitetura distribuída</strong><p>Três nós .NET se comunicam via gRPC. Cada nó possui seu próprio PostgreSQL. A API REST consulta os bancos independentemente.</p></div></div>
            {databaseError && <div className="alert-banner" role="alert"><Icon name="alert" size={20}/><div><strong>Falha ao verificar bancos</strong><span>{databaseError}</span></div></div>}
            <section className="cluster-grid" aria-label="Bancos do cluster">
              {[1, 2, 3].map(nodeId => {
                const database = databases.find(item => item.nodeId === nodeId)
                const state = database?.databaseStatus?.toLowerCase()
                const online = state === 'online'
                return <article className="cluster-card" key={nodeId}><div className="cluster-top"><span className="cluster-icon"><Icon name="database" size={24}/></span><span className={`cluster-status ${online ? 'cluster-status--online' : state === 'offline' ? 'cluster-status--offline' : ''}`}><span className="live-dot"/>{state === 'online' ? 'Disponível' : state === 'offline' ? 'Indisponível' : 'Sem informação'}</span></div><h2>PostgreSQL {nodeId}</h2><p>db{nodeId}:5432 · NODE {nodeId.toString().padStart(2,'0')}</p><div className="cluster-divider"/><div className="cluster-metric"><span>Sensores cadastrados</span><strong>{database?.sensorCount ?? '—'}</strong></div><div className="cluster-metric"><span>Estado do banco</span><strong>{state === 'online' ? 'Online' : state === 'offline' ? 'Offline' : '—'}</strong></div></article>
              })}
            </section>
            <div className="replication-launch"><div><strong>Quer verificar a consistência dos dados?</strong><p>Compare os GUIDs entre os três bancos e identifique eventos ausentes ou presentes em apenas um nó.</p></div><button type="button" className="export-button" onClick={() => setPage('replication')}>Comparar réplicas <Icon name="arrow" size={16}/></button></div>
            <section className="panel architecture-panel"><div className="panel-heading"><div><div className="eyebrow">ARQUITETURA DO SISTEMA</div><h2>Fluxo de dados</h2><p>Comunicação e armazenamento entre os módulos</p></div></div><div className="flow-diagram"><div className="flow-box"><Icon name="radio" size={27}/><strong>ESP32</strong><small>Sensores simulados</small></div><Icon name="arrow" size={24} className="flow-arrow"/><div className="flow-box"><Icon name="activity" size={27}/><strong>EMQX</strong><small>Mensagens MQTT</small></div><Icon name="arrow" size={24} className="flow-arrow"/><div className="flow-box flow-box--wide"><Icon name="server" size={27}/><strong>3 nós .NET</strong><small>gRPC / replicação</small></div><Icon name="arrow" size={24} className="flow-arrow"/><div className="flow-box"><Icon name="database" size={27}/><strong>3 bancos</strong><small>PostgreSQL</small></div><Icon name="arrow" size={24} className="flow-arrow"/><div className="flow-box"><Icon name="grid" size={27}/><strong>API + React</strong><small>Consulta e exibição</small></div></div><p className="diagram-note">Este painel mostra a arquitetura configurada. O estado acima verifica os bancos de dados, não a disponibilidade de cada processo gRPC ou do ESP32.</p></section>
          </>}

          <footer className="footer"><span>IoTDistributed · Projeto acadêmico de Computação Distribuída</span><span>Dados fornecidos pela IoTDistributed.Api</span></footer>
        </main>
      </div>
    </div>
  )
}
export default App
