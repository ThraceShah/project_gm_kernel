#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Requires an existing production build. Exit 1 means the consumer gate accepted a counterexample.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var start = new ProcessStartInfo("node")
{
    WorkingDirectory = Path.GetFullPath("../web", scripts),
    RedirectStandardInput = true,
};
start.ArgumentList.Add("-");
using var child = Process.Start(start)!;
child.StandardInput.Write("""
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const os = require('node:os');
const { chromium } = require('playwright-core');
(async () => {
  const web = process.cwd(), dist = path.join(web, 'dist');
  const output = path.resolve(web, '../temp_docs/real-uv-f3-review');
  fs.mkdirSync(output, { recursive: true });
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
  try {
    for (const mode of ['unaltered', 'wrong-case-step', 'before-as-after']) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage();
      const errors = []; page.on('pageerror', e => errors.push(e.message));
      if (mode !== 'unaltered') await page.route('**/display/real-uv/manifest.json*', async route => {
        const response = await route.fetch(); const manifest = await response.json();
        if (mode === 'wrong-case-step') {
          manifest.cases[0].id = 'review.wrong-case'; manifest.cases[0].steps[0].id = 'operation-999';
        } else manifest.cases[0].steps[0].gm.after = manifest.cases[0].steps[0].gm.before;
        await route.fulfill({ response, json: manifest });
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/?display=real-uv`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(400);
      if (mode === 'before-as-after') { await page.getByText('后态 (After)', { exact: true }).click(); await page.waitForTimeout(400); }
      const panes = await page.getByTestId('viewer-canvas-host').evaluateAll(es => es.map(e => ({ ...e.dataset })));
      const provenance = await page.getByTestId('uv-provenance').allTextContents();
      const passed = mode === 'unaltered'
        ? panes.length === 2 && panes.every(p => p.uLineCount === '5' && p.vLineCount === '5')
        : mode === 'wrong-case-step' ? panes.every(p => p.triangleCount === '0') : panes[0].triangleCount === '0';
      const result = { mode, passed, panes, provenance, errors, mutation: 'Only display manifest case/step/ref changed; mesh/ISO/display-manifest asset bytes untouched' };
      results.push(result); console.log(JSON.stringify(result));
      await page.screenshot({ path: path.join(output, `consumer-${mode}.png`) });
      await context.close();
    }
    fs.writeFileSync(path.join(output, 'consumer-counterexamples.json'), JSON.stringify(results, null, 2));
    process.exitCode = results.every(r => r.passed && r.errors.length === 0) ? 0 : 1;
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
""");
child.StandardInput.Close();
child.WaitForExit();
return child.ExitCode;

static string ScriptPath([CallerFilePath] string path = "") => path;
