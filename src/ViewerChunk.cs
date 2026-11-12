using System;
using System.Collections.Generic;

namespace WordlistViewer
{
    internal sealed class ViewerChunk
    {
        public PageData Page;
        public List<ViewerLine> Lines;
        public long FirstLineNumber;
    }

    internal static class ViewerLineBuilder
    {
        public static List<ViewerLine> Build(
            PageData page,
            ResolvedEncoding encoding,
            ViewerDisplayMode mode,
            long firstLineNumber)
        {
            List<ViewerLine> result = new List<ViewerLine>();
            byte[] bytes = page.ContentBytes ?? new byte[0];
            if (mode == ViewerDisplayMode.Hex)
            {
                for (int offset = 0; offset < bytes.Length; offset += 16)
                {
                    int count = Math.Min(16, bytes.Length - offset);
                    ViewerLine line = new ViewerLine();
                    line.LocalLineNumber = firstLineNumber + result.Count;
                    line.ByteOffset = page.ContentOffset + offset;
                    line.ByteLength = count;
                    line.Text = DisplayFormatter.FormatHexRow(bytes, offset, count);
                    line.IsContinuation = page.TruncatedAtStart && offset == 0;
                    line.IsTruncated = page.TruncatedAtEnd && offset + count >= bytes.Length;
                    result.Add(line);
                }
                return result;
            }

            byte[] newline = encoding.NewLineBytes;
            byte[] carriageReturn = encoding.Encoding.GetBytes("\r");
            int start = 0;
            while (start < bytes.Length)
            {
                int newlineIndex = FindPattern(bytes, start, newline, page.ContentOffset, encoding.UnitSize);
                int segmentEnd = newlineIndex >= 0 ? newlineIndex : bytes.Length;
                int displayEnd = segmentEnd;
                if (EndsWith(bytes, start, displayEnd, carriageReturn))
                {
                    displayEnd -= carriageReturn.Length;
                }

                int displayCount = Math.Max(0, displayEnd - start);
                int consumed = newlineIndex >= 0
                    ? newlineIndex + newline.Length - start
                    : bytes.Length - start;
                ViewerLine line = new ViewerLine();
                line.LocalLineNumber = firstLineNumber + result.Count;
                line.ByteOffset = page.ContentOffset + start;
                line.ByteLength = Math.Max(1, consumed);
                line.Text = DisplayFormatter.Decode(bytes, start, displayCount, encoding.Encoding, mode);
                if (mode == ViewerDisplayMode.VisibleCharacters && newlineIndex >= 0)
                {
                    line.Text += carriageReturn.Length > 0 && segmentEnd - displayEnd == carriageReturn.Length
                        ? "  ␍␊"
                        : "  ␊";
                }
                line.IsContinuation = page.TruncatedAtStart && result.Count == 0;
                line.IsTruncated = page.TruncatedAtEnd && newlineIndex < 0;
                result.Add(line);

                if (newlineIndex < 0) break;
                start = newlineIndex + newline.Length;
            }

            if (result.Count == 0 && bytes.Length == 0)
            {
                ViewerLine empty = new ViewerLine();
                empty.LocalLineNumber = firstLineNumber;
                empty.ByteOffset = page.ContentOffset;
                empty.ByteLength = 0;
                empty.Text = string.Empty;
                result.Add(empty);
            }
            return result;
        }

        private static int FindPattern(byte[] bytes, int start, byte[] pattern, long baseOffset, int unitSize)
        {
            int last = bytes.Length - pattern.Length;
            for (int i = Math.Max(0, start); i <= last; i++)
            {
                if (unitSize > 1 && (baseOffset + i) % unitSize != 0) continue;
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static bool EndsWith(byte[] bytes, int start, int end, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || end - start < pattern.Length) return false;
            int patternStart = end - pattern.Length;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (bytes[patternStart + i] != pattern[i]) return false;
            }
            return true;
        }
    }
}
