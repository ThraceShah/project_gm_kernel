import React from 'react';
import { ROLE_COLORS, EDGE_ROLE_COLORS } from '../utils/geometryBuilder';

export const EntityLegend: React.FC = () => {
  return (
    <div data-testid="legend-body" aria-label="共享实体角色图例" className="shrink-0 overflow-x-auto whitespace-nowrap px-3 py-1 text-[10px] text-slate-300 bg-slate-900 border-b border-slate-800 select-none">

      <div className="flex items-center gap-4 w-max">
        {/* Yellow Selected Edge */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-3 h-1 rounded"
            style={{ backgroundColor: EDGE_ROLE_COLORS.selected.css }}
          />
          <span className="truncate">{EDGE_ROLE_COLORS.selected.label}</span>
        </div>

        {/* Blue Selected / Support Face */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2.5 h-2.5 rounded-xs"
            style={{ backgroundColor: ROLE_COLORS.selected.css }}
          />
          <span className="truncate">{ROLE_COLORS.selected.label}</span>
        </div>

        {/* Green New Blend Face */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2.5 h-2.5 rounded-xs"
            style={{ backgroundColor: ROLE_COLORS.newBlend.css }}
          />
          <span className="truncate">{ROLE_COLORS.newBlend.label}</span>
        </div>

        {/* Orange Modified Face */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2.5 h-2.5 rounded-xs"
            style={{ backgroundColor: ROLE_COLORS.modified.css }}
          />
          <span className="truncate">{ROLE_COLORS.modified.label}</span>
        </div>

        {/* Red Deleted Face */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2.5 h-2.5 rounded-xs"
            style={{ backgroundColor: ROLE_COLORS.deleted.css }}
          />
          <span className="truncate">{ROLE_COLORS.deleted.label}</span>
        </div>

        {/* Slate Unchanged Entity */}
        <div className="flex items-center gap-1.5">
          <span
            className="w-2.5 h-2.5 rounded-xs"
            style={{ backgroundColor: ROLE_COLORS.unchanged.css }}
          />
          <span className="truncate">{ROLE_COLORS.unchanged.label}</span>
        </div>

        {/* Cyan Isoparametric Curve */}
        <div className="flex items-center gap-1.5">
          <div className="flex items-center justify-center w-3 h-2">
            <span className="w-3 h-0.5 bg-sky-400 rounded-full" />
          </div>
          <span className="truncate text-sky-300">真实 U/V（若已记录）</span>
        </div>

        {/* Translucent context indication */}
        <div className="flex items-center gap-1.5">
          <span className="w-2.5 h-2.5 rounded-xs border border-dashed border-slate-500 bg-slate-500/25" />
          <span className="truncate text-slate-400">未修改半透明</span>
        </div>
      </div>
    </div>
  );
};
