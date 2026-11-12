#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Independent browser verification of the project-overview candidate against a
// locally assembled preview (own built assets + read-only publisher run data,
// plus an isolated derived event-modified copy and a missing-data variant).
// Verifies: default overview, counters vs rows, catalog/status/evidence rows,
// cross-suite evidence visibility, search/filter, precise model jump incl.
// step landing, back/refresh, event row-vs-counter coherence in real pixels,
// 390px mobile, and failure loading. Writes a derived receipt JSON and
// screenshots under temp_docs/independent-overview-audit/browser/.
var here=Path.GetDirectoryName(ScriptPath())!;var root=Path.GetFullPath("..",here);
var qaOut=Path.Combine(root,"temp_docs","independent-overview-audit","browser");Directory.CreateDirectory(qaOut);
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
const context=await browser.newContext({viewport:{width:1600,height:1000}});const page=await context.newPage();
page.on('pageerror',e=>errors.push(e.message));
try{
await page.goto(base,{waitUntil:'networkidle'});
await page.getByTestId('overview-panel').waitFor();
const top=await page.locator('body').textContent();
check('default view is project overview with counters',top.includes('项目整体进度')&&top.includes('累计证据·功能通过 188')&&top.includes('性能观测 5')&&top.includes('当前版本已复验 5 op'),{});
const rowCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('catalog rows rendered and filterable',rowCount>400,{rowCount});
// row detail + evidence text
await page.locator('[data-testid="catalog-row-local-blend.cylinder-rim-fillet"]').click();
const d1=await page.getByTestId('catalog-detail-local-blend.cylinder-rim-fillet').textContent();
check('query-gap row keeps fail + real evidence text',d1.includes('5000')&&d1.includes('BodyContainsVector')&&d1.includes('axial-material-query.log'),{});
check('evidence is plain local-path text (not a clickable link)',await page.getByTestId('catalog-detail-local-blend.cylinder-rim-fillet').locator('a').count()===0,{});
// cross-suite evidence mismatch visible in one row
await page.locator('[data-testid="catalog-row-planar-boolean.disjoint-unite"]').click();
const d2=await page.getByTestId('catalog-detail-planar-boolean.disjoint-unite').textContent();
check('cross-suite row shows own source but borrowed log evidence',d2.includes('BooleanOperationOracle.cs')&&d2.includes('regular45-final.log'),{});
await page.screenshot({path:path.join(out,'overview-cross-suite-row.png')});
// search + filter
await page.getByTestId('overview-search').fill('cylinder');
await page.waitForTimeout(200);
const searchCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('search narrows the matrix',searchCount>0&&searchCount<40,{searchCount});
await page.getByTestId('overview-search').fill('');
await page.getByTestId('overview-family').selectOption('boolean');
await page.waitForTimeout(200);
const boolCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('family filter works',boolCount>0&&boolCount<200,{boolCount});
await page.getByTestId('overview-family').selectOption('all');
await page.getByTestId('overview-issues').check();
await page.waitForTimeout(200);
const issueCount=await page.locator('[data-testid^="catalog-row-"]').count();
check('issues filter works',issueCount>0&&issueCount<300,{issueCount});
await page.getByTestId('overview-issues').uncheck();
await page.screenshot({path:path.join(out,'overview-default.png')});
// precise model jump: cylinder-unequal-chamfer must land on its own operation step
await page.locator('[data-testid="catalog-row-local-blend.cylinder-unequal-chamfer"]').click();
const d3=await page.getByTestId('catalog-detail-local-blend.cylinder-unequal-chamfer').textContent();
check('cylinder rows carry step disclaimer text',d3.includes('步骤'),{});
await page.getByTestId('catalog-model-local-blend.cylinder-unequal-chamfer').click();
await page.waitForFunction(()=>{const es=[...document.querySelectorAll('[data-testid="viewer-canvas-host"]')];return es.length===2&&es.every(e=>Number(e.dataset.triangleCount)>0);},{timeout:20000});
const body2=await page.locator('body').textContent();
check('model jump opens the exact run/case (blend.axial-cylinder)',body2.includes('axial'),{});
const stepText=(await page.locator('body').textContent()).slice(0,4000);
const landsRim=stepText.includes('cylinder-rim-fillet');
check('SPECIFIC STEP: unequal-chamfer link lands on its own step (expect FAIL: no step id in display contract)',stepText.includes('cylinder-unequal-chamfer')&&!landsRim,{landsOnFirstStep:landsRim});
await page.screenshot({path:path.join(out,'model-jump-cylinder.png')});
// back to overview keeps overview usable
await page.getByTestId('back-to-overview').click();
await page.getByTestId('overview-panel').waitFor();
check('back returns to overview',true,{});
await page.reload({waitUntil:'networkidle'});
await page.getByTestId('overview-panel').waitFor();
check('refresh lands on overview again',true,{});
// derived event copy: row flips but top counter must move together
const ctx2=await browser.newContext({viewport:{width:1600,height:1000}});const page2=await ctx2.newPage();
page2.on('pageerror',e=>errors.push('event-page: '+e.message));
await page2.goto(baseEvent,{waitUntil:'networkidle'});
await page2.getByTestId('overview-panel').waitFor();
const evtData=await page2.evaluate(()=>fetch('data/project-overview.json?_t='+Date.now()).then(r=>r.json()));
const passRows=evtData.catalog.filter(c=>c.dimensions.function.status==='pass').length;
const topText=await page2.locator('body').textContent();
const counterMatch=topText.match(/累计证据·功能通过 (\d+)/);
check('EVENT UI: top counter equals recomputed pass rows',counterMatch&&Number(counterMatch[1])===passRows,{counter:counterMatch&&counterMatch[1],recomputedPassRows:passRows});
await page2.screenshot({path:path.join(out,'event-summary-mismatch.png')});
await ctx2.close();
// 390px mobile
const ctx3=await browser.newContext({viewport:{width:390,height:844}});const page3=await ctx3.newPage();
page3.on('pageerror',e=>errors.push('mobile: '+e.message));
await page3.goto(base,{waitUntil:'networkidle'});
await page3.getByTestId('overview-panel').waitFor();
const mobRows=await page3.locator('[data-testid^="catalog-row-"]').count();
check('390px mobile renders overview rows',mobRows>400,{mobRows});
await page3.screenshot({path:path.join(out,'overview-mobile-390.png')});
await ctx3.close();
// failure loading
const ctx4=await browser.newContext({viewport:{width:1200,height:800}});const page4=await ctx4.newPage();
page4.on('pageerror',e=>errors.push('broken: '+e.message));
await page4.goto(baseBroken,{waitUntil:'networkidle'});
const errVisible=await page4.getByTestId('overview-load-error').count();
check('missing overview data shows explicit failure screen (no fake data)',errVisible===1,{});
await page4.screenshot({path:path.join(out,'overview-load-failure.png')});
await ctx4.close();
check('no uncaught JS errors',errors.length===0,{errors});
}catch(e){await page.screenshot({path:path.join(out,'browser-failure.png')}).catch(()=>{});rows.push({name:'EXCEPTION',ok:false,detail:String(e)});console.error(e);}
finally{fs.writeFileSync(path.join(out,'overview-browser-audit.json'),JSON.stringify({checkedUtc:new Date().toISOString(),rows,errors},null,2));await browser.close();}
console.log('DONE checks='+rows.length+' failed='+rows.filter(r=>!r.ok).length);
})().catch(e=>{console.error(e);process.exitCode=1;});
""");p.StandardInput.Close();await p.WaitForExitAsync();
Console.WriteLine("browser audit exit="+p.ExitCode);
return p.ExitCode==0?0:1;
static string ScriptPath([CallerFilePath]string p="")=>p;
