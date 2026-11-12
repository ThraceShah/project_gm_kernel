/**
 * Live HTTPS verification for the published gm-kernel-progress portal.
 *
 * Verifies against the real Tailscale HTTPS deployment:
 *  - homepage + JS/CSS + data index/manifest + mesh + evidence all reachable,
 *  - both viewports actually render real geometry (stats + model pixels),
 *  - the real FAIL cases show their values/thresholds (never green, ratio empty),
 *  - the 390x844 layout is usable,
 *  - fresh screenshots in temp_docs for manual review.
 *
 * Browser path: SMOKE_BROWSER_PATH / PLAYWRIGHT_CHROMIUM_PATH env or auto-discovered.
 */
import { chromium } from 'playwright-core';
import path from 'path';
import zlib from 'zlib';
import fs from 'fs';
import os from 'os';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '../..');
const tempDocsDir = path.resolve(projectRoot, 'temp_docs');

if (!fs.existsSync(tempDocsDir)) {
  fs.mkdirSync(tempDocsDir, { recursive: true });
}

function resolveBrowserPath() {
  const candidates = [];
  if (process.env.SMOKE_BROWSER_PATH) candidates.push(process.env.SMOKE_BROWSER_PATH);
  if (process.env.PLAYWRIGHT_CHROMIUM_PATH) candidates.push(process.env.PLAYWRIGHT_CHROMIUM_PATH);
  try {
    const p = chromium.executablePath();
    if (p) candidates.push(p);
  } catch {
    /* ignore */
  }
  const cacheRoot =
    process.env.PLAYWRIGHT_BROWSERS_PATH || path.join(os.homedir(), '.cache', 'ms-playwright');
  if (fs.existsSync(cacheRoot)) {
    const dirs = fs.readdirSync(cacheRoot).sort().reverse();
    // prefer full chromium builds over headless-shell
    const ordered = [
      ...dirs.filter((d) => /^chromium-\d+$/.test(d)),
      ...dirs.filter((d) => /^chromium-/.test(d) && !/^chromium-\d+$/.test(d)),
      ...dirs.filter((d) => !/^chromium-/.test(d)),
    ];
    for (const d of ordered) {
      for (const rel of [
        'chrome-linux64/chrome',
        'chrome-linux/chrome',
        'chrome-headless-shell-linux64/chrome-headless-shell',
        'chrome-mac/Chromium.app/Contents/MacOS/Chromium',
      ]) {
        candidates.push(path.join(cacheRoot, d, rel));
      }
    }
  }
  candidates.push('/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome');
  const found = candidates.find((p) => p && fs.existsSync(p));
  if (!found) throw new Error('No Chromium binary found; set SMOKE_BROWSER_PATH');
  return found;
}

const chromePath = resolveBrowserPath();
const liveUrl = 'https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/';

for (const k of ['http_proxy', 'https_proxy', 'HTTP_PROXY', 'HTTPS_PROXY', 'all_proxy', 'ALL_PROXY']) {
  delete process.env[k];
}

const results = [];
function check(name, ok, detail = '') {
  results.push({ name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ` -- ${detail}` : ''}`);
}

async function canvasMetrics(page, idx) {
  return page.evaluate((i) => {
    const hosts = document.querySelectorAll('[data-testid="viewer-canvas-host"]');
    const host = hosts[i];
    const c = host && host.querySelector('canvas');
    if (!c) return null;
    const t = document.createElement('canvas');
    t.width = c.width;
    t.height = c.height;
    const ctx = t.getContext('2d');
    ctx.drawImage(c, 0, 0);
    const d = ctx.getImageData(0, 0, t.width, t.height).data;
    let bright = 0;
    for (let p = 0; p < d.length; p += 4) {
      if (Math.max(d[p], d[p + 1], d[p + 2]) >= 100) bright++;
    }
    return { bright };
  }, idx);
}

async function viewerStats(page) {
  return page.$$eval('[data-testid="viewer-canvas-host"]', (els) =>
    els.map((e) => ({
      bodyId: e.dataset.bodyId || '',
      faces: Number(e.dataset.faceCount || 0),
      triangles: Number(e.dataset.triangleCount || 0),
    }))
  );
}

// Static data members may be explicit gzip transport (.gz). Decode to the exact
// producer bytes; a corrupt member is a hard failure, never a skipped check.
async function readLiveJson(response, relPath) {
  const buf = Buffer.from(await response.body());
  const text = relPath.endsWith('.gz') ? zlib.gunzipSync(buf).toString('utf8') : buf.toString('utf8');
  return JSON.parse(text);
}

async function verifyPortalLive() {
  console.log(`[PortalVerify] browser: ${chromePath}`);
  console.log(`[PortalVerify] live URL: ${liveUrl}`);
  const browser = await chromium.launch({
    executablePath: chromePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu', '--use-gl=swiftshader', '--no-proxy-server'],
  });
  const errors = [];

  try {
    // ---- static assets reachable over real HTTPS ----
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page.on('pageerror', (err) => errors.push(`[Page Error] ${err.message}`));

    const indexRes = await page.request.get(liveUrl);
    check('https: homepage 200', indexRes.ok(), String(indexRes.status()));
    const html = await indexRes.text();
    const assetHrefs = [...html.matchAll(/(?:src|href)="([^"]+)"/g)].map((m) => m[1]);
    for (const href of assetHrefs.filter((h) => /\.(js|css)$/.test(h))) {
      const r = await page.request.get(new URL(href, liveUrl).toString());
      check(`https: asset ${href} 200`, r.ok(), String(r.status()));
    }
    const dataIndex = await page.request.get(`${liveUrl}data/index.json`);
    check('https: data/index.json 200', dataIndex.ok(), String(dataIndex.status()));
    const indexJson = await dataIndex.json();
    const latestRunId = indexJson.latestRunId;
    const latestSummary = indexJson.runs.find((r) => r.id === latestRunId);
    const manifestRes = await page.request.get(`${liveUrl}data/${latestSummary.manifest}`);
    check('https: latest manifest 200', manifestRes.ok(), String(manifestRes.status()));
    const manifest = await readLiveJson(manifestRes, latestSummary.manifest);
    const counts = { pass: 0, fail: 0, missing: 0, unsupported: 0 };
    for (const c of manifest.cases) counts[c.status] = (counts[c.status] || 0) + 1;
    console.log(`[PortalVerify] latest run ${latestRunId}: ${JSON.stringify(counts)} total=${manifest.cases.length}`);

    const meshedCase = manifest.cases.find(
      (c) => (c.status === 'pass' || c.status === 'diagnostic') && c.steps?.length > 0 && c.steps[0].gm?.before
    );
    const meshPath = `${latestSummary.manifest.replace(/\/[^/]+$/, '')}/${meshedCase.steps[0].gm.before}`;
    const meshRes = await page.request.get(`${liveUrl}data/${meshPath}`);
    check('https: mesh JSON 200', meshRes.ok(), `${meshPath} -> ${meshRes.status()}`);
    const evidencePath = `${latestSummary.manifest.replace(/\/[^/]+$/, '')}/${meshedCase.evidence[0].path}`;
    const evRes = await page.request.get(`${liveUrl}data/${evidencePath}`);
    check('https: evidence file 200', evRes.ok(), `${meshedCase.evidence[0].path} -> ${evRes.status()}`);

    // ---- real page content ----
    await page.goto(liveUrl, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(1500);
    await page.waitForFunction(
      () => {
        const hs = document.querySelectorAll('[data-testid="viewer-canvas-host"]');
        return hs.length === 2 && (hs[0].dataset.bodyId || hs[1].dataset.bodyId);
      },
      { timeout: 20000 }
    );
    const bodyText = await page.textContent('body');
    check('live: workbench title', bodyText.includes('GM Kernel vs Parasolid 对比工作台'));
    check('live: real pass count', bodyText.includes(`通过 ${counts.pass}`), `通过 ${counts.pass}`);
    check('live: real fail count', bodyText.includes(`失败 ${counts.fail}`), `失败 ${counts.fail}`);
    check('live: adapter-vs-kernel wording', bodyText.includes('非内核能力缺失'));

    const stats = await viewerStats(page);
    check('live: both viewers drew real geometry', stats.length === 2 && stats.every((s) => s.faces > 0 && s.triangles > 0), JSON.stringify(stats));
    const m0 = await canvasMetrics(page, 0);
    const m1 = await canvasMetrics(page, 1);
    check('live: GM model pixels visible', m0 && m0.bright > 3000, `bright=${m0 && m0.bright}`);
    check('live: PK model pixels visible', m1 && m1.bright > 3000, `bright=${m1 && m1.bright}`);
    await page.screenshot({ path: path.join(tempDocsDir, 'webui_v2_live_desktop.png') });

    // ---- real FAIL data visible on HTTPS ----
    const failCase = manifest.cases.find((c) => c.status === 'fail' && c.checks.some((k) => k.status === 'fail' && /Inertia/.test(k.label || '')));
    if (failCase) {
      await page.click(`[data-case-id="${failCase.id}"]`);
      await page.waitForTimeout(1200);
      await page.getByText(/逐项验收/).first().click();
      await page.waitForTimeout(300);
      await page.evaluate(() => {
        const rows = [...document.querySelectorAll('tr')];
        const row = rows.find((r) => r.textContent.includes('FAIL'));
        const body = row ? row.closest('.overflow-y-auto') : null;
        if (row && body) {
          body.scrollTop += row.getBoundingClientRect().top - body.getBoundingClientRect().top - Math.floor(body.clientHeight / 3);
          body.scrollLeft = 0;
        }
      });
      await page.waitForTimeout(300);
      const failText = await page.textContent('body');
      const inertiaFail = failCase.checks.find((k) => k.status === 'fail' && /Inertia/.test(k.label || ''));
      check('live: fail value visible', failText.includes(inertiaFail.absDelta.toExponential(7).split('e')[0]));
      check('live: fail threshold visible', failText.includes(String(inertiaFail.threshold)), String(inertiaFail.threshold));
      // A fail case may have no timing measurement at all (nothing is shown) or a
      // measurement with a withheld ratio (shown as N/A). What must never happen
      // is a fabricated numeric ratio such as "0x".
      const anyNumericRatio = /\b\d+(\.\d+)?x\b/.test(failText);
      const ratioWord = failText.includes('比率');
      const ratioNa = /比率[:：]\s*N\/A/.test(failText);
      check('live: fail ratio empty (absent or N/A, never 0x)', !anyNumericRatio && (!ratioWord || ratioNa), `ratioWord=${ratioWord} na=${ratioNa} numeric=${anyNumericRatio}`);
      await page.screenshot({ path: path.join(tempDocsDir, 'webui_v2_live_fail.png') });
    } else {
      check('live: an inertia fail case exists', false, 'not found in live manifest');
    }

    // ---- mobile usable on HTTPS ----
    const mobilePage = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await mobilePage.goto(liveUrl, { waitUntil: 'networkidle', timeout: 30000 });
    await mobilePage.waitForTimeout(1500);
    const mobOverflow = await mobilePage.evaluate(() => document.documentElement.scrollWidth);
    check('live mobile: no horizontal overflow', mobOverflow <= 390, `scrollWidth=${mobOverflow}`);
    const mobCanvas = await mobilePage.locator('[data-testid="viewer-canvas-host"]').first().boundingBox();
    check('live mobile: canvas >= 250px wide', mobCanvas.width >= 250, `w=${mobCanvas.width}`);
    await mobilePage.screenshot({ path: path.join(tempDocsDir, 'webui_v2_live_mobile.png') });
    await mobilePage.close();

    check('live: no uncaught page errors', errors.length === 0, errors.slice(0, 3).join(' | '));
    console.log('\n======================================================');
    console.log(`LIVE PORTAL VERIFICATION: ${results.filter((r) => r.ok).length}/${results.length} passed`);
    console.log(`Live URL: ${liveUrl}`);
    console.log('======================================================\n');
  } catch (err) {
    console.error('Portal verification failed:', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
  }

  const failed = results.filter((r) => !r.ok);
  if (failed.length > 0) {
    console.log('Failed checks:');
    for (const f of failed) console.log(`  - ${f.name} ${f.detail}`);
    process.exitCode = 1;
  }
  fs.writeFileSync(
    path.join(tempDocsDir, 'webui_v2_live_log.txt'),
    [`webui_v2 live portal verify -- ${new Date().toISOString()}`, `url: ${liveUrl}`, '', ...results.map((r) => `${r.ok ? 'PASS' : 'FAIL'} ${r.name}${r.detail ? ` -- ${r.detail}` : ''}`), '', `SUMMARY: ${results.length - failed.length}/${results.length} passed`].join('\n')
  );
}

verifyPortalLive();
