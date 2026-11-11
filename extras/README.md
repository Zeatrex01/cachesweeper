# Experimental extras

MCP Control, MCP Setup and Skill Manager came first as personal tools. They edit the
configuration of AI coding clients, which is riskier than clearing a cache, and they have
not reached the standard of the rest of the project. So they are not in the release, the
installer never adds them, and a default build leaves them out.

```bat
BUILD.bat -Extras
```

This builds them into `dist-extras\` with a copy of the Hub, which lists them because they
sit beside it. **Back up your configuration files before you use them.**

| Tool | What it does |
|---|---|
| **MCP Control** | Lists MCP servers across Claude Desktop, Claude Code and Codex, and switches each on or off |
| **MCP Setup** | Adds a new MCP server from a template, with a test run, to several clients |
| **Skill Manager** | Lists Claude skills with their provider, switches them on or off, edits `SKILL.md` |

## Known problems

These came out of a review of the code. None has been fixed yet.

### MCP Control and MCP Setup

- Some valid TOML layouts can be misread, so an edit to `config.toml` can damage it.
- An empty `.claude.json` counts as a valid file and can be overwritten. Claude Code
  rewrites that file itself while running and nothing coordinates the two, so a change can
  be lost. Close Claude Code first.
- Claude Desktop installed from the Microsoft Store keeps its configuration in a different
  place, which is not looked at.
- The `.bak` files left beside a configuration hold the same tokens as the original, in
  plain text. Keep that in mind before zipping or sharing the folder.
- The temp file used during a write has a fixed name.

### MCP Setup only

- **The test run executes the command you typed**, through `cmd`. Run only commands you trust.
- The test run can report success even when the command exited with an error, and stopping
  it can leave child processes running.
- Templates use `npx -y` without a fixed version, so they run whatever is newest at that
  moment. The filesystem template defaults to `C:\Users`, which is far too broad. The
  postgres template points at a deprecated package.
- The preview can differ from what is written.

### Skill Manager

- Skills from plugins or synced from elsewhere should be read-only here, but are not. An
  update can bring a disabled skill back.
- Folders such as `.trash` and marketplaces can appear in the list.
- A `.bak` left inside a skill folder gets packaged along with that skill.
- Saving `SKILL.md` does not use a temp file, so a crash mid-save can leave it truncated.

## What is safe about them today

Switching a server or skill off is a move or a flag rather than a delete, and each one asks
first. Each configuration write leaves a `.bak` copy. A file that cannot be parsed is
reported and left alone.
