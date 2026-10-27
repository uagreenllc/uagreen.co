import { describe, it, expect } from 'vitest'
import { matchRedirect } from './redirects'

const table = [
  { from: '/junior-assembly-fitter', to: '/vacancies/junior-assembly-fitter' },
  { from: '/en/about-us-1', to: '/en/about-us' },
]

describe('matchRedirect', () => {
  it('matches exact paths and tolerates a trailing slash', () => {
    expect(matchRedirect('/junior-assembly-fitter', table)).toBe('/vacancies/junior-assembly-fitter')
    expect(matchRedirect('/junior-assembly-fitter/', table)).toBe('/vacancies/junior-assembly-fitter')
    expect(matchRedirect('/en/about-us-1', table)).toBe('/en/about-us')
  })
  it('ignores everything else', () => {
    expect(matchRedirect('/vacancies/junior-assembly-fitter', table)).toBeNull()
    expect(matchRedirect('/JUNIOR-ASSEMBLY-FITTER', table)).toBeNull()
  })
})
