# Architecture

Wordlist Viewer treats the source file as an immutable byte stream. It never creates one in-memory string for the complete file.

## Display path

1. `HugeFileEngine` resolves the encoding and reads bounded byte regions.
2. Page boundaries are adjusted to complete line boundaries where possible.
3. `MainFormV2` keeps only a small set of neighboring `ViewerChunk` objects.
4. `VirtualLineViewer` paints the visible rows and requests adjacent chunks as the user scrolls.
5. Very long lines are marked and opened in bounded segments by `LongLineForm`.

## Search path

Forward and backward searches stream bounded blocks. Full-file search makes one sequential pass and writes 64-bit match offsets to `FullSearchResultStore`. The UI reads those offsets in pages instead of retaining every match in RAM.

## Navigation index

`NavigationIndex` stores sparse line checkpoints. This provides an exact total line count and lets the viewer jump near a requested line before performing a short local scan.

## Session data

`ViewerSession` stores positions, display choices, and bookmarks under `%LOCALAPPDATA%\WordlistViewer`. It does not store source line text. Search result files are temporary and orphaned files are cleaned on startup.

## Invariants

- Source files are opened read-only.
- File and byte positions use 64-bit integers.
- Long operations are cancellable.
- A changed source file invalidates search and navigation artifacts.
- UI translations cannot change search semantics or file bytes.

