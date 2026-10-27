/** "+380668696072" → "+380 66 869 60 72"; anything else is returned as is. */
export function formatPhone(e164: string): string {
  const m = e164.replace(/\D/g, '').match(/^380(\d{2})(\d{3})(\d{2})(\d{2})$/)
  return m ? `+380 ${m[1]} ${m[2]} ${m[3]} ${m[4]}` : e164
}

/** Comma-joined setting → trimmed list. */
export function splitList(value: string | undefined): string[] {
  return (value ?? '').split(',').map(s => s.trim()).filter(Boolean)
}
