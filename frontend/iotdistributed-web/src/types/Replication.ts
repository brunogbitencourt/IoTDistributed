/** Contrato de GET /api/replication/divergences (ASP.NET Core). */
export type ReplicationDatabase = {
  nodeId: number
  status: string
  eventCount: number | null
}

export type DivergentEvent = {
  eventId: string
  missingInNodes: number[]
}

export type ReplicationComparison = {
  fromUtc: string
  toUtc: string
  allDatabasesAvailable: boolean
  databases: ReplicationDatabase[]
  divergences: DivergentEvent[]
}
