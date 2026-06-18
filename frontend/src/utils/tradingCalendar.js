export function isTradingDay(date) {
  const day = date.getDay();
  return day !== 0 && day !== 6;
}

export function toIsoDate(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function parseIsoDate(iso) {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function enumerateTradingDays(fromIso, toIso) {
  const days = [];
  const cursor = parseIsoDate(fromIso);
  const end = parseIsoDate(toIso);

  while (cursor <= end) {
    if (isTradingDay(cursor)) {
      days.push(toIsoDate(cursor));
    }
    cursor.setDate(cursor.getDate() + 1);
  }

  return days;
}

export function subtractTradingDays(endIso, count) {
  const cursor = parseIsoDate(endIso);
  let remaining = count;

  while (remaining > 0) {
    cursor.setDate(cursor.getDate() - 1);
    if (isTradingDay(cursor)) {
      remaining -= 1;
    }
  }

  return toIsoDate(cursor);
}

export function defaultHeatmapRange(tradingDayCount = 252) {
  const today = new Date();
  let end = today;
  while (!isTradingDay(end)) {
    end.setDate(end.getDate() - 1);
  }

  const to = toIsoDate(end);
  const from = subtractTradingDays(to, tradingDayCount - 1);
  return { from, to };
}

export function monthLabel(isoDate) {
  const date = parseIsoDate(isoDate);
  return date.toLocaleDateString('en-US', { month: 'short' });
}

export function isMonthStart(isoDate, priorIsoDate) {
  if (!priorIsoDate) {
    return true;
  }

  const date = parseIsoDate(isoDate);
  const prior = parseIsoDate(priorIsoDate);
  return date.getMonth() !== prior.getMonth() || date.getFullYear() !== prior.getFullYear();
}
