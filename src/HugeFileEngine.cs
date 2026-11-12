using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WordlistViewer
{
    internal enum EncodingChoice
    {
        Automatic,
        Utf8,
        Windows1254,
        Windows1252,
        Latin1,
        Utf16LittleEndian,
        Utf16BigEndian
    }

    internal sealed class ResolvedEncoding
    {
        public Encoding Encoding;
        public EncodingChoice Choice;
        public string DisplayName;
        public byte[] NewLineBytes;
        public int UnitSize;
        public int BomLength;
    }

    internal sealed class PageData
    {
        public long FileLength;
        public long StartOffset;
        public long ContentOffset;
        public long EndOffset;
        public byte[] ContentBytes;
        public string Text;
        public bool TruncatedAtStart;
        public bool TruncatedAtEnd;
        public int ReplacedControlCharacters;
    }

    internal sealed class SearchProgress
    {
        public long ScannedBytes;
        public long TotalBytes;
        public long CurrentOffset;
        public double BytesPerSecond;
        public double ElapsedSeconds;
        public double EstimatedRemainingSeconds;
        public double ReadSeconds;
        public double CompareSeconds;
        public string Bottleneck;
        public long MatchCount;
    }

    internal static class EncodingResolver
    {
        private const int SampleSize = 256 * 1024;

        public static ResolvedEncoding Resolve(string path, EncodingChoice requested)
        {
            byte[] sample = ReadSample(path);
            EncodingChoice actual = requested == EncodingChoice.Automatic
                ? DetectChoice(sample)
                : requested;

            Encoding encoding;
            string name;
            int unitSize = 1;

            switch (actual)
            {
                case EncodingChoice.Utf16LittleEndian:
                    encoding = new UnicodeEncoding(false, false, false);
                    name = "UTF-16 LE";
                    unitSize = 2;
                    break;
                case EncodingChoice.Utf16BigEndian:
                    encoding = new UnicodeEncoding(true, false, false);
                    name = "UTF-16 BE";
                    unitSize = 2;
                    break;
                case EncodingChoice.Windows1254:
                    encoding = Encoding.GetEncoding(1254, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                name = "Windows-1254 (Turkish)";
                    break;
                case EncodingChoice.Windows1252:
                    encoding = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                    name = "Windows-1252";
                    break;
                case EncodingChoice.Latin1:
                    encoding = Encoding.GetEncoding(28591, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                    name = "ISO-8859-1";
                    break;
                default:
                    encoding = new UTF8Encoding(false, false);
                    name = "UTF-8";
                    break;
            }

            int bomLength = GetMatchingPreambleLength(sample, encoding);
            ResolvedEncoding result = new ResolvedEncoding();
            result.Encoding = encoding;
            result.Choice = actual;
            result.DisplayName = name;
            result.NewLineBytes = encoding.GetBytes("\n");
            result.UnitSize = unitSize;
            result.BomLength = bomLength;
            return result;
        }

        private static byte[] ReadSample(string path)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.SequentialScan))
            {
                int wanted = (int)Math.Min((long)SampleSize, stream.Length);
                byte[] buffer = new byte[wanted];
                int total = 0;
                while (total < wanted)
                {
                    int read = stream.Read(buffer, total, wanted - total);
                    if (read <= 0)
                    {
                        break;
                    }
                    total += read;
                }

                if (total == buffer.Length)
                {
                    return buffer;
                }

                byte[] resized = new byte[total];
                Buffer.BlockCopy(buffer, 0, resized, 0, total);
                return resized;
            }
        }

        private static EncodingChoice DetectChoice(byte[] sample)
        {
            if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            {
                return EncodingChoice.Utf8;
            }
            if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
            {
                return EncodingChoice.Utf16LittleEndian;
            }
            if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
            {
                return EncodingChoice.Utf16BigEndian;
            }

            if (LooksLikeUtf16(sample, false))
            {
                return EncodingChoice.Utf16LittleEndian;
            }
            if (LooksLikeUtf16(sample, true))
            {
                return EncodingChoice.Utf16BigEndian;
            }
            if (IsValidUtf8(sample))
            {
                return EncodingChoice.Utf8;
            }
            return EncodingChoice.Windows1254;
        }

        private static bool LooksLikeUtf16(byte[] sample, bool bigEndian)
        {
            int pairs = Math.Min(sample.Length / 2, 4096);
            if (pairs < 8)
            {
                return false;
            }

            int expectedZeroes = 0;
            int otherZeroes = 0;
            for (int i = 0; i < pairs; i++)
            {
                byte first = sample[i * 2];
                byte second = sample[i * 2 + 1];
                if (bigEndian)
                {
                    if (first == 0) expectedZeroes++;
                    if (second == 0) otherZeroes++;
                }
                else
                {
                    if (second == 0) expectedZeroes++;
                    if (first == 0) otherZeroes++;
                }
            }

            return expectedZeroes > pairs / 3 && expectedZeroes > otherZeroes * 4;
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte current = bytes[i];
                if (current <= 0x7F)
                {
                    i++;
                    continue;
                }

                int continuationCount;
                int minimumSecond = 0x80;
                int maximumSecond = 0xBF;
                if (current >= 0xC2 && current <= 0xDF)
                {
                    continuationCount = 1;
                }
                else if (current >= 0xE0 && current <= 0xEF)
                {
                    continuationCount = 2;
                    if (current == 0xE0) minimumSecond = 0xA0;
                    if (current == 0xED) maximumSecond = 0x9F;
                }
                else if (current >= 0xF0 && current <= 0xF4)
                {
                    continuationCount = 3;
                    if (current == 0xF0) minimumSecond = 0x90;
                    if (current == 0xF4) maximumSecond = 0x8F;
                }
                else
                {
                    return false;
                }

                if (i + continuationCount >= bytes.Length)
                {
                    // The sample may end in the middle of an otherwise valid sequence.
                    return true;
                }

                byte secondByte = bytes[i + 1];
                if (secondByte < minimumSecond || secondByte > maximumSecond)
                {
                    return false;
                }
                for (int j = 2; j <= continuationCount; j++)
                {
                    if (bytes[i + j] < 0x80 || bytes[i + j] > 0xBF)
                    {
                        return false;
                    }
                }
                i += continuationCount + 1;
            }
            return true;
        }

        private static int GetMatchingPreambleLength(byte[] sample, Encoding encoding)
        {
            byte[] preamble = encoding.GetPreamble();
            if (preamble == null || preamble.Length == 0 || sample.Length < preamble.Length)
            {
                return 0;
            }
            for (int i = 0; i < preamble.Length; i++)
            {
                if (sample[i] != preamble[i])
                {
                    return 0;
                }
            }
            return preamble.Length;
        }
    }

    internal static class HugeFileReader
    {
        public const int PageBytes = 256 * 1024;
        private const int TailScanBytes = 64 * 1024;
        private const int BackwardLineScanBytes = 1024 * 1024;

        public static PageData ReadPage(
            string path,
            long requestedOffset,
            bool alignToLine,
            ResolvedEncoding encoding,
            CancellationToken cancellationToken)
        {
            FileInfo information = new FileInfo(path);
            information.Refresh();
            long fileLength = information.Length;

            PageData empty = new PageData();
            empty.FileLength = fileLength;
            empty.StartOffset = 0;
            empty.ContentOffset = 0;
            empty.EndOffset = 0;
            empty.ContentBytes = new byte[0];
            empty.Text = string.Empty;

            if (fileLength == 0)
            {
                return empty;
            }

            long target = Math.Max(0, Math.Min(requestedOffset, fileLength));
            if (target >= fileLength)
            {
                target = Math.Max(0, fileLength - PageBytes);
            }
            target = AlignOffset(target, encoding.UnitSize);

            bool truncatedAtStart = false;
            long pageStart;
            if (alignToLine)
            {
                pageStart = FindLineStart(path, target, fileLength, encoding, cancellationToken, out truncatedAtStart);
            }
            else
            {
                pageStart = target;
                truncatedAtStart = pageStart > encoding.BomLength;
            }

            cancellationToken.ThrowIfCancellationRequested();
            int wanted = (int)Math.Min((long)PageBytes + TailScanBytes, fileLength - pageStart);
            byte[] raw = ReadRange(path, pageStart, wanted, FileOptions.RandomAccess, cancellationToken);
            bool truncatedAtEnd;
            int displayCount = DetermineDisplayCount(raw, pageStart, fileLength, encoding, out truncatedAtEnd);

            int bomSkip = pageStart == 0 ? Math.Min(encoding.BomLength, displayCount) : 0;
            int contentCount = Math.Max(0, displayCount - bomSkip);
            byte[] content = new byte[contentCount];
            if (contentCount > 0)
            {
                Buffer.BlockCopy(raw, bomSkip, content, 0, contentCount);
            }

            PageData result = new PageData();
            result.FileLength = fileLength;
            result.StartOffset = pageStart;
            result.ContentOffset = pageStart + bomSkip;
            result.EndOffset = pageStart + displayCount;
            result.ContentBytes = content;
            int replacedControlCharacters;
            string decoded = encoding.Encoding.GetString(content);
            result.Text = SanitizeForDisplay(decoded, out replacedControlCharacters);
            result.ReplacedControlCharacters = replacedControlCharacters;
            result.TruncatedAtStart = truncatedAtStart;
            result.TruncatedAtEnd = truncatedAtEnd;
            return result;
        }

        public static PageData ReadLineSegment(
            string path,
            long requestedOffset,
            ResolvedEncoding encoding,
            CancellationToken cancellationToken)
        {
            FileInfo information = new FileInfo(path);
            information.Refresh();
            long fileLength = information.Length;
            long target = Math.Max(0, Math.Min(requestedOffset, fileLength));
            target = AlignOffset(target, encoding.UnitSize);
            int wanted = (int)Math.Min((long)PageBytes, fileLength - target);
            byte[] raw = ReadRange(path, target, wanted, FileOptions.RandomAccess, cancellationToken);
            int newline = FindPattern(raw, 0, raw.Length, encoding.NewLineBytes, target, encoding.UnitSize);
            int displayCount = newline >= 0 ? newline + encoding.NewLineBytes.Length : raw.Length;
            int bomSkip = target == 0 ? Math.Min(encoding.BomLength, displayCount) : 0;
            byte[] content = new byte[Math.Max(0, displayCount - bomSkip)];
            if (content.Length > 0) Buffer.BlockCopy(raw, bomSkip, content, 0, content.Length);

            PageData result = new PageData();
            result.FileLength = fileLength;
            result.StartOffset = target;
            result.ContentOffset = target + bomSkip;
            result.EndOffset = target + displayCount;
            result.ContentBytes = content;
            result.Text = encoding.Encoding.GetString(content);
            bool startsMidLine = target > encoding.BomLength;
            if (startsMidLine && target >= encoding.NewLineBytes.Length)
            {
                byte[] preceding = ReadRange(
                    path,
                    target - encoding.NewLineBytes.Length,
                    encoding.NewLineBytes.Length,
                    FileOptions.RandomAccess,
                    cancellationToken);
                if (preceding.Length == encoding.NewLineBytes.Length)
                {
                    startsMidLine = false;
                    for (int i = 0; i < preceding.Length; i++)
                    {
                        if (preceding[i] != encoding.NewLineBytes[i])
                        {
                            startsMidLine = true;
                            break;
                        }
                    }
                }
            }
            result.TruncatedAtStart = startsMidLine;
            result.TruncatedAtEnd = newline < 0 && result.EndOffset < fileLength;
            return result;
        }

        private static string SanitizeForDisplay(string value, out int replaced)
        {
            replaced = 0;
            char[] characters = null;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                bool replace = (current < 32 && current != '\r' && current != '\n' && current != '\t') || current == 127;
                if (!replace)
                {
                    continue;
                }

                if (characters == null)
                {
                    characters = value.ToCharArray();
                }
                characters[i] = current == '\0' ? '\u2400' : '\u00B7';
                replaced++;
            }

            return characters == null ? value : new string(characters);
        }

        private static int DetermineDisplayCount(
            byte[] raw,
            long pageStart,
            long fileLength,
            ResolvedEncoding encoding,
            out bool truncated)
        {
            truncated = false;
            if (raw.Length <= PageBytes)
            {
                return AlignCount(raw.Length, encoding.UnitSize);
            }

            int scanStart = Math.Min(PageBytes, raw.Length);
            scanStart = AlignCount(scanStart, encoding.UnitSize);
            int newline = FindPattern(raw, scanStart, raw.Length, encoding.NewLineBytes, pageStart, encoding.UnitSize);
            if (newline >= 0)
            {
                return newline + encoding.NewLineBytes.Length;
            }

            int count = AlignCount(Math.Min(PageBytes, raw.Length), encoding.UnitSize);
            truncated = pageStart + count < fileLength;
            return count;
        }

        private static long FindLineStart(
            string path,
            long requestedOffset,
            long fileLength,
            ResolvedEncoding encoding,
            CancellationToken cancellationToken,
            out bool truncated)
        {
            truncated = false;
            if (requestedOffset <= encoding.BomLength)
            {
                return 0;
            }

            long searchStart = Math.Max((long)encoding.BomLength, requestedOffset - BackwardLineScanBytes);
            searchStart = AlignOffset(searchStart, encoding.UnitSize);
            int count = (int)(requestedOffset - searchStart);
            byte[] bytes = ReadRange(path, searchStart, count, FileOptions.RandomAccess, cancellationToken);
            int newline = FindLastPattern(bytes, encoding.NewLineBytes, searchStart, encoding.UnitSize);
            if (newline >= 0)
            {
                return searchStart + newline + encoding.NewLineBytes.Length;
            }

            if (searchStart <= encoding.BomLength)
            {
                return 0;
            }

            truncated = true;
            return AlignOffset(Math.Min(requestedOffset, fileLength), encoding.UnitSize);
        }

        private static byte[] ReadRange(
            string path,
            long offset,
            int count,
            FileOptions options,
            CancellationToken cancellationToken)
        {
            if (count <= 0)
            {
                return new byte[0];
            }

            byte[] buffer = new byte[count];
            int total = 0;
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                128 * 1024,
                options))
            {
                stream.Position = offset;
                while (total < count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, total, count - total);
                    if (read <= 0)
                    {
                        break;
                    }
                    total += read;
                }
            }

            if (total == buffer.Length)
            {
                return buffer;
            }
            byte[] resized = new byte[total];
            Buffer.BlockCopy(buffer, 0, resized, 0, total);
            return resized;
        }

        private static int FindPattern(byte[] bytes, int start, int end, byte[] pattern, long baseOffset, int unitSize)
        {
            int last = end - pattern.Length;
            for (int i = Math.Max(0, start); i <= last; i++)
            {
                if (unitSize > 1 && ((baseOffset + i) % unitSize) != 0)
                {
                    continue;
                }
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindLastPattern(byte[] bytes, byte[] pattern, long baseOffset, int unitSize)
        {
            for (int i = bytes.Length - pattern.Length; i >= 0; i--)
            {
                if (unitSize > 1 && ((baseOffset + i) % unitSize) != 0)
                {
                    continue;
                }
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }

        private static long AlignOffset(long value, int unitSize)
        {
            return unitSize <= 1 ? value : value - value % unitSize;
        }

        private static int AlignCount(int value, int unitSize)
        {
            return unitSize <= 1 ? value : value - value % unitSize;
        }
    }

    internal static class ByteSearch
    {
        private const int BlockSize = 4 * 1024 * 1024;
        private const long ProgressIntervalBytes = 32L * 1024 * 1024;

        public static long FindForward(
            string path,
            byte[] pattern,
            long startOffset,
            CancellationToken cancellationToken,
            IProgress<SearchProgress> progress)
        {
            return FindForward(path, pattern, startOffset, cancellationToken, 1, progress);
        }

        public static long FindForward(
            string path,
            byte[] pattern,
            long startOffset,
            CancellationToken cancellationToken,
            int alignment,
            IProgress<SearchProgress> progress)
        {
            if (pattern == null || pattern.Length == 0)
            {
                return -1;
            }

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BlockSize,
                FileOptions.SequentialScan))
            {
                long length = stream.Length;
                long start = Math.Max(0, Math.Min(startOffset, length));
                stream.Position = start;
                byte[] buffer = new byte[BlockSize + Math.Max(0, pattern.Length - 1)];
                int carry = 0;
                long readPosition = start;
                long lastReported = 0;
                SearchTiming timing = new SearchTiming();
                int[] skipTable = BuildSkipTable(pattern);

                while (readPosition < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min((long)BlockSize, length - readPosition);
                    long phaseStart = Stopwatch.GetTimestamp();
                    int read = ReadInto(stream, buffer, carry, requested, cancellationToken);
                    timing.ReadTicks += Stopwatch.GetTimestamp() - phaseStart;
                    if (read <= 0)
                    {
                        break;
                    }

                    int total = carry + read;
                    long bufferOffset = readPosition - carry;
                    phaseStart = Stopwatch.GetTimestamp();
                    int found = IndexOfAligned(buffer, total, pattern, 0, skipTable, bufferOffset, alignment);
                    timing.CompareTicks += Stopwatch.GetTimestamp() - phaseStart;
                    if (found >= 0)
                    {
                        Report(progress, bufferOffset + found - start, length - start, bufferOffset + found, timing);
                        return bufferOffset + found;
                    }

                    readPosition += read;
                    int overlap = Math.Min(pattern.Length - 1, total);
                    if (overlap > 0)
                    {
                        Buffer.BlockCopy(buffer, total - overlap, buffer, 0, overlap);
                    }
                    carry = overlap;

                    long scanned = readPosition - start;
                    if (scanned - lastReported >= ProgressIntervalBytes)
                    {
                        lastReported = scanned;
                        Report(progress, scanned, length - start, readPosition, timing);
                    }
                }

                Report(progress, length - start, length - start, length, timing);
                return -1;
            }
        }

        public static long ScanAll(
            string path,
            byte[] pattern,
            long startOffset,
            CancellationToken cancellationToken,
            int alignment,
            IProgress<SearchProgress> progress,
            Action<long> matchSink)
        {
            if (pattern == null || pattern.Length == 0)
            {
                return 0;
            }
            if (matchSink == null)
            {
                throw new ArgumentNullException("matchSink");
            }

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BlockSize,
                FileOptions.SequentialScan))
            {
                long length = stream.Length;
                long start = Math.Max(0, Math.Min(startOffset, length));
                stream.Position = start;
                byte[] buffer = new byte[BlockSize + Math.Max(0, pattern.Length - 1)];
                int carry = 0;
                long readPosition = start;
                long lastReported = 0;
                long matchCount = 0;
                long nextAllowedAbsoluteOffset = start;
                SearchTiming timing = new SearchTiming();
                int[] skipTable = BuildSkipTable(pattern);

                while (readPosition < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min((long)BlockSize, length - readPosition);
                    long phaseStart = Stopwatch.GetTimestamp();
                    int read = ReadInto(stream, buffer, carry, requested, cancellationToken);
                    timing.ReadTicks += Stopwatch.GetTimestamp() - phaseStart;
                    if (read <= 0)
                    {
                        break;
                    }

                    int total = carry + read;
                    long bufferOffset = readPosition - carry;
                    int searchFrom = 0;
                    int candidatesSinceCancellationCheck = 0;
                    phaseStart = Stopwatch.GetTimestamp();
                    while (searchFrom <= total - pattern.Length)
                    {
                        int found = IndexOfAligned(buffer, total, pattern, searchFrom, skipTable, bufferOffset, alignment);
                        if (found < 0)
                        {
                            break;
                        }

                        long absoluteOffset = bufferOffset + found;
                        if (absoluteOffset >= nextAllowedAbsoluteOffset)
                        {
                            matchSink(absoluteOffset);
                            matchCount++;
                            nextAllowedAbsoluteOffset = absoluteOffset + Math.Max(1, pattern.Length);
                            searchFrom = found + Math.Max(1, pattern.Length);
                        }
                        else
                        {
                            searchFrom = found + 1;
                        }

                        candidatesSinceCancellationCheck++;
                        if ((candidatesSinceCancellationCheck & 4095) == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    timing.CompareTicks += Stopwatch.GetTimestamp() - phaseStart;

                    readPosition += read;
                    int overlap = Math.Min(pattern.Length - 1, total);
                    if (overlap > 0)
                    {
                        Buffer.BlockCopy(buffer, total - overlap, buffer, 0, overlap);
                    }
                    carry = overlap;

                    long scanned = readPosition - start;
                    if (scanned - lastReported >= ProgressIntervalBytes)
                    {
                        lastReported = scanned;
                        Report(progress, scanned, length - start, readPosition, timing, matchCount);
                    }
                }

                Report(progress, length - start, length - start, length, timing, matchCount);
                return matchCount;
            }
        }

        public static long FindBackward(
            string path,
            byte[] pattern,
            long startExclusive,
            CancellationToken cancellationToken,
            IProgress<SearchProgress> progress)
        {
            return FindBackward(path, pattern, startExclusive, cancellationToken, 1, progress);
        }

        public static long FindBackward(
            string path,
            byte[] pattern,
            long startExclusive,
            CancellationToken cancellationToken,
            int alignment,
            IProgress<SearchProgress> progress)
        {
            if (pattern == null || pattern.Length == 0)
            {
                return -1;
            }

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BlockSize,
                FileOptions.RandomAccess))
            {
                long length = stream.Length;
                long originalUpper = Math.Max(0, Math.Min(startExclusive, length));
                long upper = originalUpper;
                long lastReported = 0;
                SearchTiming timing = new SearchTiming();
                int[] skipTable = BuildSkipTable(pattern);

                while (upper > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long lower = Math.Max(0, upper - BlockSize);
                    long readEnd = Math.Min(length, upper + pattern.Length - 1L);
                    int count = (int)(readEnd - lower);
                    byte[] buffer = new byte[count];
                    stream.Position = lower;
                    long phaseStart = Stopwatch.GetTimestamp();
                    int actual = ReadInto(stream, buffer, 0, count, cancellationToken);
                    timing.ReadTicks += Stopwatch.GetTimestamp() - phaseStart;
                    int candidateLimit = (int)(upper - lower);
                    phaseStart = Stopwatch.GetTimestamp();
                    int found = LastIndexBeforeAligned(buffer, actual, pattern, candidateLimit, skipTable, lower, alignment);
                    timing.CompareTicks += Stopwatch.GetTimestamp() - phaseStart;
                    if (found >= 0)
                    {
                        Report(progress, originalUpper - (lower + found), originalUpper, lower + found, timing);
                        return lower + found;
                    }

                    upper = lower;
                    long scanned = originalUpper - upper;
                    if (scanned - lastReported >= ProgressIntervalBytes)
                    {
                        lastReported = scanned;
                        Report(progress, scanned, originalUpper, upper, timing);
                    }
                }

                Report(progress, originalUpper, originalUpper, 0, timing);
                return -1;
            }
        }

        private static int ReadInto(
            FileStream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private static int[] BuildSkipTable(byte[] pattern)
        {
            int[] table = new int[256];
            int defaultSkip = Math.Max(1, pattern.Length);
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = defaultSkip;
            }
            for (int i = 0; i < pattern.Length - 1; i++)
            {
                table[pattern[i]] = pattern.Length - 1 - i;
            }
            return table;
        }

        private static int IndexOf(byte[] data, int dataLength, byte[] pattern, int start, int[] skipTable)
        {
            int patternLength = pattern.Length;
            if (patternLength == 1)
            {
                for (int i = start; i < dataLength; i++)
                {
                    if (data[i] == pattern[0]) return i;
                }
                return -1;
            }

            int index = start;
            int lastStart = dataLength - patternLength;
            while (index <= lastStart)
            {
                int patternIndex = patternLength - 1;
                while (patternIndex >= 0 && data[index + patternIndex] == pattern[patternIndex])
                {
                    patternIndex--;
                }
                if (patternIndex < 0)
                {
                    return index;
                }
                index += skipTable[data[index + patternLength - 1]];
            }
            return -1;
        }

        private static int LastIndexBefore(
            byte[] data,
            int dataLength,
            byte[] pattern,
            int candidateLimit,
            int[] skipTable)
        {
            int last = -1;
            int next = 0;
            while (next < candidateLimit)
            {
                int found = IndexOf(data, dataLength, pattern, next, skipTable);
                if (found < 0 || found >= candidateLimit)
                {
                    break;
                }
                last = found;
                next = found + 1;
            }
            return last;
        }

        private static int IndexOfAligned(
            byte[] data,
            int dataLength,
            byte[] pattern,
            int start,
            int[] skipTable,
            long baseOffset,
            int alignment)
        {
            int next = start;
            int safeAlignment = Math.Max(1, alignment);
            while (next <= dataLength - pattern.Length)
            {
                int found = IndexOf(data, dataLength, pattern, next, skipTable);
                if (found < 0) return -1;
                if ((baseOffset + found) % safeAlignment == 0) return found;
                next = found + 1;
            }
            return -1;
        }

        private static int LastIndexBeforeAligned(
            byte[] data,
            int dataLength,
            byte[] pattern,
            int candidateLimit,
            int[] skipTable,
            long baseOffset,
            int alignment)
        {
            int last = -1;
            int next = 0;
            int safeAlignment = Math.Max(1, alignment);
            while (next < candidateLimit)
            {
                int found = IndexOf(data, dataLength, pattern, next, skipTable);
                if (found < 0 || found >= candidateLimit) break;
                if ((baseOffset + found) % safeAlignment == 0) last = found;
                next = found + 1;
            }
            return last;
        }

        private static void Report(
            IProgress<SearchProgress> progress,
            long scanned,
            long total,
            long currentOffset,
            SearchTiming timing)
        {
            Report(progress, scanned, total, currentOffset, timing, 0);
        }

        private static void Report(
            IProgress<SearchProgress> progress,
            long scanned,
            long total,
            long currentOffset,
            SearchTiming timing,
            long matchCount)
        {
            if (progress == null)
            {
                return;
            }
            SearchProgress value = new SearchProgress();
            value.ScannedBytes = Math.Max(0, scanned);
            value.TotalBytes = Math.Max(0, total);
            value.CurrentOffset = Math.Max(0, currentOffset);
            value.MatchCount = Math.Max(0, matchCount);
            value.ElapsedSeconds = timing.Total.Elapsed.TotalSeconds;
            value.BytesPerSecond = value.ElapsedSeconds > 0.05
                ? value.ScannedBytes / value.ElapsedSeconds
                : 0;
            value.EstimatedRemainingSeconds = value.BytesPerSecond > 0
                ? Math.Max(0, value.TotalBytes - value.ScannedBytes) / value.BytesPerSecond
                : 0;
            value.ReadSeconds = timing.ReadTicks / (double)Stopwatch.Frequency;
            value.CompareSeconds = timing.CompareTicks / (double)Stopwatch.Frequency;
            if (value.ReadSeconds > value.CompareSeconds * 1.35)
            {
                value.Bottleneck = "Disk / I/O";
            }
            else if (value.CompareSeconds > value.ReadSeconds * 1.35)
            {
                value.Bottleneck = "CPU";
            }
            else
            {
                value.Bottleneck = "Mixed";
            }
            progress.Report(value);
        }

        private sealed class SearchTiming
        {
            public readonly Stopwatch Total = Stopwatch.StartNew();
            public long ReadTicks;
            public long CompareTicks;
        }
    }
}
