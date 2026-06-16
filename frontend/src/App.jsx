import { useEffect, useState } from 'react';
import DashboardLayout from './components/DashboardLayout';
import StrategyBuilder from './components/StrategyBuilder';
import BacktestResults from './components/BacktestResults';
import { checkHealth, commitBacktest, evaluateBacktest, runBacktest } from './services/api';

export default function App() {
  const [backendStatus, setBackendStatus] = useState('starting');
  const [result, setResult] = useState(null);
  const [commitId, setCommitId] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

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
    <DashboardLayout backendStatus={backendStatus}>
      <StrategyBuilder
        commitId={commitId}
        isLoading={isLoading}
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
    </DashboardLayout>
  );
}
