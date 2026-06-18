import { describe, expect, it } from 'vitest';
import {
  getCellMetricValue,
  getDefaultClamp,
  mapMetricToColor,
} from './heatmapColors';

describe('heatmapColors', () => {
  it('maps positive pnl to green', () => {
    const color = mapMetricToColor(10, 'pnlPct', getDefaultClamp('pnlPct'));
    expect(color).toContain('34, 197, 94');
  });

  it('maps negative pnl to red', () => {
    const color = mapMetricToColor(-10, 'pnlPct', getDefaultClamp('pnlPct'));
    expect(color).toContain('239, 68, 68');
  });

  it('maps flat values to gray', () => {
    const color = mapMetricToColor(0.01, 'pnlPct', getDefaultClamp('pnlPct'));
    expect(color).toBe('rgb(148, 163, 184)');
  });

  it('reads metric fields from cells', () => {
    const cell = { pnlPct: 1.2, capturePct: 40, pnlDollar: 50 };
    expect(getCellMetricValue(cell, 'capturePct')).toBe(40);
    expect(getCellMetricValue(cell, 'pnlDollar')).toBe(50);
  });
});
