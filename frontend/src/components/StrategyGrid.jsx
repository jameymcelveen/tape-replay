import { useMemo, useRef, useState } from 'react';
import {
  getCellMetricValue,
  getCoverageColor,
  getFlatEpsilon,
  mapMetricToColor,
} from '../utils/heatmapColors';
import { isMonthStart, monthLabel } from '../utils/tradingCalendar';

const ROW_HEIGHT = 14;
const CELL_WIDTH = 12;
const LABEL_WIDTH = 64;
const HEADER_HEIGHT = 28;
const VIRTUAL_OVERSCAN = 6;

/**
 * GitHub-style grid shared by coverage and strategy performance modes.
 */
export default function StrategyGrid({
  mode = 'performance',
  tradingDays = [],
  rows = [],
  metric = 'pnlPct',
  clamp = { min: -20, max: 20 },
  coverageByTicker = {},
  onCellClick,
}) {
  const scrollRef = useRef(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [viewportHeight, setViewportHeight] = useState(480);

  const gridWidth = tradingDays.length * CELL_WIDTH;

  const monthMarkers = useMemo(
    () => tradingDays.map((day, index) => ({
      day,
      index,
      showLabel: isMonthStart(day, tradingDays[index - 1]),
    })),
    [tradingDays],
  );

  const { startIndex, endIndex, topSpacer, bottomSpacer } = useMemo(() => {
    const start = Math.max(0, Math.floor(scrollTop / ROW_HEIGHT) - VIRTUAL_OVERSCAN);
    const visibleCount = Math.ceil(viewportHeight / ROW_HEIGHT) + VIRTUAL_OVERSCAN * 2;
    const end = Math.min(rows.length, start + visibleCount);
    return {
      startIndex: start,
      endIndex: end,
      topSpacer: start * ROW_HEIGHT,
      bottomSpacer: Math.max(0, (rows.length - end) * ROW_HEIGHT),
    };
  }, [rows.length, scrollTop, viewportHeight]);

  function resolveCellColor(ticker, day, cell) {
    if (mode === 'coverage') {
      const status = coverageByTicker[ticker]?.[day];
      return status ? getCoverageColor(status) : 'rgba(30, 41, 59, 0.35)';
    }

    if (!cell?.hasData) {
      return 'rgba(15, 23, 42, 0.2)';
    }

    if (!cell.traded) {
      return 'rgb(148, 163, 184)';
    }

    const value = getCellMetricValue(cell, metric);
    const epsilon = getFlatEpsilon(metric);
    if (value != null && Math.abs(value) <= epsilon) {
      return 'rgb(148, 163, 184)';
    }

    return mapMetricToColor(value, metric, clamp);
  }

  function handleScroll(event) {
    setScrollTop(event.currentTarget.scrollTop);
  }

  return (
    <div className="overflow-hidden rounded-lg border border-slate-700 bg-slate-950/60">
      <div className="flex">
        <div style={{ width: LABEL_WIDTH, minWidth: LABEL_WIDTH }} className="shrink-0 border-r border-slate-800">
          <div style={{ height: HEADER_HEIGHT }} className="border-b border-slate-800" />
        </div>
        <div className="overflow-x-auto">
          <div style={{ width: gridWidth, minWidth: gridWidth }} className="relative border-b border-slate-800" >
            <div style={{ height: HEADER_HEIGHT }} className="relative">
              {monthMarkers.filter((marker) => marker.showLabel).map((marker) => (
                <span
                  key={marker.day}
                  className="absolute top-1 text-[10px] text-slate-500"
                  style={{ left: marker.index * CELL_WIDTH }}
                >
                  {monthLabel(marker.day)}
                </span>
              ))}
            </div>
          </div>
        </div>
      </div>

      <div
        ref={scrollRef}
        className="max-h-[70vh] overflow-auto"
        onScroll={handleScroll}
        onMouseEnter={(event) => setViewportHeight(event.currentTarget.clientHeight)}
      >
        <div className="flex min-w-max">
          <div
            style={{ width: LABEL_WIDTH, minWidth: LABEL_WIDTH }}
            className="sticky left-0 z-10 shrink-0 border-r border-slate-800 bg-slate-950"
          >
            <div style={{ height: topSpacer }} />
            {rows.slice(startIndex, endIndex).map((row) => (
              <div
                key={row.ticker}
                style={{ height: ROW_HEIGHT }}
                className="truncate pr-2 text-right text-[11px] leading-[14px] text-slate-400"
                title={row.ticker}
              >
                {row.ticker}
              </div>
            ))}
            <div style={{ height: bottomSpacer }} />
          </div>

          <div style={{ width: gridWidth, minWidth: gridWidth }}>
            <div style={{ height: topSpacer }} />
            {rows.slice(startIndex, endIndex).map((row) => (
              <div key={row.ticker} className="flex" style={{ height: ROW_HEIGHT }}>
                {tradingDays.map((day) => {
                  const cell = row.dayMap?.[day];
                  const color = resolveCellColor(row.ticker, day, cell);
                  return (
                    <button
                      key={`${row.ticker}-${day}`}
                      type="button"
                      title={buildTooltip(row.ticker, day, cell, mode)}
                      className="shrink-0 border border-slate-950/40 p-0 hover:ring-1 hover:ring-slate-400"
                      style={{
                        width: CELL_WIDTH,
                        height: ROW_HEIGHT,
                        backgroundColor: color,
                      }}
                      onClick={() => onCellClick?.({ ticker: row.ticker, date: day, cell })}
                    />
                  );
                })}
              </div>
            ))}
            <div style={{ height: bottomSpacer }} />
          </div>
        </div>
      </div>
    </div>
  );
}

function buildTooltip(ticker, day, cell, mode) {
  if (mode === 'coverage') {
    return `${ticker} ${day}`;
  }

  if (!cell?.hasData) {
    return `${ticker} ${day} — no minute data`;
  }

  if (!cell.traded) {
    return `${ticker} ${day} — data, no trade`;
  }

  const parts = [
    `${ticker} ${day}`,
    `pnl ${formatPct(cell.pnlPct)}`,
    `capture ${formatPct(cell.capturePct)}`,
  ];

  if (cell.entryTime) {
    parts.push(`entry ${cell.entryPrice}`);
  }

  if (cell.exitTime) {
    parts.push(`exit ${cell.exitPrice} (${cell.exitReason ?? '—'})`);
  }

  return parts.join(' · ');
}

function formatPct(value) {
  if (value == null || Number.isNaN(value)) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${Number(value).toFixed(2)}%`;
}

export function indexRowsByDay(rows) {
  return rows.map((row) => ({
    ticker: row.ticker,
    dayMap: Object.fromEntries((row.days ?? []).map((day) => [day.date, day])),
  }));
}

export { CELL_WIDTH, LABEL_WIDTH, ROW_HEIGHT };
