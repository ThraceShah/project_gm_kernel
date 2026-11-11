import React, { useState, useRef, useEffect } from 'react';
import { TestCase, RunMetadata } from '../types/contract';
import { GroupedChecks } from './ChecksTable';
import { ComparisonView, ComparisonEvidence } from './ComparisonView';
import { PerformanceView } from './PerformanceView';
import { ApiParamView } from './ApiParamView';
import {
  CheckSquare,
  Zap,
  Code2,
  FileText,
  ChevronDown,
  ChevronUp,
  GitBranch,
} from 'lucide-react';

interface InspectionPanelProps {
  testCase: TestCase;
  manifestDir: string;
  comparison: ComparisonEvidence;
  runMeta: RunMetadata | null;
  request?: { id: number; tab: 'checks' | 'data' } | null;
}

export const InspectionPanel: React.FC<InspectionPanelProps> = ({
  testCase,
  manifestDir,
  comparison,
  runMeta,
  request,
}) => {
  const [activeTab, setActiveTab] = useState<'checks' | 'perf' | 'params' | 'evidence' | 'data'>('checks');
  const [isCollapsed, setIsCollapsed] = useState<boolean>(true);
  const panelRef = useRef<HTMLDivElement>(null);
  useEffect(() => { setIsCollapsed(true); }, [testCase.id, manifestDir]);
  useEffect(() => { if (request) { setActiveTab(request.tab); setIsCollapsed(false); } }, [request]);
  useEffect(() => {
    if (isCollapsed) return;
    const previousFocus = document.activeElement as HTMLElement | null;
    const panel = panelRef.current;
    panel?.querySelector<HTMLButtonElement>('[data-testid="inspection-close"]')?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); setIsCollapsed(true); }
      if (event.key !== 'Tab' || !panel) return;
      const items = Array.from(panel.querySelectorAll<HTMLElement>('button:not([disabled]), a[href], select:not([disabled]), input:not([disabled]), textarea:not([disabled]), summary, [tabindex="0"]')).filter((el) => {
        if (!el.getClientRects().length) return false;
        // Closed native details can retain cached layout rectangles. Their
        // evidence links must not become the apparent last focusable control.
        for (let ancestor = el.parentElement; ancestor && ancestor !== panel; ancestor = ancestor.parentElement) {
          if (ancestor instanceof HTMLDetailsElement && !ancestor.open && !ancestor.querySelector(':scope > summary')?.contains(el)) return false;
        }
        return true;
      });
      const first = items[0], last = items[items.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('keydown', onKey); previousFocus?.focus(); };
  }, [isCollapsed]);

  const failCheckCount = testCase.checks?.filter((c) => c.status === 'fail').length ?? 0;
  const passCheckCount = testCase.checks?.filter((c) => c.status === 'pass').length ?? 0;

  return (
    <div className="h-10 flex-shrink-0 min-w-0" data-testid="inspection-shell">
    <div ref={panelRef} role={isCollapsed ? undefined : 'dialog'} aria-modal={isCollapsed ? undefined : true} aria-label="验收与证据" data-testid="inspection-panel" className={`${isCollapsed ? 'h-full' : 'absolute inset-0 z-40 shadow-2xl'} bg-white border-t border-slate-200 flex flex-col min-w-0`}>
      {/* Tab Header Bar */}
      <div className="flex items-center justify-between border-b border-slate-200 px-2 bg-slate-50 shrink-0 min-w-0">
        <div className="flex items-center gap-1 -mb-px overflow-x-auto whitespace-nowrap">
          {/* Checks Tab */}
          <button
            onClick={() => {
              setActiveTab('checks');
              setIsCollapsed(false);
            }}
            className={`flex items-center gap-1.5 py-2 px-3 text-xs font-semibold border-b-2 transition-colors ${
              activeTab === 'checks' && !isCollapsed
                ? 'border-sky-600 text-sky-900 bg-white'
                : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <CheckSquare className="w-3.5 h-3.5 text-sky-600" />
            <span>逐项验收（{testCase.checks?.length ?? 0} 条记录）</span>
            {failCheckCount > 0 && (
              <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-rose-100 text-rose-800 font-bold">
                {failCheckCount} 失败
              </span>
            )}
            {failCheckCount === 0 && passCheckCount > 0 && (
              <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-emerald-100 text-emerald-800 font-bold">
                {passCheckCount} 通过
              </span>
            )}
          </button>
          <button onClick={() => { setActiveTab('data'); setIsCollapsed(false); }} className={`flex items-center gap-1.5 py-2 px-3 text-xs font-semibold border-b-2 ${activeTab === 'data' && !isCollapsed ? 'border-sky-600 text-sky-900 bg-white' : 'border-transparent text-slate-600'}`}>数据版本</button>

          {/* Performance Tab */}
          <button
            onClick={() => {
              setActiveTab('perf');
              setIsCollapsed(false);
            }}
            className={`flex items-center gap-1.5 py-2 px-3 text-xs font-semibold border-b-2 transition-colors ${
              activeTab === 'perf' && !isCollapsed
                ? 'border-sky-600 text-sky-900 bg-white'
                : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <Zap className="w-3.5 h-3.5 text-amber-500" />
            <span>性能指标</span>
          </button>

          {/* APIs & Parameters Tab */}
          <button
            onClick={() => {
              setActiveTab('params');
              setIsCollapsed(false);
            }}
            className={`flex items-center gap-1.5 py-2 px-3 text-xs font-semibold border-b-2 transition-colors ${
              activeTab === 'params' && !isCollapsed
                ? 'border-sky-600 text-sky-900 bg-white'
                : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <Code2 className="w-3.5 h-3.5 text-slate-500" />
            <span>API 与参数</span>
          </button>

          {/* Evidence & Notes Tab */}
          <button
            onClick={() => {
              setActiveTab('evidence');
              setIsCollapsed(false);
            }}
            className={`flex items-center gap-1.5 py-2 px-3 text-xs font-semibold border-b-2 transition-colors ${
              activeTab === 'evidence' && !isCollapsed
                ? 'border-sky-600 text-sky-900 bg-white'
                : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <FileText className="w-3.5 h-3.5 text-slate-500" />
            <span>证据与日志 ({testCase.evidence?.length ?? 0})</span>
          </button>
        </div>

        {/* Collapse Toggle Button */}
        <button
          data-testid="inspection-close"
          onClick={() => setIsCollapsed(!isCollapsed)}
          aria-label={isCollapsed ? '展开验收面板' : '关闭验收面板'}
          aria-expanded={!isCollapsed}
          title={isCollapsed ? '展开面板' : '折叠面板'}
          className="shrink-0 p-2 text-slate-400 hover:text-slate-700 transition-colors"
        >
          {isCollapsed ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
        </button>
      </div>

      {/* Tab Content Body */}
      {!isCollapsed && (
        <div className="flex-1 min-h-0 min-w-0 overflow-auto" data-testid="inspection-content" tabIndex={0}>
          {activeTab === 'checks' && <><ComparisonView testCase={testCase} evidence={comparison} manifestDir={manifestDir} /><GroupedChecks checks={testCase.checks} evidenceBaseDir={manifestDir} /></>}
          {activeTab === 'data' && <div data-testid="data-version" className="p-4 space-y-3 text-xs text-slate-700 select-text break-words">
            <h3 className="font-semibold text-sm">已发布基线 · 真实数据来源</h3>
            <p>界面更新不表示结果已重新采集；此处展示 manifest 的原始运行数据，不将候选证明或未合并进度计为本 case 的新通过。</p>
            <dl className="space-y-2 font-mono-code">
              <div><dt>Run</dt><dd>{runMeta?.id ?? '未记录'}</dd></div>
              <div><dt>数据采集时间（run.generatedAt）</dt><dd>{runMeta?.generatedAt ?? '未记录'}</dd></div>
              <div><dt>采集代码（run.commit）</dt><dd>{runMeta?.commit ?? '未记录'}</dd></div>
              <div><dt>范围（run.scope）</dt><dd>{runMeta?.scope ? JSON.stringify(runMeta.scope) : '未记录'}</dd></div>
              <div><dt>配置 / samplesRequested（run.configuration）</dt><dd>{runMeta?.configuration ? JSON.stringify(runMeta.configuration) : '未记录'}</dd></div>
            </dl>
            <a href={`data/${manifestDir}/manifest.json`} className="text-sky-700 underline" target="_blank" rel="noopener noreferrer">原始 manifest ↗</a>
          </div>}
          {activeTab === 'perf' && <><p className="p-3 text-xs bg-amber-50 text-amber-900">历史已发布采样 · {runMeta?.generatedAt ?? '采集时间未记录'} · 此记录不作为本轮优化结论。</p><PerformanceView perf={testCase.performance} /></>}
          {activeTab === 'params' && (
            <ApiParamView
              parameters={testCase.parameters}
              gmApi={testCase.apis?.gm}
              pkApi={testCase.apis?.pk}
            />
          )}
          {activeTab === 'evidence' && (
            <div className="p-4 space-y-4 select-text">
              {/* Evidence Items */}
              <div>
                <h4 className="text-xs font-semibold text-slate-700 mb-2">原始验证凭证文件</h4>
                {testCase.evidence && testCase.evidence.length > 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                    {testCase.evidence.map((ev, idx) => (
                      <a
                        key={idx}
                        href={`data/${manifestDir}/${ev.path}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex items-center justify-between p-2.5 bg-slate-50 border border-slate-200 rounded-md text-xs hover:bg-sky-50 hover:border-sky-300 transition-colors group"
                        title={`打开证据文件: ${ev.path}`}
                      >
                        <div className="flex items-center gap-2 truncate">
                          <FileText className="w-4 h-4 text-sky-600 flex-shrink-0 group-hover:text-sky-700" />
                          <span className="font-medium text-slate-800 group-hover:text-sky-900 truncate">{ev.label}</span>
                        </div>
                        <span className="text-[11px] font-mono-code text-slate-400 group-hover:text-sky-600 truncate ml-2">
                          {ev.path} ↗
                        </span>
                      </a>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-slate-400">无额外原始证据挂载。</p>
                )}
              </div>

              {/* Case Notes */}
              <div>
                <h4 className="text-xs font-semibold text-slate-700 mb-1">工程诊断备注</h4>
                {testCase.notes && testCase.notes.length > 0 ? (
                  <ul className="list-disc list-inside space-y-1 text-xs text-slate-600 bg-slate-50 p-3 rounded-md border border-slate-200">
                    {testCase.notes.map((note, idx) => (
                      <li key={idx} className="leading-relaxed">
                        {note}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-slate-400">无工程备注。</p>
                )}
              </div>

              {/* Capability scope & adapter status (real fields, no inferred kernel claims) */}
              {(testCase.capabilityScope || testCase.adapterStatus || testCase.kernelCapability !== undefined) && (
                <div
                  data-testid="capability-info"
                  className="text-xs text-slate-600 bg-slate-50 p-3 rounded-md border border-slate-200 space-y-1"
                >
                  <h4 className="font-semibold text-slate-700">能力范围与适配状态 (Capability / Adapter)</h4>
                  <div className="font-mono-code text-[11px]">
                    capabilityScope={String(testCase.capabilityScope ?? '-')}
                    {' '}adapterStatus={String(testCase.adapterStatus ?? '-')}
                    {' '}kernelCapability={testCase.kernelCapability == null ? 'null' : String(testCase.kernelCapability)}
                    {testCase.sessionPrecision != null ? ` sessionPrecision=${String(testCase.sessionPrecision)}` : ''}
                  </div>
                  <div className="text-[11px] text-slate-500">
                    {testCase.adapterStatus === 'not-implemented'
                      ? '展示/导出适配器未实现（web-export-adapter not-implemented），非内核操作不支持；未执行也未推断通过。'
                      : testCase.adapterStatus === 'implemented'
                        ? '展示/导配器已实现（web-export-adapter implemented）。'
                        : '早期 run 未提供 adapterStatus 字段，状态以 notes 为准。'}
                    {testCase.kernelCapability == null
                      ? ' kernelCapability=null：内核能力未评估/未知，不能称为内核缺失。'
                      : ''}
                  </div>
                </div>
              )}

              {/* Original process execution (exit codes) */}
              {testCase.execution && (
                <div
                  data-testid="execution-info"
                  className="text-xs text-slate-600 bg-slate-50 p-3 rounded-md border border-slate-200 space-y-1"
                >
                  <h4 className="font-semibold text-slate-700">原进程执行信息 (Execution)</h4>
                  <div className="font-mono-code text-[11px]">
                    exitCodes=[{(testCase.execution.exitCodes ?? (testCase.execution.exitCode !== null && testCase.execution.exitCode !== undefined ? [testCase.execution.exitCode] : [])).join(', ')}]
                    {' '}(exitCode={String(testCase.execution.exitCode)})
                    {' '}attempted={String(testCase.execution.attempted)}
                    {' '}timedOut={String(testCase.execution.timedOut)}
                  </div>
                  <div className="text-[11px] text-slate-500">
                    原进程退出码保留为结果数据；非零退出不代表导出成功，失败结果不得计为通过。
                  </div>
                </div>
              )}

              {/* Source Repo Reference */}
              {testCase.source && (
                <div className="text-xs text-slate-500 flex items-center gap-2 pt-2 border-t border-slate-100">
                  <GitBranch className="w-3.5 h-3.5 text-slate-400" />
                  <span>测试源代码:</span>
                  <span className="font-mono-code text-slate-700">{testCase.source.path}</span>
                  <span className="font-mono-code text-slate-400">({testCase.source.revision.slice(0, 7)})</span>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
    </div>
  );
};
