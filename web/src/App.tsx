import React, { useState, useEffect, useCallback } from 'react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { ViewportContainer } from './components/ViewportContainer';
import { TimelineControl } from './components/TimelineControl';
import { InspectionPanel } from './components/InspectionPanel';
import {
  fetchIndexData,
  fetchManifestData,
  fetchMeshData,
  isFixtureModeForced,
} from './api/dataLoader';
import {
  IndexData,
  ManifestData,
  TestCase,
  MeshData,
  CaseFamily,
  CaseStatus,
} from './types/contract';
import { AlertCircle, RefreshCw, Layers, Database, Sparkles } from 'lucide-react';

export const App: React.FC = () => {
  // Global App States
  const [useFixture, setUseFixture] = useState<boolean>(isFixtureModeForced());
  const [indexData, setIndexData] = useState<IndexData | null>(null);
  const [indexError, setIndexError] = useState<string | null>(null);
  const [isLoadingIndex, setIsLoadingIndex] = useState<boolean>(true);

  // Selected Run & Manifest
  const [selectedRunId, setSelectedRunId] = useState<string>('');
  const [manifestData, setManifestData] = useState<ManifestData | null>(null);
  const [manifestDir, setManifestDir] = useState<string>('');
  const [isLoadingManifest, setIsLoadingManifest] = useState<boolean>(false);

  // Filter States
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [familyFilter, setFamilyFilter] = useState<CaseFamily | 'all'>('all');
  const [statusFilter, setStatusFilter] = useState<CaseStatus | 'all'>('all');

  // Selected Case & Step
  const [selectedCaseId, setSelectedCaseId] = useState<string>('');
  const [currentStepIndex, setCurrentStepIndex] = useState<number>(0);
  const [subState, setSubState] = useState<'before' | 'after'>('before');

  // Meshes
  const [gmMeshes, setGmMeshes] = useState<MeshData | null>(null);
  const [pkMeshes, setPkMeshes] = useState<MeshData | null>(null);
  const [isLoadingMeshes, setIsLoadingMeshes] = useState<boolean>(false);

  // Camera & Viewport Controls
  const [cameraSync, setCameraSync] = useState<boolean>(true);
  const [resetTrigger, setResetTrigger] = useState<number>(0);

  // 1. Load Index
  const loadIndex = useCallback(async (fixtureMode: boolean) => {
    setIsLoadingIndex(true);
    setIndexError(null);
    const result = await fetchIndexData(fixtureMode);
    setIsLoadingIndex(false);

    if (result.error || !result.data) {
      setIndexError(result.error || '无法获取基线数据');
      setIndexData(null);
      return;
    }

    setIndexData(result.data);
    // Select latest run
    const defaultRun =
      result.data.runs.find((r) => r.id === result.data?.latestRunId) ||
      result.data.runs[0];
    if (defaultRun) {
      setSelectedRunId(defaultRun.id);
    }
  }, []);

  // Initial load
  useEffect(() => {
    loadIndex(useFixture);
  }, [loadIndex, useFixture]);

  // 2. Load Manifest when selectedRunId changes
  useEffect(() => {
    if (!indexData || !selectedRunId) return;

    const runSummary = indexData.runs.find((r) => r.id === selectedRunId);
    if (!runSummary) return;

    // manifest path e.g. "runs/<id>/manifest.json"
    // manifest directory is "runs/<id>"
    const dir = runSummary.manifest.replace(/\/[^/]+$/, '');
    setManifestDir(dir);

    setIsLoadingManifest(true);
    fetchManifestData(runSummary.manifest, useFixture).then((res) => {
      setIsLoadingManifest(false);
      if (res.data) {
        setManifestData(res.data);
        if (res.data.cases.length > 0) {
          // Select first case if not selected or current not in list
          setSelectedCaseId((prev) => {
            const exists = res.data?.cases.some((c) => c.id === prev);
            return exists ? prev : res.data!.cases[0].id;
          });
        }
      }
    });
  }, [indexData, selectedRunId, useFixture]);

  // Current active test case
  const currentCase: TestCase | null = React.useMemo(() => {
    if (!manifestData || !manifestData.cases) return null;
    return manifestData.cases.find((c) => c.id === selectedCaseId) || manifestData.cases[0] || null;
  }, [manifestData, selectedCaseId]);

  // Reset step index when case changes
  useEffect(() => {
    setCurrentStepIndex(0);
    setSubState('before');
  }, [selectedCaseId]);

  // 3. Load Meshes when case, step, or subState changes
  useEffect(() => {
    if (!currentCase) {
      setGmMeshes(null);
      setPkMeshes(null);
      return;
    }

    const step = currentCase.steps[currentStepIndex];
    if (!step) {
      setGmMeshes(null);
      setPkMeshes(null);
      return;
    }

    const gmRelPath = step.gm ? step.gm[subState] : null;
    const pkRelPath = step.pk ? step.pk[subState] : null;

    if (!gmRelPath && !pkRelPath) {
      setGmMeshes(null);
      setPkMeshes(null);
      return;
    }

    setIsLoadingMeshes(true);
    Promise.all([
      fetchMeshData(manifestDir, gmRelPath, useFixture),
      fetchMeshData(manifestDir, pkRelPath, useFixture),
    ]).then(([gmRes, pkRes]) => {
      setGmMeshes(gmRes);
      setPkMeshes(pkRes);
      setIsLoadingMeshes(false);
    });
  }, [currentCase, currentStepIndex, subState, manifestDir, useFixture]);

  // Toggle Fixture
  const handleToggleFixture = () => {
    const next = !useFixture;
    setUseFixture(next);
  };

  return (
    <div className="flex flex-col h-screen w-screen bg-slate-100 overflow-hidden font-sans">
      {/* Top Header */}
      <Header
        runs={indexData?.runs || []}
        selectedRunId={selectedRunId}
        onSelectRun={setSelectedRunId}
        runMeta={manifestData?.run || null}
        onRefresh={() => loadIndex(useFixture)}
        isLoading={isLoadingIndex || isLoadingManifest}
        cameraSync={cameraSync}
        onToggleCameraSync={() => setCameraSync(!cameraSync)}
        onResetView={() => setResetTrigger((prev) => prev + 1)}
        isFixture={useFixture}
        onToggleFixture={handleToggleFixture}
      />

      {/* Fixture Alert Notice Banner (if active) */}
      {useFixture && (
        <div className="bg-amber-500/10 border-b border-amber-300/60 px-4 py-1 text-xs text-amber-900 flex items-center justify-between select-none">
          <div className="flex items-center gap-2 font-medium">
            <Sparkles className="w-3.5 h-3.5 text-amber-600" />
            <span>
              已启用 DEV FIXTURE 演练模式：正使用内置标准几何测试例进行交互验证，与实际生产数据完全隔离。
            </span>
          </div>
          <button
            onClick={() => setUseFixture(false)}
            className="text-[11px] underline hover:text-amber-700 font-semibold"
          >
            切换回真实基线数据
          </button>
        </div>
      )}

      {/* Main Body */}
      {indexError && !useFixture ? (
        // Waiting for real data / No real data screen
        <div className="flex-1 flex flex-col items-center justify-center p-6 text-center select-none bg-slate-50">
          <div className="max-w-md w-full bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-4">
            <div className="w-12 h-12 rounded-full bg-slate-100 text-slate-500 flex items-center justify-center mx-auto">
              <Database className="w-6 h-6 text-slate-400" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-800">未检测到生产基线数据</h2>
              <p className="text-xs text-slate-500 mt-1 leading-relaxed">
                当前项目未部署 <code className="font-mono-code bg-slate-100 px-1 py-0.5 rounded text-slate-700">web/data/index.json</code>。
                Mac 数据 Worker (ctx_391ece96108a) 正在并行生成 Parasolid Oracle 真实对比数据。
              </p>
            </div>

            <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-left text-xs text-amber-800 space-y-1">
              <div className="flex items-center gap-1.5 font-semibold">
                <AlertCircle className="w-4 h-4 text-amber-600" />
                <span>界面准备就绪</span>
              </div>
              <p className="text-[11px] leading-normal text-amber-700">
                可先加载演练数据 (DEV FIXTURE) 体验双视角相机同步、真实 Mesh 离散步骤切换、图例与验收项等全量生产特性。
              </p>
            </div>

            <div className="flex items-center justify-center gap-3 pt-2">
              <button
                onClick={() => setUseFixture(true)}
                className="flex items-center gap-1.5 px-4 py-2 bg-sky-600 hover:bg-sky-700 text-white text-xs font-semibold rounded-lg shadow-sm transition-colors"
              >
                <Sparkles className="w-3.5 h-3.5" />
                <span>加载 DEV FIXTURE 演练体验</span>
              </button>

              <button
                onClick={() => loadIndex(false)}
                className="flex items-center gap-1 px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-medium rounded-lg transition-colors border border-slate-200"
              >
                <RefreshCw className="w-3.5 h-3.5" />
                <span>重新检测真实数据</span>
              </button>
            </div>
          </div>
        </div>
      ) : (
        // Standard Workbench Layout
        <div className="flex-1 flex overflow-hidden">
          {/* Left Sidebar: Filter & Case List */}
          <Sidebar
            cases={manifestData?.cases || []}
            selectedCaseId={selectedCaseId}
            onSelectCase={setSelectedCaseId}
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
            familyFilter={familyFilter}
            onFamilyFilterChange={setFamilyFilter}
            statusFilter={statusFilter}
            onStatusFilterChange={setStatusFilter}
          />

          {/* Right Area: Dual Viewport + Timeline + Inspection Panel */}
          <main className="flex-1 flex flex-col overflow-hidden bg-slate-900">
            {currentCase ? (
              <>
                {/* 3D Dual Viewports */}
                <div className="flex-1 relative overflow-hidden">
                  <ViewportContainer
                    gmMesh={gmMeshes}
                    pkMesh={pkMeshes}
                    isLoading={isLoadingMeshes}
                    cameraSync={cameraSync}
                    resetTrigger={resetTrigger}
                  />
                </div>

                {/* Timeline & Discrete Stepper */}
                <TimelineControl
                  steps={currentCase.steps || []}
                  currentStepIndex={currentStepIndex}
                  onSelectStepIndex={setCurrentStepIndex}
                  subState={subState}
                  onSelectSubState={setSubState}
                />

                {/* Bottom Inspection Panel */}
                <InspectionPanel testCase={currentCase} manifestDir={manifestDir} />
              </>
            ) : (
              <div className="flex-1 flex items-center justify-center text-slate-400 text-xs">
                <Layers className="w-8 h-8 opacity-40 mb-2" />
                <p>请在左侧列表选择一个测试用例查看对比详情</p>
              </div>
            )}
          </main>
        </div>
      )}
    </div>
  );
};
export default App;
