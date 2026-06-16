function formatCurrency(value) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value);
}

function formatPercent(value) {
  return `${Number(value).toFixed(1)}%`;
}

export default function BacktestResults({ result }) {
  if (!result) {
    return (
      <div className="rounded-xl border border-dashed border-slate-700 bg-slate-900/40 p-8 text-center text-slate-400">
        Run a backtest to see trade results, P&amp;L, and the trade log.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold text-slate-100">Backtest Results</h2>
        <p className="mt-1 text-sm text-slate-400">
          {result.ticker} on {result.date} using {result.strategyName}
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <MetricCard label="Total P&amp;L" value={formatCurrency(result.totalPnL)} highlight />
        <MetricCard label="Win Rate" value={formatPercent(result.winRate)} />
        <MetricCard label="Max Drawdown" value={formatCurrency(result.maxDrawdown)} />
      </div>

      <div className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
        <h3 className="mb-3 text-sm font-medium text-slate-300">Trades</h3>
        {result.trades.length === 0 ? (
          <p className="text-sm text-slate-400">No trades were generated for this session.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="text-slate-400">
                <tr>
                  <th className="px-2 py-2">Entry</th>
                  <th className="px-2 py-2">Exit</th>
                  <th className="px-2 py-2">Entry Price</th>
                  <th className="px-2 py-2">Exit Price</th>
                  <th className="px-2 py-2">Qty</th>
                  <th className="px-2 py-2">P&amp;L</th>
                  <th className="px-2 py-2">Reason</th>
                </tr>
              </thead>
              <tbody>
                {result.trades.map((trade, index) => (
                  <tr key={index} className="border-t border-slate-800 text-slate-200">
                    <td className="px-2 py-2">{formatTime(trade.entryTime)}</td>
                    <td className="px-2 py-2">{formatTime(trade.exitTime)}</td>
                    <td className="px-2 py-2">{trade.entryPrice.toFixed(2)}</td>
                    <td className="px-2 py-2">{trade.exitPrice.toFixed(2)}</td>
                    <td className="px-2 py-2">{trade.quantity}</td>
                    <td className={`px-2 py-2 ${trade.pnL >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                      {formatCurrency(trade.pnL)}
                    </td>
                    <td className="px-2 py-2">{trade.exitReason}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
        <h3 className="mb-3 text-sm font-medium text-slate-300">Trade Log</h3>
        <ul className="max-h-64 space-y-1 overflow-y-auto font-mono text-xs text-slate-300">
          {result.tradeLog.map((line, index) => (
            <li key={index}>{line}</li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function MetricCard({ label, value, highlight = false }) {
  return (
    <div className="rounded-xl border border-slate-700 bg-slate-900/60 p-4">
      <p className="text-xs uppercase tracking-wide text-slate-400">{label}</p>
      <p className={`mt-2 text-2xl font-semibold ${highlight ? 'text-emerald-400' : 'text-slate-100'}`}>
        {value}
      </p>
    </div>
  );
}

function formatTime(isoString) {
  const date = new Date(isoString);
  return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}
