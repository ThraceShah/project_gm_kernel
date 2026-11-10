import React, { useEffect, useRef, useCallback } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { MeshData } from '../types/contract';
import { buildThreeGeometry, PickedEntity } from '../utils/geometryBuilder';

export interface CameraSyncState {
  position: [number, number, number];
  target: [number, number, number];
  quaternion: [number, number, number, number];
  zoom: number;
}

interface MeshViewerProps {
  kernelName: 'GM Kernel' | 'Parasolid (PK)';
  meshData: MeshData | null;
  isLoading: boolean;
  emptyMessage?: string;
  errorMessage?: string | null;
  cameraSync: boolean;
  externalCameraState?: CameraSyncState | null;
  onCameraChange?: (state: CameraSyncState) => void;
  resetTrigger: number;
  onSelectEntity?: (entity: PickedEntity | null) => void;
}

export const MeshViewer: React.FC<MeshViewerProps> = ({
  kernelName,
  meshData,
  isLoading,
  emptyMessage = '无几何数据',
  errorMessage,
  cameraSync,
  externalCameraState,
  onCameraChange,
  resetTrigger,
  onSelectEntity,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const currentMeshGroupRef = useRef<THREE.Group | null>(null);
  const isSyncingFromExternal = useRef<boolean>(false);

  // Fit camera to bounding box
  const fitCameraToBox = useCallback((group: THREE.Group) => {
    if (!cameraRef.current || !controlsRef.current) return;
    const box = new THREE.Box3().setFromObject(group);
    if (box.isEmpty()) return;

    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z, 5);

    const fov = cameraRef.current.fov * (Math.PI / 180);
    const cameraDistance = (maxDim / 2) / Math.tan(fov / 2) * 1.8;

    const direction = new THREE.Vector3(1, 1, 1.2).normalize();
    cameraRef.current.position.copy(center).add(direction.multiplyScalar(cameraDistance));
    controlsRef.current.target.copy(center);
    cameraRef.current.near = maxDim * 0.01;
    cameraRef.current.far = maxDim * 50;
    cameraRef.current.updateProjectionMatrix();
    controlsRef.current.update();
  }, []);

  // Initialize Three.js scene
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const width = container.clientWidth || 400;
    const height = container.clientHeight || 300;

    // Scene
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0f172a); // Slate-900 CAD dark
    sceneRef.current = scene;

    // Camera
    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    camera.position.set(15, 15, 20);
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      powerPreference: 'high-performance',
    });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    rendererRef.current = renderer;
    container.appendChild(renderer.domElement);

    // Controls
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.target.set(5, 5, 5);
    controlsRef.current = controls;

    // Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
    scene.add(ambientLight);

    const dirLight1 = new THREE.DirectionalLight(0xffffff, 0.9);
    dirLight1.position.set(20, 40, 30);
    scene.add(dirLight1);

    const dirLight2 = new THREE.DirectionalLight(0x94a3b8, 0.5);
    dirLight2.position.set(-20, -20, -20);
    scene.add(dirLight2);

    // Subtle CAD Floor Grid
    const grid = new THREE.GridHelper(30, 30, 0x334155, 0x1e293b);
    grid.position.y = -0.01;
    scene.add(grid);

    // Axes Helper
    const axes = new THREE.AxesHelper(3);
    scene.add(axes);

    // Render loop
    let animId: number;
    const animate = () => {
      animId = requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    // Broadcast camera change
    const onControlsChange = () => {
      if (isSyncingFromExternal.current) return;
      if (cameraSync && onCameraChange && cameraRef.current && controlsRef.current) {
        const c = cameraRef.current;
        const t = controlsRef.current.target;
        onCameraChange({
          position: [c.position.x, c.position.y, c.position.z],
          target: [t.x, t.y, t.z],
          quaternion: [c.quaternion.x, c.quaternion.y, c.quaternion.z, c.quaternion.w],
          zoom: c.zoom,
        });
      }
    };
    controls.addEventListener('change', onControlsChange);

    // Resize observer
    const resizeObserver = new ResizeObserver(() => {
      if (!container || !rendererRef.current || !cameraRef.current) return;
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (w > 0 && h > 0) {
        cameraRef.current.aspect = w / h;
        cameraRef.current.updateProjectionMatrix();
        rendererRef.current.setSize(w, h);
      }
    });
    resizeObserver.observe(container);

    // Cleanup
    return () => {
      cancelAnimationFrame(animId);
      controls.removeEventListener('change', onControlsChange);
      resizeObserver.disconnect();
      controls.dispose();
      renderer.dispose();
      if (renderer.domElement && container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [cameraSync, onCameraChange]);

  // Apply external camera sync
  useEffect(() => {
    if (!cameraSync || !externalCameraState || !cameraRef.current || !controlsRef.current) {
      return;
    }

    isSyncingFromExternal.current = true;
    const [px, py, pz] = externalCameraState.position;
    const [tx, ty, tz] = externalCameraState.target;
    const [qx, qy, qz, qw] = externalCameraState.quaternion;

    cameraRef.current.position.set(px, py, pz);
    cameraRef.current.quaternion.set(qx, qy, qz, qw);
    cameraRef.current.zoom = externalCameraState.zoom;
    cameraRef.current.updateProjectionMatrix();
    controlsRef.current.target.set(tx, ty, tz);
    controlsRef.current.update();

    // Unlock after small delay
    setTimeout(() => {
      isSyncingFromExternal.current = false;
    }, 10);
  }, [externalCameraState, cameraSync]);

  // Reset View Trigger
  useEffect(() => {
    if (resetTrigger > 0 && currentMeshGroupRef.current) {
      fitCameraToBox(currentMeshGroupRef.current);
    }
  }, [resetTrigger, fitCameraToBox]);

  // Update Geometry when meshData changes
  useEffect(() => {
    const scene = sceneRef.current;
    if (!scene) return;

    // Remove old mesh group
    if (currentMeshGroupRef.current) {
      scene.remove(currentMeshGroupRef.current);
      currentMeshGroupRef.current.traverse((child) => {
        if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) child.material.forEach((m) => m.dispose());
          else child.material.dispose();
        } else if (child instanceof THREE.LineSegments) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) child.material.forEach((m) => m.dispose());
          else child.material.dispose();
        }
      });
      currentMeshGroupRef.current = null;
    }

    if (!meshData) return;

    // Build new geometry group
    const newGroup = buildThreeGeometry(meshData);
    scene.add(newGroup);
    currentMeshGroupRef.current = newGroup;

    // Center view if not synced or first load
    fitCameraToBox(newGroup);
  }, [meshData, fitCameraToBox]);

  // Raycasting for entity click & hover
  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!rendererRef.current || !cameraRef.current || !currentMeshGroupRef.current) return;
    const rect = rendererRef.current.domElement.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    const raycaster = new THREE.Raycaster();
    raycaster.params.Line = { threshold: 0.1 };
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.current);

    const intersects = raycaster.intersectObjects(currentMeshGroupRef.current.children, true);
    if (intersects.length > 0) {
      const hit = intersects[0].object;
      if (hit.userData && hit.userData.id) {
        onSelectEntity?.({
          type: hit.userData.type,
          id: hit.userData.id,
          role: hit.userData.role,
          triangleCount: hit.userData.triangleCount,
          vertexCount: hit.userData.vertexCount,
        });
        return;
      }
    }
    onSelectEntity?.(null);
  };

  return (
    <div className="relative w-full h-full bg-slate-900 overflow-hidden select-none">
      {/* 3D WebGL Canvas */}
      <div
        ref={containerRef}
        onPointerDown={handlePointerDown}
        className="w-full h-full cursor-grab active:cursor-grabbing"
      />

      {/* Top Banner Tag: Kernel & Tessellator info */}
      <div className="absolute top-2 left-2 z-10 flex items-center gap-1.5 bg-slate-900/80 backdrop-blur-xs border border-slate-700/60 rounded px-2.5 py-1 text-xs text-slate-200">
        <span
          className={`w-2 h-2 rounded-full ${
            kernelName === 'GM Kernel' ? 'bg-sky-400' : 'bg-emerald-400'
          }`}
        />
        <span className="font-semibold">{kernelName}</span>
        {meshData?.source?.tessellator && (
          <span className="text-[10px] text-slate-400 border-l border-slate-700 pl-1.5 font-mono-code">
            {meshData.source.tessellator}
          </span>
        )}
      </div>

      {/* Loading Overlay */}
      {isLoading && (
        <div className="absolute inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-20">
          <div className="flex items-center gap-2 text-xs text-sky-400 bg-slate-900 px-3 py-2 rounded border border-slate-700 shadow-md">
            <span className="w-3.5 h-3.5 border-2 border-sky-400 border-t-transparent rounded-full animate-spin" />
            <span>网格数据加载中...</span>
          </div>
        </div>
      )}

      {/* Empty / Error Overlay */}
      {!isLoading && !meshData && (
        <div className="absolute inset-0 flex flex-col items-center justify-center p-6 text-center z-10 pointer-events-none">
          <div className="bg-slate-800/90 border border-slate-700 rounded-lg p-4 max-w-xs shadow-lg">
            <p className={`text-xs font-medium mb-1 ${errorMessage ? 'text-rose-400' : 'text-slate-300'}`}>
              {errorMessage || emptyMessage}
            </p>
            <p className="text-[11px] text-slate-400">
              {errorMessage
                ? '几何切片请求异常或网络错误，旧网格已被清除'
                : `当前步骤未包含 ${kernelName} 几何切片或该用例在此内核中执行失败/不支持。`}
            </p>
          </div>
        </div>
      )}
    </div>
  );
};
