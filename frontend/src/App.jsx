import { useEffect, useState } from 'react';
import DashboardLayout from './components/DashboardLayout';
import StrategyBuilder from './components/StrategyBuilder';
import BacktestResults from './components/BacktestResults';
import ChartBacktestView from './components/ChartBacktestView';
import StrategyHeatmapView from './components/StrategyHeatmapView';
import DataPullView from './components/DataPullView';
import ExploratoryHeatmapView from './components/ExploratoryHeatmapView';
import { defaultStrategyConfig } from './config/strategyDefaults';
import { checkHealth, commitBacktest, evaluateBacktest, runBacktest } from './services/api';

export default function App() {
  const [backendStatus, setBackendStatus] = useState('starting');
  const [view, setView] = useState('strategy');
  const [result, setResult] = useState(null);
  const [commitId, setCommitId] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [chartNavigate, setChartNavigate] = useState(null);
  const [strategyConfig, setStrategyConfig] = useState(defaultStrategyConfig);
  const [startingCapitalUsd, setStartingCapitalUsd] = useState(25000);

  useEffect(() => {
    let cancelled = false;

    async function pollHealth() {
      try {
        await checkHealth();
        if (!cancelled) {
          setBackendStatus('ok');
        }
      } catch {
        if (!cancelled) {
          setBackendStatus('starting');
          setTimeout(pollHealth, 1500);
        }
      }
    }

    pollHealth();
    return () => {
      cancelled = true;
    };
  }, []);

  async function withLoading(action) {
    setIsLoading(true);
    try {
      await action();
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <DashboardLayout backendStatus={backendStatus} view={view} onViewChange={setView}>
      {view === 'strategy' ? (
        <>
          <StrategyBuilder
            commitId={commitId}
            isLoading={isLoading}
            strategyConfig={strategyConfig}
            onStrategyConfigChange={setStrategyConfig}
            startingCapitalUsd={startingCapitalUsd}
            onStartingCapitalChange={setStartingCapitalUsd}
            onRunExploratory={(payload) => withLoading(async () => {
              const data = await runBacktest(payload);
              setResult({ mode: 'exploratory', data });
            })}
            onCommit={(payload) => withLoading(async () => {
              const data = await commitBacktest(payload);
              setCommitId(data.commitId);
              setResult({ mode: 'insample', data: data.inSample });
            })}
            onEvaluate={(payload) => withLoading(async () => {
              const data = await evaluateBacktest(payload);
              setResult({ mode: 'evaluate', data });
            })}
          />
          <BacktestResults result={result} />
        </>
      ) : view === 'data-pull' ? (
        <DataPullView backendStatus={backendStatus} />
      ) : view === 'overview' ? (
        <ExploratoryHeatmapView
          strategyConfig={strategyConfig}
          startingCapitalUsd={startingCapitalUsd}
          isLoading={isLoading}
          onRun={(action) => withLoading(action)}
        />
      ) : view === 'chart' ? (
        <ChartBacktestView
          isLoading={isLoading}
          onRun={(action) => withLoading(action)}
          navigateRequest={chartNavigate}
          onNavigateHandled={() => setChartNavigate(null)}
        />
      ) : view === 'chart-heatmap' ? (
        <StrategyHeatmapView
          isLoading={isLoading}
          onRun={(action) => withLoading(action)}
          onOpenChart={(request) => {
            setChartNavigate(request);
            setView('chart');
          }}
        />
      ) : null}
    </DashboardLayout>
  );
}
