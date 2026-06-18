import { useCallback, useEffect, useRef, useState } from 'react';
import PacManProgress from './PacManProgress';
import HelpLink from './HelpLink';
import { fetchMinuteCoverage, queueMinute, scrapeData } from '../services/api';
import { DEFAULT_PULL_FROM, DEFAULT_PULL_TO } from '../config/strategyDefaults';
import { buildDefaultPullRows, computeCoveragePercent } from '../utils/coverageProgress';

const inputClass =
  'rounded-lg border border-slate-600 bg-slate-950 px-3 py-2 text-slate-100';

function emptyRow() {
  return {
    id: crypto.randomUUID(),
    ticker: '',
    dateFrom: DEFAULT_PULL_FROM,
    dateTo: DEFAULT_PULL_TO,
  };
}

export default function DataPullView({ backendStatus }) {
  const [rows, setRows] = useState(() => buildDefaultPullRows());
  const [coverage, setCoverage] = useState([]);
  const [statusByRow, setStatusByRow] = useState({});
  const [error, setError] = useState('');
  const [pullingAll, setPullingAll] = useState(false);
  const pollRef = useRef(null);

  const refreshCoverage = useCallback(async () => {
    if (rows.length === 0) {
      setCoverage([]);
      return;
    }

    const from = rows.reduce((min, row) => (row.dateFrom < min ? row.dateFrom : min), rows[0].dateFrom);
    const to = rows.reduce((max, row) => (row.dateTo > max ? row.dateTo : max), rows[0].dateTo);
    const data = await fetchMinuteCoverage({ startDate: from, endDate: to });
    setCoverage(data);
  }, [rows]);

  useEffect(() => {
    if (backendStatus !== 'ok') {
      return undefined;
    }

    refreshCoverage().catch(() => {});
    pollRef.current = window.setInterval(() => {
      refreshCoverage().catch(() => {});
    }, 3000);

    return () => {
      if (pollRef.current) {
        window.clearInterval(pollRef.current);
      }
    };
  }, [backendStatus, refreshCoverage]);

  function updateRow(id, patch) {
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)));
  }

  function addRow() {
    setRows((current) => [...current, emptyRow()]);
  }

  function removeRow(id) {
    setRows((current) => current.filter((row) => row.id !== id));
  }

  async function recordUntilSettled() {
    for (let round = 0; round < 500; round += 1) {
      const result = await scrapeData(20);
      await refreshCoverage();
      if ((result.recorded ?? 0) === 0) {
        break;
      }
    }
  }

  async function pullRow(row) {
    const ticker = row.ticker.trim().toUpperCase();
    if (!ticker) {
      throw new Error('Ticker is required on each row.');
    }

    setStatusByRow((current) => ({ ...current, [row.id]: 'Queued' }));
    await queueMinute({
      tickers: [ticker],
      dateFrom: row.dateFrom,
      dateTo: row.dateTo,
    });

    setStatusByRow((current) => ({ ...current, [row.id]: 'Recording' }));
    await recordUntilSettled();
    await refreshCoverage();
    setStatusByRow((current) => ({ ...current, [row.id]: 'Done' }));
  }

  async function pullAll() {
    setError('');
    setPullingAll(true);
    try {
      for (const row of rows) {
        await pullRow(row);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setPullingAll(false);
    }
  }

  const isEmpty = rows.length === 0;

  return (
    <div className="space-y-6 lg:col-span-2">
      <section className="rounded-xl border border-slate-700 bg-slate-900/60 p-6">
        <h2 className="text-lg font-semibold">Data pull</h2>
        <p className="mt-1 text-sm text-slate-400">
          Queue and record minute bars from your configured provider. Weekends may show as Skipped.
        </p>
        <p className="mt-2">
          <HelpLink page="collectingData">Recording pipeline guide</HelpLink>
        </p>

        {isEmpty ? (
          <p className="mt-6 rounded-lg border border-dashed border-slate-700 p-6 text-center text-sm text-slate-400">
            No pull rows yet. Add a ticker row to queue coverage.
          </p>
        ) : (
          <div className="mt-6 space-y-3">
            {rows.map((row) => {
              const percent = computeCoveragePercent(coverage, row.ticker, row.dateFrom, row.dateTo);
              const status = statusByRow[row.id] ?? (percent >= 100 ? 'Done' : 'Idle');
              return (
                <div
                  key={row.id}
                  className="flex flex-wrap items-center gap-4 rounded-lg border border-slate-700 bg-slate-950/40 p-4"
                >
                  <PacManProgress
                    percent={percent}
                    label={`${row.ticker} coverage ${percent} percent`}
                    animate={status === 'Recording'}
                  />
                  <label className="block min-w-[5rem] text-sm">
                    <span className="text-slate-400">Ticker</span>
                    <input
                      className={`${inputClass} mt-1 w-full uppercase`}
                      value={row.ticker}
                      onChange={(e) => updateRow(row.id, { ticker: e.target.value })}
                      aria-label="Ticker symbol"
                    />
                  </label>
                  <label className="block text-sm">
                    <span className="text-slate-400">From</span>
                    <input
                      type="date"
                      className={`${inputClass} mt-1`}
                      value={row.dateFrom}
                      onChange={(e) => updateRow(row.id, { dateFrom: e.target.value })}
                    />
                  </label>
                  <label className="block text-sm">
                    <span className="text-slate-400">To</span>
                    <input
                      type="date"
                      className={`${inputClass} mt-1`}
                      value={row.dateTo}
                      onChange={(e) => updateRow(row.id, { dateTo: e.target.value })}
                    />
                  </label>
                  <div className="flex flex-col gap-1 text-sm">
                    <span className="text-slate-400">Status</span>
                    <span className="text-slate-200">{status}</span>
                  </div>
                  <button
                    type="button"
                    className="rounded-lg border border-slate-600 px-3 py-2 text-sm text-slate-200 hover:bg-slate-800"
                    disabled={pullingAll || backendStatus !== 'ok'}
                    onClick={() => pullRow(row).catch((err) => setError(err.message))}
                  >
                    Pull
                  </button>
                  <button
                    type="button"
                    className="rounded-lg border border-slate-700 px-2 py-2 text-xs text-slate-500 hover:text-red-300"
                    onClick={() => removeRow(row.id)}
                    aria-label={`Remove ${row.ticker} row`}
                  >
                    Remove
                  </button>
                </div>
              );
            })}
          </div>
        )}

        <div className="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            className="rounded-lg border border-slate-600 px-3 py-2 text-sm text-slate-200 hover:bg-slate-800"
            onClick={addRow}
          >
            Add row
          </button>
          <button
            type="button"
            disabled={pullingAll || isEmpty || backendStatus !== 'ok'}
            className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-600 disabled:opacity-50"
            onClick={() => pullAll().catch((err) => setError(err.message))}
          >
            {pullingAll ? 'Pulling all…' : 'Pull all'}
          </button>
        </div>

        {error && (
          <p className="mt-3 rounded-lg border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">{error}</p>
        )}
      </section>
    </div>
  );
}
