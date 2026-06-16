export default function DashboardLayout({ children, backendStatus }) {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="text-xl font-bold tracking-tight">TapeReplay</h1>
            <p className="text-sm text-slate-400">Day trading strategy backtester</p>
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
      </header>

      <main className="mx-auto grid max-w-7xl gap-6 px-6 py-8 lg:grid-cols-2">
        {children}
      </main>
    </div>
  );
}
