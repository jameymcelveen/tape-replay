const CDN_BASE = (import.meta.env.VITE_CDN_BASE_URL ?? 'https://tapereplay.surge.sh').replace(/\/$/, '');

export const HELP_BASE = `${CDN_BASE}/help`;

/** Contextual help page URLs (static HTML on the CDN). */
export const HELP_PAGES = {
  index: `${HELP_BASE}/index.html`,
  strategyLab: `${HELP_BASE}/strategy-lab.html`,
  chartBacktest: `${HELP_BASE}/chart-backtest.html`,
  collectingData: `${HELP_BASE}/collecting-data.html`,
  honesty: `${HELP_BASE}/honesty.html`,
  updates: `${HELP_BASE}/updates.html`,
};

/**
 * Opens a help page in the system browser (Electron) or a new tab (web dev).
 * @param {keyof typeof HELP_PAGES | 'index'} page
 */
export function openHelp(page = 'index') {
  const url = HELP_PAGES[page] ?? HELP_PAGES.index;

  if (window.tapeReplay?.openExternal) {
    window.tapeReplay.openExternal(url);
    return;
  }

  window.open(url, '_blank', 'noopener,noreferrer');
}
