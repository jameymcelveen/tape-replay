import { describe, expect, it } from 'vitest';
import { comparePatchVersion } from '../../electron/patchVersion.js';

describe('comparePatchVersion', () => {
  it('orders patch versions', () => {
    expect(comparePatchVersion('0.1.1', '0.1.0')).toBe(1);
    expect(comparePatchVersion('0.1.0', '0.1.1')).toBe(-1);
    expect(comparePatchVersion('0.2.0', '0.1.9')).toBe(1);
  });
});
