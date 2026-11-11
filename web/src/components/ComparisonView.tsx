import React, { useEffect, useState } from 'react';
import { TestCase } from '../types/contract';

interface ComparisonRecord {
  globalResult: number;
  localResult: number;
  facePairs?: number;
  globalDiffs?: unknown[];
  localDiffs?: { diagnostic?: boolean; type?: number; [key: string]: unknown }[];
  mask?: Record<string, unknown>;
  diagnosticWhitelist?: string[];
}
export interface ComparisonEvidence {
  key: string;
  records: { path: string; data: ComparisonRecord | null }[];
  loading: boolean;
}

export function useComparisonEvidence(testCase: TestCase | null, manifestDir: string): ComparisonEvidence {
  const paths = (testCase?.evidence ?? []).map((e) => e.path).filter((p) => /(?:^|\/)compare[^/]*\.json$/i.test(p));
  const key = JSON.stringify([manifestDir, testCase?.id, paths]);
  const [state, setState] = useState<ComparisonEvidence>({ key: '', records: [], loading: false });
  useEffect(() => {
    const controller = new AbortController();
    setState({ key, records: [], loading: paths.length > 0 });
    Promise.all(paths.map(async (path) => {
      try {
        const response = await fetch(`data/${manifestDir}/${path}`, { signal: controller.signal });
        if (!response.ok) return { path, data: null };
        const data = await response.json();
        return { path, data: typeof data.globalResult === 'number' && typeof data.localResult === 'number' ? data as ComparisonRecord : null };
      } catch { return { path, data: null }; }
    })).then((records) => { if (!controller.signal.aborted) setState({ key, records, loading: false }); });
    return () => controller.abort();
  }, [key]);
  // Evidence follows the same ownership rule as geometry; stale results never
  // lend another case a successful comparison while its request is pending.
  return state.key === key ? state : { key, records: [], loading: paths.length > 0 };
}

export function comparisonLabel(testCase: TestCase, evidence: ComparisonEvidence): string {
  if (evidence.loading) return '读取原始记录…';
  if (!evidence.records.length || evidence.records.some((r) => !r.data)) return '原始比较未记录 / 不可用';
  const checks = testCase.checks.filter((c) => /equivalence|BODY_compare/.test(`${c.id} ${c.label}`));
  const statuses = [...new Set(checks.map((c) => c.status))];
  const diagnostics = evidence.records.reduce((n, r) => n + (r.data?.localDiffs?.filter((d) => d.diagnostic === true).length ?? 0), 0);
  const whitelist = [...new Set(evidence.records.flatMap((r) => r.data?.diagnosticWhitelist ?? []))];
  return `${statuses.length === 1 && statuses[0] === 'pass' ? '原记录主比较通过' : statuses.includes('fail') ? '原记录主比较失败' : '验收状态未记录'}${diagnostics ? ` · ${diagnostics} 条表示诊断${whitelist.length ? `（${whitelist.join(', ')}）` : ''}` : ''}`;
}

export const ComparisonView: React.FC<{ testCase: TestCase; evidence: ComparisonEvidence; manifestDir: string }> = ({ testCase, evidence, manifestDir }) => (
  <section data-testid="comparison-details" className="p-3 space-y-3 text-xs text-slate-700 select-text">
    <h3 className="font-semibold text-sm">PK_DEBUG_BODY_compare · {comparisonLabel(testCase, evidence)}</h3>
    <p>展示已发布基线的原始比较与原验收状态；表示诊断不等于 raw 全零差异。facePairs 是原始配对记录数，不能作为全部面匹配成功数量。</p>
    {evidence.records.map(({ path, data }) => (
      <div key={path} className="border border-slate-200 rounded p-3 space-y-2 min-w-0">
        <a className="text-sky-700 underline break-all" href={`data/${manifestDir}/${path}`} target="_blank" rel="noopener noreferrer">原始比较证据 ↗ {path}</a>
        {data ? <>
          <div className="font-mono-code" data-testid="comparison-results">global={data.globalResult} · local={data.localResult} · facePairs={data.facePairs ?? '未记录'}</div>
          <div className="font-mono-code break-words" data-testid="comparison-mask">{data.mask ? Object.entries(data.mask).map(([k, v]) => `${k}=${String(v)}`).join(' · ') : 'mask 未记录'}</div>
          <div>诊断白名单（原记录）：{data.diagnosticWhitelist?.join(', ') || '未记录'}</div>
          <details><summary className="cursor-pointer font-medium">global / local 差异与 face/entity 原始明细</summary><pre className="overflow-x-auto whitespace-pre p-2 text-[11px] bg-slate-50">{JSON.stringify(data, null, 2)}</pre></details>
        </> : <p>原始证据未记录、缺失或不可读取；不能据此推断通过。</p>}
      </div>
    ))}
  </section>
);
