import { useEffect, useState } from 'react';
import { HELP_PAGES, openHelp } from '../config/help';

const NAV_ITEMS = [
  { id: 'strategy', label: 'Strategy lab' },
  { id: 'data-pull', label: 'Data pull' },
  { id: 'overview', label: 'Overview' },
  { id: 'chart', label: 'Chart backtest' },
  { id: 'chart-heatmap', label: 'Chart heatmap' },
];

export default function DashboardLayout({ children, backendStatus, view, onViewChange }) {
  const [patchLabel, setPatchLabel] = useState('');
  const [helpOpen, setHelpOpen] = useState(false);

  useEffect(() => {
    if (!helpOpen) {
      return undefined;
    }

    function closeOnClick(event) {
      if (!event.target.closest('[data-help-menu]')) {
        setHelpOpen(false);
      }
    }

    document.addEventListener('click', closeOnClick);
    return () => document.removeEventListener('click', closeOnClick);
  }, [helpOpen]);

  useEffect(() => {
    if (window.tapeReplay?.getPatchInfo) {
      window.tapeReplay.getPatchInfo().then((info) => {
        if (info?.installerVersion) {
          setPatchLabel(`v${info.installerVersion} patch ${info.patchVersion}`);
        }
      }).catch(() => {});
    }
  }, []);

  const isFullWidthView = ['chart', 'chart-heatmap', 'data-pull', 'overview'].includes(view);

  function helpPageForView() {
    if (view === 'chart' || view === 'chart-heatmap') {
      return 'chartBacktest';
    }

    if (view === 'data-pull') {
      return 'collectingData';
    }

    if (view === 'overview') {
      return 'honesty';
    }

    return 'strategyLab';
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-6 py-4">
          <div>
            <h1 className="text-xl font-bold tracking-tight">TapeReplay</h1>
            <p className="text-sm text-slate-400">
              Day trading strategy backtester
              {patchLabel && <span className="ml-2 text-slate-500">({patchLabel})</span>}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-4">
            <nav className="flex flex-wrap rounded-lg border border-slate-700 p-1 text-sm" aria-label="Main navigation">
              {NAV_ITEMS.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={`rounded-md px-3 py-1.5 ${view === item.id ? 'bg-slate-700 text-white' : 'text-slate-400 hover:text-slate-200'}`}
                  onClick={() => onViewChange(item.id)}
                >
                  {item.label}
                </button>
              ))}
            </nav>
            <div className="relative" data-help-menu>
              <button
                type="button"
                className="rounded-lg border border-slate-600 px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800"
                onClick={() => setHelpOpen((open) => !open)}
                aria-expanded={helpOpen}
              >
                Help
              </button>
              {helpOpen && (
                <div className="absolute right-0 z-20 mt-2 w-52 rounded-lg border border-slate-700 bg-slate-900 py-1 shadow-xl">
                  <button type="button" className="block w-full px-3 py-2 text-left text-sm text-slate-300 hover:bg-slate-800" onClick={() => { openHelp('index'); setHelpOpen(false); }}>Getting started</button>
                  <button type="button" className="block w-full px-3 py-2 text-left text-sm text-slate-300 hover:bg-slate-800" onClick={() => { openHelp(helpPageForView()); setHelpOpen(false); }}>View guide</button>
                  <button type="button" className="block w-full px-3 py-2 text-left text-sm text-slate-300 hover:bg-slate-800" onClick={() => { openHelp('collectingData'); setHelpOpen(false); }}>Collecting data</button>
                  <button type="button" className="block w-full px-3 py-2 text-left text-sm text-slate-300 hover:bg-slate-800" onClick={() => { openHelp('honesty'); setHelpOpen(false); }}>Honesty by design</button>
                  <hr className="my-1 border-slate-700" />
                  <a href={HELP_PAGES.index} target="_blank" rel="noopener noreferrer" className="block px-3 py-2 text-xs text-slate-500 hover:text-slate-400" onClick={() => setHelpOpen(false)}>Open all docs</a>
                </div>
              )}
            </div>
            <div className="flex items-center gap-2 text-sm">
              <span
                className={`h-2.5 w-2.5 rounded-full ${backendStatus === 'ok' ? 'bg-emerald-400' : 'bg-amber-400'}`}
              />
              <span className="text-slate-400">
                Backend: {backendStatus === 'ok' ? 'Connected' : 'Starting...'}
              </span>
            </div>
          </div>
        </div>
      </header>

      <main className={`mx-auto grid max-w-7xl gap-6 px-6 py-8 ${isFullWidthView ? 'grid-cols-1' : 'lg:grid-cols-2'}`}>
        {children}
      </main>
    </div>
  );
}
