export const DEFAULT_TICKERS = ['EDHL', 'CCHH', 'CAST', 'VSME', 'JRSH'];

// June slice with local minute data: fast Overview grid (~55 cells vs ~275 for full pull range).
export const DEFAULT_DATA_FROM = '2026-06-01';
export const DEFAULT_DATA_TO = '2026-06-16';

// Full recording window (Data pull rows can still span this when pulling new data).
export const DEFAULT_PULL_FROM = '2026-04-01';
export const DEFAULT_PULL_TO = '2026-06-18';

export const defaultStrategyConfig = {
  name: 'Opening Range Breakout',
  entryTrigger: 'OpeningRangeHighBreak',
  openingRangeMinutes: 5,
  entryWindowStart: '09:35',
  entryWindowEnd: '10:30',
  positionSizeUsd: 1000,
  stopLossPercent: 1.5,
  takeProfitTargets: [
    { percent: 3, weight: 0.5 },
    { percent: 6, weight: 0.5 },
  ],
  closeAllAt: '12:00',
  maxDailyLossUsd: 300,
  maxConcurrentTrades: 1,
  maxTradesPerDay: 1,
  noReentryAfterStop: true,
  regularSessionOnly: true,
  firstBreakoutOnly: true,
};
