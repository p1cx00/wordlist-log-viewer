using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WordlistViewer
{
    internal enum ViewerDisplayMode
    {
        Normal,
        VisibleCharacters,
        Hex
    }

    internal sealed class ViewerBookmark
    {
        public long Offset { get; set; }
        public string Label { get; set; }
        public string Preview { get; set; }
        public DateTime CreatedUtc { get; set; }

        public ViewerBookmark()
        {
            Label = string.Empty;
            Preview = string.Empty;
            CreatedUtc = DateTime.UtcNow;
        }

        public ViewerBookmark Clone()
        {
            ViewerBookmark result = new ViewerBookmark();
            result.Offset = Offset;
            result.Label = Label ?? string.Empty;
            result.Preview = Preview ?? string.Empty;
            result.CreatedUtc = CreatedUtc;
            return result;
        }
    }

    internal sealed class ViewerFileSession
    {
        private const int MaximumBookmarks = 2000;
        private readonly List<ViewerBookmark> bookmarks;

        public string FilePath { get; set; }
        public long FileLength { get; set; }
        public long FileLastWriteUtcTicks { get; set; }
        public long LastOffset { get; set; }
        public EncodingChoice SelectedEncoding { get; set; }
        public ViewerDisplayMode DisplayMode { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public IList<ViewerBookmark> Bookmarks
        {
            get { return bookmarks.AsReadOnly(); }
        }

        public ViewerFileSession()
        {
            FilePath = string.Empty;
            SelectedEncoding = EncodingChoice.Automatic;
            DisplayMode = ViewerDisplayMode.Normal;
            UpdatedUtc = DateTime.UtcNow;
            bookmarks = new List<ViewerBookmark>();
        }

        public ViewerBookmark AddOrUpdateBookmark(long offset, string label, string preview)
        {
            offset = Math.Max(0, offset);
            ViewerBookmark existing = FindBookmark(offset);
            if (existing == null)
            {
                if (bookmarks.Count >= MaximumBookmarks)
                {
                    bookmarks.Sort(delegate(ViewerBookmark left, ViewerBookmark right)
                    {
                        return left.CreatedUtc.CompareTo(right.CreatedUtc);
                    });
                    bookmarks.RemoveAt(0);
                }

                existing = new ViewerBookmark();
                existing.Offset = offset;
                bookmarks.Add(existing);
            }

            existing.Label = Limit(label, 240);
            existing.Preview = Limit(preview, 800);
            if (existing.CreatedUtc == DateTime.MinValue)
            {
                existing.CreatedUtc = DateTime.UtcNow;
            }
            SortBookmarks();
            UpdatedUtc = DateTime.UtcNow;
            return existing;
        }

        public bool RemoveBookmark(long offset)
        {
            ViewerBookmark bookmark = FindBookmark(offset);
            if (bookmark == null)
            {
                return false;
            }
            bookmarks.Remove(bookmark);
            UpdatedUtc = DateTime.UtcNow;
            return true;
        }

        public bool IsBookmarked(long offset)
        {
            return FindBookmark(offset) != null;
        }

        public ViewerBookmark FindBookmark(long offset)
        {
            for (int i = 0; i < bookmarks.Count; i++)
            {
                if (bookmarks[i].Offset == offset)
                {
                    return bookmarks[i];
                }
            }
            return null;
        }

        public ViewerBookmark FindNearestBookmark(long offset, bool forward)
        {
            if (bookmarks.Count == 0)
            {
                return null;
            }

            SortBookmarks();
            if (forward)
            {
                for (int i = 0; i < bookmarks.Count; i++)
                {
                    if (bookmarks[i].Offset > offset)
                    {
                        return bookmarks[i];
                    }
                }
            }
            else
            {
                for (int i = bookmarks.Count - 1; i >= 0; i--)
                {
                    if (bookmarks[i].Offset < offset)
                    {
                        return bookmarks[i];
                    }
                }
            }
            return null;
        }

        internal void AddLoadedBookmark(ViewerBookmark bookmark)
        {
            if (bookmark == null || bookmarks.Count >= MaximumBookmarks)
            {
                return;
            }

            if (FindBookmark(bookmark.Offset) == null)
            {
                bookmarks.Add(bookmark.Clone());
            }
        }

        internal void Normalize()
        {
            FilePath = FilePath ?? string.Empty;
            LastOffset = Math.Max(0, LastOffset);
            if (!Enum.IsDefined(typeof(EncodingChoice), SelectedEncoding))
            {
                SelectedEncoding = EncodingChoice.Automatic;
            }
            if (!Enum.IsDefined(typeof(ViewerDisplayMode), DisplayMode))
            {
                DisplayMode = ViewerDisplayMode.Normal;
            }

            for (int i = bookmarks.Count - 1; i >= 0; i--)
            {
                ViewerBookmark bookmark = bookmarks[i];
                if (bookmark == null || bookmark.Offset < 0)
                {
                    bookmarks.RemoveAt(i);
                    continue;
                }
                bookmark.Label = Limit(bookmark.Label, 240);
                bookmark.Preview = Limit(bookmark.Preview, 800);
                if (bookmark.CreatedUtc == DateTime.MinValue)
                {
                    bookmark.CreatedUtc = DateTime.UtcNow;
                }
            }
            SortBookmarks();
        }

        private void SortBookmarks()
        {
            bookmarks.Sort(delegate(ViewerBookmark left, ViewerBookmark right)
            {
                return left.Offset.CompareTo(right.Offset);
            });
        }

        private static string Limit(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Length <= maximumLength ? value : value.Substring(0, maximumLength);
        }
    }

    internal sealed class NavigationHistory
    {
        private readonly List<long> entries;
        private readonly int capacity;
        private int position;

        public bool CanGoBack
        {
            get { return position > 0; }
        }

        public bool CanGoForward
        {
            get { return position >= 0 && position < entries.Count - 1; }
        }

        public long CurrentOffset
        {
            get { return position >= 0 && position < entries.Count ? entries[position] : -1; }
        }

        public int Count
        {
            get { return entries.Count; }
        }

        public NavigationHistory()
            : this(250)
        {
        }

        public NavigationHistory(int capacity)
        {
            this.capacity = Math.Max(2, capacity);
            entries = new List<long>();
            position = -1;
        }

        public void Reset(long initialOffset)
        {
            entries.Clear();
            position = -1;
            Record(initialOffset);
        }

        public void Clear()
        {
            entries.Clear();
            position = -1;
        }

        public void Record(long offset)
        {
            offset = Math.Max(0, offset);
            if (position >= 0 && position < entries.Count && entries[position] == offset)
            {
                return;
            }

            if (position < entries.Count - 1)
            {
                entries.RemoveRange(position + 1, entries.Count - position - 1);
            }

            entries.Add(offset);
            if (entries.Count > capacity)
            {
                int removeCount = entries.Count - capacity;
                entries.RemoveRange(0, removeCount);
            }
            position = entries.Count - 1;
        }

        public bool TryGoBack(out long offset)
        {
            if (!CanGoBack)
            {
                offset = CurrentOffset;
                return false;
            }
            position--;
            offset = entries[position];
            return true;
        }

        public bool TryGoForward(out long offset)
        {
            if (!CanGoForward)
            {
                offset = CurrentOffset;
                return false;
            }
            position++;
            offset = entries[position];
            return true;
        }
    }

    internal static class ViewerSessionStore
    {
        private const string Header = "WORDLIST_VIEWER_SESSION_V2";
        private const int MaximumLineLength = 16384;
        private static readonly object SyncRoot = new object();
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, false);

        public static string SessionDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WordlistViewer",
                    "sessions");
            }
        }

        public static ViewerFileSession Load(string viewedFilePath)
        {
            ViewerFileSession fallback = CreateDefault(viewedFilePath);
            if (string.IsNullOrEmpty(viewedFilePath))
            {
                return fallback;
            }

            lock (SyncRoot)
            {
                string sessionPath;
                try
                {
                    sessionPath = GetSessionPath(viewedFilePath);
                }
                catch
                {
                    return fallback;
                }

                ViewerFileSession loaded;
                if (TryReadSession(sessionPath, viewedFilePath, out loaded))
                {
                    return loaded;
                }
                if (TryReadSession(sessionPath + ".bak", viewedFilePath, out loaded))
                {
                    return loaded;
                }
                return fallback;
            }
        }

        public static bool TrySave(ViewerFileSession session, out string error)
        {
            error = null;
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                error = Localization.T("error.session_invalid");
                return false;
            }

            lock (SyncRoot)
            {
                string temporaryPath = null;
                try
                {
                    session.Normalize();
                    RefreshMetadata(session);
                    session.UpdatedUtc = DateTime.UtcNow;

                    string directory = SessionDirectory;
                    Directory.CreateDirectory(directory);
                    string destinationPath = GetSessionPath(session.FilePath);
                    string backupPath = destinationPath + ".bak";
                    temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                    byte[] bytes = Utf8WithoutBom.GetBytes(Serialize(session));
                    using (FileStream stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        16 * 1024,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    if (File.Exists(destinationPath))
                    {
                        try
                        {
                            File.Replace(temporaryPath, destinationPath, backupPath, true);
                            temporaryPath = null;
                        }
                        catch (Exception replacementError)
                        {
                            if (replacementError is UnauthorizedAccessException ||
                                replacementError is IOException ||
                                replacementError is PlatformNotSupportedException)
                            {
                                SafeFallbackReplace(temporaryPath, destinationPath, backupPath);
                                temporaryPath = null;
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                    else
                    {
                        File.Move(temporaryPath, destinationPath);
                        temporaryPath = null;
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(temporaryPath))
                    {
                        try { File.Delete(temporaryPath); }
                        catch { }
                    }
                }
            }
        }

        public static ViewerFileSession CreateDefault(string viewedFilePath)
        {
            ViewerFileSession result = new ViewerFileSession();
            result.FilePath = NormalizePath(viewedFilePath);
            RefreshMetadata(result);
            return result;
        }

        private static void RefreshMetadata(ViewerFileSession session)
        {
            try
            {
                FileInfo information = new FileInfo(session.FilePath);
                information.Refresh();
                if (information.Exists)
                {
                    session.FileLength = information.Length;
                    session.FileLastWriteUtcTicks = information.LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
                // Session data remains useful even if the viewed file is temporarily unavailable.
            }
        }

        private static bool TryReadSession(string path, string expectedViewedFilePath, out ViewerFileSession session)
        {
            session = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                string[] lines = File.ReadAllLines(path, Utf8WithoutBom);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    return false;
                }

                ViewerFileSession parsed = new ViewerFileSession();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0 || line.Length > MaximumLineLength)
                    {
                        continue;
                    }

                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, equals);
                    string value = line.Substring(equals + 1);
                    ParseField(parsed, key, value);
                }

                parsed.Normalize();
                string expected = NormalizePath(expectedViewedFilePath);
                if (!string.Equals(parsed.FilePath, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                session = parsed;
                return true;
            }
            catch
            {
                session = null;
                return false;
            }
        }

        private static void ParseField(ViewerFileSession session, string key, string value)
        {
            long longValue;
            if (string.Equals(key, "Path", StringComparison.Ordinal))
            {
                session.FilePath = Decode(value);
            }
            else if (string.Equals(key, "FileLength", StringComparison.Ordinal) && TryParseLong(value, out longValue))
            {
                session.FileLength = Math.Max(0, longValue);
            }
            else if (string.Equals(key, "FileLastWriteUtcTicks", StringComparison.Ordinal) && TryParseLong(value, out longValue))
            {
                session.FileLastWriteUtcTicks = Math.Max(0, longValue);
            }
            else if (string.Equals(key, "LastOffset", StringComparison.Ordinal) && TryParseLong(value, out longValue))
            {
                session.LastOffset = Math.Max(0, longValue);
            }
            else if (string.Equals(key, "Encoding", StringComparison.Ordinal))
            {
                EncodingChoice encoding;
                if (Enum.TryParse<EncodingChoice>(value, false, out encoding))
                {
                    session.SelectedEncoding = encoding;
                }
            }
            else if (string.Equals(key, "DisplayMode", StringComparison.Ordinal))
            {
                ViewerDisplayMode displayMode;
                if (Enum.TryParse<ViewerDisplayMode>(value, false, out displayMode))
                {
                    session.DisplayMode = displayMode;
                }
            }
            else if (string.Equals(key, "UpdatedUtcTicks", StringComparison.Ordinal) && TryParseLong(value, out longValue))
            {
                try { session.UpdatedUtc = new DateTime(longValue, DateTimeKind.Utc); }
                catch { }
            }
            else if (string.Equals(key, "Bookmark", StringComparison.Ordinal))
            {
                ViewerBookmark bookmark = ParseBookmark(value);
                session.AddLoadedBookmark(bookmark);
            }
        }

        private static ViewerBookmark ParseBookmark(string value)
        {
            string[] parts = value.Split(new[] { '|' }, 4);
            if (parts.Length != 4)
            {
                return null;
            }

            long offset;
            long ticks;
            if (!TryParseLong(parts[0], out offset) || offset < 0 || !TryParseLong(parts[1], out ticks))
            {
                return null;
            }

            ViewerBookmark result = new ViewerBookmark();
            result.Offset = offset;
            result.Label = Decode(parts[2]);
            result.Preview = Decode(parts[3]);
            try { result.CreatedUtc = new DateTime(ticks, DateTimeKind.Utc); }
            catch { result.CreatedUtc = DateTime.UtcNow; }
            return result;
        }

        private static string Serialize(ViewerFileSession session)
        {
            StringBuilder builder = new StringBuilder(1024 + session.Bookmarks.Count * 160);
            builder.AppendLine(Header);
            builder.Append("Path=").AppendLine(Encode(session.FilePath));
            builder.Append("FileLength=").AppendLine(session.FileLength.ToString(CultureInfo.InvariantCulture));
            builder.Append("FileLastWriteUtcTicks=").AppendLine(session.FileLastWriteUtcTicks.ToString(CultureInfo.InvariantCulture));
            builder.Append("LastOffset=").AppendLine(session.LastOffset.ToString(CultureInfo.InvariantCulture));
            builder.Append("Encoding=").AppendLine(session.SelectedEncoding.ToString());
            builder.Append("DisplayMode=").AppendLine(session.DisplayMode.ToString());
            builder.Append("UpdatedUtcTicks=").AppendLine(session.UpdatedUtc.Ticks.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < session.Bookmarks.Count; i++)
            {
                ViewerBookmark bookmark = session.Bookmarks[i];
                builder.Append("Bookmark=")
                    .Append(bookmark.Offset.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(bookmark.CreatedUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Encode(bookmark.Label)).Append('|')
                    .AppendLine(Encode(bookmark.Preview));
            }
            return builder.ToString();
        }

        private static void SafeFallbackReplace(string temporaryPath, string destinationPath, string backupPath)
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                {
                    File.Move(backupPath, destinationPath);
                }
                throw;
            }
        }

        private static string GetSessionPath(string viewedFilePath)
        {
            string normalized = NormalizePath(viewedFilePath).ToUpperInvariant();
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(Utf8WithoutBom.GetBytes(normalized));
            }

            StringBuilder name = new StringBuilder(hash.Length * 2 + 4);
            for (int i = 0; i < hash.Length; i++)
            {
                name.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            name.Append(".wvs");
            return Path.Combine(SessionDirectory, name.ToString());
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Utf8WithoutBom.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            try { return Utf8WithoutBom.GetString(Convert.FromBase64String(value)); }
            catch { return string.Empty; }
        }

        private static bool TryParseLong(string value, out long result)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}
