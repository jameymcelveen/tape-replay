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

  await navigateTo(page, 'Strategy lab');
  await page.getByRole('heading', { name: 'Strategy Builder' }).waitFor();
  await beat(page, beats.medium);

  await page.locator('label:has-text("Ticker") input').fill('VSME');
  await beat(page, beats.short);

  await page.getByRole('button', { name: 'Preview DSL' }).click();
  await checkpoint(page, 'DSL preview');

  await page.locator('pre').first().waitFor({ timeout: 15_000 });
  await beat(page, beats.long);

  await page.getByRole('button', { name: 'Exploratory Day' }).click();
  await checkpoint(page, 'Exploratory run');

  await page.getByText('Exploratory', { exact: false }).first().waitFor({ timeout: 120_000 });
  await beat(page, beats.long);
} finally {
  await finish(page, browser);
}
