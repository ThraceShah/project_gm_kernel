#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Browser-only consumer checks against existing assets; no PK/export/capture execution.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var start=new ProcessStartInfo("node"){WorkingDirectory=Path.GetFullPath("../web",scripts),RedirectStandardInput=true};
start.ArgumentList.Add("-");
using var child=Process.Start(start)!;
child.StandardInput.Write("""
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), os = require('node:os');
const { createHash } = require('node:crypto');
const { chromium } = require('playwright-core');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
(async () => {
  const web = process.cwd(), dist = path.join(web, 'dist'), output = path.resolve(web, '../temp_docs/real-uv-context');
  fs.mkdirSync(output, { recursive: true });
  const assetRoot = path.join(dist, 'data/display/real-uv');
  const assets = {};
  const walk = directory => { for (const name of fs.readdirSync(directory)) {
    const file = path.join(directory, name);
    if (fs.statSync(file).isDirectory()) walk(file); else assets[path.relative(assetRoot, file)] = hash(fs.readFileSync(file));
  }};
  walk(assetRoot);
  const candidates = [process.env.SMOKE_BROWSER_PATH, chromium.executablePath()];
  const cache = process.env.PLAYWRIGHT_BROWSERS_PATH || path.join(os.homedir(), '.cache/ms-playwright');
  if (fs.existsSync(cache)) for (const dir of fs.readdirSync(cache).reverse())
    for (const rel of ['chrome-linux64/chrome', 'chrome-linux/chrome']) candidates.push(path.join(cache, dir, rel));
  const executablePath = candidates.find(p => p && fs.existsSync(p));
  if (!executablePath) throw Error('ENV_NOT_READY: Chromium unavailable');
  const server = http.createServer((req, res) => {
    const pathname = new URL(req.url, 'http://localhost').pathname;
    const file = path.join(dist, pathname === '/' ? 'index.html' : pathname);
    if (!fs.existsSync(file) || !fs.statSync(file).isFile()) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', file.endsWith('.json') ? 'application/json' : file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : 'text/html');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ executablePath, args: ['--no-sandbox', '--disable-gpu', '--use-gl=swiftshader', '--no-proxy-server'] });
  const results = [];
  const modes = ['before-as-after', 'after-as-before', 'wrong-case', 'wrong-step', 'wrong-kernel', 'wrong-package', 'wrong-source-mapping'];
  try {
    for (const mode of ['normal-four', ...modes]) for (const warm of (mode === 'normal-four' || mode === 'wrong-source-mapping' ? [false] : [false, true])) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage(), errors = [], mutations = [], requests = [];
      page.on('pageerror', e => errors.push(e.message));
      page.on('request', r => { if (r.url().includes('.mesh.json')) requests.push(new URL(r.url()).pathname); });
      let active = !warm;
      await page.route('**/display/real-uv/*.json*', async route => {
        const name = new URL(route.request().url()).pathname.split('/').pop();
        const response = await route.fetch(), bytes = await response.body();
        if (!active || mode === 'normal-four' || !['manifest.json', 'index.json', 'display-manifest.json'].includes(name)) { await route.fulfill({ response, body: bytes }); return; }
        const data = JSON.parse(bytes); let mutated = false;
        if (name === 'manifest.json') {
          const c = data.cases[0], step = c.steps[0];
          if (mode === 'before-as-after') { step.gm.after = step.gm.before; mutated = true; }
          if (mode === 'after-as-before') { step.gm.before = step.gm.after; mutated = true; }
          if (mode === 'wrong-case') { c.id = 'review.wrong-case'; mutated = true; }
          if (mode === 'wrong-step') { step.id = 'operation-999'; mutated = true; }
          if (mode === 'wrong-kernel') { step.gm = { ...step.pk }; mutated = true; }
        }
        if (name === 'index.json' && mode === 'wrong-package') { data.latestRunId = data.runs[0].id = 'review.wrong-package'; mutated = true; }
        if (name === 'display-manifest.json' && mode === 'wrong-source-mapping') { data.items[0].sourceRun = 'review.wrong-source'; mutated = true; }
        const body = mutated ? JSON.stringify(data) : bytes;
        if (mutated) mutations.push({ name, originalSha256: hash(bytes), routedSha256: hash(body), input: data });
        await route.fulfill({ response, body });
      });
      const panes = () => page.getByTestId('viewer-canvas-host').evaluateAll(es => es.map(e => ({ ...e.dataset })));
      const phase = async stage => { await page.getByText(stage === 'after' ? '后态 (After)' : '前态 (Before)', { exact: true }).click(); await settle(); };
      const settle = async () => {
        await page.waitForLoadState('networkidle');
        await page.waitForFunction(() => {
          const hosts = [...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];
          return hosts.length === 2 && hosts.every(e => {
            const root = e.closest('[data-testid="viewer-root"]');
            if (root.querySelector('[data-testid="viewer-loading"]')) return false;
            return root.querySelector('[data-testid="viewer-empty"]') ? e.dataset.triangleCount === '0' : Boolean(e.dataset.bodyId);
          });
        });
      };
      const normal = (observed, stage) => observed.length === 2 && observed.every((p, i) => p.uLineCount === '5' && p.vLineCount === '5' && p.triangleCount === (stage === 'before' ? '96' : i === 0 ? '220' : '130'));
      await page.goto(`http://127.0.0.1:${server.address().port}/?display=real-uv`, { waitUntil: 'networkidle' });
      await settle();
      const warmSnapshots = [];
      if (warm || mode === 'normal-four') {
        warmSnapshots.push({ stage: 'before', panes: await panes() });
        await phase('after'); warmSnapshots.push({ stage: 'after', panes: await panes() });
        await phase('before'); warmSnapshots.push({ stage: 'before', panes: await panes() });
        if (!warmSnapshots.every(s => normal(s.panes, s.stage))) throw Error('Normal snapshot warmup failed');
      }
      const warmRequestCount = requests.length;
      if (warm) { active = true; await page.getByRole('button', { name: '刷新', exact: true }).click(); await settle(); }
      const targetStage = mode === 'before-as-after' ? 'after' : 'before';
      if (targetStage === 'after') await phase('after');
      // Refresh can replace the whole viewport twice (refreshCount, then fresh index).
      // Read readiness and counts atomically, never mix measurements across those renders.
      const observation = await (await page.waitForFunction(({ mode, targetStage }) => {
        const hosts = [...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];
        const text = document.body.textContent;
        if (hosts.length !== 2 || (mode !== 'normal-four' && !text.includes('unavailable'))) return false;
        if (!hosts.every(e => {
          const root = e.closest('[data-testid="viewer-root"]');
          return !root.querySelector('[data-testid="viewer-loading"]') &&
            (root.querySelector('[data-testid="viewer-empty"]') ? e.dataset.triangleCount === '0' : Boolean(e.dataset.bodyId));
        })) return false;
        const button = [...document.querySelectorAll('button')].find(b => b.textContent.trim() === (targetStage === 'after' ? '后态 (After)' : '前态 (Before)'));
        return { panes: hosts.map(e => ({ ...e.dataset })), text, selectedClass: button.className };
      }, { mode, targetStage })).jsonValue();
      const observed = observation.panes, text = observation.text;
      const rejected = p => p.triangleCount === '0' && p.uLineCount === '0' && p.vLineCount === '0' && p.bodyId === '';
      const onePane = ['before-as-after', 'after-as-before', 'wrong-kernel'].includes(mode);
      const validPk = observed[1]?.uLineCount === '5' && observed[1]?.vLineCount === '5' && observed[1]?.triangleCount === (targetStage === 'before' ? '96' : '130');
      let passed = mode === 'normal-four' ? normal(observed, 'before') && requests.length === 4
        : observed.length === 2 && (onePane ? rejected(observed[0]) && validPk : observed.every(rejected)) && text.includes('unavailable');
      passed = passed && observation.selectedClass.includes('font-semibold') && errors.length === 0;
      const result = { mode, warm, passed, targetStage, panes: observed, warmSnapshots, warmRequestCount, requests, mutations, errors };
      results.push(result); console.log(JSON.stringify({ mode, warm, passed, triangles: observed.map(p => p.triangleCount), warmRequestCount, finalRequestCount: requests.length }));
      if (mode !== 'normal-four') await page.screenshot({ path: path.join(output, `${mode}-${warm ? 'warm' : 'cold'}.png`) });
      await context.close();
    }
    fs.writeFileSync(path.join(output, 'selection-controls.json'), JSON.stringify({ assets, results }, null, 2));
    process.exitCode = results.every(r => r.passed) ? 0 : 1;
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
""");
child.StandardInput.Close();child.WaitForExit();return child.ExitCode;
static string ScriptPath([CallerFilePath]string path="")=>path;
