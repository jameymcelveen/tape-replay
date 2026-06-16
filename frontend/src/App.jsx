import { useEffect, useState } from 'react';
import DashboardLayout from './components/DashboardLayout';
import StrategyBuilder from './components/StrategyBuilder';
import BacktestResults from './components/BacktestResults';
import { checkHealth, runBacktest } from './services/api';

export default function App() {
  const [backendStatus, setBackendStatus] = useState('starting');
  const [result, setResult] = useState(null);
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

  async function handleRunBacktest(payload) {
    setIsLoading(true);
    try {
      const backtestResult = await runBacktest(payload);
      setResult(backtestResult);
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <DashboardLayout backendStatus={backendStatus}>
      <StrategyBuilder onRunBacktest={handleRunBacktest} isLoading={isLoading} />
      <BacktestResults result={result} />
    </DashboardLayout>
  );
}
