using System.Diagnostics;
using System.Runtime.CompilerServices;

// dotnet run LayoutCamera.cs [data directory relative to this file] [baseline]
var scriptDir = Path.GetDirectoryName(ScriptPath())!;
var process = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(scriptDir, "..") };
process.ArgumentList.Add("--input-type=module");
process.ArgumentList.Add("--eval");
process.ArgumentList.Add("""
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import http from 'node:http';
import { chromium } from 'playwright-core';
const data = process.argv[1], baseline = process.argv[2] === 'baseline';
const root = baseline ? path.resolve('../temp_docs/layout-camera/baseline/web/dist') : path.resolve('dist');
const out = path.resolve('../temp_docs/layout-camera'); fs.mkdirSync(out, {recursive:true});
const index = JSON.parse(fs.readFileSync(path.join(data, 'index.json')));
const run = index.runs.find(r => r.id === index.latestRunId);
const manifest = JSON.parse(fs.readFileSync(path.join(data, run.manifest)));
const meshRoot = path.dirname(path.join(data, run.manifest));
const rows = [], errors = [];
function check(name, ok, detail) { rows.push({name, ok:!!ok, detail}); console.log(`${ok?'PASS':'FAIL'} ${name} ${detail ? JSON.stringify(detail):''}`); }
const server = http.createServer((req,res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  const file = url.startsWith('/data/') ? path.join(data, url.slice(6)) : path.join(root, url === '/' ? 'index.html' : url);
  try { res.setHeader('Content-Type', file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.json')?'application/json':'text/html'); res.end(fs.readFileSync(file)); }
  catch {res.statusCode=404;res.end('missing');}
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const cache = path.join(os.homedir(), '.cache/ms-playwright');
const binaries = fs.readdirSync(cache).filter(d=>/^chromium-/.test(d)).sort().reverse().flatMap(d=>['chrome-linux64/chrome','chrome-linux/chrome'].map(p=>path.join(cache,d,p)));
const browser = await chromium.launch({executablePath:process.env.SMOKE_BROWSER_PATH || binaries.find(p=>fs.existsSync(p)), headless:true,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const url = `http://127.0.0.1:${server.address().port}/`;
const page = await browser.newPage({viewport:{width:1965,height:1228}});
page.on('pageerror',e=>errors.push(e.message));
const hosts = () => page.getByTestId('viewer-canvas-host');
const stats = () => hosts().evaluateAll(els=>els.map(e=>({body:e.dataset.bodyId,triangles:+e.dataset.triangleCount,position:e.dataset.cameraPos,target:e.dataset.cameraTarget,feature:JSON.parse(e.dataset.featureVisibility||'null')})));
const wait = async()=>{await page.waitForFunction(()=>[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')].every(e=>e.dataset.bodyId)); await page.waitForTimeout(450);};
const select = async(id)=>{await page.locator(`[data-case-id="${id}"]`).click();await wait();};
const capture = async(name)=>{
  const metrics = await hosts().evaluateAll(els=>els.map(el=>{
    const c=el.querySelector('canvas'), copy=document.createElement('canvas');copy.width=c.width;copy.height=c.height;
    const ctx=copy.getContext('2d');ctx.drawImage(c,0,0);const d=ctx.getImageData(0,0,c.width,c.height).data;
    let yellow=0,green=0,greenRegion=0,signature=0;
    const mask=new Uint8Array(c.width*c.height);
    for(let i=0;i<d.length;i+=4){const [r,g,b]=d.slice(i,i+3); if(r>140&&g>110&&b<90&&r-b>80&&Math.abs(r-g)<80)yellow++;if(g>r+15&&g>b+15&&g>40){green++;mask[i/4]=1;}if(i%16===0)signature=(signature*31+r*65536+g*256+b)%1000000007;}
    for(let y=3;y<c.height-3;y++)for(let x=3;x<c.width-3;x++){if(!mask[y*c.width+x])continue;let n=0;for(let dy=-3;dy<=3;dy++)for(let dx=-3;dx<=3;dx++)n+=mask[(y+dy)*c.width+x+dx];if(n>=25)greenRegion++;}
    return {yellow,green,greenRegion,signature,width:c.width,height:c.height};
  }));
  const pose=await stats(); await page.screenshot({path:path.join(out,`${baseline?'baseline':'candidate'}-${name}.png`)});
  rows.push({name:`capture ${name}`,ok:true,pose,metrics});console.log(name,JSON.stringify({pose,metrics}));return {pose,metrics};
};
const drag=async(dx,dy)=>{const b=await hosts().first().boundingBox();await page.mouse.move(b.x+b.width/2,b.y+b.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2+dx,b.y+b.height/2+dy,{steps:10});await page.mouse.up();await page.waitForTimeout(700);};
const samePose=(a,b)=>['position','target'].every(key=>Math.hypot(...a[key].split(',').map((v,i)=>+v-b[key].split(',')[i]))<0.05);
try {
  await page.goto(url);await wait();await select('support-chain.asymmetric43-base');
  const before = await capture('before');
  if(baseline){await page.getByText('后态 (After)',{exact:true}).click();await wait();await page.getByTestId('btn-focus-op').click();await page.waitForTimeout(350);await capture('after-focus');}
  else {
    check('initial evidence collapsed',await page.getByRole('dialog').count()===0);
    check('shared legend once outside both canvases',await page.getByTestId('legend-body').count()===1 && await page.getByTestId('legend-body').evaluate(el=>el.getBoundingClientRect().bottom<=document.querySelector('canvas').getBoundingClientRect().top));
    check('selected edge visible in both panes',before.pose.every(p=>p.feature?.visibleFraction>=0.9&&p.feature.projectedPixels>100)&&before.metrics.every(m=>m.yellow>100),before);
    check('linked initial pose and target identical',samePose(...before.pose));
    const canvas=await hosts().first().boundingBox();
    await page.getByText(/逐项验收/).click();await page.waitForTimeout(100);
    check('drawer overlays without shrinking canvas',JSON.stringify(canvas)===JSON.stringify(await hosts().first().boundingBox()));
    check('drawer internal scroll',await page.getByTestId('inspection-content').evaluate(e=>getComputedStyle(e).overflowY==='auto'));
    check('close receives keyboard focus',await page.getByTestId('inspection-close').evaluate(e=>document.activeElement===e));
    await page.screenshot({path:path.join(out,'candidate-drawer.png')});
    await page.keyboard.press('Tab');check('keyboard stays inside dialog',await page.getByRole('dialog').evaluate(e=>e.contains(document.activeElement)));
    await page.keyboard.press('Escape');await page.waitForTimeout(80);check('Escape closes and returns tab focus',await page.getByRole('dialog').count()===0&&await page.getByText(/逐项验收/).evaluate(e=>document.activeElement===e.closest('button')),await page.evaluate(()=>document.activeElement.outerHTML.slice(0,250)));
    await page.getByText('后态 (After)',{exact:true}).click();await wait();
    check('step preserves initial camera',samePose((await stats())[0],before.pose[0]));
    await page.getByTestId('btn-focus-op').click();await page.waitForTimeout(450);const after=await capture('after-focus');
    check('after generated face visible in both panes',after.pose.every(p=>p.feature?.visibleFraction>=0.7&&p.feature.projectedPixels>100)&&after.metrics.every(m=>m.greenRegion>100),after);
    check('linked after uses same pose and target',samePose(...after.pose));
    // Other actual operation directions, selected from data rather than transformed history.
    for(const id of ['local-blend.block-single-fillet','trivalent-corner.base-self','planar-boolean.overlap-two-axes-subtract']) {
      if(!manifest.cases.some(c=>c.id===id))continue;await select(id);const r=await capture(id);
      check(`${id}: features visible or explicit fallback`,r.pose.every(p=>p.feature?.samples ? p.feature.visibleFraction>0.25 : /全模型/.test(p.feature?.reason)),r.pose);
    }
    await select('support-chain.asymmetric43-base');await drag(120,-80);const manual=await stats();
    check('linked orbit identical',samePose(...manual));
    await page.getByText('后态 (After)',{exact:true}).click();await wait();check('manual orbit survives step',samePose((await stats())[0],manual[0]),{manual,now:await stats()});
    await page.locator('[title="自动按快照播放"]').click();await page.waitForTimeout(1200);await page.locator('[title="暂停"]').click();await wait();check('manual orbit survives playback',samePose((await stats())[0],manual[0]),{manual,now:await stats()});
    await page.getByTestId('btn-toggle-sync').click();const independent=await stats();await drag(-80,40);const independent2=await stats();check('unlinked orbit remains independent',!samePose(independent2[0],independent[0])&&samePose(independent2[1],independent[1]));await page.getByTestId('btn-toggle-sync').click();
    for(const width of [320,390]) {
      await page.setViewportSize({width,height:844});await page.waitForTimeout(250);
      check(`${width}: no document overflow`,await page.evaluate(()=>document.documentElement.scrollWidth)<=width);
      check(`${width}: legend scrolls in own row`,await page.getByTestId('legend-body').evaluate(e=>e.scrollWidth>e.clientWidth&&e.clientHeight<40));
      await page.getByText(/API 与参数/).click();check(`${width}: tab opens dialog`,await page.getByRole('dialog').count()===1);
      await page.screenshot({path:path.join(out,`candidate-${width}-drawer.png`)});await page.keyboard.press('Escape');
      await page.screenshot({path:path.join(out,`candidate-${width}.png`)});
    }
    await page.setViewportSize({width:1440,height:900});await page.waitForTimeout(250);
    // Preserve 424 ownership under delayed, failed and absent destination geometry.
    const other='planar-boolean.overlap-two-axes-subtract';
    await page.reload();await wait();await select('support-chain.asymmetric43-base');
    await page.route('**/meshes/*.json*',async r=>{await new Promise(done=>setTimeout(done,400));await r.continue().catch(()=>{});});
    await page.locator(`[data-case-id="${other}"]`).click();await page.waitForTimeout(80);
    check('oldV3b delayed destination clears previous snapshot',(await stats()).every(s=>s.body===''&&s.triangles===0));await wait();
    const destination=await stats();await page.getByText('复位视角',{exact:true}).click();await page.waitForTimeout(350);const reset=await stats();check('oldV3b owned fit equals explicit reset',destination.every((s,i)=>samePose(s,reset[i])));
    await page.unroute('**/meshes/*.json*');
    const missing=manifest.cases.find(c=>!c.steps.length);await page.locator(`[data-case-id="${missing.id}"]`).click();await page.waitForTimeout(150);check('missing destination clears both panes',(await stats()).every(s=>s.body===''&&s.triangles===0));
    await select('support-chain.asymmetric43-base');
    await page.reload();await wait();await select('support-chain.asymmetric43-base');let aborted=0;
    await page.route('**/meshes/*.json*',async r=>{aborted++;await r.abort();});await page.locator(`[data-case-id="${other}"]`).click();await page.waitForTimeout(500);
    check('aborted destination clears both panes',aborted>=2&&(await stats()).every(s=>s.body===''&&s.triangles===0));await page.unroute('**/meshes/*.json*');await select('support-chain.asymmetric43-base');await select(other);check('request recovery restores fair owned fit',samePose(...await stats()));
  }
  check('no uncaught browser errors',errors.length===0,errors);
} finally { await browser.close(); server.close(); fs.writeFileSync(path.join(out,`${baseline?'baseline':'candidate'}-report.json`),JSON.stringify({run:manifest.run,rows,errors},null,2)); }
if(rows.some(r=>!r.ok))process.exitCode=1;
""");
process.ArgumentList.Add(Path.GetFullPath(Path.Combine(scriptDir, args.ElementAtOrDefault(0) ?? "../data")));
process.ArgumentList.Add(args.ElementAtOrDefault(1) ?? "candidate");
using var child = Process.Start(process)!;
child.WaitForExit();
return child.ExitCode;

static string ScriptPath([CallerFilePath] string path = "") => path;
