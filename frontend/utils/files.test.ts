import { describe, it, expect } from 'vitest'
import { cvProblem } from './files'

const file = (name: string, type: string, bytes: number) => ({ name, type, size: bytes }) as File

describe('cvProblem', () => {
  it('rejects files over 10 MB', () => {
    expect(cvProblem(file('cv.pdf', 'application/pdf', 12 * 1024 * 1024))).toBe('tooBig')
  })
  it('rejects anything but PDF or Word', () => {
    expect(cvProblem(file('cv.exe', 'application/x-msdownload', 1024))).toBe('type')
  })
  it('accepts a small PDF', () => {
    expect(cvProblem(file('cv.pdf', 'application/pdf', 1024 * 1024))).toBeNull()
  })
  it('accepts a .docx the browser reports with an empty type, and no file at all', () => {
    expect(cvProblem(file('cv.docx', '', 2048))).toBeNull()
    expect(cvProblem(null)).toBeNull()
  })
})
