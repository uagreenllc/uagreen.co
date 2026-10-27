import { defineConfig } from 'vitest/config'

// Pure-TS tests only (scene geometry, pricing, cart, redirects); no Nuxt runtime needed.
export default defineConfig({
  test: { include: ['components/**/*.test.ts', 'utils/**/*.test.ts', 'stores/**/*.test.ts'] },
})
