#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
var scripts=Path.GetDirectoryName(ScriptPath())!;
var start=new ProcessStartInfo("node"){WorkingDirectory=Path.GetFullPath("../web",scripts),RedirectStandardInput=true};start.ArgumentList.Add("-");
using var child=Process.Start(start)!;
child.StandardInput.Write("""
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),crypto=require('node:crypto'),os=require('node:os');
const {chromium}=require('playwright-core');
(async()=>{
const out=path.resolve('../temp_docs/uv-static'),retention=JSON.parse(fs.readFileSync(path.join(out,'two-round/retention-report.json')));
const bundle=path.resolve('..',retention.rounds[1].bundle),mount='/apps/gm-kernel-progress/',rows=[],errors=[],outside=[];
const hash=b=>crypto.createHash('sha256').update(b).digest('hex'),read=p=>JSON.parse(fs.readFileSync(path.join(bundle,p)));
const check=(name,ok,detail)=>{rows.push({name,ok:!!ok,detail});console.log(`${ok?'PASS':'FAIL'} ${name} ${JSON.stringify(detail??'')}`);};
const server=http.createServer((req,res)=>{const u=new URL(req.url,'http://localhost').pathname;if(!u.startsWith(mount)){if(u!=='/favicon.ico')outside.push(u);res.writeHead(404);res.end();return;}try{const f=path.join(bundle,u.slice(mount.length)||'index.html');res.setHeader('Content-Type',f.endsWith('.js')?'text/javascript':f.endsWith('.css')?'text/css':f.endsWith('.json')?'application/json':'text/html');res.end(fs.readFileSync(f));}catch{res.writeHead(404);res.end();}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));const base=`http://127.0.0.1:${server.address().port}${mount}`;
const cache=path.join(os.homedir(),'.cache/ms-playwright'),exe=fs.readdirSync(cache).filter(n=>/^chromium-/.test(n)).sort().reverse().flatMap(n=>['chrome-linux64/chrome','chrome-linux/chrome'].map(p=>path.join(cache,n,p))).find(p=>fs.existsSync(p));
const browser=await chromium.launch({executablePath:exe,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1965,height:1228}});page.on('pageerror',e=>errors.push(e.message));
const uv=read('display/real-uv/display-manifest.json');
try{
await page.goto(base+'?display=real-uv');
for(const stage of ['before','after']){
 await page.getByText(stage==='before'?'前态 (Before)':'后态 (After)',{exact:true}).click();
 const observed=await(await page.waitForFunction(stage=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')],ps=[...document.querySelectorAll('[data-testid="uv-provenance"]')];if(es.length!==2||ps.length!==2||!es.every(e=>e.dataset.uLineCount==='5'&&e.dataset.vLineCount==='5')||!ps.every(e=>e.textContent.includes(' / '+stage)))return false;return{panes:es.map(e=>({...e.dataset})),source:ps.map(e=>e.textContent)};},stage)).jsonValue();
 const expected=['GM','PK'].map(kernel=>{const i=uv.items.find(i=>i.kernel===kernel&&i.stage===stage);return{snapshot:i.sourceSnapshot,triangles:read('display/real-uv/'+i.meshPath).faces.reduce((n,f)=>n+f.indices.length/3,0)};});
 check(`nested app ${stage}: both actual receipt snapshots U5/V5`,observed.panes.every((p,i)=>+p.triangleCount===expected[i].triangles&&observed.source[i].includes(expected[i].snapshot)),{expected,observed});
}
await page.screenshot({path:path.join(out,'nested-uv-after.png')});
const files=[];function walk(d){for(const n of fs.readdirSync(d)){const f=path.join(d,n);if(fs.statSync(f).isDirectory())walk(f);else files.push(path.relative(path.join(bundle,'display/real-uv'),f).replaceAll('\\','/'));}}walk(path.join(bundle,'display/real-uv'));
const fetched=[];for(const name of files){const r=await page.request.get(base+'display/real-uv/'+name);fetched.push({name,status:r.status(),sha256:hash(await r.body()),expected:hash(fs.readFileSync(path.join(bundle,'display/real-uv',name)))});}
check('all 15 frozen UI resources fetch with exact hashes under app base',fetched.length===15&&fetched.every(f=>f.status===200&&f.sha256===f.expected),fetched);
const references=[];for(const item of uv.items){const meshUrl=base+'display/real-uv/'+item.meshPath,mesh=await(await page.request.get(meshUrl)).json(),summaryUrl=new URL(mesh.displayReceipt,meshUrl).href,summary=await(await page.request.get(summaryUrl)).json();for(const url of [summaryUrl,new URL(summary.meshPath,summaryUrl).href,new URL(summary.derivativePath,summaryUrl).href])references.push({path:new URL(url).pathname,status:(await page.request.get(url)).status()});}
check('relative mesh/summary/ISO chain stays in the UI namespace',references.every(r=>r.status===200&&r.path.startsWith(mount+'display/real-uv/')),references);
check('old producer overlay namespace is absent',(await page.request.get(base+'data/display/real-uv/index.json')).status()===404);
let mutations=0;await page.route('**/display/real-uv/manifest.json*',async route=>{const r=await route.fetch(),j=await r.json();j.cases[0].steps[0].gm.after=j.cases[0].steps[0].gm.before;mutations++;await route.fulfill({response:r,json:j});});
await page.getByRole('button',{name:'刷新',exact:true}).click();await page.waitForLoadState('networkidle');await page.getByText('后态 (After)',{exact:true}).click();
const rejected=await(await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];if(es.length!==2||es[0].dataset.triangleCount!=='0'||es[1].dataset.triangleCount!=='130'||!document.body.textContent.includes('unavailable'))return false;return es.map(e=>({...e.dataset}));})).jsonValue();
check('nested warm stage attack is actually intercepted and rejected',mutations>0&&rejected[0].uLineCount==='0'&&rejected[1].uLineCount==='5',{mutations,rejected});await page.unroute('**/display/real-uv/manifest.json*');
await page.goto(base);await page.locator('[data-case-id="support-chain.asymmetric43-base"]').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>e.dataset.triangleCount==='102'&&e.dataset.uLineCount==='0');});await page.waitForTimeout(250);
const index=read('data/index.json'),run=index.runs.find(r=>r.id===index.latestRunId),manifest=read('data/'+run.manifest),c=manifest.cases.find(c=>c.id==='support-chain.asymmetric43-base');
check('nested legacy source retains exact index and original 610 case',hash(await(await page.request.get(base+'data/index.json')).body())==='117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990'&&manifest.run.commit.startsWith('610e3ce'));
const points=await page.getByTestId('viewer-canvas-host').evaluateAll(es=>es.map(e=>{const c=e.querySelector('canvas'),t=document.createElement('canvas');t.width=c.width;t.height=c.height;const x=t.getContext('2d');x.drawImage(c,0,0);const d=x.getImageData(0,0,t.width,t.height).data,p=[];for(let i=0;i<d.length;i+=4){if(d[i]>140&&d[i+1]>110&&d[i+2]<90&&d[i]-d[i+2]>80&&Math.abs(d[i]-d[i+1])<80)p.push([i/4%t.width,Math.floor(i/4/t.width)]);}p.sort((a,b)=>a[0]-b[0]);const q=p[Math.floor(p.length*.25)],r=c.getBoundingClientRect();return{x:r.x+(q[0]+.5)*r.width/t.width,y:r.y+(q[1]+.5)*r.height/t.height,pixels:p.length};}));
for(const [i,side]of ['gm','pk'].entries()){const expected=read('data/'+path.dirname(run.manifest)+'/'+c.steps[0][side].before).edges.filter(e=>e.role==='selected').map(e=>String(e.id));await page.mouse.click(points[i].x,points[i].y);await page.getByTestId('entity-inspector').waitFor();const picked=await page.getByTestId('entity-inspector').textContent();check(`nested ${side} actual yellow 25% pixel picks original selected edge`,expected.some(id=>picked.includes('['+id+']'))&&picked.includes('边 ['),{point:points[i],expected,picked});await page.getByLabel('清除实体选择').click();}
const box=await page.getByTestId('viewer-canvas-host').first().boundingBox();await page.getByTestId('comparison-summary').click();await page.getByRole('dialog').waitFor();
check('nested legacy drawer preserves overlay and close focus',JSON.stringify(box)===JSON.stringify(await page.getByTestId('viewer-canvas-host').first().boundingBox())&&await page.getByTestId('inspection-close').evaluate(e=>document.activeElement===e));await page.keyboard.press('Escape');await page.getByRole('dialog').waitFor({state:'detached'});
check('nested legacy Escape restores invoking comparison focus',await page.getByTestId('comparison-summary').evaluate(e=>document.activeElement===e));await page.screenshot({path:path.join(out,'nested-legacy.png')});
check('no asset requests escape app base and no browser errors',outside.length===0&&errors.length===0,{outside,errors});
}finally{await browser.close();await new Promise(r=>server.close(r));fs.writeFileSync(path.join(out,'base-browser-report.json'),JSON.stringify({candidate:retention.candidate,bundle:retention.rounds[1].bundle,uiHash:retention.uiHash,mount,rows,errors,outside},null,2));}
if(rows.some(r=>!r.ok))process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
""");
child.StandardInput.Close();child.WaitForExit();return child.ExitCode;
static string ScriptPath([CallerFilePath]string path="")=>path;
