import { useEffect, useMemo, useState } from 'react';
import StrategyGrid, { indexRowsByDay } from './StrategyGrid';
import HelpLink from './HelpLink';
import { fetchMinuteCoverage, fetchStrategyHeatmap } from '../services/api';
import {
  computePercentileClamp,
  formatMetricValue,
  getDefaultClamp,
} from '../utils/heatmapColors';
import { defaultHeatmapRange } from '../utils/tradingCalendar';

const inputClass =
  'rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100';

const defaultRange = defaultHeatmapRange();

export default function StrategyHeatmapView({ isLoading, onRun, onOpenChart }) {
  const [mode, setMode] = useState('performance');
  const [tickersMode, setTickersMode] = useState('watchlist');
  const [from, setFrom] = useState(defaultRange.from);
  const [to, setTo] = useState(defaultRange.to);
  const [rule, setRule] = useState('orb');
  const [orMinutes, setOrMinutes] = useState(5);
  const [stopPct, setStopPct] = useState(5);
  const [targetPct, setTargetPct] = useState(10);
  const [shares, setShares] = useState(100);
  const [scope, setScope] = useState('all');
  const [metric, setMetric] = useState('pnlPct');
  const [clampMode, setClampMode] = useState('fixed');
  const [clampMin, setClampMin] = useState(-20);
  const [clampMax, setClampMax] = useState(20);
  const [heatmap, setHeatmap] = useState(null);
  const [coverageRows, setCoverageRows] = useState([]);
  const [error, setError] = useState('');

  const strategyConfig = useMemo(() => ({
    rule,
    params: { orMinutes, stopPct, targetPct },
    scope,
    shares,
  }), [rule, orMinutes, stopPct, targetPct, scope, shares]);

  const indexedRows = useMemo(
    () => indexRowsByDay(heatmap?.rows ?? []),
    [heatmap],
  );

  const flatCells = useMemo(
    () => (heatmap?.rows ?? []).flatMap((row) => row.days ?? []),
    [heatmap],
  );

  const clamp = useMemo(() => {
    if (clampMode === 'percentile') {
      return computePercentileClamp(flatCells, metric, 95);
    }

    return { min: clampMin, max: clampMax };
  }, [clampMode, clampMin, clampMax, flatCells, metric]);

  const coverageByTicker = useMemo(() => {
    const map = {};
    for (const row of coverageRows) {
      const ticker = row.ticker;
      map[ticker] ??= {};
      map[ticker][row.date] = row.status;
    }
    return map;
  }, [coverageRows]);

  useEffect(() => {
    if (clampMode === 'fixed') {
      const defaults = getDefaultClamp(metric);
      setClampMin(defaults.min);
      setClampMax(defaults.max);
    }
  }, [metric, clampMode]);

  async function loadGrid() {
    setError('');
    const tickers = tickersMode;

    await onRun(async () => {
      const [heatmapResponse, coverageResponse] = await Promise.all([
        fetchStrategyHeatmap({
          tickers,
          from,
          to,
          strategy: strategyConfig,
          metric,
        }),
        fetchMinuteCoverage({ startDate: from, endDate: to }),
      ]);

      setHeatmap(heatmapResponse);
      setCoverageRows(coverageResponse);
    });
  }

  useEffect(() => {
    loadGrid().catch((err) => setError(err.message));
  }, [from, to, tickersMode, rule, orMinutes, stopPct, targetPct, shares, scope]);

  function handleCellClick({ ticker, date, cell }) {
    if (mode === 'coverage' || !cell?.hasData) {
      return;
    }

    onOpenChart({
      ticker,
      date,
      rule,
      orMinutes,
      stopPct,
      targetPct,
      shares,
      scope,
      autoRun: true,
    });
  }

  const tradingDays = (heatmap?.tradingDays ?? []).map((day) => (
    typeof day === 'string' ? day : day
  ));

  return (
    <div className="space-y-6 lg:col-span-2">
      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Strategy heatmap</h2>
            <p className="mt-1 text-sm text-slate-400">
              Per-ticker, per-day strategy results across your local database. Greener is better; redder is worse.
            </p>
            <p className="mt-2">
              <HelpLink page="chartBacktest">ORB / PMH rules &amp; capture % →</HelpLink>
            </p>
          </div>
          <div className="flex rounded-lg border border-slate-700 p-1 text-sm">
            <button
              type="button"
              className={`rounded-md px-3 py-1.5 ${mode === 'coverage' ? 'bg-slate-700 text-white' : 'text-slate-400'}`}
              onClick={() => setMode('coverage')}
            >
              Coverage
            </button>
            <button
              type="button"
              className={`rounded-md px-3 py-1.5 ${mode === 'performance' ? 'bg-slate-700 text-white' : 'text-slate-400'}`}
              onClick={() => setMode('performance')}
            >
              Performance
            </button>
          </div>
        </div>

        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block text-sm">
            <span className="text-slate-400">Tickers</span>
            <select className={`${inputClass} mt-1 w-full`} value={tickersMode} onChange={(e) => setTickersMode(e.target.value)}>
              <option value="watchlist">Watchlist (Recording jobs)</option>
              <option value="all-with-data">All with minute data</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">From</span>
            <input type="date" className={`${inputClass} mt-1 w-full`} value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">To</span>
            <input type="date" className={`${inputClass} mt-1 w-full`} value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Metric</span>
            <select className={`${inputClass} mt-1 w-full`} value={metric} onChange={(e) => setMetric(e.target.value)}>
              <option value="pnlPct">P&amp;L %</option>
              <option value="capturePct">Capture %</option>
              <option value="pnlDollar">P&amp;L $</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Rule</span>
            <select className={`${inputClass} mt-1 w-full`} value={rule} onChange={(e) => setRule(e.target.value)}>
              <option value="orb">ORB</option>
              <option value="pmh">PMH</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">OR minutes</span>
            <input type="number" min={1} className={`${inputClass} mt-1 w-full`} value={orMinutes} onChange={(e) => setOrMinutes(Number(e.target.value))} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Stop %</span>
            <input type="number" min={0} step={0.1} className={`${inputClass} mt-1 w-full`} value={stopPct} onChange={(e) => setStopPct(Number(e.target.value))} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Target %</span>
            <input type="number" min={0} step={0.1} className={`${inputClass} mt-1 w-full`} value={targetPct} onChange={(e) => setTargetPct(Number(e.target.value))} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Shares</span>
            <input type="number" min={1} className={`${inputClass} mt-1 w-full`} value={shares} onChange={(e) => setShares(Number(e.target.value))} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Scope</span>
            <select className={`${inputClass} mt-1 w-full`} value={scope} onChange={(e) => setScope(e.target.value)}>
              <option value="regular">Regular session</option>
              <option value="all">Full session</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Intensity clamp</span>
            <select className={`${inputClass} mt-1 w-full`} value={clampMode} onChange={(e) => setClampMode(e.target.value)}>
              <option value="fixed">Fixed domain</option>
              <option value="percentile">95th percentile (visible grid)</option>
            </select>
          </label>
          {clampMode === 'fixed' && (
            <>
              <label className="block text-sm">
                <span className="text-slate-400">Clamp min</span>
                <input type="number" className={`${inputClass} mt-1 w-full`} value={clampMin} onChange={(e) => setClampMin(Number(e.target.value))} />
              </label>
              <label className="block text-sm">
                <span className="text-slate-400">Clamp max</span>
                <input type="number" className={`${inputClass} mt-1 w-full`} value={clampMax} onChange={(e) => setClampMax(Number(e.target.value))} />
              </label>
            </>
          )}
        </div>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            disabled={isLoading}
            className="rounded-lg bg-violet-700 px-4 py-2 text-sm font-medium text-white hover:bg-violet-600 disabled:opacity-50"
            onClick={() => loadGrid().catch((err) => setError(err.message))}
          >
            {isLoading ? 'Computing…' : 'Refresh grid'}
          </button>
          {heatmap?.strategyConfigHash && (
            <span className="text-xs text-slate-500">
              cache key {heatmap.strategyConfigHash.slice(0, 12)}…
            </span>
          )}
        </div>

        {error && (
          <p className="mt-3 rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">{error}</p>
        )}
      </section>

      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-slate-400">
            {indexedRows.length} tickers · {tradingDays.length} trading days
            {mode === 'performance' ? ' · click a cell to open chart backtest' : ' · recording coverage'}
          </p>
          {mode === 'performance' && (
            <HeatmapLegend metric={metric} clamp={clamp} />
          )}
          {mode === 'coverage' && (
            <div className="flex gap-3 text-xs text-slate-400">
              <span><span className="mr-1 inline-block h-3 w-3 rounded-sm bg-emerald-500/80" /> Done</span>
              <span><span className="mr-1 inline-block h-3 w-3 rounded-sm bg-amber-500/80" /> Pending</span>
              <span><span className="mr-1 inline-block h-3 w-3 rounded-sm bg-slate-500/55" /> Skipped / none</span>
            </div>
          )}
        </div>

        {heatmap ? (
          <StrategyGrid
            mode={mode}
            tradingDays={tradingDays}
            rows={indexedRows}
            metric={metric}
            clamp={clamp}
            coverageByTicker={coverageByTicker}
            onCellClick={handleCellClick}
          />
        ) : (
          <p className="text-sm text-slate-500">Loading grid…</p>
        )}
      </section>
    </div>
  );
}

function HeatmapLegend({ metric, clamp }) {
  const stops = [-1, -0.5, -0.1, 0, 0.1, 0.5, 1];
  const domain = Math.max(Math.abs(clamp.min), Math.abs(clamp.max));

  return (
    <div className="flex items-center gap-2 text-xs text-slate-400">
      <span>{formatMetricValue(-domain, metric)}</span>
      <div className="flex h-3 w-40 overflow-hidden rounded-sm border border-slate-700">
        {stops.map((stop) => {
          const value = stop * domain;
          const color = value === 0
            ? 'rgb(148, 163, 184)'
            : (value > 0
              ? `rgba(34, 197, 94, ${0.25 + Math.abs(stop) * 0.75})`
              : `rgba(239, 68, 68, ${0.25 + Math.abs(stop) * 0.75})`);
          return <span key={stop} className="flex-1" style={{ backgroundColor: color }} />;
        })}
      </div>
      <span>{formatMetricValue(domain, metric)}</span>
    </div>
  );
}
