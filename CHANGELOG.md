# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/) · Versioning: [SemVer](https://semver.org/).

## [0.9.0] - Unreleased

First public release, published as a pre-release: DevSuite Hub and CacheSweep. No earlier
version was ever published.

### DevSuite Hub
- The screen that opens first. Lists the modules beside it and starts one when asked
- No network code, no download, no catalog; it does no work until a button is pressed
- Starts only the programs on its own fixed list, never another executable in the folder

### CacheSweep
- Measures about thirty developer-tool caches in parallel, only when Scan is pressed, with
  a working cancel button
- Nothing selected in advance; every row says what deleting it costs
- Confirmation window listing every path, opening on "Keep everything"
- A cache whose owning tool is running is held back and reported
- Temp files newer than seven days, `_npx` folders and browser profiles beside Playwright
  are kept
- Items that are costly to get back are hidden until turned on in Settings
- Unity Asset Store downloads and the `User Data` folder of every Chrome channel are
  protected at path level and re-checked right before each deletion
- Custom folders pass a guard when added, when settings load and again before deletion
- Every check judges where a path really leads, through junctions, symlinks and substituted
  drives; a path that cannot be resolved is refused
- Links inside a cache are unlinked, never followed; read-only files are handled
- Recycle Bin mode, honest about items too large for the bin; CSV and JSON export
- Refuses to close during a delete; cancelling stops between files and reports what remains
- Only one copy runs at a time
- Shows the last scan as soon as it opens, without measuring anything
- Quick scan re-measures only folders that look changed; Full scan measures everything
- Sizes reused from an earlier scan are marked with `~` and measured again before the
  confirmation
- Exports say which sizes are estimates
- New layout: one scrolling window with free space, cache total and change since the last scan
  at the top, and flat rows below
- Small draggable panel and round icon, always on top, with free disk space updated live. They
  measure folders only on request and cannot delete

### Project
- English by default; Turkish ships as a separate package with `lang.json`
- Build driven by manifests, with no SDK or dependency; writes only to its output folders
- Per-user installer, portable zips, `SHA256SUMS.txt`; a version tag creates a draft release
  for review
- Tests for the guard, the delete engine and the Hub, opening no window

### Experimental, not part of the release
- MCP Control, MCP Setup and Skill Manager moved to `extras/`, built only with `-Extras`.
  See [extras/README.md](extras/README.md) for their known problems
