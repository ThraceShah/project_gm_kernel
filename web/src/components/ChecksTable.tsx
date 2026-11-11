import React from 'react';
import { CheckItem } from '../types/contract';
import { formatStatus } from '../utils/formatters';
import { CheckCircle2, XCircle, HelpCircle, AlertCircle, FileText } from 'lucide-react';

interface ChecksTableProps {
  checks: CheckItem[];
  evidenceBaseDir?: string;
}

export const ChecksTable: React.FC<ChecksTableProps> = ({ checks, evidenceBaseDir }) => {
  if (!checks || checks.length === 0) {
    return (
      <div className="p-8 text-center text-slate-400 text-xs">
        <HelpCircle className="w-8 h-8 mx-auto mb-2 opacity-40 text-slate-400" />
        <p>此用例尚未录入逐项验收检查数据</p>
      </div>
    );
  }

  return (
    <div className="w-full overflow-x-auto select-text">
      <table className="w-full text-left text-xs border-collapse">
        <thead>
          <tr className="bg-slate-100 text-slate-600 border-b border-slate-200">
            <th className="py-2 px-3 font-semibold">状态</th>
            <th className="py-2 px-3 font-semibold">检查项 (Label)</th>
            <th className="py-2 px-3 font-semibold font-mono-code">GM 实测</th>
            <th className="py-2 px-3 font-semibold font-mono-code">PK 基准</th>
            <th className="py-2 px-3 font-semibold font-mono-code">绝对偏差</th>
            <th className="py-2 px-3 font-semibold font-mono-code">相对偏差</th>
            <th className="py-2 px-3 font-semibold font-mono-code">容差门限</th>
            <th className="py-2 px-3 font-semibold">单位</th>
            <th className="py-2 px-3 font-semibold">证据 / 备注</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {checks.map((chk) => {
            const st = formatStatus(chk.status);

            const formatVal = (v: number | string | null | undefined | unknown) => {
              if (v === null || v === undefined) return <span className="text-slate-400 italic">null</span>;
              if (typeof v === 'number') {
                if (Number.isInteger(v)) return v.toString();
                // Keep tiny non-zero deviations visible (never render them as "0" or "")
                return String(Number(v.toPrecision(10)));
              }
              if (typeof v === 'object') {
                return JSON.stringify(v);
              }
              return String(v);
            };

            return (
              <tr key={chk.id} className="hover:bg-slate-50/80 transition-colors">
                {/* Status */}
                <td className="py-2.5 px-3 whitespace-nowrap">
                  <span
                    className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded font-medium ${st.bgLight} border`}
                  >
                    {chk.status === 'pass' && <CheckCircle2 className="w-3 h-3 text-emerald-600" />}
                    {chk.status === 'fail' && <XCircle className="w-3 h-3 text-rose-600" />}
                    {chk.status === 'missing' && <HelpCircle className="w-3 h-3 text-slate-500" />}
                    {chk.status !== 'pass' && chk.status !== 'fail' && chk.status !== 'missing' && (
                      <AlertCircle className="w-3 h-3 text-amber-600" />
                    )}
                    <span>{chk.status.toUpperCase()}</span>
                  </span>
                </td>

                {/* Label */}
                <td className="py-2.5 px-3 font-medium text-slate-800">
                  <div className="leading-snug">{chk.label}</div>
                  <div className="font-mono-code text-[10px] text-slate-400">{chk.id}</div>
                </td>

                {/* GM */}
                <td className="py-2.5 px-3 font-mono-code text-slate-700 whitespace-nowrap">
                  {formatVal(chk.gm)}
                </td>

                {/* PK */}
                <td className="py-2.5 px-3 font-mono-code text-slate-700 whitespace-nowrap">
                  {formatVal(chk.pk)}
                </td>

                {/* Abs Delta */}
                <td className="py-2.5 px-3 font-mono-code text-slate-600 whitespace-nowrap">
                  {formatVal(chk.absDelta)}
                </td>

                {/* Normalized Delta */}
                <td className="py-2.5 px-3 font-mono-code text-slate-600 whitespace-nowrap">
                  {formatVal(chk.normalizedDelta)}
                </td>

                {/* Threshold */}
                <td className="py-2.5 px-3 font-mono-code text-slate-500 whitespace-nowrap">
                  {chk.threshold !== null && chk.threshold !== undefined ? `≤ ${chk.threshold}` : '-'}
                </td>

                {/* Unit */}
                <td className="py-2.5 px-3 text-slate-500 whitespace-nowrap">
                  {chk.unit || '-'}
                </td>

                {/* Note & Evidence */}
                <td className="py-2.5 px-3 max-w-xs text-slate-600 text-[11px]">
                  {chk.note && <div className="leading-tight mb-1">{chk.note}</div>}
                  {chk.evidence && (
                    evidenceBaseDir ? (
                      <a
                        href={`data/${evidenceBaseDir}/${chk.evidence}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1 text-sky-700 hover:text-sky-900 hover:underline font-mono-code text-[10px]"
                        title={`查看证据文件: ${chk.evidence}`}
                      >
                        <FileText className="w-3 h-3 flex-shrink-0" />
                        <span className="truncate max-w-[180px]">{chk.evidence}</span>
                      </a>
                    ) : (
                      <div className="flex items-center gap-1 text-sky-700 font-mono-code text-[10px]">
                        <FileText className="w-3 h-3 flex-shrink-0" />
                        <span className="truncate" title={chk.evidence}>
                          {chk.evidence}
                        </span>
                      </div>
                    )
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};

export const GroupedChecks: React.FC<ChecksTableProps> = ({ checks, evidenceBaseDir }) => {
  const groups = new Map<string, CheckItem[]>();
  for (const check of checks ?? []) {
    const group = /equivalence|BODY_compare/.test(`${check.id} ${check.label}`) ? '原主比较验收记录（PK_DEBUG_BODY_compare + type / regions）'
      : /body-check|BODY_check/.test(`${check.id} ${check.label}`) ? 'BODY_check 有效性'
      : /-mass-/.test(check.id) ? '物性量'
      : /-count-\d+$/.test(check.id) ? '拓扑重复计数'
      : /-api$|-blend-fault$|-body-count$/.test(check.id) ? '操作契约：API return / blend fault / 输出体数量'
      : /oracle-exit/.test(check.id) ? '原进程 / 场景 exit'
      : '其他原始验收记录';
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group)!.push(check);
  }
  return <div className="p-3 space-y-2 min-w-0" data-testid="grouped-checks">
    <p className="text-[11px] text-slate-500">{checks?.length ?? 0} 条原始验收记录，含重复计数与辅助项；记录数不代表独立能力数。展开保留实测值、微小偏差、阈值与原始证据。</p>
    {Array.from(groups, ([group, rows]) => <details key={group} className="border border-slate-200 rounded min-w-0">
      <summary className="cursor-pointer px-3 py-2 bg-slate-50 text-xs font-medium text-slate-700">{group} · {rows.length} 条 · {rows.filter((c) => c.status === 'pass').length} 通过 / {rows.filter((c) => c.status === 'fail').length} 失败 / {rows.filter((c) => c.status !== 'pass' && c.status !== 'fail').length} 其他</summary>
      <ChecksTable checks={rows} evidenceBaseDir={evidenceBaseDir} />
    </details>)}
  </div>;
};
