# JS patch updates (CDN)

TapeReplay ships a **full installer** only when the **major or minor** app version changes (e.g. `v0.1.0` to `v0.2.0`). **Patch** version tags (`v0.1.0` to `v0.1.1`) do not trigger installer CI.

Between installer releases, ship **JavaScript-only** updates as zip patches on your CDN.

## How it works

1. On startup (packaged app only), Electron fetches `manifest.json` from your CDN.
2. If `manifest.installerVersion` matches the installed app and `latestPatchVersion` is newer than the local patch, it downloads `patch_ver_X.Y.Z.zip`.
3. SHA256 is verified, the zip is unpacked to `userData/frontend-patch/`, and the UI loads from there instead of the bundled `frontend/dist`.

The .NET backend is **not** updated by patches. Ship a new installer for backend changes.

## Configure CDN URLs

Edit `electron/update-config.json` before building installers:

```json
{
  "manifestUrl": "https://cdn.example.com/tapereplay/manifest.json",
  "cdnBaseUrl": "https://cdn.example.com/tapereplay"
}
```

Override at runtime with env vars:

- `TAPEREPLAY_UPDATE_MANIFEST_URL`
- `TAPEREPLAY_UPDATE_CDN_BASE`

## manifest.json (on your CDN)

See `cdn/manifest.example.json`:

```json
{
  "installerVersion": "0.2.0",
  "latestPatchVersion": "0.1.1",
  "patchFile": "patch_ver_0.1.1.zip",
  "sha256": "abc123...",
  "releasedAt": "2026-06-16T00:00:00Z",
  "releaseNotes": "Fix strategy builder label."
}
```

| Field | Meaning |
|-------|---------|
| `installerVersion` | Must match `package.json` `version` baked into the installer |
| `latestPatchVersion` | Patch stream version (independent of installer patch semver) |
| `patchFile` | Zip name on CDN (`patch_ver_0.1.1.zip`) |
| `sha256` | Hex digest of the zip file |
| `patchUrl` | Optional full URL (else `cdnBaseUrl` + `patchFile`) |

## Build and publish a patch

```bash
make cdn-dist PATCH=0.1.1 SURGE_DOMAIN=tapereplay.surge.sh
cd dist && surge . tapereplay.surge.sh
```

`dist/` is the surge deploy folder. It contains:

| File | Purpose |
|------|---------|
| `manifest.json` | Auto-update metadata (generated with SHA256) |
| `patch_ver_X.Y.Z.zip` | Frontend-only JS/CSS/assets |
| `help/` | Static HTML documentation (linked from in-app Help menu) |
| `TapeReplay-*.{dmg,exe,zip}` | Optional — copied from `release/` when version matches |

Environment variables:

| Variable | Default | Meaning |
|----------|---------|---------|
| `PATCH` | `package.json` → `tapereplay.patchVersion` | Patch version in the zip name |
| `SURGE_DOMAIN` | `tapereplay.surge.sh` | Host for `cdnBaseUrl` in manifest |
| `CDN_BASE_URL` | — | Full URL override (e.g. `https://custom.example.com`) |
| `RELEASE_NOTES` | — | Optional `releaseNotes` in manifest |
| `SKIP_INSTALLERS=1` | default | Patch + manifest only |
| `INCLUDE_INSTALLERS=1` | — | Also copy matching installers from `release/` |

After first surge deploy, set `electron/update-config.json` to match:

```json
{
  "manifestUrl": "https://tapereplay.surge.sh/manifest.json",
  "cdnBaseUrl": "https://tapereplay.surge.sh"
}
```

`build-patch` is an alias for `cdn-dist`.

## Version tags and CI

| Tag change | Installer CI | What to ship |
|------------|--------------|--------------|
| `v0.1.0` -> `v0.1.1` | Skipped | CDN patch zip only |
| `v0.1.0` -> `v0.2.0` | Runs | New DMG / Setup.exe |
| `v0.2.0` -> `v1.0.0` | Runs | New DMG / Setup.exe |

Gate script: `scripts/semver-gate.sh v0.2.0`

## Local patch version

Bundled default: `package.json` -> `tapereplay.patchVersion` (e.g. `0.1.0`).

After a patch applies, the active version is stored in `userData/patch-state.json`.

The renderer can call `window.tapeReplay.getPatchInfo()` for installer + patch versions.
