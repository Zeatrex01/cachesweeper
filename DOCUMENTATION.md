# DevSuite — technical documentation

Comments in the code say *what* it does. This file says *why* it is built this way.
A Turkish translation lives at [`docs/tr/DOCUMENTATION.tr.md`](docs/tr/DOCUMENTATION.tr.md).

---

## 1. The problem

Three symptoms on one machine:

1. Several console windows flashing at boot.
2. The machine getting slower over time.
3. Free disk space dropping from 50 GB to 30 GB with no obvious cause.

### Diagnosis

**Disk.** A WizTree scan of the NTFS master file table, exported to CSV, showed
`%LOCALAPPDATA%` alone at **133 GB**:

| GB | Folder | What |
|---|---|---|
| 22.5 | `Google\Chrome\User Data` | profiles, cache, on-device model |
| 19.2 | `Unity` | 18.6 GB of it Package Manager cache |
| 17.6 | `uv\cache\archive-v0` | Python package archive |
| 10.5 | `Google\DriveFS` | Drive offline copies |
| 7.0 | `Android\Sdk` | NDK, emulator, sources, build-tools |
| 2.2 | `npm-cache` | 2 GB of it `_npx` |
| 2.0 | `SquirrelTemp` | abandoned installers |

Nothing malicious, nothing downloading in the background — just development tool
caches. None of them clean up after themselves.

**Console windows and slowdown.** AI coding clients start **one process per configured
MCP server**. Those run through `npx` / `uvx`, which briefly opens a console window on
Windows. Ten configured servers means ten processes and ten windows. On top of that,
every installed skill's description is loaded into context on every conversation, so
bloated descriptions cost both money and latency.

The disk symptom became the product: CacheSweep, opened from a small Hub. The other two
became experimental tools that stay out of the release; see [`extras/README.md`](extras/README.md).

---

## 2. What was used, and why

| Choice | Reason |
|---|---|
| **C# / WinForms** | One `.exe`, no installer, no dependencies. Electron is 150 MB; Tauri needs Rust. |
| **.NET Framework 4.x** | Ships with Windows 10 and 11. The user downloads nothing. |
| **`csc.exe`** | The C# compiler that ships with the framework, at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. No SDK, no MSBuild, no Visual Studio. |
| **PowerShell** for the build | So the thing that runs the compiler does not itself need compiling. |
| **`System.Web.Extensions`** | `JavaScriptSerializer` for JSON in the extras — already in the framework, no NuGet package. |
| **`Microsoft.VisualBasic`** | `FileSystem.DeleteDirectory(..., SendToRecycleBin)` is the only built-in way to reach the Recycle Bin. |
| **Inno Setup** | Free, standard Windows installer generator. |
| **GitHub Actions** | A version tag produces a draft release with the zips, the installer and checksums, for a person to review and publish. |

**Deliberately not used:** no NuGet packages (so a single file can be compiled with
`csc`), no JSON library (the framework has one), no TOML library — line-based editing
preserves the user's comments and ordering, a serialiser would rewrite their file.

---

## 3. Architecture

```
hub/Hub.cs + app.json              the first screen
apps/CacheSweep/                   the module: window, MiniWindow, Targets, Sweeper, ScanMemory, RunningTools,
                                   ConfirmWindow, Widgets
shared/Tokens.cs                   design tokens: spacing, type, colour, DPI
shared/Native.cs                   dark title bar and scrollbars (Win32)
shared/Ui.cs                       components and the window shell
shared/Logo.cs                     the logo, drawn in code so it stays sharp
extras/<Name>/ + extras/shared/    experimental tools, not part of a release
lang/tr.json                       language pack
tools/build.ps1                    manifest-driven build
tests/                             guard, sweeper, Hub and memory tests, opening no window
installer/ · .github/workflows/    distribution
```

### Manifest-driven build

No application name appears in the build script. It scans `apps/*/app.json`, reads
`sources` and `references`, and hands them to `csc.exe`. The Hub is built the same way from
`hub/app.json`, and `-Extras` also scans `extras/*/app.json`.

```json
{
  "name": "Your App",
  "en": "What it does",
  "color": "#3B82F6", "glyph": "Y",
  "sources":    [ "apps/YourApp/YourApp.cs", "shared/Tokens.cs",
                  "shared/Native.cs", "shared/Ui.cs" ],
  "references": [ ]
}
```

Adding an app means adding a folder and a manifest. The build, the installer and the
release all pick it up with no edits. To get a button in the Hub it also needs one line in
the Hub's module list, for the reason below.

### The Hub lists and starts, nothing more

The Hub opens first, and modules do no work until they are opened: at startup it only checks
whether each known file exists. Starting a module takes a button press, and it is the one
`Process.Start` in the file, which `tests/HubTests.ps1` checks along with the absence of
network code.

An earlier version was a store. It merged a `catalog.json` with every `.exe` it found beside
it, and could download new ones. That made it a way to run whatever was in the folder (an
uninstaller appeared as an app), and the download path was only as trustworthy as an
unsigned catalog. For a program whose whole promise is that it does nothing you did not ask
for, none of this was worth having, so the module list is now fixed in the code and a module
appears only when that exact file sits beside the Hub.

### Localisation

The binaries are English. A language pack is a **separate package**: the same
executables plus a `lang.json` beside them, which `Loc.LoadPack()` reads at startup and
uses to override the built-in strings. The build produces `dist/` (English) and
`dist-tr/` (English binaries + Turkish `lang.json`). Adding a language means copying
`lang/tr.json`, translating the values, and shipping it as `lang.json`.

---

## 4. The apps

### CacheSweep

Measures roughly thirty known cache locations when you press Scan, and empties the
**contents** of the ones you tick. Opening the window does no work; a setting turns on
scanning at open for anyone who wants it, and it is off by default.

Measuring a folder means walking every file in it. Three things keep that fast and
stable: **parallel measurement** (`Parallel.ForEach`, one job per core, so total time
is the slowest folder rather than the sum), a **stack-based walk** instead of recursion
so one permission error does not kill the scan, and **reparse-point skipping** so
junctions cannot cause infinite loops.

**The guard.** Every delete goes through one place, `DeleteEntry`, and every custom
folder goes through one place, `Guard.Reject`. The guard runs three times — when a folder
is added, when settings are loaded, and again right before its contents are removed — so
a folder that was harmless when added and became a project later is refused at delete
time. It rejects drive roots, UNC paths, the user profile and its well-known folders
(Desktop, Documents, Downloads, Pictures, AppData, OneDrive, the dot-folders that hold
credentials and AI-client state), Windows and Program Files in either direction, anything
overlapping a protected root, folders that contain a project marker (`.git`, `.sln`,
`package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod`, `Assets` + `ProjectSettings`
and friends), and folders whose children are checked-out projects — a `repos` folder is
not a cache. Inside a cache, only entries directly under the target folder are touched,
junctions and symlinks are unlinked rather than followed, and every single entry is
tested against the protected roots (Unity Asset Store, the `User Data` folder of every
Chrome channel) before it goes.

Every check judges a path twice: as written, and where it really leads. Windows reports
the final location (`GetFinalPathNameByHandle`) through junctions, symlinks and
substituted drives, so a junction to Documents counts as Documents, and a path that
cannot be resolved is refused. A built-in target that has been redirected takes the full
guard too, and the confirmation dialog shows its real location. A junction inside a
cache that points at a protected root is left alone and reported as blocked.
`tests/GuardTests.ps1` covers the junction cases.

**Rules per target.** `apps/CacheSweep/Targets.cs` holds the list, and a target can carry
rules: `Needs` names the programs that must not be running, `Keep` names entries left in
place (`_npx` under npm, `mcp-*` browser profiles beside Playwright), `OlderThan` keeps
recent files (seven days in Temp), and `AdvancedOnly` hides items that are costly to get
back. Windows Update's download folder used to be a target and was removed: it belongs to a
system service, and clearing it can break an update in progress.

**One engine for measuring and deleting.** `Sweeper.cs` decides which entries a target
contains, and the same function feeds the size and the delete, so the number on screen is
what gets removed. Deleting walks the tree itself instead of calling `Directory.Delete`,
which fails on read-only files and offers no cancellation between files. Links are removed
rather than entered.

**Remembered scan.** `ScanMemory.cs` keeps what the last scan measured, so the window can
show it at once and measure nothing on open. A quick scan compares a fingerprint of each target
(names, dates and sizes three levels down, at most 20,000 entries) and re-measures only what
differs. On the machine this was built on, fingerprints took 352 ms in total against 41.5 s for
walking the same caches one after another, measured with a warm disk cache.

That speed has a price: a folder's date only reflects its direct children, so a change deeper
down can be missed. Three rules keep that honest. A reused size is marked `~`. Targets with an
age rule are always measured, because the clock changes their answer. Selected folders are
measured again before the confirmation, so a remembered number never reaches it. The file
supplies sizes only: it is looked up by each target's own key, so it can never add a folder to
the list. A false alarm just costs one extra measurement.

**Floating modes.** `MiniWindow.cs` holds a small panel and a round icon, both draggable and
always on top. The full window stays alive, hidden, behind them. The only thing they read on
a timer is free disk space, one system call every five seconds and only while one of them is
showing. Folder sizes come from the last scan, Quick scan is the only way to measure from
there, and they have no way to delete: that always goes through the confirmation in the full
window.

**Confirmation.** `ConfirmWindow.cs` lists every path in a scrollable box, states what is
held back and why, and opens on "Keep everything", so pressing Enter deletes nothing. The
main window refuses to close during a delete, and cancelling stops between files and reports
what remains. A mutex keeps it to one copy, so two windows cannot delete from one folder.

**Why there are no package-manager commands.** An earlier version ran `uv cache clean`,
`npm cache clean --force`, `pnpm store prune` and `pip cache purge` after deleting. They
deleted outside the confirmation dialog, ignored the Recycle Bin setting, and matched
targets by their localised display name. They are gone; the folders are emptied like any
other target and the package managers rebuild their indexes on the next run.

**Recycle Bin.** The setting is read once, before the background task starts, so a
settings change mid-run cannot flip behaviour half way. The Settings and Export buttons
are disabled while a scan or delete is running. `FileSystem.DeleteDirectory(...,
SendToRecycleBin)` with `OnlyErrorDialogs` permanently deletes items too large for the
bin without asking, so the option and the confirmation dialog say so in plain words.
A delete only counts as done when the path is really gone afterwards.

**Scan edge cases.** A cancelled measurement returns `-1` and shows `?` instead of a
partial number that looks final; a scan with no targets finishes immediately instead of
leaving the window in the busy state; callbacks from a superseded scan are ignored
through a generation counter; a malformed `custom=` line in `settings.ini` is dropped
instead of crashing at startup.

Locked files are skipped, reported, and the likely culprit is guessed from the path.
Settings live in `%APPDATA%\CacheSweep\settings.ini`.

The tools below are experimental, live in `extras/`, and are not part of a release. Their
known problems are listed in [`extras/README.md`](extras/README.md).

### MCP Control

| Client | Config | Format |
|---|---|---|
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` | JSON |
| Claude Code | `%USERPROFILE%\.claude.json` | JSON |
| Codex / ChatGPT | `%USERPROFILE%\.codex\config.toml` | TOML |

Two different disable mechanisms, for a reason. **Codex/ChatGPT** have an official
`enabled` field, so the file is edited line by line: find the `[mcp_servers.<name>]`
block, write `enabled = false`, leave everything else — comments, ordering, formatting —
untouched. **Claude** has no such field, so the entry is *moved* from `mcpServers` to
`mcpServersDisabled`; Claude ignores the unknown key, the server never starts, and the
settings survive. Re-enabling moves it back.

Writing rules, all in `extras/shared/Mcp.cs`: a timestamped `.bak` (unique even within the same
second) before every change; the new text goes to a temp file and is swapped in with
`File.Replace`, so a crash mid-write cannot leave a truncated config; newline style and
BOM of the original are preserved; numbers round-trip through the `R` format. A file that
cannot be parsed is listed in `Mcp.Errors`, shown once, and never written to. A server
that exists in both `mcpServers` and `mcpServersDisabled` is refused rather than
silently overwritten. On the TOML side, `enabled = true  # comment` keeps its comment,
multi-line arrays cannot be mistaken for the end of a table, a sub-table such as
`[mcp_servers.x.env]` is not mistaken for the server's own keys, and adding a server
whose table already exists replaces the table in place instead of appending a duplicate.
Server names are validated (`[A-Za-z0-9_-]`, up to 64) before they reach either format,
because a dot or a space would break the TOML table name.

The footer reads *"14 servers · 11 on — 11 processes start at launch"*, which is the
direct answer to the console-windows question.

### MCP Setup

Templates (filesystem, github, memory, postgres, puppeteer, uvx), a real test run of the
command with a six-second timeout, a live preview of both the JSON and TOML form, and
one-shot writing to every selected client in its own format. Existing names prompt before
being overwritten; a client whose config cannot be parsed is skipped with a message
rather than guessed at.

### Skill Manager

Scans `~/.claude/skills`, `~/.claude/plugins` and `%APPDATA%\Claude\skills` for
`SKILL.md`, seven levels deep so marketplace and cache layouts
(`plugins/cache/<marketplace>/<plugin>/<version>/skills/<name>`) are found, skipping
`node_modules`, `.git` and reparse points. Roots are de-duplicated case-insensitively
(NTFS treats `skills` and `Skills` as one folder). The provider is derived from the path
— the folder before `skills`, or the one before that when it looks like a version number
— and shown as a coloured badge.

Disabling moves the folder into a sibling `_disabled/`; nothing is deleted. A skill is
considered disabled when *any* ancestor is named `_disabled`. A toggle that cannot be
done (target exists, already disabled) throws with a plain reason instead of silently
doing nothing. Flags:
duplicate names, missing descriptions, descriptions over 500 characters, `SKILL.md`
over 50 KB, and triggers broad enough to fire on everything. These are the things that
quietly consume context on every conversation.

---

## 5. Design system

`shared/Tokens.cs` holds the tokens; no component carries its own numbers.
Every measurement in them is a logical pixel, as if the screen were 96 dpi:
`Dpi.Px` converts one for anything Windows positions, and custom painting
draws onto a canvas `Draw.Quality` has already scaled, so paint code keeps
the numbers it was written with.

- `Space` — 8pt grid: 4 / 8 / 12 / 16 / 20 / 24 / 32
- `Fonts` — H1, H2, Title, Body, Label, Small, Micro, Numeric, Mono
- `Palette` — **semantic** names (`Accent`, `Warning`, `TextSecondary`), never "blue",
  so the names stay correct when the theme flips

`Palette.Apply(dark)` reassigns every token and raises `Changed`; windows repaint
themselves. The preference is stored in `%APPDATA%\DevSuite\theme.txt` and shared by all
apps.

`Loc` keeps two dictionaries: the built-in English strings that each app registers with
`Add()`, and the strings from `lang.json`. They are kept apart so an app's `Add()` can
never overwrite a translation, whichever runs first.

Components: `UiButton` (five variants with real hover/pressed/disabled states), `Row`
(status dot, title, badges, subtitle, right-hand value with a proportional bar, actions
revealed on hover), `FilterBar` (chips with counts), `SearchBox`, `EmptyState`,
`ProgressLine`, `Shell` (header, toolbar, list, footer).

---

## 6. Bugs worth remembering

**Ambiguous `Timer`.** `System.Threading` and `System.Windows.Forms` both define it;
`CS0104`. Fixed by fully qualifying `System.Windows.Forms.Timer`.

**WinForms dock order.** Docking processes the `Controls` collection last-added-first —
`Controls[0]` is laid out **last** and gets whatever space every later-added sibling left
behind. The `Fill` panel must therefore be added **first** (making it `Controls[0]`),
with edge-docked strips added after it and the outermost strip added **last**. Added in
the wrong order the `Fill` panel swallows the whole window and the strips overlap it;
`BringToFront()` calls made it worse.

**Scan freezing.** The first PowerShell version measured folders sequentially and locked
up on the `uv` cache. Replaced by `robocopy /L` with a timeout, then by parallel
measurement in C#.

**Name clashes.** `DevSuite.Button` and `DevSuite.Type` were ambiguous with
`System.Windows.Forms.Button` and `System.Type` in every app that imports both
namespaces (`CS0104`). Renamed to `UiButton` and `Fonts`. In the same spirit, no field is
named after a type it shadows (`Path`, `Version`, `Text` on a control).

**csc.exe and encodings.** The compiler that ships with the framework reads a source
file without a BOM in the system code page. Every `.cs` file that contains a non-ASCII
character therefore carries a UTF-8 BOM; `.bat` files never do.

---

## 7. Distribution

Local: `BUILD.bat` → `tools\build.ps1` → `dist\` and `dist-tr\`; with `-Extras`, also
`dist-extras\`. The build writes only to those folders.

Release: a tag such as `v0.9.0` builds on a Windows runner, runs the tests, produces the two
portable zips (English, English + Turkish pack), the per-user Inno Setup installer (with the
Turkish pack as an optional component) and `SHA256SUMS.txt`, and creates a **draft** release.
Nothing is public until a person presses Publish.

### Signing

| Option | Cost | Status |
|---|---|---|
| Unsigned | 0 | current state, verifiable through SHA256 |
| Self-signed | 0 | **useless** — SmartScreen still blocks |
| Azure Artifact Signing | ~$9.99/mo | individuals limited to US/Canada |
| OV certificate | $150–300/yr | workable |
| EV certificate | $400+/yr | no advantage over OV since 2024 |
| SignPath (open source) | free | requires application; the realistic target |

Signing is not wired into the release workflow yet.

---

## 8. Known limits

- Windows only.
- CacheSweep still carries its own copy of the theme/button code instead of using
  `shared/Ui.cs`. It shares `Tokens.cs` and `Native.cs`, so scaling and window
  chrome behave the same; the components are still duplicated.
- The guard is a list of rules, not a proof: a cache folder can still hold something
  precious if it was put there by hand. Read the confirmation dialog.
- Skill Manager (experimental): skill roots are fixed; project-level `.claude/skills` folders are not scanned.
- The Hub has no update check, by choice: it has no network code at all. New versions are
  found on the releases page.
- The tests cover the guard, the delete engine and which modules the Hub lists. Windows and
  clicks are checked by hand.

---

MIT © 2026 Enes Aksu
