import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import fs from 'node:fs';
import path from 'node:path';

function copyDataPlugin() {
  return {
    name: 'copy-data-plugin',
    closeBundle() {
      const srcDir = path.resolve(__dirname, 'data');
      const dstDir = path.resolve(__dirname, 'dist/data');
      if (fs.existsSync(srcDir)) {
        fs.cpSync(srcDir, dstDir, { recursive: true });
        console.log('[copy-data-plugin] Successfully bundled web/data into dist/data');
      }
    },
    configureServer(server: any) {
      server.middlewares.use((req: any, res: any, next: any) => {
        const url = req.url || '';
        if (url.startsWith('/data/') || url.startsWith('data/')) {
          const cleanUrl = url.replace(/^\/?data\//, '').split('?')[0];
          const filePath = path.resolve(__dirname, 'data', decodeURIComponent(cleanUrl));
          if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
            const ext = path.extname(filePath);
            const contentType =
              ext === '.json'
                ? 'application/json'
                : ext === '.log' || ext === '.txt'
                ? 'text/plain; charset=utf-8'
                : 'application/octet-stream';
            res.setHeader('Content-Type', contentType);
            fs.createReadStream(filePath).pipe(res);
            return;
          }
        }
        next();
      });
    },
  };
}

export default defineConfig({
  plugins: [react(), copyDataPlugin()],
  base: './',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    sourcemap: true,
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    host: true,
  },
});
