# Changelog

## 2.3.2 — 2026-11-12

- Search results now navigate on a single left click; Enter remains supported.
- Fixed a stale-scroll race that could move the viewer roughly one 256 KB block past the selected match.
- Background neighbor loading now preserves the selected line, exact top offset, and active-match highlight.
- Cancelled or superseded result navigation no longer updates the UI as if it succeeded.
- Added regression coverage for viewer-state preservation and window-replacement chunk requests.

## 2.3.1 — 2026-11-12

- Clarified the product identity as a huge text, wordlist, dictionary, and archived log viewer.
- Added `.log`, `.out`, `.trace`, `.jsonl`, and `.ndjson` to the localized file picker.
- Added explicit scope notes: archived/exported logs are supported; live tailing and structured log analytics are not.
- Added GitHub repository metadata guidance and a repository banner.

## 2.3.0 — 2026-11-12

- Added complete Spanish, French, German, and Brazilian Portuguese translations.
- Kept English as the default language and retained Turkish support.
- Made language selection data-driven instead of relying on fixed list positions.
- Added persistent language codes and culture-specific number formatting for all six languages.
- Added a GitHub-ready source layout, CI workflow, build scripts, documentation, and MIT license.

## 2.2.0

- Added live English and Turkish interface localization.
- Added persistent language selection with English as the default.

## 2.1.0

- Added disk-backed full-file search results and an exact total match count.
- Added result pagination and safe free-space checks.

