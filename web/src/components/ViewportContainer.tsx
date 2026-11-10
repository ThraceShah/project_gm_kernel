import React, { useState, useCallback, useRef } from 'react';
import { MeshViewer, CameraSyncState } from './MeshViewer';
import { EntityLegend } from './EntityLegend';
import { MeshData } from '../types/contract';
import { PickedEntity, ROLE_COLORS, EDGE_ROLE_COLORS } from '../utils/geometryBuilder';
import { Box, SplitSquareVertical } from 'lucide-react';

interface ViewportContainerProps {
  gmMesh: MeshData | null;
  pkMesh: MeshData | null;
  gmError?: string | null;
  pkError?: string | null;
  isLoading: boolean;
  cameraSync: boolean;
  resetTrigger: number;
}

export const ViewportContainer: React.FC<ViewportContainerProps> = ({
  gmMesh,
  pkMesh,
  gmError,
  pkError,
  isLoading,
  cameraSync,
  resetTrigger,
}) => {
  const [leftSyncCam, setLeftSyncCam] = useState<CameraSyncState | null>(null);
  const [rightSyncCam, setRightSyncCam] = useState<CameraSyncState | null>(null);
  const [selectedEntity, setSelectedEntity] = useState<PickedEntity | null>(null);
  const [mobileTab, setMobileTab] = useState<'both' | 'gm' | 'pk'>('both');
  const [legendOpen, setLegendOpen] = useState<boolean>(() =>
    typeof window === 'undefined' ? true : window.innerWidth >= 768
  );

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

  return (
    <div className="relative w-full h-full flex flex-col bg-slate-950 overflow-hidden select-none">
      {/* Mobile Tab Switcher */}
      <div className="md:hidden flex items-center justify-between bg-slate-900 border-b border-slate-800 px-3 py-1.5 text-xs text-slate-300 z-20">
        <span className="font-semibold text-slate-200">视角选择:</span>
        <div className="flex items-center gap-1 bg-slate-800 p-0.5 rounded">
          <button
            onClick={() => setMobileTab('both')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'both' ? 'bg-sky-600 text-white' : 'text-slate-400'
            }`}
          >
            双视图
          </button>
          <button
            onClick={() => setMobileTab('gm')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'gm' ? 'bg-sky-600 text-white' : 'text-slate-400'
            }`}
          >
            GM 内核
          </button>
          <button
            onClick={() => setMobileTab('pk')}
            className={`px-2 py-0.5 rounded text-[11px] ${
              mobileTab === 'pk' ? 'bg-sky-600 text-white' : 'text-slate-400'
            }`}
          >
            Parasolid
          </button>
        </div>
      </div>

      {/* Viewports Grid */}
      <div className="flex-1 grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-slate-800 relative">
        {/* Left: GM Kernel Viewport */}
        <div
          className={`relative w-full h-full ${
            mobileTab === 'pk' ? 'hidden md:block' : 'block'
          }`}
        >
          <MeshViewer
            kernelName="GM Kernel"
            meshData={gmMesh}
            isLoading={isLoading}
            emptyMessage="GM 网格未就绪或未生成"
            errorMessage={gmError}
            cameraSync={cameraSync}
            externalCameraState={leftSyncCam}
            onCameraChange={handleGmCamChange}
            resetTrigger={resetTrigger}
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
            meshData={pkMesh}
            isLoading={isLoading}
            emptyMessage="Parasolid 基准网格缺失"
            errorMessage={pkError}
            cameraSync={cameraSync}
            externalCameraState={rightSyncCam}
            onCameraChange={handlePkCamChange}
            resetTrigger={resetTrigger}
            onSelectEntity={setSelectedEntity}
          />
        </div>

        {/* Center Divider Synchronized Indicator */}
        {cameraSync && (
          <div className="hidden md:flex absolute left-1/2 top-4 -translate-x-1/2 z-20 items-center gap-1 bg-slate-900/90 text-sky-400 border border-slate-700/80 px-2 py-0.5 rounded-full text-[10px] font-mono-code shadow-md pointer-events-none">
            <SplitSquareVertical className="w-3 h-3 text-sky-400" />
            <span>视角实时同步</span>
          </div>
        )}
      </div>

      {/* Floating Entity Inspector (Top Center) */}
      {selectedEntity && (
        <div
          data-testid="entity-inspector"
          className="absolute top-12 left-1/2 -translate-x-1/2 z-20 bg-slate-900/95 backdrop-blur-xs border border-sky-600/80 rounded-md px-3 py-1.5 shadow-xl flex items-center gap-2 text-xs text-slate-200"
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
      <div className="absolute bottom-3 left-3 z-20 max-w-[min(20rem,calc(100%-1.5rem))]">
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
