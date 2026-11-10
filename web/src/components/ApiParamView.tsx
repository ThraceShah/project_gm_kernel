import React, { useState } from 'react';
import { ApiCallInfo } from '../types/contract';
import { Copy, Check, Code2 } from 'lucide-react';

interface ApiParamViewProps {
  parameters: Record<string, unknown>;
  gmApi: ApiCallInfo;
  pkApi: ApiCallInfo;
}

export const ApiParamView: React.FC<ApiParamViewProps> = ({
  parameters,
  gmApi,
  pkApi,
}) => {
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  const copyToClipboard = (text: string, key: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  return (
    <div className="p-4 space-y-4 select-text">
      {/* APIs Dual Comparison */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {/* GM API */}
        <div className="bg-slate-50 border border-slate-200 rounded-lg overflow-hidden">
          <div className="bg-slate-100/80 px-3 py-2 border-b border-slate-200 flex items-center justify-between">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-sky-800">
              <Code2 className="w-3.5 h-3.5 text-sky-600" />
              <span>GM Kernel API: {gmApi?.name || '未知 API'}</span>
            </div>
            <button
              onClick={() =>
                copyToClipboard(
                  JSON.stringify(gmApi?.parameters || {}, null, 2),
                  'gm'
                )
              }
              className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-800"
            >
              {copiedKey === 'gm' ? (
                <>
                  <Check className="w-3 h-3 text-emerald-600" />
                  <span className="text-emerald-600">已复制</span>
                </>
              ) : (
                <>
                  <Copy className="w-3 h-3" />
                  <span>复制 JSON</span>
                </>
              )}
            </button>
          </div>
          <pre className="p-3 text-[11px] font-mono-code text-slate-800 bg-white overflow-x-auto max-h-60">
            {JSON.stringify(gmApi?.parameters || {}, null, 2)}
          </pre>
        </div>

        {/* PK API */}
        <div className="bg-slate-50 border border-slate-200 rounded-lg overflow-hidden">
          <div className="bg-slate-100/80 px-3 py-2 border-b border-slate-200 flex items-center justify-between">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-emerald-800">
              <Code2 className="w-3.5 h-3.5 text-emerald-600" />
              <span>Parasolid API: {pkApi?.name || '未知 API'}</span>
            </div>
            <button
              onClick={() =>
                copyToClipboard(
                  JSON.stringify(pkApi?.parameters || {}, null, 2),
                  'pk'
                )
              }
              className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-800"
            >
              {copiedKey === 'pk' ? (
                <>
                  <Check className="w-3 h-3 text-emerald-600" />
                  <span className="text-emerald-600">已复制</span>
                </>
              ) : (
                <>
                  <Copy className="w-3 h-3" />
                  <span>复制 JSON</span>
                </>
              )}
            </button>
          </div>
          <pre className="p-3 text-[11px] font-mono-code text-slate-800 bg-white overflow-x-auto max-h-60">
            {JSON.stringify(pkApi?.parameters || {}, null, 2)}
          </pre>
        </div>
      </div>

      {/* Case Global Parameters */}
      <div className="bg-slate-50 border border-slate-200 rounded-lg overflow-hidden">
        <div className="bg-slate-100/80 px-3 py-2 border-b border-slate-200 flex items-center justify-between">
          <span className="text-xs font-semibold text-slate-700">
            测试例全量输入参数 (Case Parameters)
          </span>
          <button
            onClick={() =>
              copyToClipboard(JSON.stringify(parameters || {}, null, 2), 'params')
            }
            className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-800"
          >
            {copiedKey === 'params' ? (
              <>
                <Check className="w-3 h-3 text-emerald-600" />
                <span className="text-emerald-600">已复制</span>
              </>
            ) : (
              <>
                <Copy className="w-3 h-3" />
                <span>复制参数</span>
              </>
            )}
          </button>
        </div>
        <pre className="p-3 text-[11px] font-mono-code text-slate-800 bg-white overflow-x-auto max-h-60">
          {JSON.stringify(parameters || {}, null, 2)}
        </pre>
      </div>
    </div>
  );
};
