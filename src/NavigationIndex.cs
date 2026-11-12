using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WordlistViewer
{
    internal sealed class IndexProgress
    {
        public long ProcessedBytes;
        public long TotalBytes;
        public double BytesPerSecond;
        public long LinesCounted;
    }

    internal sealed class NavigationIndexData
    {
        public const int DefaultStride = 1000000;

        public long FileLength;
        public long LastWriteUtcTicks;
        public int CodePage;
        public int UnitSize;
        public int Stride;
        public long TotalLines;
        public long[] Anchors;

        public bool IsValidFor(string path, ResolvedEncoding encoding)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                info.Refresh();
                return info.Exists &&
                    info.Length == FileLength &&
                    info.LastWriteTimeUtc.Ticks == LastWriteUtcTicks &&
                    encoding != null &&
                    encoding.Encoding.CodePage == CodePage &&
                    encoding.UnitSize == UnitSize &&
                    Anchors != null && Anchors.Length > 0 && Stride > 0;
            }
            catch
            {
                return false;
            }
        }

        public long ApproximateLineForOffset(long offset)
        {
            int anchorIndex = FindAnchorAtOrBefore(offset);
            return 1L + (long)anchorIndex * Stride;
        }

        public long AnchorOffsetForLine(long lineNumber, out long anchorLine)
        {
            if (lineNumber < 1) lineNumber = 1;
            int index = (int)Math.Min(Anchors.Length - 1L, (lineNumber - 1L) / Stride);
            anchorLine = 1L + (long)index * Stride;
            return Anchors[index];
        }

        public long AnchorOffsetForByte(long offset, out long anchorLine)
        {
            int index = FindAnchorAtOrBefore(offset);
            anchorLine = 1L + (long)index * Stride;
            return Anchors[index];
        }

        private int FindAnchorAtOrBefore(long offset)
        {
            int low = 0;
            int high = Anchors.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (Anchors[middle] <= offset)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return Math.Max(0, high);
        }
    }

    internal static class NavigationIndexStore
    {
        private const string Magic = "WLVINDEX2";
        private const int Version = 2;
        private const int BlockSize = 8 * 1024 * 1024;

        public static NavigationIndexData TryLoad(string path, ResolvedEncoding encoding)
        {
            string indexPath = GetIndexPath(path);
            if (!File.Exists(indexPath))
            {
                return null;
            }

            try
            {
                using (FileStream stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadString() != Magic || reader.ReadInt32() != Version)
                    {
                        return null;
                    }

                    NavigationIndexData data = new NavigationIndexData();
                    data.FileLength = reader.ReadInt64();
                    data.LastWriteUtcTicks = reader.ReadInt64();
                    data.CodePage = reader.ReadInt32();
                    data.UnitSize = reader.ReadInt32();
                    data.Stride = reader.ReadInt32();
                    data.TotalLines = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    if (count <= 0 || count > 10000000)
                    {
                        return null;
                    }

                    data.Anchors = new long[count];
                    long previous = -1;
                    for (int i = 0; i < count; i++)
                    {
                        long value = reader.ReadInt64();
                        if (value < 0 || value < previous || value > data.FileLength)
                        {
                            return null;
                        }
                        data.Anchors[i] = value;
                        previous = value;
                    }

                    return data.IsValidFor(path, encoding) ? data : null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static NavigationIndexData Build(
            string path,
            ResolvedEncoding encoding,
            CancellationToken cancellationToken,
            IProgress<IndexProgress> progress)
        {
            FileInfo before = new FileInfo(path);
            before.Refresh();
            long length = before.Length;
            long lastWriteTicks = before.LastWriteTimeUtc.Ticks;
            int stride = NavigationIndexData.DefaultStride;
            List<long> anchors = new List<long>();
            anchors.Add(encoding.BomLength);

            long lineNumber = 1;
            long nextAnchorLine = 1L + stride;
            byte[] pattern = encoding.NewLineBytes;
            byte[] buffer = new byte[BlockSize + Math.Max(0, pattern.Length - 1)];
            int carry = 0;
            long readPosition = encoding.BomLength;
            long lastReportPosition = readPosition;
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BlockSize,
                FileOptions.SequentialScan))
            {
                stream.Position = readPosition;
                while (readPosition < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min((long)BlockSize, length - readPosition);
                    int read = ReadInto(stream, buffer, carry, requested, cancellationToken);
                    if (read <= 0) break;

                    int total = carry + read;
                    long bufferOffset = readPosition - carry;
                    int last = total - pattern.Length;
                    for (int i = 0; i <= last; i++)
                    {
                        long absolute = bufferOffset + i;
                        if (encoding.UnitSize > 1 && absolute % encoding.UnitSize != 0)
                        {
                            continue;
                        }
                        if (!Matches(buffer, i, pattern))
                        {
                            continue;
                        }

                        lineNumber++;
                        if (lineNumber == nextAnchorLine)
                        {
                            anchors.Add(absolute + pattern.Length);
                            nextAnchorLine += stride;
                        }
                        i += pattern.Length - 1;
                    }

                    readPosition += read;
                    carry = Math.Min(pattern.Length - 1, total);
                    if (carry > 0)
                    {
                        Buffer.BlockCopy(buffer, total - carry, buffer, 0, carry);
                    }

                    if (readPosition - lastReportPosition >= 64L * 1024 * 1024)
                    {
                        lastReportPosition = readPosition;
                        ReportProgress(progress, readPosition, length, lineNumber, stopwatch);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            FileInfo after = new FileInfo(path);
            after.Refresh();
            if (after.Length != length || after.LastWriteTimeUtc.Ticks != lastWriteTicks)
            {
                throw new IOException(Localization.T("error.index_source_changed"));
            }

            NavigationIndexData result = new NavigationIndexData();
            result.FileLength = length;
            result.LastWriteUtcTicks = lastWriteTicks;
            result.CodePage = encoding.Encoding.CodePage;
            result.UnitSize = encoding.UnitSize;
            result.Stride = stride;
            result.TotalLines = lineNumber;
            result.Anchors = anchors.ToArray();
            Save(path, result);
            ReportProgress(progress, length, length, lineNumber, stopwatch);
            return result;
        }

        public static long FindLineNumberAtOffset(
            string path,
            long targetOffset,
            ResolvedEncoding encoding,
            NavigationIndexData index,
            CancellationToken cancellationToken)
        {
            long anchorLine;
            long anchorOffset = index.AnchorOffsetForByte(targetOffset, out anchorLine);
            long ignored;
            long newLines = CountNewLines(path, anchorOffset, targetOffset, encoding, cancellationToken, -1, out ignored);
            return anchorLine + newLines;
        }

        public static long FindOffsetForLine(
            string path,
            long targetLine,
            ResolvedEncoding encoding,
            NavigationIndexData index,
            CancellationToken cancellationToken)
        {
            if (targetLine <= 1) return encoding.BomLength;
            if (targetLine > index.TotalLines) targetLine = index.TotalLines;
            long anchorLine;
            long anchorOffset = index.AnchorOffsetForLine(targetLine, out anchorLine);
            long linesNeeded = targetLine - anchorLine;
            if (linesNeeded <= 0) return anchorOffset;
            long foundOffset;
            CountNewLines(path, anchorOffset, index.FileLength, encoding, cancellationToken, linesNeeded, out foundOffset);
            return foundOffset;
        }

        private static long CountNewLines(
            string path,
            long startOffset,
            long endOffset,
            ResolvedEncoding encoding,
            CancellationToken cancellationToken,
            long stopAfter,
            out long offsetAfterLast)
        {
            byte[] pattern = encoding.NewLineBytes;
            byte[] buffer = new byte[1024 * 1024 + Math.Max(0, pattern.Length - 1)];
            int carry = 0;
            long position = startOffset;
            long count = 0;
            offsetAfterLast = startOffset;

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.SequentialScan))
            {
                stream.Position = startOffset;
                while (position < endOffset)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min(1024L * 1024, endOffset - position);
                    int read = ReadInto(stream, buffer, carry, requested, cancellationToken);
                    if (read <= 0) break;

                    int total = carry + read;
                    long baseOffset = position - carry;
                    int last = total - pattern.Length;
                    for (int i = 0; i <= last; i++)
                    {
                        long absolute = baseOffset + i;
                        if (absolute >= endOffset) break;
                        if (encoding.UnitSize > 1 && absolute % encoding.UnitSize != 0) continue;
                        if (!Matches(buffer, i, pattern)) continue;
                        count++;
                        offsetAfterLast = absolute + pattern.Length;
                        if (stopAfter > 0 && count >= stopAfter)
                        {
                            return count;
                        }
                        i += pattern.Length - 1;
                    }

                    position += read;
                    carry = Math.Min(pattern.Length - 1, total);
                    if (carry > 0)
                    {
                        Buffer.BlockCopy(buffer, total - carry, buffer, 0, carry);
                    }
                }
            }
            return count;
        }

        private static int ReadInto(FileStream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int total = 0;
            while (total < count)
            {
                token.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        private static bool Matches(byte[] data, int offset, byte[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        private static void ReportProgress(
            IProgress<IndexProgress> progress,
            long position,
            long total,
            long lines,
            Stopwatch stopwatch)
        {
            if (progress == null) return;
            IndexProgress value = new IndexProgress();
            value.ProcessedBytes = position;
            value.TotalBytes = total;
            value.LinesCounted = lines;
            value.BytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0.05
                ? position / stopwatch.Elapsed.TotalSeconds
                : 0;
            progress.Report(value);
        }

        private static void Save(string sourcePath, NavigationIndexData data)
        {
            string target = GetIndexPath(sourcePath);
            string directory = Path.GetDirectoryName(target);
            Directory.CreateDirectory(directory);
            string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write(data.FileLength);
                    writer.Write(data.LastWriteUtcTicks);
                    writer.Write(data.CodePage);
                    writer.Write(data.UnitSize);
                    writer.Write(data.Stride);
                    writer.Write(data.TotalLines);
                    writer.Write(data.Anchors.Length);
                    for (int i = 0; i < data.Anchors.Length; i++) writer.Write(data.Anchors[i]);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(target))
                {
                    try
                    {
                        File.Replace(temporary, target, null, true);
                    }
                    catch
                    {
                        File.Delete(target);
                        File.Move(temporary, target);
                    }
                }
                else
                {
                    File.Move(temporary, target);
                }
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        private static string GetIndexPath(string sourcePath)
        {
            string normalized = Path.GetFullPath(sourcePath).ToUpperInvariant();
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            }
            StringBuilder name = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) name.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WordlistViewer", "Indexes");
            return Path.Combine(root, name.ToString() + ".wli");
        }
    }
}
