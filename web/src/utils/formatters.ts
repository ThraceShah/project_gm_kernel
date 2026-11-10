import { CaseStatus } from '../types/contract';

export function formatStatus(status: CaseStatus | string): {
  label: string;
  badgeClass: string;
  bgLight: string;
  borderClass: string;
  textClass: string;
} {
  switch (status) {
    case 'pass':
      return {
        label: '通过 (PASS)',
        badgeClass: 'bg-emerald-600 text-white',
        bgLight: 'bg-emerald-50 text-emerald-800 border-emerald-300',
        borderClass: 'border-emerald-500',
        textClass: 'text-emerald-700',
      };
    case 'fail':
      return {
        label: '失败 (FAIL)',
        badgeClass: 'bg-rose-600 text-white',
        bgLight: 'bg-rose-50 text-rose-800 border-rose-300',
        borderClass: 'border-rose-500',
        textClass: 'text-rose-700',
      };
    case 'missing':
      return {
        label: '数据缺失 (MISSING)',
        badgeClass: 'bg-slate-500 text-white',
        bgLight: 'bg-slate-100 text-slate-700 border-slate-300',
        borderClass: 'border-slate-400',
        textClass: 'text-slate-600',
      };
    case 'unsupported':
      return {
        label: '未适配 (UNSUPPORTED)',
        badgeClass: 'bg-amber-600 text-white',
        bgLight: 'bg-amber-50 text-amber-800 border-amber-300',
        borderClass: 'border-amber-500',
        textClass: 'text-amber-700',
      };
    case 'timeout':
      return {
        label: '超时 (TIMEOUT)',
        badgeClass: 'bg-purple-600 text-white',
        bgLight: 'bg-purple-50 text-purple-800 border-purple-300',
        borderClass: 'border-purple-500',
        textClass: 'text-purple-700',
      };
    case 'diagnostic':
      return {
        label: '诊断测试 (DIAGNOSTIC)',
        badgeClass: 'bg-sky-600 text-white',
        bgLight: 'bg-sky-50 text-sky-800 border-sky-300',
        borderClass: 'border-sky-500',
        textClass: 'text-sky-700',
      };
    default:
      return {
        label: status ? String(status).toUpperCase() : '未知',
        badgeClass: 'bg-slate-600 text-white',
        bgLight: 'bg-slate-50 text-slate-700 border-slate-300',
        borderClass: 'border-slate-400',
        textClass: 'text-slate-600',
      };
  }
}

export function formatRatio(ratio: number | null | undefined): {
  text: string;
  badgeClass: string;
  subtext: string;
} {
  if (ratio === null || ratio === undefined || isNaN(ratio) || ratio <= 0) {
    return {
      text: 'N/A',
      badgeClass: 'bg-slate-100 text-slate-500 border border-slate-300',
      subtext: '无有效比率 (null)',
    };
  }

  const formatted = ratio.toFixed(2) + 'x';

  if (ratio < 0.95) {
    const diffPct = Math.round((1 - ratio) * 100);
    return {
      text: formatted,
      badgeClass: 'bg-emerald-100 text-emerald-800 border border-emerald-300 font-semibold',
      subtext: `GM 快 ${diffPct}%`,
    };
  } else if (ratio <= 1.05) {
    return {
      text: formatted,
      badgeClass: 'bg-blue-100 text-blue-800 border border-blue-300 font-semibold',
      subtext: '耗时相当 (±5%)',
    };
  } else {
    const diffPct = Math.round((ratio - 1) * 100);
    return {
      text: formatted,
      badgeClass: 'bg-amber-100 text-amber-800 border border-amber-300 font-semibold',
      subtext: `PK 快 ${diffPct}%`,
    };
  }
}

export function formatMs(ms: number | null | undefined): string {
  if (ms === null || ms === undefined || isNaN(ms)) return 'N/A';
  if (ms < 0.001) return '< 0.001 ms';
  if (ms < 1) return `${(ms).toFixed(3)} ms`;
  if (ms < 10) return `${(ms).toFixed(2)} ms`;
  return `${ms.toFixed(1)} ms`;
}

export function formatDateTime(isoString: string): string {
  if (!isoString) return '-';
  try {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleString('zh-CN', {
      hour12: false,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return isoString;
  }
}
