/**
 * Real end-to-end smoke tests for the GM vs Parasolid progress workbench.
 *
 * Focus: the real-data bugs this suite must never let regress:
 *  1. Both viewports must actually draw real geometry (not just an existing canvas):
 *     valid geometry counts == source mesh JSON, model pixels in the WebGL canvas,
 *     raycast picking returns real entity IDs.
 *  2. before -> after snapshots really change geometry and role colors are visible
 *     (yellow selected edges before, green new-blend faces after).
 *  3. Dual-camera sync works, unlock decouples, reset keeps geometry.
 *  4. Playback switches real snapshots (not just button state).
 *  5. Fast case/run switching never shows stale data; 404 clears old geometry.
 *  6. missing / adapter-unsupported / diagnostic / fail are never green, ratios stay
 *     empty (null) and the real fail numbers/thresholds/API params are visible.
 *  7. No external CDN, no dev fixture data in the production bundle.
 *  8. Desktop and 390x844 narrow layouts stay usable (drawer sidebar, wide canvas,
 *     no horizontal overflow, collapsible legend, viewable secondary metadata).
 *
 * All expected numbers are read from web/data (index + manifests) - no hardcoded
 * case counts. Browser path is configurable (SMOKE_BROWSER_PATH) or auto-discovered.
 */
import { chromium } from 'playwright-core';
import http from 'http';
import fs from 'fs';
import os from 'os';
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

// ============================================================
// Browser discovery (configurable, version-agnostic)
// ============================================================
function resolveBrowserPath() {
  const candidates = [];
  if (process.env.SMOKE_BROWSER_PATH) candidates.push(process.env.SMOKE_BROWSER_PATH);
  if (process.env.PLAYWRIGHT_CHROMIUM_PATH) candidates.push(process.env.PLAYWRIGHT_CHROMIUM_PATH);
  try {
    const p = chromium.executablePath();
    if (p) candidates.push(p);
  } catch {
    /* playwright-core may not know a browser */
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
  if (!found) {
    throw new Error('No Chromium binary found; set SMOKE_BROWSER_PATH to a chrome executable');
  }
  return found;
}

// ============================================================
// Static server for web/dist
// ============================================================
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

function startServer(port) {
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
    server.listen(port, '127.0.0.1', () => resolve(server));
  });
}

// ============================================================
// Real statistics from shipped data (never hardcoded)
// ============================================================
function loadRealStats() {
  const index = JSON.parse(fs.readFileSync(path.join(distDir, 'data', 'index.json'), 'utf8'));
  const runs = {};
  for (const r of index.runs) {
    const manifestPath = path.join(distDir, 'data', r.manifest);
    const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    const counts = { pass: 0, fail: 0, missing: 0, unsupported: 0, timeout: 0, diagnostic: 0 };
    for (const c of manifest.cases) {
      counts[c.status] = (counts[c.status] || 0) + 1;
    }
    runs[r.id] = {
      summary: r,
      manifest,
      dir: r.manifest.replace(/\/[^/]+$/, ''),
      counts,
      total: manifest.cases.length,
    };
  }
  return { index, runs };
}

function meshJson(runEntry, relPath) {
  return JSON.parse(fs.readFileSync(path.join(distDir, 'data', runEntry.dir, relPath), 'utf8'));
}

// Expected drawn geometry (mirrors the documented mesh contract: a face needs
// >= 3 vertices and >= 1 triangle, an edge needs >= 2 points).
function expectedStats(mesh) {
  const faces = (mesh.faces || []).filter(
    (f) => (f.positions || []).length >= 9 && (f.indices || []).length >= 3
  );
  const edges = (mesh.edges || []).filter((e) => (e.positions || []).length >= 6);
  const roleFaces = {};
  const roleEdges = {};
  let triangles = 0;
  for (const f of faces) {
    roleFaces[f.role] = (roleFaces[f.role] || 0) + 1;
    triangles += f.indices.length / 3;
  }
  for (const e of edges) {
    roleEdges[e.role] = (roleEdges[e.role] || 0) + 1;
  }
  return {
    faces: faces.length,
    edges: edges.length,
    triangles,
    roleFaces,
    roleEdges,
    faceIds: (mesh.faces || []).map((f) => f.id),
    edgeIds: (mesh.edges || []).map((e) => e.id),
    bodyId: mesh.bodyId,
  };
}

function pickCases(runEntry) {
  const cs = runEntry.manifest.cases;
  const hasAfter = (c) => {
    try {
      const after = meshJson(runEntry, c.steps[0].gm.after);
      return after.faces.some((f) => f.role === 'newBlend');
    } catch {
      return false;
    }
  };
  const blend = cs.find(
    (c) =>
      (c.status === 'pass' || c.status === 'diagnostic') &&
      c.steps?.length > 0 &&
      c.steps[0].gm?.before &&
      c.steps[0].gm?.after &&
      (c.steps[0].gm.selectedEdgeTags || []).length > 0 &&
      hasAfter(c)
  );
  const boolean = cs.find(
    (c) => c.status === 'pass' && c.steps?.length > 0 && (c.steps[0].gm?.tools || []).length > 0
  );
  const fails = cs.filter((c) => c.status === 'fail');
  const failCompare = fails.find((c) =>
    c.checks.some((k) => k.status === 'fail' && /BODY_compare|strict/.test(`${k.label} ${k.note}`))
  );
  const failBodyCheck = fails.find((c) =>
    c.checks.some((k) => k.status === 'fail' && /13826/.test(k.note || ''))
  );
  const failInertia = fails.find((c) =>
    c.checks.some((k) => k.status === 'fail' && /Inertia/.test(k.label || '') && k.threshold === 1e-7)
  );
  const missing = cs.find((c) => c.status === 'missing');
  const unsupported = cs.find((c) => c.status === 'unsupported');
  const diagnostic = cs.find((c) => c.status === 'diagnostic');
  return { blend, boolean, fails, failCompare, failBodyCheck, failInertia, missing, unsupported, diagnostic };
}

// ============================================================
// Assertion plumbing
// ============================================================
const results = [];
function check(name, ok, detail = '') {
  results.push({ name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ` -- ${detail}` : ''}`);
}

// ============================================================
// In-page measurement helpers (read the real WebGL canvas pixels)
// ============================================================
async function canvasMetrics(page, idx) {
  return page.evaluate((i) => {
    const hosts = document.querySelectorAll('[data-testid="viewer-canvas-host"]');
    const host = hosts[i];
    if (!host) return null;
    const c = host.querySelector('canvas');
    if (!c) return null;
    const t = document.createElement('canvas');
    t.width = c.width;
    t.height = c.height;
    const ctx = t.getContext('2d');
    ctx.drawImage(c, 0, 0);
    const d = ctx.getImageData(0, 0, t.width, t.height).data;
    let sig = 0;
    let bright = 0;
    let yellow = 0;
    let green = 0;
    let blue = 0;
    for (let p = 0; p < d.length; p += 16) {
      sig = (sig * 31 + d[p] * 65536 + d[p + 1] * 256 + d[p + 2]) % 1000000007;
    }
    const w = t.width;
    const h = t.height;
    const greenMask = new Uint8Array(w * h);
    for (let p = 0, q = 0; p < d.length; p += 4, q++) {
      const r = d[p];
      const g = d[p + 1];
      const b = d[p + 2];
      if (Math.max(r, g, b) >= 100) bright++;
      // yellow selected-edge hue (excludes red axis and orange modified faces)
      if (r > 140 && g > 110 && b < 90 && r - b > 80 && Math.abs(r - g) < 80) yellow++;
      // green-dominant hue (new-blend faces incl. dark shaded sides)
      if (g > r + 15 && g > b + 15 && g > 40) {
        green++;
        greenMask[q] = 1;
      }
      // blue selected/support face hue
      if (b > 120 && b - r > 50 && b - g > 30) blue++;
    }
    // green REGION pixels: dense green neighborhoods only. The green y-axis
    // helper is a ~2px wide line (< 25 green px in a 7x7 window); real blend
    // faces are filled regions (>= 25 green px in the window). Calibrated on
    // real data: before-state regions = 0, blend-facing pose = thousands.
    const integ = new Int32Array((w + 1) * (h + 1));
    for (let y = 0; y < h; y++) {
      let rowSum = 0;
      for (let x = 0; x < w; x++) {
        rowSum += greenMask[y * w + x];
        integ[(y + 1) * (w + 1) + (x + 1)] = integ[y * (w + 1) + (x + 1)] + rowSum;
      }
    }
    let greenRegion = 0;
    const R = 3;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!greenMask[y * w + x]) continue;
        const x0 = Math.max(0, x - R);
        const x1 = Math.min(w - 1, x + R);
        const y0 = Math.max(0, y - R);
        const y1 = Math.min(h - 1, y + R);
        const s =
          integ[(y1 + 1) * (w + 1) + (x1 + 1)] -
          integ[y0 * (w + 1) + (x1 + 1)] -
          integ[(y1 + 1) * (w + 1) + x0] +
          integ[y0 * (w + 1) + x0];
        if (s >= 25) greenRegion++;
      }
    }
    return { sig, bright, yellow, green, greenRegion, blue, w, h };
  }, idx);
}

async function viewerStats(page) {
  return page.$$eval('[data-testid="viewer-canvas-host"]', (els) =>
    els.map((e) => {
      const root = e.closest('[data-testid="viewer-root"]');
      return {
        viewer: root ? root.dataset.viewer : '?',
        bodyId: e.dataset.bodyId || '',
        faces: Number(e.dataset.faceCount || 0),
        edges: Number(e.dataset.edgeCount || 0),
        triangles: Number(e.dataset.triangleCount || 0),
        roleFaces: JSON.parse(e.dataset.roleFaces || '{}'),
        roleEdges: JSON.parse(e.dataset.roleEdges || '{}'),
      };
    })
  );
}

async function waitForMeshes(page, timeout = 15000) {
  await page.waitForFunction(
    () => {
      const hosts = document.querySelectorAll('[data-testid="viewer-canvas-host"]');
      return hosts.length === 2 && (hosts[0].dataset.bodyId || hosts[1].dataset.bodyId);
    },
    { timeout }
  );
  await page.waitForTimeout(300);
}

function bbox(page, selector) {
  return page.locator(selector).first().boundingBox();
}

async function dragViewer(page, idx, dx, dy) {
  const host = page.locator('[data-testid="viewer-canvas-host"]').nth(idx);
  const box = await host.boundingBox();
  const cx = box.x + box.width / 2;
  const cy = box.y + box.height / 2;
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + dx, cy + dy, { steps: 12 });
  await page.mouse.up();
  await page.waitForTimeout(800);
}

// Deterministic camera rotation sweep: returns metrics per pose and keeps the
// full-page screenshot of the pose where `metric` peaks (role colors can be
// occluded from a single fixed viewpoint on real models).
async function sweepAndCapture(page, metric, tag) {
  const poses = [];
  let best = { value: -1, shot: null, metrics: null };
  const measure = async (i) => {
    const m = await canvasMetrics(page, 0);
    const p = path.join(tempDocsDir, `.sweep_${tag}_${i}.png`);
    await page.screenshot({ path: p });
    poses.push(p);
    if (m && m[metric] > best.value) best = { value: m ? m[metric] : -1, shot: p, metrics: m };
    return m;
  };
  const first = await measure(0);
  const maxes = { yellow: first.yellow, green: first.green, greenRegion: first.greenRegion, bright: first.bright };
  for (let i = 1; i <= 5; i++) {
    await dragViewer(page, 0, i === 4 ? 0 : 150, i === 4 ? -120 : 0);
    const m = await measure(i);
    if (m) {
      maxes.yellow = Math.max(maxes.yellow, m.yellow);
      maxes.green = Math.max(maxes.green, m.green);
      maxes.greenRegion = Math.max(maxes.greenRegion, m.greenRegion);
      maxes.bright = Math.max(maxes.bright, m.bright);
    }
  }
  return { poses, best, maxes };
}

function keepBestShot(poses, best, destName) {
  const dest = path.join(tempDocsDir, destName);
  if (best.shot) fs.copyFileSync(best.shot, dest);
  for (const p of poses) {
    try {
      fs.unlinkSync(p);
    } catch {}
  }
  return dest;
}

// ============================================================
// Main
// ============================================================
async function inspectionChecks(page, latest, shot) {
  const select = async (id) => {
    await page.click(`[data-case-id="${id}"]`);
    await waitForMeshes(page);
  };
  const camera = () => page.$$eval('[data-testid="viewer-canvas-host"]', (els) => els.map((e) => e.dataset.cameraPos));
  const blend = 'solid-blend.plane-nurbs';
  await select(blend);
  await page.getByText('复位视角').click();
  await page.waitForTimeout(500);
  await dragViewer(page, 0, 85, 45);
  const orbit = await camera();
  await page.getByText('后态 (After)').click();
  await waitForMeshes(page);
  const after = await camera();
  for (let i = 0; i < 2; i++) {
    const a = orbit[i].split(',').map(Number), b = after[i].split(',').map(Number);
    check(`V3: reset then orbit then snapshot preserves camera ${i}`, Math.hypot(...a.map((x, j) => x - b[j])) < 0.05, JSON.stringify({ orbit: orbit[i], after: after[i] }));
  }
  await page.screenshot({ path: shot('inspection-reset-snapshot.png') });

  // Find an actual unchanged entity by raycast; a missed pick must fail the check.
  await page.getByText('复位视角').click();
  await page.waitForTimeout(400);
  const host = await bbox(page, '[data-testid="viewer-canvas-host"]');
  const mesh = meshJson(latest, latest.manifest.cases.find((c) => c.id === blend).steps[0].gm.after);
  const unchanged = new Set([...mesh.faces, ...mesh.edges].filter((e) => e.role === 'unchanged').map((e) => e.id));
  let picked = '';
  for (const y of [0.5, 0.6, 0.4, 0.7, 0.3]) {
    for (const x of [0.5, 0.6, 0.4, 0.7, 0.3]) {
      await page.mouse.click(host.x + host.width * x, host.y + host.height * y);
      const text = await page.locator('[data-testid="entity-inspector"]').textContent().catch(() => '');
      picked = text.match(/\[(.+?)\]/)?.[1] || '';
      if (unchanged.has(picked)) break;
    }
    if (unchanged.has(picked)) break;
  }
  check('V8: selected a real unchanged entity before hiding', unchanged.has(picked), picked);
  await page.locator('[data-testid="btn-toggle-isolate"]').first().click();
  await page.waitForTimeout(250);
  check('V8: hidden selection is cleared', await page.locator('[data-testid="entity-inspector"]').count() === 0);
  await page.screenshot({ path: shot('inspection-isolate-selection.png') });
  await page.locator('[data-testid="btn-toggle-isolate"]').first().click();
  await page.mouse.click(host.x + host.width / 2, host.y + host.height / 2);
  check('V8: picked another real entity before case switch', await page.locator('[data-testid="entity-inspector"]').count() === 1);
  await select('planar-boolean.overlap-two-axes-subtract');
  check('V8: case switch clears snapshot-local selection', await page.locator('[data-testid="entity-inspector"]').count() === 0);
  await page.getByText('前态 (Before)').click();
  await waitForMeshes(page);
  const withTools = await viewerStats(page), withCamera = await camera();
  const withPixels = await canvasMetrics(page, 0);
  await page.screenshot({ path: shot('inspection-tools-on.png') });
  await page.locator('[data-testid="btn-toggle-tools"]').first().click();
  await page.waitForTimeout(300);
  const without = await viewerStats(page), withoutCamera = await camera();
  const withoutPixels = await canvasMetrics(page, 0);
  const bool = latest.manifest.cases.find((c) => c.id === 'planar-boolean.overlap-two-axes-subtract');
  for (let i = 0; i < 2; i++) {
    const side = i === 0 ? 'gm' : 'pk';
    const expected = expectedStats(meshJson(latest, bool.steps[0][side].before));
    check(`V7: hide tools leaves exact primary geometry ${side}`, without[i].faces === expected.faces && without[i].edges === expected.edges && without[i].triangles === expected.triangles, JSON.stringify({ with: withTools[i], without: without[i], expected }));
    check(`V7: tools geometry actually removed ${side}`, withTools[i].faces > without[i].faces && withTools[i].edges > without[i].edges);
    check(`V7: tool toggle preserves camera ${side}`, withCamera[i] === withoutCamera[i]);
  }
  check('V7: tool toggle changes actual canvas pixels', withPixels.sig !== withoutPixels.sig, JSON.stringify({ before: withPixels.sig, after: withoutPixels.sig }));
  await page.screenshot({ path: shot('inspection-tools-off.png') });
  await page.locator('[data-testid="btn-toggle-tools"]').first().click();
  await page.waitForTimeout(250);
  check('V7: restoring tools restores geometry', JSON.stringify(await viewerStats(page)) === JSON.stringify(withTools));

  await page.setViewportSize({ width: 320, height: 844 });
  await page.waitForTimeout(350);
  const panes = await page.locator('[data-testid="viewer-canvas-host"]').evaluateAll((els) => els.map((e) => ({ h: e.clientHeight, w: e.clientWidth })));
  check('mobile: both panes retain equal usable height', panes.length === 2 && Math.abs(panes[0].h - panes[1].h) <= 2 && panes.every((p) => p.h > 90), JSON.stringify(panes));
  const shell = await page.locator('[data-testid="sidebar-shell"]').boundingBox();
  check('mobile: closed sidebar is fully outside viewport', shell.x + shell.width <= 1, JSON.stringify(shell));
  await page.screenshot({ path: shot('inspection-mobile-320.png') });
}

async function run() {
  const chromePath = resolveBrowserPath();
  const port = 8089;
  const server = await startServer(port);
  const baseUrl = `http://127.0.0.1:${port}`;
  const { index, runs } = loadRealStats();
  const latestId = index.latestRunId;
  const latest = runs[latestId];
  const cases = pickCases(latest);
  const shot = (name) => path.join(tempDocsDir, name);

  console.log(`[Smoke] browser: ${chromePath}`);
  console.log(`[Smoke] latest run: ${latestId} counts: ${JSON.stringify(latest.counts)} total=${latest.total}`);
  for (const [id, c] of Object.entries(cases)) {
    if (c && c.id) console.log(`[Smoke] case[${id}]: ${c.id}`);
  }

  check('data: blend case with selected edges + newBlend faces exists', !!cases.blend);
  check('data: two or more real fail cases exist', cases.fails.length >= 2, `fail=${cases.fails.length}`);
  check('data: strict-compare fail case exists', !!cases.failCompare);
  check('data: BODY_check 13826 fail case exists', !!cases.failBodyCheck);
  check('data: inertia-threshold fail case exists', !!cases.failInertia);

  const browser = await chromium.launch({
    executablePath: chromePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu', '--use-gl=swiftshader', '--no-proxy-server'],
  });
  const pageErrors = [];

  try {
    // ==========================================================
    // Desktop page
    // ==========================================================
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page.on('pageerror', (err) => pageErrors.push(`[Page Error] ${err.message}`));
    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(800);
    await waitForMeshes(page);

    if (process.argv.includes('--inspection-only')) {
      await inspectionChecks(page, latest, shot);
      check('runtime: no uncaught page errors', pageErrors.length === 0, pageErrors.join(' | '));
      fs.writeFileSync(shot('inspection-remediation.json'), JSON.stringify(results, null, 2));
      if (results.some((r) => !r.ok)) process.exitCode = 1;
      return;
    }

    // ---- T1: real workbench + real statistics from index/manifest ----
    const bodyText = await page.textContent('body');
    check('desktop: workbench title', bodyText.includes('GM Kernel vs Parasolid 对比工作台'));
    check(
      'desktop: no dev fixture toggle in production build',
      (await page.$('text=演练模式 (DEV FIXTURE)')) === null
    );
    check('desktop: sidebar shows real pass count', bodyText.includes(`通过 ${latest.counts.pass}`), `通过 ${latest.counts.pass}`);
    check('desktop: sidebar shows real fail count', bodyText.includes(`失败 ${latest.counts.fail}`), `失败 ${latest.counts.fail}`);
    check('desktop: sidebar shows real missing count', bodyText.includes(`缺失 ${latest.counts.missing}`), `缺失 ${latest.counts.missing}`);
    check('desktop: sidebar shows real adapter-unsupported count', bodyText.includes(`未适配 ${latest.counts.unsupported}`), `未适配 ${latest.counts.unsupported}`);
    check('desktop: sidebar total matches manifest', bodyText.includes(`${latest.total} / ${latest.total} 项`));
    check('desktop: adapter-vs-kernel semantics note present', bodyText.includes('非内核能力缺失'));

    // ---- T2: both viewports draw REAL geometry (counts + pixels) ----
    // Default selected case = first pass/diagnostic case with steps (same rule as app)
    const defaultCase =
      latest.manifest.cases.find((c) => (c.status === 'pass' || c.status === 'diagnostic') && c.steps?.length > 0) ||
      latest.manifest.cases[0];
    const gmExpected = expectedStats(meshJson(latest, defaultCase.steps[0].gm.before));
    const pkExpected = expectedStats(meshJson(latest, defaultCase.steps[0].pk.before));
    const stats0 = await viewerStats(page);
    check('render: two viewers present', stats0.length === 2);
    const gm0 = stats0.find((s) => s.viewer === 'gm');
    const pk0 = stats0.find((s) => s.viewer === 'pk');
    check('render: GM bodyId from real data', gm0 && gm0.bodyId === gmExpected.bodyId, `${gm0 && gm0.bodyId} vs ${gmExpected.bodyId}`);
    check('render: PK bodyId from real data', pk0 && pk0.bodyId === pkExpected.bodyId, `${pk0 && pk0.bodyId} vs ${pkExpected.bodyId}`);
    check('render: GM face count == mesh JSON', gm0 && gm0.faces === gmExpected.faces, `${gm0 && gm0.faces} vs ${gmExpected.faces}`);
    check('render: GM edge count == mesh JSON', gm0 && gm0.edges === gmExpected.edges, `${gm0 && gm0.edges} vs ${gmExpected.edges}`);
    check('render: GM triangle count == mesh JSON', gm0 && gm0.triangles === gmExpected.triangles, `${gm0 && gm0.triangles} vs ${gmExpected.triangles}`);
    check('render: PK face/edge/triangle counts == mesh JSON', pk0 && pk0.faces === pkExpected.faces && pk0.edges === pkExpected.edges && pk0.triangles === pkExpected.triangles, JSON.stringify({ pk0: { f: pk0 && pk0.faces, e: pk0 && pk0.edges, t: pk0 && pk0.triangles }, pkExpected }));
    check('render: GM role face counts == mesh JSON', gm0 && JSON.stringify(gm0.roleFaces) === JSON.stringify(gmExpected.roleFaces), JSON.stringify({ got: gm0 && gm0.roleFaces, exp: gmExpected.roleFaces }));
    const mGm = await canvasMetrics(page, 0);
    const mPk = await canvasMetrics(page, 1);
    check('pixels: GM model pixels visible', mGm && mGm.bright > 3000, `bright=${mGm && mGm.bright}`);
    check('pixels: PK model pixels visible', mPk && mPk.bright > 3000, `bright=${mPk && mPk.bright}`);
    await page.screenshot({ path: shot('webui_v2_desktop_workbench.png') });

    // ---- T3: before -> after real change + role colors visible ----
    await page.click(`[data-case-id="${cases.blend.id}"]`);
    await waitForMeshes(page);
    await page.waitForTimeout(400);
    const gmBeforeId = meshJson(latest, cases.blend.steps[0].gm.before).bodyId;
    const gmAfterId = meshJson(latest, cases.blend.steps[0].gm.after).bodyId;
    let st = (await viewerStats(page))[0];
    check('before: GM shows before body', st.bodyId === gmBeforeId, `${st.bodyId} vs ${gmBeforeId}`);
    const beforeExpected = expectedStats(meshJson(latest, cases.blend.steps[0].gm.before));
    check('before: selected edges present in draw group', (st.roleEdges.selected || 0) >= 1, JSON.stringify(st.roleEdges));
    const mBefore = await canvasMetrics(page, 0);
    check('before: yellow selected-edge pixels visible', mBefore.yellow >= 80, `yellow=${mBefore.yellow}`);
    check('before: support/selected face pixels visible', mBefore.blue >= 3000, `blue=${mBefore.blue}`);

    await page.getByText('后态 (After)').click();
    await page.waitForTimeout(1200);
    st = (await viewerStats(page))[0];
    const afterExpected = expectedStats(meshJson(latest, cases.blend.steps[0].gm.after));
    check('after: GM shows after body', st.bodyId === gmAfterId, `${st.bodyId} vs ${gmAfterId}`);
    check('after: newBlend faces in draw group', (st.roleFaces.newBlend || 0) >= 1, JSON.stringify(st.roleFaces));
    check('after: modified faces in draw group', (st.roleFaces.modified || 0) >= 1, JSON.stringify(st.roleFaces));
    check('after: face/edge/triangle counts == after mesh JSON', st.faces === afterExpected.faces && st.edges === afterExpected.edges && st.triangles === afterExpected.triangles, JSON.stringify({ got: { f: st.faces, e: st.edges, t: st.triangles }, exp: afterExpected }));
    const mAfter = await canvasMetrics(page, 0);
    check('after: yellow selected-edge pixels consumed by blend', mAfter.yellow < mBefore.yellow, `before=${mBefore.yellow} after=${mAfter.yellow}`);
    check('after: snapshot really changed pixels', mAfter.sig !== mBefore.sig, `before=${mBefore.sig} after=${mAfter.sig}`);

    // Rotation sweep: role colors must be visible from a realistic inspection
    // angle (a blend face can be occluded from the default viewpoint).
    await page.getByText('前态 (Before)').click();
    await page.waitForTimeout(800);
    await page.getByText('复位视角').click();
    await page.waitForTimeout(500);
    const beforeSweep = await sweepAndCapture(page, 'yellow', 'before');
    keepBestShot(beforeSweep.poses, beforeSweep.best, 'webui_v2_desktop_blend_before.png');
    check('before: yellow selected edges visible (rotation sweep)', beforeSweep.maxes.yellow >= 80, `maxYellow=${beforeSweep.maxes.yellow}`);
    check('before: support faces visible (rotation sweep)', beforeSweep.maxes.bright > 3000, `maxBright=${beforeSweep.maxes.bright}`);
    check('before: no green blend regions yet', beforeSweep.maxes.greenRegion < 100, `maxGreenRegion=${beforeSweep.maxes.greenRegion}`);

    await page.getByText('后态 (After)').click();
    await page.waitForTimeout(800);
    await page.getByText('复位视角').click();
    await page.waitForTimeout(500);
    const afterSweep = await sweepAndCapture(page, 'greenRegion', 'after');
    keepBestShot(afterSweep.poses, afterSweep.best, 'webui_v2_desktop_blend_after.png');
    check('after: green new-blend region visible (rotation sweep)', afterSweep.maxes.greenRegion >= 100, `maxGreenRegion=${afterSweep.maxes.greenRegion}`);
    check('after: green region grew vs before (axis-line noise filtered)', afterSweep.maxes.greenRegion - beforeSweep.maxes.greenRegion >= 80, `before=${beforeSweep.maxes.greenRegion} after=${afterSweep.maxes.greenRegion}`);

    // ---- T4: raycast picking returns REAL entity IDs ----
    await page.getByText('复位视角').click();
    await page.waitForTimeout(500);
    const host0 = await bbox(page, '[data-testid="viewer-canvas-host"]');
    await page.mouse.click(host0.x + host0.width / 2, host0.y + host0.height / 2);
    await page.waitForTimeout(300);
    const inspector = await page.$('[data-testid="entity-inspector"]');
    check('pick: entity inspector appears on canvas click', !!inspector);
    if (inspector) {
      const txt = await inspector.textContent();
      const m = txt.match(/\[(.+?)\]/);
      const pickedId = m ? m[1] : '';
      const realIds = new Set([...afterExpected.faceIds, ...afterExpected.edgeIds]);
      check('pick: picked ID is a real mesh entity ID', realIds.has(pickedId), `picked=${pickedId}`);
      check('pick: role label shown', /角色/.test(txt), txt.trim().slice(0, 80));
    }

    // ---- T5: camera sync + independence + reset keeps geometry ----
    const s0 = await canvasMetrics(page, 0);
    const s1 = await canvasMetrics(page, 1);
    await dragViewer(page, 0, 120, 60);
    const d0 = await canvasMetrics(page, 0);
    const d1 = await canvasMetrics(page, 1);
    check('sync: dragging GM moves GM', d0.sig !== s0.sig);
    check('sync: dragging GM also moves PK (linked)', d1.sig !== s1.sig);
    await page.mouse.move(host0.x + host0.width / 2, host0.y + host0.height / 2);
    await page.mouse.wheel(0, -240);
    await page.waitForTimeout(800);
    const z0 = await canvasMetrics(page, 0);
    const z1 = await canvasMetrics(page, 1);
    check('sync: zooming GM also zooms PK (linked)', z0.sig !== d0.sig && z1.sig !== d1.sig);
    // unlock
    await page.getByText('相机联动').click();
    await page.waitForTimeout(200);
    const u1 = await canvasMetrics(page, 1);
    await dragViewer(page, 0, -100, 40);
    const u0b = await canvasMetrics(page, 0);
    const u1b = await canvasMetrics(page, 1);
    check('independent: dragging GM moves GM after unlock', u0b.sig !== u1.sig);
    check('independent: PK does NOT follow after unlock', u1b.sig === u1.sig, `before=${u1.sig} after=${u1b.sig}`);
    // reset keeps geometry
    await page.getByText('复位视角').click();
    await page.waitForTimeout(600);
    const r0 = await canvasMetrics(page, 0);
    const r1 = await canvasMetrics(page, 1);
    check('reset: geometry still present after reset', r0.bright > 3000 && r1.bright > 3000, `gm=${r0.bright} pk=${r1.bright}`);
    check('reset: view actually reset (pixels changed)', r0.sig !== u0b.sig);

    // ==========================================================
    // Operation-Aware Initial View, Inspection Modes & True U/V Isoparametrics
    // ==========================================================
    // 1. Operation-Aware View & Camera Persistence
    const blendCamPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('op-view: initial camera is operation-aware, not fixed at (5,5,5)',
      blendCamPos && blendCamPos !== '5.00,5.00,5.00' && blendCamPos !== '15.00,15.00,20.00',
      `blendCamPos=${blendCamPos}`
    );

    // Camera Persistence: user rotates camera, then toggles step -> camera does NOT reset
    await dragViewer(page, 0, 70, 40);
    await page.waitForTimeout(200);
    const userRotatedCamPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('camera: user rotation registered', userRotatedCamPos && userRotatedCamPos !== blendCamPos, `rotated=${userRotatedCamPos}`);

    // Switch step (After)
    await page.getByText('后态 (After)').click();
    await waitForMeshes(page);
    await page.waitForTimeout(400);
    const afterSwitchCamPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    const uCoords = userRotatedCamPos.split(',').map(Number);
    const aCoords = afterSwitchCamPos.split(',').map(Number);
    const camDist = Math.hypot(uCoords[0] - aCoords[0], uCoords[1] - aCoords[1], uCoords[2] - aCoords[2]);
    check('camera: step switch does NOT steal camera from user rotation',
      camDist < 0.05,
      `expected=${userRotatedCamPos} got=${afterSwitchCamPos} dist=${camDist.toFixed(3)}`
    );

    // 2. Viewport Toolbar & Focus Actions
    // "查看修改"
    await page.locator('[data-testid="btn-focus-op"]').first().click();
    await page.waitForTimeout(400);
    const opFocusedPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('toolbar: "查看修改" adjusts camera to operation focus', opFocusedPos && opFocusedPos !== afterSwitchCamPos, `opFocused=${opFocusedPos}`);

    // "全模型"
    await page.locator('[data-testid="btn-focus-full"]').first().click();
    await page.waitForTimeout(400);
    const fullFocusedPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('toolbar: "全模型" adjusts camera to full model bounding box', fullFocusedPos && fullFocusedPos !== opFocusedPos, `fullFocused=${fullFocusedPos}`);

    // "标准视图" (Top view)
    await page.locator('[data-testid="select-standard-view"]').first().selectOption('top');
    await page.waitForTimeout(400);
    const topViewPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('toolbar: standard top view adjusts camera along Y normal', topViewPos && topViewPos !== fullFocusedPos, `topView=${topViewPos}`);
    await page.screenshot({ path: shot('webui_v2_standard_top.png') });

    // "复位"
    await page.locator('[data-testid="btn-reset-view"]').first().click();
    await page.waitForTimeout(400);
    const resetCamPos = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.cameraPos);
    check('toolbar: "复位" restores initial operation-aware view',
      resetCamPos && (resetCamPos === opFocusedPos || resetCamPos === blendCamPos),
      `reset=${resetCamPos} opFocused=${opFocusedPos} blendCamPos=${blendCamPos}`
    );

    // 3. Shading Modes & Transparency
    const initOpacity = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.dimmedOpacity);
    check('shading: unchanged context is semi-transparent by default (0.25)', initOpacity === '0.25', `opacity=${initOpacity}`);
    await page.screenshot({ path: shot('webui_v2_dimmed_context_after.png') });

    // Toggle dimming off -> opaque
    await page.locator('[data-testid="btn-toggle-dim"]').first().click();
    await page.waitForTimeout(200);
    const opaqueVal = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.dimmedOpacity);
    check('shading: toggling dim button switches context to 1.0 opaque', opaqueVal === '1', `opaqueVal=${opaqueVal}`);

    // Toggle dimming back on
    await page.locator('[data-testid="btn-toggle-dim"]').first().click();
    await page.waitForTimeout(200);

    // Toggle "仅看修改"
    await page.locator('[data-testid="btn-toggle-isolate"]').first().click();
    await page.waitForTimeout(300);
    const isolatedStats = (await viewerStats(page))[0];
    check('shading: "仅看修改" isolates modified & blend faces without unchanged context',
      isolatedStats.roleFaces.unchanged === undefined || isolatedStats.roleFaces.unchanged === 0,
      JSON.stringify(isolatedStats.roleFaces)
    );
    await page.screenshot({ path: shot('webui_v2_isolated_modified.png') });

    // Restore full model context
    await page.locator('[data-testid="btn-toggle-isolate"]').first().click();
    await page.waitForTimeout(300);

    // 4. True Surface U/V Isoparametric Lines
    const hasIso = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => el.dataset.hasIsoparametrics);
    const isoCount = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => Number(el.dataset.isoparametricsCount || 0));
    check('isoparametrics: solid-blend.plane-nurbs has true U/V parameter curves', hasIso === 'true', `hasIso=${hasIso}`);
    check('isoparametrics: curve segment count > 0 from Parasolid B-rep evaluation', isoCount > 0, `isoCount=${isoCount}`);

    const badgeText = await page.textContent('[data-testid="viewer-iso-badge"]');
    check('isoparametrics: U/V indicator badge shown on viewport banner', badgeText && badgeText.includes('等参线'), `badgeText=${badgeText}`);
    await page.screenshot({ path: shot('webui_v2_op_aware_blend_after.png') });

    // Toggle isoparametrics off
    await page.locator('[data-testid="btn-toggle-iso"]').first().click();
    await page.waitForTimeout(200);
    const isoCountOff = await page.$eval('[data-testid="viewer-canvas-host"]', (el) => Number(el.dataset.isoparametricsCount || 0));
    check('isoparametrics: toggle button hides isoparametric curves cleanly', isoCountOff === 0, `isoCountOff=${isoCountOff}`);

    // Toggle isoparametrics back on
    await page.locator('[data-testid="btn-toggle-iso"]').first().click();
    await page.waitForTimeout(200);

    // Switch back to Before to test and capture Before state
    await page.getByText('前态 (Before)').click();
    await waitForMeshes(page);
    await page.waitForTimeout(400);
    await page.screenshot({ path: shot('webui_v2_op_aware_blend_before.png') });

    // ---- T6: playback switches real snapshots ----
    await page.locator('button[title="自动按快照播放"]').click();
    const seenRoles = new Set();
    const seenCounters = new Set();
    for (let i = 0; i < 5; i++) {
      await page.waitForTimeout(900);
      const s = (await viewerStats(page))[0];
      seenRoles.add(JSON.stringify(s.roleFaces));
      const counter = await page.locator('input[type="range"]').first().inputValue();
      seenCounters.add(counter);
    }
    check('playback: real snapshots switch during playback', seenRoles.size >= 2, [...seenRoles].join(' | '));
    check('playback: timeline counter advances', seenCounters.size >= 2, [...seenCounters].join(','));
    await page.locator('button[title="暂停"]').click();
    await page.waitForTimeout(400);
    const p0 = await canvasMetrics(page, 0);
    await page.waitForTimeout(1500);
    const p1 = await canvasMetrics(page, 0);
    check('playback: pause freezes the snapshot', p0.sig === p1.sig, `${p0.sig} vs ${p1.sig}`);

    // ---- T7: boolean case with tool bodies ----
    if (cases.boolean) {
      await page.click(`[data-case-id="${cases.boolean.id}"]`);
      await waitForMeshes(page);
      await page.waitForTimeout(400);
      const combinedExpected = expectedStats(meshJson(latest, cases.boolean.steps[0].gm.before));
      const toolExpected = cases.boolean.steps[0].gm.tools.map((t) => expectedStats(meshJson(latest, t)));
      const bst = (await viewerStats(page))[0];
      const expFaces = combinedExpected.faces + toolExpected.reduce((a, t) => a + t.faces, 0);
      const expTris = combinedExpected.triangles + toolExpected.reduce((a, t) => a + t.triangles, 0);
      check('boolean: tool bodies rendered in before state', bst.faces === expFaces && bst.triangles === expTris, JSON.stringify({ got: { f: bst.faces, t: bst.triangles }, exp: { f: expFaces, t: expTris } }));
      const mb = await canvasMetrics(page, 0);
      check('boolean: model pixels visible', mb.bright > 3000, `bright=${mb.bright}`);
      await page.screenshot({ path: shot('webui_v2_desktop_boolean_tools.png') });
    } else {
      check('boolean: case with tool bodies exists', false, 'no pass case with gm.tools in run');
    }

    // ---- T8: the real fail cases - values, thresholds, API params, logs ----
    const failShots = [
      [cases.failCompare, 'webui_v2_desktop_fail_compare.png'],
      [cases.failBodyCheck, 'webui_v2_desktop_fail_bodycheck.png'],
      [cases.failInertia, 'webui_v2_desktop_fail_numeric.png'],
    ];
    for (const [failCase, shotName] of failShots) {
      if (!failCase) continue;
      await page.click(`[data-case-id="${failCase.id}"]`);
      await waitForMeshes(page);
      await page.waitForTimeout(300);
      const topbar = await page.textContent('[data-testid="case-topbar"]');
      check(`fail[${failCase.id}]: badge is 失败 (FAIL)`, topbar.includes('失败 (FAIL)'));
      check(`fail[${failCase.id}]: ratio empty (N/A, never 0x)`, topbar.includes('比率: N/A') && !/\b0(\.0+)?x\b/.test(topbar));
      const badgeClass = await page.$eval('[data-testid="case-topbar"] span.rounded', (el) => el.className);
      check(`fail[${failCase.id}]: badge not green`, !/emerald/.test(badgeClass), badgeClass.slice(0, 60));

      // checks tab: real diffs / thresholds
      await page.getByText(/逐项验收/).first().click();
      await page.waitForTimeout(200);
      const checksText = await page.textContent('body');
      const failChecks = failCase.checks.filter((k) => k.status === 'fail');
      for (const k of failChecks.slice(0, 4)) {
        check(`fail[${failCase.id}]: fail check visible: ${k.label}`, checksText.includes(k.label), k.label);
      }
      if (/13826/.test(JSON.stringify(failCase.checks))) {
        check(`fail[${failCase.id}]: bad_loops state 13826 visible`, checksText.includes('13826'));
      }
      const inertiaFail = failCase.checks.find((k) => k.status === 'fail' && /Inertia/.test(k.label || ''));
      if (inertiaFail) {
        const deltaPrefix = inertiaFail.absDelta.toExponential(7).split('e')[0];
        check(`fail[${failCase.id}]: inertia absDelta value visible (${deltaPrefix})`, checksText.includes(deltaPrefix), deltaPrefix);
        check(`fail[${failCase.id}]: original threshold visible (${inertiaFail.threshold})`, checksText.includes(String(inertiaFail.threshold)), String(inertiaFail.threshold));
        check(`fail[${failCase.id}]: unit visible`, checksText.includes(inertiaFail.unit), inertiaFail.unit);
      }
      const compareFail = failCase.checks.find((k) => k.status === 'fail' && /BODY_compare|strict/.test(`${k.label} ${k.note}`));
      if (compareFail) {
        check(`fail[${failCase.id}]: strict-compare difference note visible`, checksText.includes(compareFail.note), compareFail.note);
      }
      // scroll the first actual FAIL row into view for the evidence screenshot
      await page.evaluate(() => {
        const rows = [...document.querySelectorAll('tr')];
        const row = rows.find((r) => r.textContent.includes('FAIL'));
        const body = row ? row.closest('.overflow-y-auto') : null;
        if (row && body) {
          const delta = row.getBoundingClientRect().top - body.getBoundingClientRect().top;
          body.scrollTop += delta - Math.floor(body.clientHeight / 3);
          body.scrollLeft = 0;
        }
      });
      await page.waitForTimeout(250);
      await page.screenshot({ path: shot(shotName) });

      // params tab: real API + parameters
      await page.getByText(/API 与参数/).first().click();
      await page.waitForTimeout(200);
      const paramsText = await page.textContent('body');
      check(`fail[${failCase.id}]: GM API name visible`, paramsText.includes(failCase.apis.gm.name), failCase.apis.gm.name);
      const firstParam = Object.entries(failCase.parameters)[0];
      if (firstParam) {
        check(
          `fail[${failCase.id}]: parameter visible (${firstParam[0]}=${JSON.stringify(firstParam[1])})`,
          paramsText.includes(`"${firstParam[0]}": ${JSON.stringify(firstParam[1])}`) || paramsText.includes(String(firstParam[1])),
          String(firstParam[1])
        );
      }

      // evidence tab: exit codes + log links
      await page.getByText(/证据与日志/).first().click();
      await page.waitForTimeout(200);
      const evText = await page.textContent('body');
      const exitCodes = failCase.execution?.exitCodes || [];
      check(`fail[${failCase.id}]: original exit codes visible (${exitCodes.join(',')})`, evText.includes(`exitCodes=[${exitCodes.join(', ')}]`), `exitCodes=[${exitCodes.join(', ')}]`);
      const firstEvidence = failCase.evidence[0];
      const evResp = await page.request.get(`${baseUrl}/data/${latest.dir}/${firstEvidence.path}`);
      check(`fail[${failCase.id}]: evidence file reachable`, evResp.ok(), `${firstEvidence.path} -> ${evResp.status()}`);
    }

    // ---- T9: performance panel honest labels ----
    await page.click(`[data-case-id="${cases.blend.id}"]`);
    await waitForMeshes(page);
    await page.getByText(/性能指标/).first().click();
    await page.waitForTimeout(200);
    const perfText = await page.textContent('body');
    const perf = cases.blend.performance;
    check('perf: measurement kind from real field', perf.measurementKind ? perfText.includes(perf.measurementKind) : true, String(perf.measurementKind));
    check('perf: qualified scope text visible', perf.qualifiedScope ? perfText.includes(perf.qualifiedScope) : true);
    check('perf: samples/warmup shown', perfText.includes(`samples=${perf.samples}`) && perfText.includes(`warmup=${perf.warmup ?? 0}`));
    check('perf: not called a performance certification', !perfText.includes('性能认证 (') && perfText.includes('Correctness Qualified'));
    await page.screenshot({ path: shot('webui_v2_desktop_perf.png') });

    // fail case ratio is null -> explicit empty-ratio wording
    if (cases.failInertia) {
      await page.click(`[data-case-id="${cases.failInertia.id}"]`);
      await waitForMeshes(page);
      await page.getByText(/性能指标/).first().click();
      await page.waitForTimeout(200);
      const failPerfText = await page.textContent('body');
      check('perf: null ratio never rendered as 0x', !/\b0(\.0+)?x\b/.test(failPerfText));
      check('perf: null ratio explained', failPerfText.includes('null') && failPerfText.includes('不代表 0 倍'));
    }

    // ---- T10: missing / unsupported / diagnostic honesty ----
    await page.click(`[data-case-id="${cases.missing.id}"]`);
    await page.waitForTimeout(800);
    const missingBar = await page.textContent('[data-testid="case-topbar"]');
    check('missing: badge 数据缺失 (MISSING)', missingBar.includes('数据缺失 (MISSING)'));
    check('missing: not green', !(await page.$eval('[data-testid="case-topbar"] span.rounded', (el) => el.className)).includes('emerald'));
    check('missing: ratio empty', missingBar.includes('比率: N/A'));
    const mStats = await viewerStats(page);
    check('missing: no stale geometry shown', mStats.every((s) => s.bodyId === ''), JSON.stringify(mStats.map((s) => s.bodyId)));

    await page.click(`[data-case-id="${cases.unsupported.id}"]`);
    await page.waitForTimeout(500);
    const unsupBar = await page.textContent('[data-testid="case-topbar"]');
    check('unsupported: badge 未适配 (UNSUPPORTED)', unsupBar.includes('未适配 (UNSUPPORTED)'));
    check('unsupported: not green', !(await page.$eval('[data-testid="case-topbar"] span.rounded', (el) => el.className)).includes('emerald'));
    await page.getByText(/证据与日志/).first().click();
    await page.waitForTimeout(200);
    const capText = await page.textContent('body');
    if (cases.unsupported.capabilityScope || cases.unsupported.adapterStatus !== undefined) {
      check('unsupported: adapter status from real field', capText.includes(`adapterStatus=${cases.unsupported.adapterStatus ?? '-'}`));
      check('unsupported: kernel capability shown as null/unknown', capText.includes('kernelCapability=null'));
      check('unsupported: adapter gap, NOT kernel gap', capText.includes('非内核操作不支持'));
    }
    if (latest.counts.diagnostic > 0 && cases.diagnostic) {
      await page.click(`[data-case-id="${cases.diagnostic.id}"]`);
      await page.waitForTimeout(500);
      const diagBar = await page.textContent('[data-testid="case-topbar"]');
      check('diagnostic: badge is diagnostic (never green pass)', diagBar.includes('诊断') && !diagBar.includes('通过 (PASS)'));
    } else {
      check('diagnostic: zero diagnostic cases in run - no diagnostic pill', !(await page.textContent('body')).includes('诊断 0'));
    }

    // ---- T11: desktop layout bounding boxes ----
    const headerBox = await bbox(page, '[data-testid="app-header"]');
    check('layout: header stays within width', headerBox.width <= 1440 && headerBox.height < 160, JSON.stringify(headerBox));
    const headerOverflow = await page.$eval('[data-testid="app-header"]', (el) => el.scrollWidth <= el.clientWidth + 1);
    check('layout: header does not overflow horizontally', headerOverflow);
    const sidebarBox = await bbox(page, '[data-testid="sidebar-shell"]');
    check('layout: desktop sidebar width 320', Math.abs(sidebarBox.width - 320) <= 2, `w=${sidebarBox.width}`);
    const vpBox = await bbox(page, '[data-testid="viewport-region"]');
    check('layout: desktop viewport wide', vpBox.width >= 1000, `w=${vpBox.width}`);
    const h0 = await bbox(page, '[data-testid="viewer-root"]');
    const h1 = await page.locator('[data-testid="viewer-root"]').nth(1).boundingBox();
    check('layout: two viewports side by side, no overlap', h0.x + h0.width <= h1.x + 2 && h1.width >= 480 && h0.width >= 480, JSON.stringify({ h0, h1 }));
    const legendVisible = await page.$eval('[data-testid="legend-body"]', (el) => getComputedStyle(el).display !== 'none');
    check('layout: legend visible on desktop', legendVisible);

    // ---- T12: run switching shows each run's REAL statistics ----
    // ensure a meshed case is active before the loop (same-value select is a no-op)
    await page.click(`[data-case-id="${cases.blend.id}"]`);
    await waitForMeshes(page);
    for (const run of index.runs) {
      await page.selectOption('[data-testid="run-select"]', run.id);
      await page.waitForTimeout(1200);
      const runCounts = runs[run.id].counts;
      const runTotal = runs[run.id].total;
      const text = await page.textContent('body');
      const optionText = await page.$eval(
        `[data-testid="run-select"] option[value="${run.id}"]`,
        (el) => el.textContent
      );
      const isHistory = run.id !== latestId;
      check(
        `run[${run.id}]: sidebar shows its own stats (${runTotal} total, ${runCounts.pass}/${runCounts.fail}/${runCounts.missing}/${runCounts.unsupported})`,
        text.includes(`${runTotal} / ${runTotal} 项`) &&
          text.includes(`通过 ${runCounts.pass}`) &&
          text.includes(`失败 ${runCounts.fail}`) &&
          text.includes(`缺失 ${runCounts.missing}`) &&
          text.includes(`未适配 ${runCounts.unsupported}`),
        `${runTotal} ${JSON.stringify(runCounts)}`
      );
      check(
        `run[${run.id}]: history runs marked as history in selector`,
        optionText.includes('·历史') === isHistory,
        optionText.trim()
      );
      // each run must render its own geometry via its own meshed case
      // (a preserved selection can be "missing" in a history run - no meshes)
      const runCase = runs[run.id].manifest.cases.find(
        (c) => (c.status === 'pass' || c.status === 'diagnostic') && c.steps?.length > 0 && c.steps[0].gm?.before
      );
      if (runCase) {
        await page.click(`[data-case-id="${runCase.id}"]`);
        await waitForMeshes(page);
        const rs = await viewerStats(page);
        const runExpected = expectedStats(meshJson(runs[run.id], runCase.steps[0].gm.before));
        check(
          `run[${run.id}]: real geometry rendered (${runCase.id})`,
          rs.some((s) => s.faces === runExpected.faces && s.triangles === runExpected.triangles),
          JSON.stringify({ got: rs.map((s) => ({ b: s.bodyId, f: s.faces, t: s.triangles })), exp: runExpected.bodyId })
        );
      } else {
        check(`run[${run.id}]: has a meshed case`, false, 'no pass/diagnostic case with steps');
      }
    }
    await page.selectOption('[data-testid="run-select"]', latestId);
    await page.waitForTimeout(1200);

    // ---- T13: fast case/run switching - no stale data ----
    await page.click(`[data-case-id="${cases.blend.id}"]`);
    await page.click(`[data-case-id="${cases.boolean ? cases.boolean.id : cases.failInertia.id}"]`, { noWaitAfter: true });
    await page.waitForTimeout(1500);
    const fastId = cases.boolean ? cases.boolean.id : cases.failInertia.id;
    const fastCase = cases.boolean ? cases.boolean : cases.failInertia;
    const fastExpected = expectedStats(meshJson(latest, fastCase.steps[0].gm.before));
    const fastToolExpected = (fastCase.steps[0].gm.tools || []).map((t) => expectedStats(meshJson(latest, t)));
    const expFastFaces = fastExpected.faces + fastToolExpected.reduce((a, t) => a + t.faces, 0);
    const expFastTris = fastExpected.triangles + fastToolExpected.reduce((a, t) => a + t.triangles, 0);
    const fastStats = (await viewerStats(page))[0];
    check(
      'fast-switch: final case wins (no stale geometry)',
      fastStats.bodyId === fastExpected.bodyId &&
        fastStats.faces === expFastFaces &&
        fastStats.triangles === expFastTris,
      `${JSON.stringify({ b: fastStats.bodyId, f: fastStats.faces, t: fastStats.triangles })} vs ${JSON.stringify({ b: fastExpected.bodyId, f: expFastFaces, t: expFastTris })}`
    );
    await page.selectOption('[data-testid="run-select"]', index.runs.find((r) => r.id !== latestId).id, { noWaitAfter: true });
    await page.selectOption('[data-testid="run-select"]', latestId, { noWaitAfter: true });
    await page.waitForTimeout(1500);
    const afterRunText = await page.textContent('body');
    check('fast-switch: run flip ends on latest manifest stats', afterRunText.includes(`${latest.total} / ${latest.total} 项`));

    await page.close();

    // ==========================================================
    // 404 page: mesh request failure must clear old geometry
    // ==========================================================
    const page404 = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page404.on('pageerror', (err) => pageErrors.push(`[Page Error] ${err.message}`));
    const afterFile = cases.blend.steps[0].gm.after.split('/').pop();
    await page404.route(`**/*${afterFile}*`, (route) => route.abort());
    await page404.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await waitForMeshes(page404);
    await page404.click(`[data-case-id="${cases.blend.id}"]`);
    await waitForMeshes(page404);
    await page404.getByText('后态 (After)').click();
    await page404.waitForTimeout(1000);
    const s404 = await viewerStats(page404);
    const gm404 = s404.find((s) => s.viewer === 'gm');
    check('404: failed mesh shows error, old geometry cleared', gm404.bodyId === '' && gm404.faces === 0, JSON.stringify(gm404));
    const emptyText = await page404.textContent('[data-testid="viewer-empty"]');
    check('404: error message visible', /异常|错误|未找到/.test(emptyText), emptyText.trim().slice(0, 60));
    await page404.close();

    // ==========================================================
    // Mobile page 390x844
    // ==========================================================
    const mob = await browser.newPage({ viewport: { width: 390, height: 844 } });
    mob.on('pageerror', (err) => pageErrors.push(`[Page Error] ${err.message}`));
    await mob.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await waitForMeshes(mob);
    await mob.waitForTimeout(500);

    // no horizontal overflow anywhere in header/topbar/timeline
    const overflow = await mob.evaluate(() => {
      const els = [
        document.querySelector('[data-testid="app-header"]'),
        document.querySelector('[data-testid="case-topbar"]'),
      ].filter(Boolean);
      return els.map((el) => ({ tag: el.dataset.testid, scroll: el.scrollWidth, client: el.clientWidth }));
    });
    for (const o of overflow) {
      check(`mobile: ${o.tag} no horizontal overflow`, o.scroll <= o.client + 1, JSON.stringify(o));
    }
    const docScroll = await mob.evaluate(() => document.documentElement.scrollWidth);
    check('mobile: document no horizontal overflow', docScroll <= 390, `scrollWidth=${docScroll}`);

    // sidebar is a drawer: closed by default, main canvas wide
    const sidebarMobile = await bbox(mob, '[data-testid="sidebar-shell"]');
    check('mobile: sidebar off-canvas drawer when closed', sidebarMobile.x + sidebarMobile.width <= 2, JSON.stringify(sidebarMobile));
    const canvasBox = await bbox(mob, '[data-testid="viewer-canvas-host"]');
    check('mobile: main canvas width >= 250px', canvasBox.width >= 250, `w=${canvasBox.width}`);

    // legend collapsed by default on narrow screens
    const legendHidden = await mob.$eval('[data-testid="legend-body"]', (el) => getComputedStyle(el).display === 'none');
    check('mobile: legend collapsed by default (does not cover model)', legendHidden);
    await mob.click('[data-testid="legend-toggle"]');
    const legendShown = await mob.$eval('[data-testid="legend-body"]', (el) => getComputedStyle(el).display !== 'none');
    check('mobile: legend can be opened on demand', legendShown);
    await mob.click('[data-testid="legend-toggle"]');

    // secondary metadata hidden but viewable
    await mob.click('[data-testid="header-meta-toggle"]');
    const metaVisible = await mob.isVisible('[data-testid="header-meta-panel"]');
    const metaText = metaVisible ? await mob.textContent('[data-testid="header-meta-panel"]') : '';
    check('mobile: secondary run metadata hidden but viewable', metaVisible && metaText.includes(latest.manifest.run.commit.slice(0, 7)), metaText.trim().slice(0, 60));
    await mob.screenshot({ path: shot('webui_v2_mobile_main.png') });

    // GM/PK single-view switching
    await mob.getByText('GM 内核', { exact: true }).click();
    await mob.waitForTimeout(200);
    const visGm = await mob.evaluate(() => {
      const roots = document.querySelectorAll('[data-testid="viewer-root"]');
      return [...roots].map((r) => getComputedStyle(r.parentElement).display !== 'none');
    });
    check('mobile: GM tab shows GM only', visGm[0] === true && visGm[1] === false, JSON.stringify(visGm));
    const gmCanvas = await bbox(mob, '[data-testid="viewer-root"] >> nth=0 >> [data-testid="viewer-canvas-host"]');
    check('mobile: GM single view canvas >= 250px', gmCanvas.width >= 250, `w=${gmCanvas.width}`);
    await mob.getByText('Parasolid', { exact: true }).click();
    await mob.waitForTimeout(200);
    const visPk = await mob.evaluate(() => {
      const roots = document.querySelectorAll('[data-testid="viewer-root"]');
      return [...roots].map((r) => getComputedStyle(r.parentElement).display !== 'none');
    });
    check('mobile: PK tab shows PK only', visPk[0] === false && visPk[1] === true, JSON.stringify(visPk));
    await mob.getByText('双视图', { exact: true }).click();

    // drawer: open -> select case -> closes, real selection
    await mob.click('[data-testid="open-sidebar"]');
    await mob.waitForTimeout(400);
    const drawerOpen = await bbox(mob, '[data-testid="sidebar-shell"]');
    check('mobile: drawer opens', drawerOpen.x >= -2, JSON.stringify(drawerOpen));
    await mob.screenshot({ path: shot('webui_v2_mobile_drawer.png') });
    await mob.click(`[data-case-id="${cases.blend.id}"]`);
    await waitForMeshes(mob);
    const drawerClosed = await bbox(mob, '[data-testid="sidebar-shell"]');
    check('mobile: drawer closes after selecting a case', drawerClosed.x + drawerClosed.width <= 2, JSON.stringify(drawerClosed));
    const mobTopbar = await mob.textContent('[data-testid="case-topbar"]');
    check('mobile: selected case is active', mobTopbar.includes(cases.blend.id));

    // before/after on mobile really switches geometry
    const mobBefore = (await viewerStats(mob))[0];
    await mob.getByText('后态 (After)').click();
    await mob.waitForTimeout(1000);
    const mobAfter = (await viewerStats(mob))[0];
    check('mobile: before/after switches real geometry', mobAfter.roleFaces.newBlend >= 1 && JSON.stringify(mobAfter.roleFaces) !== JSON.stringify(mobBefore.roleFaces), JSON.stringify({ before: mobBefore.roleFaces, after: mobAfter.roleFaces }));

    // inspection panel: params / diffs / perf viewable and collapsible on mobile
    await mob.getByText(/API 与参数/).first().click();
    await mob.waitForTimeout(300);
    const preBox = await bbox(mob, 'pre');
    check('mobile: parameters JSON visible in viewport', preBox && preBox.width > 100 && preBox.height > 20 && preBox.x >= 0 && preBox.x + preBox.width <= 391, JSON.stringify(preBox));
    await mob.screenshot({ path: shot('webui_v2_mobile_inspection.png') });

    // fail values viewable on mobile
    await mob.click('[data-testid="open-sidebar"]');
    await mob.waitForTimeout(300);
    await mob.click(`[data-case-id="${cases.failInertia.id}"]`);
    await waitForMeshes(mob);
    await mob.getByText(/逐项验收/).first().click();
    await mob.waitForTimeout(300);
    const mobFailText = await mob.textContent('body');
    check('mobile: fail thresholds/diffs visible', mobFailText.includes('Inertia YZ') && mobFailText.includes(String(1e-7)) && mobFailText.includes('model⁵'));
    await mob.screenshot({ path: shot('webui_v2_mobile_fail.png') });
    await mob.close();

    // ==========================================================
    // Dist hygiene: no external CDN, no dev fixture data
    // ==========================================================
    const distFiles = [];
    (function walk(dir) {
      for (const f of fs.readdirSync(dir)) {
        const p = path.join(dir, f);
        if (fs.statSync(p).isDirectory()) walk(p);
        else distFiles.push(p);
      }
    })(distDir);
    const codeFiles = distFiles.filter((f) => /\.(html|js|css)$/.test(f));
    const allowedHosts = new Set(['www.w3.org', 'react.dev']);
    let externalHits = [];
    for (const f of codeFiles) {
      const content = fs.readFileSync(f, 'utf8');
      for (const m of content.matchAll(/https?:\/\/([^/"'`\s)]+)/g)) {
        const host = m[1];
        if (!allowedHosts.has(host)) externalHits.push(`${path.basename(f)}:${host}`);
      }
    }
    check('dist: no external CDN/network dependencies', externalHits.length === 0, externalHits.slice(0, 5).join(', '));
    check('dist: no dev fixture files bundled', !distFiles.some((f) => /devfixture/i.test(path.basename(f))));
    check('dist: no DEV_FIXTURE data in code', !codeFiles.some((f) => fs.readFileSync(f, 'utf8').includes('DEV_FIXTURE')));
    check('dist: real data index + all run manifests shipped',
      fs.existsSync(path.join(distDir, 'data', 'index.json')) &&
        index.runs.every((r) => fs.existsSync(path.join(distDir, 'data', r.manifest))));
  } finally {
    await browser.close();
    server.close();
  }

  check('runtime: no uncaught page errors', pageErrors.length === 0, pageErrors.slice(0, 3).join(' | '));

  // ============================================================
  // Report
  // ============================================================
  const failed = results.filter((r) => !r.ok);
  const lines = [
    `webui_v2 smoke -- ${new Date().toISOString()}`,
    `browser: ${chromePath}`,
    `latest run: ${latestId} counts=${JSON.stringify(latest.counts)} total=${latest.total}`,
    '',
    ...results.map((r) => `${r.ok ? 'PASS' : 'FAIL'} ${r.name}${r.detail ? ` -- ${r.detail}` : ''}`),
    '',
    `SUMMARY: ${results.length - failed.length}/${results.length} passed, ${failed.length} failed`,
  ];
  fs.writeFileSync(path.join(tempDocsDir, 'webui_v2_smoke_log.txt'), lines.join('\n'));
  console.log('');
  console.log(`SUMMARY: ${results.length - failed.length}/${results.length} passed, ${failed.length} failed`);
  if (failed.length > 0) {
    console.log('Failed checks:');
    for (const f of failed) console.log(`  - ${f.name} ${f.detail}`);
    process.exitCode = 1;
  }
}

run().catch((err) => {
  console.error('[Smoke] crashed:', err);
  process.exitCode = 1;
});
