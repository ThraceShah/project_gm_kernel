import React, { useState } from 'react';
import { TestCase } from '../types/contract';
import { ChecksTable } from './ChecksTable';
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
}

export const InspectionPanel: React.FC<InspectionPanelProps> = ({
  testCase,
  manifestDir,
}) => {
  const [activeTab, setActiveTab] = useState<'checks' | 'perf' | 'params' | 'evidence'>('checks');
  const [isCollapsed, setIsCollapsed] = useState<boolean>(false);

  const failCheckCount = testCase.checks?.filter((c) => c.status === 'fail').length ?? 0;
  const passCheckCount = testCase.checks?.filter((c) => c.status === 'pass').length ?? 0;

  return (
    <div className="bg-white border-t border-slate-200 flex flex-col flex-shrink-0 z-20 shadow-md">
      {/* Tab Header Bar */}
      <div className="flex items-center justify-between border-b border-slate-200 px-4 bg-slate-50/80">
        <div className="flex items-center gap-1 -mb-px">
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
            <span>逐项验收 ({testCase.checks?.length ?? 0})</span>
            {failCheckCount > 0 && (
              <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-rose-100 text-rose-800 font-bold">
                {failCheckCount} 失败
              </span>
            )}
            {failCheckCount === 0 && passCheckCount > 0 && (
              <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-emerald-100 text-emerald-800 font-bold">
                全通过
              </span>
            )}
          </button>

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
            {testCase.performance?.ratioGmOverPk && (
              <span className="text-[10px] px-1.5 py-0.2 rounded font-mono-code bg-slate-200 text-slate-700">
                {testCase.performance.ratioGmOverPk.toFixed(2)}x
              </span>
            )}
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
          onClick={() => setIsCollapsed(!isCollapsed)}
          title={isCollapsed ? '展开面板' : '折叠面板'}
          className="p-1 text-slate-400 hover:text-slate-700 transition-colors"
        >
          {isCollapsed ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
        </button>
      </div>

      {/* Tab Content Body */}
      {!isCollapsed && (
        <div className="h-48 sm:h-64 overflow-y-auto">
          {activeTab === 'checks' && <ChecksTable checks={testCase.checks} evidenceBaseDir={manifestDir} />}
          {activeTab === 'perf' && <PerformanceView perf={testCase.performance} />}
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
  );
};
