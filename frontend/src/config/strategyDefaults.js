export const DEFAULT_TICKERS = ['EDHL', 'CCHH', 'CAST', 'VSME', 'JRSH'];

export const DEFAULT_DATA_FROM = '2026-04-01';
export const DEFAULT_DATA_TO = '2026-06-18';

export const defaultStrategyConfig = {
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
