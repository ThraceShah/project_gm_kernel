#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

var review = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "..", "..", "..", "temp_docs", "ui-c710-review"));
var start = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(review, "web") };
start.ArgumentList.Add("--input-type=module");
start.ArgumentList.Add("--eval");
start.ArgumentList.Add("""
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import http from 'node:http';
import crypto from 'node:crypto';
import {chromium} from 'playwright-core';
const data=path.resolve('../data'),out=path.resolve('../independent');fs.mkdirSync(out,{recursive:true});
const indexBytes=fs.readFileSync(path.join(data,'index.json')),index=JSON.parse(indexBytes);
const hash=crypto.createHash('sha256').update(indexBytes).digest('hex');
const run=index.runs.find(r=>r.id===index.latestRunId),manifest=JSON.parse(fs.readFileSync(path.join(data,run.manifest)));
const runRoot=path.dirname(path.join(data,run.manifest));
const pickingOnly=process.argv[1]==='--picking-only';
const rows=[],errors=[];const check=(name,ok,detail)=>{rows.push({name,ok:!!ok,detail});console.log(`${ok?'PASS':'FAIL'} ${name} ${detail?JSON.stringify(detail):''}`);};
const server=http.createServer((req,res)=>{const u=decodeURIComponent(req.url.split('?')[0]),file=u.startsWith('/data/')?path.join(data,u.slice(6)):path.join(path.resolve('dist'),u==='/'?'index.html':u);try{res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.json')?'application/json':'text/html');res.end(fs.readFileSync(file));}catch{res.statusCode=404;res.end('missing');}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const cache=path.join(os.homedir(),'.cache/ms-playwright'),exe=fs.readdirSync(cache).filter(n=>/^chromium-/.test(n)).sort().reverse().flatMap(n=>['chrome-linux64/chrome','chrome-linux/chrome'].map(p=>path.join(cache,n,p))).find(p=>fs.existsSync(p));
const browser=await chromium.launch({executablePath:exe,headless:true,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1965,height:1228}});page.on('pageerror',e=>errors.push(e.message));
const hosts=()=>page.getByTestId('viewer-canvas-host');
const stats=()=>hosts().evaluateAll(es=>es.map(e=>({body:e.dataset.bodyId,faces:+e.dataset.faceCount,edges:+e.dataset.edgeCount,triangles:+e.dataset.triangleCount,pose:e.dataset.cameraPos,target:e.dataset.cameraTarget,feature:JSON.parse(e.dataset.featureVisibility||'null')})));
const wait=async()=>{await page.waitForFunction(()=>[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')].length===2&&[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')].every(e=>e.dataset.bodyId));await page.waitForTimeout(350);};
const select=async id=>{await page.locator(`[data-case-id="${id}"]`).click();await wait();await page.waitForTimeout(100);};
const pixels=async()=>hosts().evaluateAll(es=>es.map(e=>{const c=e.querySelector('canvas'),x=document.createElement('canvas');x.width=c.width;x.height=c.height;const ctx=x.getContext('2d');ctx.drawImage(c,0,0);const d=ctx.getImageData(0,0,x.width,x.height).data,yellow=[];let green=0;for(let i=0;i<d.length;i+=4){const [r,g,b]=[d[i],d[i+1],d[i+2]];if(r>140&&g>110&&b<90&&r-b>80&&Math.abs(r-g)<80)yellow.push([i/4%x.width,Math.floor(i/4/x.width)]);if(g>r+15&&g>b+15&&g>40)green++;}const p=yellow[Math.floor(yellow.length/2)],rect=c.getBoundingClientRect();return {yellow:yellow.length,green,point:p?{x:rect.x+p[0]*rect.width/x.width,y:rect.y+p[1]*rect.height/x.height}:null,width:x.width,height:x.height};}));
const same=(a,b)=>a.pose===b.pose&&a.target===b.target;
try{
 check('published input index matches dispatched hash',hash==='117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990',hash);
 await page.goto(`http://127.0.0.1:${server.address().port}/`);await wait();await select('support-chain.asymmetric43-base');
 if(pickingOnly){
  const points=await hosts().evaluateAll(es=>es.map(e=>{const c=e.querySelector('canvas'),x=document.createElement('canvas');x.width=c.width;x.height=c.height;const ctx=x.getContext('2d');ctx.drawImage(c,0,0);const d=ctx.getImageData(0,0,x.width,x.height).data,list=[];for(let i=0;i<d.length;i+=4){const [r,g,b]=[d[i],d[i+1],d[i+2]];if(r>140&&g>110&&b<90&&r-b>80&&Math.abs(r-g)<80)list.push([i/4%x.width,Math.floor(i/4/x.width)]);}list.sort((a,b)=>a[0]-b[0]);const rect=c.getBoundingClientRect();return [.25,.5,.75].map(q=>{const p=list[Math.floor(list.length*q)];return {q,x:rect.x+(p[0]+.5)*rect.width/x.width,y:rect.y+(p[1]+.5)*rect.height/x.height};});}));
  const c=manifest.cases.find(c=>c.id==='support-chain.asymmetric43-base');
  for(const [i,side] of ['gm','pk'].entries()){const mesh=JSON.parse(fs.readFileSync(path.join(runRoot,c.steps[0][side].before))),ids=mesh.edges.filter(e=>e.role==='selected').map(e=>String(e.id));for(const p of points[i]){await page.mouse.click(p.x,p.y);await page.waitForTimeout(80);const picked=await page.getByTestId('entity-inspector').textContent().catch(()=>null);check(`${side} interior pixel-center ${p.q}: selected edge pick`,picked?.includes('边 [')&&ids.some(id=>picked.includes('['+id+']')),{point:p,picked,expected:ids});await page.getByLabel('清除实体选择').click();}}
  check('no picking page errors',errors.length===0,errors);
 }else{
 const before=await stats(),beforePixels=await pixels();
 check('desktop default collapsed and one external single-line legend',await page.getByRole('dialog').count()===0&&await page.getByTestId('legend-body').count()===1&&await page.getByTestId('legend-body').evaluate(e=>e.clientHeight<40&&e.getBoundingClientRect().bottom<=document.querySelector('canvas').getBoundingClientRect().top));
 check('real selected edge has visibility and projected length, not just yellow width',before.every(s=>s.feature.visibleFraction>.9&&s.feature.projectedPixels>100),{before,beforePixels});
 check('linked before pose shared',same(...before));
 const c=manifest.cases.find(c=>c.id==='support-chain.asymmetric43-base');
 const expected=['gm','pk'].map(side=>{const mesh=JSON.parse(fs.readFileSync(path.join(runRoot,c.steps[0][side].before)));return {faces:mesh.faces.length,edges:mesh.edges.length,triangles:mesh.faces.reduce((n,f)=>n+f.indices.length/3,0),selected:mesh.edges.filter(e=>e.role==='selected').map(e=>String(e.id))};});
 check('LineSegments2 does not count as faces or triangles',before.every((s,i)=>s.faces===expected[i].faces&&s.edges===expected[i].edges&&s.triangles===expected[i].triangles),{actual:before.map(({faces,edges,triangles})=>({faces,edges,triangles})),expected});
 for(const i of [0,1]){const p=(await pixels())[i].point;await page.mouse.click(p.x,p.y);await page.waitForTimeout(80);const picked=await page.getByTestId('entity-inspector').textContent().catch(()=>null);check(`pane ${i}: actual visible yellow stroke picks selected edge`,picked?.includes('边 [')&&expected[i].selected.some(id=>picked.includes('['+id+']')),{point:p,picked,selected:expected[i].selected});if(await page.getByLabel('清除实体选择').count())await page.getByLabel('清除实体选择').click();}
 await page.screenshot({path:path.join(out,'desktop-before.png')});
 check('original PK summary and historical ratio placement',(await page.getByTestId('comparison-summary').textContent()).includes('12 条表示诊断')&&!(await page.textContent('body')).includes('644.54x'));
 const canvas=await hosts().first().boundingBox();await page.getByTestId('comparison-summary').click();await page.waitForTimeout(80);
 const panel=await page.getByRole('dialog').boundingBox();
 check('evidence covers 3D without resizing it',JSON.stringify(canvas)===JSON.stringify(await hosts().first().boundingBox())&&panel.y<=canvas.y&&panel.y+panel.height>=canvas.y+canvas.height,{canvas,panel});
 const comparison=await page.getByTestId('comparison-details').textContent();check('raw local differences and actual mask remain explicit',comparison.includes('local=21930')&&comparison.includes('global=21921')&&comparison.includes('facePairs=7')&&comparison.includes('max_diffs=1024')&&comparison.includes('all_tests=false')&&comparison.includes('acc_dev_tests=false')&&comparison.includes('tol_dev_tests=true')&&comparison.includes('n_cht_pts'),comparison);
 const groupNames=await page.getByTestId('grouped-checks').locator('summary').allTextContents();
 check('requested six original-check groups',groupNames.length===6,groupNames);
 await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.forEach(e=>e.open=true));
 check('all 29 source rows remain',await page.getByTestId('grouped-checks').locator('tbody tr').count()===29);
 check('overlay content actually scrolls internally',await page.getByTestId('inspection-content').evaluate(e=>{e.scrollTop=400;return getComputedStyle(e).overflowY==='auto'&&e.scrollTop>0&&e.scrollHeight>e.clientHeight;}));
 await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.forEach(e=>e.open=false));
 await page.getByTestId('grouped-checks').locator('summary').last().focus();await page.keyboard.press('Tab');check('Tab traps inside evidence',await page.getByRole('dialog').evaluate(e=>document.activeElement===e.querySelector('button')));
 await page.keyboard.press('Escape');check('Escape restores invoking summary focus',await page.getByRole('dialog').count()===0&&await page.getByTestId('comparison-summary').evaluate(e=>document.activeElement===e));
 await page.getByTestId('header-meta-toggle').click();const provenance=await page.getByTestId('data-version').textContent();check('manifest collection time, commit and sample scope, not index time',provenance.includes(manifest.run.generatedAt)&&provenance.includes(manifest.run.commit)&&provenance.includes('"executed":17')&&provenance.includes('"registered":463')&&provenance.includes('"samplesRequested":1')&&!provenance.includes(index.generatedAt),provenance);await page.keyboard.press('Escape');
 await page.getByText('后态 (After)',{exact:true}).click();await wait();check('snapshot step retains manual/picked camera',same((await stats())[0],before[0]));
 await page.getByTestId('btn-focus-op').click();await page.waitForTimeout(400);const after=await stats(),afterPixels=await pixels();check('after generated faces visible in both panes',after.every(s=>s.feature.visibleFraction>.7&&s.feature.projectedPixels>100)&&afterPixels.every(p=>p.green>1000),{after,afterPixels});check('linked after pose shared',same(...after));await page.screenshot({path:path.join(out,'desktop-after.png')});
 await select('local-blend.block-single-fillet');const localRaw=JSON.parse(fs.readFileSync(path.join(runRoot,manifest.cases.find(c=>c.id==='local-blend.block-single-fillet').evidence.find(e=>/compare.*json$/.test(e.path)).path)));await page.getByTestId('comparison-summary').click();check('other case summary reads its actual comparison', (await page.getByTestId('comparison-results').first().textContent()).includes(`global=${localRaw.globalResult}`)&&(await page.getByTestId('comparison-results').first().textContent()).includes(`local=${localRaw.localResult}`));await page.keyboard.press('Escape');
 await page.route('**/evidence/compare-*.json',r=>r.abort());await select('support-chain.asymmetric43-base');await page.waitForTimeout(80);check('missing comparison explicitly unavailable',!(await page.getByTestId('comparison-summary').textContent()).includes('通过')&&(await page.getByTestId('comparison-summary').textContent()).includes('不可用'));await page.unroute('**/evidence/compare-*.json');
 for(const width of [320,390]){await page.setViewportSize({width,height:844});await page.waitForTimeout(200);check(`${width}: document fits and legend stays its own row`,await page.evaluate(()=>document.documentElement.scrollWidth)<=width&&await page.getByTestId('legend-body').evaluate(e=>e.clientHeight<40&&e.scrollWidth>e.clientWidth));await page.getByText(/逐项验收/).click();check(`${width}: overlay close receives focus`,await page.getByTestId('inspection-close').evaluate(e=>document.activeElement===e));await page.screenshot({path:path.join(out,`${width}-overlay.png`)});await page.keyboard.press('Escape');check(`${width}: Escape closes evidence`,await page.getByRole('dialog').count()===0);await page.screenshot({path:path.join(out,`${width}-closed.png`)});}
 check('no uncaught browser errors',errors.length===0,errors);
 }
}finally{await browser.close();server.close();fs.writeFileSync(path.join(out,pickingOnly?'picking-report.json':'report.json'),JSON.stringify({candidate:'c710171bfea776a77217a3c417bc73f7906ac4fd',base:'424e85d6aac32c2afe7cad12cfd04bea4f0037ab',indexHash:hash,run:manifest.run.id,rows,errors},null,2));}
if(rows.some(r=>!r.ok))process.exitCode=1;
""");
start.ArgumentList.Add("--");
start.ArgumentList.Add(args.ElementAtOrDefault(0) ?? "");
using var process = Process.Start(start)!;
process.WaitForExit();
return process.ExitCode;
static string ScriptPath([CallerFilePath] string path = "") => path;
