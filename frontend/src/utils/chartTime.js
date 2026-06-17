export function getEasternParts(utcMs) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(new Date(utcMs));

  const map = Object.fromEntries(parts.map((p) => [p.type, p.value]));
  return {
    y: Number(map.year),
    m: Number(map.month),
    d: Number(map.day),
    hh: Number(map.hour === '24' ? '0' : map.hour),
    mm: Number(map.minute),
  };
}

/** Converts an Eastern market date/time to a UTC ISO string. */
export function easternToUtcIso(dateStr, timeStr) {
  const [y, m, d] = dateStr.split('-').map(Number);
  const [hh, mm] = timeStr.split(':').map(Number);
  let ms = Date.UTC(y, m - 1, d, hh + 5, mm);

  for (let i = 0; i < 48; i += 1) {
    const parts = getEasternParts(ms);
    if (parts.y === y && parts.m === m && parts.d === d && parts.hh === hh && parts.mm === mm) {
      return new Date(ms).toISOString();
    }

    const targetMinutes = hh * 60 + mm;
    const actualMinutes = parts.hh * 60 + parts.mm;
    const dayDelta = d - parts.d;
    ms += (targetMinutes - actualMinutes) * 60_000 + dayDelta * 86_400_000;
  }

  return new Date(ms).toISOString();
}

export function toChartTime(isoUtc) {
  return Math.floor(new Date(isoUtc).getTime() / 1000);
}

export function formatEasternTime(isoUtc) {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(new Date(isoUtc));
}

export function formatEasternDateTime(isoUtc) {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(isoUtc));
}

export function findBarTime(bars, isoUtc) {
  if (!isoUtc || !bars?.length) {
    return null;
  }

  const target = toChartTime(isoUtc);
  const exact = bars.find((bar) => toChartTime(bar.t) === target);
  if (exact) {
    return target;
  }

  const targetMs = new Date(isoUtc).getTime();
  let closest = null;
  let closestDelta = Number.POSITIVE_INFINITY;

  for (const bar of bars) {
    const delta = Math.abs(new Date(bar.t).getTime() - targetMs);
    if (delta < closestDelta) {
      closestDelta = delta;
      closest = toChartTime(bar.t);
    }
  }

  return closestDelta <= 60_000 ? closest : null;
}

export const SESSION_VOLUME_COLORS = {
  premarket: 'rgba(139, 92, 246, 0.45)',
  regular: 'rgba(100, 116, 139, 0.55)',
  post: 'rgba(56, 189, 248, 0.45)',
};
