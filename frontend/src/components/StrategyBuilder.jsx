import { useState } from 'react';
import { generateDsl } from '../services/api';
import HelpLink from './HelpLink';

const defaultConfig = {
  name: 'Daily High Breakout',
  entryTrigger: 'PriceBreaksAboveDailyHigh',
  positionSizeUsd: 1000,
  stopLossPercent: 1,
  takeProfitTargets: [
    { percent: 2, weight: 0.5 },
    { percent: 4, weight: 0.5 },
  ],
  closeAllAt: '14:00',
  maxDailyLossUsd: 500,
  maxConcurrentTrades: 3,
};

export default function StrategyBuilder({
  onRunExploratory,
  onCommit,
  onEvaluate,
  isLoading,
  commitId,
}) {
  const [ticker, setTicker] = useState('AAPL');
  const [exploreDate, setExploreDate] = useState('2024-06-03');
  const [inSampleStart, setInSampleStart] = useState('2024-06-01');
  const [inSampleEnd, setInSampleEnd] = useState('2024-06-07');
  const [outSampleStart, setOutSampleStart] = useState('2024-06-10');
  const [outSampleEnd, setOutSampleEnd] = useState('2024-06-14');
  const [startingCapital, setStartingCapital] = useState(25000);
  const [config, setConfig] = useState(defaultConfig);
  const [dslPreview, setDslPreview] = useState('');
  const [error, setError] = useState('');

  function updateConfig(field, value) {
    setConfig((current) => ({ ...current, [field]: value }));
  }

  function updateTakeProfit(index, field, value) {
    setConfig((current) => {
      const targets = [...current.takeProfitTargets];
      targets[index] = { ...targets[index], [field]: Number(value) };
      return { ...current, takeProfitTargets: targets };
    });
  }

  function buildPayload() {
    return {
      ticker: ticker.trim().toUpperCase(),
      strategy: {
        name: config.name,
        entryTrigger: config.entryTrigger,
        positionSizeUsd: Number(config.positionSizeUsd),
        stopLossPercent: Number(config.stopLossPercent),
        takeProfitTargets: config.takeProfitTargets.map((target) => ({
          percent: Number(target.percent),
          weight: Number(target.weight),
        })),
        closeAllAt: config.closeAllAt,
        maxDailyLossUsd: Number(config.maxDailyLossUsd),
        maxConcurrentTrades: Number(config.maxConcurrentTrades),
      },
      startingCapitalUsd: Number(startingCapital),
    };
  }

  async function handlePreviewDsl() {
    setError('');
    try {
      const response = await generateDsl(buildPayload().strategy);
      setDslPreview(response.dsl);
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleExploratory(event) {
    event.preventDefault();
    setError('');
    try {
      await onRunExploratory({ ...buildPayload(), date: exploreDate });
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleCommit() {
    setError('');
    try {
      await onCommit({
        ...buildPayload(),
        inSampleStart,
        inSampleEnd,
      });
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleEvaluate() {
    if (!commitId) {
      setError('Commit a strategy on the in-sample window before evaluating out-of-sample.');
      return;
    }

    setError('');
    try {
      await onEvaluate({
        commitId,
        outOfSampleStart: outSampleStart,
        outOfSampleEnd: outSampleEnd,
        startingCapitalUsd: Number(startingCapital),
      });
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <form onSubmit={handleExploratory} className="space-y-6 rounded-xl border border-slate-700 bg-slate-900/60 p-6">
      <div>
        <h2 className="text-lg font-semibold text-slate-100">Strategy Builder</h2>
        <p className="mt-1 text-sm text-slate-400">
          Tune on in-sample dates, commit, then score on out-of-sample. Single-day runs are exploratory only.
        </p>
        <p className="mt-2">
          <HelpLink page="strategyLab">Strategy lab guide →</HelpLink>
          {' · '}
          <HelpLink page="honesty">Why exploratory ≠ evidence</HelpLink>
        </p>
      </div>

      <label className="block text-sm">
        <span className="mb-1 block text-slate-300">Ticker</span>
        <input
          className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
          value={ticker}
          onChange={(event) => setTicker(event.target.value)}
          required
        />
      </label>

      <label className="block text-sm">
        <span className="mb-1 block text-slate-300">Starting Capital (USD)</span>
        <input
          type="number"
          min="1000"
          className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
          value={startingCapital}
          onChange={(event) => setStartingCapital(event.target.value)}
        />
      </label>

      <div className="rounded-lg border border-amber-800/60 bg-amber-950/30 p-4">
        <p className="text-sm font-medium text-amber-300">Exploratory (not evidence)</p>
        <label className="mt-2 block text-sm">
          <span className="mb-1 block text-slate-400">Single day (dev only)</span>
          <input
            type="date"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={exploreDate}
            onChange={(event) => setExploreDate(event.target.value)}
          />
        </label>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <fieldset className="rounded-lg border border-slate-700 p-4">
          <legend className="px-1 text-sm font-medium text-slate-300">In-sample (tune here)</legend>
          <div className="mt-2 space-y-2">
            <input type="date" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={inSampleStart} onChange={(e) => setInSampleStart(e.target.value)} />
            <input type="date" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={inSampleEnd} onChange={(e) => setInSampleEnd(e.target.value)} />
          </div>
        </fieldset>
        <fieldset className="rounded-lg border border-emerald-800/50 p-4">
          <legend className="px-1 text-sm font-medium text-emerald-300">Out-of-sample (headline)</legend>
          <div className="mt-2 space-y-2">
            <input type="date" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={outSampleStart} onChange={(e) => setOutSampleStart(e.target.value)} />
            <input type="date" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={outSampleEnd} onChange={(e) => setOutSampleEnd(e.target.value)} />
          </div>
        </fieldset>
      </div>

      <label className="block text-sm">
        <span className="mb-1 block text-slate-300">Strategy Name</span>
        <input className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.name} onChange={(e) => updateConfig('name', e.target.value)} />
      </label>

      <div className="rounded-lg border border-slate-700 bg-slate-950/70 p-4">
        <p className="text-sm font-medium text-emerald-300">Entry Trigger</p>
        <p className="mt-1 text-sm text-slate-400">Price breaks above running daily high at bar open (no look-ahead).</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Position Size (USD)</span>
          <input type="number" min="1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.positionSizeUsd} onChange={(e) => updateConfig('positionSizeUsd', e.target.value)} />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Stop Loss (%)</span>
          <input type="number" min="0.1" step="0.1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.stopLossPercent} onChange={(e) => updateConfig('stopLossPercent', e.target.value)} />
        </label>
      </div>

      <div className="space-y-3">
        <p className="text-sm font-medium text-slate-300">Take Profit Targets</p>
        {config.takeProfitTargets.map((target, index) => (
          <div key={index} className="grid gap-3 rounded-lg border border-slate-700 p-3 md:grid-cols-2">
            <label className="block text-sm">
              <span className="mb-1 block text-slate-400">TP {index + 1} Percent</span>
              <input type="number" min="0.1" step="0.1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={target.percent} onChange={(e) => updateTakeProfit(index, 'percent', e.target.value)} />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block text-slate-400">Weight (0 to 1)</span>
              <input type="number" min="0.1" max="1" step="0.1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={target.weight} onChange={(e) => updateTakeProfit(index, 'weight', e.target.value)} />
            </label>
          </div>
        ))}
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Auto Exit Time</span>
          <input type="time" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.closeAllAt} onChange={(e) => updateConfig('closeAllAt', e.target.value)} />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Max Daily Loss (USD)</span>
          <input type="number" min="1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.maxDailyLossUsd} onChange={(e) => updateConfig('maxDailyLossUsd', e.target.value)} />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Max Concurrent Trades</span>
          <input type="number" min="1" className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100" value={config.maxConcurrentTrades} onChange={(e) => updateConfig('maxConcurrentTrades', e.target.value)} />
        </label>
      </div>

      {error && <p className="rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">{error}</p>}

      <div className="flex flex-wrap gap-3">
        <button type="button" onClick={handlePreviewDsl} className="rounded-lg border border-slate-500 px-4 py-2 text-sm text-slate-200 hover:bg-slate-800">Preview DSL</button>
        <button type="submit" disabled={isLoading} className="rounded-lg border border-amber-700 px-4 py-2 text-sm text-amber-200 hover:bg-amber-950/50 disabled:opacity-50">Exploratory Day</button>
        <button type="button" onClick={handleCommit} disabled={isLoading} className="rounded-lg bg-violet-700 px-4 py-2 text-sm font-medium text-white hover:bg-violet-600 disabled:opacity-50">Commit In-Sample</button>
        <button type="button" onClick={handleEvaluate} disabled={isLoading || !commitId} className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-600 disabled:opacity-50">Evaluate Out-of-Sample</button>
      </div>

      {commitId && <p className="text-xs text-slate-400">Frozen commit: {commitId}</p>}

      {dslPreview && <pre className="overflow-x-auto rounded-lg border border-slate-700 bg-slate-950 p-4 text-xs text-emerald-200">{dslPreview}</pre>}
    </form>
  );
}
