/**
 * Shared Data Contract for GM Kernel vs Parasolid Benchmark Workbench
 * Strictly complies with the project contract definition.
 */

export interface IndexData {
  schemaVersion: number;
  generatedAt: string;
  latestRunId: string;
  runs: RunSummary[];
}

export interface RunSummary {
  id: string;
  commit: string;
  generatedAt: string;
  label: string;
  manifest: string; // relative to data/ directory, e.g. "runs/<id>/manifest.json"
}

export interface RunMetadata {
  id: string;
  commit: string;
  generatedAt: string;
  pkVersion?: string;
  host?: string | Record<string, unknown>;
  configuration?: string | Record<string, unknown>;
  scope?: string | Record<string, unknown>;
  provenance?: string | Record<string, unknown>;
  [key: string]: unknown;
}

export type CaseFamily = 'blend' | 'boolean';

export type CaseStatus =
  | 'pass'
  | 'fail'
  | 'missing'
  | 'unsupported'
  | 'timeout'
  | 'diagnostic';

export interface ApiCallInfo {
  name: string;
  parameters: Record<string, unknown>;
}

export interface StepMeshRef {
  before: string | null; // relative to manifest dir, e.g. "meshes/c1_gm_before.json"
  after: string | null;  // relative to manifest dir, e.g. "meshes/c1_gm_after.json"
  tools?: string[];
  selectedEdgeTags?: number[];
  tracking?: unknown[];
  afterBodies?: string[];
  blendTracking?: unknown[];
}

export interface StepData {
  id: string;
  title: string;
  gm: StepMeshRef;
  pk: StepMeshRef;
}

export interface CheckItem {
  id: string;
  label: string;
  status: 'pass' | 'fail' | 'missing' | string;
  gm: number | string | null;
  pk: number | string | null;
  absDelta: number | null;
  normalizedDelta: number | null;
  threshold: number | null;
  unit: string;
  evidence: string | null;
  note: string;
}

export interface PerformanceInfo {
  gmMs: number | null;
  pkMs: number | null;
  ratioGmOverPk: number | null; // smaller means GM is faster
  samples?: number | string | null;
  warmup?: number | string | null;
  scope?: string | Record<string, unknown> | null;
  correctnessQualified?: boolean | string | null;
  host?: string | Record<string, unknown> | null;
  configuration?: string | Record<string, unknown> | null;
  note?: string | null;
  measurementKind?: string | null;
  stableBenchmark?: boolean | string | null;
  qualifiedScope?: string | null;
  [key: string]: unknown;
}

export interface EvidenceItem {
  label: string;
  path: string; // relative to manifest dir
}

export interface CaseSource {
  path: string;
  revision: string;
}

export interface ExecutionInfo {
  selected?: boolean;
  attempted?: boolean;
  exitCode?: number | null;
  exitCodes?: number[];
  timedOut?: boolean;
}

export interface TestCase {
  id: string;
  title: string;
  family: CaseFamily;
  operation: string;
  status: CaseStatus;
  parameters: Record<string, unknown>;
  apis: {
    gm: ApiCallInfo;
    pk: ApiCallInfo;
  };
  steps: StepData[];
  checks: CheckItem[];
  performance: PerformanceInfo;
  evidence: EvidenceItem[];
  notes: string[];
  source: CaseSource;
  execution?: ExecutionInfo;
  suite?: string;
  capabilityScope?: string | null;
  adapterStatus?: string | null;
  kernelCapability?: string | null;
  sessionPrecision?: string | number | null;
}

export interface ManifestData {
  schemaVersion: number;
  run: RunMetadata;
  cases: TestCase[];
}

export type EntityRole =
  | 'unchanged'
  | 'selected'
  | 'support'
  | 'newBlend'
  | 'modified'
  | 'deleted';

export interface IsoparametricData {
  uLines?: number[][]; // Array of flat xyz polylines: [x1, y1, z1, x2, y2, z2, ...]
  vLines?: number[][];
  unclassifiedLines?: number[][];
  source?: string;     // Provenance of isoparametric evaluation (e.g. Parasolid PK_TOPOL_render_line)
  chordTolerance?: number;
}

export interface FaceData {
  id: string;
  positions: number[]; // Flat xyz: [x1, y1, z1, x2, y2, z2, ...]
  normals?: number[];  // Optional flat xyz normals
  indices: number[];    // Face-local 0-based triangle vertex indices
  role: EntityRole;
  identifier?: number;
  entityTag?: number;
  isoparametrics?: IsoparametricData;
  isTool?: boolean;
}

export interface EdgeData {
  id: string;
  positions: number[]; // Sequential line segments: [x1, y1, z1, x2, y2, z2, ...]
  role: EntityRole;
  identifier?: number;
  entityTag?: number;
  isTool?: boolean;
}

export interface MeshSource {
  kernel: string;
  geometryAuthor: string;
  tessellator: string;
  commit: string;
  chordTolerance: number;
  [key: string]: unknown;
}

export interface MeshIsoparametricLine {
  faceId: string;
  direction?: 'u' | 'v';
  positions: number[]; // Flat xyz polyline
}

export interface MeshData {
  schemaVersion: number;
  bodyId: string;
  source: MeshSource;
  faces: FaceData[];
  edges: EdgeData[];
  isoparametrics?: MeshIsoparametricLine[];
  surfaceParameters?: { status: 'available' | 'unavailable' | 'unsupported'; reason: string };
  displayProvenance?: { sourceRun: string; snapshot: string; stage: string; meshSha256: string; xtSha256: string; receiptSha256: string; evidencePath: string };
}

export type StandardView = 'iso' | 'front' | 'top' | 'right';


// ---- Project overview (current contract; produced by scripts/BuildProjectOverview.cs)
export interface OverviewStatusCell {
  status: 'pass' | 'fail' | 'unresolved' | 'unverified' | 'na';
  lastVerified: { commit: string; tree: string; host: string; exit: string; evidence: string };
  note: string;
}
export interface OverviewCatalogItem {
  id: string;
  title: string;
  suite: string;
  family: string;
  operation: string;
  parametersDefinition: Record<string, unknown>;
  source: string;
  selector: string;
  dimensions: {
    function: OverviewStatusCell;
    mass: OverviewStatusCell;
    query: OverviewStatusCell;
    performance: OverviewStatusCell;
    currentVersion?: string;
  };
  history?: { label: string; status: string; note: string }[];
}
export interface OverviewNamedItem {
  id: string; title: string; status: string; note: string; evidence: string; inModelingDenominator: boolean;
}
export interface OverviewDelivery { runId: string; label: string; commit: string; utc: string; note: string; }
export interface OverviewSourceItem { name: string; identity: string; where: string; note: string; }
export interface ProjectOverview {
  schemaVersion: number;
  generatedAt: string;
  catalogVersion: string;
  currentVersion: Record<string, string>;
  catalog: OverviewCatalogItem[];
  boundaryRegisters: OverviewNamedItem[];
  verificationRecords: OverviewNamedItem[];
  deliveries: OverviewDelivery[];
  sourceIndex: OverviewSourceItem[];
  summary: Record<string, number | string>;
}
