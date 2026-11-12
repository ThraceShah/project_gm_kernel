#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Browser re-verification of the f463 UI fix against locally assembled previews
// (f463 built assets + read-only publisher run data; 8813 normal, 8814 missing
// overview, 8815 derived event copy). Verifies the original 17 flows plus the
// step-landing contract: first cross-run click lands on the exact run/case/step
// (op-5 -> operation-5), same-run case/step switches land correctly, back/reopen
// and reload behave, evidence labeling distinguishes hash-anchored private
// originals from clickable links, and event row/summary counters move together
// in real pixels. Receipt + screenshots under temp_docs/f463-reverify/browser/.
var here=Path.GetDirectoryName(ScriptPath())!;var root=Path.GetFullPath("..",here);
var qaOut=Path.Combine(root,"temp_docs","f463-reverify","browser");Directory.CreateDirectory(qaOut);
var info=new ProcessStartInfo("node"){WorkingDirectory=Path.GetFullPath(Path.Combine("..","gm-web-real-uv","web"),root),RedirectStandardInput=true};
info.ArgumentList.Add("-");
info.Environment["QA_OUT"]=qaOut;
info.Environment["QA_BROWSER_CACHE"]=Path.GetFullPath(Path.Combine("..","..","..","..",".cache","ms-playwright"),root);
using var p=Process.Start(info)!;p.StandardInput.Write("""
const fs=require('node:fs'),path=require('node:path');const{chromium}=require('playwright-core');
(async()=>{const out=process.env.QA_OUT,cache=process.env.QA_BROWSER_CACHE;
const exe=fs.readdirSync(cache).filter(n=>/^chromium-/.test(n)).sort().reverse().flatMap(n=>['chrome-linux64/chrome','chrome-linux/chrome'].map(p=>path.join(cache,n,p))).find(p=>fs.existsSync(p));
const browser=await chromium.launch({executablePath:exe,args:['--no-sandbox','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--no-proxy-server']});
const base='http://127.0.0.1:8813/apps/gm-kernel-progress/';
const baseEvent='http://127.0.0.1:8815/apps/gm-kernel-progress/';
const baseBroken='http://127.0.0.1:8814/apps/gm-kernel-progress/';
const rows=[],errors=[];
const check=(name,ok,detail)=>{rows.push({name,ok:!!ok,detail});console.log(`${ok?'PASS':'FAIL'} ${name} :: ${JSON.stringify(detail||{})}`);};
const step=async(page)=>{const el=page.getByTestId('display-current-step');await el.waitFor({timeout:15000});return{index:await el.getAttribute('data-step-index'),text:(await el.textContent()).trim()};};
const context=await browser.newContext({viewport:{width:1600,height:1000}});const page=await context.newPage();
page.on('pageerror',e=>errors.push(e.message));
try{
await page.goto(base,{waitUntil:'networkidle'});
await page.getByTestId('overview-panel').waitFor();
const top=await page.locator('body').textContent();
check('default view is project overview with counters',top.includes('项目整体进度')&&top.includes('累计证据·功能通过 183')&&top.includes('当前版本已复验 5 op'),{});
const rowCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('catalog rows rendered',rowCount===463,{rowCount});
// evidence labeling: hash-anchored private originals, no fake clickable links
await page.locator('[data-testid="catalog-row-local-blend.cylinder-rim-fillet"]').click();
const d1=await page.getByTestId('catalog-detail-local-blend.cylinder-rim-fillet').textContent();
check('query-gap row keeps fail + real evidence + hash-anchored label',d1.includes('5000')&&d1.includes('BodyContainsVector')&&d1.includes('私存原件')&&d1.includes('来源 runner'),{});
check('no fake clickable evidence links',await page.getByTestId('catalog-detail-local-blend.cylinder-rim-fillet').locator('a').count()===0,{});
// cross-suite row now unverified and documented (B1)
await page.locator('[data-testid="catalog-row-planar-boolean.disjoint-unite"]').click();
const d2=await page.getByTestId('catalog-detail-planar-boolean.disjoint-unite').textContent();
check('cross-suite row unverified with borrow documented',d2.includes('本套件无执行证据')||d2.includes('退未验证'),{});
check('cross-suite row is not green',d2.includes('未验证'),{});
await page.screenshot({path:path.join(out,'f463-cross-suite-row.png')});
// search + filters
await page.getByTestId('overview-search').fill('cylinder');await page.waitForTimeout(200);
const searchCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('search narrows the matrix',searchCount>0&&searchCount<40,{searchCount});
await page.getByTestId('overview-search').fill('');
await page.getByTestId('overview-family').selectOption('boolean');await page.waitForTimeout(200);
const boolCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('family filter works',boolCount>0&&boolCount<200,{boolCount});
await page.getByTestId('overview-family').selectOption('all');
await page.getByTestId('overview-issues').check();await page.waitForTimeout(200);
const issueCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('issues filter works',issueCount>0&&issueCount<300,{issueCount});
await page.getByTestId('overview-issues').uncheck();
await page.screenshot({path:path.join(out,'f463-overview-default.png')});
// ---- STEP LANDING 1: first cross-run click, cylinder-unequal-chamfer -> op-5
await page.locator('[data-testid="catalog-row-local-blend.cylinder-unequal-chamfer"]').click();
await page.getByTestId('catalog-model-local-blend.cylinder-unequal-chamfer').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const s1=await step(page);
check('STEP: unequal-chamfer link lands on operation-5 (index 4)',s1.index==='4'&&s1.text.includes('operation-5'),s1);
await page.screenshot({path:path.join(out,'f463-step-unequal-chamfer.png')});
// ---- STEP LANDING 2: same run, same case, different step (op-1)
await page.getByTestId('back-to-overview').click();
await page.getByTestId('overview-panel').waitFor();
await page.locator('[data-testid="catalog-row-local-blend.cylinder-rim-fillet"]').click();
await page.getByTestId('catalog-model-local-blend.cylinder-rim-fillet').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const s2=await step(page);
check('STEP: rim-fillet link lands on operation-1 (index 0) — same run/case, no manifest reload',s2.index==='0'&&s2.text.includes('operation-1'),s2);
// ---- STEP LANDING 3: same run, different case (prism-all-chamfer -> blend.planar-chamfer op-1)
await page.getByTestId('back-to-overview').click();
await page.getByTestId('overview-panel').waitFor();
await page.locator('[data-testid="catalog-row-local-blend.prism-all-chamfer"]').click();
await page.getByTestId('catalog-model-local-blend.prism-all-chamfer').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const s3=await step(page);
const body3=await page.locator('body').textContent();
check('STEP: prism-all-chamfer lands on blend.planar-chamfer operation-1',s3.index==='0'&&s3.text.includes('operation-1')&&body3.includes('planar-chamfer'),s3);
// ---- reopen unequal-chamfer after other case (stale step index must not stick)
await page.getByTestId('back-to-overview').click();
await page.getByTestId('overview-panel').waitFor();
await page.locator('[data-testid="catalog-row-local-blend.cylinder-unequal-chamfer"]').click();
await page.getByTestId('catalog-model-local-blend.cylinder-unequal-chamfer').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const s4=await step(page);
check('STEP: reopen after case switch still lands on operation-5',s4.index==='4'&&s4.text.includes('operation-5'),s4);
// ---- STEP LANDING 5: SAME case, different step (isolates the [selectedCaseId] reset)
await page.getByTestId('back-to-overview').click();
await page.getByTestId('overview-panel').waitFor();
await page.locator('[data-testid="catalog-row-local-blend.cylinder-both-rim-chamfer"]').click();
await page.getByTestId('catalog-model-local-blend.cylinder-both-rim-chamfer').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const s5=await step(page);
check('STEP: same-case step switch (op-4=both-rim-chamfer) lands on operation-4 (index 3)',s5.index==='3'&&s5.text.includes('operation-4'),s5);
// ---- reload behavior
await page.reload({waitUntil:'networkidle'});
await page.getByTestId('overview-panel').waitFor();
check('reload lands on overview again',true,{});
// ---- derived event copy: row flips and top counter moves together
const ctx2=await browser.newContext({viewport:{width:1600,height:1000}});const page2=await ctx2.newPage();
page2.on('pageerror',e=>errors.push('event-page: '+e.message));
await page2.goto(baseEvent,{waitUntil:'networkidle'});
await page2.getByTestId('overview-panel').waitFor();
const evtData=await page2.evaluate(()=>fetch('data/project-overview.json?_t='+Date.now()).then(r=>r.json()));
const failRows=evtData.catalog.filter(c=>c.dimensions.function.status==='fail').length;
const passRows=evtData.catalog.filter(c=>c.dimensions.function.status==='pass').length;
const topText=await page2.locator('body').textContent();
const counterMatch=topText.match(/累计证据·功能通过 (\d+)/);
check('EVENT UI: top counter equals recomputed pass rows',counterMatch&&Number(counterMatch[1])===passRows,{counter:counterMatch&&counterMatch[1],recomputedPassRows:passRows,failRows});
await page2.screenshot({path:path.join(out,'f463-event-summary.png')});
await ctx2.close();
// ---- 390px mobile
const ctx3=await browser.newContext({viewport:{width:390,height:844}});const page3=await ctx3.newPage();
page3.on('pageerror',e=>errors.push('mobile: '+e.message));
await page3.goto(base,{waitUntil:'networkidle'});
await page3.getByTestId('overview-panel').waitFor();
const mobRows=await page3.locator('[data-testid^="catalog-row-"]').count();
check('390px mobile renders overview rows',mobRows===463,{mobRows});
await page3.screenshot({path:path.join(out,'f463-overview-mobile-390.png')});
await ctx3.close();
// ---- failure loading
const ctx4=await browser.newContext({viewport:{width:1200,height:800}});const page4=await ctx4.newPage();
page4.on('pageerror',e=>errors.push('broken: '+e.message));
await page4.goto(baseBroken,{waitUntil:'networkidle'});
const errVisible=await page4.getByTestId('overview-load-error').count();
check('missing overview data shows explicit failure screen (no fake data)',errVisible===1,{});
await page4.screenshot({path:path.join(out,'f463-overview-load-failure.png')});
await ctx4.close();
check('no uncaught JS errors',errors.length===0,{errors});
}catch(e){await page.screenshot({path:path.join(out,'f463-browser-failure.png')}).catch(()=>{});rows.push({name:'EXCEPTION',ok:false,detail:String(e)});console.error(e);}
finally{fs.writeFileSync(path.join(out,'f463-browser-audit.json'),JSON.stringify({checkedUtc:new Date().toISOString(),rows,errors},null,2));await browser.close();}
console.log('DONE checks='+rows.length+' failed='+rows.filter(r=>!r.ok).length);
})().catch(e=>{console.error(e);process.exitCode=1;});
""");p.StandardInput.Close();await p.WaitForExitAsync();
Console.WriteLine("browser audit exit="+p.ExitCode);
return p.ExitCode==0?0:1;
static string ScriptPath([CallerFilePath]string p="")=>p;
