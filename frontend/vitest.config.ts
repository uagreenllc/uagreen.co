import { defineConfig } from 'vitest/config'

// Pure-TS tests only (scene geometry); no Nuxt runtime needed.
export default defineConfig({
  test: { include: ['components/**/*.test.ts'] },
})
