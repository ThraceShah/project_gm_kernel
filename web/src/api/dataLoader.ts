import { IndexData, ManifestData, MeshData } from '../types/contract';
import {
  DEV_FIXTURE_INDEX,
  DEV_FIXTURE_MANIFEST,
  DEV_FIXTURE_MESHES,
} from '../fixtures/devFixtures';

export interface LoadResult<T> {
  data: T | null;
  error: string | null;
  fromFixture: boolean;
}

// In-memory mesh cache
const meshCache = new Map<string, MeshData>();

/**
 * Determine if fixture mode is forced by URL or storage
 */
export function isFixtureModeForced(): boolean {
  if (typeof window === 'undefined') return false;
  const params = new URLSearchParams(window.location.search);
  return params.get('fixture') === '1' || params.get('fixture') === 'true';
}

/**
 * Fetches index.json without cache
 */
export async function fetchIndexData(useFixture = false): Promise<LoadResult<IndexData>> {
  if (useFixture) {
    return { data: DEV_FIXTURE_INDEX, error: null, fromFixture: true };
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    const url = `data/index.json?${cacheBuster}`;
    const res = await fetch(url);
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
  fromFixture = false
): Promise<LoadResult<ManifestData>> {
  if (fromFixture) {
    return { data: DEV_FIXTURE_MANIFEST, error: null, fromFixture: true };
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    // manifestRelPath is relative to data/, e.g. "runs/<id>/manifest.json"
    const url = `data/${manifestRelPath}?${cacheBuster}`;
    const res = await fetch(url);
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
    return {
      data: null,
      error: err instanceof Error ? err.message : String(err),
      fromFixture: false,
    };
  }
}

/**
 * Fetches mesh JSON for a case step
 * meshPath is relative to the directory containing manifest.json
 */
export async function fetchMeshData(
  manifestDir: string,
  meshRelPath: string | null,
  fromFixture = false
): Promise<MeshData | null> {
  if (!meshRelPath) return null;

  const fullKey = `${fromFixture ? 'fixture:' : manifestDir + '/'}${meshRelPath}`;
  if (meshCache.has(fullKey)) {
    return meshCache.get(fullKey)!;
  }

  if (fromFixture) {
    const fixtureMesh = DEV_FIXTURE_MESHES[meshRelPath];
    if (fixtureMesh) {
      meshCache.set(fullKey, fixtureMesh);
      return fixtureMesh;
    }
    return null;
  }

  try {
    const cacheBuster = `_t=${Date.now()}`;
    // manifestDir is e.g. "runs/run-123", so url is "data/runs/run-123/meshes/..."
    const url = `data/${manifestDir}/${meshRelPath}?${cacheBuster}`;
    const res = await fetch(url);
    if (!res.ok) {
      console.warn(`[DataLoader] Failed to load mesh from ${url}: ${res.status}`);
      return null;
    }
    const mesh = (await res.json()) as MeshData;
    meshCache.set(fullKey, mesh);
    return mesh;
  } catch (err) {
    console.error(`[DataLoader] Error loading mesh ${meshRelPath}:`, err);
    return null;
  }
}
