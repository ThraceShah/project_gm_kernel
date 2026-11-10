import { chromium } from 'playwright-core';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '../..');
const tempDocsDir = path.resolve(projectRoot, 'temp_docs');

if (!fs.existsSync(tempDocsDir)) {
  fs.mkdirSync(tempDocsDir, { recursive: true });
}

const chromePath = '/home/thrace/.cache/ms-playwright/chromium-1248/chrome-linux64/chrome';
const liveUrl = 'https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/';

delete process.env.http_proxy;
delete process.env.https_proxy;
delete process.env.HTTP_PROXY;
delete process.env.HTTPS_PROXY;
delete process.env.all_proxy;
delete process.env.ALL_PROXY;

async function verifyPortalLive() {
  const cleanEnv = { ...process.env };
  delete cleanEnv.http_proxy;
  delete cleanEnv.https_proxy;
  delete cleanEnv.HTTP_PROXY;
  delete cleanEnv.HTTPS_PROXY;
  delete cleanEnv.all_proxy;
  delete cleanEnv.ALL_PROXY;

  console.log(`[PortalVerify] Navigating to live HTTPS portal: ${liveUrl}`);
  const browser = await chromium.launch({
    executablePath: chromePath,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-gpu',
      '--use-gl=swiftshader',
      '--no-proxy-server',
    ],
  });

  const errors = [];
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  page.on('pageerror', (err) => errors.push(`[Portal Page Error] ${err.message}`));
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push(`[Portal Console Error] ${msg.text()}`);
  });

  try {
    const res = await page.goto(liveUrl, { waitUntil: 'networkidle', timeout: 30000 });
    console.log(`[PortalVerify] Status: ${res.status()} ${res.statusText()}`);
    if (res.status() !== 200) {
      throw new Error(`Failed to load portal URL: status ${res.status()}`);
    }

    await page.waitForTimeout(1500);

    const bodyText = await page.textContent('body');
    if (!bodyText.includes('GM Kernel vs Parasolid 对比工作台')) {
      throw new Error('Title not found on live portal page');
    }
    if (!bodyText.includes('Current GM / real PK oracle data')) {
      throw new Error('Real Run data not loaded on live portal page');
    }
    if (!bodyText.includes('通过 4')) {
      throw new Error('Real Pass counter not found on live portal page');
    }

    const canvases = await page.$$('canvas');
    console.log(`[PortalVerify] Found ${canvases.length} WebGL canvases`);
    if (canvases.length < 2) {
      throw new Error('Expected 2 3D WebGL canvases on live portal page');
    }

    // Capture Desktop Live Portal Screenshot
    const liveDesktopScreenshot = path.join(tempDocsDir, 'portal_https_desktop_live.png');
    await page.screenshot({ path: liveDesktopScreenshot });
    console.log(`[PortalVerify] Saved live desktop screenshot to: ${liveDesktopScreenshot}`);

    // Verify Checks tab
    const checksTab = await page.waitForSelector('text=逐项验收');
    await checksTab.click();
    await page.waitForTimeout(500);

    const liveChecksScreenshot = path.join(tempDocsDir, 'portal_https_checks_live.png');
    await page.screenshot({ path: liveChecksScreenshot });
    console.log(`[PortalVerify] Saved live checks screenshot to: ${liveChecksScreenshot}`);

    // Narrow Screen Viewport
    const mobilePage = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await mobilePage.goto(liveUrl, { waitUntil: 'networkidle', timeout: 30000 });
    await mobilePage.waitForTimeout(1000);

    const liveMobileScreenshot = path.join(tempDocsDir, 'portal_https_mobile_live.png');
    await mobilePage.screenshot({ path: liveMobileScreenshot });
    console.log(`[PortalVerify] Saved live mobile screenshot to: ${liveMobileScreenshot}`);
    await mobilePage.close();

    console.log('\n======================================================');
    console.log('🎉 LIVE PORTAL VERIFICATION FULLY PASSED!');
    console.log(`Live URL: ${liveUrl}`);
    console.log(`Errors: ${errors.length}`);
    if (errors.length > 0) console.error(errors);
    console.log('======================================================\n');
  } catch (err) {
    console.error('❌ Portal verification failed:', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
}

verifyPortalLive();
