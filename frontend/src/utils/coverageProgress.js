import {
  DEFAULT_PULL_FROM,
  DEFAULT_PULL_TO,
  DEFAULT_TICKERS,
} from '../config/strategyDefaults';
import { enumerateTradingDays } from './tradingCalendar';

/**
 * Coverage percent for a row: (Done + Skipped) / trading days in range.
 */
export function computeCoveragePercent(coverageRows, ticker, fromIso, toIso) {
  const tradingDays = enumerateTradingDays(fromIso, toIso);
  if (tradingDays.length === 0) {
    return 0;
  }

  const statusByDate = {};
  for (const row of coverageRows) {
    if (row.ticker?.toUpperCase() !== ticker.toUpperCase()) {
      continue;
    }

    const date = typeof row.date === 'string' ? row.date : row.date;
    statusByDate[date] = (row.status ?? '').toLowerCase();
  }

  let complete = 0;
  for (const day of tradingDays) {
    const status = statusByDate[day];
    if (status === 'done' || status === 'skipped') {
      complete += 1;
    }
  }

  return Math.round((complete / tradingDays.length) * 100);
}

export function buildDefaultPullRows() {
  return DEFAULT_TICKERS.map((ticker) => ({
    id: crypto.randomUUID(),
    ticker,
    dateFrom: DEFAULT_PULL_FROM,
    dateTo: DEFAULT_PULL_TO,
  }));
}
