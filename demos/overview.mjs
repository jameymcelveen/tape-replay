import {
  beat,
  beats,
  checkpoint,
  finish,
  launchDemo,
  navigateTo,
  openApp,
  waitForStack,
} from './lib/harness.mjs';

await waitForStack();
const { browser, page } = await launchDemo();

try {
  await openApp(page);
  await checkpoint(page, 'App loaded');

  await navigateTo(page, 'Overview');
  await checkpoint(page, 'Overview tab');

  await page.getByText('Exploratory overview', { exact: false }).waitFor();
  await beat(page, beats.medium);

  await page.getByText('Net P&L (headline)', { exact: false }).waitFor({ timeout: 120_000 });
  await checkpoint(page, 'Totals panel loaded');

  await beat(page, beats.long);

  const gridCell = page.locator('button:not([disabled])[style*="background-color"]').first();
  await gridCell.hover();
  await beat(page, beats.medium);

  await gridCell.click();
  await checkpoint(page, 'Drill-down chart');

  await page.getByRole('button', { name: 'Back to overview' }).waitFor({ timeout: 60_000 });
  await beat(page, beats.long);

  await page.getByRole('button', { name: 'Back to overview' }).click();
  await checkpoint(page, 'Returned to heatmap');

  await beat(page, beats.long);
} finally {
  await finish(page, browser);
}
