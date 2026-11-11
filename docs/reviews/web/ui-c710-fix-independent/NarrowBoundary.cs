#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

var scriptDir = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", ".."));
var original = File.ReadAllText(Path.Combine(scriptDir, "..", "ui-c710", "Review.cs"));
if (Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original))) != "8f05cb5fbdbb06d10e5894c52527be8c384eac2495305535eda6cb42cc63dace")
    throw new InvalidOperationException("Original probe changed");
var jsStart = original.IndexOf("start.ArgumentList.Add(\"\"\"", StringComparison.Ordinal) + "start.ArgumentList.Add(\"\"\"".Length;
var js = original[jsStart..];
var prelude = js[..js.IndexOf("\ntry{", StringComparison.Ordinal)];
var start = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "temp_docs", "ui-53f9871-review", "web") };
start.ArgumentList.Add("--input-type=module");
start.ArgumentList.Add("--eval");
start.ArgumentList.Add(prelude + "\n" + """
const THREE=await import('three');
const audit=JSON.parse(fs.readFileSync(process.argv[1]));
const item=manifest.cases.find(c=>c.id==='support-chain.asymmetric43-base');
const rendered=[];
try{
 await page.goto(`http://127.0.0.1:${server.address().port}/`);await wait();await select(item.id);
 check('same immutable screenshot data input',hash==='117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990',hash);
 await page.getByTestId('comparison-summary').click();
 await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.forEach(e=>e.open=true));
 const groups=await page.getByTestId('grouped-checks').locator('details').evaluateAll(es=>es.map(e=>({summary:e.querySelector('summary').textContent,rows:[...e.querySelectorAll('tbody tr')].map(r=>({id:r.cells[1].querySelector('.font-mono-code').textContent,status:r.cells[0].textContent.trim(),label:r.cells[1].querySelector('div').textContent,cells:[...r.cells].slice(2,8).map(c=>c.textContent.trim()),evidence:r.cells[8].querySelector('a')?.getAttribute('href')??null,note:r.cells[8].querySelector('.leading-tight')?.textContent??''}))})));
 const equalIds=(a,b)=>JSON.stringify([...a].sort())===JSON.stringify([...b].sort());
 const mapping=audit.coverageGroups.map(g=>({auditId:g.id,ids:g.checks.map(c=>c.id),actual:groups.find(actual=>equalIds(actual.rows.map(r=>r.id),g.checks.map(c=>c.id)))}));
 check('six groups match every c9b88 member ID with no loss or duplication',groups.length===6&&groups.flatMap(g=>g.rows).length===29&&new Set(groups.flatMap(g=>g.rows.map(r=>r.id))).size===29&&mapping.every(m=>m.actual),mapping);
 const fmt=v=>v==null?'null':typeof v==='number'?(Number.isInteger(v)?String(v):String(Number(v.toPrecision(10)))):String(v);
 const mismatches=groups.flatMap(g=>g.rows).flatMap(r=>{const c=item.checks.find(c=>c.id===r.id);const cells=[fmt(c.gm),fmt(c.pk),fmt(c.absDelta),fmt(c.normalizedDelta),c.threshold==null?'-':`≤ ${c.threshold}`,c.unit||'-'];return r.status===c.status.toUpperCase()&&r.label===c.label&&JSON.stringify(r.cells)===JSON.stringify(cells)&&r.evidence===(c.evidence?`data/${path.dirname(run.manifest)}/${c.evidence}`:null)&&r.note===(c.note||'')?[]:[{id:r.id,rendered:r,expected:c,expectedCells:cells}];});
 check('all 29 original statuses, values, deltas, thresholds, labels and evidence unchanged',mismatches.length===0,mismatches);
 rendered.push({groups});await page.keyboard.press('Escape');
 await page.selectOption('[data-testid="select-standard-view"]','iso');await page.waitForTimeout(300);
 const points=[
  {side:'gm',face:'gm:face:48',facePoint:[698.3171169063603,416.7915803555108],edge:'gm:edge:102',edgePoint:[831.6637177210731,564.0234085212992],front:'gm:face:66',rear:'gm:edge:104',hiddenPoint:[721.7037889113542,781.4258385670447]},
  {side:'pk',face:'pk:face:180',facePoint:[1521.3171169063603,416.7915803555108],edge:'pk:edge:181',edgePoint:[1654.663717721073,564.0234085212992],front:'pk:face:111',rear:'pk:edge:166',hiddenPoint:[1544.7037889113542,781.4258385670447]}
 ];
 const inspect=async p=>{await page.mouse.click(...p);await page.waitForTimeout(60);const text=await page.getByTestId('entity-inspector').textContent().catch(()=>null);if(await page.getByLabel('清除实体选择').count())await page.getByLabel('清除实体选择').click();return text;};
 for(const [i,p] of points.entries()){
  const mesh=JSON.parse(fs.readFileSync(path.join(runRoot,item.steps[0][p.side].before))),rect=await hosts().nth(i).locator('canvas').boundingBox(),state=(await stats())[i],size=await hosts().nth(i).locator('canvas').evaluate(c=>[c.width,c.height]);
  const camera=new THREE.PerspectiveCamera(45,size[0]/size[1],.001,500);camera.position.fromArray(state.pose.split(',').map(Number));camera.lookAt(new THREE.Vector3().fromArray(state.target.split(',').map(Number)));camera.updateMatrixWorld();
  const hits=point=>{const ray=new THREE.Raycaster();ray.setFromCamera(new THREE.Vector2((point[0]-rect.x)/rect.width*2-1,1-(point[1]-rect.y)/rect.height*2),camera);const result=[];for(const f of mesh.faces)for(let k=0;k<f.indices.length;k+=3){const vs=f.indices.slice(k,k+3).map(j=>new THREE.Vector3().fromArray(f.positions,j*3)),hit=ray.ray.intersectTriangle(...vs,false,new THREE.Vector3());if(hit)result.push({id:f.id,role:f.role,distance:hit.distanceTo(camera.position)});}return result.sort((a,b)=>a.distance-b.distance);};
  const foreground=hits(p.facePoint),transparent=hits(p.edgePoint),opaque=hits(p.hiddenPoint).find(h=>h.role!=='unchanged');
  const f=await inspect(p.facePoint),e=await inspect(p.edgePoint),h=await inspect(p.hiddenPoint);
  check(`${p.side}: ordinary face and ordinary rear edge through transparent context remain selectable`,foreground[0]?.id===p.face&&transparent[0]?.role==='unchanged'&&f?.includes(`[${p.face}]`)&&e?.includes(`[${p.edge}]`),{facePoint:p.facePoint,faceHit:f,edgePoint:p.edgePoint,edgeHit:e,foreground,transparent});
  check(`${p.side}: actual opaque foreground wins over rear edge`,opaque?.id===p.front&&h?.includes(`[${p.front}]`)&&!h?.includes(`[${p.rear}]`),{point:p.hiddenPoint,picked:h,expectedFront:p.front,rear:p.rear,hits:hits(p.hiddenPoint)});
 }
 check('no boundary page errors',errors.length===0,errors);
}finally{await browser.close();server.close();fs.writeFileSync(path.join(out,'narrow-boundary-report.json'),JSON.stringify({actualCandidate:'53f9871e55574ec66a96f12904293515ce40c23c',webSrcTree:'07ded43c9321e7e76a8177865c2852cc4fbd1db3',indexHash:hash,run:manifest.run.id,rows,rendered,errors},null,2));}
if(rows.some(r=>!r.ok))process.exitCode=1;
""");
start.ArgumentList.Add(Path.Combine(root, "docs", "reviews", "advanced-modeling", "web-primary-evidence-audit-610e3ce.json"));
using var process = Process.Start(start)!;
process.WaitForExit();
return process.ExitCode;
static string ScriptPath([CallerFilePath] string path = "") => path;
