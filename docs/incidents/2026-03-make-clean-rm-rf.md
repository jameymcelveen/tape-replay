# Incident: `make clean` expanded to `rm -rf /*`

**Date:** 2026-03 (discovered during local dev)  
**Severity:** Critical — attempted deletion of system paths including `/Applications`  
**Status:** Mitigated in repo (`66ded19`); user data loss may have occurred where permissions allowed writes

## Summary

The Makefile `clean` target contained:

```makefile
rm -rf $(ARTIFACTS_DIR) $(RELEASE_DIR) $(CDN_DIST_DIR)/*
```

`CDN_DIST_DIR` was **never defined**. In Make, an undefined variable expands to empty, so `$(CDN_DIST_DIR)/*` became `/*`. The shell then expanded `/*` to top-level paths (`/Applications`, `/Users`, `/usr`, …).

macOS SIP and file permissions blocked most deletions; the user interrupted with Ctrl+C. Some application bundle contents under `/Applications` may have been damaged before permission denials stopped further harm.

## Root cause

1. Undefined Makefile variable + glob suffix.
2. No `ROOT_DIR` anchoring on delete paths.
3. No preflight validation before `rm -rf`.
4. AI-generated Makefile change without reviewing expanded shell command.

## Fix (in repository)

- Define `CDN_DIST_DIR := dist`
- Anchor all deletes under `$(ROOT_DIR)/...`
- Guard: refuse `clean` if path variables are empty or `ROOT_DIR` is `/`
- Project rule: `.cursor/rules/repo-boundary-safety.mdc`

## Reporting upstream

This class of bug should be escalated:

1. **Cursor** — Agent proposed/wrote a Makefile with a catastrophic `rm` expansion. Report via [Cursor Forum](https://forum.cursor.com) or in-app feedback, referencing: *undefined Make variable + `/*` glob in `rm -rf` suggested by agent*.
2. **TapeReplay** — Treat all agent-authored destructive scripts as requiring explicit path review before merge.

## User recovery checklist

- [ ] Verify `/Applications` apps still launch (especially any that showed `Permission denied` during the run).
- [ ] Restore from Time Machine or reinstall any apps that fail to open.
- [ ] Pull latest `main` (`66ded19` or later) before running `make clean` again.
- [ ] Never run `make clean` on an old checkout without the fix.

## Prevention checklist (agents and humans)

- [ ] Every `rm -rf` uses quoted paths under a known repo root.
- [ ] No `rm` target uses `*` on a variable without a default in the same file.
- [ ] Run `make -n clean` and inspect expanded commands before first execution.
- [ ] Agents must not modify files outside the git repository root.
