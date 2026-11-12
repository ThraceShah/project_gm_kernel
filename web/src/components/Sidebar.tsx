import React from 'react';
import { Search, CheckCircle2, XCircle, HelpCircle, AlertCircle, Ban, Clock, Filter, Layers } from 'lucide-react';
import { TestCase, CaseFamily, CaseStatus } from '../types/contract';

interface SidebarProps {
  cases: TestCase[];
  selectedCaseId: string;
  onSelectCase: (caseId: string) => void;
  searchQuery: string;
  onSearchChange: (q: string) => void;
  familyFilter: CaseFamily | 'all';
  onFamilyFilterChange: (f: CaseFamily | 'all') => void;
  statusFilter: CaseStatus | 'all';
  onStatusFilterChange: (s: CaseStatus | 'all') => void;
  suiteFilter: string;
  onSuiteFilterChange: (s: string) => void;
  // Case IDs that carry a current-round improvement record (history/badge only).
  improvedCaseIds: Set<string>;
  improvedOnly: boolean;
  onImprovedOnlyChange: (v: boolean) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  cases,
  selectedCaseId,
  onSelectCase,
  searchQuery,
  onSearchChange,
  familyFilter,
  onFamilyFilterChange,
  statusFilter,
  onStatusFilterChange,
  suiteFilter,
  onSuiteFilterChange,
  improvedCaseIds,
  improvedOnly,
  onImprovedOnlyChange,
}) => {
  // Compute counts
  const counts = React.useMemo(() => {
    let pass = 0;
    let fail = 0;
    let missing = 0;
    let unsupported = 0;
    let timeout = 0;
    let diagnostic = 0;

    cases.forEach((c) => {
      if (c.status === 'pass') pass++;
      else if (c.status === 'fail') fail++;
      else if (c.status === 'missing') missing++;
      else if (c.status === 'unsupported') unsupported++;
      else if (c.status === 'timeout') timeout++;
      else if (c.status === 'diagnostic') diagnostic++;
    });

    return {
      total: cases.length,
      pass,
      fail,
      missing,
      unsupported,
      timeout,
      diagnostic,
    };
  }, [cases]);

  // Suites (stable registry grouping, never shrunk by a filter)
  const suites = React.useMemo(() => {
    const counts = new Map<string, number>();
    cases.forEach((c) => {
      const key = c.suite || '未分组';
      counts.set(key, (counts.get(key) ?? 0) + 1);
    });
    return Array.from(counts.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [cases]);

  // Filter cases
  const filteredCases = React.useMemo(() => {
    return cases.filter((c) => {
      // Suite filter
      if (suiteFilter !== 'all' && (c.suite || '未分组') !== suiteFilter) {
        return false;
      }
      // Current-round improvement filter
      if (improvedOnly && !improvedCaseIds.has(c.id)) {
        return false;
      }
      // Family filter
      if (familyFilter !== 'all' && c.family !== familyFilter) {
        return false;
      }
      // Status filter
      if (statusFilter !== 'all' && c.status !== statusFilter) {
        return false;
      }
      // Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchId = c.id.toLowerCase().includes(q);
        const matchTitle = c.title.toLowerCase().includes(q);
        const matchOp = c.operation.toLowerCase().includes(q);
        if (!matchId && !matchTitle && !matchOp) return false;
      }
      return true;
    });
  }, [cases, familyFilter, statusFilter, searchQuery, suiteFilter, improvedOnly, improvedCaseIds]);

  // Grouped rows: a suite header followed by its cases, preserving registry order.
  const grouped = React.useMemo(() => {
    const rows: Array<{ kind: 'group'; suite: string; count: number } | { kind: 'case'; testCase: TestCase }> = [];
    let current = '';
    for (const testCase of filteredCases) {
      const suite = testCase.suite || '未分组';
      if (suite !== current) {
        current = suite;
        rows.push({ kind: 'group', suite, count: filteredCases.filter((c) => (c.suite || '未分组') === suite).length });
      }
      rows.push({ kind: 'case', testCase });
    }
    return rows;
  }, [filteredCases]);

  return (
    <aside className="w-80 flex-shrink-0 bg-white border-r border-slate-200 flex flex-col h-full select-none z-10">
      {/* Search & Header */}
      <div className="p-3 border-b border-slate-200 space-y-2">
        <div className="relative">
          <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-2.5" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="搜索用例 ID / 标题 / API 操作..."
            data-testid="case-search"
            className="w-full pl-8 pr-3 py-1.5 text-xs bg-slate-50 border border-slate-200 rounded-md placeholder-slate-400 focus:outline-none focus:ring-1 focus:ring-sky-500 focus:bg-white text-slate-800"
          />
          {searchQuery && (
            <button
              onClick={() => onSearchChange('')}
              className="absolute right-2.5 top-2 text-xs text-slate-400 hover:text-slate-600"
            >
              ×
            </button>
          )}
        </div>

        {/* Family Tabs */}
        <div className="grid grid-cols-3 gap-1 bg-slate-100 p-0.5 rounded text-xs">
          <button
            onClick={() => onFamilyFilterChange('all')}
            className={`py-1 rounded font-medium transition-all ${
              familyFilter === 'all'
                ? 'bg-white text-sky-900 shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            全部 ({counts.total})
          </button>
          <button
            onClick={() => onFamilyFilterChange('blend')}
            className={`py-1 rounded font-medium transition-all ${
              familyFilter === 'blend'
                ? 'bg-white text-sky-900 shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            倒角 Blend
          </button>
          <button
            onClick={() => onFamilyFilterChange('boolean')}
            className={`py-1 rounded font-medium transition-all ${
              familyFilter === 'boolean'
                ? 'bg-white text-sky-900 shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            布尔 Boolean
          </button>
        </div>

        {/* Suite selector (stable 20-suite registry grouping) */}
        <div className="flex items-center gap-1">
          <select
            data-testid="suite-filter"
            value={suiteFilter}
            onChange={(e) => onSuiteFilterChange(e.target.value)}
            className="flex-1 text-xs bg-slate-50 border border-slate-200 rounded-md px-1.5 py-1 text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-500"
          >
            <option value="all">全部 suite（{suites.length}）</option>
            {suites.map(([name, count]) => (
              <option key={name} value={name}>
                {name}（{count}）
              </option>
            ))}
          </select>
          <button
            data-testid="improved-filter"
            onClick={() => onImprovedOnlyChange(!improvedOnly)}
            title="只显示带有当前轮次改进/复验记录（history）的用例；这是筛选，不改变 463 固定目录"
            className={`px-1.5 py-1 rounded border text-[11px] whitespace-nowrap transition-colors ${
              improvedOnly
                ? 'bg-indigo-600 text-white border-indigo-600 font-semibold'
                : 'bg-indigo-50 text-indigo-800 border-indigo-200 hover:bg-indigo-100'
            }`}
          >
            本轮改进 {improvedCaseIds.size}
          </button>
        </div>

        {/* Status Pills */}
        <div className="flex flex-wrap items-center gap-1 text-[11px] pt-1">
          <button
            onClick={() => onStatusFilterChange('all')}
            className={`px-2 py-0.5 rounded border transition-colors ${
              statusFilter === 'all'
                ? 'bg-slate-800 text-white border-slate-800 font-semibold'
                : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100'
            }`}
          >
            全状态
          </button>

          <button
            onClick={() => onStatusFilterChange('pass')}
            className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
              statusFilter === 'pass'
                ? 'bg-emerald-600 text-white border-emerald-600 font-semibold'
                : 'bg-emerald-50 text-emerald-800 border-emerald-200 hover:bg-emerald-100'
            }`}
          >
            <CheckCircle2 className="w-3 h-3 text-emerald-600" />
            <span>通过 {counts.pass}</span>
          </button>

          <button
            onClick={() => onStatusFilterChange('fail')}
            className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
              statusFilter === 'fail'
                ? 'bg-rose-600 text-white border-rose-600 font-semibold'
                : 'bg-rose-50 text-rose-800 border-rose-200 hover:bg-rose-100'
            }`}
          >
            <XCircle className="w-3 h-3 text-rose-600" />
            <span>失败 {counts.fail}</span>
          </button>

          <button
            onClick={() => onStatusFilterChange('missing')}
            title="本次 run 未选中执行，当前证据缺失（不推断通过，也不代表内核能力缺失）"
            className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
              statusFilter === 'missing'
                ? 'bg-slate-600 text-white border-slate-600 font-semibold'
                : 'bg-slate-100 text-slate-600 border-slate-200 hover:bg-slate-200'
            }`}
          >
            <HelpCircle className="w-3 h-3 text-slate-500" />
            <span>缺失 {counts.missing}</span>
          </button>

          {counts.unsupported > 0 && (
            <button
              onClick={() => onStatusFilterChange('unsupported')}
              title="展示/导出未适配：已注册用例，web 快照/结构化检查适配器未实现（非内核操作不支持）；未执行也未推断通过，以 manifest notes 为准"
              data-testid="status-unsupported-pill"
              className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
                statusFilter === 'unsupported'
                  ? 'bg-amber-600 text-white border-amber-600 font-semibold'
                  : 'bg-amber-50 text-amber-800 border-amber-200 hover:bg-amber-100'
              }`}
            >
              <Ban className="w-3 h-3 text-amber-600" />
              <span>未适配 {counts.unsupported}</span>
            </button>
          )}

          {counts.diagnostic > 0 && (
            <button
              onClick={() => onStatusFilterChange('diagnostic')}
              className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
                statusFilter === 'diagnostic'
                  ? 'bg-sky-600 text-white border-sky-600 font-semibold'
                  : 'bg-sky-50 text-sky-800 border-sky-200 hover:bg-sky-100'
              }`}
            >
              <AlertCircle className="w-3 h-3 text-sky-600" />
              <span>诊断 {counts.diagnostic}</span>
            </button>
          )}

          {counts.timeout > 0 && (
            <button
              onClick={() => onStatusFilterChange('timeout')}
              className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded border transition-colors ${
                statusFilter === 'timeout'
                  ? 'bg-purple-600 text-white border-purple-600 font-semibold'
                  : 'bg-purple-50 text-purple-800 border-purple-200 hover:bg-purple-100'
              }`}
            >
              <Clock className="w-3 h-3 text-purple-600" />
              <span>超时 {counts.timeout}</span>
            </button>
          )}
        </div>
      </div>

      {/* Case List */}
      <div className="flex-1 overflow-y-auto divide-y divide-slate-100">
        {filteredCases.length === 0 ? (
          <div className="p-8 text-center text-slate-400 text-xs">
            <Filter className="w-8 h-8 mx-auto mb-2 opacity-40 text-slate-400" />
            <p>未找到符合筛选条件的用例</p>
          </div>
        ) : (
          grouped.map((row) => {
            if (row.kind === 'group') {
              return (
                <div
                  key={`group:${row.suite}`}
                  data-testid="suite-group-header"
                  className="sticky top-0 z-[1] bg-slate-100/95 backdrop-blur px-3 py-1 text-[10px] font-semibold uppercase tracking-wide text-slate-500 border-y border-slate-200 flex items-center justify-between"
                >
                  <span>{row.suite}</span>
                  <span className="font-mono-code text-slate-400">{row.count}</span>
                </div>
              );
            }
            const c = row.testCase;
            const isSelected = c.id === selectedCaseId;
            const improved = improvedCaseIds.has(c.id);

            return (
              <div
                key={c.id}
                onClick={() => onSelectCase(c.id)}
                data-testid="case-item"
                data-case-id={c.id}
                className={`p-3 cursor-pointer text-left transition-colors border-l-4 ${
                  isSelected
                    ? 'bg-sky-50/70 border-l-sky-600 text-sky-950 shadow-xs'
                    : 'border-l-transparent hover:bg-slate-50 text-slate-700'
                }`}
              >
                <div className="flex items-start justify-between gap-1 mb-1">
                  <div className="flex items-center gap-1.5 min-w-0">
                    {c.status === 'pass' && (
                      <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 flex-shrink-0" />
                    )}
                    {c.status === 'fail' && (
                      <XCircle className="w-3.5 h-3.5 text-rose-600 flex-shrink-0" />
                    )}
                    {c.status === 'missing' && (
                      <HelpCircle className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                    )}
                    {c.status === 'unsupported' && (
                      <Ban
                        className="w-3.5 h-3.5 text-amber-600 flex-shrink-0"
                        aria-label="展示/导出未适配（非内核能力缺失）"
                      />
                    )}
                    {c.status === 'timeout' && (
                      <Clock className="w-3.5 h-3.5 text-purple-600 flex-shrink-0" />
                    )}
                    {c.status === 'diagnostic' && (
                      <AlertCircle className="w-3.5 h-3.5 text-sky-600 flex-shrink-0" />
                    )}
                    <span className="text-xs font-semibold truncate leading-tight">
                      {c.title}
                    </span>
                  </div>

                </div>

                <div className="flex items-center justify-between text-[11px] text-slate-500 font-mono-code">
                  <span className="truncate max-w-[160px] text-slate-400">{c.id}</span>
                  <span className="flex items-center gap-1">
                    {improved && (
                      <span
                        data-testid="improved-badge"
                        title="当前轮次有改进/复验记录（见右侧历史）"
                        className="text-[9px] bg-indigo-100 text-indigo-700 px-1 rounded font-semibold"
                      >
                        改进
                      </span>
                    )}
                    <span className="text-[10px] bg-slate-100 text-slate-600 px-1 rounded">
                      {c.operation}
                    </span>
                  </span>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Footer Info */}
      <div className="p-2 border-t border-slate-200 bg-slate-50 text-[11px] text-slate-500 flex items-center justify-between px-3">
        <div className="flex items-center gap-1">
          <Layers className="w-3 h-3 text-slate-400" />
          <span>显示 {filteredCases.length} / {cases.length} 项</span>
        </div>
        <span className="font-mono-code text-[10px] text-slate-400">Schema v1</span>
      </div>
      <div className="px-3 pb-2 text-[10px] leading-relaxed text-slate-400 bg-slate-50">
        未适配 = 展示/导出适配器未实现（非内核能力缺失）；缺失 = 本次 run 未执行。状态含义以各用例 manifest notes 为准。
      </div>
    </aside>
  );
};
