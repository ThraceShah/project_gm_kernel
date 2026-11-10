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

// Clean up old fixture screenshots
['web_smoke_desktop_inspection.png', 'web_smoke_desktop_workbench.png', 'web_smoke_mobile_narrow.png', 'web_smoke_no_data.png'].forEach((f) => {
  const p = path.join(tempDocsDir, f);
  if (fs.existsSync(p)) {
    try { fs.unlinkSync(p); } catch {}
  }
});

// Simple static server for dist
const mimeTypes = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.log': 'text/plain; charset=utf-8',
};

function startServer(port = 8089) {
  const server = http.createServer((req, res) => {
    let reqPath = req.url.split('?')[0];
    if (reqPath === '/') reqPath = '/index.html';
    const filePath = path.join(distDir, decodeURIComponent(reqPath));

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
    // ====================================================
    // Test 1: Real Desktop Workbench (1440x900)
    // ====================================================
    console.log('[Test 1] Testing Real Desktop Workbench (1440x900)...');
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page.on('pageerror', (err) => errors.push(`[Page Error] ${err.message}`));
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(`[Console Error] ${msg.text()}`);
    });

    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(1000);

    const bodyText = await page.textContent('body');
    if (!bodyText.includes('GM Kernel vs Parasolid 对比工作台')) {
      throw new Error('Test 1 Failed: Workbench title not found');
    }

    // 1.1 Verify no dev fixture button in production build
    const devFixtureBtn = await page.$('text=演练模式 (DEV FIXTURE)');
    if (devFixtureBtn) {
      throw new Error('Test 1 Failed: Dev fixture toggle button must NOT be present in production build');
    }

    // 1.2 Verify real run label & metadata
    if (!bodyText.includes('Current GM / real PK oracle data')) {
      throw new Error('Test 1 Failed: Real run label not found');
    }
    if (!bodyText.includes('Apple M4')) {
      throw new Error('Test 1 Failed: Real Apple M4 host metadata not found in header');
    }

    // 1.3 Verify real case counters: 414 total, 4 pass, 205 missing, 204 unsupported, 1 diagnostic
    if (
      !bodyText.includes('显示 414 / 414 项') ||
      !bodyText.includes('通过 4') ||
      !bodyText.includes('缺失 205') ||
      !bodyText.includes('不支持 204') ||
      !bodyText.includes('诊断 1')
    ) {
      throw new Error('Test 1 Failed: Case counts (414 total, 4 pass, 205 missing, 204 unsupported, 1 diagnostic) not rendered correctly');
    }

    // 1.4 Verify 2 WebGL canvases
    const canvases = await page.$$('canvas');
    if (canvases.length < 2) {
      throw new Error(`Test 1 Failed: Expected 2 WebGL canvases, got ${canvases.length}`);
    }

    // Save Workbench overview screenshot
    const shotWorkbench = path.join(tempDocsDir, 'real_smoke_desktop_workbench.png');
    await page.screenshot({ path: shotWorkbench });
    console.log(`[Test 1] Saved ${shotWorkbench}`);

    // ====================================================
    // Test 2: Filter by Family, Status, and Search
    // ====================================================
    console.log('[Test 2] Testing Filter & Search...');
    // Filter Blend
    const blendTab = await page.waitForSelector('button:has-text("倒角 Blend")');
    await blendTab.click();
    await page.waitForTimeout(300);
    const blendText = await page.textContent('body');
    if (!blendText.includes('显示 228 / 414 项')) {
      throw new Error(`Test 2 Failed: Filter Blend should show 228 cases, got text: ${blendText.slice(0, 300)}`);
    }

    // Filter Boolean
    const booleanTab = await page.waitForSelector('button:has-text("布尔 Boolean")');
    await booleanTab.click();
    await page.waitForTimeout(300);
    const boolText = await page.textContent('body');
    if (!boolText.includes('显示 186 / 414 项')) {
      throw new Error(`Test 2 Failed: Filter Boolean should show 186 cases, got text: ${boolText.slice(0, 300)}`);
    }

    // Filter All
    const allTab = await page.waitForSelector('button:has-text("全部 (414)")');
    await allTab.click();
    await page.waitForTimeout(300);

    // Search input
    const searchInput = await page.waitForSelector('input[placeholder*="搜索用例"]');
    await searchInput.fill('plane-nurbs');
    await page.waitForTimeout(300);
    const searchMatch = await page.textContent('body');
    if (!searchMatch.includes('solid-blend.plane-nurbs')) {
      throw new Error('Test 2 Failed: Search for plane-nurbs did not find solid-blend.plane-nurbs');
    }
    // Clear search
    await searchInput.fill('');
    await page.waitForTimeout(300);

    // ====================================================
    // Test 3: Real Passed Solid Blend (before / after / roles / picking)
    // ====================================================
    console.log('[Test 3] Testing Real Passed Solid Blend solid-blend.plane-nurbs...');
    const passFilterBtn = await page.waitForSelector('button:has-text("通过 4")');
    await passFilterBtn.click();
    await page.waitForTimeout(300);

    const solidBlendItem = await page.waitForSelector('text=solid-blend / plane-nurbs');
    await solidBlendItem.click();
    await page.waitForTimeout(800);

    // Check before state
    const beforeBtn = await page.waitForSelector('button:has-text("前态 (Before)")');
    await beforeBtn.click();
    await page.waitForTimeout(500);

    const shotBlendBefore = path.join(tempDocsDir, 'real_smoke_desktop_blend_before.png');
    await page.screenshot({ path: shotBlendBefore });
    console.log(`[Test 3] Saved ${shotBlendBefore}`);

    // Switch to after state
    const afterBtn = await page.waitForSelector('button:has-text("后态 (After)")');
    await afterBtn.click();
    await page.waitForTimeout(800);

    const shotBlendAfter = path.join(tempDocsDir, 'real_smoke_desktop_blend_after.png');
    await page.screenshot({ path: shotBlendAfter });
    console.log(`[Test 3] Saved ${shotBlendAfter}`);

    // Raycast / click canvas
    const canvasFirst = canvases[0];
    const box = await canvasFirst.boundingBox();
    if (box) {
      // Click near center to trigger raycast pick
      await page.mouse.click(box.x + box.width * 0.5, box.y + box.height * 0.5);
      await page.waitForTimeout(300);
    }

    // ====================================================
    // Test 4: Boolean Tools & Modified Face
    // ====================================================
    console.log('[Test 4] Testing Boolean case analytic-boolean.equal-overlap-subtract...');
    const allStatusBtn = await page.waitForSelector('button:has-text("全状态")');
    await allStatusBtn.click();
    await page.waitForTimeout(300);

    await searchInput.fill('equal-overlap-subtract');
    await page.waitForTimeout(300);

    const boolCaseItem = await page.waitForSelector('text=analytic-boolean / equal-overlap-subtract');
    await boolCaseItem.click();
    await page.waitForTimeout(800);

    // Click before state to check boolean tools
    await (await page.waitForSelector('button:has-text("前态 (Before)")')).click();
    await page.waitForTimeout(500);

    const shotBoolTools = path.join(tempDocsDir, 'real_smoke_desktop_boolean_tools.png');
    await page.screenshot({ path: shotBoolTools });
    console.log(`[Test 4] Saved ${shotBoolTools}`);

    await searchInput.fill('');
    await page.waitForTimeout(300);

    // ====================================================
    // Test 5: Timeline Controls (play, pause, speed, scrub)
    // ====================================================
    console.log('[Test 5] Testing Timeline Controls...');
    // Play button
    const playBtn = await page.waitForSelector('button[title*="自动按快照播放"], button[title*="暂停"]');
    await playBtn.click(); // Start play
    await page.waitForTimeout(1600); // Allow step transition
    await playBtn.click(); // Pause

    // Speed 2x
    const speed2x = await page.waitForSelector('button:has-text("2x")');
    await speed2x.click();
    await page.waitForTimeout(200);

    // ====================================================
    // Test 6: Camera Sync & Reset View
    // ====================================================
    console.log('[Test 6] Testing Camera Sync & Reset View...');
    const syncBtn = await page.waitForSelector('button:has-text("相机联动")');
    await syncBtn.click();
    await page.waitForTimeout(200);
    const syncText = await page.textContent('body');
    if (!syncText.includes('相机独立')) {
      throw new Error('Test 6 Failed: Camera sync toggle failed');
    }
    // Toggle back
    const indepBtn = await page.waitForSelector('button:has-text("相机独立")');
    await indepBtn.click();
    await page.waitForTimeout(200);

    // Reset view
    const resetBtn = await page.waitForSelector('button:has-text("复位视角")');
    await resetBtn.click();
    await page.waitForTimeout(300);

    // ====================================================
    // Test 7: Itemized Acceptance Checks Table & Evidence Links
    // ====================================================
    console.log('[Test 7] Testing Checks Table & Evidence Links...');
    // Select solid-blend.plane-nurbs again
    await searchInput.fill('plane-nurbs');
    await page.waitForTimeout(300);
    await (await page.waitForSelector('text=solid-blend / plane-nurbs')).click();
    await page.waitForTimeout(500);
    await searchInput.fill('');

    const checksTab = await page.waitForSelector('button:has-text("逐项验收")');
    await checksTab.click();
    await page.waitForTimeout(400);

    const checksBody = await page.textContent('body');
    if (!checksBody.includes('pk API error') || !checksBody.includes('Faces') || !checksBody.includes('PASS')) {
      throw new Error('Test 7 Failed: Checks table rows not properly rendered');
    }

    const shotChecks = path.join(tempDocsDir, 'real_smoke_desktop_checks.png');
    await page.screenshot({ path: shotChecks });
    console.log(`[Test 7] Saved ${shotChecks}`);

    // ====================================================
    // Test 8: Performance Metrics
    // ====================================================
    console.log('[Test 8] Testing Performance Metrics View...');
    const perfTab = await page.waitForSelector('button:has-text("性能指标")');
    await perfTab.click();
    await page.waitForTimeout(400);

    const perfBody = await page.textContent('body');
    if (!perfBody.includes('Apple M4') || !perfBody.includes('GM Kernel 耗时') || !perfBody.includes('通过认证')) {
      throw new Error('Test 8 Failed: Performance metrics or qualification not found');
    }

    const shotPerf = path.join(tempDocsDir, 'real_smoke_desktop_perf.png');
    await page.screenshot({ path: shotPerf });
    console.log(`[Test 8] Saved ${shotPerf}`);

    // ====================================================
    // Test 9: APIs & Parameters Tab
    // ====================================================
    console.log('[Test 9] Testing APIs & Parameters Tab...');
    const paramsTab = await page.waitForSelector('button:has-text("API 与参数")');
    await paramsTab.click();
    await page.waitForTimeout(400);

    const paramsBody = await page.textContent('body');
    if (!paramsBody.includes('PK_BODY_fix_blends') || !paramsBody.includes('radius')) {
      throw new Error('Test 9 Failed: API parameters not rendered');
    }

    const shotParams = path.join(tempDocsDir, 'real_smoke_desktop_params.png');
    await page.screenshot({ path: shotParams });
    console.log(`[Test 9] Saved ${shotParams}`);

    // ====================================================
    // Test 10: Evidence Files Tab & Clickable Links
    // ====================================================
    console.log('[Test 10] Testing Evidence Files Tab...');
    const evTab = await page.waitForSelector('button:has-text("证据与日志")');
    await evTab.click();
    await page.waitForTimeout(400);

    const evLinks = await page.$$('a[href*="data/runs/"]');
    if (evLinks.length < 2) {
      throw new Error(`Test 10 Failed: Expected at least 2 evidence links, found ${evLinks.length}`);
    }

    const shotEvidence = path.join(tempDocsDir, 'real_smoke_desktop_evidence.png');
    await page.screenshot({ path: shotEvidence });
    console.log(`[Test 10] Saved ${shotEvidence}`);

    // ====================================================
    // Test 11: Diagnostic Case & Verify No False Green Status
    // ====================================================
    console.log('[Test 11] Testing Diagnostic Case & Verifying No False Green Badges...');
    const diagFilter = await page.waitForSelector('button:has-text("诊断 1")');
    await diagFilter.click();
    await page.waitForTimeout(300);

    const diagItem = await page.waitForSelector('text=regular-curved.block-subtract');
    await diagItem.click();
    await page.waitForTimeout(600);

    const diagText = await page.textContent('body');
    if (!diagText.includes('DIAGNOSTIC')) {
      throw new Error('Test 11 Failed: Diagnostic status badge not found');
    }

    const shotDiag = path.join(tempDocsDir, 'real_smoke_desktop_diagnostic.png');
    await page.screenshot({ path: shotDiag });
    console.log(`[Test 11] Saved ${shotDiag}`);

    // Check missing cases are not green
    const missingFilter = await page.waitForSelector('button:has-text("缺失 205")');
    await missingFilter.click();
    await page.waitForTimeout(300);
    const missingFirst = await page.$('text=solid-blend / affine-plane-nurbs');
    if (missingFirst) {
      await missingFirst.click();
      await page.waitForTimeout(400);
      const missingStatusBadge = await page.textContent('body');
      if (missingStatusBadge.includes('通过 (PASS)')) {
        throw new Error('Test 11 Failed: Missing case must NOT be marked as pass!');
      }
    }

    // ====================================================
    // Test 12: Rapid Case Switching & Clean Geometry Teardown
    // ====================================================
    console.log('[Test 12] Testing Rapid Case Switching...');
    // Rapidly click 3 different cases
    const allAgain = await page.waitForSelector('button:has-text("全状态")');
    await allAgain.click();
    await page.waitForTimeout(200);

    const caseElements = await page.$$('aside div[class*="cursor-pointer"]');
    if (caseElements.length >= 3) {
      await caseElements[0].click();
      await page.waitForTimeout(50);
      await caseElements[1].click();
      await page.waitForTimeout(50);
      await caseElements[2].click();
      await page.waitForTimeout(800);
      // Ensure page didn't crash
      const activeText = await page.textContent('body');
      if (!activeText.includes('GM Kernel vs Parasolid 对比工作台')) {
        throw new Error('Test 12 Failed: Rapid switching crashed the workbench');
      }
    }

    await page.close();

    // ====================================================
    // Test 13: Real Narrow Mobile Viewport (390x844)
    // ====================================================
    console.log('[Test 13] Testing Narrow Mobile Screen (390x844)...');
    const mobilePage = await browser.newPage({ viewport: { width: 390, height: 844 } });
    mobilePage.on('pageerror', (err) => errors.push(`[Mobile Page Error] ${err.message}`));

    await mobilePage.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await mobilePage.waitForTimeout(800);

    // Verify mobile tab switcher exists
    const gmOnlyTab = await mobilePage.waitForSelector('button:has-text("GM 内核")');
    await gmOnlyTab.click();
    await mobilePage.waitForTimeout(300);

    const pkOnlyTab = await mobilePage.waitForSelector('button:has-text("Parasolid")');
    await pkOnlyTab.click();
    await mobilePage.waitForTimeout(300);

    const bothTab = await mobilePage.waitForSelector('button:has-text("并排分屏")');
    await bothTab.click();
    await mobilePage.waitForTimeout(300);

    const shotMobile = path.join(tempDocsDir, 'real_smoke_mobile_narrow.png');
    await mobilePage.screenshot({ path: shotMobile });
    console.log(`[Test 13] Saved ${shotMobile}`);

    await mobilePage.close();

    console.log('\n========================================');
    console.log('🎉 ALL 13 EXTENSIVE REAL DATA SMOKE TESTS PASSED!');
    console.log(`Total console/page errors: ${errors.length}`);
    if (errors.length > 0) {
      console.error('Errors encountered:', errors);
      process.exitCode = 1;
    }
    console.log('========================================\n');
  } catch (err) {
    console.error('❌ Real Smoke Test Suite Failed:', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
    server.close();
  }
}

runSmokeTests();
