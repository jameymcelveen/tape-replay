#!/usr/bin/env node
/**
 * Generates dist/index.html for the Surge CDN landing page.
 * Usage: node scripts/generate-cdn-index.mjs <distDir> [repoRoot]
 *
 * Keeps the dynamic behavior of the original generator (scans dist/ for
 * installer + patch files, reads versions from manifest.json/package.json,
 * copies the brand stylesheet) and renders the marketing landing layout.
 */
import fs from 'node:fs';
import path from 'node:path';

const distDir = path.resolve(process.argv[2] ?? 'dist');
const repoRoot = path.resolve(process.argv[3] ?? path.join(distDir, '..'));

const REPO_URL = 'https://github.com/jameymcelveen/tape-replay';
const RELEASES_URL = `${REPO_URL}/releases/latest`;

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

// Coarse platform key used by the client OS-detection script to highlight a card.
function osKey(filename) {
  const f = filename.toLowerCase();
  if (f.endsWith('.dmg') && f.includes('x64')) return 'mac-intel';
  if (f.endsWith('.dmg')) return 'mac-arm';
  if (f.endsWith('.exe')) return 'win';
  if (f.endsWith('.appimage')) return 'linux';
  if (f.endsWith('.zip') && f.includes('mac')) return 'mac-arm';
  if (f.endsWith('.zip') && f.includes('win')) return 'win';
  return '';
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
        return `<div class="download-card" data-os="${osKey(file)}">
  <div class="dl-left">
    <strong class="dl-title">${escapeHtml(labelFor(file))}<span class="pill">Your platform</span></strong>
    <div class="meta">${escapeHtml(file)} · ${sizeMb} MB</div>
  </div>
  <a class="btn btn-primary" href="./${encodeURIComponent(file)}" download>Download</a>
</div>`;
      })
      .join('\n')
  : `<div class="muted-box">
  No installers are bundled in this deploy yet. Build them with
  <code>INCLUDE_INSTALLERS=1 make cdn-dist</code> after <code>make installer-mac</code>,
  or grab the latest from
  <a href="${RELEASES_URL}">GitHub Releases</a>.
</div>`;

const patchCard = patchFile && files.includes(patchFile)
  ? `<div class="download-card" data-os="">
  <div class="dl-left">
    <strong class="dl-title">App patch (auto-update)</strong>
    <div class="meta">${escapeHtml(patchFile)} · applied automatically by the desktop app</div>
  </div>
  <a class="btn btn-secondary" href="./${encodeURIComponent(patchFile)}">Patch zip</a>
</div>`
  : '';

const releasedLabel = releasedAt
  ? `${new Date(releasedAt).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' })} UTC`
  : '—';

const heroChart = `<figure class="hero-chart" aria-label="Two equity curves: the in-sample line you tuned climbs higher than the out-of-sample line that survives after costs.">
  <svg viewBox="0 0 560 300" role="img" xmlns="http://www.w3.org/2000/svg">
    <defs>
      <linearGradient id="ddFill" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" stop-color="#fb7185" stop-opacity="0.35"/>
        <stop offset="100%" stop-color="#fb7185" stop-opacity="0"/>
      </linearGradient>
    </defs>
    <line x1="52" y1="262" x2="528" y2="262" stroke="#334155" stroke-width="1"/>
    <line x1="52" y1="200" x2="528" y2="200" stroke="#1e293b" stroke-width="1"/>
    <line x1="52" y1="138" x2="528" y2="138" stroke="#1e293b" stroke-width="1"/>
    <line x1="52" y1="76"  x2="528" y2="76"  stroke="#1e293b" stroke-width="1"/>
    <text x="52" y="26" fill="#94a3b8" font-size="11" font-family="ui-sans-serif,system-ui">Equity</text>
    <text x="476" y="282" fill="#94a3b8" font-size="11" font-family="ui-sans-serif,system-ui">Session &#8594;</text>
    <path d="M300,150 L300,205 L340,228 L380,206 L380,150 Z" fill="url(#ddFill)"/>
    <polyline points="60,238 140,206 220,166 300,128 380,98 460,80 520,62"
      fill="none" stroke="#fbbf24" stroke-width="2.4" stroke-dasharray="6 5"
      stroke-linecap="round" stroke-linejoin="round"/>
    <polyline points="60,238 140,224 220,200 300,194 340,222 380,202 460,184 520,170"
      fill="none" stroke="#38bdf8" stroke-width="3"
      stroke-linecap="round" stroke-linejoin="round"/>
    <line x1="520" y1="62" x2="520" y2="170" stroke="#64748b" stroke-width="1"/>
    <line x1="515" y1="62"  x2="525" y2="62"  stroke="#64748b" stroke-width="1"/>
    <line x1="515" y1="170" x2="525" y2="170" stroke="#64748b" stroke-width="1"/>
    <text x="532" y="112" fill="#cbd5e1" font-size="11" font-family="ui-sans-serif,system-ui" transform="rotate(90 532 112)">overfit gap</text>
    <text x="332" y="252" fill="#fb7185" font-size="11" font-family="ui-sans-serif,system-ui" text-anchor="middle">drawdown</text>
  </svg>
  <figcaption class="chart-caption">
    <span class="key"><span class="swatch amber"></span> In-sample, gross &middot; what you tuned</span>
    <span class="key"><span class="swatch sky"></span> Out-of-sample, net of costs &middot; what survives</span>
  </figcaption>
</figure>`;

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta name="description" content="TapeReplay replays the market tape minute by minute and tells you whether a day-trading strategy survives after costs, on data you never tuned against." />
  <meta property="og:title" content="TapeReplay: Honest day-trading backtester" />
  <meta property="og:description" content="Backtest like a skeptic. After costs, out-of-sample, no look-ahead." />
  <meta property="og:type" content="website" />
  <title>TapeReplay: Honest day-trading backtester</title>
  <link rel="stylesheet" href="./assets/site.css" />
</head>
<body>
  <main class="wrap">
    <p class="eyebrow"><span class="dot" aria-hidden="true"></span> Desktop day-trading backtester</p>
    <h1 class="brand">Tape<span class="replay">Replay</span></h1>
    <p class="tagline hero">
      Replay the market tape minute by minute and find out whether a strategy
      <strong>survives after costs</strong>, on <strong>data you never tuned against</strong>.
      A single green day is not evidence.
    </p>
    <div class="cta-row">
      <a class="btn btn-primary" href="#download">&#8595; Download</a>
      <a class="btn btn-secondary" href="#honest">How it stays honest</a>
      <a class="btn btn-secondary" href="${REPO_URL}">View source</a>
    </div>

    ${heroChart}

    <section id="download">
      <h2>Get TapeReplay</h2>
      <p class="sub">Free, local-first desktop app. Runs in mock-data mode out of the box, no API key needed to try it.</p>
      <div class="downloads" id="dl-list">
        ${downloadCards}
        ${patchCard}
      </div>
    </section>

    <section id="honest">
      <h2>Built to doubt you</h2>
      <p class="sub">TapeReplay is pessimistic on purpose. The real question is simple: durable edge after costs, or curve-fit to one lucky day?</p>
      <div class="honest-grid">
        <div class="honest-item">
          <h3><span class="tick">&#10003;</span> Train / test split</h3>
          <p>Tune freely on an in-sample window, then <span class="hl">commit</span> to freeze it. The headline number comes only from <span class="hl">out-of-sample</span> dates you never touched.</p>
        </div>
        <div class="honest-item">
          <h3><span class="tick">&#10003;</span> Costs baked in</h3>
          <p>Every fill pays commission, spread, and slippage through the cost model. You see gross and <span class="hl">net side by side, and net is the headline</span>.</p>
        </div>
        <div class="honest-item">
          <h3><span class="tick">&#10003;</span> Drawdown over return</h3>
          <p>Max drawdown, losing streaks, and time-to-recover lead the scoreboard. <span class="hl">Total return is de-emphasized</span>, because ruin risk is the real story.</p>
        </div>
        <div class="honest-item">
          <h3><span class="tick">&#10003;</span> No look-ahead</h3>
          <p>Entries see only prior bars and the current bar's open, never its close, high, or low. <span class="hl">A strategy cannot cheat with the future</span>.</p>
        </div>
      </div>
    </section>

    <section id="docs">
      <h2>Docs &amp; help</h2>
      <p class="sub">Get running, then learn why the verdicts look the way they do.</p>
      <div class="doclinks">
        <a class="doclink" href="./help/index.html"><span class="ic">&#9636;</span> Help guide</a>
        <a class="doclink" href="./help/honesty.html"><span class="ic">&#9636;</span> How it stays honest</a>
        <a class="doclink" href="./help/strategy-lab.html"><span class="ic">&#9636;</span> Strategy lab</a>
        <a class="doclink" href="./help/collecting-data.html"><span class="ic">&#9636;</span> Collecting data</a>
        <a class="doclink" href="./data/manifest.json"><span class="ic">&#9636;</span> Data manifest</a>
        <a class="doclink" href="${REPO_URL}"><span class="ic">&#8599;</span> Source on GitHub</a>
      </div>
    </section>

    <footer>
      <span class="disclaimer">
        Local-first: your market data and strategies stay on your machine. TapeReplay is a backtester, not a broker, and nothing here is financial advice.
      </span>
      <span>
        Installer v${escapeHtml(installerVersion)} &middot; patch ${escapeHtml(patchVersion)} &middot; released ${escapeHtml(releasedLabel)} &middot;
        CDN <code>${escapeHtml(cdnBase)}</code> &middot; <a href="${REPO_URL}">tape-replay</a> &middot; &#169; 2026 Jamey McElveen
      </span>
    </footer>
  </main>

  <script>
    // Highlight the download matching the visitor's OS. Progressive enhancement:
    // with JS off, every card stays visible and usable.
    (function () {
      var ua = navigator.userAgent || "";
      var plat = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || "";
      var s = (plat + " " + ua).toLowerCase();
      var pick = null;
      if (s.indexOf("win") !== -1) pick = "win";
      else if (s.indexOf("mac") !== -1 || s.indexOf("iphone") !== -1 || s.indexOf("ipad") !== -1) {
        pick = (s.indexOf("intel") !== -1 && s.indexOf("arm") === -1) ? "mac-intel" : "mac-arm";
      }
      if (!pick) return;
      var card = document.querySelector('.download-card[data-os="' + pick + '"]');
      if (!card) return;
      card.classList.add("suggested");
      var btn = card.querySelector(".btn");
      if (btn) { btn.classList.remove("btn-secondary"); btn.classList.add("btn-primary"); }
      var list = document.getElementById("dl-list");
      if (list && card !== list.firstElementChild) list.insertBefore(card, list.firstElementChild);
    })();
  </script>
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
