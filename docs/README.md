This folder holds everything that supports the root docs but isn't meant to be read on
its own:

- `screenshots/` — the images the root `README.md` embeds. See its own `README.md` for
  the exact file names expected.
- `tr/` — the Turkish translation of `README.md` and `DOCUMENTATION.md`. Kept in its own
  subfolder instead of `*.tr.md` files at the root so the two languages don't interleave
  in a directory listing.

The actual documentation lives at the repository root: `README.md` (overview),
`DOCUMENTATION.md` (design decisions and internals), `SECURITY.md`, `CHANGELOG.md`.
