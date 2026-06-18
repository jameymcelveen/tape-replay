import { useEffect, useMemo, useState } from 'react';
import ChartBacktestView from './ChartBacktestView';
import ExploratoryOverviewGrid, { indexExploratoryRows } from './ExploratoryOverviewGrid';
import HelpLink from './HelpLink';
import { fetchExploratoryGrid } from '../services/api';
import { DEFAULT_DATA_FROM, DEFAULT_DATA_TO, DEFAULT_TICKERS } from '../config/strategyDefaults';
import { formatMoney } from '../utils/exploratoryHeatmapColors';

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

  const displayRows = useMemo(() => {
    const indexed = indexExploratoryRows(grid?.rows);
    if (tickerFilter === 'ALL') {
      return indexed;
    }

    return indexed.filter((row) => row.ticker === tickerFilter);
  }, [grid, tickerFilter]);

  const totals = useMemo(() => {
    if (!grid) {
      return null;
    }

    if (tickerFilter === 'ALL') {
      return grid.totals;
    }

    const row = (grid.rows ?? []).find((r) => r.ticker === tickerFilter);
    return computeRowTotals(row?.days ?? []);
  }, [grid, tickerFilter]);

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
              {strategyConfig.name} across pulled minute data. One row per ticker, net P&amp;L color scale.
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
              <span className="text-slate-400">Show</span>
              <select className="mt-1 block rounded-lg border border-slate-600 bg-slate-950 px-3 py-2" value={tickerFilter} onChange={(e) => setTickerFilter(e.target.value)}>
                <option value="ALL">All tickers</option>
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

      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
        {grid ? (
          <ExploratoryOverviewGrid
            tradingDays={tradingDays}
            rows={displayRows}
            onCellClick={({ ticker, date }) => setDrillDown({ ticker, date })}
          />
        ) : (
          <p className="text-sm text-slate-500">Loading grid…</p>
        )}
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
