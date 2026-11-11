import React from 'react';
import {
  RefreshCw,
  Video,
  VideoOff,
  RotateCcw,
  AlertTriangle,
  Layers,
  ListTree,
  Info,
} from 'lucide-react';
import { RunSummary, RunMetadata } from '../types/contract';
import { formatDateTime } from '../utils/formatters';

interface HeaderProps {
  runs: RunSummary[];
  selectedRunId: string;
  onSelectRun: (runId: string) => void;
  latestRunId?: string;
  runMeta: RunMetadata | null;
  onRefresh: () => void;
  isLoading: boolean;
  cameraSync: boolean;
  onToggleCameraSync: () => void;
  onResetView: () => void;
  isFixture: boolean;
  onToggleFixture: () => void;
  onOpenSidebar: () => void;
  onOpenData: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  runs,
  selectedRunId,
  onSelectRun,
  latestRunId,
  runMeta,
  onRefresh,
  isLoading,
  cameraSync,
  onToggleCameraSync,
  onResetView,
  isFixture,
  onToggleFixture,
  onOpenSidebar,
  onOpenData,
}) => {
  return (
    <header
      data-testid="app-header"
      className="bg-white border-b border-slate-200 px-3 sm:px-4 py-2 flex flex-wrap items-center justify-between gap-x-3 gap-y-2 shadow-sm z-30 select-none"
    >
      {/* Left: Title & Run Selector */}
      <div className="flex items-center gap-3 min-w-0">
        <div className="flex items-center gap-2 min-w-0">
          <div className="w-8 h-8 rounded bg-gradient-to-br from-sky-600 to-indigo-700 text-white flex items-center justify-center font-bold shadow-sm flex-shrink-0">
            <Layers className="w-4 h-4" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2 min-w-0">
              <h1 className="font-bold text-slate-800 text-sm sm:text-base leading-tight truncate">
                GM Kernel vs Parasolid 对比工作台
              </h1>
              <span className="hidden sm:inline-block text-xs px-2 py-0.5 rounded font-medium bg-slate-100 text-slate-600 border border-slate-200 whitespace-nowrap">
                PROD WORKBENCH
              </span>
            </div>
            <div className="text-xs text-slate-500 flex items-center gap-3 min-w-0">
              <span className="truncate">几何建模内核对比与工程指标追踪</span>
              {runMeta?.pkVersion && (
                <span className="hidden sm:inline-block font-mono-code text-[11px] text-sky-700 bg-sky-50 px-1 rounded">
                  Oracle: {runMeta.pkVersion}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Run Selector */}
        {runs.length > 0 && (
          <div className="flex items-center gap-1.5 ml-2 border-l border-slate-200 pl-3 min-w-0">
            <label className="text-xs text-slate-500 font-medium whitespace-nowrap">基线 Run:</label>
            <select
              value={selectedRunId}
              onChange={(e) => onSelectRun(e.target.value)}
              data-testid="run-select"
              className="text-xs bg-slate-50 border border-slate-300 rounded px-2 py-1 text-slate-700 hover:border-slate-400 focus:outline-none focus:ring-1 focus:ring-sky-500 font-mono-code w-[140px] sm:w-[200px] min-w-0 truncate"
            >
              {runs.map((r) => (
                <option key={r.id} value={r.id}>
                  已发布基线 · {r.id} ({r.commit.slice(0, 7)})
                  {latestRunId && r.id !== latestRunId ? ' ·历史' : ''}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* The UI revision and captured result revision are independent. */}
      {runMeta && (
        <button
          onClick={onOpenData}
          className="flex items-center gap-1 text-[11px] px-2 py-1 rounded border border-slate-200 text-slate-600 hover:bg-slate-100 max-w-full"
          title={`已发布数据 ${runMeta.id}；采集时间 ${runMeta.generatedAt}；代码 ${runMeta.commit}`}
          data-testid="header-meta-toggle"
        >
          <Info className="w-3.5 h-3.5 text-slate-400" />
          <span className="truncate">数据版本 {runMeta.commit.slice(0, 7)} · 采集 {formatDateTime(runMeta.generatedAt)} ↗</span>
        </button>
      )}

      {/* Right: Controls & Actions */}
      <div className="flex items-center gap-2 flex-wrap">
        {/* Mobile: open case list drawer */}
        <button
          onClick={onOpenSidebar}
          data-testid="open-sidebar"
          className="md:hidden flex items-center gap-1 text-xs px-2.5 py-1 rounded font-medium border bg-slate-100 text-slate-700 border-slate-200 hover:bg-slate-200"
        >
          <ListTree className="w-3.5 h-3.5 text-slate-500" />
          <span>用例列表</span>
        </button>

        {/* Fixture Mode Toggle: Dev only, forbidden in prod */}
        {import.meta.env.DEV && (
          <button
            onClick={onToggleFixture}
            title={isFixture ? '当前为 DEV FIXTURE 演练模式，点击尝试切换为真实数据' : '点击切换为 DEV FIXTURE 演练数据模式'}
            className={`flex items-center gap-1 text-xs px-2.5 py-1 rounded font-medium transition-colors border ${
              isFixture
                ? 'bg-amber-100 text-amber-900 border-amber-300 hover:bg-amber-200'
                : 'bg-slate-100 text-slate-600 border-slate-200 hover:bg-slate-200'
            }`}
          >
            {isFixture ? (
              <>
                <AlertTriangle className="w-3.5 h-3.5 text-amber-600" />
                <span>演练模式 (DEV FIXTURE)</span>
              </>
            ) : (
              <span>真实基准数据模式</span>
            )}
          </button>
        )}

        {/* Camera Sync Toggle */}
        <button
          onClick={onToggleCameraSync}
          title={cameraSync ? '左右视角已联动 (点击解除)' : '左右视角独立 (点击锁定联动)'}
          className={`flex items-center gap-1 text-xs px-2.5 py-1 rounded border font-medium transition-colors ${
            cameraSync
              ? 'bg-sky-50 text-sky-700 border-sky-300 hover:bg-sky-100'
              : 'bg-slate-100 text-slate-600 border-slate-200 hover:bg-slate-200'
          }`}
        >
          {cameraSync ? <Video className="w-3.5 h-3.5 text-sky-600" /> : <VideoOff className="w-3.5 h-3.5 text-slate-400" />}
          <span>{cameraSync ? '相机联动' : '相机独立'}</span>
        </button>

        {/* Reset Camera View */}
        <button
          onClick={onResetView}
          title="将左右相机视角复位并适配网格尺寸"
          className="flex items-center gap-1 text-xs px-2 py-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 border border-slate-200 transition-colors"
        >
          <RotateCcw className="w-3.5 h-3.5 text-slate-500" />
          <span>复位视角</span>
        </button>

        {/* Reload / Refresh Button */}
        <button
          onClick={onRefresh}
          disabled={isLoading}
          title="无缓存重新拉取数据"
          className="flex items-center gap-1 text-xs px-2.5 py-1 rounded bg-sky-600 hover:bg-sky-700 text-white font-medium transition-colors disabled:opacity-50 shadow-sm"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          <span>刷新</span>
        </button>
      </div>
    </header>
  );
};
