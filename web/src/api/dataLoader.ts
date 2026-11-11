import { IndexData, ManifestData, MeshData, StepMeshRef } from '../types/contract';

export interface LoadResult<T> {
  data: T | null;
  error: string | null;
  fromFixture: boolean;
}

export interface MeshLoadResult {
  data: MeshData | null;
  error: string | null;
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
    const url = `data/index.json?${cacheBuster}`;
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
    const url = `data/${manifestRelPath}?${cacheBuster}`;
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
  fromFixture = false,
  signal?: AbortSignal
): Promise<MeshLoadResult> {
  if (!meshRelPath) return { data: null, error: null };

  const fullKey = `${fromFixture ? 'fixture:' : manifestDir + '/'}${meshRelPath}`;
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
    const url = `data/${manifestDir}/${meshRelPath}?${cacheBuster}`;
    const res = await fetch(url, { signal });
    if (!res.ok) {
      const errMsg = `HTTP ${res.status}: 网格文件未找到 (${url})`;
      console.warn(`[DataLoader] Failed to load mesh from ${url}: ${res.status}`);
      return { data: null, error: errMsg };
    }
    const bytes = await res.arrayBuffer();
    const mesh = JSON.parse(new TextDecoder().decode(bytes)) as MeshData;
    await attachSurfaceParameters(mesh, bytes, manifestDir, meshRelPath, signal);

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
async function attachSurfaceParameters(mesh: MeshData, bytes: ArrayBuffer, manifestDir: string, snapshot: string, signal?: AbortSignal) {
  mesh.surfaceParameters = { status: 'unavailable', reason: '此快照未提供可核验的 XT 等参线派生数据' };
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
  subState: 'before' | 'after',
  fromFixture = false,
  signal?: AbortSignal
): Promise<MeshLoadResult> {
  if (!stepRef) return { data: null, error: null };

  const primaryPath = subState === 'before' ? stepRef.before : stepRef.after;
  if (!primaryPath) return { data: null, error: null };

  const primaryRes = await fetchMeshData(manifestDir, primaryPath, fromFixture, signal);
  if (primaryRes.error || !primaryRes.data) {
    return primaryRes;
  }

  // If before state and tools exist, fetch tools and combine
  if (subState === 'before' && stepRef.tools && stepRef.tools.length > 0) {
    const toolResults = await Promise.all(
      stepRef.tools.map((tPath) => fetchMeshData(manifestDir, tPath, fromFixture, signal))
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
