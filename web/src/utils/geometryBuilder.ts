import * as THREE from 'three';
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

/**
 * Builds a Three.js Group representing a body from MeshData
 */
export function buildThreeGeometry(meshData: MeshData): THREE.Group {
  const root = new THREE.Group();
  root.name = `body_${meshData.bodyId}`;

  // 1. Build Faces
  if (Array.isArray(meshData.faces)) {
    meshData.faces.forEach((face, fIndex) => {
      if (!face.positions || face.positions.length < 9 || !face.indices || face.indices.length < 3) {
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

      const mat = new THREE.MeshStandardMaterial({
        color: roleColor.hex,
        roughness: 0.35,
        metalness: 0.1,
        side: THREE.DoubleSide,
        polygonOffset: true,
        polygonOffsetFactor: 1,
        polygonOffsetUnits: 1,
      });

      const mesh = new THREE.Mesh(geom, mat);
      mesh.name = `face_${face.id || fIndex}`;
      mesh.userData = {
        type: 'face',
        id: face.id || `face_${fIndex}`,
        role: face.role,
        triangleCount: face.indices.length / 3,
        vertexCount: face.positions.length / 3,
      };

      root.add(mesh);
    });
  }

  // 2. Build Edges
  if (Array.isArray(meshData.edges)) {
    meshData.edges.forEach((edge, eIndex) => {
      if (!edge.positions || edge.positions.length < 6) {
        return;
      }

      const geom = new THREE.BufferGeometry();
      geom.setAttribute('position', new THREE.Float32BufferAttribute(edge.positions, 3));

      const edgeColor = EDGE_ROLE_COLORS[edge.role] ?? EDGE_ROLE_COLORS.unchanged;

      const mat = new THREE.LineBasicMaterial({
        color: edgeColor.hex,
        linewidth: edge.role === 'selected' ? 2 : 1,
        depthTest: true,
      });

      const lines = new THREE.LineSegments(geom, mat);
      lines.name = `edge_${edge.id || eIndex}`;
      lines.userData = {
        type: 'edge',
        id: edge.id || `edge_${eIndex}`,
        role: edge.role,
        vertexCount: edge.positions.length / 3,
      };

      root.add(lines);
    });
  }

  return root;
}
