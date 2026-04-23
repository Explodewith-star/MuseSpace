import { defineConfig } from 'vite'
import path from 'node:path'
import { execSync } from 'node:child_process'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'

// 取 changelog.ts 最后一次修改的 commit hash
// 只有这个文件内容变更时 hash 才会变，避免每次 push 都触发更新提示
function getChangelogVersion(): string {
  try {
    return execSync('git log --format=%h -1 -- src/config/changelog.ts')
      .toString()
      .trim() || 'dev'
  } catch {
    return 'dev'
  }
}

// https://vite.dev/config/
export default defineConfig({
  define: {
    // 构建时注入 changelog.ts 的 git hash，运行时前端作为版本标识读取
    __CHANGELOG_VERSION__: JSON.stringify(getChangelogVersion()),
  },
  plugins: [
    vue(),
    UnoCSS(),
    AutoImport({
      imports: ['vue', 'vue-router', 'pinia'],
      dts: 'src/auto-imports.d.ts',
      eslintrc: {
        enabled: true,
      },
    }),
    Components({
      dirs: ['src/components'],
      dts: 'src/components.d.ts',
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    open: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5142',
        changeOrigin: true,
      },
    },
  },
  build: {
    target: 'es2015',
    outDir: 'dist',
    chunkSizeWarningLimit: 1500,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (
            id.includes('node_modules/vue') ||
            id.includes('node_modules/vue-router') ||
            id.includes('node_modules/pinia')
          ) {
            return 'vue'
          }
          if (id.includes('node_modules/axios')) {
            return 'axios'
          }
        },
      },
    },
  },
})
