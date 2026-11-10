import { chromium } from 'playwright-core';
import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '../..');
const distDir = path.resolve(__dirname, '../dist');
const tempDocsDir = path.resolve(projectRoot, 'temp_docs');

if (!fs.existsSync(tempDocsDir)) {
  fs.mkdirSync(tempDocsDir, { recursive: true });
}

// Simple static server for dist
const mimeTypes = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
};

function startServer(port = 8089) {
  const server = http.createServer((req, res) => {
    let reqPath = req.url.split('?')[0];
    if (reqPath === '/') reqPath = '/index.html';
    const filePath = path.join(distDir, reqPath);

    if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
      const ext = path.extname(filePath);
      res.writeHead(200, {
        'Content-Type': mimeTypes[ext] || 'application/octet-stream',
        'Access-Control-Allow-Origin': '*',
      });
      fs.createReadStream(filePath).pipe(res);
    } else {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('Not Found: ' + reqPath);
    }
  });

  return new Promise((resolve) => {
    server.listen(port, '127.0.0.1', () => {
      console.log(`[SmokeServer] Serving ${distDir} at http://127.0.0.1:${port}/`);
      resolve(server);
    });
  });
}

const chromePath = '/home/thrace/.cache/ms-playwright/chromium-1248/chrome-linux64/chrome';

async function runSmokeTests() {
  const port = 8089;
  const server = await startServer(port);
  const baseUrl = `http://127.0.0.1:${port}`;

  console.log('[Smoke] Launching Chromium from:', chromePath);
  const browser = await chromium.launch({
    executablePath: chromePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu', '--use-gl=swiftshader'],
  });

  const errors = [];

  try {
    // ----------------------------------------------------
    // Test 1: Real Production Data Mode (Default /)
    // ----------------------------------------------------
    console.log('[Test 1] Testing Real Production Data Mode (/)...');
    const page1 = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page1.on('pageerror', (err) => errors.push(`[Page 1 Error] ${err.message}`));
    page1.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(`[Page 1 Console Error] ${msg.text()}`);
    });

    await page1.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await page1.waitForTimeout(1000);

    const body1 = await page1.textContent('body');
    if (!body1.includes('GM Kernel vs Parasolid 对比工作台')) {
      throw new Error('Test 1 Failed: Title not found');
    }

    // Verify Real Run Metadata
    if (!body1.includes('Current GM / real PK oracle data')) {
      throw new Error('Test 1 Failed: Real run label not found');
    }

    // Verify Real Case Counts: 4 pass, 1 diagnostic, 205 missing, 204 unsupported (total 414)
    if (!body1.includes('通过 4') || !body1.includes('缺失 205') || !body1.includes('不支持 204')) {
      throw new Error('Test 1 Failed: Expected status counters (通过 4, 缺失 205, 不支持 204) not found');
    }

    // Verify WebGL Canvases
    const canvases1 = await page1.$$('canvas');
    if (canvases1.length < 2) {
      throw new Error(`Test 1 Failed: Expected 2 WebGL canvases, got ${canvases1.length}`);
    }

    // Screenshot 1: Real Desktop Workbench
    const realWorkbenchScreenshot = path.join(tempDocsDir, 'real_smoke_desktop_workbench.png');
    await page1.screenshot({ path: realWorkbenchScreenshot });
    console.log(`[Test 1] Real Desktop Workbench screenshot saved to ${realWorkbenchScreenshot}`);

    // Verify default selected case has checks
    console.log('[Test 1.1] Verifying Checks Table for real case...');
    const checksText = await page1.textContent('body');
    if (!checksText.includes('逐项验收') || !checksText.includes('Full BODY_check GM fresh XT')) {
      throw new Error('Test 1.1 Failed: Checks table rows not rendered');
    }

    const realChecksScreenshot = path.join(tempDocsDir, 'real_smoke_desktop_checks.png');
    await page1.screenshot({ path: realChecksScreenshot });
    console.log(`[Test 1.1] Real Checks Screenshot saved to ${realChecksScreenshot}`);

    // Verify Performance tab for real case
    console.log('[Test 1.2] Verifying Performance metrics for real case...');
    const perfTab = await page1.waitForSelector('text=性能指标');
    await perfTab.click();
    await page1.waitForTimeout(400);

    const perfText = await page1.textContent('body');
    if (!perfText.includes('Apple M4') || !perfText.includes('GM Kernel 耗时') || !perfText.includes('正确性资格认证')) {
      throw new Error('Test 1.2 Failed: Real performance metrics not rendered properly');
    }

    // Test Diagnostic case
    console.log('[Test 1.3] Testing Diagnostic case regular-curved.block-subtract...');
    const diagFilterBtn = await page1.waitForSelector('text=诊断 1');
    await diagFilterBtn.click();
    await page1.waitForTimeout(400);

    const diagCaseItem = await page1.waitForSelector('text=regular-curved.block-subtract');
    await diagCaseItem.click();
    await page1.waitForTimeout(800);

    const diagText = await page1.textContent('body');
    if (!diagText.includes('DIAGNOSTIC')) {
      throw new Error('Test 1.3 Failed: Diagnostic status badge not found');
    }

    const realDiagScreenshot = path.join(tempDocsDir, 'real_smoke_desktop_diagnostic.png');
    await page1.screenshot({ path: realDiagScreenshot });
    console.log(`[Test 1.3] Real Diagnostic Screenshot saved to ${realDiagScreenshot}`);

    await page1.close();

    // ----------------------------------------------------
    // Test 2: Real Mobile Screen (390x844)
    // ----------------------------------------------------
    console.log('[Test 2] Testing Real Mobile Screen (390x844)...');
    const page2 = await browser.newPage({ viewport: { width: 390, height: 844 } });
    page2.on('pageerror', (err) => errors.push(`[Page 2 Error] ${err.message}`));

    await page2.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await page2.waitForTimeout(800);

    const realMobileScreenshot = path.join(tempDocsDir, 'real_smoke_mobile_narrow.png');
    await page2.screenshot({ path: realMobileScreenshot });
    console.log(`[Test 2] Real Mobile Screenshot saved to ${realMobileScreenshot}`);

    await page2.close();

    console.log('\n========================================');
    console.log('✅ ALL REAL DATA SMOKE TESTS PASSED!');
    console.log(`Errors encountered: ${errors.length}`);
    if (errors.length > 0) {
      console.error('Errors:', errors);
      process.exitCode = 1;
    }
    console.log('========================================\n');
  } catch (err) {
    console.error('❌ Real Smoke Test Failed:', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
    server.close();
  }
}

runSmokeTests();
