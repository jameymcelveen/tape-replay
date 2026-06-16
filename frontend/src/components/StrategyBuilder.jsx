import { useState } from 'react';
import { generateDsl } from '../services/api';

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

export default function StrategyBuilder({ onRunBacktest, isLoading }) {
  const [ticker, setTicker] = useState('AAPL');
  const [date, setDate] = useState('2024-06-03');
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

  async function handlePreviewDsl() {
    setError('');
    try {
      const response = await generateDsl(buildPayload());
      setDslPreview(response.dsl);
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    try {
      await onRunBacktest({
        ticker: ticker.trim().toUpperCase(),
        date,
        strategy: buildPayload(),
      });
    } catch (err) {
      setError(err.message);
    }
  }

  function buildPayload() {
    return {
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
    };
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6 rounded-xl border border-slate-700 bg-slate-900/60 p-6">
      <div>
        <h2 className="text-lg font-semibold text-slate-100">Strategy Builder</h2>
        <p className="mt-1 text-sm text-slate-400">
          Configure a daily high breakout strategy for a single ticker and trading day.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Ticker</span>
          <input
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={ticker}
            onChange={(event) => setTicker(event.target.value)}
            placeholder="AAPL"
            required
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Date</span>
          <input
            type="date"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={date}
            onChange={(event) => setDate(event.target.value)}
            required
          />
        </label>
      </div>

      <label className="block text-sm">
        <span className="mb-1 block text-slate-300">Strategy Name</span>
        <input
          className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
          value={config.name}
          onChange={(event) => updateConfig('name', event.target.value)}
        />
      </label>

      <div className="rounded-lg border border-slate-700 bg-slate-950/70 p-4">
        <p className="text-sm font-medium text-emerald-300">Entry Trigger</p>
        <p className="mt-1 text-sm text-slate-400">Price breaks above the running daily high.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Position Size (USD)</span>
          <input
            type="number"
            min="1"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={config.positionSizeUsd}
            onChange={(event) => updateConfig('positionSizeUsd', event.target.value)}
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Stop Loss (%)</span>
          <input
            type="number"
            min="0.1"
            step="0.1"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={config.stopLossPercent}
            onChange={(event) => updateConfig('stopLossPercent', event.target.value)}
          />
        </label>
      </div>

      <div className="space-y-3">
        <p className="text-sm font-medium text-slate-300">Take Profit Targets</p>
        {config.takeProfitTargets.map((target, index) => (
          <div key={index} className="grid gap-3 rounded-lg border border-slate-700 p-3 md:grid-cols-2">
            <label className="block text-sm">
              <span className="mb-1 block text-slate-400">TP {index + 1} Percent</span>
              <input
                type="number"
                min="0.1"
                step="0.1"
                className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
                value={target.percent}
                onChange={(event) => updateTakeProfit(index, 'percent', event.target.value)}
              />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block text-slate-400">Weight (0 to 1)</span>
              <input
                type="number"
                min="0.1"
                max="1"
                step="0.1"
                className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
                value={target.weight}
                onChange={(event) => updateTakeProfit(index, 'weight', event.target.value)}
              />
            </label>
          </div>
        ))}
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Auto Exit Time</span>
          <input
            type="time"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={config.closeAllAt}
            onChange={(event) => updateConfig('closeAllAt', event.target.value)}
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Max Daily Loss (USD)</span>
          <input
            type="number"
            min="1"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={config.maxDailyLossUsd}
            onChange={(event) => updateConfig('maxDailyLossUsd', event.target.value)}
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-slate-300">Max Concurrent Trades</span>
          <input
            type="number"
            min="1"
            className="w-full rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100"
            value={config.maxConcurrentTrades}
            onChange={(event) => updateConfig('maxConcurrentTrades', event.target.value)}
          />
        </label>
      </div>

      {error && (
        <p className="rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">
          {error}
        </p>
      )}

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          onClick={handlePreviewDsl}
          className="rounded-lg border border-slate-500 px-4 py-2 text-sm text-slate-200 hover:bg-slate-800"
        >
          Preview DSL
        </button>
        <button
          type="submit"
          disabled={isLoading}
          className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
        >
          {isLoading ? 'Running Backtest...' : 'Run Backtest'}
        </button>
      </div>

      {dslPreview && (
        <pre className="overflow-x-auto rounded-lg border border-slate-700 bg-slate-950 p-4 text-xs text-emerald-200">
          {dslPreview}
        </pre>
      )}
    </form>
  );
}
