import * as THREE from 'three';
import { MeshData, StandardView } from '../types/contract';

export interface CameraPose {
  position: THREE.Vector3;
  target: THREE.Vector3;
  distance: number;
  near: number;
  far: number;
}

interface FeatureSample {
  point: THREE.Vector3;
  tangent?: THREE.Vector3;
  normal?: THREE.Vector3;
  weight: number;
}
interface Occluder { box: THREE.Box3; triangles: THREE.Triangle[] }
interface FeatureSet { samples: FeatureSample[]; occluders: Occluder[] }
export interface OperationFocusInfo {
  hasOperationFeatures: boolean;
  focusBox: THREE.Box3;
  fullBox: THREE.Box3;
  focusNormal: THREE.Vector3;
  focusCenter: THREE.Vector3;
  fullCenter: THREE.Vector3;
  sets: FeatureSet[];
  reason: string;
}

// Bounded deterministic samples, with every occluding triangle retained. Large
// snapshots fall back explicitly rather than claiming visibility from partial solids.
const MAX_TRIANGLES = 30000;
const MAX_SAMPLES = 48;

export function extractOperationFocus(mesh: MeshData | null, tools?: MeshData[] | null): OperationFocusInfo {
  const fullBox = new THREE.Box3(), focusBox = new THREE.Box3();
  const focusNormal = new THREE.Vector3();
  const occluders: Occluder[] = [];
  const samples: FeatureSample[] = [];
  const point = (p: number[], i: number) => new THREE.Vector3(p[i], p[i + 1], p[i + 2]);
  let triangleCount = 0;
  for (const part of [mesh, ...(tools ?? [])]) {
    if (!part) continue;
    for (const entity of [...(part.faces ?? []), ...(part.edges ?? [])]) {
      for (let i = 0; i + 2 < entity.positions.length; i += 3) fullBox.expandByPoint(point(entity.positions, i));
    }
    triangleCount += part.faces.reduce((n, f) => n + f.indices.length / 3, 0);
  }
  const finish = (reason: string): OperationFocusInfo => {
    const hasOperationFeatures = samples.length > 0;
    if (!hasOperationFeatures) focusBox.copy(fullBox);
    return { hasOperationFeatures, fullBox, focusBox, focusNormal: focusNormal.normalize(),
      focusCenter: focusBox.isEmpty() ? new THREE.Vector3() : focusBox.getCenter(new THREE.Vector3()),
      fullCenter: fullBox.isEmpty() ? new THREE.Vector3() : fullBox.getCenter(new THREE.Vector3()),
      sets: hasOperationFeatures ? [{ samples, occluders }] : [], reason };
  };
  if (!mesh || triangleCount > MAX_TRIANGLES) return finish(triangleCount > MAX_TRIANGLES ? '全模型：超出可见性计算预算' : '全模型：无几何');
  for (const part of [mesh, ...(tools ?? [])]) {
    for (const face of part?.faces ?? []) {
      const triangles: THREE.Triangle[] = [], box = new THREE.Box3();
      for (let i = 0; i + 2 < face.indices.length; i += 3) {
        const tri = new THREE.Triangle(...face.indices.slice(i, i + 3).map((index) => point(face.positions, index * 3)) as [THREE.Vector3, THREE.Vector3, THREE.Vector3]);
        triangles.push(tri); box.expandByPoint(tri.a).expandByPoint(tri.b).expandByPoint(tri.c);
      }
      occluders.push({ box, triangles });
    }
  }
  // A blue support face can be much larger than the actual operation. Never use
  // its bounding box to displace selected edges or new result faces.
  const edges = mesh.edges.filter((e) => !e.isTool && e.role === 'selected' && e.positions.length >= 6);
  const role = ['newBlend', 'modified', 'selected', 'deleted'].find((r) => mesh.faces.some((f) => !f.isTool && f.role === r));
  if (edges.length) {
    const segments: { a: THREE.Vector3; b: THREE.Vector3; length: number }[] = [];
    for (const edge of edges) {
      for (let i = 0; i + 5 < edge.positions.length; i += 3) {
        const a = point(edge.positions, i), b = point(edge.positions, i + 3), length = a.distanceTo(b);
        if (length > 0) segments.push({ a, b, length });
        focusBox.expandByPoint(a).expandByPoint(b);
      }
    }
    const total = segments.reduce((n, s) => n + s.length, 0);
    let offset = 0, segmentIndex = 0;
    for (let i = 0; i < MAX_SAMPLES && segments.length; i++) {
      const d = total * (i + 0.5) / MAX_SAMPLES;
      while (segmentIndex < segments.length - 1 && offset + segments[segmentIndex].length < d) offset += segments[segmentIndex++].length;
      const s = segments[segmentIndex], p = s.a.clone().lerp(s.b, (d - offset) / s.length);
      samples.push({ point: p, tangent: s.b.clone().sub(s.a).normalize(), weight: total / MAX_SAMPLES });
    }
    // Adjacency from actual geometry, without relying on exporter-specific tags.
    const epsilon = fullBox.getSize(new THREE.Vector3()).length() * 1e-5;
    const nearest = new THREE.Vector3();
    for (const block of occluders) {
      if (samples.some((s) => block.triangles.some((t) => t.closestPointToPoint(s.point, nearest).distanceTo(s.point) < epsilon))) {
        focusNormal.add(block.triangles[0]?.getNormal(new THREE.Vector3()) ?? new THREE.Vector3());
      }
    }
  } else if (role) {
    const triangles: THREE.Triangle[] = [];
    for (const face of mesh.faces.filter((f) => !f.isTool && f.role === role)) {
      for (let i = 0; i + 2 < face.indices.length; i += 3) {
        triangles.push(new THREE.Triangle(...face.indices.slice(i, i + 3).map((index) => point(face.positions, index * 3)) as [THREE.Vector3, THREE.Vector3, THREE.Vector3]));
      }
    }
    const total = triangles.reduce((n, t) => n + t.getArea(), 0);
    let offset = 0, triangleIndex = 0;
    for (const tri of triangles) focusBox.expandByPoint(tri.a).expandByPoint(tri.b).expandByPoint(tri.c);
    for (let i = 0; i < MAX_SAMPLES && total > 0; i++) {
      const area = total * (i + 0.5) / MAX_SAMPLES;
      while (triangleIndex < triangles.length - 1 && offset + triangles[triangleIndex].getArea() < area) offset += triangles[triangleIndex++].getArea();
      const tri = triangles[triangleIndex], normal = tri.getNormal(new THREE.Vector3());
      samples.push({ point: tri.getMidpoint(new THREE.Vector3()), normal, weight: total / MAX_SAMPLES });
      focusNormal.add(normal);
    }
  }
  return finish(samples.length ? (edges.length ? '选中边 · 可见性采样' : `${role} 面 · 可见性采样`) : '全模型：未记录可靠修改标记');
}

/** Same target, direction and bounds for linked GM/PK; each retains its own occluders. */
export function mergeOperationFocus(infos: OperationFocusInfo[]): OperationFocusInfo {
  const fullBox = new THREE.Box3(), focusBox = new THREE.Box3(), focusNormal = new THREE.Vector3();
  for (const info of infos) { fullBox.union(info.fullBox); focusBox.union(info.focusBox); focusNormal.add(info.focusNormal); }
  const sets = infos.flatMap((i) => i.sets);
  const reliable = infos.length > 0 && infos.every((i) => i.hasOperationFeatures);
  if (!reliable) focusBox.copy(fullBox);
  return { fullBox, focusBox, focusNormal: focusNormal.normalize(), hasOperationFeatures: reliable,
    fullCenter: fullBox.getCenter(new THREE.Vector3()), focusCenter: focusBox.getCenter(new THREE.Vector3()),
    sets: reliable ? sets : [], reason: reliable ? '两侧操作几何 · 共享取景' : '全模型：一侧未记录可靠修改标记' };
}

function sampleVisible(sample: FeatureSample, direction: THREE.Vector3, set: FeatureSet, epsilon: number): boolean {
  const ray = new THREE.Ray(sample.point.clone().addScaledVector(direction, epsilon), direction);
  const hit = new THREE.Vector3();
  for (const block of set.occluders) {
    if (!ray.intersectsBox(block.box)) continue;
    for (const tri of block.triangles) {
      if (ray.intersectTriangle(tri.a, tri.b, tri.c, false, hit) && hit.distanceTo(ray.origin) > epsilon) return false;
    }
  }
  return true;
}

export function scoreViewDirection(info: OperationFocusInfo, direction: THREE.Vector3) {
  const epsilon = Math.max(info.fullBox.getSize(new THREE.Vector3()).length() * 1e-5, 1e-8);
  const results = info.sets.map((set) => {
    let visible = 0, projected = 0, total = 0;
    for (const sample of set.samples) {
      total += sample.weight;
      if (!sampleVisible(sample, direction, set, epsilon)) continue;
      visible += sample.weight;
      projected += sample.weight * (sample.tangent ? Math.sqrt(Math.max(0, 1 - sample.tangent.dot(direction) ** 2)) : Math.abs(sample.normal!.dot(direction)));
    }
    return { visible: total ? visible / total : 0, projected: total ? projected / total : 0 };
  });
  const scores = results.map((r) => r.visible * 0.75 + r.projected * 0.25);
  // Reward the weaker pane first; a strong GM view cannot hide the PK feature.
  return { results, score: scores.length ? Math.min(...scores) * 0.7 + scores.reduce((a, b) => a + b, 0) / scores.length * 0.3 : 0 };
}

export function determineOptimalViewDirection(info: OperationFocusInfo): THREE.Vector3 {
  const standard = new THREE.Vector3(1, 1, 1.2).normalize();
  if (!info.hasOperationFeatures) return standard;
  const candidates: THREE.Vector3[] = [];
  // Include underside and side views, with no preferred world-up hemisphere.
  for (const x of [-1, 0, 1]) for (const y of [-1, 0, 1]) for (const z of [-1, 0, 1]) {
    if (x || y || z) candidates.push(new THREE.Vector3(x, y, z).normalize());
  }
  const outward = info.focusCenter.clone().sub(info.fullCenter).normalize();
  for (const dir of [info.focusNormal, outward, info.focusNormal.clone().add(outward)]) {
    if (dir.lengthSq() > 0.01) candidates.push(dir.clone().normalize());
  }
  // Bounded normal candidates improve small sloped result faces as well.
  for (const set of info.sets) for (let i = 0; i < set.samples.length; i += 8) {
    const normal = set.samples[i].normal;
    if (normal) candidates.push(normal.clone().normalize());
  }
  let best = standard, bestScore = -Infinity;
  for (const dir of candidates) {
    const score = scoreViewDirection(info, dir).score + Math.max(0, dir.dot(info.focusNormal)) * 0.005;
    if (score > bestScore) { bestScore = score; best = dir; }
  }
  return best.clone();
}

/** Actual perspective projection/occlusion, also exposed for review evidence. */
export function measureFeatureVisibility(info: OperationFocusInfo, camera: THREE.PerspectiveCamera, width: number, height: number) {
  const epsilon = Math.max(info.fullBox.getSize(new THREE.Vector3()).length() * 1e-5, 1e-8);
  let samples = 0, visible = 0, projectedPixels = 0;
  const bounds = new THREE.Box2();
  for (const set of info.sets) for (const sample of set.samples) {
    samples++;
    const projected = sample.point.clone().project(camera);
    const inFrame = Math.abs(projected.x) < 0.95 && Math.abs(projected.y) < 0.95 && Math.abs(projected.z) < 1;
    const direction = camera.position.clone().sub(sample.point).normalize();
    if (!inFrame || !sampleVisible(sample, direction, set, epsilon)) continue;
    visible++;
    bounds.expandByPoint(new THREE.Vector2((projected.x + 1) * width / 2, (1 - projected.y) * height / 2));
    const scale = height / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * camera.position.distanceTo(sample.point));
    projectedPixels += sample.tangent ? sample.weight * Math.sqrt(Math.max(0, 1 - sample.tangent.dot(direction) ** 2)) * scale : sample.weight * Math.abs(sample.normal!.dot(direction)) * scale ** 2;
  }
  return { samples, visible, visibleFraction: samples ? visible / samples : null, projectedPixels,
    projectionUnit: info.sets[0]?.samples[0]?.tangent ? 'length-px' : 'area-px2',
    bounds: bounds.isEmpty() ? null : { min: bounds.min.toArray(), max: bounds.max.toArray() }, reason: info.reason };
}

export function getStandardViewDirection(view: StandardView): THREE.Vector3 {
  switch (view) {
    case 'front': return new THREE.Vector3(0, 0, 1);
    case 'top': return new THREE.Vector3(0, 1, 0.0001).normalize();
    case 'right': return new THREE.Vector3(1, 0, 0);
    default: return new THREE.Vector3(1, 1, 1.2).normalize();
  }
}

export function calculateCameraPose(targetBox: THREE.Box3, viewDir: THREE.Vector3, fovDegrees = 45, margin = 1.3, aspect = 1, target?: THREE.Vector3): CameraPose {
  if (targetBox.isEmpty()) return { position: new THREE.Vector3(15, 15, 20), target: new THREE.Vector3(), distance: 25, near: 0.1, far: 1000 };
  const center = target?.clone() ?? targetBox.getCenter(new THREE.Vector3());
  const dir = viewDir.clone().normalize();
  const right = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), dir).normalize();
  if (right.lengthSq() < 0.01) right.set(1, 0, 0);
  const up = new THREE.Vector3().crossVectors(dir, right).normalize();
  const tan = Math.tan(THREE.MathUtils.degToRad(fovDegrees / 2));
  let distance = 0;
  for (const x of [targetBox.min.x, targetBox.max.x]) for (const y of [targetBox.min.y, targetBox.max.y]) for (const z of [targetBox.min.z, targetBox.max.z]) {
    const delta = new THREE.Vector3(x, y, z).sub(center);
    distance = Math.max(distance, delta.dot(dir) + margin * Math.max(Math.abs(delta.dot(right)) / (tan * Math.max(aspect, 0.1)), Math.abs(delta.dot(up)) / tan));
  }
  const size = Math.max(targetBox.getSize(new THREE.Vector3()).length(), 1e-4);
  distance = Math.max(distance, size * 0.1);
  return { position: center.clone().addScaledVector(dir, distance), target: center, distance, near: size * 0.001, far: Math.max(distance + size * 10, 500) };
}

export function operationCameraPose(info: OperationFocusInfo, aspect = 1): CameraPose {
  const dir = determineOptimalViewDirection(info);
  return calculateCameraPose(info.fullBox, dir, 45, 1.15, aspect, info.hasOperationFeatures ? info.focusCenter : info.fullCenter);
}
