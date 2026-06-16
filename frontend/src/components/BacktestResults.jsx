function formatCurrency(value) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value ?? 0);
}

function formatPercent(value) {
  return `${Number(value ?? 0).toFixed(1)}%`;
}

export default function BacktestResults({ result }) {
  if (!result) {
    return (
      <div className="rounded-xl border border-dashed border-slate-700 bg-slate-900/40 p-8 text-center text-slate-400">
        Commit and evaluate out-of-sample to see honest metrics. Exploratory runs are labeled and de-emphasized.
      </div>
    );
  }

  if (result.mode === 'evaluate') {
    return <EvaluateResults data={result.data} />;
  }

  if (result.mode === 'insample') {
    return <InSampleResults data={result.data} />;
  }

  return <ExploratoryResults data={result.data} />;
}

function InSampleResults({ data }) {
  return (
    <div className="space-y-6">
      <SampleBanner label="In-sample (suspect, tuning allowed)" tone="amber" />
      {data.metrics?.verdict && <VerdictBox text={data.metrics.verdict} />}
      <HonestMetricsGrid metrics={data.metrics} />
      <WindowSummary window={data} />
      <p className="text-sm text-slate-500">Do not treat this as evidence. Evaluate on out-of-sample dates next.</p>
      <TradesTable trades={data.trades} />
    </div>
  );
}

function EvaluateResults({ data }) {
  const oos = data.outOfSample;
  const metrics = oos.metrics;

  return (
    <div className="space-y-6">
      <SampleBanner label="Out-of-sample (headline)" tone="emerald" />
      <VerdictBox text={data.verdict} />

      {data.overfittingWarning && (
        <div className="rounded-lg border border-amber-700 bg-amber-950/40 p-4 text-sm text-amber-200">
          <p className="font-medium">Overfitting warning</p>
          <p className="mt-1">{data.overfittingWarning.message}</p>
        </div>
      )}

      <HonestMetricsGrid metrics={metrics} headline />

      <WindowSummary window={oos} />

      <div className="rounded-lg border border-slate-700 bg-slate-900/40 p-4 opacity-80">
        <p className="text-xs uppercase tracking-wide text-slate-500">In-sample (suspect, tuning allowed)</p>
        <p className="mt-2 text-sm text-slate-400">
          Net {formatPercent(data.inSample.metrics.netReturnPercent)} vs out-of-sample {formatPercent(metrics.netReturnPercent)}
        </p>
      </div>

      <TradesTable trades={oos.trades} />
    </div>
  );
}

function ExploratoryResults({ data }) {
  const metrics = data.metrics;

  return (
    <div className="space-y-6">
      <SampleBanner label="Exploratory single day (not evidence)" tone="amber" />
      {metrics?.verdict && <VerdictBox text={metrics.verdict} />}
      {metrics && <HonestMetricsGrid metrics={metrics} />}
      <p className="text-sm text-slate-400">
        {data.ticker} on {data.date} using {data.strategyName}
      </p>
      <TradesTable trades={data.trades} />
    </div>
  );
}

function HonestMetricsGrid({ metrics, headline = false }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <MetricCard label="Max Drawdown" value={formatPercent(metrics.maxDrawdownPercent)} sub={formatCurrency(metrics.maxDrawdownAbsolute)} danger headline />
      <MetricCard label="Net Return (after costs)" value={formatPercent(metrics.netReturnPercent)} sub={formatCurrency(metrics.netTotalPnL)} muted={!headline} />
      <MetricCard label="Gross P&amp;L (before costs)" value={formatCurrency(metrics.grossTotalPnL)} muted />
      <MetricCard label="Total Costs" value={formatCurrency(metrics.totalCosts)} />
      <MetricCard label="Longest Losing Streak" value={`${metrics.longestLosingStreakTrades} trades`} sub={`${metrics.longestLosingStreakDays} days`} danger />
      <MetricCard label="Recovery" value={metrics.recoveredFromMaxDrawdown ? `${metrics.daysToRecoverFromMaxDrawdown ?? 0} days` : 'Never recovered'} danger={!metrics.recoveredFromMaxDrawdown} />
      <MetricCard label="Win Rate" value={formatPercent(metrics.winRate)} />
      <MetricCard label="Expectancy / Trade" value={formatCurrency(metrics.expectancyPerTrade)} />
      <MetricCard label="Payoff Ratio" value={metrics.payoffRatio.toFixed(2)} />
      {metrics.sharpeRatio != null && <MetricCard label="Sharpe (approx)" value={metrics.sharpeRatio.toFixed(2)} />}
    </div>
  );
}

function WindowSummary({ window }) {
  return (
    <p className="text-sm text-slate-400">
      {window.ticker}: {window.startDate} to {window.endDate} ({window.trades.length} trade legs)
    </p>
  );
}

function TradesTable({ trades }) {
  return (
    <div className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
      <h3 className="mb-3 text-sm font-medium text-slate-300">Trades</h3>
      {trades.length === 0 ? (
        <p className="text-sm text-slate-400">No trades were generated.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="text-slate-400">
              <tr>
                <th className="px-2 py-2">Exit</th>
                <th className="px-2 py-2">Gross</th>
                <th className="px-2 py-2">Costs</th>
                <th className="px-2 py-2">Net</th>
                <th className="px-2 py-2">Reason</th>
              </tr>
            </thead>
            <tbody>
              {trades.map((trade, index) => (
                <tr key={index} className="border-t border-slate-800 text-slate-200">
                  <td className="px-2 py-2">{formatTime(trade.exitTime)}</td>
                  <td className="px-2 py-2">{formatCurrency(trade.grossPnL)}</td>
                  <td className="px-2 py-2">{formatCurrency(trade.totalCosts)}</td>
                  <td className={`px-2 py-2 ${trade.netPnL >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>{formatCurrency(trade.netPnL)}</td>
                  <td className="px-2 py-2">{trade.exitReason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function SampleBanner({ label, tone }) {
  const styles = tone === 'emerald'
    ? 'border-emerald-800 bg-emerald-950/40 text-emerald-200'
    : 'border-amber-800 bg-amber-950/40 text-amber-200';

  return <div className={`rounded-lg border px-4 py-2 text-sm font-medium ${styles}`}>{label}</div>;
}

function VerdictBox({ text }) {
  return (
    <div className="rounded-lg border border-slate-600 bg-slate-950 p-4 text-sm leading-relaxed text-slate-200">
      {text}
    </div>
  );
}

function MetricCard({ label, value, sub, danger = false, headline = false, muted = false }) {
  const valueClass = danger
    ? 'text-red-400'
    : headline
      ? 'text-red-400'
      : muted
        ? 'text-slate-400 text-lg'
        : 'text-slate-100';

  return (
    <div className={`rounded-xl border border-slate-700 bg-slate-900/60 p-4 ${headline ? 'ring-1 ring-red-900/50' : ''}`}>
      <p className="text-xs uppercase tracking-wide text-slate-400">{label}</p>
      <p className={`mt-2 text-2xl font-semibold ${valueClass}`}>{value}</p>
      {sub && <p className="mt-1 text-xs text-slate-500">{sub}</p>}
    </div>
  );
}

function formatTime(isoString) {
  const date = new Date(isoString);
  return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}
