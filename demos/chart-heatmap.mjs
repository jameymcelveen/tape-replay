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

  await navigateTo(page, 'Chart heatmap');
  await page.getByRole('heading', { name: 'Strategy heatmap' }).waitFor({ timeout: 30_000 });
  await beat(page, beats.medium);

  await page.getByRole('button', { name: 'Coverage' }).click();
  await checkpoint(page, 'Coverage mode');
  await beat(page, beats.long);

  await page.getByRole('button', { name: 'Performance' }).click();
  await checkpoint(page, 'Performance mode');

  await page.getByText('tickers', { exact: false }).waitFor({ timeout: 120_000 });
  await beat(page, beats.long);

  const cell = page.locator('button[style*="background-color"]').nth(20);
  await cell.hover();
  await beat(page, beats.medium);
} finally {
  await finish(page, browser);
}
