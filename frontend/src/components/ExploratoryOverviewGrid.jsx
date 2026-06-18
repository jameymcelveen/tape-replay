import { useMemo } from 'react';
import { formatMoney, mapNetPnlToColor } from '../utils/exploratoryHeatmapColors';
import { isMonthStart, monthLabel } from '../utils/tradingCalendar';

const ROW_HEIGHT = 14;
const CELL_WIDTH = 12;
const LABEL_WIDTH = 52;
const HEADER_HEIGHT = 24;
const PNL_CLAMP = 500;

export default function ExploratoryOverviewGrid({
  tradingDays = [],
  rows = [],
  onCellClick,
}) {
  const gridWidth = tradingDays.length * CELL_WIDTH;

  const monthMarkers = useMemo(
    () => tradingDays.map((day, index) => ({
      day,
      index,
      showLabel: isMonthStart(day, tradingDays[index - 1]),
    })),
    [tradingDays],
  );

  return (
    <div className="overflow-hidden rounded-lg border border-slate-700 bg-slate-950/60">
      <div className="flex">
        <div style={{ width: LABEL_WIDTH, minWidth: LABEL_WIDTH }} className="shrink-0 border-r border-slate-800">
          <div style={{ height: HEADER_HEIGHT }} className="border-b border-slate-800" />
        </div>
        <div className="overflow-x-auto">
          <div style={{ width: gridWidth, minWidth: gridWidth }} className="relative border-b border-slate-800">
            <div style={{ height: HEADER_HEIGHT }} className="relative">
              {monthMarkers.filter((m) => m.showLabel).map((marker) => (
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

      <div className="max-h-[320px] overflow-auto">
        <div className="flex min-w-max">
          <div
            style={{ width: LABEL_WIDTH, minWidth: LABEL_WIDTH }}
            className="sticky left-0 z-10 shrink-0 border-r border-slate-800 bg-slate-950"
          >
            {rows.map((row) => (
              <div
                key={row.ticker}
                style={{ height: ROW_HEIGHT }}
                className="truncate pr-2 text-right text-[11px] leading-[14px] font-medium text-slate-400"
                title={row.ticker}
              >
                {row.ticker}
              </div>
            ))}
          </div>

          <div style={{ width: gridWidth, minWidth: gridWidth }}>
            {rows.map((row) => (
              <div key={row.ticker} className="flex" style={{ height: ROW_HEIGHT }}>
                {tradingDays.map((day) => {
                  const cell = row.dayMap?.[day];
                  const color = mapNetPnlToColor(
                    cell?.netTotalPnL,
                    cell?.hasData,
                    cell?.traded,
                    PNL_CLAMP,
                  );
                  return (
                    <button
                      key={`${row.ticker}-${day}`}
                      type="button"
                      title={buildTooltip(row.ticker, day, cell)}
                      disabled={!cell?.hasData}
                      className="shrink-0 rounded-sm border border-slate-950/60 p-0 hover:ring-1 hover:ring-slate-400 disabled:cursor-default"
                      style={{ width: CELL_WIDTH, height: ROW_HEIGHT, backgroundColor: color }}
                      onClick={() => {
                        if (cell?.hasData) {
                          onCellClick?.({ ticker: row.ticker, date: day, cell });
                        }
                      }}
                    />
                  );
                })}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-800 px-3 py-2 text-xs text-slate-500">
        <span>Click a cell to open the candlestick chart for that ticker and day.</span>
        <OverviewLegend clamp={PNL_CLAMP} />
      </div>
    </div>
  );
}

function OverviewLegend({ clamp }) {
  const samples = [
    { net: -clamp, traded: true },
    { net: -clamp * 0.5, traded: true },
    { net: -25, traded: true },
    { net: 0, traded: false },
    { net: 25, traded: true },
    { net: clamp * 0.5, traded: true },
    { net: clamp, traded: true },
  ];

  return (
    <div className="flex items-center gap-2">
      <span>Less</span>
      <div className="flex gap-0.5">
        {samples.map((sample) => (
          <span
            key={sample.net}
            className="inline-block rounded-sm border border-slate-800"
            style={{
              width: 12,
              height: 12,
              backgroundColor: mapNetPnlToColor(sample.net, true, sample.traded, clamp),
            }}
          />
        ))}
      </div>
      <span>More</span>
    </div>
  );
}

function buildTooltip(ticker, day, cell) {
  if (!cell?.hasData) {
    return `${ticker} ${day}: no minute data`;
  }

  if (!cell.traded) {
    return `${ticker} ${day}: no trades`;
  }

  return `${ticker} ${day}: net ${formatMoney(cell.netTotalPnL)} (${cell.tradeCount} trades)`;
}

export function indexExploratoryRows(gridRows) {
  return (gridRows ?? []).map((row) => ({
    ticker: row.ticker,
    dayMap: Object.fromEntries((row.days ?? []).map((day) => [day.date, day])),
  }));
}
