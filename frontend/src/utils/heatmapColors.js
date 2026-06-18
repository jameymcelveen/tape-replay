/** @typedef {'pnlPct' | 'capturePct' | 'pnlDollar'} HeatmapMetric */

const FLAT_EPSILON = {
  pnlPct: 0.05,
  capturePct: 1,
  pnlDollar: 1,
};

const DEFAULT_CLAMP = {
  pnlPct: { min: -20, max: 20 },
  capturePct: { min: -100, max: 100 },
  pnlDollar: { min: -2000, max: 2000 },
};

export function getDefaultClamp(metric) {
  return DEFAULT_CLAMP[metric] ?? DEFAULT_CLAMP.pnlPct;
}

export function getFlatEpsilon(metric) {
  return FLAT_EPSILON[metric] ?? FLAT_EPSILON.pnlPct;
}

/**
 * @param {{ hasData?: boolean, traded?: boolean, pnlPct?: number|null, capturePct?: number|null, pnlDollar?: number|null }} cell
 * @param {HeatmapMetric} metric
 */
export function getCellMetricValue(cell, metric) {
  if (!cell) {
    return null;
  }

  if (metric === 'capturePct') {
    return cell.capturePct ?? null;
  }

  if (metric === 'pnlDollar') {
    return cell.pnlDollar ?? null;
  }

  return cell.pnlPct ?? null;
}

/**
 * @param {number|null|undefined} value
 * @param {HeatmapMetric} metric
 * @param {{ min: number, max: number }} clamp
 */
export function mapMetricToColor(value, metric, clamp) {
  const epsilon = getFlatEpsilon(metric);

  if (value == null || Number.isNaN(value)) {
    return 'rgba(30, 41, 59, 0.35)';
  }

  if (Math.abs(value) <= epsilon) {
    return 'rgb(148, 163, 184)';
  }

  const magnitude = Math.min(Math.abs(value), Math.max(Math.abs(clamp.min), Math.abs(clamp.max)));
  const domainMax = value >= 0 ? Math.abs(clamp.max) : Math.abs(clamp.min);
  const intensity = domainMax > 0 ? Math.min(1, magnitude / domainMax) : 0;
  const alpha = 0.25 + intensity * 0.75;

  if (value > epsilon) {
    return `rgba(34, 197, 94, ${alpha.toFixed(3)})`;
  }

  return `rgba(239, 68, 68, ${alpha.toFixed(3)})`;
}

/**
 * @param {Array<{ hasData?: boolean, traded?: boolean, pnlPct?: number|null, capturePct?: number|null, pnlDollar?: number|null }>} cells
 * @param {HeatmapMetric} metric
 * @param {number} percentile 0-100 symmetric tail percentile for domain
 */
export function computePercentileClamp(cells, metric, percentile = 95) {
  const values = cells
    .map((cell) => getCellMetricValue(cell, metric))
    .filter((value) => value != null && !Number.isNaN(value))
    .map((value) => Math.abs(Number(value)))
    .sort((a, b) => a - b);

  if (values.length === 0) {
    return getDefaultClamp(metric);
  }

  const index = Math.min(values.length - 1, Math.floor((percentile / 100) * values.length));
  const bound = values[index] || getDefaultClamp(metric).max;
  return { min: -bound, max: bound };
}

export function getCoverageColor(status) {
  switch ((status ?? '').toLowerCase()) {
    case 'done':
      return 'rgba(34, 197, 94, 0.85)';
    case 'pending':
      return 'rgba(245, 158, 11, 0.85)';
    case 'skipped':
      return 'rgba(100, 116, 139, 0.55)';
    default:
      return 'rgba(30, 41, 59, 0.35)';
  }
}

export function formatMetricValue(value, metric) {
  if (value == null || Number.isNaN(value)) {
    return '—';
  }

  if (metric === 'pnlDollar') {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  return `${value >= 0 ? '+' : ''}${Number(value).toFixed(2)}%`;
}
