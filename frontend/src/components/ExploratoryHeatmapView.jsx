import { useEffect, useMemo, useRef, useState } from 'react';
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
  const [selectedCell, setSelectedCell] = useState(null);
  const [chartNavigate, setChartNavigate] = useState(null);
  const chartRef = useRef(null);

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

  const gainLoss = useMemo(() => computeGainLossSummary(grid?.rows ?? [], tickerFilter), [grid, tickerFilter]);

  function handleCellClick({ ticker, date, cell }) {
    if (!cell?.hasData) {
      return;
    }

    setSelectedCell({ ticker, date });
    setChartNavigate({
      ticker,
      date,
      autoRun: true,
      scope: 'all',
      navKey: `${ticker}-${date}-${Date.now()}`,
    });

    requestAnimationFrame(() => {
      chartRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
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

        {gainLoss && (
          <dl className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            <Metric label="Total gains" value={formatMoney(gainLoss.totalGains)} positive />
            <Metric label="Total losses" value={formatMoney(gainLoss.totalLosses)} negative />
            <Metric label="Net P&amp;L (headline)" value={formatMoney(gainLoss.net)} emphasis />
            <Metric label="Win rate (trading days)" value={`${Number(totals?.winRateDays ?? 0).toFixed(1)}%`} />
            <Metric label="Max drawdown" value={formatMoney(totals?.maxDrawdownAbsolute ?? 0)} />
          </dl>
        )}

        {gainLoss && Object.keys(gainLoss.byTicker).length > 0 && (
          <div className="mt-6 overflow-x-auto rounded-lg border border-slate-700">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-950/80 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Ticker</th>
                  <th className="px-4 py-3">Gains</th>
                  <th className="px-4 py-3">Losses</th>
                  <th className="px-4 py-3">Net</th>
                  <th className="px-4 py-3">Traded days</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(gainLoss.byTicker)
                  .sort(([a], [b]) => a.localeCompare(b))
                  .map(([ticker, row]) => (
                    <tr key={ticker} className="border-t border-slate-800 text-slate-200">
                      <td className="px-4 py-2 font-medium">{ticker}</td>
                      <td className="px-4 py-2 text-emerald-400">{formatMoney(row.gains)}</td>
                      <td className="px-4 py-2 text-red-400">{formatMoney(row.losses)}</td>
                      <td className={`px-4 py-2 font-medium ${row.net >= 0 ? 'text-emerald-300' : 'text-red-300'}`}>
                        {formatMoney(row.net)}
                      </td>
                      <td className="px-4 py-2 text-slate-400">{row.tradedDays}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
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
            selectedCell={selectedCell}
            onCellClick={handleCellClick}
          />
        ) : (
          <p className="text-sm text-slate-500">Loading grid…</p>
        )}
      </section>

      <div ref={chartRef}>
        {selectedCell ? (
          <ChartBacktestView
            embedded
            isLoading={isLoading}
            onRun={onRun}
            navigateRequest={chartNavigate}
            onNavigateHandled={() => {}}
          />
        ) : (
          <section className="rounded-xl border border-dashed border-slate-700 bg-slate-900/40 p-8 text-center text-sm text-slate-500">
            Click a heatmap cell with data to load the ORB/PMH chart for that ticker and day.
          </section>
        )}
      </div>
    </div>
  );
}

function Metric({ label, value, emphasis = false, positive = false, negative = false }) {
  let valueClass = 'text-slate-100';
  if (emphasis) {
    valueClass = 'text-emerald-300';
  } else if (positive) {
    valueClass = 'text-emerald-400';
  } else if (negative) {
    valueClass = 'text-red-400';
  }

  return (
    <div className="rounded-lg border border-slate-700 bg-slate-950/50 p-4">
      <dt className="text-xs uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className={`mt-2 text-lg font-semibold ${valueClass}`}>{value}</dd>
    </div>
  );
}

function computeGainLossSummary(rows, tickerFilter) {
  const scopedRows = tickerFilter === 'ALL'
    ? rows
    : rows.filter((row) => row.ticker === tickerFilter);

  let totalGains = 0;
  let totalLosses = 0;
  const byTicker = {};

  for (const row of scopedRows) {
    let gains = 0;
    let losses = 0;
    let net = 0;
    let tradedDays = 0;

    for (const cell of row.days ?? []) {
      if (!cell.hasData) {
        continue;
      }

      const dayNet = cell.netTotalPnL ?? 0;
      net += dayNet;

      if (dayNet > 0) {
        gains += dayNet;
      } else if (dayNet < 0) {
        losses += dayNet;
      }

      if (cell.traded) {
        tradedDays += 1;
      }
    }

    totalGains += gains;
    totalLosses += losses;
    byTicker[row.ticker] = { gains, losses, net, tradedDays };
  }

  return {
    totalGains,
    totalLosses,
    net: totalGains + totalLosses,
    byTicker,
  };
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
