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

  await navigateTo(page, 'Chart backtest');
  await page.getByRole('heading', { name: 'Chart backtest' }).waitFor();
  await beat(page, beats.medium);

  await page.getByRole('button', { name: 'Full session preset' }).click();
  await beat(page, beats.short);

  await page.getByRole('button', { name: 'Run backtest' }).click();
  await checkpoint(page, 'Chart backtest running');

  await page.getByText('Strategy trade', { exact: false }).waitFor({ timeout: 120_000 });
  await beat(page, beats.medium);

  await page.getByText('Perfect hindsight', { exact: false }).waitFor();
  await beat(page, beats.long);
} finally {
  await finish(page, browser);
}
