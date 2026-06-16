import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { pipeline } from 'node:stream/promises';
import { createWriteStream } from 'node:fs';
import { Readable } from 'node:stream';
import extract from 'extract-zip';
import { app } from 'electron';
import { fileURLToPath } from 'node:url';
import { comparePatchVersion } from './patchVersion.js';

export { comparePatchVersion };

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootPackage = JSON.parse(
  fs.readFileSync(path.join(__dirname, '..', 'package.json'), 'utf8'),
);

const PATCH_STATE_FILE = 'patch-state.json';
const PATCH_DIR_NAME = 'frontend-patch';

function readUpdateConfig() {
  const bundled = path.join(process.resourcesPath ?? '', 'update-config.json');
  const defaults = {
    manifestUrl: process.env.TAPEREPLAY_UPDATE_MANIFEST_URL ?? '',
    cdnBaseUrl: process.env.TAPEREPLAY_UPDATE_CDN_BASE ?? '',
  };

  if (fs.existsSync(bundled)) {
    const file = JSON.parse(fs.readFileSync(bundled, 'utf8'));
    return {
      manifestUrl: file.manifestUrl || defaults.manifestUrl,
      cdnBaseUrl: file.cdnBaseUrl || defaults.cdnBaseUrl,
    };
  }

  return defaults;
}

function patchStatePath() {
  return path.join(app.getPath('userData'), PATCH_STATE_FILE);
}

function patchDir() {
  return path.join(app.getPath('userData'), PATCH_DIR_NAME);
}

export function readPatchState() {
  const installerVersion = app.getVersion();
  const defaultPatch = rootPackage.tapereplay?.patchVersion ?? '0.1.0';

  try {
    const raw = fs.readFileSync(patchStatePath(), 'utf8');
    const state = JSON.parse(raw);
    return {
      installerVersion: state.installerVersion ?? installerVersion,
      patchVersion: state.patchVersion ?? defaultPatch,
    };
  } catch {
    return { installerVersion, patchVersion: defaultPatch };
  }
}

function writePatchState(state) {
  fs.mkdirSync(path.dirname(patchStatePath()), { recursive: true });
  fs.writeFileSync(patchStatePath(), JSON.stringify(state, null, 2));
}

async function downloadFile(url, destination) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Download failed (${response.status}): ${url}`);
  }

  if (response.body) {
    await pipeline(Readable.fromWeb(response.body), createWriteStream(destination));
    return;
  }

  const buffer = Buffer.from(await response.arrayBuffer());
  fs.writeFileSync(destination, buffer);
}

function verifySha256(filePath, expected) {
  if (!expected || expected.startsWith('replace-with')) {
    return;
  }

  const hash = crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex');
  if (hash.toLowerCase() !== expected.toLowerCase()) {
    throw new Error(`SHA256 mismatch for patch (expected ${expected}, got ${hash})`);
  }
}

/**
 * Fetch manifest.json from CDN, download patch zip if newer, unpack to userData.
 */
export async function checkAndApplyPatchUpdate() {
  if (!app.isPackaged) {
    return { status: 'skipped', reason: 'dev mode' };
  }

  const config = readUpdateConfig();
  if (!config.manifestUrl) {
    return { status: 'skipped', reason: 'no manifestUrl configured' };
  }

  const local = readPatchState();
  const installerVersion = app.getVersion();

  let manifest;
  try {
    const response = await fetch(config.manifestUrl, { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`Manifest fetch failed (${response.status})`);
    }

    manifest = await response.json();
  } catch (error) {
    console.error('[patch-update] manifest error:', error.message);
    return { status: 'error', reason: error.message };
  }

  if (manifest.installerVersion !== installerVersion) {
    return {
      status: 'skipped',
      reason: `installer mismatch (app ${installerVersion}, manifest ${manifest.installerVersion})`,
    };
  }

  const remotePatch = manifest.latestPatchVersion;
  if (!remotePatch || comparePatchVersion(remotePatch, local.patchVersion) <= 0) {
    return {
      status: 'current',
      patchVersion: local.patchVersion,
      installerVersion,
    };
  }

  const cdnBase = (manifest.cdnBaseUrl || config.cdnBaseUrl || '').replace(/\/$/, '');
  const patchFile = manifest.patchFile ?? `patch_ver_${remotePatch}.zip`;
  const patchUrl = manifest.patchUrl ?? `${cdnBase}/${patchFile}`;

  const tempZip = path.join(app.getPath('temp'), patchFile);
  const targetDir = patchDir();

  try {
    console.log(`[patch-update] downloading ${patchUrl}`);
    await downloadFile(patchUrl, tempZip);
    verifySha256(tempZip, manifest.sha256);

    fs.rmSync(targetDir, { recursive: true, force: true });
    fs.mkdirSync(targetDir, { recursive: true });
    await extract(tempZip, { dir: targetDir });

    writePatchState({ installerVersion, patchVersion: remotePatch });
    fs.unlinkSync(tempZip);

    console.log(`[patch-update] applied patch ${remotePatch}`);
    return {
      status: 'applied',
      patchVersion: remotePatch,
      installerVersion,
      releaseNotes: manifest.releaseNotes ?? null,
    };
  } catch (error) {
    console.error('[patch-update] apply failed:', error.message);
    fs.rmSync(tempZip, { force: true });
    return { status: 'error', reason: error.message };
  }
}

/**
 * Resolve index.html: patched frontend in userData, else bundled dist.
 */
export function resolveFrontendIndex(rootDir) {
  const patched = path.join(patchDir(), 'index.html');
  if (fs.existsSync(patched)) {
    return patched;
  }

  return path.join(rootDir, 'frontend', 'dist', 'index.html');
}

export function getPatchInfo() {
  const state = readPatchState();
  return {
    installerVersion: state.installerVersion,
    patchVersion: state.patchVersion,
    patchDir: patchDir(),
    usingPatch: fs.existsSync(path.join(patchDir(), 'index.html')),
  };
}
