import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    proxy: {
      '/api': 'http://localhost:5188'
    }
  },
  build: {
    outDir: '../Elements.Pipeline.Server/wwwroot',
    emptyOutDir: true,
    sourcemap: true
  }
});
