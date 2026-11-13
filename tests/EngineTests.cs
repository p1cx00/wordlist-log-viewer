using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace WordlistViewer
{
    internal static class EngineTests
    {
        private static int passed;

        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "WordlistViewerTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestSmallUtf8(root);
                TestLocalization();
                TestControlCharacterDisplay(root);
                TestViewerLineModes(root);
                TestViewerStatePreservation();
                TestViewerChunkRequestAfterWindowReplacement();
                TestPageBoundary(root);
                TestLongLinePaging(root);
                TestForwardAndBackwardSearch(root);
                TestFullFileSearch(root);
                TestFullSearchResultStore();
                TestUtf16(root);
                TestNavigationIndex(root);
                TestOffsetsBeyond32Bit(root);
                Console.WriteLine("PASS: {0} test", passed);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch { }
            }
        }

        private static void TestSmallUtf8(string root)
        {
            string path = Path.Combine(root, "utf8.txt");
            File.WriteAllText(path, "alpha\r\nbeta\ngamma\r\nTürkçe\n", new UTF8Encoding(true));
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Automatic);
            Assert(encoding.Choice == EncodingChoice.Utf8, "UTF-8 BOM detection");
            PageData page = HugeFileReader.ReadPage(path, 0, true, encoding, CancellationToken.None);
            Assert(page.Text.StartsWith("alpha"), "BOM is omitted from display text");
            Assert(page.Text.Contains("Türkçe"), "UTF-8 text is decoded");
            Assert(page.EndOffset == page.FileLength, "Small file is shown in one page");
            Pass("small UTF-8");
        }

        private static void TestLocalization()
        {
            Localization.SetLanguageForTests(AppLanguage.English);
            Assert(Localization.T("open_file") == "Open File…", "English is the primary UI language");
            Assert(Localization.T("viewer.header.offset") == "BYTE OFFSET", "English viewer headers");
            Assert(Localization.EncodingName(EncodingChoice.Windows1254).Contains("Turkish"), "English encoding label");

            Localization.SetLanguageForTests(AppLanguage.Turkish);
            Assert(Localization.T("open_file") == "Dosya Aç…", "Turkish UI translation");
            Assert(Localization.T("viewer.header.offset") == "BYTE KONUMU", "Turkish viewer headers");
            Assert(Localization.DisplayModeName(ViewerDisplayMode.VisibleCharacters) == "Görünmez karakter", "Turkish display mode translation");

            Localization.SetLanguageForTests(AppLanguage.Spanish);
            Assert(Localization.T("open_file") == "Abrir archivo…", "Spanish UI translation");
            Assert(Localization.T("viewer.header.line") == "LÍNEA", "Spanish viewer headers");

            Localization.SetLanguageForTests(AppLanguage.French);
            Assert(Localization.T("open_file") == "Ouvrir un fichier…", "French UI translation");
            Assert(Localization.T("viewer.header.line") == "LIGNE", "French viewer headers");

            Localization.SetLanguageForTests(AppLanguage.German);
            Assert(Localization.T("open_file") == "Datei öffnen…", "German UI translation");
            Assert(Localization.T("viewer.header.line") == "ZEILE", "German viewer headers");

            Localization.SetLanguageForTests(AppLanguage.Portuguese);
            Assert(Localization.T("open_file") == "Abrir arquivo…", "Portuguese UI translation");
            Assert(Localization.T("viewer.header.line") == "LINHA", "Portuguese viewer headers");

            Assert(Localization.SupportedLanguages.Length == 6, "six supported languages");

            Localization.SetLanguageForTests(AppLanguage.English);
            Pass("six-language localization");
        }

        private static void TestPageBoundary(string root)
        {
            string path = Path.Combine(root, "pages.txt");
            using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                for (int i = 0; i < 60000; i++)
                {
                    writer.Write("word-{0:D6}\n", i);
                }
            }
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Automatic);
            PageData first = HugeFileReader.ReadPage(path, 0, true, encoding, CancellationToken.None);
            Assert(first.EndOffset > HugeFileReader.PageBytes, "Page extends to a line boundary");
            Assert(first.Text.EndsWith("\n"), "First page ends at newline");
            PageData second = HugeFileReader.ReadPage(path, first.EndOffset, true, encoding, CancellationToken.None);
            Assert(second.StartOffset == first.EndOffset, "Next page begins without a gap or overlap");
            Assert(second.Text.StartsWith("word-"), "Next page starts at complete line");
            Pass("page boundary");
        }

        private static void TestViewerLineModes(string root)
        {
            string path = Path.Combine(root, "line-modes.bin");
            File.WriteAllBytes(path, new byte[] {
                (byte)'a', 0, (byte)'b', 9, (byte)'c', 13, 10,
                (byte)'x', (byte)'y', 10
            });
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Windows1254);
            PageData page = HugeFileReader.ReadPage(path, 0, true, encoding, CancellationToken.None);
            List<ViewerLine> normal = ViewerLineBuilder.Build(page, encoding, ViewerDisplayMode.Normal, 1);
            List<ViewerLine> visible = ViewerLineBuilder.Build(page, encoding, ViewerDisplayMode.VisibleCharacters, 1);
            List<ViewerLine> hex = ViewerLineBuilder.Build(page, encoding, ViewerDisplayMode.Hex, 1);
            Assert(normal.Count == 2, "Normal view splits two lines");
            Assert(normal[0].ByteOffset == 0 && normal[0].ByteLength == 7, "Line byte metadata includes CRLF");
            Assert(normal[1].ByteOffset == 7 && normal[1].ByteLength == 3, "Second line byte metadata");
            Assert(visible[0].Text.Contains("␀") && visible[0].Text.Contains("⇥") && visible[0].Text.Contains("␍␊"), "Visible control glyphs");
            Assert(hex.Count == 1 && hex[0].Text.Contains("61 00 62"), "Hex view formats raw bytes");
            PageData segment = HugeFileReader.ReadLineSegment(path, 7, encoding, CancellationToken.None);
            Assert(segment.ContentBytes.Length == 3 && !segment.TruncatedAtStart && !segment.TruncatedAtEnd, "Separate line window reads one logical line");
            Pass("viewer line modes");
        }

        private static void TestControlCharacterDisplay(string root)
        {
            string path = Path.Combine(root, "controls.bin");
            File.WriteAllBytes(path, new byte[] { (byte)'a', 0, (byte)'b', 1, (byte)'c', 10 });
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Windows1254);
            PageData page = HugeFileReader.ReadPage(path, 0, true, encoding, CancellationToken.None);
            Assert(page.ReplacedControlCharacters == 2, "Control characters are counted");
            Assert(page.Text == "a\u2400b\u00B7c\n", "Control characters are display-safe");
            Assert(page.Text.IndexOf('\0') < 0, "NUL does not truncate the rich text control");
            Pass("control-character display");
        }

        private static void TestViewerStatePreservation()
        {
            List<ViewerLine> original = new List<ViewerLine>();
            for (int i = 0; i < 80; i++)
            {
                original.Add(new ViewerLine
                {
                    ByteOffset = 1000 + i * 10,
                    ByteLength = 10,
                    Text = "line-" + i
                });
            }

            using (VirtualLineViewer viewer = new VirtualLineViewer())
            {
                viewer.SetLines(original);
                viewer.RestoreTopByteOffset(1200);
                Assert(viewer.SelectOffsetIfPresent(1250, false), "Viewer selects an exact byte offset");
                viewer.ActiveMatchOffset = 1253;
                viewer.ActiveMatchCharIndex = 3;

                List<ViewerLine> expanded = new List<ViewerLine>();
                for (int i = 0; i < 10; i++)
                {
                    expanded.Add(new ViewerLine
                    {
                        ByteOffset = 900 + i * 10,
                        ByteLength = 10,
                        Text = "prefetched-" + i
                    });
                }
                expanded.AddRange(original);

                viewer.ReplaceLinesPreservingState(expanded, 1200);
                Assert(viewer.TopByteOffset == 1200, "Background chunk rebuild preserves the top byte offset");
                Assert(viewer.SelectedLine != null && viewer.SelectedLine.ByteOffset == 1250, "Background chunk rebuild preserves the selected result line");
                Assert(viewer.ActiveMatchOffset == 1253 && viewer.ActiveMatchCharIndex == 3, "Background chunk rebuild preserves the active match highlight");
            }
            Pass("viewer state preservation");
        }

        private static void TestViewerChunkRequestAfterWindowReplacement()
        {
            using (VirtualLineViewer viewer = new VirtualLineViewer())
            {
                List<ViewerLine> oldWindow = CreateViewerLines(0, 200);
                viewer.SetLines(oldWindow);
                viewer.TopLineIndex = 190;

                List<bool> directions = new List<bool>();
                List<long> edges = new List<long>();
                viewer.ChunkRequested += delegate(object sender, ChunkRequestEventArgs e)
                {
                    directions.Add(e.Forward);
                    edges.Add(e.EdgeByteOffset);
                };

                List<ViewerLine> targetWindow = CreateViewerLines(2000000, 200);
                viewer.SetLines(targetWindow);

                Assert(directions.Count == 1 && !directions[0], "Replacing a window requests only the stable top-side neighbor");
                Assert(edges[0] == targetWindow[0].ByteOffset, "Chunk request uses the new window's top edge instead of the stale scroll position");
            }
            Pass("viewer chunk request replacement");
        }

        private static List<ViewerLine> CreateViewerLines(long firstOffset, int count)
        {
            List<ViewerLine> result = new List<ViewerLine>();
            for (int i = 0; i < count; i++)
            {
                result.Add(new ViewerLine
                {
                    ByteOffset = firstOffset + i * 10,
                    ByteLength = 10,
                    Text = "line-" + i
                });
            }
            return result;
        }

        private static void TestLongLinePaging(string root)
        {
            string path = Path.Combine(root, "long-line.txt");
            byte[] longLine = new byte[HugeFileReader.PageBytes * 2 + 100];
            for (int i = 0; i < longLine.Length; i++) longLine[i] = (byte)'x';
            using (FileStream stream = File.Create(path))
            {
                stream.Write(longLine, 0, longLine.Length);
                stream.WriteByte((byte)'\n');
                byte[] tail = Encoding.ASCII.GetBytes("tail\n");
                stream.Write(tail, 0, tail.Length);
            }
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Utf8);
            PageData first = HugeFileReader.ReadPage(path, 0, true, encoding, CancellationToken.None);
            Assert(first.TruncatedAtEnd, "Long line is safely truncated");
            PageData second = HugeFileReader.ReadPage(path, first.EndOffset, false, encoding, CancellationToken.None);
            Assert(second.TruncatedAtStart, "Continuation is marked as mid-line");
            Assert(second.StartOffset == first.EndOffset, "Long-line continuation is contiguous");
            Pass("long line paging");
        }

        private static void TestForwardAndBackwardSearch(string root)
        {
            string path = Path.Combine(root, "search.bin");
            int block = 4 * 1024 * 1024;
            byte[] bytes = new byte[block + 256];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)'a';
            byte[] pattern = Encoding.ASCII.GetBytes("boundary-needle");
            int firstOffset = block - 5;
            Buffer.BlockCopy(pattern, 0, bytes, firstOffset, pattern.Length);
            int secondOffset = block + 100;
            Buffer.BlockCopy(pattern, 0, bytes, secondOffset, pattern.Length);
            File.WriteAllBytes(path, bytes);

            long forward = ByteSearch.FindForward(path, pattern, 0, CancellationToken.None, null);
            Assert(forward == firstOffset, "Forward search finds pattern across chunk boundary");
            long next = ByteSearch.FindForward(path, pattern, forward + pattern.Length, CancellationToken.None, null);
            Assert(next == secondOffset, "Forward search finds next match");
            long backward = ByteSearch.FindBackward(path, pattern, bytes.Length, CancellationToken.None, null);
            Assert(backward == secondOffset, "Backward search finds last match");
            long previous = ByteSearch.FindBackward(path, pattern, secondOffset, CancellationToken.None, null);
            Assert(previous == firstOffset, "Backward search finds previous boundary match");
            Pass("forward/backward search");
        }

        private static void TestFullFileSearch(string root)
        {
            string path = Path.Combine(root, "search-all.bin");
            int block = 4 * 1024 * 1024;
            byte[] bytes = new byte[block + 256];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)'x';
            byte[] pattern = Encoding.ASCII.GetBytes("whole-file-needle");
            int firstOffset = block - 6;
            int secondOffset = block + 100;
            int thirdOffset = bytes.Length - pattern.Length;
            Buffer.BlockCopy(pattern, 0, bytes, firstOffset, pattern.Length);
            Buffer.BlockCopy(pattern, 0, bytes, secondOffset, pattern.Length);
            Buffer.BlockCopy(pattern, 0, bytes, thirdOffset, pattern.Length);
            File.WriteAllBytes(path, bytes);

            List<long> offsets = new List<long>();
            long count = ByteSearch.ScanAll(path, pattern, 0, CancellationToken.None, 1, null, offsets.Add);
            Assert(count == 3 && offsets.Count == 3, "Full search counts every result");
            Assert(offsets[0] == firstOffset && offsets[1] == secondOffset && offsets[2] == thirdOffset, "Full search preserves boundary and EOF offsets without duplicates");

            offsets.Clear();
            count = ByteSearch.ScanAll(path, new byte[bytes.Length + 1], 0, CancellationToken.None, 1, null, offsets.Add);
            Assert(count == 0 && offsets.Count == 0, "Pattern longer than the file has no matches");

            string overlapPath = Path.Combine(root, "search-non-overlap.txt");
            File.WriteAllText(overlapPath, "aaaaa", Encoding.ASCII);
            offsets.Clear();
            count = ByteSearch.ScanAll(overlapPath, Encoding.ASCII.GetBytes("aa"), 0, CancellationToken.None, 1, null, offsets.Add);
            Assert(count == 2 && offsets[0] == 0 && offsets[1] == 2, "Full search uses the same non-overlapping semantics as next-match search");

            CancellationTokenSource cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            bool cancellationObserved = false;
            try
            {
                ByteSearch.ScanAll(path, pattern, 0, cancelled.Token, 1, null, delegate(long value) { });
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }
            finally
            {
                cancelled.Dispose();
            }
            Assert(cancellationObserved, "Full search observes cancellation");
            Pass("full-file search");
        }

        private static void TestFullSearchResultStore()
        {
            FullSearchResultStore store = FullSearchResultStore.Create();
            try
            {
                store.Append(7);
                store.Append((long)int.MaxValue + 12345L);
                store.Append(5L * 1024 * 1024 * 1024 + 99L);
                store.Complete();
                Assert(store.Count == 3 && store.StorageBytes == 24, "Disk-backed search result count and size");
                long[] tail = store.ReadRange(1, 10);
                Assert(tail.Length == 2, "Result store range returns available suffix");
                Assert(tail[0] == (long)int.MaxValue + 12345L && tail[1] > uint.MaxValue, "Result store preserves 64-bit offsets");
            }
            finally
            {
                store.Dispose();
            }
            Pass("full-search result store");
        }

        private static void TestUtf16(string root)
        {
            string path = Path.Combine(root, "utf16.txt");
            File.WriteAllText(path, "bir\r\niki\r\nüç\r\n", new UnicodeEncoding(false, true));
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Automatic);
            Assert(encoding.Choice == EncodingChoice.Utf16LittleEndian, "UTF-16 BOM detection");
            PageData page = HugeFileReader.ReadPage(path, 8, true, encoding, CancellationToken.None);
            Assert(page.StartOffset % 2 == 0, "UTF-16 byte alignment");
            Assert(page.Text.Contains("iki"), "UTF-16 page decoding");
            byte[] pattern = encoding.Encoding.GetBytes("üç");
            long found = ByteSearch.FindForward(path, pattern, 0, CancellationToken.None, null);
            Assert(found > 0 && found % 2 == 0, "UTF-16 search");
            List<long> all = new List<long>();
            long allCount = ByteSearch.ScanAll(path, pattern, encoding.BomLength, CancellationToken.None, encoding.UnitSize, null, all.Add);
            Assert(allCount == 1 && all[0] == found, "UTF-16 full search respects alignment");
            Pass("UTF-16");
        }

        private static void TestNavigationIndex(string root)
        {
            string path = Path.Combine(root, "navigation.txt");
            using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                for (int i = 1; i <= 5000; i++) writer.Write("line-{0:D5}\n", i);
            }
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Utf8);
            NavigationIndexData index = NavigationIndexStore.Build(path, encoding, CancellationToken.None, null);
            Assert(index.TotalLines == 5001, "Navigation index total lines");
            long offset = NavigationIndexStore.FindOffsetForLine(path, 4321, encoding, index, CancellationToken.None);
            long line = NavigationIndexStore.FindLineNumberAtOffset(path, offset, encoding, index, CancellationToken.None);
            Assert(line == 4321, "Go-to-line round trip");
            NavigationIndexData loaded = NavigationIndexStore.TryLoad(path, encoding);
            Assert(loaded != null && loaded.TotalLines == index.TotalLines, "Navigation index persistence");
            Pass("navigation index");
        }

        private static void TestOffsetsBeyond32Bit(string root)
        {
            string path = Path.Combine(root, "large-offset.bin");
            long length = 5L * 1024 * 1024 * 1024 + 12345;
            byte[] ending = Encoding.ASCII.GetBytes("\nBEYOND-32-BIT\n");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                stream.SetLength(length);
                stream.Position = length - ending.Length;
                stream.Write(ending, 0, ending.Length);
            }
            ResolvedEncoding encoding = EncodingResolver.Resolve(path, EncodingChoice.Utf8);
            PageData page = HugeFileReader.ReadPage(path, length, true, encoding, CancellationToken.None);
            Assert(page.FileLength == length, "64-bit file length is preserved");
            Assert(page.StartOffset > int.MaxValue, "Page seek uses 64-bit offset");
            Assert(page.Text.Contains("BEYOND-32-BIT"), "Data beyond 32-bit offset is readable");
            Pass("64-bit offset");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Pass(string name)
        {
            passed++;
            Console.WriteLine("PASS: " + name);
        }
    }
}
