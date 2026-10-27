import type { Redirect } from '~/types/api'

/** Old Weblium path → new path. Exact, case-sensitive; a trailing slash is ignored. */
export function matchRedirect(path: string, table: Redirect[]): string | null {
  const p = path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path
  return table.find(r => r.from === p)?.to ?? null
}
