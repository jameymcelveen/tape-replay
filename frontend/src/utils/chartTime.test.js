import { describe, expect, it } from 'vitest';
import { easternToUtcIso, toChartTime } from './chartTime';

describe('chartTime', () => {
  it('converts Eastern 09:30 on 2026-06-16 to 13:30 UTC (EDT)', () => {
    const iso = easternToUtcIso('2026-06-16', '09:30');
    expect(iso).toBe('2026-06-16T13:30:00.000Z');
    expect(toChartTime(iso)).toBe(Math.floor(Date.parse('2026-06-16T13:30:00.000Z') / 1000));
  });
});
