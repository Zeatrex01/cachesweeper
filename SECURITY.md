# Security

This covers the release: DevSuite Hub and CacheSweep. The experimental tools in `extras/`
are not part of it and have [their own list of known problems](extras/README.md).

## What the release does, and does not do

**It does**

- Measure folders when you press Scan
- Delete the **contents** of the cache folders you select, after you confirm
- Start CacheSweep when you press Open in the Hub
- Save its settings to `%APPDATA%\CacheSweep\settings.ini` and `%APPDATA%\DevSuite\theme.txt`
- Save the last scan's folder paths and sizes to `%APPDATA%\CacheSweep\last-scan.txt`

**It does not**

- Use the network. Neither program contains network code; `tests/HubTests.ps1` checks this
  for the Hub.
- Collect telemetry, analytics or usage data.
- Start with Windows or delete anything on a schedule. It runs only while a window of it is
  open, including the small panel and icon you can switch to. Those read free disk space every
  few seconds, measure folders only when you press Quick scan, and cannot delete anything.
- Run package-manager commands. Everything that is deleted appears in the confirmation
  window first.

## Guard rails

- Nothing is selected in advance.
- The confirmation window lists **every** affected path and opens on "Keep everything".
- Only entries directly inside a target folder are removed. Junctions and symlinks are
  unlinked, never followed.
- Unity Asset Store downloads and the `User Data` folder of every Chrome channel are
  protected at path level: each entry is checked against the protected roots immediately
  before it is deleted.
- Every check judges a path as written and where it really leads, through junctions,
  symlinks and substituted drives. A path that cannot be resolved is refused, and the
  confirmation window shows the real location of a redirected folder.
- Custom folders are checked by `Guard.Reject` when added, when settings load, and again
  right before deletion. Refused: drive roots, UNC paths, the user profile and its
  well-known folders (Desktop, Documents, Downloads, Pictures, Music, Videos, AppData,
  OneDrive, `.ssh`, `.gnupg`, `.aws`, `.azure`, `.kube`, `.docker`, `.config`, `.claude`,
  `.codex`, `.cursor`, `.vscode`), Windows, Program Files and ProgramData, anything
  overlapping a protected root, project folders (`.git`, `.svn`, `.hg`, `.sln`, `.csproj`,
  `package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod`, `pom.xml`, `build.gradle`,
  `CMakeLists.txt`, `Makefile`, `Assets` + `ProjectSettings`, ...) and folders whose direct
  children are checked-out projects.
- A cache whose owning tool is running is held back, and the confirmation window says so.
- Only one copy of CacheSweep runs at a time.
- The last-scan file only supplies sizes to show. The folders to act on always come from the
  program's own list, so editing that file cannot add anything to a delete, and selected
  folders are measured again before the confirmation.
- The Recycle Bin setting is read once before a delete starts; the window states that items
  too large for the bin are deleted permanently.
- The Hub starts only the programs on its own fixed list, and only if that exact file sits
  beside it. It does not list or run any other executable in the folder.

## Limits

- The guard is a set of rules, not a proof. A file placed by hand inside a cache folder looks
  like cache. Read the list before you confirm.
- CacheSweep runs with your rights and can delete what you can delete. Run it as
  administrator only if you need Windows Temp.
- The Hub starts whatever file carries a listed name beside it. Keep the program in a folder
  only you can write to; the installer's per-user location is one.

## Reporting a vulnerability

Please use **Report a vulnerability** on the repository's Security tab rather than opening a
public issue.

## Signing

Releases are not code-signed yet, so SmartScreen warns on first run. Verify downloads against
`SHA256SUMS.txt` in the release, or build from source.
