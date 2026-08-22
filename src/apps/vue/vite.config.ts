import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

const apiTarget =
  process.env.VITE_API_PROXY_TARGET ??
  process.env.services__webapi__https__0 ??
  process.env.services__webapi__http__0

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: true,
    port: Number(process.env.PORT ?? 5173),
    strictPort: true,
    proxy: apiTarget
      ? {
          '/api': {
            target: apiTarget,
            changeOrigin: true,
            secure: false,
          },
        }
      : undefined,
  },
})
