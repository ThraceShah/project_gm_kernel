import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { ViewportContainer } from './components/ViewportContainer';
import { TimelineControl } from './components/TimelineControl';
import { InspectionPanel } from './components/InspectionPanel';
import { comparisonLabel, useComparisonEvidence } from './components/ComparisonView';
import {
  fetchIndexData,
  fetchOverviewData,
  fetchManifestData,
  fetchStepMeshes,
  isFixtureModeForced,
} from './api/dataLoader';
import {
  IndexData,
  ManifestData,
  TestCase,
  MeshData,
  CaseFamily,
  CaseStatus,
  ProjectOverview,
} from './types/contract';
import { formatStatus } from './utils/formatters';
import { AlertCircle, RefreshCw, Layers, Database, Sparkles } from 'lucide-react';

export const App: React.FC = () => {
  // Global App States
  const [useFixture, setUseFixture] = useState<boolean>(isFixtureModeForced());
  const [indexData, setIndexData] = useState<IndexData | null>(null);
  const [indexError, setIndexError] = useState<string | null>(null);
  const [isLoadingIndex, setIsLoadingIndex] = useState<boolean>(true);
  const [refreshCount, setRefreshCount] = useState<number>(0);

  // Selected Run & Manifest
  const [selectedRunId, setSelectedRunId] = useState<string>('');
  const [manifestData, setManifestData] = useState<ManifestData | null>(null);
  const [manifestError, setManifestError] = useState<string | null>(null);
  const [manifestDir, setManifestDir] = useState<string>('');
  const [isLoadingManifest, setIsLoadingManifest] = useState<boolean>(false);

  // Filter States
  const [overviewData, setOverviewData] = useState<ProjectOverview | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [familyFilter, setFamilyFilter] = useState<CaseFamily | 'all'>('all');
  const [statusFilter, setStatusFilter] = useState<CaseStatus | 'all'>('all');
  const [suiteFilter, setSuiteFilter] = useState<string>('all');
  const [improvedOnly, setImprovedOnly] = useState<boolean>(false);

  // Selected Case & Step
  const [selectedCaseId, setSelectedCaseId] = useState<string>('');
  const [currentStepIndex, setCurrentStepIndex] = useState<number>(0);
  const pendingTarget = useRef<{ runId?: string; caseId?: string; stepId?: string } | null>(null);
  const [subState, setSubState] = useState<'before' | 'after'>('before');

  // Meshes & Mesh Errors
  const [gmMeshes, setGmMeshes] = useState<MeshData | null>(null);
  const [pkMeshes, setPkMeshes] = useState<MeshData | null>(null);
  const [loadedMeshKey, setLoadedMeshKey] = useState<string>('');
  const [gmError, setGmError] = useState<string | null>(null);
  const [pkError, setPkError] = useState<string | null>(null);
  const [isLoadingMeshes, setIsLoadingMeshes] = useState<boolean>(false);

  // Camera & Viewport Controls
  const [cameraSync, setCameraSync] = useState<boolean>(true);
  const [resetTrigger, setResetTrigger] = useState<number>(0);

  // Version / historical source view. A catalog case can point at another
  // run's exact case+step (accepted event artifact); selecting it renders that
  // source without leaving the 463-case workbench.
  const [altView, setAltView] = useState<{ dir: string; runId: string; caseData: TestCase; stepId: string; note: string } | null>(null);
  const [altError, setAltError] = useState<string | null>(null);
  const [altLoading, setAltLoading] = useState<boolean>(false);

  // Mobile sidebar drawer
  const [sidebarOpen, setSidebarOpen] = useState<boolean>(false);
  const [inspectionRequest, setInspectionRequest] = useState<{ id: number; tab: 'checks' | 'data' } | null>(null);

  // Request Guard & Cancellation Refs
  const manifestReqRef = useRef<number>(0);
  const manifestAbortRef = useRef<AbortController | null>(null);
  const meshReqRef = useRef<number>(0);
  const meshAbortRef = useRef<AbortController | null>(null);

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

  // 2. Load Manifest when selectedRunId changes or refresh is requested
  useEffect(() => {
    let cancelled = false;
    fetchOverviewData().then((r) => { if (!cancelled) { setOverviewData(r.data); } });
    return () => { cancelled = true; };
  }, [refreshCount]);

  useEffect(() => {
    if (!indexData || !selectedRunId) return;

    const runSummary = indexData.runs.find((r) => r.id === selectedRunId);
    if (!runSummary) return;

    // manifest path e.g. "runs/<id>/manifest.json"
    // manifest directory is "runs/<id>"
    const dir = runSummary.manifest.replace(/\/[^/]+$/, '');
    setManifestDir(dir);

    // Cancel pending manifest and mesh fetches
    manifestAbortRef.current?.abort();
    meshAbortRef.current?.abort();

    const currentReqId = ++manifestReqRef.current;
    const abortController = new AbortController();
    manifestAbortRef.current = abortController;

    // Immediately clear old manifest and geometry state
    setIsLoadingManifest(true);
    setManifestData(null);
    setManifestError(null);
    setGmMeshes(null);
    setPkMeshes(null);
    setGmError(null);
    setPkError(null);
    setCurrentStepIndex(0);
    setSubState('before');

    fetchManifestData(runSummary.manifest, useFixture, abortController.signal).then((res) => {
      // Discard stale request
      if (manifestReqRef.current !== currentReqId) return;

      setIsLoadingManifest(false);
      if (res.error || !res.data) {
        setManifestError(res.error || `无法加载清单 ${runSummary.manifest}`);
        setManifestData(null);
        setSelectedCaseId('');
        return;
      }

      setManifestData(res.data);
      if (res.data.cases.length > 0) {
        setSelectedCaseId((prev) => {
          const exists = res.data?.cases.some((c) => c.id === prev);
          if (exists && prev) return prev;
          // Prefer a passed or diagnostic case with meshes
          const preferred = res.data!.cases.find(
            (c) => (c.status === 'pass' || c.status === 'diagnostic') && c.steps?.length > 0
          );
          return preferred ? preferred.id : res.data!.cases[0].id;
        });
      }
    });

    return () => {
      abortController.abort();
    };
  }, [indexData, selectedRunId, useFixture, refreshCount]);

  // Current active test case
  const currentCase: TestCase | null = React.useMemo(() => {
    if (!manifestData || !manifestData.cases) return null;
    return manifestData.cases.find((c) => c.id === selectedCaseId) || manifestData.cases[0] || null;
  }, [manifestData, selectedCaseId]);

  // Current-round improvement record set: cases carrying at least one history
  // entry in the fixed 463 catalog. Badge/filter only; it never shrinks the list.
  const improvedCaseIds = React.useMemo(() => {
    const set = new Set<string>();
    for (const item of overviewData?.catalog ?? []) {
      if (Array.isArray(item.history) && item.history.length > 0) set.add(item.id);
    }
    return set;
  }, [overviewData]);

  // Display-link landing (R4): consume the pending real step id only after the
  // target run/case/manifest is actually loaded; a plain case switch resets to
  // the first step (documented policy), display links carry their exact step.
  useEffect(() => {
    const p = pendingTarget.current;
    if (p) {
      // consume only once the TARGET run/case/manifest is actually loaded
      if (currentCase && currentCase.steps.length > 0 && selectedRunId === p.runId && currentCase.id === p.caseId) {
        const idx = currentCase.steps.findIndex((st) => st.id === p.stepId);
        pendingTarget.current = null;
        setCurrentStepIndex(idx >= 0 ? idx : 0);
        setSubState('before');
      }
      return; // keep waiting for the target run; never clear early
    }
    setCurrentStepIndex(0);
    setSubState('before');
  }, [currentCase, selectedRunId]);

  const stepsCount = currentCase?.steps?.length ?? 0;
  const safeStepIndex = stepsCount > 0 ? Math.min(currentStepIndex, stepsCount - 1) : 0;

  // Effective view source: the case's own current capture, or an explicit
  // accepted version/source selected from the catalog display link.
  const viewCase: TestCase | null = altView ? altView.caseData : currentCase;
  const viewDir = altView ? altView.dir : manifestDir;
  const viewRunId = altView ? altView.runId : selectedRunId;
  const viewStepsCount = viewCase?.steps?.length ?? 0;
  const viewStepIndex = altView
    ? Math.max(0, viewCase?.steps.findIndex((st) => st.id === altView.stepId) ?? 0)
    : safeStepIndex;
  const comparison = useComparisonEvidence(viewCase, viewDir);
  const catalogDisplayLink = React.useMemo(() => {
    const item = overviewData?.catalog?.find((c) => c.id === currentCase?.id);
    return item?.display ?? null;
  }, [overviewData, currentCase?.id]);
  // A plain case switch drops any alternate source.
  useEffect(() => { setAltView(null); setAltError(null); }, [currentCase?.id]);

  const openAlternateSource = useCallback(async () => {
    if (!catalogDisplayLink) return;
    if (altView) { setAltView(null); setAltError(null); return; }
    const link = catalogDisplayLink;
    const run = indexData?.runs.find((r) => r.id === link.runId);
    if (!run) { setAltError(`版本来源 ${link.runId} 不在当前数据索引中`); return; }
    setAltLoading(true);
    setAltError(null);
    const dir = run.manifest.replace(/\/[^/]+$/, '');
    const res = await fetchManifestData(run.manifest, useFixture);
    setAltLoading(false);
    if (res.error || !res.data) { setAltError(res.error || `无法加载版本来源 ${run.manifest}`); return; }
    const caseData = res.data.cases.find((c) => c.id === link.caseId);
    if (!caseData) { setAltError(`版本来源 ${link.runId} 缺少用例 ${link.caseId}`); return; }
    setAltView({ dir, runId: link.runId, caseData, stepId: link.stepId, note: link.note });
    setSubState('before');
  }, [catalogDisplayLink, altView, indexData, useFixture]);

  // An async result belongs to the selection that requested it. Gate at render
  // time, before child effects can fit a previous case's geometry.
  const meshKey = JSON.stringify([useFixture, viewRunId, viewDir, viewCase?.id, viewStepIndex, subState]);
  const cameraContext = JSON.stringify([useFixture, viewDir, viewCase?.id, altView?.stepId ?? '']);
  useEffect(() => { setInspectionRequest(null); }, [cameraContext]);
  useEffect(() => {
    if (stepsCount > 0 && currentStepIndex >= stepsCount) {
      setCurrentStepIndex(Math.max(0, stepsCount - 1));
    }
  }, [stepsCount, currentStepIndex]);

  // 3. Load Meshes when case, step, or subState changes
  useEffect(() => {
    // Cancel pending mesh fetch
    meshAbortRef.current?.abort();

    if (!viewCase) {
      setGmMeshes(null);
      setPkMeshes(null);
      setGmError(null);
      setPkError(null);
      return;
    }

    const step = viewCase.steps[viewStepIndex];
    if (!step) {
      setGmMeshes(null);
      setPkMeshes(null);
      setGmError(null);
      setPkError(null);
      return;
    }

    const currentReqId = ++meshReqRef.current;
    const abortController = new AbortController();
    meshAbortRef.current = abortController;

    // Clear old geometry immediately on switch so no stale mesh remains
    setGmMeshes(null);
    setPkMeshes(null);
    setGmError(null);
    setPkError(null);

    setIsLoadingMeshes(true);
    const selection = { runId: viewRunId, caseId: viewCase.id, stepId: step.id, stage: subState };
    Promise.all([
      fetchStepMeshes(viewDir, step.gm, { ...selection, kernel: 'GM' }, useFixture, abortController.signal),
      fetchStepMeshes(viewDir, step.pk, { ...selection, kernel: 'PK' }, useFixture, abortController.signal),
    ])
      .then(([gmRes, pkRes]) => {
        // Discard stale response
        if (meshReqRef.current !== currentReqId) return;

        setGmMeshes(gmRes.data);
        setPkMeshes(pkRes.data);
        setLoadedMeshKey(meshKey);
        setGmError(gmRes.error);
        setPkError(pkRes.error);
        setIsLoadingMeshes(false);
      })
      .catch((err) => {
        if (meshReqRef.current !== currentReqId) return;
        setIsLoadingMeshes(false);
        if (err?.name !== 'AbortError') {
          console.error('[App] Mesh fetch error:', err);
        }
      });

    return () => {
      abortController.abort();
    };
  }, [viewCase, viewStepIndex, subState, viewRunId, viewDir, useFixture, meshKey]);

  // Refresh handler: reload index and manifest
  const handleRefresh = () => {
    setRefreshCount((prev) => prev + 1);
    loadIndex(useFixture);
  };

  // Toggle Fixture (dev only)
  const handleToggleFixture = () => {
    if (!import.meta.env.DEV) return;
    const next = !useFixture;
    setUseFixture(next);
  };

  return (
    <div className="flex flex-col h-screen w-full bg-slate-100 overflow-hidden font-sans">
      {/* Top Header */}
      <Header
        runs={indexData?.runs || []}
        selectedRunId={selectedRunId}
        onSelectRun={setSelectedRunId}
        latestRunId={indexData?.latestRunId || ''}
        runMeta={manifestData?.run || null}
        onRefresh={handleRefresh}
        isLoading={isLoadingIndex || isLoadingManifest}
        cameraSync={cameraSync}
        onToggleCameraSync={() => setCameraSync(!cameraSync)}
        onResetView={() => setResetTrigger((prev) => prev + 1)}
        isFixture={useFixture}
        onToggleFixture={handleToggleFixture}
        onOpenSidebar={() => setSidebarOpen(true)}
        onOpenData={() => setInspectionRequest({ id: Date.now(), tab: 'data' })}
      />

      {/* Embedded Overview Progress Bar */}
      {overviewData && (
        <div data-testid="overview-summary-bar" className="bg-slate-900 border-b border-slate-800 px-4 py-1.5 text-xs text-slate-300 flex items-center justify-between gap-2 flex-wrap select-none z-20">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-slate-100">{overviewData.currentVersion?.label || '项目整体进度'}</span>
            <span className="text-[11px] text-slate-400">固定目录 {overviewData.catalog?.length ?? 463} 个用例</span>
          </div>
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="px-1.5 py-0.5 rounded bg-emerald-950/80 border border-emerald-700/60 text-[10px] text-emerald-300">功能通过 {overviewData.summary?.['function:pass'] ?? 0}</span>
            <span className="px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700 text-[10px] text-slate-400">功能未验证 {overviewData.summary?.['function:unverified'] ?? 0}</span>
            <span className="px-1.5 py-0.5 rounded bg-amber-950/80 border border-amber-700/60 text-[10px] text-amber-300">物性未决 {overviewData.summary?.['mass:unresolved'] ?? 0}</span>
            <span className="px-1.5 py-0.5 rounded bg-sky-950/80 border border-sky-700/60 text-[10px] text-sky-300">查询通过 {overviewData.summary?.['query:pass'] ?? 0}</span>
            <span className="px-1.5 py-0.5 rounded bg-indigo-950/80 border border-indigo-700/60 text-[10px] text-indigo-300">性能观测 {overviewData.summary?.['performance:observed'] ?? 0}</span>
            <span className="px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700 text-[10px] text-slate-300">本轮改进记录 {improvedCaseIds.size}</span>
          </div>
        </div>
      )}

      {new URLSearchParams(window.location.search).get('display') === 'real-uv' && <div className="hidden sm:flex bg-slate-800 border-b border-slate-700 px-4 py-1 text-xs text-slate-200 flex-wrap gap-x-3 gap-y-1" data-testid="display-source-bar">
          <strong>真实 U/V · 独立显示源</strong>
          <span>r2-curved-receipt-eecab6d · sample-0 · operation-1；无新增几何验收或性能结果</span>
          <a href="?" className="text-sky-300 underline">返回 live17 基线</a>
      </div>}

      {/* Fixture Alert Notice Banner (if active in dev) */}
      {import.meta.env.DEV && useFixture && (
        <div className="bg-amber-500/10 border-b border-amber-300/60 px-4 py-1 text-xs text-amber-900 flex items-center justify-between select-none">
          <div className="flex items-center gap-2 font-medium">
            <Sparkles className="w-3.5 h-3.5 text-amber-600" />
            <span>
              已启用 DEV FIXTURE 演练模式：正使用内置标准几何测试例进行交互验证，与实际生产数据完全隔离。
            </span>
          </div>
          <button
            onClick={() => setUseFixture(false)}
            className="text-[11px] underline hover:text-amber-700 font-semibold cursor-pointer"
          >
            切换回真实基线数据
          </button>
        </div>
      )}

      {/* Main Body */}
      {indexError && !useFixture ? (
        // No real data / index fetch failure screen
        <div className="flex-1 flex flex-col items-center justify-center p-6 text-center select-none bg-slate-50">
          <div className="max-w-md w-full bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-4">
            <div className="w-12 h-12 rounded-full bg-slate-100 text-slate-500 flex items-center justify-center mx-auto">
              <Database className="w-6 h-6 text-slate-400" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-800">未检测到生产基线数据</h2>
              <p className="text-xs text-slate-500 mt-1 leading-relaxed">
                当前无法加载 <code className="font-mono-code bg-slate-100 px-1 py-0.5 rounded text-slate-700">web/data/index.json</code>。
                {indexError && <span className="block mt-1 text-rose-600">错误: {indexError}</span>}
              </p>
            </div>

            {import.meta.env.DEV && (
              <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-left text-xs text-amber-800 space-y-1">
                <div className="flex items-center gap-1.5 font-semibold">
                  <AlertCircle className="w-4 h-4 text-amber-600" />
                  <span>本地开发演练模式</span>
                </div>
                <p className="text-[11px] leading-normal text-amber-700">
                  可加载 DEV FIXTURE 体验双视角相机同步、真实 Mesh 离散步骤切换、图例与验收项等特性。
                </p>
              </div>
            )}

            <div className="flex items-center justify-center gap-3 pt-2">
              {import.meta.env.DEV && (
                <button
                  onClick={() => setUseFixture(true)}
                  className="flex items-center gap-1.5 px-4 py-2 bg-sky-600 hover:bg-sky-700 text-white text-xs font-semibold rounded-lg shadow-sm transition-colors cursor-pointer"
                >
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>加载 DEV FIXTURE 演练体验</span>
                </button>
              )}

              <button
                onClick={() => loadIndex(false)}
                className="flex items-center gap-1 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-medium rounded-lg transition-colors border border-slate-200 cursor-pointer"
              >
                <RefreshCw className="w-3.5 h-3.5" />
                <span>重新检测数据</span>
              </button>
            </div>
          </div>
        </div>
      ) : (
        // Standard Workbench Layout
        <div className="flex-1 flex overflow-hidden">
          {/* Mobile drawer backdrop */}
          {sidebarOpen && (
            <div
              className="fixed inset-0 bg-slate-900/40 z-50 md:hidden"
              onClick={() => setSidebarOpen(false)}
              data-testid="sidebar-backdrop"
            />
          )}

          {/* Left Sidebar: Filter & Case List (drawer on narrow screens) */}
          <div
            data-testid="sidebar-shell"
            className={`fixed md:static inset-y-0 left-0 z-[60] w-80 max-w-[85vw] overflow-hidden transform transition-transform duration-200 ${
              sidebarOpen ? 'translate-x-0' : '-translate-x-full md:translate-x-0'
            }`}
          >
            <Sidebar
              cases={manifestData?.cases || []}
              selectedCaseId={selectedCaseId}
              onSelectCase={(caseId) => {
                setSelectedCaseId(caseId);
                setSidebarOpen(false);
              }}
              searchQuery={searchQuery}
              onSearchChange={setSearchQuery}
              familyFilter={familyFilter}
              onFamilyFilterChange={setFamilyFilter}
              statusFilter={statusFilter}
              onStatusFilterChange={setStatusFilter}
              suiteFilter={suiteFilter}
              onSuiteFilterChange={setSuiteFilter}
              improvedCaseIds={improvedCaseIds}
              improvedOnly={improvedOnly}
              onImprovedOnlyChange={setImprovedOnly}
            />
          </div>

          {/* Right Area: Dual Viewport + Timeline + Inspection Panel */}
          <main className="relative min-w-0 flex-1 flex flex-col overflow-hidden bg-slate-900">
            {manifestError ? (
              <div className="flex-1 flex flex-col items-center justify-center p-6 text-center text-slate-300">
                <div className="bg-slate-800 border border-slate-700 rounded-lg p-6 max-w-sm space-y-3 shadow-lg">
                  <AlertCircle className="w-8 h-8 text-rose-400 mx-auto" />
                  <h3 className="text-sm font-bold text-slate-100">运行清单加载失败</h3>
                  <p className="text-xs text-slate-400 leading-relaxed font-mono-code">{manifestError}</p>
                  <button
                    onClick={handleRefresh}
                    className="px-3 py-1.5 bg-sky-600 hover:bg-sky-700 text-white text-xs font-medium rounded transition-colors"
                  >
                    重试加载
                  </button>
                </div>
              </div>
            ) : viewCase ? (
              <>
                {/* Active Case Top Bar */}
                <div
                  data-testid="case-topbar"
                  className="bg-slate-900 border-b border-slate-800 px-4 py-2 flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-xs text-slate-300 z-10 flex-shrink-0"
                >
                  <div className="flex items-center gap-2 min-w-0">
                    <span className="font-semibold text-slate-100 truncate text-sm">
                      {viewCase.title}
                    </span>
                    <span className="hidden sm:inline-block font-mono-code text-[11px] text-slate-400">
                      ({viewCase.id})
                    </span>
                    <span
                      title={
                        viewCase.status === 'unsupported'
                          ? `以 manifest notes 为准: ${viewCase.notes?.join(' ') || '未提供原因'}`
                          : viewCase.status === 'missing'
                            ? `以 manifest notes 为准: ${viewCase.notes?.join(' ') || '未提供原因'}`
                            : undefined
                      }
                      className={`px-2 py-0.5 rounded text-[11px] font-semibold whitespace-nowrap ${
                        formatStatus(viewCase.status).badgeClass
                      }`}
                    >
                      {formatStatus(viewCase.status).label}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 min-w-0 flex-wrap">
                    <span className="text-[11px] text-slate-400 font-mono-code bg-slate-800 px-2 py-0.5 rounded">
                      {viewCase.operation}
                    </span>
                    {catalogDisplayLink && (
                      <button
                        data-testid="version-toggle"
                        onClick={openAlternateSource}
                        disabled={altLoading}
                        title={catalogDisplayLink.note}
                        className={`text-[11px] px-2 py-0.5 rounded border transition-colors ${
                          altView
                            ? 'bg-indigo-600 text-white border-indigo-500'
                            : 'bg-slate-800 text-indigo-200 border-slate-700 hover:bg-slate-700'
                        }`}
                      >
                        {altView ? '返回本 run 采集' : `版本来源：${catalogDisplayLink.runId}`}
                      </button>
                    )}
                    <button data-testid="comparison-summary" onClick={() => setInspectionRequest({ id: Date.now(), tab: 'checks' })} className="text-[11px] text-sky-200 hover:underline text-left">
                      <strong>PK_DEBUG_BODY_compare</strong> · {comparisonLabel(viewCase, comparison)} ↗
                    </button>
                  </div>
                </div>

                {altError && (
                  <div data-testid="version-error" className="bg-rose-950/60 border-b border-rose-800 px-4 py-1 text-[11px] text-rose-200">
                    版本来源加载失败：{altError}
                  </div>
                )}
                {altView && (
                  <div data-testid="version-banner" className="bg-indigo-950/60 border-b border-indigo-800 px-4 py-1 text-[11px] text-indigo-100">
                    正在显示版本来源 <strong>{altView.runId}</strong> / {altView.caseData.id}（{altView.note}）；本 run 采集结果未被替换。
                  </div>
                )}

                {/* 3D Dual Viewports */}
                <div className="flex-1 relative overflow-hidden min-h-[220px]" data-testid="viewport-region">
                  <ViewportContainer
                    caseId={cameraContext}
                    operation={viewCase.operation}
                    gmMesh={loadedMeshKey === meshKey ? gmMeshes : null}
                    pkMesh={loadedMeshKey === meshKey ? pkMeshes : null}
                    gmError={gmError}
                    pkError={pkError}
                    isLoading={isLoadingMeshes}
                    cameraSync={cameraSync}
                    onCameraSyncChange={setCameraSync}
                    resetTrigger={resetTrigger}
                    onResetTrigger={() => setResetTrigger((prev) => prev + 1)}
                  />
                </div>

                {/* Timeline & Discrete Stepper */}
                <div data-testid="display-current-step" data-step-index={viewStepIndex} className="px-3 py-1 text-[11px] text-slate-500 border-b border-slate-200">
                  当前步骤：{viewCase.steps[viewStepIndex]?.id ?? '-'}（第 {viewStepIndex + 1} 步，共 {viewStepsCount} 步）
                </div>
                <TimelineControl
                  steps={viewCase.steps || []}
                  currentStepIndex={viewStepIndex}
                  onSelectStepIndex={(index) => {
                    if (altView) {
                      const step = altView.caseData.steps[index];
                      if (step) setAltView({ ...altView, stepId: step.id });
                    } else {
                      setCurrentStepIndex(index);
                    }
                  }}
                  subState={subState}
                  onSelectSubState={setSubState}
                />

                {/* Bottom Inspection Panel */}
                <InspectionPanel testCase={altView ? viewCase : currentCase!} manifestDir={viewDir} comparison={comparison} runMeta={manifestData?.run ?? null} request={inspectionRequest} />
              </>
            ) : (
              <div className="flex-1 flex flex-col items-center justify-center text-slate-400 text-xs">
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
