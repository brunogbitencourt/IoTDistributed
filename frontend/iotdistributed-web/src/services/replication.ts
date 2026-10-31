import type { ReplicationComparison } from '../types/Replication'

/** O backend permite períodos de no máximo 60 minutos. */
export async function compareReplication(minutes: number, signal: AbortSignal): Promise<ReplicationComparison> {
  const to = new Date()
  const from = new Date(to.getTime() - minutes * 60_000)
  const params = new URLSearchParams({
    fromUtc: from.toISOString(),
    toUtc: to.toISOString(),
  })

  const response = await fetch(`/api/replication/divergences?${params.toString()}`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`Não foi possível comparar as réplicas (HTTP ${response.status}).`)
  }

  return (await response.json()) as ReplicationComparison
}
