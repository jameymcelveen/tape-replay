import { useState } from 'react';
import BacktestChart from './BacktestChart';
import HelpLink from './HelpLink';
import { runChartBacktest } from '../services/api';
import { easternToUtcIso, formatEasternDateTime } from '../utils/chartTime';

const inputClass =
  'rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100';

function formatMoney(value) {
  if (value == null || Number.isNaN(value)) {
    return '—';
  }

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
  }).format(value);
}

function formatPct(value) {
  if (value == null || Number.isNaN(value)) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${Number(value).toFixed(2)}%`;
}

export default function ChartBacktestView({ isLoading, onRun }) {
  const [ticker, setTicker] = useState('VSME');
  const [date, setDate] = useState('2026-06-16');
  const [fromTime, setFromTime] = useState('04:00');
  const [toTime, setToTime] = useState('20:00');
  const [rule, setRule] = useState('orb');
  const [orMinutes, setOrMinutes] = useState(5);
  const [stopPct, setStopPct] = useState(5);
  const [targetPct, setTargetPct] = useState(10);
  const [shares, setShares] = useState(100);
  const [scope, setScope] = useState('all');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');

  async function handleRun() {
    setError('');
    const payload = {
      ticker: ticker.trim().toUpperCase(),
      from: easternToUtcIso(date, fromTime),
      to: easternToUtcIso(date, toTime),
      scope,
      rule,
      params: { orMinutes, stopPct, targetPct },
      shares,
    };

    await onRun(async () => {
      const data = await runChartBacktest(payload);
      setResult(data);
    });
  }

  function applyRegularHours() {
    setFromTime('09:30');
    setToTime('16:00');
    setScope('regular');
  }

  function applyFullSession() {
    setFromTime('04:00');
    setToTime('20:00');
    setScope('all');
  }

  const trade = result?.trade;
  const hindsight = result?.hindsight;

  return (
    <div className="space-y-6 lg:col-span-2">
      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-6">
        <h2 className="text-lg font-semibold">Chart backtest</h2>
        <p className="mt-1 text-sm text-slate-400">
          Run ORB or PMH against stored minute bars and compare to perfect hindsight.
        </p>
        <p className="mt-2">
          <HelpLink page="chartBacktest">How to read the chart &amp; markers →</HelpLink>
        </p>

        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block text-sm">
            <span className="text-slate-400">Ticker</span>
            <input className={`${inputClass} mt-1 w-full`} value={ticker} onChange={(e) => setTicker(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Date (ET)</span>
            <input type="date" className={`${inputClass} mt-1 w-full`} value={date} onChange={(e) => setDate(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">From (ET)</span>
            <input type="time" className={`${inputClass} mt-1 w-full`} value={fromTime} onChange={(e) => setFromTime(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">To (ET)</span>
            <input type="time" className={`${inputClass} mt-1 w-full`} value={toTime} onChange={(e) => setToTime(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="text-slate-400">Rule</span>
            <select className={`${inputClass} mt-1 w-full`} value={rule} onChange={(e) => setRule(e.target.value)}>
              <option value="orb">ORB (opening range breakout)</option>
              <option value="pmh">PMH (premarket high breakout)</option>
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
              <option value="regular">Regular session only</option>
              <option value="all">Pre + regular + post</option>
            </select>
          </label>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button type="button" className="rounded-lg border border-slate-600 px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800" onClick={applyRegularHours}>
            RTH preset
          </button>
          <button type="button" className="rounded-lg border border-slate-600 px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800" onClick={applyFullSession}>
            Full session preset
          </button>
          <button
            type="button"
            disabled={isLoading}
            className="rounded-lg bg-violet-700 px-4 py-2 text-sm font-medium text-white hover:bg-violet-600 disabled:opacity-50"
            onClick={() => handleRun().catch((err) => setError(err.message))}
          >
            {isLoading ? 'Running…' : 'Run backtest'}
          </button>
        </div>

        {error && (
          <p className="mt-3 rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">{error}</p>
        )}
      </section>

      {result && (
        <>
          <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-6">
            <h3 className="text-base font-semibold">Results</h3>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <div className="rounded-lg border border-slate-700 bg-slate-950/50 p-4">
                <h4 className="text-sm font-medium text-sky-300">Strategy trade</h4>
                {trade?.taken ? (
                  <dl className="mt-3 space-y-2 text-sm">
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">Entry</dt>
                      <dd>{formatEasternDateTime(trade.entryTime)} @ {formatMoney(trade.entryPrice)}</dd>
                    </div>
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">Exit</dt>
                      <dd>
                        {formatEasternDateTime(trade.exitTime)} @ {formatMoney(trade.exitPrice)}
                        <span className="ml-1 text-amber-300">({trade.exitReason})</span>
                      </dd>
                    </div>
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">P&amp;L</dt>
                      <dd className={trade.pnl >= 0 ? 'text-emerald-400' : 'text-red-400'}>
                        {formatMoney(trade.pnl)} ({formatPct(trade.pct)})
                      </dd>
                    </div>
                  </dl>
                ) : (
                  <p className="mt-3 text-sm text-slate-400">No trade: {trade?.reason ?? 'unknown'}</p>
                )}
              </div>

              <div className="rounded-lg border border-slate-700 bg-slate-950/50 p-4">
                <h4 className="text-sm font-medium text-violet-300">Perfect hindsight</h4>
                {hindsight?.buyTime ? (
                  <dl className="mt-3 space-y-2 text-sm">
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">Ideal buy</dt>
                      <dd>{formatEasternDateTime(hindsight.buyTime)} @ {formatMoney(hindsight.buyPrice)}</dd>
                    </div>
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">Ideal sell</dt>
                      <dd>{formatEasternDateTime(hindsight.sellTime)} @ {formatMoney(hindsight.sellPrice)}</dd>
                    </div>
                    <div className="flex justify-between gap-4">
                      <dt className="text-slate-400">Ceiling</dt>
                      <dd className="text-violet-300">{formatMoney(hindsight.profPerShare)} / share ({formatPct(hindsight.pct)})</dd>
                    </div>
                    <div className="flex justify-between gap-4 border-t border-slate-700 pt-2">
                      <dt className="text-slate-400">Capture</dt>
                      <dd className="font-medium text-slate-100">{formatPct(result.summary?.capturePct)}</dd>
                    </div>
                  </dl>
                ) : (
                  <p className="mt-3 text-sm text-slate-400">Not enough movement for a hindsight pair.</p>
                )}
              </div>
            </div>
            <p className="mt-3 text-xs text-slate-500">{result.bars?.length ?? 0} minute bars plotted (sparse series — no gap filling).</p>
          </section>

          <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
            <BacktestChart bars={result.bars} trade={trade} hindsight={hindsight} />
          </section>
        </>
      )}
    </div>
  );
}
