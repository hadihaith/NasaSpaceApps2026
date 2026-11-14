import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const relativePath = (path) => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
  root: relativePath('./'),
  publicDir: relativePath('../public'),
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    fs: { allow: [relativePath('../')] },
  },
  build: {
    outDir: relativePath('../out/earth-preview'),
    emptyOutDir: true,
  },
});
