const MAX_CV_BYTES = 10 * 1024 * 1024
const CV_TYPES = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document']
const CV_EXT = /\.(pdf|docx?)$/i

/** Same rules as POST /api/applications: ≤ 10 MB, PDF or Word. Browsers sometimes report no type, so the extension counts too. */
export function cvProblem(file: File | null | undefined): 'tooBig' | 'type' | null {
  if (!file) return null
  if (file.size > MAX_CV_BYTES) return 'tooBig'
  if (file.type ? !CV_TYPES.includes(file.type) : !CV_EXT.test(file.name)) return 'type'
  return null
}
