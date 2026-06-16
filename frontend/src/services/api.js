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

export async function runBacktest({ ticker, date, strategy }) {
  return request('/api/backtest/run', {
    method: 'POST',
    body: JSON.stringify({ ticker, date, strategy }),
  });
}

export async function checkHealth() {
  return request('/api/health');
}
