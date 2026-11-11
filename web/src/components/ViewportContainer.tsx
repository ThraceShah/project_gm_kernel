import React, { useState, useCallback, useRef, useEffect } from 'react';
import { MeshViewer, CameraSyncState, FocusTriggerState } from './MeshViewer';
import { EntityLegend } from './EntityLegend';
import { MeshData, StandardView } from '../types/contract';
import { PickedEntity, ROLE_COLORS, EDGE_ROLE_COLORS, GeometryDisplayOptions } from '../utils/geometryBuilder';
import {
  Box,
  SplitSquareVertical,
  Focus,
  Maximize2,
  RotateCcw,
  Compass,
  Spline,
  Eye,
  EyeOff,
  Layers,
  Wrench,
} from 'lucide-react';

export interface ViewportContainerProps {
  caseId?: string;
  operation?: string;
  gmMesh: MeshData | null;
  pkMesh: MeshData | null;
  gmError?: string | null;
  pkError?: string | null;
  isLoading: boolean;
  cameraSync: boolean;
  onCameraSyncChange?: (sync: boolean) => void;
  resetTrigger?: number;
  onResetTrigger?: () => void;
}

export const ViewportContainer: React.FC<ViewportContainerProps> = ({
  caseId,
  gmMesh,
  pkMesh,
  gmError,
  pkError,
  isLoading,
  cameraSync,
  onCameraSyncChange,
  resetTrigger = 0,
}) => {
  const [leftSyncCam, setLeftSyncCam] = useState<CameraSyncState | null>(null);
  const [rightSyncCam, setRightSyncCam] = useState<CameraSyncState | null>(null);
  const [selectedEntity, setSelectedEntity] = useState<PickedEntity | null>(null);
  const [mobileTab, setMobileTab] = useState<'both' | 'gm' | 'pk'>('both');
  const [legendOpen, setLegendOpen] = useState<boolean>(() =>
    typeof window === 'undefined' ? true : window.innerWidth >= 768
  );

  // Focus trigger state
  const [focusTrigger, setFocusTrigger] = useState<FocusTriggerState | null>(null);

  // Display options state
  const [unchangedOpacity, setUnchangedOpacity] = useState<number>(0.25);
  const [isDimmed, setIsDimmed] = useState<boolean>(true); // default semi-transparent unchanged context
  const [showIsoparametrics, setShowIsoparametrics] = useState<boolean>(true);
  const [isolateModified, setIsolateModified] = useState<boolean>(false);
  const [showTools, setShowTools] = useState<boolean>(true);

  // Entity IDs are local to a snapshot; do not carry a pick across snapshots.
  useEffect(() => {
    setSelectedEntity(null);
  }, [caseId, gmMesh, pkMesh]);

  useEffect(() => {
    if (!selectedEntity) return;

    if (!gmMesh && !pkMesh) {
      setSelectedEntity(null);
      return;
    }

    if (isolateModified && selectedEntity.role === 'unchanged') {
      setSelectedEntity(null);
      return;
    }

    const isVisibleInMesh = (mesh: MeshData | null) => {
      if (!mesh) return false;
      if (selectedEntity.type === 'face') {
        return Boolean(mesh.faces?.some((f) => f.id === selectedEntity.id && (!f.isTool || showTools) && (!isolateModified || f.role !== 'unchanged')));
      } else {
        return Boolean(mesh.edges?.some((e) => e.id === selectedEntity.id && (!e.isTool || showTools) && (!isolateModified || e.role !== 'unchanged')));
      }
    };

    if (!isVisibleInMesh(gmMesh) && !isVisibleInMesh(pkMesh)) {
      setSelectedEntity(null);
    }
  }, [caseId, gmMesh, pkMesh, isolateModified, showTools, selectedEntity]);

  // Stable callbacks: camera sync must never recreate the scene lifecycle.
  const cameraSyncRef = useRef<boolean>(cameraSync);
  cameraSyncRef.current = cameraSync;

  const handleGmCamChange = useCallback((state: CameraSyncState) => {
    if (cameraSyncRef.current) {
      setRightSyncCam(state);
    }
  }, []);

  const handlePkCamChange = useCallback((state: CameraSyncState) => {
    if (cameraSyncRef.current) {
      setLeftSyncCam(state);
    }
  }, []);

  // Compute geometry display options
  const displayOptions: GeometryDisplayOptions = React.useMemo(() => {
    return {
      unchangedOpacity: isDimmed ? unchangedOpacity : 1.0,
      isolateModified,
      showTools,
      showIsoparametrics,
    };
  }, [isDimmed, unchangedOpacity, isolateModified, showTools, showIsoparametrics]);

  // View action handlers
  const handleFocusOp = useCallback(() => {
    setFocusTrigger({ id: Date.now(), type: 'operation' });
  }, []);

  const handleFocusFull = useCallback(() => {
    setFocusTrigger({ id: Date.now(), type: 'full' });
  }, []);

  const handleStandardView = useCallback((view: StandardView) => {
    setFocusTrigger({ id: Date.now(), type: view });
  }, []);

  const handleResetView = useCallback(() => {
    setFocusTrigger({ id: Date.now(), type: 'operation' });
  }, []);

  const toggleDimming = useCallback(() => {
    setIsDimmed((prev) => !prev);
  }, []);

  const cycleOpacity = useCallback(() => {
    setUnchangedOpacity((prev) => {
      if (prev <= 0.25) return 0.5;
      if (prev <= 0.5) return 0.75;
      return 0.25;
    });
  }, []);

  const toggleIsoparametrics = useCallback(() => {
    setShowIsoparametrics((prev) => !prev);
  }, []);

  const toggleIsolateModified = useCallback(() => {
    setIsolateModified((prev) => !prev);
  }, []);

  const toggleShowTools = useCallback(() => {
    setShowTools((prev) => !prev);
  }, []);

  const toggleCameraSync = useCallback(() => {
    if (onCameraSyncChange) {
      onCameraSyncChange(!cameraSync);
    }
  }, [cameraSync, onCameraSyncChange]);

  return (
    <div className="relative w-full h-full flex flex-col bg-slate-950 overflow-hidden select-none">
      {/* 3D Viewport Engineering Toolbar */}
      <div
        data-testid="viewport-toolbar"
        className="flex items-center justify-between gap-2 bg-slate-900/90 backdrop-blur-xs border-b border-slate-800 px-3 py-1.5 text-xs text-slate-300 z-20 overflow-x-auto scrollbar-none"
      >
        {/* Left: View Controls */}
        <div className="flex items-center gap-1 shrink-0">
          <span className="text-[11px] font-semibold text-slate-400 hidden sm:inline mr-1">
            视角:
          </span>
          <button
            onClick={handleFocusOp}
            data-testid="btn-focus-op"
            title="聚焦到当前操作受影响区域与最佳观察方向"
            className="flex items-center gap-1 px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-sky-300 hover:text-sky-200 border border-slate-700 transition-colors text-[11px] font-medium"
          >
            <Focus className="w-3.5 h-3.5 text-sky-400" />
            <span>查看修改</span>
          </button>
          <button
            onClick={handleFocusFull}
            data-testid="btn-focus-full"
            title="查看完整模型范围"
            className="flex items-center gap-1 px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white border border-slate-700 transition-colors text-[11px]"
          >
            <Maximize2 className="w-3.5 h-3.5 text-slate-400" />
            <span>全模型</span>
          </button>

          {/* Standard View Select */}
          <div className="flex items-center gap-1 bg-slate-800 border border-slate-700 rounded px-1.5 py-0.5">
            <Compass className="w-3.5 h-3.5 text-slate-400" />
            <select
              data-testid="select-standard-view"
              aria-label="标准视图"
              onChange={(e) => handleStandardView(e.target.value as StandardView)}
              defaultValue=""
              className="bg-transparent text-[11px] text-slate-300 focus:outline-hidden cursor-pointer"
            >
              <option value="" disabled>
                标准视图
              </option>
              <option value="iso" className="bg-slate-800 text-slate-200">
                轴测 (Isometric)
              </option>
              <option value="front" className="bg-slate-800 text-slate-200">
                主视 (Front)
              </option>
              <option value="top" className="bg-slate-800 text-slate-200">
                俯视 (Top)
              </option>
              <option value="right" className="bg-slate-800 text-slate-200">
                右视 (Right)
              </option>
            </select>
          </div>

          <button
            onClick={handleResetView}
            data-testid="btn-reset-view"
            title="重置为初始智能操作视角"
            className="p-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white border border-slate-700 transition-colors"
          >
            <RotateCcw className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Right: Inspection & Shading Modes */}
        <div className="flex items-center gap-1.5 shrink-0">
          <span className="text-[11px] font-semibold text-slate-400 hidden lg:inline mr-1">
            显示模式:
          </span>

          {/* Dim Unchanged Context Toggle */}
          <button
            onClick={toggleDimming}
            data-testid="btn-toggle-dim"
            title={isDimmed ? '当前: 未修改半透明 (点击切换为全实体)' : '当前: 全实体 (点击切换为未修改半透明)'}
            className={`flex items-center gap-1 px-2 py-1 rounded border text-[11px] transition-colors ${
              isDimmed
                ? 'bg-sky-950/70 text-sky-300 border-sky-600/60 font-medium'
                : 'bg-slate-800 text-slate-400 border-slate-700 hover:text-slate-200'
            }`}
          >
            <Layers className="w-3.5 h-3.5" />
            <span>{isDimmed ? '未修改半透明' : '实体不透明'}</span>
          </button>

          {/* Opacity Cycle (when dimmed) */}
          {isDimmed && (
            <button
              onClick={cycleOpacity}
              data-testid="btn-cycle-opacity"
              title={`当前半透明度: ${Math.round((1 - unchangedOpacity) * 100)}% (点击切换)`}
              className="px-1.5 py-1 rounded bg-slate-800 text-sky-400 border border-slate-700 hover:text-white text-[10px] font-mono-code"
            >
              {Math.round(unchangedOpacity * 100)}%
            </button>
          )}

          {/* Isoparametric Curves Toggle */}
          <button
            onClick={toggleIsoparametrics}
            data-testid="btn-toggle-iso"
            title={showIsoparametrics ? '隐藏曲面等参线' : '显示曲面等参线 (U/V)'}
            className={`flex items-center gap-1 px-2 py-1 rounded border text-[11px] transition-colors ${
              showIsoparametrics
                ? 'bg-sky-950/70 text-sky-300 border-sky-600/60 font-medium'
                : 'bg-slate-800 text-slate-400 border-slate-700 hover:text-slate-200'
            }`}
          >
            <Spline className="w-3.5 h-3.5" />
            <span>等参线</span>
          </button>

          {/* Isolate Modified Toggle */}
          <button
            onClick={toggleIsolateModified}
            data-testid="btn-toggle-isolate"
            title={isolateModified ? '还原全部上下文实体' : '仅显示新增/修改及操作元素'}
            className={`flex items-center gap-1 px-2 py-1 rounded border text-[11px] transition-colors ${
              isolateModified
                ? 'bg-amber-950/70 text-amber-300 border-amber-600/60 font-medium'
                : 'bg-slate-800 text-slate-400 border-slate-700 hover:text-slate-200'
            }`}
          >
            {isolateModified ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
            <span>{isolateModified ? '仅看修改' : '完整模型'}</span>
          </button>

          {/* Show Tools Toggle */}
          <button
            onClick={toggleShowTools}
            data-testid="btn-toggle-tools"
            title={showTools ? '隐藏刀具实体' : '显示刀具实体'}
            className={`flex items-center gap-1 px-2 py-1 rounded border text-[11px] transition-colors ${
              showTools
                ? 'bg-slate-800 text-slate-300 border-slate-700'
                : 'bg-slate-900 text-slate-500 border-slate-800 line-through'
            }`}
          >
            <Wrench className="w-3.5 h-3.5 text-amber-400" />
            <span className="hidden sm:inline">刀具</span>
          </button>

          {/* Camera Sync Toggle */}
          {onCameraSyncChange && (
            <button
              onClick={toggleCameraSync}
              data-testid="btn-toggle-sync"
              title={cameraSync ? '点击解耦双侧视角 (独立旋转)' : '点击锁定双侧视角联动'}
              className={`flex items-center gap-1 px-2 py-1 rounded border text-[11px] transition-colors ${
                cameraSync
                  ? 'bg-sky-950/70 text-sky-300 border-sky-600/60'
                  : 'bg-slate-800 text-slate-400 border-slate-700 hover:text-slate-200'
              }`}
            >
              <SplitSquareVertical className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">{cameraSync ? '双侧联动' : '独立视角'}</span>
            </button>
          )}
        </div>
      </div>

      {/* Mobile Tab Switcher */}
      <div className="md:hidden flex items-center justify-between bg-slate-900 border-b border-slate-800 px-3 py-1 text-xs text-slate-300 z-20">
        <span className="font-semibold text-slate-300 text-[11px]">视图切换:</span>
        <div className="flex items-center gap-1 bg-slate-800 p-0.5 rounded">
          <button
            onClick={() => setMobileTab('both')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'both' ? 'bg-sky-600 text-white font-medium' : 'text-slate-400'
            }`}
          >
            双视图
          </button>
          <button
            onClick={() => setMobileTab('gm')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'gm' ? 'bg-sky-600 text-white font-medium' : 'text-slate-400'
            }`}
          >
            GM 内核
          </button>
          <button
            onClick={() => setMobileTab('pk')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'pk' ? 'bg-sky-600 text-white font-medium' : 'text-slate-400'
            }`}
          >
            Parasolid
          </button>
        </div>
      </div>

      {/* Viewports Grid */}
      <div
        className={`flex-1 grid grid-cols-1 md:grid-cols-2 ${
          mobileTab === 'both' ? 'grid-rows-2 md:grid-rows-1' : ''
        } divide-y md:divide-y-0 md:divide-x divide-slate-800 relative min-h-0`}
      >
        {/* Left: GM Kernel Viewport */}
        <div
          className={`relative w-full h-full ${
            mobileTab === 'pk' ? 'hidden md:block' : 'block'
          }`}
        >
          <MeshViewer
            kernelName="GM Kernel"
            caseId={caseId}
            meshData={gmMesh}
            isLoading={isLoading}
            emptyMessage="GM 网格未就绪或未生成"
            errorMessage={gmError}
            cameraSync={cameraSync}
            externalCameraState={leftSyncCam}
            onCameraChange={handleGmCamChange}
            resetTrigger={resetTrigger}
            focusTrigger={focusTrigger}
            displayOptions={displayOptions}
            onSelectEntity={setSelectedEntity}
          />
        </div>

        {/* Right: Parasolid (PK) Viewport */}
        <div
          className={`relative w-full h-full ${
            mobileTab === 'gm' ? 'hidden md:block' : 'block'
          }`}
        >
          <MeshViewer
            kernelName="Parasolid (PK)"
            caseId={caseId}
            meshData={pkMesh}
            isLoading={isLoading}
            emptyMessage="Parasolid 基准网格缺失"
            errorMessage={pkError}
            cameraSync={cameraSync}
            externalCameraState={rightSyncCam}
            onCameraChange={handlePkCamChange}
            resetTrigger={resetTrigger}
            focusTrigger={focusTrigger}
            displayOptions={displayOptions}
            onSelectEntity={setSelectedEntity}
          />
        </div>

        {/* Center Divider Synchronized Indicator */}
        {cameraSync && (
          <div className="hidden md:flex absolute left-1/2 top-4 -translate-x-1/2 z-20 items-center gap-1 bg-slate-900/90 text-sky-400 border border-slate-700/80 px-2.5 py-0.5 rounded-full text-[10px] font-mono-code shadow-md pointer-events-none">
            <SplitSquareVertical className="w-3 h-3 text-sky-400" />
            <span>视角实时同步</span>
          </div>
        )}
      </div>

      {/* Floating Entity Inspector (Top Center) */}
      {selectedEntity && (
        <div
          data-testid="entity-inspector"
          className="absolute top-14 left-1/2 -translate-x-1/2 z-20 bg-slate-900/95 backdrop-blur-xs border border-sky-600/80 rounded-md px-3 py-1.5 shadow-xl flex items-center gap-2 text-xs text-slate-200"
        >
          <Box className="w-3.5 h-3.5 text-sky-400" />
          <span className="font-semibold">
            {selectedEntity.type === 'face' ? '面' : '边'} [
            <span className="font-mono-code text-sky-300">{selectedEntity.id}</span>]
          </span>
          <span className="w-px h-3 bg-slate-700" />
          <span className="text-[11px] text-slate-400">角色:</span>
          <span
            className="px-1.5 py-0.5 rounded text-[11px] font-semibold text-white"
            style={{
              backgroundColor:
                (selectedEntity.type === 'face'
                  ? ROLE_COLORS[selectedEntity.role]?.css
                  : EDGE_ROLE_COLORS[selectedEntity.role]?.css) ?? '#64748b',
            }}
          >
            {ROLE_COLORS[selectedEntity.role]?.label || selectedEntity.role}
          </span>
          {selectedEntity.triangleCount !== undefined && (
            <span className="text-[10px] text-slate-400 font-mono-code">
              ({selectedEntity.triangleCount} 三角片)
            </span>
          )}
          <button
            onClick={() => setSelectedEntity(null)}
            className="ml-2 text-slate-400 hover:text-white text-sm"
          >
            ×
          </button>
        </div>
      )}

      {/* Floating Entity Legend (Bottom Left, collapsible on narrow screens) */}
      <div className="absolute bottom-3 left-3 z-20 max-w-[min(22rem,calc(100%-1.5rem))]">
        <button
          onClick={() => setLegendOpen((v) => !v)}
          data-testid="legend-toggle"
          className="md:hidden mb-1 px-2 py-0.5 rounded text-[11px] bg-slate-900/90 border border-slate-700 text-slate-300 shadow-md"
        >
          {legendOpen ? '收起图例' : '图例'}
        </button>
        <div className={legendOpen ? 'block' : 'hidden md:block'} data-testid="legend-body">
          <EntityLegend />
        </div>
      </div>
    </div>
  );
};
