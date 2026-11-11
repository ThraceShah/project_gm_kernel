import * as THREE from 'three';
import { LineSegments2 } from 'three/examples/jsm/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/examples/jsm/lines/LineSegmentsGeometry.js';
import { LineMaterial } from 'three/examples/jsm/lines/LineMaterial.js';
import { MeshData, EntityRole } from '../types/contract';

export const ROLE_COLORS: Record<EntityRole, { hex: number; css: string; label: string }> = {
  selected: { hex: 0x2563eb, css: '#2563eb', label: '选中 / 关键面' },
  support: { hex: 0x3b82f6, css: '#3b82f6', label: '支撑面' },
  newBlend: { hex: 0x16a34a, css: '#16a34a', label: '新圆角 / 生成面' },
  modified: { hex: 0xea580c, css: '#ea580c', label: '修改面' },
  deleted: { hex: 0xdc2626, css: '#dc2626', label: '删除面 (仅前态)' },
  unchanged: { hex: 0x94a3b8, css: '#94a3b8', label: '未改动实体' },
};

export const EDGE_ROLE_COLORS: Record<EntityRole, { hex: number; css: string; label: string }> = {
  selected: { hex: 0xfacc15, css: '#facc15', label: '选中边' },
  support: { hex: 0x60a5fa, css: '#60a5fa', label: '支撑边' },
  newBlend: { hex: 0x22c55e, css: '#22c55e', label: '新边界边' },
  modified: { hex: 0xf97316, css: '#f97316', label: '修改边' },
  deleted: { hex: 0xef4444, css: '#ef4444', label: '删除边' },
  unchanged: { hex: 0x334155, css: '#334155', label: '未改动边' },
};

export interface PickedEntity {
  type: 'face' | 'edge';
  id: string;
  role: EntityRole;
  triangleCount?: number;
  vertexCount?: number;
}

export interface GeometryDisplayOptions {
  unchangedOpacity?: number; // 0.0 to 1.0 (default 1.0; 0.25 when dimmed)
  isolateModified?: boolean;  // When true, hide unchanged faces & edges
  showTools?: boolean;        // When false, hide tool bodies
  showIsoparametrics?: boolean; // When true, render true surface isoparametrics
}

/**
 * Builds a Three.js Group representing a body from MeshData, supporting
 * layered shading, transparency ordering, depthWrite control, and true B-rep isoparametric curves.
 */
export function buildThreeGeometry(
  meshData: MeshData,
  options?: GeometryDisplayOptions
): THREE.Group {
  const root = new THREE.Group();
  root.name = `body_${meshData.bodyId}`;

  const unchangedOpacity = options?.unchangedOpacity ?? 1.0;
  const isolateModified = options?.isolateModified ?? false;
  const isDimmed = unchangedOpacity < 0.99;
  const showIsoparametrics = options?.showIsoparametrics ?? true;
  const showTools = options?.showTools ?? true;

  // 1. Build Faces
  if (Array.isArray(meshData.faces)) {
    meshData.faces.forEach((face, fIndex) => {
      if (!face.positions || face.positions.length < 9 || !face.indices || face.indices.length < 3) {
        return;
      }

      if (!showTools && face.isTool) {
        return;
      }

      const isUnchanged = face.role === 'unchanged';
      if (isolateModified && isUnchanged) {
        return;
      }

      const geom = new THREE.BufferGeometry();
      geom.setAttribute('position', new THREE.Float32BufferAttribute(face.positions, 3));
      geom.setIndex(face.indices);

      if (face.normals && face.normals.length === face.positions.length) {
        geom.setAttribute('normal', new THREE.Float32BufferAttribute(face.normals, 3));
      } else {
        geom.computeVertexNormals();
      }

      const roleColor = ROLE_COLORS[face.role] ?? ROLE_COLORS.unchanged;

      // Handle transparent ordering: transparent unchanged surfaces do not write depth,
      // allowing opaque modified/selected features and edges inside/behind to show through clearly.
      const mat = new THREE.MeshStandardMaterial({
        color: roleColor.hex,
        roughness: isUnchanged && isDimmed ? 0.6 : 0.35,
        metalness: isUnchanged && isDimmed ? 0.0 : 0.1,
        side: THREE.DoubleSide,
        polygonOffset: true,
        polygonOffsetFactor: 1,
        polygonOffsetUnits: 1,
        transparent: isUnchanged && isDimmed,
        opacity: isUnchanged ? unchangedOpacity : 1.0,
        depthWrite: !(isUnchanged && isDimmed),
      });

      const mesh = new THREE.Mesh(geom, mat);
      mesh.name = `face_${face.id || fIndex}`;
      mesh.renderOrder = isUnchanged && isDimmed ? 0 : 1;
      mesh.userData = {
        type: 'face',
        id: face.id || `face_${fIndex}`,
        role: face.role,
        isTool: face.isTool ?? false,
        triangleCount: face.indices.length / 3,
        vertexCount: face.positions.length / 3,
      };

      root.add(mesh);
    });
  }

  // 2. Build Topological Boundary Edges
  if (Array.isArray(meshData.edges)) {
    meshData.edges.forEach((edge, eIndex) => {
      if (!edge.positions || edge.positions.length < 6) {
        return;
      }

      if (!showTools && edge.isTool) {
        return;
      }

      const isUnchanged = edge.role === 'unchanged';
      if (isolateModified && isUnchanged) {
        return;
      }

      const geom = new THREE.BufferGeometry();
      geom.setAttribute('position', new THREE.Float32BufferAttribute(edge.positions, 3));

      const edgeColor = EDGE_ROLE_COLORS[edge.role] ?? EDGE_ROLE_COLORS.unchanged;

      const mat = new THREE.LineBasicMaterial({
        color: edgeColor.hex,
        linewidth: edge.role === 'selected' ? 2 : 1,
        depthTest: true,
        transparent: isUnchanged && isDimmed,
        opacity: isUnchanged ? Math.max(unchangedOpacity, 0.4) : 1.0,
      });

      // Native WebGL line widths are capped at 1px on many devices. Preserve
      // depth occlusion while giving the actual selected edge a readable stroke.
      const lines = edge.role === 'selected'
        ? new LineSegments2(new LineSegmentsGeometry().setPositions(edge.positions), new LineMaterial({ color: edgeColor.hex, linewidth: 2.5, depthTest: true, depthWrite: false, toneMapped: false }))
        : new THREE.LineSegments(geom, mat);
      if (edge.role === 'selected') { geom.dispose(); mat.dispose(); }
      lines.name = `edge_${edge.id || eIndex}`;
      lines.renderOrder = edge.role === 'selected' ? 4 : 2;
      lines.userData = {
        type: 'edge',
        id: edge.id || `edge_${eIndex}`,
        role: edge.role,
        isTool: edge.isTool ?? false,
        vertexCount: edge.positions.length / 3,
      };

      root.add(lines);
    });
  }

  // 3. Build True Isoparametric Lines (if available and enabled)
  if (showIsoparametrics) {
    const isoSegments: number[] = [];
    const isoColors: number[] = [];
    let isoLineCount = 0;
    let uLineCount = 0;
    let vLineCount = 0;

    const addPolyline = (polyline: number[], direction?: 'u' | 'v') => {
      if (!polyline || polyline.length < 6) return;
      isoLineCount++;
      if (direction === 'u') uLineCount++;
      if (direction === 'v') vLineCount++;
      for (let i = 0; i < polyline.length - 3; i += 3) {
        isoSegments.push(
          polyline[i],
          polyline[i + 1],
          polyline[i + 2],
          polyline[i + 3],
          polyline[i + 4],
          polyline[i + 5]
        );
        const color = new THREE.Color(direction === 'v' ? 0xc084fc : 0x38bdf8);
        isoColors.push(color.r, color.g, color.b, color.r, color.g, color.b);
      }
    };

    // Collect from FaceData.isoparametrics
    if (Array.isArray(meshData.faces)) {
      meshData.faces.forEach((face) => {
        if (!showTools && face.isTool) return;
        if (isolateModified && face.role === 'unchanged') return;
        if (face.isoparametrics) {
          if (Array.isArray(face.isoparametrics.uLines)) {
            face.isoparametrics.uLines.forEach((line) => addPolyline(line, 'u'));
          }
          if (Array.isArray(face.isoparametrics.vLines)) {
            face.isoparametrics.vLines.forEach((line) => addPolyline(line, 'v'));
          }
          if (Array.isArray(face.isoparametrics.unclassifiedLines)) {
            face.isoparametrics.unclassifiedLines.forEach((line) => addPolyline(line));
          }
        }
      });
    }

    // Also collect from MeshData.isoparametrics if present and not already loaded from faces
    if (isoLineCount === 0 && Array.isArray(meshData.isoparametrics)) {
      meshData.isoparametrics.forEach((iso) => {
        const face = meshData.faces?.find((f) => f.id === iso.faceId);
        if (!face || (!showTools && face.isTool) || (isolateModified && face.role === 'unchanged')) return;
        addPolyline(iso.positions, iso.direction);
      });
    }

    if (isoSegments.length > 0) {
      const isoGeom = new THREE.BufferGeometry();
      isoGeom.setAttribute('position', new THREE.Float32BufferAttribute(isoSegments, 3));
      isoGeom.setAttribute('color', new THREE.Float32BufferAttribute(isoColors, 3));

      // Distinct, elegant CAD cyan-teal line for U/V surface parameters
      const isoMat = new THREE.LineBasicMaterial({
        vertexColors: true,
        depthTest: true,
        transparent: true,
        opacity: 0.85,
      });

      const isoLines = new THREE.LineSegments(isoGeom, isoMat);
      isoLines.name = '__helper_isoparametrics';
      isoLines.renderOrder = 3;
      isoLines.userData = {
        type: 'isoparametric',
        lineCount: isoLineCount,
        uLineCount,
        vLineCount,
        segmentCount: isoSegments.length / 6,
      };

      root.add(isoLines);
    }
  }

  return root;
}
