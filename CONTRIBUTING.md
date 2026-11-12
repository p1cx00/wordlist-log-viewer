# Contributing

Contributions are welcome. Please keep the viewer read-only, memory-bounded, and responsive on files that are much larger than available RAM.

## Before opening a pull request

1. Build the x64 application with `scripts/build.ps1`.
2. Run `scripts/test.ps1` and ensure all tests pass.
3. Avoid adding external runtime dependencies unless the benefit is substantial and documented.
4. Do not commit sample wordlists, credentials, personal data, generated search indexes, or local session files.
5. Update `CHANGELOG.md` for user-visible behavior.

## Code style

- Keep compatibility with .NET Framework 4.8 and the existing C# language level.
- Use `long` for byte offsets and file sizes.
- Never read the complete source file into memory.
- Keep cancellation and source-file-change checks in long-running operations.
- Route every user-visible string through `Localization.T` or `Localization.Format`.

## Translations

See [docs/LOCALIZATION.md](docs/LOCALIZATION.md). A language contribution must preserve all format placeholders and pass the localization tests.

By contributing, you agree that your contribution may be distributed under the MIT License.

