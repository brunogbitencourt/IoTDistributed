export type DatabaseStatus = {
  nodeId: number
  databaseStatus: 'online' | 'offline' | string
  sensorCount: number | null
}
