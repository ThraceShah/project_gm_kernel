#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
var scripts=Path.GetDirectoryName(ScriptPath())!;
var start=new ProcessStartInfo("node"){WorkingDirectory=Path.GetFullPath("../web",scripts),RedirectStandardInput=true};
start.ArgumentList.Add("-");
start.ArgumentList.Add(Path.GetFullPath(args.ElementAtOrDefault(0)??"../temp_docs/reviewed-inspection/bundle-final",scripts));
start.ArgumentList.Add(Path.GetFullPath("../../gm-web-primary-evidence-audit/docs/reviews/advanced-modeling/web-primary-evidence-audit-610e3ce.json",scripts));
using var child=Process.Start(start)!;
child.StandardInput.Write("""
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),os=require('node:os'),crypto=require('node:crypto');
const {chromium}=require('playwright-core');
(async()=>{
const bundle=process.argv[2],audit=JSON.parse(fs.readFileSync(process.argv[3])),out=path.resolve('../temp_docs/reviewed-inspection');
const read=p=>JSON.parse(fs.readFileSync(path.join(bundle,'data',p)));
const index=read('index.json'),run=index.runs.find(r=>r.id===index.latestRunId),manifest=read(run.manifest),dir=path.dirname(run.manifest);
const uv=read('display/real-uv/display-manifest.json'),rows=[],errors=[];
const check=(name,ok,detail)=>{rows.push({name,ok:!!ok,detail});console.log(`${ok?'PASS':'FAIL'} ${name} ${JSON.stringify(detail??'')}`);};
const server=http.createServer((req,res)=>{try{const p=path.join(bundle,new URL(req.url,'http://localhost').pathname==='/'?'index.html':new URL(req.url,'http://localhost').pathname);res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.css')?'text/css':p.endsWith('.json')?'application/json':'text/html');res.end(fs.readFileSync(p));}catch{res.writeHead(404);res.end();}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const cache=path.join(os.homedir(),'.cache/ms-playwright'),exe=fs.readdirSync(cache).filter(n=>/^chromium-/.test(n)).sort().reverse().flatMap(n=>['chrome-linux64/chrome','chrome-linux/chrome'].map(p=>path.join(cache,n,p))).find(p=>fs.existsSync(p));
const browser=await chromium.launch({executablePath:exe,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1965,height:1228}});page.on('pageerror',e=>errors.push(e.message));
const base=`http://127.0.0.1:${server.address().port}`;
const stats=()=>page.getByTestId('viewer-canvas-host').evaluateAll(es=>es.map(e=>({...e.dataset})));
const settle=async()=>{await page.waitForLoadState('networkidle');await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>{const r=e.closest('[data-testid="viewer-root"]');return !r.querySelector('[data-testid="viewer-loading"]')&&(r.querySelector('[data-testid="viewer-empty"]')?e.dataset.triangleCount==='0':!!e.dataset.bodyId);});});};
try{
await page.goto(base);await settle();
check('prepared root index retains exact dispatched bytes',crypto.createHash('sha256').update(await(await page.request.get(base+'/data/index.json')).body()).digest('hex')==='117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990');
const legacy=manifest.cases.filter(c=>c.steps.some(s=>s.gm?.before||s.pk?.before));
check('published source remains 610 and exactly 17 drawable cases',manifest.run.commit.startsWith('610e3ce')&&legacy.length===17,{run:manifest.run.id,commit:manifest.run.commit,count:legacy.length});
for(const c of legacy){
  await page.locator(`[data-case-id="${c.id}"]`).click();await settle();
  const si=c.steps.findIndex(s=>s.gm?.before||s.pk?.before),step=c.steps[si];
  if(si)await page.getByRole('button',{name:step.title||`步骤 ${si+1}`,exact:true}).click();
  await page.getByText('前态 (Before)',{exact:true}).click();await settle();
  const expected=['gm','pk'].map(side=>{const r=step[side];if(!r?.before)return 0;return [r.before,...(r.tools||[])].reduce((n,p)=>n+read(path.join(dir,p)).faces.reduce((t,f)=>t+f.indices.length/3,0),0);});
  const actual=await stats();check(`legacy ${c.id}: original geometry with UV unavailable`,actual.length===2&&actual.every((s,i)=>+s.triangleCount===expected[i]&&s.uLineCount==='0'&&s.vLineCount==='0'),{expected,actual:actual.map(s=>({triangles:s.triangleCount,u:s.uLineCount,v:s.vLineCount}))});
}
await page.locator('[data-case-id="support-chain.asymmetric43-base"]').click();await settle();await page.getByTestId('comparison-summary').click();
await page.getByRole('dialog').waitFor();await page.getByTestId('grouped-checks').locator('details').first().waitFor();
await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.forEach(e=>e.open=true));
const groups=await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.map(e=>({title:e.querySelector('summary').textContent,ids:[...e.querySelectorAll('tbody tr')].map(r=>r.querySelectorAll('td')[1].querySelectorAll('div')[1].textContent)})));
check('six groups exactly retain independent audit membership and 29 rows',groups.length===6&&audit.coverageGroups.every(g=>groups.some(actual=>JSON.stringify([...actual.ids].sort())===JSON.stringify(g.checks.map(c=>c.id).sort())))&&groups.reduce((n,g)=>n+g.ids.length,0)===29,groups);
await page.keyboard.press('Escape');
await page.goto(base+'/?display=real-uv');await settle();
check('UV is explicitly render-only without published baseline label',!(await page.getByTestId('run-select').textContent()).includes('已发布基线')&&(await page.getByTestId('run-select').textContent()).includes('仅显示派生包')&&!(await page.getByTestId('comparison-summary').textContent()).includes('通过'));
for(const stage of ['before','after']){
 await page.getByText(stage==='before'?'前态 (Before)':'后态 (After)',{exact:true}).click();await settle();
 await page.waitForFunction(stage=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')],ps=[...document.querySelectorAll('[data-testid="uv-provenance"]')];return es.length===2&&ps.length===2&&es.every(e=>e.dataset.uLineCount==='5'&&e.dataset.vLineCount==='5')&&ps.every(e=>e.textContent.includes(' / '+stage));},stage);
 const actual=await stats(),provenance=await page.getByTestId('uv-provenance').allTextContents();
 const expected=['GM','PK'].map(kernel=>{const item=uv.items.find(i=>i.kernel===kernel&&i.stage===stage);const mesh=read('display/real-uv/'+item.meshPath);return {triangles:mesh.faces.reduce((n,f)=>n+f.indices.length/3,0),snapshot:item.sourceSnapshot};});
 check(`UV ${stage}: actual four-snapshot triangles/U5V5 and receipt kernel/stage`,actual.length===2&&actual.every((s,i)=>+s.triangleCount===expected[i].triangles&&s.uLineCount==='5'&&s.vLineCount==='5'&&provenance[i].includes(expected[i].snapshot+' / '+stage)),{expected,actual});
}
await page.getByTestId('btn-focus-op').click();await page.waitForTimeout(500);
const pose=(await stats()).map(s=>[s.cameraPos,s.cameraTarget]);
await page.evaluate(()=>{window.uvOn=[...document.querySelectorAll('[data-testid="viewer-canvas-host"] canvas')].map(c=>{const t=document.createElement('canvas');t.width=c.width;t.height=c.height;const x=t.getContext('2d');x.drawImage(c,0,0);return x.getImageData(0,0,t.width,t.height).data;});});
await page.getByTestId('btn-toggle-iso').click();await page.waitForTimeout(300);
const pixels=await page.evaluate(()=>[...document.querySelectorAll('[data-testid="viewer-canvas-host"] canvas')].map((c,k)=>{const t=document.createElement('canvas');t.width=c.width;t.height=c.height;const x=t.getContext('2d');x.drawImage(c,0,0);const d=x.getImageData(0,0,t.width,t.height).data,on=window.uvOn[k];let changed=0,cyan=0,purple=0;for(let i=0;i<d.length;i+=4){if(Math.abs(on[i]-d[i])+Math.abs(on[i+1]-d[i+1])+Math.abs(on[i+2]-d[i+2])<20)continue;changed++;if(on[i+1]>80&&on[i+2]>120&&on[i+2]-on[i]>40)cyan++;if(on[i]>80&&on[i+2]>120&&on[i+2]-on[i+1]>20&&on[i]-on[i+1]>10)purple++;}return{changed,cyan,purple};}));
check('merged camera exposes real UV pixels in both panes; toggle removes actual lines',pixels.every(p=>p.changed>100&&p.cyan>0&&p.purple>0)&&(await stats()).every(s=>s.uLineCount==='0'&&s.vLineCount==='0')&&JSON.stringify(pose)===JSON.stringify((await stats()).map(s=>[s.cameraPos,s.cameraTarget])),pixels);
await page.getByTestId('btn-toggle-iso').click();await page.waitForTimeout(250);await page.screenshot({path:path.join(out,'combined-uv-after.png')});
await page.getByTestId('header-meta-toggle').click();check('UV package time is never relabeled capture time',(await page.getByTestId('data-version').textContent()).includes('显示包生成时间，非采集时间'));
await page.getByText('性能指标',{exact:true}).click();check('UV drawer has no inherited ratio or measurement',(await page.textContent('body')).includes('无性能采样或新增几何验收')&&!(await page.textContent('body')).includes('644.54x'));await page.keyboard.press('Escape');
for(const width of [320,390]){
 await page.setViewportSize({width,height:844});await page.waitForTimeout(250);const closed=await page.getByTestId('viewer-canvas-host').evaluateAll(es=>es.map(e=>({h:e.clientHeight,w:e.clientWidth})));
 check(`UV ${width}: shared external legend and usable dual canvases`,await page.getByTestId('legend-body').count()===1&&closed.every(s=>s.h>150&&s.w===width)&&await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),closed);
 await page.getByText(/API 与参数/).click();check(`UV ${width}: drawer overlays without shrinking and receives focus`,JSON.stringify(closed)===JSON.stringify(await page.getByTestId('viewer-canvas-host').evaluateAll(es=>es.map(e=>({h:e.clientHeight,w:e.clientWidth}))))&&await page.getByTestId('inspection-close').evaluate(e=>document.activeElement===e));await page.keyboard.press('Escape');
 await page.getByRole('dialog').waitFor({state:'detached'});
 check(`UV ${width}: Escape closes, restores focus and retains actual U5/V5`,await page.getByText(/API 与参数/).evaluate(e=>document.activeElement===e.closest('button'))&&(await stats()).every(s=>s.uLineCount==='5'&&s.vLineCount==='5'));await page.screenshot({path:path.join(out,`combined-uv-${width}.png`)});
}
const references=[];for(const item of uv.items){const meshUrl=base+'/data/display/real-uv/'+item.meshPath,mesh=await(await page.request.get(meshUrl)).json();const summaryUrl=new URL(mesh.displayReceipt,meshUrl).href,summary=await(await page.request.get(summaryUrl)).json();for(const u of [meshUrl,base+'/data/display/real-uv/'+item.path,summaryUrl,new URL(summary.meshPath,summaryUrl).href,new URL(summary.derivativePath,summaryUrl).href])references.push({path:new URL(u).pathname,status:(await page.request.get(u)).status()});}
check('all UV original mesh/line/safe-summary references fetch in final bundle',references.every(r=>r.status===200),references);
check('no uncaught fused browser errors',errors.length===0,errors);
}finally{await browser.close();await new Promise(r=>server.close(r));fs.writeFileSync(path.join(out,'fusion-report.json'),JSON.stringify({product:'6a433222da0775dcb5524d7751d94e8f096fe797',layoutParent:'53f9871e55574ec66a96f12904293515ce40c23c',uvParent:'e8dd252b2a1baa848c2c59ad9c9b43dd9dc845ba',rows,errors},null,2));}
if(rows.some(r=>!r.ok))process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
""");
child.StandardInput.Close();child.WaitForExit();return child.ExitCode;
static string ScriptPath([CallerFilePath]string path="")=>path;
