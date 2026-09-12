import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.XNK_API_TARGET || 'http://localhost:5270',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
