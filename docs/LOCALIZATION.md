# Localization

English is the source and fallback language. The application currently supports English, Turkish, Spanish, French, German, and Brazilian Portuguese.

## Files

- `src/Localization.cs` contains the English and Turkish dictionaries plus the localization API.
- `src/Localization.MoreLanguages.cs` contains the supported-language list, culture/code mapping, and the Spanish, French, German, and Portuguese dictionaries.

## Adding a language

1. Add a value to `AppLanguage`.
2. Add it to `Localization.SupportedLanguages`.
3. Add its native display name, culture, persistent code, parser mapping, and dictionary selection.
4. Create a dictionary by copying English and overriding every user-visible key.
5. Add representative assertions to `tests/EngineTests.cs`.
6. Run `scripts/test.ps1` and manually verify the live language switch.

## Rules

- Preserve placeholders exactly, including indices and format specifiers such as `{0:N0}` and `{1:hh\:mm\:ss}`.
- Preserve `\r\n` where the source message uses multiple lines.
- Keep the language name in its native form in the selector.
- Do not translate product names, byte values, file extensions, or encoding identifiers unless the descriptive text requires it.
- Every user-visible runtime message must use `Localization.T` or `Localization.Format`.
- Missing keys intentionally fall back to English, but a completed built-in translation should cover every translatable key.

Language preference is stored as a short code in `%LOCALAPPDATA%\WordlistViewer\language.txt`. Unknown or corrupt values fall back to English.

