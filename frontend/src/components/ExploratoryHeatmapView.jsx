import { useEffect, useMemo, useState } from 'react';
import ChartBacktestView from './ChartBacktestView';
import HelpLink from './HelpLink';
import { fetchExploratoryGrid } from '../services/api';
import { DEFAULT_DATA_FROM, DEFAULT_DATA_TO, DEFAULT_TICKERS } from '../config/strategyDefaults';
import { formatMoney, mapNetPnlToColor } from '../utils/exploratoryHeatmapColors';
import { isMonthStart, monthLabel } from '../utils/tradingCalendar';

const CELL = 14;

export default function ExploratoryHeatmapView({
  strategyConfig,
  startingCapitalUsd,
  isLoading,
  onRun,
}) {
  const [from, setFrom] = useState(DEFAULT_DATA_FROM);
  const [to, setTo] = useState(DEFAULT_DATA_TO);
  const [tickerFilter, setTickerFilter] = useState('ALL');
  const [grid, setGrid] = useState(null);
  const [error, setError] = useState('');
  const [drillDown, setDrillDown] = useState(null);

  const tickers = DEFAULT_TICKERS;

  useEffect(() => {
    loadGrid().catch((err) => setError(err.message));
    // Reload when strategy or range changes.
  }, [from, to, strategyConfig, startingCapitalUsd]);

  async function loadGrid() {
    setError('');
    await onRun(async () => {
      const data = await fetchExploratoryGrid({
        tickers,
        from,
        to,
        strategy: strategyConfig,
        startingCapitalUsd,
      });
      setGrid(data);
    });
  }

  const tradingDays = useMemo(
    () => (grid?.tradingDays ?? []).map((d) => (typeof d === 'string' ? d : d)),
    [grid],
  );

  const { cells, totals } = useMemo(() => {
    if (!grid) {
      return { cells: {}, totals: null };
    }

    if (tickerFilter === 'ALL') {
      const byDate = {};
      for (const day of tradingDays) {
        byDate[day] = {
          date: day,
          hasData: false,
          traded: false,
          netTotalPnL: 0,
          tradeCount: 0,
        };
      }

      for (const row of grid.rows ?? []) {
        for (const cell of row.days ?? []) {
          const date = cell.date;
          const bucket = byDate[date];
          if (!bucket) {
            continue;
          }

          if (cell.hasData) {
            bucket.hasData = true;
          }

          if (cell.traded) {
            bucket.traded = true;
          }

          bucket.netTotalPnL += cell.netTotalPnL ?? 0;
          bucket.tradeCount += cell.tradeCount ?? 0;
        }
      }

      return { cells: byDate, totals: grid.totals };
    }

    const row = (grid.rows ?? []).find((r) => r.ticker === tickerFilter);
    const byDate = Object.fromEntries((row?.days ?? []).map((cell) => [cell.date, cell]));
    const rowTotals = computeRowTotals(row?.days ?? []);
    return { cells: byDate, totals: rowTotals };
  }, [grid, tickerFilter, tradingDays]);

  if (drillDown) {
    return (
      <div className="space-y-4 lg:col-span-2">
        <button
          type="button"
          className="rounded-lg border border-slate-600 px-3 py-2 text-sm text-slate-200 hover:bg-slate-800"
          onClick={() => setDrillDown(null)}
        >
          Back to overview
        </button>
        <ChartBacktestView
          isLoading={isLoading}
          onRun={onRun}
          navigateRequest={{
            ticker: drillDown.ticker,
            date: drillDown.date,
            autoRun: true,
            scope: 'all',
          }}
          onNavigateHandled={() => {}}
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 lg:col-span-2">
      <section className="rounded-xl border border-amber-800/60 bg-amber-950/20 p-4 text-sm text-amber-100">
        <p className="font-medium">Exploratory overview (not out-of-sample evidence)</p>
        <p className="mt-1 text-amber-200/80">
          Net-after-costs per day across your local data. Use Commit and Evaluate in Strategy lab for the headline verdict.
        </p>
        <p className="mt-2">
          <HelpLink page="honesty">Why this is not the headline</HelpLink>
        </p>
      </section>

      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Strategy overview</h2>
            <p className="mt-1 text-sm text-slate-400">
              {strategyConfig.name} across pulled minute data. Net P&amp;L is the headline color scale.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <label className="block text-sm">
              <span className="text-slate-400">From</span>
              <input type="date" className="mt-1 block rounded-lg border border-slate-600 bg-slate-950 px-3 py-2" value={from} onChange={(e) => setFrom(e.target.value)} />
            </label>
            <label className="block text-sm">
              <span className="text-slate-400">To</span>
              <input type="date" className="mt-1 block rounded-lg border border-slate-600 bg-slate-950 px-3 py-2" value={to} onChange={(e) => setTo(e.target.value)} />
            </label>
            <label className="block text-sm">
              <span className="text-slate-400">Ticker</span>
              <select className="mt-1 block rounded-lg border border-slate-600 bg-slate-950 px-3 py-2" value={tickerFilter} onChange={(e) => setTickerFilter(e.target.value)}>
                <option value="ALL">All tickers (aggregated)</option>
                {tickers.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </label>
            <button
              type="button"
              disabled={isLoading}
              className="rounded-lg bg-violet-700 px-4 py-2 text-sm font-medium text-white hover:bg-violet-600 disabled:opacity-50"
              onClick={() => loadGrid().catch((err) => setError(err.message))}
            >
              {isLoading ? 'Computing…' : 'Refresh'}
            </button>
          </div>
        </div>

        {totals && (
          <dl className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Metric label="Net P&amp;L (headline)" value={formatMoney(totals.netTotalPnL)} emphasis />
            <Metric label="Win rate (trading days)" value={`${Number(totals.winRateDays ?? 0).toFixed(1)}%`} />
            <Metric label="Max drawdown" value={formatMoney(totals.maxDrawdownAbsolute)} />
            <Metric label="Longest losing streak (days)" value={String(totals.longestLosingStreakDays ?? 0)} />
          </dl>
        )}

        {error && (
          <p className="mt-3 rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">{error}</p>
        )}
      </section>

      <section className="overflow-x-auto rounded-xl border border-slate-700 bg-slate-900/60 p-4">
        <div className="mb-2 flex gap-1">
          {tradingDays.map((day, index) => (
            isMonthStart(day, tradingDays[index - 1]) ? (
              <span
                key={`m-${day}`}
                className="text-[10px] text-slate-500"
                style={{ marginLeft: index * CELL }}
              >
                {monthLabel(day)}
              </span>
            ) : null
          ))}
        </div>
        <div className="flex">
          {tradingDays.map((day) => {
            const cell = cells[day];
            const color = mapNetPnlToColor(
              cell?.netTotalPnL,
              cell?.hasData,
              cell?.traded,
            );
            const drillTicker = resolveDrillTicker(grid, tickers, tickerFilter, day);
            return (
              <button
                key={day}
                type="button"
                title={buildTooltip(day, cell)}
                disabled={!cell?.hasData}
                className="shrink-0 border border-slate-950/50 p-0 hover:ring-1 hover:ring-slate-400 disabled:cursor-default"
                style={{ width: CELL, height: CELL, backgroundColor: color }}
                onClick={() => {
                  if (cell?.hasData) {
                    setDrillDown({ ticker: drillTicker, date: day });
                  }
                }}
              />
            );
          })}
        </div>
        <p className="mt-3 text-xs text-slate-500">
          Click a day with data to open the candlestick chart. Chart markers use ORB/PMH rules, not the DSL overview strategy.
        </p>
      </section>
    </div>
  );
}

function Metric({ label, value, emphasis = false }) {
  return (
    <div className="rounded-lg border border-slate-700 bg-slate-950/50 p-4">
      <dt className="text-xs uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className={`mt-2 text-lg font-semibold ${emphasis ? 'text-emerald-300' : 'text-slate-100'}`}>{value}</dd>
    </div>
  );
}

function buildTooltip(day, cell) {
  if (!cell?.hasData) {
    return `${day}: no minute data`;
  }

  if (!cell.traded) {
    return `${day}: no trades`;
  }

  return `${day}: net ${formatMoney(cell.netTotalPnL)} (${cell.tradeCount} trades)`;
}

function resolveDrillTicker(gridData, tickerList, filter, day) {
  if (filter !== 'ALL') {
    return filter;
  }

  for (const row of gridData?.rows ?? []) {
    const cell = (row.days ?? []).find((d) => d.date === day && d.hasData);
    if (cell) {
      return row.ticker;
    }
  }

  return tickerList[0];
}

function computeRowTotals(days) {
  let net = 0;
  let traded = 0;
  let wins = 0;
  let longestLosing = 0;
  let currentLosing = 0;
  let maxDrawdown = 0;
  let equity = 0;
  let peak = 0;

  for (const cell of days) {
    if (!cell.hasData) {
      continue;
    }

    const dayNet = cell.netTotalPnL ?? 0;
    net += dayNet;
    equity += dayNet;
    peak = Math.max(peak, equity);
    maxDrawdown = Math.max(maxDrawdown, peak - equity);

    if (cell.traded) {
      traded += 1;
      if (dayNet > 0) {
        wins += 1;
      }
    }

    if (dayNet < 0) {
      currentLosing += 1;
      longestLosing = Math.max(longestLosing, currentLosing);
    } else {
      currentLosing = 0;
    }
  }

  return {
    netTotalPnL: net,
    winRateDays: traded > 0 ? (wins / traded) * 100 : 0,
    maxDrawdownAbsolute: maxDrawdown,
    longestLosingStreakDays: longestLosing,
  };
}
