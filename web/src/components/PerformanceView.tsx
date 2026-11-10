import React from 'react';
import { PerformanceInfo } from '../types/contract';
import { formatRatio, formatMs } from '../utils/formatters';
import { Zap, Cpu, Award, AlertCircle, BarChart3, HelpCircle } from 'lucide-react';

interface PerformanceViewProps {
  perf: PerformanceInfo;
}

export const PerformanceView: React.FC<PerformanceViewProps> = ({ perf }) => {
  if (!perf || (perf.gmMs === null && perf.pkMs === null && perf.ratioGmOverPk === null)) {
    return (
      <div className="p-8 text-center text-slate-400 text-xs">
        <HelpCircle className="w-8 h-8 mx-auto mb-2 opacity-40 text-slate-400" />
        <p>此用例尚未录入基准性能测试数据</p>
      </div>
    );
  }

  const ratioInfo = formatRatio(perf.ratioGmOverPk);

  const formatHost = (host: unknown): string => {
    if (!host) return '-';
    if (typeof host === 'string') return host;
    if (typeof host === 'object' && host !== null) {
      const h = host as Record<string, unknown>;
      const parts = [];
      if (h.cpu) parts.push(String(h.cpu));
      if (h.architecture) parts.push(String(h.architecture));
      if (h.os) parts.push(String(h.os));
      if (h.sdk) parts.push(`SDK ${h.sdk}`);
      return parts.join(' / ') || JSON.stringify(host);
    }
    return String(host);
  };

  const formatConfig = (config: unknown): string => {
    if (!config) return '-';
    if (typeof config === 'string') return config;
    if (typeof config === 'object' && config !== null) {
      const c = config as Record<string, unknown>;
      const parts = [];
      if (c.build) parts.push(String(c.build));
      if (c.nativeAot !== undefined) parts.push(c.nativeAot ? 'NativeAOT' : 'Managed');
      if (c.timing) parts.push(String(c.timing));
      return parts.join(' | ') || JSON.stringify(config);
    }
    return String(config);
  };

  return (
    <div className="p-4 space-y-4 select-text">
      {/* Top Metric Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {/* GM Time Card */}
        <div className="bg-sky-50/70 border border-sky-200 rounded-lg p-3">
          <div className="flex items-center justify-between text-xs text-sky-800 font-semibold mb-1">
            <span className="flex items-center gap-1">
              <Zap className="w-3.5 h-3.5 text-sky-600" />
              <span>GM Kernel 耗时</span>
            </span>
            <span className="text-[10px] text-sky-600 font-mono-code">实测</span>
          </div>
          <div className="text-2xl font-bold font-mono-code text-sky-950">
            {formatMs(perf.gmMs)}
          </div>
          <div className="text-[11px] text-sky-700/80 mt-1">
            {perf.samples ? `统计样本: ${perf.samples} 次 (预热: ${perf.warmup ?? 0})` : '单次基准'}
          </div>
        </div>

        {/* PK Time Card */}
        <div className="bg-emerald-50/70 border border-emerald-200 rounded-lg p-3">
          <div className="flex items-center justify-between text-xs text-emerald-800 font-semibold mb-1">
            <span className="flex items-center gap-1">
              <BarChart3 className="w-3.5 h-3.5 text-emerald-600" />
              <span>Parasolid (PK) 耗时</span>
            </span>
            <span className="text-[10px] text-emerald-600 font-mono-code">基准 Oracle</span>
          </div>
          <div className="text-2xl font-bold font-mono-code text-emerald-950">
            {formatMs(perf.pkMs)}
          </div>
          <div className="text-[11px] text-emerald-700/80 mt-1">
            Parasolid 官方内核执行基线
          </div>
        </div>

        {/* Ratio Card */}
        <div className="bg-white border border-slate-200 rounded-lg p-3 shadow-xs">
          <div className="flex items-center justify-between text-xs text-slate-700 font-semibold mb-1">
            <span>GM ÷ PK 耗时倍率</span>
            <span className="text-[10px] text-slate-400">数字越小 GM 越快</span>
          </div>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold font-mono-code text-slate-900">
              {ratioInfo.text}
            </span>
            <span className={`text-xs px-2 py-0.5 rounded font-medium ${ratioInfo.badgeClass}`}>
              {ratioInfo.subtext}
            </span>
          </div>
          <div className="text-[11px] text-slate-500 mt-1">
            {perf.ratioGmOverPk && perf.ratioGmOverPk < 1.0 ? 'GM 具备速度优势' : 'PK 处于性能领先'}
          </div>
        </div>
      </div>

      {/* Correctness & Qualification Banner */}
      <div className="flex items-center gap-2 p-3 bg-slate-50 rounded-lg border border-slate-200 text-xs text-slate-700">
        <Award
          className={`w-4 h-4 flex-shrink-0 ${
            perf.correctnessQualified ? 'text-emerald-600' : 'text-amber-500'
          }`}
        />
        <div className="flex-1">
          <span className="font-semibold mr-1">正确性资格认证 (Correctness Qualified):</span>
          {perf.correctnessQualified === true ? (
            <span className="text-emerald-700 font-medium">
              通过认证 (几何与拓扑所有强制验收项满足容差门限)
            </span>
          ) : perf.correctnessQualified === false ? (
            <span className="text-rose-700 font-medium">
              未满足资格 (存在几何超标或拓扑不一致，此耗时成绩仅作参考)
            </span>
          ) : (
            <span className="text-slate-500">待资格评定</span>
          )}
        </div>
      </div>

      {/* Environment & Hardware Details */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
        <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 space-y-1.5">
          <div className="font-semibold text-slate-800 flex items-center gap-1.5 mb-2">
            <Cpu className="w-3.5 h-3.5 text-slate-500" />
            <span>基准测试环境 (Host & Config)</span>
          </div>
          <div className="flex justify-between py-1 border-b border-slate-100">
            <span className="text-slate-500">测试宿主机器:</span>
            <span className="font-mono-code text-slate-700">{formatHost(perf.host)}</span>
          </div>
          <div className="flex justify-between py-1 border-b border-slate-100">
            <span className="text-slate-500">编译/发布配置:</span>
            <span className="font-mono-code text-slate-700">{formatConfig(perf.configuration)}</span>
          </div>
          <div className="flex justify-between py-1">
            <span className="text-slate-500">基准测试范围 (Scope):</span>
            <span className="text-slate-700">{typeof perf.scope === 'object' ? JSON.stringify(perf.scope) : (perf.scope || '-')}</span>
          </div>
        </div>

        {/* Note / Analysis */}
        <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 space-y-1.5">
          <div className="font-semibold text-slate-800 flex items-center gap-1.5 mb-2">
            <AlertCircle className="w-3.5 h-3.5 text-slate-500" />
            <span>性能分析与备注 (Performance Notes)</span>
          </div>
          <p className="text-slate-600 leading-relaxed text-[11px]">
            {perf.note || '无额外性能备注。'}
          </p>
        </div>
      </div>
    </div>
  );
};
