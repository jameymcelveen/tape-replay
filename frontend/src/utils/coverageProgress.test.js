import { describe, expect, it } from 'vitest';
import { computeCoveragePercent } from './coverageProgress';
import { enumerateTradingDays } from './tradingCalendar';

describe('coverageProgress', () => {
  it('counts done and skipped toward completion', () => {
    const rows = [
      { ticker: 'VSME', date: '2026-04-01', status: 'Done' },
      { ticker: 'VSME', date: '2026-04-02', status: 'Skipped' },
      { ticker: 'VSME', date: '2026-04-03', status: 'Pending' },
    ];
    const totalDays = enumerateTradingDays('2026-04-01', '2026-04-03').length;
    const percent = computeCoveragePercent(rows, 'VSME', '2026-04-01', '2026-04-03');
    expect(totalDays).toBeGreaterThan(0);
    expect(percent).toBe(Math.round((2 / totalDays) * 100));
  });
});
