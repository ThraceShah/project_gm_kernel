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

// 1. Simple static server for dist
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

// Chromium executable path
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
  const warnings = [];

  try {
    // ----------------------------------------------------
    // Test 1: Empty state (Default mode without data/index.json)
    // ----------------------------------------------------
    console.log('[Test 1] Testing production mode without data/index.json (Waiting Screen)...');
    const page1 = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page1.on('pageerror', (err) => errors.push(`[Page 1 Error] ${err.message}`));
    page1.on('console', (msg) => {
      // Ignore expected 404 on data/index.json during no-data probe
      if (msg.type() === 'error' && !msg.text().includes('404')) {
        errors.push(`[Page 1 Console Error] ${msg.text()}`);
      }
    });

    await page1.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await page1.waitForTimeout(500);

    const emptyText = await page1.textContent('body');
    if (!emptyText.includes('未检测到生产基线数据')) {
      throw new Error('Test 1 Failed: Expected "未检测到生产基线数据" not found');
    }

    const noDataScreenshotPath = path.join(tempDocsDir, 'web_smoke_no_data.png');
    await page1.screenshot({ path: noDataScreenshotPath });
    console.log(`[Test 1] Passed! Screenshot saved to ${noDataScreenshotPath}`);
    await page1.close();

    // ----------------------------------------------------
    // Test 2: Desktop Dev Fixture Mode
    // ----------------------------------------------------
    console.log('[Test 2] Testing Desktop Dev Fixture Mode (?fixture=1)...');
    const page2 = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page2.on('pageerror', (err) => errors.push(`[Page 2 Error] ${err.message}`));
    page2.on('console', (msg) => {
      if (msg.type() === 'error') {
        // filter out harmless 404s if any
        errors.push(`[Page 2 Console Error] ${msg.text()}`);
      }
    });

    await page2.goto(`${baseUrl}/?fixture=1`, { waitUntil: 'networkidle' });
    await page2.waitForTimeout(1000);

    // Verify Title & Fixture Banner
    const bodyContent = await page2.textContent('body');
    if (!bodyContent.includes('GM Kernel vs Parasolid 对比工作台')) {
      throw new Error('Test 2 Failed: Title not found');
    }
    if (!bodyContent.includes('DEV FIXTURE')) {
      throw new Error('Test 2 Failed: Dev Fixture banner not found');
    }

    // Verify Case Counts
    if (!bodyContent.includes('通过 2') || !bodyContent.includes('失败 1') || !bodyContent.includes('缺失 1')) {
      throw new Error('Test 2 Failed: Expected status counters (通过 2, 失败 1, 缺失 1) not found');
    }

    // Verify WebGL Canvases
    const canvases = await page2.$$('canvas');
    if (canvases.length < 2) {
      throw new Error(`Test 2 Failed: Expected at least 2 WebGL canvases for GM & PK, got ${canvases.length}`);
    }

    // Take Desktop Workbench Screenshot
    const desktopScreenshotPath = path.join(tempDocsDir, 'web_smoke_desktop_workbench.png');
    await page2.screenshot({ path: desktopScreenshotPath });
    console.log(`[Test 2] Main Workbench Screenshot saved to ${desktopScreenshotPath}`);

    // Test Step 2 Switching
    console.log('[Test 2.1] Switching to Step 2...');
    const step2Btn = await page2.waitForSelector('text=步骤 2: 滚球圆角曲面生成与裁剪收敛');
    await step2Btn.click();
    await page2.waitForTimeout(600);

    // Verify Checks tab
    console.log('[Test 2.2] Verifying Checks Tab...');
    const checksText = await page2.textContent('body');
    if (!checksText.includes('圆角曲面最大几何偏差') || !checksText.includes('PK_DEBUG_BODY_compare 全局结果')) {
      throw new Error('Test 2.2 Failed: Acceptance check rows not found');
    }

    // Test Performance Tab
    console.log('[Test 2.3] Clicking Performance Tab...');
    const perfTab = await page2.waitForSelector('text=性能指标');
    await perfTab.click();
    await page2.waitForTimeout(400);

    const perfText = await page2.textContent('body');
    if (!perfText.includes('0.42') || !perfText.includes('0.61') || !perfText.includes('0.69x')) {
      throw new Error('Test 2.3 Failed: Performance metrics (0.42, 0.61, 0.69x) not found');
    }

    // Test API & Parameters Tab
    console.log('[Test 2.4] Clicking API & Parameters Tab...');
    const apiTab = await page2.waitForSelector('text=API 与参数');
    await apiTab.click();
    await page2.waitForTimeout(400);

    const apiText = await page2.textContent('body');
    if (!apiText.includes('GmBlendEngine.FixBlends') || !apiText.includes('PK_BODY_fix_blends')) {
      throw new Error('Test 2.4 Failed: API names not found');
    }

    // Test Missing PK Case
    console.log('[Test 2.5] Testing Missing PK case...');
    const missingCaseItem = await page2.waitForSelector('text=复杂导轨截面可变半径倒圆角');
    await missingCaseItem.click();
    await page2.waitForTimeout(600);

    const missingCaseText = await page2.textContent('body');
    if (!missingCaseText.includes('Parasolid 基准网格缺失')) {
      throw new Error('Test 2.5 Failed: Expected "Parasolid 基准网格缺失" overlay');
    }

    // Take Inspection Details Screenshot
    const inspectionScreenshotPath = path.join(tempDocsDir, 'web_smoke_desktop_inspection.png');
    await page2.screenshot({ path: inspectionScreenshotPath });
    console.log(`[Test 2] Inspection Screenshot saved to ${inspectionScreenshotPath}`);

    await page2.close();

    // ----------------------------------------------------
    // Test 3: Narrow / Mobile Screen (390x844)
    // ----------------------------------------------------
    console.log('[Test 3] Testing Narrow / Mobile Screen (390x844)...');
    const page3 = await browser.newPage({ viewport: { width: 390, height: 844 } });
    page3.on('pageerror', (err) => errors.push(`[Page 3 Error] ${err.message}`));

    await page3.goto(`${baseUrl}/?fixture=1`, { waitUntil: 'networkidle' });
    await page3.waitForTimeout(800);

    const mobileScreenshotPath = path.join(tempDocsDir, 'web_smoke_mobile_narrow.png');
    await page3.screenshot({ path: mobileScreenshotPath });
    console.log(`[Test 3] Mobile Screenshot saved to ${mobileScreenshotPath}`);

    await page3.close();

    console.log('\n========================================');
    console.log('✅ ALL SMOKE TESTS PASSED SUCCESSFULLY!');
    console.log(`Errors encountered: ${errors.length}`);
    if (errors.length > 0) {
      console.error('Errors:', errors);
      process.exitCode = 1;
    }
    console.log('========================================\n');
  } catch (err) {
    console.error('❌ Smoke Test Failed:', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
    server.close();
  }
}

runSmokeTests();
