/**
 * Shared Playwright harness for headed showcase recordings.
 * Assumes pre-filled SQLite data and dev servers on :5173 / :5180.
 */

const VIEWPORT = { width: 1920, height: 1080 };
const SLOW_MO = Number(process.env.DEMO_SLOW_MO ?? 280);
const APP_URL = process.env.DEMO_URL ?? 'http://localhost:5173';
const API_URL = process.env.DEMO_API_URL ?? 'http://localhost:5180';

export const beats = {
  short: Number(process.env.DEMO_BEAT_SHORT ?? 1800),
  medium: Number(process.env.DEMO_BEAT_MEDIUM ?? 3200),
  long: Number(process.env.DEMO_BEAT_LONG ?? 5500),
};

async function probe(url) {
  try {
    const response = await fetch(url);
    return response.ok;
  } catch {
    return false;
  }
}

export async function waitForStack() {
  const backendOk = await probe(`${API_URL}/api/health`);
  const frontendOk = await probe(APP_URL);

  if (!backendOk || !frontendOk) {
    const lines = [
      'Demo servers are not reachable.',
      !backendOk ? `  Backend missing: ${API_URL} (start with npm run dev:ui in another terminal)` : null,
      !frontendOk ? `  Frontend missing: ${APP_URL} (start with npm run dev:ui in another terminal)` : null,
      '',
      'Quick start:',
      '  Terminal 1: npm run dev:ui',
      '  Terminal 2: DEMO_HOLD=1 make demo-data-pull',
    ].filter(Boolean);
    throw new Error(lines.join('\n'));
  }
}

export async function launchDemo() {
  const { chromium } = await import('playwright');
  const browser = await chromium.launch({
    headless: false,
    slowMo: SLOW_MO,
  });
  const context = await browser.newContext({
    viewport: VIEWPORT,
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  return { browser, page };
}

export async function openApp(page) {
  await page.goto(APP_URL, { waitUntil: 'networkidle' });
  await page.getByText('Backend: Connected', { exact: false }).waitFor({ timeout: 60_000 });
}

export async function navigateTo(page, label) {
  await page.getByRole('navigation', { name: 'Main navigation' })
    .getByRole('button', { name: label, exact: true })
    .click();
  await page.waitForTimeout(400);
}

export async function beat(page, ms = beats.medium) {
  await page.waitForTimeout(ms);
}

export async function checkpoint(page, label) {
  if (process.env.DEMO_INTERACTIVE === '1') {
    console.log(`[demo pause] ${label}. Click Resume in Playwright inspector or press Enter in terminal.`);
    await page.pause();
    return;
  }

  console.log(`[demo] ${label}`);
  await beat(page, beats.short);
}

export async function finish(page, browser) {
  await beat(page, beats.long);

  if (process.env.DEMO_HOLD === '1') {
    console.log('DEMO_HOLD=1: close the browser window when your recording is finished.');
    await page.waitForEvent('close', { timeout: 0 }).catch(() => {});
  }

  await browser.close();
}
