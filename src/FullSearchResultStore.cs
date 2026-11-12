using System;
using System.IO;
using System.Text;

namespace WordlistViewer
{
    /// <summary>
    /// Stores full-search byte offsets in a fixed-width, disk-backed file.
    /// The store keeps no result collection in memory; only a requested range
    /// is allocated when results are read.
    /// </summary>
    internal sealed class FullSearchResultStore : IDisposable
    {
        private const string ResultExtension = ".wvsr";
        private const int BytesPerResult = sizeof(long);
        private const int StreamBufferSize = 64 * 1024;
        private const long MinimumFreeSpaceBytes = 2L * 1024 * 1024 * 1024;
        private const long FreeSpaceCheckIntervalResults = 1024L * 1024;

        private readonly object syncRoot;
        private readonly string filePath;
        private FileStream stream;
        private BinaryWriter writer;
        private BinaryReader reader;
        private long resultCount;
        private long nextFreeSpaceCheckCount;
        private StoreState state;

        private enum StoreState
        {
            Writing,
            Completed,
            Faulted,
            Disposed
        }

        private FullSearchResultStore(string path, FileStream resultStream)
        {
            syncRoot = new object();
            filePath = path;
            stream = resultStream;
            writer = new BinaryWriter(resultStream, Encoding.UTF8);
            nextFreeSpaceCheckCount = 0;
            state = StoreState.Writing;
        }

        public static void CleanupOrphans()
        {
            try
            {
                string directory = GetStorageDirectory();
                if (!Directory.Exists(directory)) return;
                DeleteOrphanedFiles(directory);
            }
            catch
            {
                // Cleanup is best-effort. Files belonging to another running
                // viewer instance are locked and deliberately left untouched.
            }
        }

        public static FullSearchResultStore Create()
        {
            string directory = GetStorageDirectory();
            Directory.CreateDirectory(directory);
            DeleteOrphanedFiles(directory);

            string path = Path.Combine(
                directory,
                Guid.NewGuid().ToString("N") + ResultExtension);

            FileStream resultStream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                StreamBufferSize,
                FileOptions.SequentialScan | FileOptions.DeleteOnClose);

            try
            {
                return new FullSearchResultStore(path, resultStream);
            }
            catch
            {
                resultStream.Dispose();
                TryDelete(path);
                throw;
            }
        }

        public long Count
        {
            get
            {
                lock (syncRoot)
                {
                    EnsureUsable();
                    return resultCount;
                }
            }
        }

        public long StorageBytes
        {
            get
            {
                lock (syncRoot)
                {
                    EnsureUsable();
                    return checked(resultCount * BytesPerResult);
                }
            }
        }

        public void Append(long offset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", "A file offset cannot be negative.");
            }

            lock (syncRoot)
            {
                EnsureState(StoreState.Writing, "Results can only be appended before completion.");

                try
                {
                    if (resultCount >= nextFreeSpaceCheckCount)
                    {
                        EnsureSafeFreeSpace(filePath);
                        nextFreeSpaceCheckCount = checked(resultCount + FreeSpaceCheckIntervalResults);
                    }
                    writer.Write(offset);
                    resultCount = checked(resultCount + 1L);
                }
                catch
                {
                    state = StoreState.Faulted;
                    throw;
                }
            }
        }

        public void Complete()
        {
            lock (syncRoot)
            {
                EnsureState(StoreState.Writing, "The result store has already been completed.");

                try
                {
                    // BinaryWriter and FileStream both buffer. Flush before changing
                    // the stream from sequential writes to random-access reads.
                    writer.Flush();
                    stream.Flush();
                    stream.Position = 0L;
                    reader = new BinaryReader(stream, Encoding.UTF8);
                    writer = null;
                    state = StoreState.Completed;
                }
                catch
                {
                    state = StoreState.Faulted;
                    throw;
                }
            }
        }

        public long[] ReadRange(long startIndex, int count)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            lock (syncRoot)
            {
                EnsureState(StoreState.Completed, "Complete must be called before results are read.");

                if (startIndex > resultCount)
                {
                    throw new ArgumentOutOfRangeException("startIndex");
                }

                int availableCount = (int)Math.Min((long)count, resultCount - startIndex);
                if (availableCount == 0)
                {
                    return new long[0];
                }

                long[] results = new long[availableCount];
                try
                {
                    stream.Position = checked(startIndex * BytesPerResult);
                    for (int index = 0; index < availableCount; index++)
                    {
                        results[index] = reader.ReadInt64();
                    }
                }
                catch (EndOfStreamException exception)
                {
                    state = StoreState.Faulted;
                    throw new InvalidDataException("The search result store is truncated.", exception);
                }
                catch
                {
                    state = StoreState.Faulted;
                    throw;
                }

                return results;
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (state == StoreState.Disposed)
                {
                    return;
                }

                state = StoreState.Disposed;

                if (reader != null)
                {
                    TryDispose(reader);
                    reader = null;
                    stream = null;
                }
                else if (writer != null)
                {
                    TryDispose(writer);
                    writer = null;
                    stream = null;
                }
                else if (stream != null)
                {
                    TryDispose(stream);
                    stream = null;
                }

                TryDelete(filePath);
            }
        }

        private void EnsureUsable()
        {
            if (state == StoreState.Disposed)
            {
                throw new ObjectDisposedException(typeof(FullSearchResultStore).FullName);
            }
            if (state == StoreState.Faulted)
            {
                throw new InvalidOperationException("The result store is unusable after an I/O failure.");
            }
        }

        private void EnsureState(StoreState requiredState, string message)
        {
            EnsureUsable();
            if (state != requiredState)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static string GetStorageDirectory()
        {
            string localApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData))
            {
                throw new InvalidOperationException("The local application data directory is unavailable.");
            }

            return Path.Combine(localApplicationData, "WordlistViewer", "SearchResults");
        }

        private static void DeleteOrphanedFiles(string directory)
        {
            string canonicalDirectory;
            string[] candidates;
            try
            {
                canonicalDirectory = AddDirectorySeparator(Path.GetFullPath(directory));
                candidates = Directory.GetFiles(
                    directory,
                    "*" + ResultExtension,
                    SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (int index = 0; index < candidates.Length; index++)
            {
                try
                {
                    string candidate = Path.GetFullPath(candidates[index]);
                    if (!candidate.StartsWith(canonicalDirectory, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(Path.GetExtension(candidate), ResultExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    FileInfo information = new FileInfo(candidate);
                    if (information.Exists &&
                        (information.Attributes & FileAttributes.Directory) == 0)
                    {
                        File.Delete(candidate);
                    }
                }
                catch
                {
                    // Cleanup must never prevent a new search. Open result files
                    // are protected by their sharing mode and will be retried later.
                }
            }
        }

        private static void EnsureSafeFreeSpace(string path)
        {
            string root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return;
            DriveInfo drive = new DriveInfo(root);
            long writeAllowance = FreeSpaceCheckIntervalResults * BytesPerResult;
            if (drive.AvailableFreeSpace <= MinimumFreeSpaceBytes + writeAllowance)
            {
                throw new IOException(Localization.T("error.result_disk_space"));
            }
        }

        private static string AddDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static void TryDispose(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
