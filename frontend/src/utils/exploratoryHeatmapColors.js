const FLAT_EPSILON = 0.5;

export function mapNetPnlToColor(netPnl, hasData, traded, clamp = 500) {
  if (!hasData) {
    return 'rgba(15, 23, 42, 0.25)';
  }

  if (!traded || netPnl == null || Math.abs(netPnl) < FLAT_EPSILON) {
    return 'rgb(148, 163, 184)';
  }

  const magnitude = Math.min(Math.abs(netPnl), clamp);
  const intensity = 0.25 + (magnitude / clamp) * 0.75;

  if (netPnl > 0) {
    return `rgba(34, 197, 94, ${intensity.toFixed(3)})`;
  }

  return `rgba(239, 68, 68, ${intensity.toFixed(3)})`;
}

export function formatMoney(value) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value ?? 0);
}
