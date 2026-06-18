const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5180';

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    ...options,
  });

  if (!response.ok) {
    const errorBody = await response.json().catch(() => ({}));
    throw new Error(errorBody.error ?? `Request failed: ${response.status}`);
  }

  return response.json();
}

export async function generateDsl(strategy) {
  return request('/api/strategy/generate', {
    method: 'POST',
    body: JSON.stringify(strategy),
  });
}

export async function parseDsl(dsl) {
  return request('/api/strategy/parse', {
    method: 'POST',
    body: JSON.stringify({ dsl }),
  });
}

export async function runBacktest(payload) {
  return request('/api/backtest/run', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function runChartBacktest(payload) {
  return request('/api/backtest/chart', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function fetchStrategyHeatmap(payload) {
  return request('/api/backtest/chart/heatmap', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function fetchMinuteCoverage({ ticker, startDate, endDate } = {}) {
  const params = new URLSearchParams();
  if (ticker) {
    params.set('ticker', ticker);
  }
  if (startDate) {
    params.set('startDate', startDate);
  }
  if (endDate) {
    params.set('endDate', endDate);
  }

  const query = params.toString();
  return request(`/api/data/coverage/minute${query ? `?${query}` : ''}`);
}

export async function commitBacktest(payload) {
  return request('/api/backtest/commit', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function evaluateBacktest(payload) {
  return request('/api/backtest/evaluate', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function checkHealth() {
  return request('/api/health');
}
