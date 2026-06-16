import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import BacktestResults from './BacktestResults';

describe('BacktestResults', () => {
  it('labels exploratory runs as not evidence', () => {
    render(
      <BacktestResults
        result={{
          mode: 'exploratory',
          data: {
            ticker: 'AAPL',
            date: '2024-06-03',
            strategyName: 'Test',
            trades: [],
            metrics: {
              grossTotalPnL: 0,
              netTotalPnL: -50,
              totalCosts: 50,
              netReturnPercent: -0.2,
              maxDrawdownAbsolute: 100,
              maxDrawdownPercent: 0.4,
              longestLosingStreakTrades: 2,
              longestLosingStreakDays: 1,
              recoveredFromMaxDrawdown: false,
              winRate: 0,
              averageWin: 0,
              averageLoss: -25,
              payoffRatio: 0,
              expectancyPerTrade: -25,
              tradeCount: 2,
              verdict: 'Exploratory single day (not evidence)',
            },
          },
        }}
      />
    );

    expect(screen.getAllByText(/Exploratory single day \(not evidence\)/i).length).toBeGreaterThan(0);
  });
});
