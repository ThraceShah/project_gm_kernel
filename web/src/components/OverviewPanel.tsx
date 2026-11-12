import React, { useMemo, useState } from 'react';
import {
  ProjectOverview,
  OverviewStatusCell,
} from '../types/contract';
import { formatDateTime } from '../utils/formatters';

interface OverviewPanelProps {
  overview: ProjectOverview;
  onOpenRuns: (runId?: string, caseId?: string, stepId?: string) => void;
}

const STATUS_LABEL: Record<string, string> = {
  pass: '通过', fail: '失败', unresolved: '未决', unverified: '未验证', na: '不适用',
  verified: '已验证', different: '不同',
};
const STATUS_CLASS: Record<string, string> = {
  pass: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  verified: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  fail: 'bg-rose-50 text-rose-700 border-rose-200',
  different: 'bg-rose-50 text-rose-700 border-rose-200',
  unresolved: 'bg-amber-50 text-amber-700 border-amber-200',
  unverified: 'bg-slate-50 text-slate-500 border-slate-200',
  na: 'bg-slate-50 text-slate-400 border-slate-200',
};

const StatusChip: React.FC<{ cell: OverviewStatusCell | undefined; label: string }> = ({ cell, label }) => {
  const s = cell?.status ?? 'unverified';
  return (
    <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded border text-[10px] font-medium ${STATUS_CLASS[s] ?? STATUS_CLASS.unverified}`} title={cell?.note ?? ''}>
      {label}·{STATUS_LABEL[s] ?? s}
    </span>
  );
};

export const OverviewPanel: React.FC<OverviewPanelProps> = ({ overview, onOpenRuns }) => {
  const [search, setSearch] = useState('');
  const [family, setFamily] = useState<'all' | 'blend' | 'boolean'>('all');
  const [onlyIssues, setOnlyIssues] = useState(false);
  const [expanded, setExpanded] = useState<string | null>(null);
  const summary = overview.summary as Record<string, string | number>;
  const catalog = overview.catalog;

  const filtered = useMemo(() => catalog.filter((c) => {
    if (family !== 'all' && c.family !== family) return false;
    if (search && !(c.id + ' ' + c.title + ' ' + c.suite).toLowerCase().includes(search.toLowerCase())) return false;
    if (onlyIssues) {
      const d = c.dimensions;
      if (![d.function, d.mass, d.query, d.performance].some((x) => x.status === 'fail' || x.status === 'unresolved')) return false;
    }
    return true;
  }), [catalog, family, search, onlyIssues]);

  const suites = useMemo(() => Array.from(new Set(filtered.map((c) => c.suite))), [filtered]);

  return (
    <div data-testid="overview-panel" className="flex flex-col h-full min-h-0 bg-slate-50 text-slate-800">
      {/* summary */}
      <div className="p-3 border-b border-slate-200 bg-white">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div>
            <h2 className="text-sm font-bold">项目整体进度 · 固定用例目录</h2>
            <div className="text-[11px] text-slate-500">
              当前指定版本：{overview.currentVersion.label} · 目录 {String(summary['catalog'] ?? catalog.length)} 个具名建模用例（稳定 ID）· 生成 {formatDateTime(overview.generatedAt)}
            </div>
          </div>
          <button data-testid="open-run-history" onClick={() => onOpenRuns()} className="text-xs px-3 py-1.5 rounded border border-slate-300 hover:bg-slate-100">交付记录 / 历史运行 ↗</button>
        </div>
        <div className="flex flex-wrap gap-1.5 mt-2">
          <span title="多版本累计证据（各行历史分列），非当前版本全量复验" className="px-2 py-1 rounded bg-emerald-50 border border-emerald-200 text-[11px] text-emerald-700">累计证据·功能通过 {String(summary['function:pass'] ?? 0)}</span>
          <span className="px-2 py-1 rounded bg-slate-50 border border-slate-200 text-[11px] text-slate-500">功能未验证 {String(summary['function:unverified'] ?? 0)}</span>
          <span className="px-2 py-1 rounded bg-amber-50 border border-amber-200 text-[11px] text-amber-700">物性未决 {String(summary['mass:unresolved'] ?? 0)}</span>
          <span className="px-2 py-1 rounded bg-rose-50 border border-rose-200 text-[11px] text-rose-700">查询失败 {String(summary['boundary:fail'] ?? 0)}+边界</span>
          <span className="px-2 py-1 rounded bg-sky-50 border border-sky-200 text-[11px] text-sky-700">性能观测 {String(summary['performance:observed'] ?? 0)}</span>
          <span title="当前指定统一版本 1654 实际复验的操作数（与累计通过不同）" className="px-2 py-1 rounded bg-indigo-50 border border-indigo-200 text-[11px] text-indigo-700">当前版本已复验 {String(summary['performance:observed'] ?? 0)} op</span>
        </div>
        <div className="text-[10px] text-slate-400 mt-1">{String(summary['denominatorNote'] ?? '')}</div>
      </div>
      {/* controls */}
      <div className="flex items-center gap-2 p-2 border-b border-slate-200 bg-white">
        <input data-testid="overview-search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="搜索用例 / 套件 / ID" className="flex-1 text-xs px-2 py-1.5 rounded border border-slate-300" />
        <select data-testid="overview-family" value={family} onChange={(e) => setFamily(e.target.value as 'all' | 'blend' | 'boolean')} className="text-xs px-2 py-1.5 rounded border border-slate-300">
          <option value="all">全部</option><option value="blend">Blend</option><option value="boolean">Boolean</option>
        </select>
        <label className="text-xs flex items-center gap-1"><input type="checkbox" data-testid="overview-issues" checked={onlyIssues} onChange={(e) => setOnlyIssues(e.target.checked)} />只看有问题</label>
      </div>
      {/* matrix */}
      <div className="flex-1 min-h-0 overflow-auto">
        {suites.map((suite) => (
          <div key={suite} className="border-b border-slate-100">
            <div className="px-3 py-1.5 bg-slate-100 text-[11px] font-semibold text-slate-600 sticky top-0">{suite}（{filtered.filter((c) => c.suite === suite).length}）</div>
            {filtered.filter((c) => c.suite === suite).map((c) => (
              <div key={c.id} className="border-t border-slate-50">
                <button data-testid={`catalog-row-${c.id}`} onClick={() => setExpanded(expanded === c.id ? null : c.id)} className="w-full text-left px-3 py-2 hover:bg-sky-50 flex items-center gap-2 flex-wrap">
                  <span className="text-[11px] font-mono-code text-slate-700 min-w-[220px]">{c.id}</span>
                  <StatusChip cell={c.dimensions.function} label="功能" />
                  <StatusChip cell={c.dimensions.mass} label="物性" />
                  <StatusChip cell={c.dimensions.query} label="查询" />
                  <StatusChip cell={c.dimensions.performance} label="性能" />
                  <span className={`text-[10px] px-1.5 py-0.5 rounded ${c.dimensions.currentVersion === 'reverified' ? 'bg-indigo-100 text-indigo-700' : 'bg-slate-100 text-slate-500'}`}>
                    {c.dimensions.currentVersion === 'reverified' ? '当前版本已复验' : '当前版本未复测'}
                  </span>
                </button>
                {expanded === c.id && (
                  <div data-testid={`catalog-detail-${c.id}`} className="px-4 py-2 bg-slate-50 text-[11px] space-y-2">
                    <div className="text-slate-600">{c.title} · {c.operation} · 来源 {c.source}</div>
                    <div className="font-mono-code text-slate-500 break-all">参数定义：{JSON.stringify(c.parametersDefinition)}</div>
                    {(['function', 'mass', 'query', 'performance'] as const).map((dim) => {
                      const cell = (c.dimensions as unknown as Record<string, OverviewStatusCell>)[dim];
                      return (
                        <div key={dim} className="flex items-start gap-2">
                          <span className="w-64"><StatusChip cell={cell} label={dim === 'function' ? '几何拓扑/操作功能' : dim === 'mass' ? '物性数值' : dim === 'query' ? '查询/分类' : '性能观测'} /></span>
                          <span className="flex-1 text-slate-600">{cell.note}
                            {cell.lastVerified.evidence ? <span className="block text-[10px] text-slate-400 break-all">最后验证：{cell.lastVerified.commit.slice(0, 7)} / {cell.lastVerified.tree} / {cell.lastVerified.host} / exit {cell.lastVerified.exit} · 私存原件（hash 锚定）：{cell.lastVerified.evidence || '（无）'}</span> : null}
                          </span>
                        </div>
                      );
                    })}
                    <div className="flex items-center gap-2">
                      <span className="w-64 text-slate-500">来源 runner</span>
                      <span className="text-slate-600 text-xs">{c.source ?? '-'}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="w-64 text-slate-500">在线产物</span>
                      {c.display ? (
                        <button data-testid={`catalog-model-${c.id}`} className="text-sky-700 underline" onClick={(e) => { e.stopPropagation(); onOpenRuns(c.display!.runId, c.display!.caseId, c.display!.stepId); }}>
                          查看模型 / 操作步骤 / 证据 ↗
                        </button>
                      ) : (
                        <span className="text-slate-400">无在线模型（原始证据见来源/归档索引；已归档/未采集不造 404）</span>
                      )}
                      {c.display && <span className="text-[10px] text-slate-400">{c.display.note} · run {c.display.runId} / case {c.display.caseId} / 步骤 {c.display.stepId}</span>}
                    </div>
                    <div>
                      <div className="font-semibold text-slate-600 mb-0.5">历史验证（各版本分别标注，不互相顶替）</div>
                      {(c.history ?? []).map((h, i) => (
                        <div key={i} className="flex items-center gap-2 py-0.5">
                          <span className="min-w-[220px] font-mono-code text-slate-500">{h.label}</span>
                          <StatusChip cell={{ status: h.status as OverviewStatusCell['status'], lastVerified: { commit: '-', tree: '-', host: '-', exit: '-', evidence: '' }, note: h.note }} label={h.status} />
                          <span className="text-slate-500 flex-1">{h.note}</span>
                        </div>
                      ))}
                      {(!c.history || c.history.length === 0) && <div className="text-slate-400">无历史验证记录（未测/未支持项保留于目录）。</div>}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        ))}
      </div>
      {/* boundary + records + deliveries + sources */}
      <div className="border-t border-slate-200 bg-white p-3 text-[11px] space-y-3 max-h-[36vh] overflow-auto">
        <div>
          <h3 className="font-semibold text-slate-700 mb-1">边界登记（不计入建模分母）</h3>
          {overview.boundaryRegisters.map((b) => (
            <div key={b.id} className="flex items-center gap-2 py-0.5"><span className="font-mono-code min-w-[220px]">{b.title}</span><StatusChip cell={{ status: b.status as OverviewStatusCell['status'], lastVerified: { commit: '-', tree: '-', host: '-', exit: '-', evidence: b.evidence }, note: b.note }} label={b.status} /><span className="text-slate-500 flex-1">{b.note}</span></div>
          ))}
        </div>
        <div>
          <h3 className="font-semibold text-slate-700 mb-1">验证记录（套件/质量/性能口径，不计入建模分母）</h3>
          {overview.verificationRecords.map((b) => (
            <div key={b.id} className="flex items-center gap-2 py-0.5"><span className="min-w-[220px]">{b.title}</span><StatusChip cell={{ status: b.status as OverviewStatusCell['status'], lastVerified: { commit: '-', tree: '-', host: '-', exit: '-', evidence: b.evidence }, note: b.note }} label={b.status} /><span className="text-slate-500 flex-1">{b.note}</span></div>
          ))}
        </div>
        <div>
          <h3 className="font-semibold text-slate-700 mb-1">交付记录 / 历史运行（单轮快照）</h3>
          {overview.deliveries.map((d, i) => (
            <div key={i} className="flex items-center gap-2 py-0.5">
              {d.runId === 'archived' ? <span className="min-w-[220px] text-slate-500">已归档快照</span> :
                <button className="min-w-[220px] text-sky-700 underline text-left" onClick={() => onOpenRuns(d.runId)}>{d.label}</button>}
              <span className="font-mono-code text-slate-400">{d.commit.slice(0, 7)}</span><span className="text-slate-400">{d.utc}</span><span className="text-slate-500 flex-1">{d.note}</span>
            </div>
          ))}
        </div>
        <div>
          <h3 className="font-semibold text-slate-700 mb-1">来源 / 归档索引</h3>
          {overview.sourceIndex.map((s, i) => (
            <div key={i} className="py-0.5"><span className="font-medium">{s.name}</span> · <span className="font-mono-code text-slate-500 break-all">{s.identity}</span> · <span className="text-slate-500">{s.where}</span> · <span className="text-slate-400">{s.note}</span></div>
          ))}
        </div>
      </div>
    </div>
  );
};
