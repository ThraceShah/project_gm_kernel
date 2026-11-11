import * as THREE from 'three';
import { MeshData, StandardView } from '../types/contract';

export interface CameraPose {
  position: THREE.Vector3;
  target: THREE.Vector3;
  distance: number;
  near: number;
  far: number;
}

export interface OperationFocusInfo {
  hasOperationFeatures: boolean;
  focusBox: THREE.Box3;
  fullBox: THREE.Box3;
  focusNormal: THREE.Vector3;
  focusCenter: THREE.Vector3;
  fullCenter: THREE.Vector3;
}

/**
 * Derives focus and bounding information from real selected topology, modified result,
 * tool entities, and affected bounds with face normals.
 */
export function extractOperationFocus(
  meshData: MeshData | null,
  toolMeshes?: MeshData[] | null
): OperationFocusInfo {
  const fullBox = new THREE.Box3();
  const focusBox = new THREE.Box3();
  const focusNormal = new THREE.Vector3(0, 0, 0);
  let focusNormalWeight = 0;
  let hasFeatures = false;

  const processMesh = (mesh: MeshData, isTool = false) => {
    // 1. Process Faces
    if (Array.isArray(mesh.faces)) {
      mesh.faces.forEach((face) => {
        const isFocusFace =
          isTool ||
          face.role === 'newBlend' ||
          face.role === 'modified' ||
          face.role === 'selected' ||
          face.role === 'support' ||
          face.role === 'deleted';

        const pos = face.positions;
        if (!pos || pos.length < 9) return;

        // Bounding box calculation
        for (let i = 0; i < pos.length; i += 3) {
          const pt = new THREE.Vector3(pos[i], pos[i + 1], pos[i + 2]);
          fullBox.expandByPoint(pt);
          if (isFocusFace) {
            focusBox.expandByPoint(pt);
            hasFeatures = true;
          }
        }

        // Weighted normal calculation for focus faces
        if (isFocusFace && face.indices && face.indices.length >= 3) {
          for (let i = 0; i < face.indices.length; i += 3) {
            const iA = face.indices[i] * 3;
            const iB = face.indices[i + 1] * 3;
            const iC = face.indices[i + 2] * 3;

            if (iC + 2 < pos.length) {
              const pA = new THREE.Vector3(pos[iA], pos[iA + 1], pos[iA + 2]);
              const pB = new THREE.Vector3(pos[iB], pos[iB + 1], pos[iB + 2]);
              const pC = new THREE.Vector3(pos[iC], pos[iC + 1], pos[iC + 2]);

              const triNormal = new THREE.Vector3()
                .crossVectors(
                  new THREE.Vector3().subVectors(pB, pA),
                  new THREE.Vector3().subVectors(pC, pA)
                );
              const triArea = triNormal.length() * 0.5;
              if (triArea > 1e-8) {
                triNormal.normalize();
                focusNormal.addScaledVector(triNormal, triArea);
                focusNormalWeight += triArea;
              }
            }
          }
        }
      });
    }

    // 2. Process Edges (especially selected edges)
    if (Array.isArray(mesh.edges)) {
      mesh.edges.forEach((edge) => {
        const pos = edge.positions;
        if (!pos || pos.length < 6) return;

        const isFocusEdge =
          edge.role === 'selected' ||
          edge.role === 'newBlend' ||
          edge.role === 'modified' ||
          edge.role === 'support' ||
          edge.role === 'deleted';

        for (let i = 0; i < pos.length; i += 3) {
          const pt = new THREE.Vector3(pos[i], pos[i + 1], pos[i + 2]);
          fullBox.expandByPoint(pt);
          if (isFocusEdge) {
            focusBox.expandByPoint(pt);
            hasFeatures = true;
          }
        }
      });
    }
  };

  if (meshData) {
    processMesh(meshData, false);
  }
  if (Array.isArray(toolMeshes)) {
    toolMeshes.forEach((tool) => processMesh(tool, true));
  }

  // Normalize focus normal
  if (focusNormalWeight > 1e-8) {
    focusNormal.normalize();
  } else {
    focusNormal.set(0, 0, 0);
  }

  // If no features found or focus box empty, fall back to fullBox
  if (!hasFeatures || focusBox.isEmpty()) {
    focusBox.copy(fullBox);
  }

  const focusCenter = focusBox.isEmpty()
    ? new THREE.Vector3(0, 0, 0)
    : focusBox.getCenter(new THREE.Vector3());
  const fullCenter = fullBox.isEmpty()
    ? new THREE.Vector3(0, 0, 0)
    : fullBox.getCenter(new THREE.Vector3());

  return {
    hasOperationFeatures: hasFeatures && !focusBox.isEmpty(),
    focusBox,
    fullBox,
    focusNormal,
    focusCenter,
    fullCenter,
  };
}

/**
 * Candidate directions for isometric inspection.
 * Deterministic scoring considers feature normal alignment, overhead elevation,
 * projected visibility and avoidance of grazing angles.
 */
export function determineOptimalViewDirection(
  focusInfo: OperationFocusInfo
): THREE.Vector3 {
  const { hasOperationFeatures, focusNormal, focusCenter, fullCenter } = focusInfo;

  // Candidates: 8 octants with CAD overhead elevation
  const candidates: THREE.Vector3[] = [
    new THREE.Vector3(1, 1, 1).normalize(),
    new THREE.Vector3(-1, 1, 1).normalize(),
    new THREE.Vector3(1, 1, -1).normalize(),
    new THREE.Vector3(-1, 1, -1).normalize(),
    new THREE.Vector3(1, 0.75, 1.25).normalize(),
    new THREE.Vector3(-1, 0.75, 1.25).normalize(),
    new THREE.Vector3(1, 0.75, -1.25).normalize(),
    new THREE.Vector3(-1, 0.75, -1.25).normalize(),
    new THREE.Vector3(1.25, 1, 0.5).normalize(),
    new THREE.Vector3(-1.25, 1, 0.5).normalize(),
    new THREE.Vector3(0.5, 1, 1.25).normalize(),
    new THREE.Vector3(-0.5, 1, 1.25).normalize(),
  ];

  // If no specific features, return standard CAD isometric
  if (!hasOperationFeatures) {
    return new THREE.Vector3(1, 1, 1.2).normalize();
  }

  // Target direction from full center to focus center (helps find outward-facing side)
  const outwardDir = new THREE.Vector3().subVectors(focusCenter, fullCenter);
  const outwardDist = outwardDir.length();
  if (outwardDist > 1e-4) {
    outwardDir.normalize();
  } else {
    outwardDir.set(0, 0, 0);
  }

  let bestDir = candidates[0];
  let bestScore = -Infinity;

  for (const dir of candidates) {
    let score = 0;

    // 1. Normal alignment: positive dot product means viewing the front of the feature
    if (focusNormal.lengthSq() > 0.1) {
      const normalDot = dir.dot(focusNormal);
      score += normalDot * 4.0;

      // Penalize grazing angles where feature becomes edge-on
      if (Math.abs(normalDot) < 0.15) {
        score -= 2.0;
      }
    }

    // 2. Outward positioning: viewing from the exterior side of the model
    if (outwardDir.lengthSq() > 0.1) {
      const outwardDot = dir.dot(outwardDir);
      score += outwardDot * 2.5;
    }

    // 3. Overhead preference (CAD convention: looking slightly downwards onto the part)
    if (dir.y > 0.2) {
      score += dir.y * 1.5;
    }

    // 4. Stable tie-breaker towards primary octant (+X, +Y, +Z)
    if (dir.x > 0 && dir.z > 0) {
      score += 0.2;
    }

    if (score > bestScore) {
      bestScore = score;
      bestDir = dir;
    }
  }

  return bestDir.clone();
}

/**
 * Returns standard CAD camera direction for predefined views.
 */
export function getStandardViewDirection(view: StandardView): THREE.Vector3 {
  switch (view) {
    case 'front':
      return new THREE.Vector3(0, 0, 1);
    case 'top':
      return new THREE.Vector3(0, 1, 0.0001); // slightly off-axis for OrbitControls stability
    case 'right':
      return new THREE.Vector3(1, 0, 0);
    case 'iso':
    default:
      return new THREE.Vector3(1, 1, 1.2).normalize();
  }
}

/**
 * Computes camera position, target, distance and clipping planes given a bounding box and direction.
 */
export function calculateCameraPose(
  targetBox: THREE.Box3,
  viewDir: THREE.Vector3,
  fovDegrees = 45,
  margin = 1.7
): CameraPose {
  if (targetBox.isEmpty()) {
    return {
      position: new THREE.Vector3(15, 15, 20),
      target: new THREE.Vector3(0, 0, 0),
      distance: 25,
      near: 0.1,
      far: 1000,
    };
  }

  const center = targetBox.getCenter(new THREE.Vector3());
  const size = targetBox.getSize(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z, 1e-4);

  const fovRad = (fovDegrees * Math.PI) / 180;
  const cameraDistance = ((maxDim / 2) / Math.tan(fovRad / 2)) * margin;

  const position = new THREE.Vector3()
    .copy(center)
    .addScaledVector(viewDir.clone().normalize(), cameraDistance);

  const near = Math.max(maxDim * 0.01, 0.01);
  const far = Math.max(maxDim * 80, 500);

  return {
    position,
    target: center,
    distance: cameraDistance,
    near,
    far,
  };
}
