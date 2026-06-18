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

  await navigateTo(page, 'Data pull');
  await checkpoint(page, 'Data pull view');

  await page.getByRole('heading', { name: 'Data pull' }).waitFor();
  await beat(page, beats.medium);

  const pies = page.locator('svg').filter({ has: page.locator('circle') });
  const pieCount = await pies.count();
  console.log(`[demo] ${pieCount} coverage pies visible (pre-filled DB)`);

  await beat(page, beats.long);

  const firstRow = page.getByRole('button', { name: 'Pull' }).first();
  await firstRow.hover();
  await beat(page, beats.medium);

  await page.mouse.wheel(0, 120);
  await beat(page, beats.long);
} finally {
  await finish(page, browser);
}
