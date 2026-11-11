import React, { useEffect, useRef, useCallback } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { MeshData, StandardView } from '../types/contract';
import { buildThreeGeometry, PickedEntity, GeometryDisplayOptions } from '../utils/geometryBuilder';
import {
  extractOperationFocus,
  determineOptimalViewDirection,
  getStandardViewDirection,
  calculateCameraPose,
  CameraPose,
} from '../utils/cameraUtils';

export interface CameraSyncState {
  position: [number, number, number];
  target: [number, number, number];
  quaternion: [number, number, number, number];
  zoom: number;
  near: number;
  far: number;
}

export interface FocusTriggerState {
  id: number;
  type: 'operation' | 'full' | StandardView;
}

interface MeshViewerProps {
  kernelName: 'GM Kernel' | 'Parasolid (PK)';
  meshData: MeshData | null;
  toolMeshes?: MeshData[] | null;
  isLoading: boolean;
  emptyMessage?: string;
  errorMessage?: string | null;
  cameraSync: boolean;
  externalCameraState?: CameraSyncState | null;
  onCameraChange?: (state: CameraSyncState) => void;
  resetTrigger: number;
  focusTrigger?: FocusTriggerState | null;
  displayOptions?: GeometryDisplayOptions;
  caseId?: string;
  onSelectEntity?: (entity: PickedEntity | null) => void;
}

export const MeshViewer: React.FC<MeshViewerProps> = ({
  kernelName,
  meshData,
  toolMeshes,
  isLoading,
  emptyMessage = '无几何数据',
  errorMessage,
  cameraSync,
  externalCameraState,
  onCameraChange,
  resetTrigger,
  focusTrigger,
  displayOptions,
  caseId,
  onSelectEntity,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const currentMeshGroupRef = useRef<THREE.Group | null>(null);
  const isSyncingFromExternal = useRef<boolean>(false);
  const pickThresholdRef = useRef<number>(0.1);

  // User manual interaction and case tracking refs
  const userInteractedRef = useRef<boolean>(false);
  const lastCaseIdRef = useRef<string | undefined>(undefined);
  const lastFocusTriggerIdRef = useRef<number>(-1);

  // Navigation invalidates the previous fit, even when the destination has no mesh.
  // The geometry effect consumes it only after this context owns drawable geometry.
  useEffect(() => {
    lastCaseIdRef.current = undefined;
  }, [caseId]);

  // Latest-prop refs so the scene lifecycle never depends on callback identity.
  const cameraSyncRef = useRef<boolean>(cameraSync);
  const onCameraChangeRef = useRef<((state: CameraSyncState) => void) | undefined>(onCameraChange);
  useEffect(() => {
    cameraSyncRef.current = cameraSync;
    onCameraChangeRef.current = onCameraChange;
  });

  // Broadcasts current camera state to peer viewport
  const broadcastCamera = useCallback(() => {
    if (isSyncingFromExternal.current) return;
    if (cameraSyncRef.current && onCameraChangeRef.current && cameraRef.current && controlsRef.current) {
      const c = cameraRef.current;
      const t = controlsRef.current.target;
      onCameraChangeRef.current({
        position: [c.position.x, c.position.y, c.position.z],
        target: [t.x, t.y, t.z],
        quaternion: [c.quaternion.x, c.quaternion.y, c.quaternion.z, c.quaternion.w],
        zoom: c.zoom,
        near: c.near,
        far: c.far,
      });
    }
  }, []);

  // Applies a calculated camera pose to camera and controls
  const applyCameraPose = useCallback(
    (pose: CameraPose, broadcast = true) => {
      const camera = cameraRef.current;
      const controls = controlsRef.current;
      if (!camera || !controls) return;

      camera.position.copy(pose.position);
      controls.target.copy(pose.target);
      camera.near = pose.near;
      camera.far = pose.far;
      camera.updateProjectionMatrix();
      controls.update();

      pickThresholdRef.current = (pose.far - pose.near) * 0.0005;

      if (containerRef.current) {
        containerRef.current.dataset.cameraPos = `${pose.position.x.toFixed(2)},${pose.position.y.toFixed(2)},${pose.position.z.toFixed(2)}`;
        containerRef.current.dataset.cameraTarget = `${pose.target.x.toFixed(2)},${pose.target.y.toFixed(2)},${pose.target.z.toFixed(2)}`;
      }

      if (broadcast) {
        broadcastCamera();
      }
    },
    [broadcastCamera]
  );

  // Computes and applies operation-aware initial view
  const fitOperationAwareView = useCallback(
    (targetMesh: MeshData | null, targetTools?: MeshData[] | null, broadcast = true) => {
      const focusInfo = extractOperationFocus(targetMesh, targetTools);
      const optimalDir = determineOptimalViewDirection(focusInfo);
      const pose = calculateCameraPose(focusInfo.fullBox, optimalDir, cameraRef.current?.fov ?? 45, 1.75);

      // Center view comfortably between whole model center and operation focus center
      if (focusInfo.hasOperationFeatures) {
        pose.target.copy(focusInfo.fullCenter).lerp(focusInfo.focusCenter, 0.6);
        pose.position.copy(pose.target).addScaledVector(optimalDir, pose.distance);
      }

      applyCameraPose(pose, broadcast);
    },
    [applyCameraPose]
  );

  // Initialize Three.js scene exactly once (stable scene lifecycle)
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
      preserveDrawingBuffer: true,
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
    controls.target.set(0, 0, 0);
    controlsRef.current = controls;

    // Track user manual interaction to prevent playback / async response from resetting camera
    const onControlsStart = () => {
      userInteractedRef.current = true;
    };
    controls.addEventListener('start', onControlsStart);

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
    grid.name = '__helper_grid';
    scene.add(grid);

    // Axes Helper
    const axes = new THREE.AxesHelper(3);
    axes.name = '__helper_axes';
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
      if (containerRef.current && cameraRef.current && controlsRef.current) {
        const c = cameraRef.current;
        const t = controlsRef.current.target;
        containerRef.current.dataset.cameraPos = `${c.position.x.toFixed(2)},${c.position.y.toFixed(2)},${c.position.z.toFixed(2)}`;
        containerRef.current.dataset.cameraTarget = `${t.x.toFixed(2)},${t.y.toFixed(2)},${t.z.toFixed(2)}`;
      }
      broadcastCamera();
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

    // Cleanup: full scene / renderer / geometry disposal
    return () => {
      cancelAnimationFrame(animId);
      controls.removeEventListener('change', onControlsChange);
      controls.removeEventListener('start', onControlsStart);
      resizeObserver.disconnect();
      controls.dispose();
      scene.traverse((child) => {
        if (child instanceof THREE.Mesh || child instanceof THREE.LineSegments || child instanceof THREE.Line) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) child.material.forEach((m) => m.dispose());
          else child.material.dispose();
        }
      });
      renderer.dispose();
      if (renderer.domElement && container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
      sceneRef.current = null;
      cameraRef.current = null;
      controlsRef.current = null;
      rendererRef.current = null;
    };
  }, [broadcastCamera]);

  // Apply external camera sync (synchronous guard, shared near/far clipping)
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
    if (externalCameraState.near > 0) cameraRef.current.near = externalCameraState.near;
    if (externalCameraState.far > externalCameraState.near) cameraRef.current.far = externalCameraState.far;
    cameraRef.current.updateProjectionMatrix();
    controlsRef.current.target.set(tx, ty, tz);
    controlsRef.current.update();

    if (containerRef.current) {
      containerRef.current.dataset.cameraPos = `${px.toFixed(2)},${py.toFixed(2)},${pz.toFixed(2)}`;
      containerRef.current.dataset.cameraTarget = `${tx.toFixed(2)},${ty.toFixed(2)},${tz.toFixed(2)}`;
    }

    isSyncingFromExternal.current = false;
  }, [externalCameraState, cameraSync]);

  // Reset View Trigger: only execute once when resetTrigger counter increments,
  // never steal the camera on subsequent meshData/toolMeshes updates.
  const lastResetTriggerRef = useRef(resetTrigger);
  useEffect(() => {
    if (resetTrigger > lastResetTriggerRef.current) {
      lastResetTriggerRef.current = resetTrigger;
      userInteractedRef.current = false;
      if (meshData) {
        fitOperationAwareView(meshData, toolMeshes, true);
      }
    }
  }, [resetTrigger, meshData, toolMeshes, fitOperationAwareView]);

  // Explicit Focus / Standard View Trigger
  useEffect(() => {
    if (!focusTrigger || focusTrigger.id === lastFocusTriggerIdRef.current || !meshData) {
      return;
    }
    lastFocusTriggerIdRef.current = focusTrigger.id;

    const focusInfo = extractOperationFocus(meshData, toolMeshes);
    const camera = cameraRef.current;
    if (!camera) return;

    if (focusTrigger.type === 'operation') {
      const dir = determineOptimalViewDirection(focusInfo);
      const pose = calculateCameraPose(focusInfo.focusBox, dir, camera.fov, 1.5);
      applyCameraPose(pose, true);
    } else if (focusTrigger.type === 'full') {
      const dir = determineOptimalViewDirection(focusInfo);
      const pose = calculateCameraPose(focusInfo.fullBox, dir, camera.fov, 1.8);
      applyCameraPose(pose, true);
    } else {
      // Standard Views: 'iso' | 'front' | 'top' | 'right'
      const dir = getStandardViewDirection(focusTrigger.type);
      const pose = calculateCameraPose(focusInfo.fullBox, dir, camera.fov, 1.8);
      applyCameraPose(pose, true);
    }
  }, [focusTrigger, meshData, toolMeshes, applyCameraPose]);

  // Update Geometry when meshData or displayOptions change
  useEffect(() => {
    const scene = sceneRef.current;
    const container = containerRef.current;
    if (!scene) return;

    // Remove old mesh group
    if (currentMeshGroupRef.current) {
      scene.remove(currentMeshGroupRef.current);
      currentMeshGroupRef.current.traverse((child) => {
        if (child instanceof THREE.Mesh || child instanceof THREE.LineSegments) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) child.material.forEach((m) => m.dispose());
          else child.material.dispose();
        }
      });
      currentMeshGroupRef.current = null;
    }

    // Expose real drawn-geometry counts on the DOM for inspection/tests
    const setStats = (
      bodyId: string,
      faces: number,
      edges: number,
      triangles: number,
      roleFaces: Record<string, number>,
      roleEdges: Record<string, number>,
      hasIsoparametrics: boolean,
      isoLineCount: number
    ) => {
      if (!container) return;
      container.dataset.bodyId = bodyId;
      container.dataset.faceCount = String(faces);
      container.dataset.edgeCount = String(edges);
      container.dataset.triangleCount = String(triangles);
      container.dataset.roleFaces = JSON.stringify(roleFaces);
      container.dataset.roleEdges = JSON.stringify(roleEdges);
      container.dataset.hasIsoparametrics = hasIsoparametrics ? 'true' : 'false';
      container.dataset.isoparametricsCount = String(isoLineCount);
      container.dataset.uLineCount = '0';
      container.dataset.vLineCount = '0';
      container.dataset.dimmedOpacity = String(displayOptions?.unchangedOpacity ?? 1.0);
    };

    if (!meshData) {
      setStats('', 0, 0, 0, {}, {}, false, 0);
      return;
    }

    // Build new geometry group with display options
    const newGroup = new THREE.Group();
    newGroup.name = `viewer_root_${meshData.bodyId}`;

    const mainGroup = buildThreeGeometry(meshData, displayOptions);
    newGroup.add(mainGroup);

    // Optionally render tool bodies (e.g. for boolean cuts)
    if (displayOptions?.showTools !== false && Array.isArray(toolMeshes)) {
      toolMeshes.forEach((tool) => {
        const toolGroup = buildThreeGeometry(tool, displayOptions);
        toolGroup.name = `tool_${tool.bodyId}`;
        newGroup.add(toolGroup);
      });
    }

    let faces = 0;
    let edges = 0;
    let triangles = 0;
    let hasIsoparametrics = false;
    let isoLineCount = 0;
    let uLineCount = 0;
    let vLineCount = 0;
    const roleFaces: Record<string, number> = {};
    const roleEdges: Record<string, number> = {};

    newGroup.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        faces += 1;
        const role = String((child.userData && child.userData.role) || 'unknown');
        roleFaces[role] = (roleFaces[role] || 0) + 1;
        const index = child.geometry.getIndex();
        triangles += index ? index.count / 3 : child.geometry.getAttribute('position').count / 3;
      } else if (child instanceof THREE.LineSegments) {
        if (child.name === '__helper_isoparametrics') {
          hasIsoparametrics = true;
          isoLineCount += (child.userData?.lineCount as number) || 0;
          uLineCount += (child.userData?.uLineCount as number) || 0;
          vLineCount += (child.userData?.vLineCount as number) || 0;
        } else {
          edges += 1;
          const role = String((child.userData && child.userData.role) || 'unknown');
          roleEdges[role] = (roleEdges[role] || 0) + 1;
        }
      }
    });

    scene.add(newGroup);
    currentMeshGroupRef.current = newGroup;
    setStats(meshData.bodyId, faces, edges, triangles, roleFaces, roleEdges, hasIsoparametrics, isoLineCount);
    if (container) {
      container.dataset.uLineCount = String(uLineCount);
      container.dataset.vLineCount = String(vLineCount);
    }

    // Camera policy:
    // Only automatically re-fit camera when navigating to a NEW case.
    // If staying in the same case (e.g. user toggles step, plays timeline, or async response arrives),
    // and user has interacted or camera already placed, DO NOT steal/reset the camera!
    const isNewCase = caseId !== lastCaseIdRef.current;
    if (isNewCase && (faces > 0 || edges > 0)) {
      lastCaseIdRef.current = caseId;
      userInteractedRef.current = false;
      fitOperationAwareView(meshData, toolMeshes, true);
    }
  }, [meshData, toolMeshes, displayOptions, caseId, fitOperationAwareView]);

  // Raycasting for entity click & hover
  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    userInteractedRef.current = true;
    if (!rendererRef.current || !cameraRef.current || !currentMeshGroupRef.current) return;
    const rect = rendererRef.current.domElement.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    const raycaster = new THREE.Raycaster();
    raycaster.params.Line = { threshold: pickThresholdRef.current };
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.current);

    const intersects = raycaster.intersectObjects(currentMeshGroupRef.current.children, true);
    if (intersects.length > 0) {
      // Find first hit entity with an id
      const hit = intersects.find((i) => i.object.userData && i.object.userData.id)?.object;
      if (hit && hit.userData && hit.userData.id) {
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

  const showIso = displayOptions?.showIsoparametrics !== false;
  const hasIso =
    showIso &&
    (Boolean(meshData?.isoparametrics?.length) ||
      Boolean(
        meshData?.faces?.some(
          (f) =>
            f.isoparametrics &&
            (f.isoparametrics.uLines?.length ||
              f.isoparametrics.vLines?.length ||
              f.isoparametrics.unclassifiedLines?.length)
        )
      ));
  const hasBothDirections = Boolean(meshData?.faces?.some((f) =>
    (displayOptions?.showTools !== false || !f.isTool) &&
    (!displayOptions?.isolateModified || f.role !== 'unchanged') &&
    f.isoparametrics?.uLines?.length && f.isoparametrics?.vLines?.length));

  return (
    <div
      className="relative w-full h-full bg-slate-900 overflow-hidden select-none"
      data-testid="viewer-root"
      data-viewer={kernelName === 'GM Kernel' ? 'gm' : 'pk'}
    >
      {/* 3D WebGL Canvas */}
      <div
        ref={containerRef}
        onPointerDown={handlePointerDown}
        data-testid="viewer-canvas-host"
        className="w-full h-full cursor-grab active:cursor-grabbing"
      />

      {/* Top Banner Tag: Kernel & Tessellator info + Isoparametric indicator */}
      <div
        data-testid="viewer-banner"
        className="absolute top-2 left-2 z-10 flex items-center gap-1.5 bg-slate-900/80 backdrop-blur-xs border border-slate-700/60 rounded px-2.5 py-1 text-xs text-slate-200"
      >
        <span
          className={`w-2 h-2 rounded-full ${
            kernelName === 'GM Kernel' ? 'bg-sky-400' : 'bg-emerald-400'
          }`}
        />
        <span className="font-semibold">{kernelName}</span>
        {meshData?.source?.tessellator && (
          <span className="hidden sm:inline text-[10px] text-slate-400 border-l border-slate-700 pl-1.5 font-mono-code">
            {meshData.source.tessellator}
          </span>
        )}
        {hasIso ? (
          <span
            data-testid="viewer-iso-badge"
            className="hidden sm:inline text-[10px] text-sky-300 bg-sky-950/80 border border-sky-800/80 px-1.5 py-0.2 rounded font-mono-code"
            title="曲面具备真实U/V等参线，可直接用于几何曲率与走向观察"
          >
            {hasBothDirections ? 'U/V等参线' : '曲面参数线'}
          </span>
        ) : showIso ? (
          <span
            data-testid="viewer-no-iso-badge"
            className="hidden sm:inline text-[10px] text-slate-400 bg-slate-800/80 border border-slate-700/80 px-1.5 py-0.2 rounded font-mono-code"
            title={meshData?.surfaceParameters?.reason || '此快照暂无等参线数据'}
          >
            {meshData?.surfaceParameters?.status === 'unsupported' ? '等参线暂不支持' : '无等参线'}
          </span>
        ) : null}
      </div>

      {meshData?.displayProvenance && <div data-testid="uv-provenance" className="absolute bottom-2 left-2 right-2 text-[10px] text-slate-200 bg-slate-900/85 rounded border border-slate-700 px-2 py-1 pointer-events-auto">
        <div><span className="text-sky-300">U = 常数 u</span> · <span className="text-purple-300">V = 常数 v</span> · {meshData.displayProvenance.snapshot} / {meshData.displayProvenance.stage}</div>
        <div>{meshData.surfaceParameters?.reason}</div>
        <a className="text-sky-300 underline" href={meshData.displayProvenance.evidencePath} target="_blank" rel="noreferrer" title={`mesh SHA ${meshData.displayProvenance.meshSha256}\nXT SHA ${meshData.displayProvenance.xtSha256}\nreceipt SHA ${meshData.displayProvenance.receiptSha256}`}>
          来源 / 面参数 / trim / 误差证据 · mesh {meshData.displayProvenance.meshSha256.slice(0, 12)}
        </a>
      </div>}

      {/* Loading Overlay */}
      {isLoading && (
        <div
          data-testid="viewer-loading"
          className="absolute inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-20"
        >
          <div className="flex items-center gap-2 text-xs text-sky-400 bg-slate-900 px-3 py-2 rounded border border-slate-700 shadow-md">
            <span className="w-3.5 h-3.5 border-2 border-sky-400 border-t-transparent rounded-full animate-spin" />
            <span>网格数据加载中...</span>
          </div>
        </div>
      )}

      {/* Empty / Error Overlay */}
      {!isLoading && !meshData && (
        <div
          data-testid="viewer-empty"
          className="absolute inset-0 flex flex-col items-center justify-center p-6 text-center z-10 pointer-events-none"
        >
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
