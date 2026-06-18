#!/usr/bin/env node
/**
 * Generates dist/index.html for Surge CDN landing page.
 * Usage: node scripts/generate-cdn-index.mjs <distDir> [repoRoot]
 */
import fs from 'node:fs';
import path from 'node:path';

const distDir = path.resolve(process.argv[2] ?? 'dist');
const repoRoot = path.resolve(process.argv[3] ?? path.join(distDir, '..'));

const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'));
const manifestPath = path.join(distDir, 'manifest.json');
if (!fs.existsSync(manifestPath)) {
  console.error('manifest.json missing in dist — run build-cdn-dist first.');
  process.exit(1);
}

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const installerVersion = manifest.installerVersion ?? pkg.version;
const patchVersion = manifest.latestPatchVersion ?? pkg.tapereplay?.patchVersion ?? '0.0.0';
const releasedAt = manifest.releasedAt ?? '';
const cdnBase = (manifest.cdnBaseUrl ?? 'https://tapereplay.surge.sh').replace(/\/$/, '');

const files = fs.readdirSync(distDir);
const patchFile = manifest.patchFile;

function labelFor(filename) {
  if (filename.endsWith('.dmg') && filename.includes('arm64')) return 'macOS (Apple Silicon)';
  if (filename.endsWith('.dmg') && filename.includes('x64')) return 'macOS (Intel)';
  if (filename.endsWith('.dmg')) return 'macOS';
  if (filename.includes('Setup') && filename.endsWith('.exe')) return 'Windows (installer)';
  if (filename.includes('Portable') && filename.endsWith('.exe')) return 'Windows (portable)';
  if (filename.endsWith('.exe')) return 'Windows';
  if (filename.endsWith('.AppImage')) return 'Linux (AppImage)';
  if (filename === patchFile) return `JS patch ${patchVersion}`;
  if (filename.endsWith('.zip') && filename.includes('mac')) return 'macOS (zip)';
  if (filename.endsWith('.zip') && filename.includes('win')) return 'Windows (zip)';
  if (filename.endsWith('.zip')) return 'Archive';
  return filename;
}

function sortDownloads(a, b) {
  const rank = (f) => {
    if (f.endsWith('.dmg')) return 0;
    if (f.includes('Setup')) return 1;
    if (f.includes('Portable')) return 2;
    if (f.endsWith('.exe')) return 3;
    if (f.endsWith('.AppImage')) return 4;
    return 5;
  };
  return rank(a) - rank(b) || a.localeCompare(b);
}

const installerExts = /\.(dmg|exe|AppImage)$/i;
const installerFiles = files
  .filter((f) => installerExts.test(f) && !f.startsWith('patch_'))
  .sort(sortDownloads);

const downloadCards = installerFiles.length
  ? installerFiles
      .map((file) => {
        const stat = fs.statSync(path.join(distDir, file));
        const sizeMb = (stat.size / (1024 * 1024)).toFixed(1);
        return `<div class="download-card">
  <div>
    <strong>${escapeHtml(labelFor(file))}</strong>
    <div class="meta">${escapeHtml(file)} · ${sizeMb} MB</div>
  </div>
  <a class="btn btn-primary" href="./${encodeURIComponent(file)}" download>Download</a>
</div>`;
      })
      .join('\n')
  : `<div class="muted-box">
  Installers are not in this deploy yet. Build with
  <code>INCLUDE_INSTALLERS=1 make cdn-dist</code>
  after <code>make installer-mac</code>, or see
  <a href="https://github.com/jameymcelveen/tape-replay/releases">GitHub Releases</a>.
</div>`;

const patchCard = patchFile && files.includes(patchFile)
  ? `<div class="download-card">
  <div>
    <strong>App patch (auto-update)</strong>
    <div class="meta">${escapeHtml(patchFile)} — applied automatically by the desktop app</div>
  </div>
  <a class="btn btn-secondary" href="./${encodeURIComponent(patchFile)}">Patch zip</a>
</div>`
  : '';

const releasedLabel = releasedAt
  ? `${new Date(releasedAt).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' })} UTC`
  : '—';

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta name="description" content="TapeReplay — day trading strategy backtester. Download for macOS and Windows." />
  <title>TapeReplay — Download</title>
  <link rel="stylesheet" href="./assets/site.css" />
</head>
<body>
  <div class="wrap">
    <header>
      <h1>TapeReplay</h1>
      <p class="tagline">Day trading strategy backtester — honest backtests, candlestick charts, local minute-bar data.</p>
      <span class="badge">Installer v${escapeHtml(installerVersion)} · Patch ${escapeHtml(patchVersion)}</span>
    </header>

    <section>
      <h2>Download</h2>
      <div class="downloads">
        ${downloadCards}
        ${patchCard}
      </div>
    </section>

    <section>
      <h2>What you get</h2>
      <ul class="features">
        <li>Desktop app (Electron) with a local .NET API and SQLite cache</li>
        <li>Strategy lab — commit in-sample, evaluate out-of-sample</li>
        <li>Chart backtest — ORB/PMH with candlesticks and hindsight markers</li>
        <li>Optional Polygon recording and CDN data sync</li>
      </ul>
    </section>

    <section>
      <h2>Documentation &amp; data</h2>
      <div class="links">
        <a href="./help/index.html">Help &amp; getting started</a>
        <a href="./data/manifest.json">Market data manifest</a>
        <a href="https://github.com/jameymcelveen/tape-replay">Source on GitHub</a>
      </div>
    </section>

    <footer>
      <p>Released ${escapeHtml(releasedLabel)} · CDN <code>${escapeHtml(cdnBase)}</code></p>
      <p>JS patches ship between full installer releases. See <a href="./help/updates.html">updates guide</a>.</p>
    </footer>
  </div>
</body>
</html>
`;

fs.mkdirSync(path.join(distDir, 'assets'), { recursive: true });
fs.copyFileSync(
  path.join(repoRoot, 'docs/cdn/assets/site.css'),
  path.join(distDir, 'assets/site.css'),
);
fs.writeFileSync(path.join(distDir, 'index.html'), html);
console.log(`Wrote ${path.join(distDir, 'index.html')}`);

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
