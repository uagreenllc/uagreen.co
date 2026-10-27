import type { Config } from 'tailwindcss'

// Tokens live in assets/css/main.css; Tailwind only maps them to utilities.
export default <Partial<Config>>{
  theme: {
    extend: {
      colors: {
        bg: 'var(--bg)',
        ink: 'var(--ink)',
        ink2: 'var(--ink-2)',
        green: 'var(--green)',
        green2: 'var(--green-2)',
        copper: 'var(--copper)',
        line: 'var(--line)',
        metal: 'var(--metal)',
      },
      fontFamily: { sans: ['var(--font)'] },
    },
  },
}
