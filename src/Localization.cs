using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace WordlistViewer
{
    internal enum AppLanguage
    {
        English,
        Turkish,
        Spanish,
        French,
        German,
        Portuguese
    }

    internal sealed class LanguageOption
    {
        public AppLanguage Language;
        public string Name;

        public LanguageOption(AppLanguage language, string name)
        {
            Language = language;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal static partial class Localization
    {
        private static readonly Dictionary<string, string> English = BuildEnglish();
        private static readonly Dictionary<string, string> Turkish = BuildTurkish();
        private static readonly Dictionary<string, string> Spanish = BuildSpanish();
        private static readonly Dictionary<string, string> French = BuildFrench();
        private static readonly Dictionary<string, string> German = BuildGerman();
        private static readonly Dictionary<string, string> Portuguese = BuildPortuguese();
        private static AppLanguage currentLanguage = AppLanguage.English;

        public static AppLanguage CurrentLanguage
        {
            get { return currentLanguage; }
        }

        public static void Initialize()
        {
            currentLanguage = LoadLanguage();
            ApplyCulture();
        }

        public static void SetLanguage(AppLanguage language)
        {
            currentLanguage = language;
            ApplyCulture();
            SaveLanguage(language);
        }

        internal static void SetLanguageForTests(AppLanguage language)
        {
            currentLanguage = language;
            ApplyCulture();
        }

        public static string T(string key)
        {
            string value;
            Dictionary<string, string> selected = GetDictionary(currentLanguage);
            if (selected.TryGetValue(key, out value)) return value;
            if (English.TryGetValue(key, out value)) return value;
            return key;
        }

        public static string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), arguments);
        }

        public static string EncodingName(EncodingChoice choice)
        {
            switch (choice)
            {
                case EncodingChoice.Automatic: return T("encoding.automatic");
                case EncodingChoice.Windows1254: return T("encoding.windows1254");
                case EncodingChoice.Windows1252: return "Windows-1252";
                case EncodingChoice.Latin1: return "ISO-8859-1";
                case EncodingChoice.Utf16LittleEndian: return "UTF-16 LE";
                case EncodingChoice.Utf16BigEndian: return "UTF-16 BE";
                default: return "UTF-8";
            }
        }

        public static string DisplayModeName(ViewerDisplayMode mode)
        {
            if (mode == ViewerDisplayMode.VisibleCharacters) return T("view.visible_characters");
            if (mode == ViewerDisplayMode.Hex) return T("view.hex");
            return T("view.normal");
        }

        public static string BottleneckName(string value)
        {
            if (string.Equals(value, "Mixed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Karma", StringComparison.OrdinalIgnoreCase))
            {
                return T("bottleneck.mixed");
            }
            return value;
        }

        private static void ApplyCulture()
        {
            CultureInfo culture = new CultureInfo(GetCultureName(currentLanguage));
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        private static AppLanguage LoadLanguage()
        {
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return AppLanguage.English;
                string value = File.ReadAllText(path, Encoding.UTF8).Trim();
                return ParseLanguageCode(value);
            }
            catch
            {
                return AppLanguage.English;
            }
        }

        private static void SaveLanguage(AppLanguage language)
        {
            string temporary = null;
            try
            {
                string path = GetSettingsPath();
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, GetLanguageCode(language), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                temporary = null;
            }
            catch
            {
                // A language switch remains active for this run even if the
                // preference cannot be persisted.
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporary))
                {
                    try { File.Delete(temporary); }
                    catch { }
                }
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WordlistViewer",
                "language.txt");
        }

        private static Dictionary<string, string> BuildEnglish()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
            d["window.title"] = "Wordlist & Log Viewer 2.3.1";
            d["header.badge"] = "  VIRTUAL LINES  •  READ ONLY  •  FIXED RAM  ";
            d["file.none"] = "No file is open";
            d["file.drop_hint"] = "Drop a file here or use the Open File button.";
            d["open_file"] = "Open File…";
            d["history.back"] = "Back in location history";
            d["history.forward"] = "Forward in location history";
            d["bookmark.off"] = "☆ Bookmark";
            d["bookmark.on"] = "★ Bookmark";
            d["position"] = "Position";
            d["go"] = "Go";
            d["view"] = "View";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Invisible characters";
            d["view.hex"] = "Hex";
            d["encoding"] = "Encoding";
            d["encoding.automatic"] = "Automatic";
            d["encoding.windows1254"] = "Windows-1254 (Turkish)";
            d["language"] = "Language";
            d["search.exact"] = "Exact search";
            d["search.previous"] = "↑ Previous";
            d["search.next"] = "Next ↓";
            d["search.all"] = "Find All";
            d["search.all.tip"] = "Scans the entire file, counts every match, and shows all result offsets in disk-backed pages.";
            d["cancel"] = "Cancel";
            d["go_line"] = "Go to line";
            d["index.create"] = "Build Index";
            d["index.refresh"] = "Refresh Index";
            d["index.cancel"] = "Cancel Index";
            d["file_position"] = "FILE POSITION";
            d["tab.search_results"] = "Search Results";
            d["tab.bookmarks"] = "Bookmarks";
            d["tab.performance"] = "Performance";
            d["results.loaded.zero"] = "Matches in loaded area: 0";
            d["results.loaded.count"] = "Matches in loaded area: {0}{1}";
            d["results.local_view"] = "Local view";
            d["bookmark.jump"] = "Go to Position";
            d["delete"] = "Delete";
            d["metric.speed"] = "SPEED";
            d["metric.eta"] = "TIME LEFT";
            d["metric.scanned"] = "SCANNED";
            d["metric.bottleneck"] = "BOTTLENECK";
            d["metric.read_compare"] = "READ / COMPARE";
            d["metric.read_compare.value"] = "{0:N1} s / {1:N1} s";
            d["metric.index"] = "NAVIGATION INDEX";
            d["none"] = "None";
            d["ready"] = "Ready";
            d["status.position.empty"] = "Position: —";
            d["status.encoding.empty"] = "Encoding: —";
            d["status.selected_position"] = "Selected position: {0:N3}%";
            d["dialog.open.title"] = "Open a text, wordlist, dictionary, or log file";
            d["dialog.open.filter"] = "Text, wordlist and log files|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|All files|*.*";
            d["error.file_not_found"] = "File not found.";
            d["status.loading_file"] = "Reading file information and session…";
            d["status.file_open_failed"] = "Could not open file";
            d["error.file_open_failed"] = "Could not open file:\r\n\r\n{0}";
            d["status.reading_view"] = "Reading the visible region from disk…";
            d["error.read_view"] = "Could not read the visible region";
            d["status.cache_ready"] = "Continuous cache ready — {0:N0} lines, up to {1} blocks in memory";
            d["status.prefetch_stopped"] = "Prefetch stopped: {0}";
            d["status.search_forward"] = "Searching forward…";
            d["status.search_backward"] = "Searching backward…";
            d["status.searching"] = "Searching… {0:N1}%  •  {1}/s  •  left {2}";
            d["status.no_match_forward"] = "No match was found before the end of the file.";
            d["status.no_match_backward"] = "No match was found before the beginning of the file.";
            d["status.match_found"] = "Match found — byte {0:N0}";
            d["status.search_cancelled"] = "Search cancelled.";
            d["error.search"] = "Search could not be completed";
            d["results.scan_initial"] = "Scanning entire file • 0 matches found";
            d["results.scan_running"] = "Scan in progress";
            d["status.scan_all_start"] = "Searching for exact matches in the entire file…";
            d["metric.scanned_matches"] = "SCANNED\r\n{0} / {1}  ({2:N1}%)\r\n{3:N0} matches";
            d["results.scan_progress"] = "Scanning entire file • {0:N0} matches found";
            d["status.scan_progress"] = "Scanning entire file… {0:N1}%  •  {1:N0} matches  •  {2}/s  •  left {3}";
            d["error.source_changed"] = "The source file changed during the scan. Results were not saved because they are no longer reliable.";
            d["status.scan_complete"] = "Full scan complete — {0:N0} matches • result index {1}";
            d["status.scan_cancelled"] = "Full-file scan cancelled; incomplete results were deleted.";
            d["error.scan_all"] = "Full-file scan could not be completed";
            d["error.results_read"] = "Search results could not be read";
            d["results.full"] = "Entire file: {0:N0} matches\r\nShowing: {1:N0}–{2:N0}";
            d["status.result"] = "Result #{0:N0} — byte {1:N0}";
            d["status.view"] = "View: {0}";
            d["status.encoding"] = "Encoding: {0}  •  {1}";
            d["status.encoding_changing"] = "Changing encoding…";
            d["error.encoding_change"] = "Encoding could not be changed";
            d["status.index_building"] = "Building the line navigation index…";
            d["metric.index_counted"] = "NAVIGATION INDEX\r\n{0:N0} lines counted";
            d["status.index_progress"] = "Building index… {0:N1}%  •  {1:N0} lines  •  left {2}";
            d["status.index_ready"] = "Navigation index ready — {0:N0} total lines";
            d["status.index_cancelled"] = "Index build cancelled.";
            d["error.index_build"] = "Index could not be built";
            d["status.finding_line"] = "Finding the position of line {0:N0}…";
            d["error.go_line"] = "Could not go to the line";
            d["status.bookmark_removed"] = "Bookmark removed — byte {0:N0}";
            d["status.bookmark_added"] = "Bookmark added — byte {0:N0}";
            d["status.ready_loaded"] = "Ready — {0:N0} lines in memory  •  last read {1:N0} ms";
            d["status.position"] = "Position: {0} / {1}  ({2:N3}%)";
            d["status.byte_length"] = "Byte: {0:N0}  •  Length: {1:N0}";
            d["index.available"] = "NAVIGATION INDEX\r\nReady  •  {0:N0} total lines";
            d["index.unavailable"] = "NAVIGATION INDEX\r\nNone — line numbers belong to the loaded area";
            d["status.font_size"] = "Font size: {0:0.#} pt";
            d["duration.days"] = "{0:N0} days {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/s";
            d["bottleneck.mixed"] = "Mixed";
            d["fatal.unexpected"] = "An unexpected error occurred:\r\n\r\n{0}";
            d["viewer.copy"] = "Copy selected lines";
            d["viewer.copy_info"] = "Copy with byte information";
            d["viewer.toggle_bookmark"] = "Toggle bookmark";
            d["viewer.open_line"] = "Open line separately";
            d["viewer.header.line"] = "LINE";
            d["viewer.header.offset"] = "BYTE OFFSET";
            d["viewer.header.length"] = "LENGTH";
            d["viewer.header.text"] = "TEXT";
            d["long_line.title"] = "Long Line / Raw Segment";
            d["long_line.previous"] = "← Previous segment";
            d["long_line.next"] = "Next segment →";
            d["long_line.copy"] = "Copy selection";
            d["long_line.reading"] = "Reading segment…";
            d["long_line.info"] = "Byte {0:N0}–{1:N0}  •  {2:N0} bytes{3}";
            d["long_line.continues"] = "  •  line continues";
            d["long_line.error"] = "Could not read segment:\r\n\r\n{0}";
            d["error.index_source_changed"] = "The file changed while the index was being built; the index was not saved.";
            d["error.result_disk_space"] = "Not enough safe disk space remains for search results. The scan was stopped to keep at least 2 GB free on the system drive.";
            d["error.session_invalid"] = "There is no valid file session to save.";
            d["language.changed"] = "Language changed to English.";
            return d;
        }

        private static Dictionary<string, string> BuildTurkish()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
            d["window.title"] = "Wordlist & Log Viewer 2.3.1";
            d["header.badge"] = "  SANAL SATIR  •  SALT OKUNUR  •  SABİT RAM  ";
            d["file.none"] = "Henüz dosya açılmadı";
            d["file.drop_hint"] = "Dosyayı sürükleyip bırakabilir veya Dosya Aç düğmesini kullanabilirsiniz.";
            d["open_file"] = "Dosya Aç…";
            d["history.back"] = "Konum geçmişinde geri";
            d["history.forward"] = "Konum geçmişinde ileri";
            d["bookmark.off"] = "☆ Yer İmi";
            d["bookmark.on"] = "★ Yer İmi";
            d["position"] = "Konum";
            d["go"] = "Git";
            d["view"] = "Görünüm";
            d["view.normal"] = "Normal";
            d["view.visible_characters"] = "Görünmez karakter";
            d["view.hex"] = "Hex";
            d["encoding"] = "Kodlama";
            d["encoding.automatic"] = "Otomatik";
            d["encoding.windows1254"] = "Windows-1254 (Türkçe)";
            d["language"] = "Dil";
            d["search.exact"] = "Birebir ara";
            d["search.previous"] = "↑ Önceki";
            d["search.next"] = "Sonraki ↓";
            d["search.all"] = "Tümünü Tara";
            d["search.all.tip"] = "Dosyayı baştan sona tarar, toplamı sayar ve tüm sonuç konumlarını disk tabanlı sayfalarda gösterir.";
            d["cancel"] = "İptal";
            d["go_line"] = "Satıra git";
            d["index.create"] = "İndeks Oluştur";
            d["index.refresh"] = "İndeksi Yenile";
            d["index.cancel"] = "İndeksi İptal";
            d["file_position"] = "DOSYA KONUMU";
            d["tab.search_results"] = "Arama Sonuçları";
            d["tab.bookmarks"] = "Yer İmleri";
            d["tab.performance"] = "Performans";
            d["results.loaded.zero"] = "Yüklü alandaki eşleşmeler: 0";
            d["results.loaded.count"] = "Yüklü alandaki eşleşmeler: {0}{1}";
            d["results.local_view"] = "Yerel görünüm";
            d["bookmark.jump"] = "Konuma Git";
            d["delete"] = "Sil";
            d["metric.speed"] = "HIZ";
            d["metric.eta"] = "KALAN SÜRE";
            d["metric.scanned"] = "TARANAN";
            d["metric.bottleneck"] = "DARBOĞAZ";
            d["metric.read_compare"] = "OKUMA / KARŞILAŞTIRMA";
            d["metric.read_compare.value"] = "{0:N1} sn / {1:N1} sn";
            d["metric.index"] = "NAVİGASYON İNDEKSİ";
            d["none"] = "Yok";
            d["ready"] = "Hazır";
            d["status.position.empty"] = "Konum: —";
            d["status.encoding.empty"] = "Kodlama: —";
            d["status.selected_position"] = "Seçilen konum: %{0:N3}";
            d["dialog.open.title"] = "Metin, wordlist, sözlük veya log dosyasını aç";
            d["dialog.open.filter"] = "Metin, wordlist ve log dosyaları|*.txt;*.lst;*.dic;*.wordlist;*.log;*.out;*.trace;*.jsonl;*.ndjson|Tüm dosyalar|*.*";
            d["error.file_not_found"] = "Dosya bulunamadı.";
            d["status.loading_file"] = "Dosya bilgisi ve oturum okunuyor…";
            d["status.file_open_failed"] = "Dosya açılamadı";
            d["error.file_open_failed"] = "Dosya açılamadı:\r\n\r\n{0}";
            d["status.reading_view"] = "Görüntü bölgesi diskten okunuyor…";
            d["error.read_view"] = "Görüntü bölgesi okunamadı";
            d["status.cache_ready"] = "Kesintisiz önbellek hazır — {0:N0} satır, en fazla {1} blok bellekte";
            d["status.prefetch_stopped"] = "Ön yükleme durduruldu: {0}";
            d["status.search_forward"] = "İleri doğru aranıyor…";
            d["status.search_backward"] = "Geri doğru aranıyor…";
            d["status.searching"] = "Aranıyor… %{0:N1}  •  {1}/sn  •  kalan {2}";
            d["status.no_match_forward"] = "Dosyanın sonuna kadar eşleşme bulunamadı.";
            d["status.no_match_backward"] = "Dosyanın başına kadar eşleşme bulunamadı.";
            d["status.match_found"] = "Eşleşme bulundu — byte {0:N0}";
            d["status.search_cancelled"] = "Arama iptal edildi.";
            d["error.search"] = "Arama tamamlanamadı";
            d["results.scan_initial"] = "Tüm dosya taranıyor • 0 eşleşme bulundu";
            d["results.scan_running"] = "Tarama sürüyor";
            d["status.scan_all_start"] = "Tüm dosyada birebir eşleşmeler aranıyor…";
            d["metric.scanned_matches"] = "TARANAN\r\n{0} / {1}  (%{2:N1})\r\n{3:N0} eşleşme";
            d["results.scan_progress"] = "Tüm dosya taranıyor • {0:N0} eşleşme bulundu";
            d["status.scan_progress"] = "Tüm dosya taranıyor… %{0:N1}  •  {1:N0} eşleşme  •  {2}/sn  •  kalan {3}";
            d["error.source_changed"] = "Kaynak dosya tarama sırasında değişti. Sonuçlar güvenilir olmadığı için saklanmadı.";
            d["status.scan_complete"] = "Tam tarama tamamlandı — {0:N0} eşleşme • sonuç dizini {1}";
            d["status.scan_cancelled"] = "Tüm dosya taraması iptal edildi; eksik sonuçlar silindi.";
            d["error.scan_all"] = "Tüm dosya taraması tamamlanamadı";
            d["error.results_read"] = "Arama sonuçları okunamadı";
            d["results.full"] = "Tüm dosya: {0:N0} eşleşme\r\nGösterilen: {1:N0}–{2:N0}";
            d["status.result"] = "Sonuç #{0:N0} — byte {1:N0}";
            d["status.view"] = "Görünüm: {0}";
            d["status.encoding"] = "Kodlama: {0}  •  {1}";
            d["status.encoding_changing"] = "Kodlama değiştiriliyor…";
            d["error.encoding_change"] = "Kodlama değiştirilemedi";
            d["status.index_building"] = "Satır navigasyon indeksi oluşturuluyor…";
            d["metric.index_counted"] = "NAVİGASYON İNDEKSİ\r\n{0:N0} satır sayıldı";
            d["status.index_progress"] = "İndeks oluşturuluyor… %{0:N1}  •  {1:N0} satır  •  kalan {2}";
            d["status.index_ready"] = "Navigasyon indeksi hazır — toplam {0:N0} satır";
            d["status.index_cancelled"] = "İndeks oluşturma iptal edildi.";
            d["error.index_build"] = "İndeks oluşturulamadı";
            d["status.finding_line"] = "{0:N0}. satırın konumu bulunuyor…";
            d["error.go_line"] = "Satıra gidilemedi";
            d["status.bookmark_removed"] = "Yer imi kaldırıldı — byte {0:N0}";
            d["status.bookmark_added"] = "Yer imi eklendi — byte {0:N0}";
            d["status.ready_loaded"] = "Hazır — {0:N0} satır bellekte  •  son okuma {1:N0} ms";
            d["status.position"] = "Konum: {0} / {1}  (%{2:N3})";
            d["status.byte_length"] = "Byte: {0:N0}  •  Uzunluk: {1:N0}";
            d["index.available"] = "NAVİGASYON İNDEKSİ\r\nHazır  •  {0:N0} toplam satır";
            d["index.unavailable"] = "NAVİGASYON İNDEKSİ\r\nYok — satır numaraları yüklü alana ait";
            d["status.font_size"] = "Yazı boyutu: {0:0.#} pt";
            d["duration.days"] = "{0:N0} gün {1:hh\\:mm\\:ss}";
            d["unit.per_second"] = "/sn";
            d["bottleneck.mixed"] = "Karma";
            d["fatal.unexpected"] = "Beklenmeyen bir hata oluştu:\r\n\r\n{0}";
            d["viewer.copy"] = "Seçili satırları kopyala";
            d["viewer.copy_info"] = "Byte bilgisiyle kopyala";
            d["viewer.toggle_bookmark"] = "Yer imini aç/kapat";
            d["viewer.open_line"] = "Satırı ayrı görüntüle";
            d["viewer.header.line"] = "SATIR";
            d["viewer.header.offset"] = "BYTE KONUMU";
            d["viewer.header.length"] = "UZUNLUK";
            d["viewer.header.text"] = "METİN";
            d["long_line.title"] = "Uzun Satır / Ham Segment";
            d["long_line.previous"] = "← Önceki segment";
            d["long_line.next"] = "Sonraki segment →";
            d["long_line.copy"] = "Seçimi kopyala";
            d["long_line.reading"] = "Segment okunuyor…";
            d["long_line.info"] = "Byte {0:N0}–{1:N0}  •  {2:N0} byte{3}";
            d["long_line.continues"] = "  •  satır devam ediyor";
            d["long_line.error"] = "Segment okunamadı:\r\n\r\n{0}";
            d["error.index_source_changed"] = "Dosya indeks oluşturulurken değişti; indeks kaydedilmedi.";
            d["error.result_disk_space"] = "Arama sonuçları için güvenli boş disk alanı kalmadı. Sistem diskinde en az 2 GB boş alan bırakmak için tarama durduruldu.";
            d["error.session_invalid"] = "Kaydedilecek geçerli bir dosya oturumu yok.";
            d["language.changed"] = "Dil Türkçe olarak değiştirildi.";
            return d;
        }
    }
}
