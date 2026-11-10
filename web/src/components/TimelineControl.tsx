import React, { useState, useEffect } from 'react';
import { StepData } from '../types/contract';
import { Play, Pause, SkipBack, SkipForward, Clock } from 'lucide-react';

interface TimelineControlProps {
  steps: StepData[];
  currentStepIndex: number;
  onSelectStepIndex: (index: number) => void;
  subState: 'before' | 'after';
  onSelectSubState: (sub: 'before' | 'after') => void;
}

export const TimelineControl: React.FC<TimelineControlProps> = ({
  steps,
  currentStepIndex,
  onSelectStepIndex,
  subState,
  onSelectSubState,
}) => {
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [speed, setSpeed] = useState<number>(1); // 0.5, 1, 2

  // Total snapshot points: each step has 'before', and optionally 'after'
  // Build flattened discrete snapshot list
  const snapshots = React.useMemo(() => {
    const list: { stepIndex: number; sub: 'before' | 'after'; title: string }[] = [];
    steps.forEach((s, idx) => {
      list.push({
        stepIndex: idx,
        sub: 'before',
        title: `${s.title} (前态)`,
      });
      if (s.gm.after || s.pk.after) {
        list.push({
          stepIndex: idx,
          sub: 'after',
          title: `${s.title} (后态)`,
        });
      }
    });
    return list;
  }, [steps]);

  // Current active index in snapshots list
  const currentSnapshotIndex = snapshots.findIndex(
    (sn) => sn.stepIndex === currentStepIndex && sn.sub === subState
  );

  const activeIndex = currentSnapshotIndex >= 0 ? currentSnapshotIndex : 0;

  // Auto-play loop
  useEffect(() => {
    if (!isPlaying || snapshots.length <= 1) return;

    const intervalMs = 1500 / speed;
    const timer = setInterval(() => {
      const nextIndex = (activeIndex + 1) % snapshots.length;
      const target = snapshots[nextIndex];
      if (target) {
        onSelectStepIndex(target.stepIndex);
        onSelectSubState(target.sub);
      }
    }, intervalMs);

    return () => clearInterval(timer);
  }, [isPlaying, activeIndex, snapshots, speed, onSelectStepIndex, onSelectSubState]);

  const handlePrev = () => {
    if (snapshots.length <= 1) return;
    const prevIndex = (activeIndex - 1 + snapshots.length) % snapshots.length;
    const target = snapshots[prevIndex];
    if (target) {
      onSelectStepIndex(target.stepIndex);
      onSelectSubState(target.sub);
    }
  };

  const handleNext = () => {
    if (snapshots.length <= 1) return;
    const nextIndex = (activeIndex + 1) % snapshots.length;
    const target = snapshots[nextIndex];
    if (target) {
      onSelectStepIndex(target.stepIndex);
      onSelectSubState(target.sub);
    }
  };

  const handleSliderChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const idx = parseInt(e.target.value, 10);
    const target = snapshots[idx];
    if (target) {
      onSelectStepIndex(target.stepIndex);
      onSelectSubState(target.sub);
    }
  };

  return (
    <div className="bg-white border-t border-slate-200 px-4 py-2 flex flex-wrap items-center justify-between gap-3 select-none z-20 shadow-xs">
      {/* Step Selector Pills */}
      <div className="flex items-center gap-2 overflow-x-auto">
        <span className="text-xs font-semibold text-slate-700 flex items-center gap-1">
          <Clock className="w-3.5 h-3.5 text-sky-600" />
          <span>步骤:</span>
        </span>
        <div className="flex items-center gap-1 bg-slate-100 p-0.5 rounded border border-slate-200">
          {steps.map((step, idx) => (
            <button
              key={step.id || idx}
              onClick={() => onSelectStepIndex(idx)}
              className={`text-xs px-2.5 py-1 rounded font-medium transition-colors ${
                currentStepIndex === idx
                  ? 'bg-sky-600 text-white shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              {step.title || `步骤 ${idx + 1}`}
            </button>
          ))}
        </div>

        {/* Sub-state before / after toggle */}
        <div className="flex items-center gap-1 bg-slate-100 p-0.5 rounded border border-slate-200 ml-1">
          <button
            onClick={() => onSelectSubState('before')}
            className={`text-xs px-2 py-0.5 rounded font-medium transition-colors ${
              subState === 'before'
                ? 'bg-white text-sky-900 font-semibold shadow-xs'
                : 'text-slate-500 hover:text-slate-800'
            }`}
          >
            前态 (Before)
          </button>
          <button
            onClick={() => onSelectSubState('after')}
            className={`text-xs px-2 py-0.5 rounded font-medium transition-colors ${
              subState === 'after'
                ? 'bg-white text-sky-900 font-semibold shadow-xs'
                : 'text-slate-500 hover:text-slate-800'
            }`}
          >
            后态 (After)
          </button>
        </div>
      </div>

      {/* Scrubbable Timeline & Play Controls */}
      <div className="flex items-center gap-3">
        {/* Play/Pause & Step Buttons */}
        <div className="flex items-center gap-1">
          <button
            onClick={handlePrev}
            title="上一个快照"
            className="p-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 transition-colors"
          >
            <SkipBack className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={() => setIsPlaying(!isPlaying)}
            title={isPlaying ? '暂停' : '自动按快照播放'}
            className="p-1 rounded bg-sky-600 hover:bg-sky-700 text-white transition-colors"
          >
            {isPlaying ? <Pause className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
          </button>
          <button
            onClick={handleNext}
            title="下一个快照"
            className="p-1 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 transition-colors"
          >
            <SkipForward className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Timeline Slider */}
        <div className="flex items-center gap-2">
          <input
            type="range"
            min={0}
            max={Math.max(snapshots.length - 1, 0)}
            value={activeIndex}
            onChange={handleSliderChange}
            className="w-28 sm:w-40 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer accent-sky-600"
          />
          <span className="text-[11px] font-mono-code text-slate-500 whitespace-nowrap">
            {activeIndex + 1}/{snapshots.length}
          </span>
        </div>

        {/* Speed Selector */}
        <div className="flex items-center gap-0.5 bg-slate-100 p-0.5 rounded text-[11px] border border-slate-200">
          {[0.5, 1, 2].map((s) => (
            <button
              key={s}
              onClick={() => setSpeed(s)}
              className={`px-1.5 py-0.5 rounded font-mono-code ${
                speed === s ? 'bg-white text-sky-800 font-bold shadow-xs' : 'text-slate-500'
              }`}
            >
              {s}x
            </button>
          ))}
        </div>

        {/* Discrete Notice */}
        <span className="hidden xl:inline-block text-[11px] text-slate-400 border-l border-slate-200 pl-3">
          离散快照切换 (真实几何无形变插值)
        </span>
      </div>
    </div>
  );
};
