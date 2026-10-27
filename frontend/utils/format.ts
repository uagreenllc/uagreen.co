/** "+380668696072" → "+380 66 869 60 72"; anything else is returned as is. */
export function formatPhone(e164: string): string {
  const m = e164.replace(/\D/g, '').match(/^380(\d{2})(\d{3})(\d{2})(\d{2})$/)
  return m ? `+380 ${m[1]} ${m[2]} ${m[3]} ${m[4]}` : e164
}

/** ISO date → "27.10.2026" (UTC, so SSR and the browser render the same string). */
export function formatDate(iso: string): string {
  const d = new Date(iso)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getUTCDate())}.${p(d.getUTCMonth() + 1)}.${d.getUTCFullYear()}`
}

/** HTML → first ~160 characters of plain text, for meta descriptions. */
export function excerpt(html: string | null | undefined, max = 160): string {
  const text = (html ?? '').replace(/<[^>]+>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim()
  return text.length > max ? `${text.slice(0, max - 1).trimEnd()}…` : text
}

/** Comma-joined setting → trimmed list. */
export function splitList(value: string | undefined): string[] {
  return (value ?? '').split(',').map(s => s.trim()).filter(Boolean)
}
