// Reviewed package descriptor. Selection expectations must not come from the
// mutable case manifest, display-manifest item, or the asset being checked.
// This namespace is owned by the frozen UI, never by producer data/ refreshes.
export const realUvDisplay = {
  runId: 'real-uv-display',
  directory: 'display/real-uv',
  displayManifestSha256: '0875f9b8208db2d248e3a4fc912b0f5a077d75deaba8b3d1553b840dfb732714',
  caseId: 'solid-blend.plane-nurbs',
  stepId: 'operation-1',
  sourceRun: 'r2-curved-receipt-eecab6d',
  sourceCaseId: 'solid-blend.plane-nurbs',
  sourceStepId: 'operation-1',
  sampleId: 'sample-0',
  producerCommit: 'eecab6d51cae79a687fa459af512adc3e03895f5',
  snapshots: {
    GM: {
      before: { meshPath: 'meshes/gm-before.mesh.json', sourceSnapshot: 'snapshot-2' },
      after: { meshPath: 'meshes/gm-after.mesh.json', sourceSnapshot: 'snapshot-3' },
    },
    PK: {
      before: { meshPath: 'meshes/pk-before.mesh.json', sourceSnapshot: 'snapshot-0' },
      after: { meshPath: 'meshes/pk-after.mesh.json', sourceSnapshot: 'snapshot-1' },
    },
  },
} as const;

export function progressAssetUrl(relativePath: string): string {
  if (relativePath.startsWith('/') || relativePath.includes('\\') || relativePath.split('/').some((part) => part === '..' || part === '.')) throw new Error('Progress asset path must stay relative to the app base');
  const path = relativePath.startsWith(realUvDisplay.directory + '/') ? relativePath : 'data/' + relativePath;
  return new URL(path, new URL(import.meta.env.BASE_URL, document.baseURI)).href;
}

export interface MeshSelection {
  runId: string;
  caseId: string;
  stepId: string;
  kernel: 'GM' | 'PK';
  stage: 'before' | 'after';
}

export function validateDisplaySelection(directory: string, meshPath: string | null, selection: MeshSelection) {
  if (directory !== realUvDisplay.directory && selection.runId !== realUvDisplay.runId && !directory.startsWith('display/')) return;
  if (directory !== realUvDisplay.directory || selection.runId !== realUvDisplay.runId || selection.caseId !== realUvDisplay.caseId || selection.stepId !== realUvDisplay.stepId || !['GM', 'PK'].includes(selection.kernel) || !['before', 'after'].includes(selection.stage)) {
    throw new Error('真实 U/V unavailable：当前 run/case/step/kernel/stage 与独立显示源不匹配');
  }
  const snapshot = realUvDisplay.snapshots[selection.kernel][selection.stage];
  if (meshPath !== snapshot.meshPath) throw new Error('真实 U/V unavailable：当前选择的快照路径与独立阶段映射不匹配');
  return snapshot;
}
