import { IndexData, ManifestData, MeshData, StepMeshRef, ProjectOverview } from '../types/contract';
import { MeshSelection, realUvDisplay, validateDisplaySelection, progressAssetUrl } from './realUvDisplay';

export interface LoadResult<T> {
  data: T | null;
  error: string | null;
  fromFixture: boolean;
}

export interface MeshLoadResult {
  data: MeshData | null;
  error: string | null;
}

// Reads a static data member. `encoding` is the explicit transport fact carried by
// the current manifest side; "gzip" members are decompressed to the exact producer
// bytes and anything else (including a missing decoder) is a hard failure so no
// blank or invented model is ever drawn.
async function readDataBytes(res: Response, encoding: 'gzip' | undefined, url: string): Promise<ArrayBuffer> {
  if (!encoding) return await res.arrayBuffer();
  if (encoding !== 'gzip') throw new Error(`不支持的传输编码: ${String(encoding)} (${url})`);
  if (typeof DecompressionStream === 'undefined' || !res.body) throw new Error(`浏览器缺少 gzip 解压支持，无法读取 ${url}`);
  const stream = res.body.pipeThrough(new DecompressionStream('gzip'));
  return await new Response(stream).arrayBuffer();
}

// In-memory mesh cache
const meshCache = new Map<string, MeshData>();

/**
 * Determine if fixture mode is forced by URL
 * Ignored completely in production builds.
 */
export function isFixtureModeForced(): boolean {
  if (!import.meta.env.DEV) return false;
  if (typeof window === 'undefined') return false;
  const params = new URLSearchParams(window.location.search);
  return params.get('fixture') === '1' || params.get('fixture') === 'true';
}

/**
 * Lazily load DEV fixtures only in dev mode
 */
async function getDevFixtures() {
  if (!import.meta.env.DEV) {
    throw new Error('DEV Fixtures are disabled in production build');
  }
  return await import('../fixtures/devFixtures');
}

/**
 * Fetches index.json without cache
 */
export async function fetchIndexData(
  useFixture = false,
  signal?: AbortSignal
): Promise<LoadResult<IndexData>> {
  if (useFixture) {
    if (!import.meta.env.DEV) {
      return {
        data: null,
        error: '生产环境禁止加载 DEV FIXTURE 演练数据',
        fromFixture: false,
      };
    }
    const fixtures = await getDevFixtures();
    return { data: fixtures.DEV_FIXTURE_INDEX, error: null, fromFixture: true };
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    const displayExample = new URLSearchParams(window.location.search).get('display') === 'real-uv';
    const url = progressAssetUrl(`${displayExample ? realUvDisplay.directory + '/' : ''}index.json?${cacheBuster}`);
    const res = await fetch(url, { signal });
    if (!res.ok) {
      return {
        data: null,
        error: `HTTP ${res.status} ${res.statusText}: 无法加载 data/index.json`,
        fromFixture: false,
      };
    }
    const json = (await res.json()) as IndexData;
    return { data: json, error: null, fromFixture: false };
  } catch (err) {
    if ((err as Error)?.name === 'AbortError') {
      return { data: null, error: 'Request aborted', fromFixture: false };
    }
    return {
      data: null,
      error: err instanceof Error ? err.message : String(err),
      fromFixture: false,
    };
  }
}

/**
 * Fetches manifest.json for a specific run
 */
export async function fetchManifestData(
  manifestRelPath: string,
  fromFixture = false,
  signal?: AbortSignal
): Promise<LoadResult<ManifestData>> {
  if (fromFixture) {
    if (!import.meta.env.DEV) {
      return {
        data: null,
        error: '生产环境禁止加载 DEV FIXTURE',
        fromFixture: false,
      };
    }
    const fixtures = await getDevFixtures();
    return { data: fixtures.DEV_FIXTURE_MANIFEST, error: null, fromFixture: true };
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    // manifestRelPath is relative to data/, e.g. "runs/<id>/manifest.json"
    const url = progressAssetUrl(`${manifestRelPath}?${cacheBuster}`);
    const res = await fetch(url, { signal });
    if (!res.ok) {
      return {
        data: null,
        error: `HTTP ${res.status}: 无法加载清单 ${url}`,
        fromFixture: false,
      };
    }
    const json = (await res.json()) as ManifestData;
    return { data: json, error: null, fromFixture: false };
  } catch (err) {
    if ((err as Error)?.name === 'AbortError') {
      return { data: null, error: 'Request aborted', fromFixture: false };
    }
    return {
      data: null,
      error: err instanceof Error ? err.message : String(err),
      fromFixture: false,
    };
  }
}

/**
 * Fetches single mesh JSON
 */
export async function fetchMeshData(
  manifestDir: string,
  meshRelPath: string | null,
  selection: MeshSelection,
  fromFixture = false,
  signal?: AbortSignal,
  encoding?: 'gzip'
): Promise<MeshLoadResult> {
  // Check independent selection before dereferencing a path or reusing any
  // verified attachment, including a previously warmed before snapshot.
  try { validateDisplaySelection(manifestDir, meshRelPath, selection); }
  catch (err) { return { data: null, error: (err as Error).message }; }
  if (!meshRelPath) return { data: null, error: null };
  // The transport encoding must agree with the referenced member: a .gz path
  // without the gzip marker, or a gzip marker without a .gz member, is a
  // contract violation and is reported instead of guessed.
  const isGz = meshRelPath.endsWith('.gz');
  if (isGz !== (encoding === 'gzip')) {
    return { data: null, error: `传输编码与成员不一致: ${meshRelPath} encoding=${String(encoding)}` };
  }

  const assetUrl = progressAssetUrl(`${manifestDir}/${meshRelPath}`);
  const fullKey = JSON.stringify([fromFixture, assetUrl, selection.runId, selection.caseId, selection.stepId, selection.kernel, selection.stage]);
  if (meshCache.has(fullKey)) {
    return { data: meshCache.get(fullKey)!, error: null };
  }

  if (fromFixture) {
    if (!import.meta.env.DEV) {
      return { data: null, error: '生产环境不支持演练网格' };
    }
    const fixtures = await getDevFixtures();
    const fixtureMesh = fixtures.DEV_FIXTURE_MESHES[meshRelPath];
    if (fixtureMesh) {
      meshCache.set(fullKey, fixtureMesh);
      return { data: fixtureMesh, error: null };
    }
    return { data: null, error: `未找到演练网格: ${meshRelPath}` };
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    const url = `${assetUrl}?${cacheBuster}`;
    const res = await fetch(url, { signal });
    if (!res.ok) {
      const errMsg = `HTTP ${res.status}: 网格文件未找到 (${url})`;
      console.warn(`[DataLoader] Failed to load mesh from ${url}: ${res.status}`);
      return { data: null, error: errMsg };
    }
    const bytes = await readDataBytes(res, encoding, url);
    const mesh = JSON.parse(new TextDecoder().decode(bytes)) as MeshData;
    await attachSurfaceParameters(mesh, bytes, manifestDir, meshRelPath, selection, signal);

    meshCache.set(fullKey, mesh);
    return { data: mesh, error: null };
  } catch (err) {
    if ((err as Error)?.name === 'AbortError') {
      return { data: null, error: 'Request aborted' };
    }
    const errMsg = err instanceof Error ? err.message : String(err);
    console.error(`[DataLoader] Error loading mesh ${meshRelPath}:`, err);
    return { data: null, error: errMsg };
  }
}

async function sha256(bytes: ArrayBuffer): Promise<string> {
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), (b) => b.toString(16).padStart(2, '0')).join('');
}

// Optional display companions are keyed by the complete run/snapshot identity and
// checked against exact bytes. They never alter a run's checks or qualification.
async function attachSurfaceParameters(mesh: MeshData, bytes: ArrayBuffer, manifestDir: string, snapshot: string, selection: MeshSelection, signal?: AbortSignal) {
  mesh.surfaceParameters = { status: 'unavailable', reason: '此快照未提供可核验的 XT 等参线派生数据' };
  if (manifestDir === 'display/real-uv') {
    await attachVerifiedDisplay(mesh, bytes, manifestDir, snapshot, selection, signal);
    return;
  }
  try {
    const response = await fetch('data/derivatives/isoparametrics/index.json', { signal });
    if (!response.ok) return;
    const index = await response.json();
    const run = manifestDir.replace(/^runs\//, '');
    const item = index.items?.find((entry: { sourceRun: string; sourceSnapshot: string }) => entry.sourceRun === run && entry.sourceSnapshot === snapshot);
    if (!item) return;
    if (typeof item.path !== 'string' || item.path.startsWith('/') || item.path.split('/').some((part: string) => part === '..' || part === '.')) throw new Error('Invalid derivative path');
    const isoResponse = await fetch(`data/derivatives/isoparametrics/${item.path}`, { signal });
    if (!isoResponse.ok) throw new Error('Derivative unavailable');
    const isoBytes = await isoResponse.arrayBuffer();
    if (await sha256(isoBytes) !== item.sha256) throw new Error('Derivative SHA mismatch');
    const iso = JSON.parse(new TextDecoder().decode(isoBytes));
    if (iso.schemaVersion !== 2 || iso.type !== 'render-only-isoparametrics-derivative' || iso.sourceRun !== run || iso.sourceSnapshot !== snapshot || iso.kernel !== mesh.source.kernel || iso.sourceMeshSha256 !== await sha256(bytes) || iso.inputXtSha256 !== mesh.source.snapshotXtSha256) throw new Error('Derivative snapshot identity mismatch');
    if (iso.status !== 'available') {
      mesh.surfaceParameters = { status: 'unsupported', reason: '此快照的曲面裁剪域尚不支持真实 U/V 导出' };
      return;
    }
    const pending = [];
    const seen = new Set<string>();
    for (const entry of iso.faces) {
      const face = mesh.faces.find((f) => f.id === entry.faceId && f.identifier === entry.identifier && f.entityTag === entry.meshEntityTag);
      if (!face || seen.has(face.id)) throw new Error('Derivative face binding mismatch');
      seen.add(face.id);
      const validLines = (lines: number[][]) => Array.isArray(lines) && lines.length > 0 && lines.every((line) => Array.isArray(line) && line.length >= 6 && line.length % 3 === 0 && line.every(Number.isFinite));
      if (!validLines(entry.uLines) || !validLines(entry.vLines)) throw new Error('Invalid U/V polylines');
      pending.push({ face, entry });
    }
    if (pending.length === 0) throw new Error('No verified faces');
    for (const { face, entry } of pending) face.isoparametrics = { uLines: entry.uLines, vLines: entry.vLines, source: entry.source, chordTolerance: iso.chordTolerance };
    mesh.surfaceParameters = { status: 'available', reason: `${pending.length} 个曲面：U=常数 u，V=常数 v；仅显示派生数据，未覆盖的面不生成线` };
  } catch (err) {
    if ((err as Error)?.name === 'AbortError') throw err;
    mesh.surfaceParameters = { status: 'unavailable', reason: '等参线来源校验失败；未加载派生线' };
  }
}

async function attachVerifiedDisplay(mesh: MeshData, bytes: ArrayBuffer, directory: string, snapshot: string, selection: MeshSelection, signal?: AbortSignal) {
  const expectedSnapshot = validateDisplaySelection(directory, snapshot, selection)!;
  const response = await fetch(progressAssetUrl(`${directory}/display-manifest.json`), { signal });
  if (!response.ok) throw new Error('真实 U/V 显示来源清单加载失败');
  const manifestBytes = await response.arrayBuffer();
  if (await sha256(manifestBytes) !== realUvDisplay.displayManifestSha256) throw new Error('真实 U/V unavailable：显示包与独立来源描述符不匹配');
  const manifest = JSON.parse(new TextDecoder().decode(manifestBytes));
  const matches = manifest.items?.filter((entry: { meshPath: string }) => entry.meshPath === snapshot);
  if (manifest.schemaVersion !== 1 || manifest.type !== 'verified-render-only-display-manifest' || matches?.length !== 1) throw new Error('显示快照来源不唯一');
  const item = matches[0];
  if (item.sourceRun !== realUvDisplay.sourceRun || item.caseId !== realUvDisplay.sourceCaseId || item.stepId !== realUvDisplay.sourceStepId || item.sampleId !== realUvDisplay.sampleId || item.producerCommit !== realUvDisplay.producerCommit || item.sourceSnapshot !== expectedSnapshot.sourceSnapshot || item.kernel !== selection.kernel || item.stage !== selection.stage) throw new Error('真实 U/V unavailable：来源快照不属于当前选择');
  const hashPattern = /^[a-f0-9]{64}$/;
  for (const key of ['sha256', 'sourceMeshSha256', 'inputXtSha256', 'snapshotReceiptSha256', 'executionSha256', 'captureCodeSha256', 'receiptCodeSha256']) {
    if (!hashPattern.test(item[key])) throw new Error('显示快照来源 SHA 无效');
  }
  if (item.sourceMeshSha256 !== await sha256(bytes) || item.kernel !== mesh.source.kernel || item.producerCommit !== mesh.source.commit || !['before', 'after'].includes(item.stage)) throw new Error('显示网格 SHA / 来源校验失败，拒绝绘制');
  const ids = new Set(mesh.faces.map((face) => face.id));
  const identifiers = new Set(mesh.faces.map((face) => face.identifier));
  if (ids.size !== mesh.faces.length || identifiers.size !== mesh.faces.length || mesh.faces.some((face) => !Number.isInteger(face.identifier) || face.identifier! <= 0)) throw new Error('显示网格 face identifier 不唯一');
  try {
    if (typeof item.path !== 'string' || !/^[a-z-]+\.iso\.json$/.test(item.path)) throw new Error('Invalid display derivative path');
    const isoResponse = await fetch(progressAssetUrl(`${directory}/${item.path}`), { signal });
    if (!isoResponse.ok) throw new Error('Derivative unavailable');
    const isoBytes = await isoResponse.arrayBuffer();
    if (await sha256(isoBytes) !== item.sha256) throw new Error('Derivative SHA mismatch');
    const iso = JSON.parse(new TextDecoder().decode(isoBytes));
    if (iso.schemaVersion !== 3 || iso.type !== 'render-only-isoparametrics-derivative') throw new Error('Invalid derivative schema');
    for (const key of ['sourceRun', 'sourceSnapshot', 'kernel', 'caseId', 'sampleId', 'stepId', 'stage', 'sourceMeshSha256', 'inputXtSha256', 'snapshotReceiptSha256', 'executionSha256', 'producerCommit', 'captureCodeSha256', 'receiptCodeSha256']) {
      if (iso[key] !== item[key]) throw new Error(`Derivative identity mismatch: ${key}`);
    }
    if (iso.sourceHashes?.['scripts/WebProgressCapture.cs'] !== item.captureCodeSha256 || iso.sourceHashes?.['scripts/WebProgressDisplayReceipts.cs'] !== item.receiptCodeSha256 || iso.dirtyAtStart !== false || iso.parameterHash !== null) throw new Error('Unverified producer sources');
    if (iso.status !== 'available') {
      mesh.surfaceParameters = { status: 'unsupported', reason: '此快照没有受支持的精确矩形非平面 trim' };
      return;
    }
    if (!Number.isFinite(iso.chordTolerance) || iso.chordTolerance <= 0 || !Array.isArray(iso.faces) || !iso.faces.length) throw new Error('Invalid display error/face evidence');
    const seen = new Set<string>();
    const pending = iso.faces.map((entry: any) => {
      const face = mesh.faces.find((f) => f.id === entry.faceId && f.identifier === entry.identifier && f.entityTag === entry.meshEntityTag);
      if (!face || seen.has(face.id) || entry.trimMethod !== 'PK_FACE_is_uvbox confirmed exact face rectangle') throw new Error('Derivative face/trim binding mismatch');
      seen.add(face.id);
      const valid = (lines: number[][]) => Array.isArray(lines) && lines.length > 0 && lines.every((line) => Array.isArray(line) && line.length >= 6 && line.length % 3 === 0 && line.every(Number.isFinite));
      if (!valid(entry.uLines) || !valid(entry.vLines) || entry.curves?.length !== entry.uLines.length + entry.vLines.length) throw new Error('Invalid U/V evidence');
      entry.curves.forEach((curve: any, index: number) => {
        const direction = index < entry.uLines.length ? 'u' : 'v';
        const line = direction === 'u' ? entry.uLines[index] : entry.vLines[index - entry.uLines.length];
        if (curve.direction !== direction || !Number.isFinite(curve.constantParameter) || !Number.isFinite(curve.sampledChordError) || curve.sampledChordError > iso.chordTolerance || curve.uvSamples?.length !== line.length / 3 * 2 || !curve.uvSamples.every(Number.isFinite) || curve.uvSamples.some((v: number, i: number) => i % 2 === (direction === 'u' ? 0 : 1) && v !== curve.constantParameter)) throw new Error('Invalid constant-parameter evidence');
      });
      return { face, entry };
    });
    for (const { face, entry } of pending) face.isoparametrics = { uLines: entry.uLines, vLines: entry.vLines, source: entry.source, chordTolerance: iso.chordTolerance };
    mesh.surfaceParameters = { status: 'available', reason: `${pending.length} 个非平面；仅精确矩形 trim；${iso.unsupportedFaces.length} 面未覆盖。采样弦差 ≤ ${iso.chordTolerance}，无解析全局界。` };
    mesh.displayProvenance = { sourceRun: item.sourceRun, snapshot: item.sourceSnapshot, stage: item.stage, meshSha256: item.sourceMeshSha256, xtSha256: item.inputXtSha256, receiptSha256: item.snapshotReceiptSha256, evidencePath: progressAssetUrl(`${directory}/${item.path}`) };
  } catch (err) {
    if ((err as Error)?.name === 'AbortError') throw err;
    mesh.surfaceParameters = { status: 'unavailable', reason: '等参线来源 / SHA / 面绑定校验失败；未加载派生线' };
  }
}

/**
 * Combine multiple meshes into one by merging faces and edges
 */
export function combineMeshes(primary: MeshData, additional: MeshData[]): MeshData {
  if (!additional || additional.length === 0) return primary;
  const faces = [...(primary.faces || [])];
  const edges = [...(primary.edges || [])];
  for (const m of additional) {
    if (m?.faces) {
      for (const f of m.faces) {
        faces.push({ ...f, isTool: true });
      }
    }
    if (m?.edges) {
      for (const e of m.edges) {
        edges.push({ ...e, isTool: true });
      }
    }
  }
  return {
    ...primary,
    faces,
    edges,
  };
}

/**
 * Fetches step meshes, including primary before/after mesh and any tools meshes
 */
export async function fetchStepMeshes(
  manifestDir: string,
  stepRef: StepMeshRef | undefined,
  selection: MeshSelection,
  fromFixture = false,
  signal?: AbortSignal
): Promise<MeshLoadResult> {
  const subState = selection.stage;
  const primaryPath = stepRef?.[subState] ?? null;
  try {
    const display = validateDisplaySelection(manifestDir, primaryPath, selection);
    if (display && stepRef?.tools?.length) throw new Error('真实 U/V unavailable：显示源未定义工具映射');
  } catch (err) { return { data: null, error: (err as Error).message }; }
  if (!stepRef) return { data: null, error: null };

  if (!primaryPath) return { data: null, error: null };

  const primaryRes = await fetchMeshData(manifestDir, primaryPath, selection, fromFixture, signal, stepRef.encoding);
  if (primaryRes.error || !primaryRes.data) {
    return primaryRes;
  }

  // If before state and tools exist, fetch tools and combine
  if (subState === 'before' && stepRef.tools && stepRef.tools.length > 0) {
    const toolResults = await Promise.all(
      stepRef.tools.map((tPath) => fetchMeshData(manifestDir, tPath, selection, fromFixture, signal, stepRef.encoding))
    );
    const validTools: MeshData[] = [];
    for (const tr of toolResults) {
      if (tr.data) validTools.push(tr.data);
    }
    if (validTools.length > 0) {
      return {
        data: combineMeshes(primaryRes.data, validTools),
        error: null,
      };
    }
  }

  return primaryRes;
}

/**
 * Fetch the project-wide overview (data/project-overview.json), current contract.
 */
export async function fetchOverviewData(signal?: AbortSignal): Promise<LoadResult<ProjectOverview>> {
  try {
    const url = progressAssetUrl(`project-overview.json?_t=${Date.now()}`);
    const res = await fetch(url, { signal });
    if (!res.ok) {
      return { data: null, error: `HTTP ${res.status} ${res.statusText}: 无法加载 data/project-overview.json`, fromFixture: false };
    }
    const data = (await res.json()) as ProjectOverview;
    return { data, error: null, fromFixture: false };
  } catch (e) {
    return { data: null, error: `无法加载 project-overview.json: ${e instanceof Error ? e.message : String(e)}`, fromFixture: false };
  }
}
