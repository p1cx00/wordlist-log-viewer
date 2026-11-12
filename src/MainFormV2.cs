using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordlistViewer
{
    internal sealed class EncodingOptionV2
    {
        public string Name;
        public EncodingChoice Choice;

        public EncodingOptionV2(string name, EncodingChoice choice)
        {
            Name = name;
            Choice = choice;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class DisplayModeOption
    {
        public string Name;
        public ViewerDisplayMode Mode;

        public DisplayModeOption(string name, ViewerDisplayMode mode)
        {
            Name = name;
            Mode = mode;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class SearchResultListItem
    {
        public ViewerLine Line;
        public int CharacterIndex;
        public bool IsFullFileResult;
        public long MatchNumber;
        public long MatchOffset;

        public long ByteOffset
        {
            get { return IsFullFileResult ? MatchOffset : Line == null ? 0 : Line.ByteOffset; }
        }

        public override string ToString()
        {
            if (IsFullFileResult)
            {
                return string.Format("#{0:N0}  •  byte {1:N0}", MatchNumber, MatchOffset);
            }
            string preview = DisplayFormatter.MakePreview(Line == null ? string.Empty : Line.Text, 90);
            return string.Format("{0:N0}  •  {1}", Line == null ? 0 : Line.ByteOffset, preview);
        }
    }

    internal sealed class BookmarkListItem
    {
        public ViewerBookmark Bookmark;

        public override string ToString()
        {
            return string.Format("★  {0:N0}  •  {1}", Bookmark.Offset, Bookmark.Label);
        }
    }

    internal sealed class MainFormV2 : Form
    {
        private const int PositionUnits = 1000000;
        private const int MaximumChunks = 3;
        private const int MaximumLocalSearchResults = 250;
        private const int FullSearchResultsPerPage = 500;

        private static readonly Color WindowColor = Color.FromArgb(20, 23, 30);
        private static readonly Color PanelColor = Color.FromArgb(29, 34, 44);
        private static readonly Color ViewerColor = Color.FromArgb(14, 17, 23);
        private static readonly Color PrimaryTextColor = Color.FromArgb(235, 239, 247);
        private static readonly Color SecondaryTextColor = Color.FromArgb(155, 166, 184);
        private static readonly Color AccentColor = Color.FromArgb(83, 143, 245);
        private static readonly Color AccentHoverColor = Color.FromArgb(105, 160, 250);
        private static readonly Color BorderColor = Color.FromArgb(54, 63, 79);

        private readonly string initialFile;
        private readonly List<ViewerChunk> chunks;
        private readonly NavigationHistory navigationHistory;

        private string currentPath;
        private ResolvedEncoding currentEncoding;
        private ViewerFileSession currentSession;
        private NavigationIndexData navigationIndex;
        private ViewerDisplayMode displayMode;
        private long currentMatchOffset;
        private string currentMatchQuery;
        private int pageGeneration;
        private bool suppressUiEvents;
        private bool adjacentLoadRunning;
        private bool searchRunning;
        private bool indexRunning;
        private bool fileControlsReady;
        private float viewerFontSize;
        private FullSearchResultStore fullSearchResults;
        private long fullSearchResultPage;
        private string fullSearchQuery;
        private string fullSearchPath;

        private CancellationTokenSource pageCancellation;
        private CancellationTokenSource searchCancellation;
        private CancellationTokenSource indexCancellation;

        private Label fileNameLabel;
        private Label filePathLabel;
        private VirtualLineViewer lineViewer;
        private TrackBar positionTrackBar;
        private NumericUpDown percentInput;
        private NumericUpDown lineInput;
        private ComboBox encodingCombo;
        private ComboBox displayModeCombo;
        private ComboBox languageCombo;
        private TextBox searchBox;
        private Button openButton;
        private Button backButton;
        private Button forwardButton;
        private Button bookmarkButton;
        private Button previousMatchButton;
        private Button nextMatchButton;
        private Button fullSearchButton;
        private Button cancelButton;
        private Button indexButton;
        private Button goLineButton;
        private ProgressBar operationProgress;
        private ListBox searchResultsList;
        private ListBox bookmarksList;
        private Label searchResultsCaption;
        private Label searchResultsPageLabel;
        private Button previousResultsPageButton;
        private Button nextResultsPageButton;
        private Label performanceSpeedLabel;
        private Label performanceEtaLabel;
        private Label performanceScannedLabel;
        private Label performanceBottleneckLabel;
        private Label performanceReadLabel;
        private Label indexInfoLabel;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusMessage;
        private ToolStripStatusLabel statusPosition;
        private ToolStripStatusLabel statusEncoding;
        private System.Windows.Forms.Timer positionTimer;
        private System.Windows.Forms.Timer sessionTimer;
        private ToolTip toolTip;

        public MainFormV2(string initialFile)
        {
            this.initialFile = initialFile;
            chunks = new List<ViewerChunk>();
            navigationHistory = new NavigationHistory(250);
            currentMatchOffset = -1;
            viewerFontSize = 10.5f;
            displayMode = ViewerDisplayMode.Normal;
            BuildInterface();
            WireEvents();
        }

        private void BuildInterface()
        {
            Text = Localization.T("window.title");
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 680);
            Size = new Size(1460, 880);
            BackColor = WindowColor;
            ForeColor = PrimaryTextColor;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;
            AllowDrop = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 12000;
            toolTip.InitialDelay = 300;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 68;
            header.BackColor = PanelColor;

            Label title = new Label();
            title.AutoSize = true;
            title.Text = "WORDLIST / LOG VIEWER  2.3.1";
            title.Font = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold);
            title.ForeColor = PrimaryTextColor;
            title.Location = new Point(18, 8);
            header.Controls.Add(title);

            Label badge = new Label();
            badge.AutoSize = true;
            BindLocalization(badge, "header.badge");
            badge.Font = new Font("Segoe UI Semibold", 8.0f, FontStyle.Bold);
            badge.ForeColor = Color.FromArgb(132, 206, 157);
            badge.BackColor = Color.FromArgb(35, 63, 48);
            badge.Location = new Point(245, 13);
            badge.Padding = new Padding(4, 3, 4, 3);
            header.Controls.Add(badge);

            fileNameLabel = new Label();
            fileNameLabel.Text = Localization.T("file.none");
            fileNameLabel.ForeColor = SecondaryTextColor;
            fileNameLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            fileNameLabel.Location = new Point(20, 42);
            fileNameLabel.Size = new Size(285, 20);
            fileNameLabel.AutoEllipsis = true;
            header.Controls.Add(fileNameLabel);

            filePathLabel = new Label();
            filePathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            filePathLabel.Text = Localization.T("file.drop_hint");
            filePathLabel.ForeColor = SecondaryTextColor;
            filePathLabel.Location = new Point(315, 43);
            filePathLabel.Size = new Size(1100, 20);
            filePathLabel.AutoEllipsis = true;
            header.Controls.Add(filePathLabel);

            FlowLayoutPanel navigationBar = new FlowLayoutPanel();
            navigationBar.Dock = DockStyle.Top;
            navigationBar.Height = 52;
            navigationBar.BackColor = WindowColor;
            navigationBar.Padding = new Padding(12, 8, 8, 5);
            navigationBar.WrapContents = false;
            navigationBar.AutoScroll = true;

            openButton = CreateLocalizedButton("open_file", true, 104);
            navigationBar.Controls.Add(openButton);
            backButton = CreateButton("←", false, 42);
            backButton.Enabled = false;
            toolTip.SetToolTip(backButton, Localization.T("history.back"));
            navigationBar.Controls.Add(backButton);
            forwardButton = CreateButton("→", false, 42);
            forwardButton.Enabled = false;
            toolTip.SetToolTip(forwardButton, Localization.T("history.forward"));
            navigationBar.Controls.Add(forwardButton);
            bookmarkButton = CreateButton(Localization.T("bookmark.off"), false, 95);
            bookmarkButton.Enabled = false;
            navigationBar.Controls.Add(bookmarkButton);

            navigationBar.Controls.Add(CreateLocalizedInlineLabel("position"));
            percentInput = CreateNumeric(88, 3, 100M);
            percentInput.Enabled = false;
            navigationBar.Controls.Add(percentInput);
            Label percentLabel = CreateInlineLabel("%");
            percentLabel.Margin = new Padding(1, 8, 1, 0);
            navigationBar.Controls.Add(percentLabel);
            Button goPercentButton = CreateLocalizedButton("go", false, 48);
            goPercentButton.Name = "goPercentButton";
            goPercentButton.Enabled = false;
            navigationBar.Controls.Add(goPercentButton);

            navigationBar.Controls.Add(CreateLocalizedInlineLabel("view"));
            displayModeCombo = CreateComboBox(145);
            RebuildDisplayModeOptions(ViewerDisplayMode.Normal);
            displayModeCombo.Enabled = false;
            navigationBar.Controls.Add(displayModeCombo);

            navigationBar.Controls.Add(CreateLocalizedInlineLabel("encoding"));
            encodingCombo = CreateComboBox(170);
            RebuildEncodingOptions(EncodingChoice.Automatic);
            encodingCombo.Enabled = false;
            navigationBar.Controls.Add(encodingCombo);

            navigationBar.Controls.Add(CreateLocalizedInlineLabel("language"));
            languageCombo = CreateComboBox(132);
            foreach (AppLanguage language in Localization.SupportedLanguages)
            {
                languageCombo.Items.Add(new LanguageOption(language, Localization.NativeLanguageName(language)));
            }
            SelectLanguageInCombo(Localization.CurrentLanguage);
            navigationBar.Controls.Add(languageCombo);

            Button fontDownButton = CreateButton("A−", false, 42);
            Button fontUpButton = CreateButton("A+", false, 42);
            navigationBar.Controls.Add(fontDownButton);
            navigationBar.Controls.Add(fontUpButton);

            FlowLayoutPanel searchBar = new FlowLayoutPanel();
            searchBar.Dock = DockStyle.Top;
            searchBar.Height = 50;
            searchBar.BackColor = PanelColor;
            searchBar.Padding = new Padding(12, 8, 8, 5);
            searchBar.WrapContents = false;
            searchBar.AutoScroll = true;

            searchBar.Controls.Add(CreateLocalizedInlineLabel("search.exact"));
            searchBox = new TextBox();
            searchBox.Width = 330;
            searchBox.MaxLength = 4096;
            searchBox.BackColor = ViewerColor;
            searchBox.ForeColor = PrimaryTextColor;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.Margin = new Padding(4, 2, 5, 0);
            searchBox.Enabled = false;
            searchBar.Controls.Add(searchBox);
            previousMatchButton = CreateLocalizedButton("search.previous", false, 88);
            previousMatchButton.Enabled = false;
            searchBar.Controls.Add(previousMatchButton);
            nextMatchButton = CreateLocalizedButton("search.next", true, 92);
            nextMatchButton.Enabled = false;
            searchBar.Controls.Add(nextMatchButton);
            fullSearchButton = CreateLocalizedButton("search.all", false, 105);
            fullSearchButton.Enabled = false;
            toolTip.SetToolTip(fullSearchButton, Localization.T("search.all.tip"));
            searchBar.Controls.Add(fullSearchButton);
            cancelButton = CreateLocalizedButton("cancel", false, 64);
            cancelButton.Enabled = false;
            searchBar.Controls.Add(cancelButton);
            operationProgress = new ProgressBar();
            operationProgress.Width = 145;
            operationProgress.Height = 16;
            operationProgress.Maximum = 1000;
            operationProgress.Margin = new Padding(8, 8, 5, 0);
            searchBar.Controls.Add(operationProgress);

            searchBar.Controls.Add(CreateLocalizedInlineLabel("go_line"));
            lineInput = CreateNumeric(115, 0, 999999999999M);
            lineInput.Minimum = 1;
            lineInput.Value = 1;
            lineInput.Enabled = false;
            searchBar.Controls.Add(lineInput);
            goLineButton = CreateLocalizedButton("go", false, 48);
            goLineButton.Enabled = false;
            searchBar.Controls.Add(goLineButton);
            indexButton = CreateButton(Localization.T("index.create"), false, 112);
            indexButton.Enabled = false;
            searchBar.Controls.Add(indexButton);

            SplitContainer mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Orientation = Orientation.Vertical;
            mainSplit.SplitterWidth = 5;
            mainSplit.SplitterDistance = 1080;
            mainSplit.Panel1.BackColor = WindowColor;
            mainSplit.Panel2.BackColor = PanelColor;
            mainSplit.Panel1.Padding = new Padding(12, 10, 4, 6);
            mainSplit.Panel2.Padding = new Padding(4, 10, 10, 6);

            Panel viewerBorder = new Panel();
            viewerBorder.Dock = DockStyle.Fill;
            viewerBorder.BackColor = BorderColor;
            viewerBorder.Padding = new Padding(1);
            lineViewer = new VirtualLineViewer();
            lineViewer.Dock = DockStyle.Fill;
            lineViewer.Font = new Font("Consolas", viewerFontSize);
            viewerBorder.Controls.Add(lineViewer);

            Panel positionPanel = new Panel();
            positionPanel.Dock = DockStyle.Bottom;
            positionPanel.Height = 34;
            positionPanel.BackColor = ViewerColor;
            positionPanel.Padding = new Padding(8, 2, 5, 0);
            Label positionCaption = new Label();
            positionCaption.Dock = DockStyle.Left;
            positionCaption.Width = 98;
            BindLocalization(positionCaption, "file_position");
            positionCaption.ForeColor = SecondaryTextColor;
            positionCaption.TextAlign = ContentAlignment.MiddleLeft;
            positionCaption.Font = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
            positionTrackBar = new TrackBar();
            positionTrackBar.Dock = DockStyle.Fill;
            positionTrackBar.Minimum = 0;
            positionTrackBar.Maximum = PositionUnits;
            positionTrackBar.TickStyle = TickStyle.None;
            positionTrackBar.Enabled = false;
            positionPanel.Controls.Add(positionTrackBar);
            positionPanel.Controls.Add(positionCaption);

            mainSplit.Panel1.Controls.Add(viewerBorder);
            mainSplit.Panel1.Controls.Add(positionPanel);

            TabControl sideTabs = new TabControl();
            sideTabs.Dock = DockStyle.Fill;
            sideTabs.Font = new Font("Segoe UI Semibold", 9.0f);
            TabPage resultsPage = CreateLocalizedTabPage("tab.search_results");
            TabPage bookmarksPage = CreateLocalizedTabPage("tab.bookmarks");
            TabPage performancePage = CreateLocalizedTabPage("tab.performance");
            sideTabs.TabPages.Add(resultsPage);
            sideTabs.TabPages.Add(bookmarksPage);
            sideTabs.TabPages.Add(performancePage);

            searchResultsCaption = CreateSideCaption(Localization.T("results.loaded.zero"));
            searchResultsCaption.Height = 46;
            searchResultsList = CreateDarkListBox();
            Panel searchResultsPager = new Panel();
            searchResultsPager.Dock = DockStyle.Bottom;
            searchResultsPager.Height = 42;
            searchResultsPager.BackColor = PanelColor;
            previousResultsPageButton = CreateButton("← 500", false, 72);
            nextResultsPageButton = CreateButton("500 →", false, 72);
            previousResultsPageButton.Location = new Point(6, 5);
            nextResultsPageButton.Location = new Point(84, 5);
            previousResultsPageButton.Enabled = false;
            nextResultsPageButton.Enabled = false;
            searchResultsPageLabel = new Label();
            searchResultsPageLabel.Location = new Point(162, 5);
            searchResultsPageLabel.Size = new Size(135, 28);
            searchResultsPageLabel.TextAlign = ContentAlignment.MiddleLeft;
            searchResultsPageLabel.ForeColor = SecondaryTextColor;
            searchResultsPageLabel.Text = Localization.T("results.local_view");
            searchResultsPager.Controls.Add(previousResultsPageButton);
            searchResultsPager.Controls.Add(nextResultsPageButton);
            searchResultsPager.Controls.Add(searchResultsPageLabel);
            resultsPage.Controls.Add(searchResultsList);
            resultsPage.Controls.Add(searchResultsPager);
            resultsPage.Controls.Add(searchResultsCaption);

            Panel bookmarkButtons = new Panel();
            bookmarkButtons.Dock = DockStyle.Bottom;
            bookmarkButtons.Height = 42;
            bookmarkButtons.BackColor = PanelColor;
            Button jumpBookmarkButton = CreateLocalizedButton("bookmark.jump", true, 95);
            Button removeBookmarkButton = CreateLocalizedButton("delete", false, 62);
            jumpBookmarkButton.Location = new Point(6, 5);
            removeBookmarkButton.Location = new Point(108, 5);
            bookmarkButtons.Controls.Add(jumpBookmarkButton);
            bookmarkButtons.Controls.Add(removeBookmarkButton);
            bookmarksList = CreateDarkListBox();
            bookmarksPage.Controls.Add(bookmarksList);
            bookmarksPage.Controls.Add(bookmarkButtons);

            Panel performancePanel = new Panel();
            performancePanel.Dock = DockStyle.Fill;
            performancePanel.BackColor = PanelColor;
            performancePanel.Padding = new Padding(12);
            performanceSpeedLabel = CreateMetricLabel("metric.speed", "—", 8);
            performanceEtaLabel = CreateMetricLabel("metric.eta", "—", 62);
            performanceScannedLabel = CreateMetricLabel("metric.scanned", "—", 116);
            performanceBottleneckLabel = CreateMetricLabel("metric.bottleneck", "—", 170);
            performanceReadLabel = CreateMetricLabel("metric.read_compare", "—", 224);
            indexInfoLabel = CreateMetricLabel("metric.index", Localization.T("none"), 295);
            performancePanel.Controls.Add(performanceSpeedLabel);
            performancePanel.Controls.Add(performanceEtaLabel);
            performancePanel.Controls.Add(performanceScannedLabel);
            performancePanel.Controls.Add(performanceBottleneckLabel);
            performancePanel.Controls.Add(performanceReadLabel);
            performancePanel.Controls.Add(indexInfoLabel);
            performancePage.Controls.Add(performancePanel);
            mainSplit.Panel2.Controls.Add(sideTabs);

            statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Bottom;
            statusStrip.BackColor = Color.FromArgb(24, 28, 36);
            statusStrip.ForeColor = SecondaryTextColor;
            statusStrip.SizingGrip = false;
            statusMessage = new ToolStripStatusLabel();
            statusMessage.Spring = true;
            statusMessage.TextAlign = ContentAlignment.MiddleLeft;
            statusMessage.Text = Localization.T("ready");
            statusPosition = new ToolStripStatusLabel(Localization.T("status.position.empty"));
            statusPosition.BorderSides = ToolStripStatusLabelBorderSides.Left;
            statusEncoding = new ToolStripStatusLabel(Localization.T("status.encoding.empty"));
            statusEncoding.BorderSides = ToolStripStatusLabelBorderSides.Left;
            statusStrip.Items.Add(statusMessage);
            statusStrip.Items.Add(statusPosition);
            statusStrip.Items.Add(statusEncoding);

            Controls.Add(mainSplit);
            Controls.Add(searchBar);
            Controls.Add(navigationBar);
            Controls.Add(header);
            Controls.Add(statusStrip);

            goPercentButton.Click += delegate { JumpToPercent(); };
            jumpBookmarkButton.Click += delegate { JumpToSelectedBookmark(); };
            removeBookmarkButton.Click += delegate { RemoveSelectedBookmark(); };
            fontDownButton.Click += delegate { ChangeViewerFont(-1.0f); };
            fontUpButton.Click += delegate { ChangeViewerFont(1.0f); };

            positionTimer = new System.Windows.Forms.Timer();
            positionTimer.Interval = 220;
            positionTimer.Tick += delegate { positionTimer.Stop(); JumpFromTrackBar(); };
            sessionTimer = new System.Windows.Forms.Timer();
            sessionTimer.Interval = 900;
            sessionTimer.Tick += delegate { sessionTimer.Stop(); SaveCurrentSession(); };
        }

        private Button CreateButton(string text, bool accent, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 31;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = accent ? AccentColor : BorderColor;
            button.FlatAppearance.MouseOverBackColor = accent ? AccentHoverColor : Color.FromArgb(43, 50, 63);
            button.FlatAppearance.MouseDownBackColor = accent ? Color.FromArgb(64, 118, 207) : Color.FromArgb(36, 42, 54);
            button.BackColor = accent ? AccentColor : PanelColor;
            button.ForeColor = PrimaryTextColor;
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(3, 1, 3, 0);
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Button CreateLocalizedButton(string key, bool accent, int width)
        {
            Button button = CreateButton(Localization.T(key), accent, width);
            BindLocalization(button, key);
            return button;
        }

        private Label CreateInlineLabel(string text)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = text;
            label.ForeColor = SecondaryTextColor;
            label.Margin = new Padding(8, 8, 0, 0);
            return label;
        }

        private Label CreateLocalizedInlineLabel(string key)
        {
            Label label = CreateInlineLabel(Localization.T(key));
            BindLocalization(label, key);
            return label;
        }

        private NumericUpDown CreateNumeric(int width, int decimals, decimal maximum)
        {
            NumericUpDown numeric = new NumericUpDown();
            numeric.Width = width;
            numeric.DecimalPlaces = decimals;
            numeric.Maximum = maximum;
            numeric.BackColor = PanelColor;
            numeric.ForeColor = PrimaryTextColor;
            numeric.BorderStyle = BorderStyle.FixedSingle;
            numeric.TextAlign = HorizontalAlignment.Right;
            numeric.Margin = new Padding(4, 2, 1, 0);
            return numeric;
        }

        private ComboBox CreateComboBox(int width)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Width = width;
            combo.BackColor = PanelColor;
            combo.ForeColor = PrimaryTextColor;
            combo.Margin = new Padding(4, 3, 4, 0);
            return combo;
        }

        private TabPage CreateTabPage(string text)
        {
            TabPage page = new TabPage(text);
            page.BackColor = PanelColor;
            page.ForeColor = PrimaryTextColor;
            page.Padding = new Padding(7);
            return page;
        }

        private TabPage CreateLocalizedTabPage(string key)
        {
            TabPage page = CreateTabPage(Localization.T(key));
            BindLocalization(page, key);
            return page;
        }

        private ListBox CreateDarkListBox()
        {
            ListBox list = new ListBox();
            list.Dock = DockStyle.Fill;
            list.BackColor = ViewerColor;
            list.ForeColor = PrimaryTextColor;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = new Font("Consolas", 9.0f);
            list.IntegralHeight = false;
            return list;
        }

        private Label CreateSideCaption(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Top;
            label.Height = 32;
            label.Text = text;
            label.ForeColor = SecondaryTextColor;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private Label CreateMetricLabel(string nameKey, string value, int top)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Location = new Point(12, top);
            label.Size = new Size(285, 47);
            label.Text = MetricText(nameKey, value);
            label.ForeColor = PrimaryTextColor;
            label.Font = new Font("Segoe UI", 9.5f);
            return label;
        }

        private static string MetricText(string nameKey, string value)
        {
            return Localization.T(nameKey).ToUpperInvariant() + "\r\n" + value;
        }

        private static void BindLocalization(Control control, string key)
        {
            control.Tag = "loc:" + key;
            control.Text = Localization.T(key);
        }

        private static void ApplyLocalizedControlTexts(Control parent)
        {
            string tag = parent.Tag as string;
            if (!string.IsNullOrEmpty(tag) && tag.StartsWith("loc:", StringComparison.Ordinal))
            {
                parent.Text = Localization.T(tag.Substring(4));
            }
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                ApplyLocalizedControlTexts(parent.Controls[i]);
            }
        }

        private void WireEvents()
        {
            Shown += async delegate
            {
                if (!string.IsNullOrEmpty(initialFile)) await OpenFileAsync(initialFile);
            };
            openButton.Click += async delegate { await ChooseFileAsync(); };
            backButton.Click += delegate { NavigateHistory(false); };
            forwardButton.Click += delegate { NavigateHistory(true); };
            bookmarkButton.Click += delegate { ToggleSelectedBookmark(); };
            previousMatchButton.Click += delegate { StartSearch(false); };
            nextMatchButton.Click += delegate { StartSearch(true); };
            fullSearchButton.Click += delegate { StartFullSearch(); };
            cancelButton.Click += delegate { CancelActiveOperation(); };
            indexButton.Click += delegate { ToggleIndexBuild(); };
            goLineButton.Click += delegate { GoToLine(); };

            searchBox.TextChanged += delegate
            {
                ClearFullSearchResults();
                if (!string.Equals(searchBox.Text, currentMatchQuery, StringComparison.Ordinal)) currentMatchOffset = -1;
                lineViewer.SearchText = searchBox.Text;
                UpdateLocalSearchResults();
                UpdateSearchControls();
            };
            searchBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    StartSearch(!e.Shift);
                    e.SuppressKeyPress = true;
                }
            };
            percentInput.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    JumpToPercent();
                    e.SuppressKeyPress = true;
                }
            };
            lineInput.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    GoToLine();
                    e.SuppressKeyPress = true;
                }
            };

            encodingCombo.SelectionChangeCommitted += delegate { ChangeEncoding(); };
            displayModeCombo.SelectionChangeCommitted += delegate { ChangeDisplayMode(); };
            languageCombo.SelectionChangeCommitted += delegate { ChangeLanguage(); };
            positionTrackBar.Scroll += delegate
            {
                if (suppressUiEvents || string.IsNullOrEmpty(currentPath)) return;
                positionTimer.Stop();
                positionTimer.Start();
                decimal percent = (decimal)positionTrackBar.Value * 100M / PositionUnits;
                statusMessage.Text = Localization.Format("status.selected_position", percent);
            };
            positionTrackBar.MouseUp += delegate
            {
                if (suppressUiEvents || string.IsNullOrEmpty(currentPath)) return;
                positionTimer.Stop();
                JumpFromTrackBar();
            };

            lineViewer.ChunkRequested += delegate(object sender, ChunkRequestEventArgs e)
            {
                LoadAdjacentChunk(e.Forward);
            };
            lineViewer.SelectedLineChanged += delegate(object sender, ViewerLineEventArgs e)
            {
                UpdateSelectionUi(e.Line);
            };
            lineViewer.BookmarkToggleRequested += delegate(object sender, ViewerLineEventArgs e)
            {
                ToggleBookmark(e.Line);
            };
            lineViewer.LineActivated += delegate(object sender, ViewerLineEventArgs e)
            {
                if (!string.IsNullOrEmpty(currentPath) && currentEncoding != null)
                {
                    using (LongLineForm dialog = new LongLineForm(currentPath, e.Line.ByteOffset, currentEncoding, displayMode))
                    {
                        dialog.ShowDialog(this);
                    }
                }
            };

            searchResultsList.DoubleClick += async delegate { await JumpToSelectedSearchResultAsync(); };
            searchResultsList.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    await JumpToSelectedSearchResultAsync();
                    e.SuppressKeyPress = true;
                }
            };
            previousResultsPageButton.Click += delegate { ShowFullSearchResultsPage(fullSearchResultPage - 1); };
            nextResultsPageButton.Click += delegate { ShowFullSearchResultsPage(fullSearchResultPage + 1); };
            bookmarksList.DoubleClick += delegate { JumpToSelectedBookmark(); };

            DragEnter += delegate(object sender, DragEventArgs e)
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += async delegate(object sender, DragEventArgs e)
            {
                string[] files = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && File.Exists(files[0])) await OpenFileAsync(files[0]);
            };
            FormClosing += delegate
            {
                if (lineViewer != null && currentSession != null) currentSession.LastOffset = lineViewer.TopByteOffset;
                SaveCurrentSession();
                CancelAllOperations();
                ClearFullSearchResults();
            };
            KeyDown += MainFormKeyDown;
        }

        private async Task ChooseFileAsync()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = Localization.T("dialog.open.title");
                dialog.Filter = Localization.T("dialog.open.filter");
                dialog.CheckFileExists = true;
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) await OpenFileAsync(dialog.FileName);
            }
        }

        private async Task OpenFileAsync(string path)
        {
            CancelAllOperations();
            ClearFullSearchResults();
            SaveCurrentSession();
            SetFileControlsEnabled(false);
            chunks.Clear();
            lineViewer.SetLines(new List<ViewerLine>());
            searchResultsList.Items.Clear();
            bookmarksList.Items.Clear();
            currentMatchOffset = -1;
            currentMatchQuery = null;
            statusMessage.Text = Localization.T("status.loading_file");

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath)) throw new FileNotFoundException(Localization.T("error.file_not_found"), fullPath);

                ViewerFileSession session = ViewerSessionStore.Load(fullPath);
                currentSession = session;
                SetEncodingCombo(session.SelectedEncoding);
                SetDisplayModeCombo(session.DisplayMode);
                displayMode = session.DisplayMode;
                EncodingChoice requested = session.SelectedEncoding;
                ResolvedEncoding resolved = await Task.Run(delegate { return EncodingResolver.Resolve(fullPath, requested); });

                currentPath = fullPath;
                currentEncoding = resolved;
                navigationIndex = NavigationIndexStore.TryLoad(fullPath, resolved);
                lineViewer.BookmarkPredicate = delegate(ViewerLine line)
                {
                    return currentSession != null && currentSession.IsBookmarked(line.ByteOffset);
                };
                fileNameLabel.Text = Path.GetFileName(fullPath);
                filePathLabel.Text = fullPath;
                toolTip.SetToolTip(filePathLabel, fullPath);
                Text = Path.GetFileName(fullPath) + " — " + Localization.T("window.title");
                RefreshBookmarksList();
                UpdateIndexUi();
                SetFileControlsEnabled(true);

                long initialOffset = Math.Max(0, Math.Min(session.LastOffset, new FileInfo(fullPath).Length));
                navigationHistory.Reset(initialOffset);
                await LoadWindowAsync(initialOffset, true, initialOffset, false);
            }
            catch (Exception exception)
            {
                currentPath = null;
                currentEncoding = null;
                currentSession = null;
                navigationIndex = null;
                statusMessage.Text = Localization.T("status.file_open_failed");
                SetFileControlsEnabled(false);
                MessageBox.Show(this, Localization.Format("error.file_open_failed", exception.Message), "Wordlist Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadWindowAsync(long offset, bool alignToLine, long? selectOffset, bool recordHistory)
        {
            if (string.IsNullOrEmpty(currentPath) || currentEncoding == null) return;
            if (pageCancellation != null)
            {
                pageCancellation.Cancel();
                pageCancellation.Dispose();
            }
            pageCancellation = new CancellationTokenSource();
            CancellationToken token = pageCancellation.Token;
            int generation = ++pageGeneration;
            string path = currentPath;
            ResolvedEncoding encoding = currentEncoding;
            ViewerDisplayMode requestedMode = displayMode;
            statusMessage.Text = Localization.T("status.reading_view");
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                PageData page = await Task.Run(delegate
                {
                    return HugeFileReader.ReadPage(path, offset, alignToLine, encoding, token);
                }, token);
                if (token.IsCancellationRequested || generation != pageGeneration || !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;

                long firstLine = await ResolveFirstLineNumber(page, requestedMode, token);
                List<ViewerLine> lines = await Task.Run(delegate
                {
                    return ViewerLineBuilder.Build(page, encoding, requestedMode, firstLine);
                }, token);
                if (token.IsCancellationRequested || generation != pageGeneration) return;

                ViewerChunk chunk = new ViewerChunk();
                chunk.Page = page;
                chunk.Lines = lines;
                chunk.FirstLineNumber = firstLine;
                chunks.Clear();
                chunks.Add(chunk);
                lineViewer.SetLines(lines);
                long target = selectOffset.HasValue ? selectOffset.Value : offset;
                lineViewer.ScrollToOffset(target, selectOffset.HasValue);
                lineViewer.SearchText = searchBox.Text;
                lineViewer.ActiveMatchOffset = currentMatchOffset;
                if (recordHistory) navigationHistory.Record(target);
                stopwatch.Stop();
                UpdateLoadedWindowUi(page, stopwatch.Elapsed.TotalMilliseconds);
                UpdateLocalSearchResults();
                ScheduleSessionSave();
                UpdateHistoryButtons();

                PrefetchInitialNeighbors();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (generation == pageGeneration) ShowOperationError(Localization.T("error.read_view"), exception);
            }
        }

        private async Task<long> ResolveFirstLineNumber(PageData page, ViewerDisplayMode mode, CancellationToken token)
        {
            if (mode == ViewerDisplayMode.Hex) return page.ContentOffset / 16L + 1L;
            NavigationIndexData index = navigationIndex;
            if (index == null) return 1;
            string path = currentPath;
            ResolvedEncoding encoding = currentEncoding;
            return await Task.Run(delegate
            {
                return NavigationIndexStore.FindLineNumberAtOffset(path, page.ContentOffset, encoding, index, token);
            }, token);
        }

        private async void PrefetchInitialNeighbors()
        {
            await Task.Yield();
            if (chunks.Count == 0) return;
            if (chunks[0].Page.EndOffset < chunks[0].Page.FileLength) await LoadAdjacentChunkAsync(true);
            if (chunks.Count < MaximumChunks && chunks[0].Page.StartOffset > 0) await LoadAdjacentChunkAsync(false);
        }

        private async void LoadAdjacentChunk(bool forward)
        {
            await LoadAdjacentChunkAsync(forward);
        }

        private async Task LoadAdjacentChunkAsync(bool forward)
        {
            if (adjacentLoadRunning || chunks.Count == 0 || string.IsNullOrEmpty(currentPath) || currentEncoding == null) return;
            ViewerChunk edge = forward ? chunks[chunks.Count - 1] : chunks[0];
            if (forward && edge.Page.EndOffset >= edge.Page.FileLength) return;
            if (!forward && edge.Page.StartOffset <= 0) return;

            adjacentLoadRunning = true;
            long topOffset = lineViewer.CaptureTopByteOffset();
            try
            {
                long requestedOffset = forward
                    ? edge.Page.EndOffset
                    : Math.Max(0, edge.Page.StartOffset - HugeFileReader.PageBytes);
                bool align = forward ? !edge.Page.TruncatedAtEnd : !edge.Page.TruncatedAtStart;
                string path = currentPath;
                ResolvedEncoding encoding = currentEncoding;
                ViewerDisplayMode mode = displayMode;
                int generation = pageGeneration;
                PageData page = await Task.Run(delegate
                {
                    return HugeFileReader.ReadPage(path, requestedOffset, align, encoding, CancellationToken.None);
                });
                if (generation != pageGeneration || !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;
                if (chunks.Exists(delegate(ViewerChunk item) { return item.Page.StartOffset == page.StartOffset; })) return;

                long firstLine = await ResolveFirstLineNumber(page, mode, CancellationToken.None);
                List<ViewerLine> lines = await Task.Run(delegate { return ViewerLineBuilder.Build(page, encoding, mode, firstLine); });
                ViewerChunk chunk = new ViewerChunk();
                chunk.Page = page;
                chunk.Lines = lines;
                chunk.FirstLineNumber = firstLine;
                chunks.Add(chunk);
                chunks.Sort(delegate(ViewerChunk left, ViewerChunk right) { return left.Page.StartOffset.CompareTo(right.Page.StartOffset); });
                while (chunks.Count > MaximumChunks)
                {
                    chunks.RemoveAt(forward ? 0 : chunks.Count - 1);
                }
                RebuildCombinedLines(topOffset, false);
                UpdateLoadedRangeStatus();
                statusMessage.Text = Localization.Format("status.cache_ready", lineViewer.Lines.Count, MaximumChunks);
            }
            catch (Exception exception)
            {
                statusMessage.Text = Localization.Format("status.prefetch_stopped", exception.Message);
            }
            finally
            {
                adjacentLoadRunning = false;
            }
        }

        private void RebuildCombinedLines(long anchorOffset, bool selectAnchor)
        {
            List<ViewerLine> combined = new List<ViewerLine>();
            long lastEnd = -1;
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                List<ViewerLine> source = chunks[chunkIndex].Lines;
                for (int i = 0; i < source.Count; i++)
                {
                    ViewerLine line = source[i];
                    if (line.ByteOffset < lastEnd) continue;
                    combined.Add(line);
                    lastEnd = line.ByteOffset + Math.Max(1, line.ByteLength);
                }
            }
            if (navigationIndex == null)
            {
                for (int i = 0; i < combined.Count; i++) combined[i].LocalLineNumber = i + 1;
            }
            lineViewer.SetLines(combined);
            lineViewer.ScrollToOffset(anchorOffset, selectAnchor);
            lineViewer.SearchText = searchBox.Text;
            lineViewer.ActiveMatchOffset = currentMatchOffset;
            UpdateLocalSearchResults();
        }

        private async void StartSearch(bool forward)
        {
            if (searchRunning || indexRunning || string.IsNullOrEmpty(currentPath) || currentEncoding == null || string.IsNullOrEmpty(searchBox.Text)) return;
            string query = searchBox.Text;
            byte[] pattern = currentEncoding.Encoding.GetBytes(query);
            if (pattern.Length == 0) return;

            ViewerLine selected = lineViewer.SelectedLine;
            long currentOffset = selected == null ? lineViewer.TopByteOffset : selected.ByteOffset;
            bool continuing = currentMatchOffset >= 0 && string.Equals(currentMatchQuery, query, StringComparison.Ordinal);
            long startOffset;
            if (forward)
            {
                startOffset = continuing ? currentMatchOffset + Math.Max(1, pattern.Length) : currentOffset;
            }
            else
            {
                startOffset = continuing ? currentMatchOffset : currentOffset;
            }

            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;
            searchRunning = true;
            cancelButton.Enabled = true;
            operationProgress.Value = 0;
            UpdateSearchControls();
            statusMessage.Text = Localization.T(forward ? "status.search_forward" : "status.search_backward");

            Progress<SearchProgress> progress = new Progress<SearchProgress>(delegate(SearchProgress value)
            {
                if (!searchRunning) return;
                int progressValue = value.TotalBytes <= 0 ? 0 : (int)Math.Min(1000, value.ScannedBytes * 1000.0 / value.TotalBytes);
                operationProgress.Value = Math.Max(0, Math.Min(1000, progressValue));
                performanceSpeedLabel.Text = MetricText("metric.speed", FormatBytes((long)value.BytesPerSecond) + Localization.T("unit.per_second"));
                performanceEtaLabel.Text = MetricText("metric.eta", FormatDuration(value.EstimatedRemainingSeconds));
                performanceScannedLabel.Text = MetricText("metric.scanned", string.Format("{0} / {1}  ({2:N1}%)",
                    FormatBytes(value.ScannedBytes), FormatBytes(value.TotalBytes), value.TotalBytes <= 0 ? 0 : value.ScannedBytes * 100.0 / value.TotalBytes));
                performanceBottleneckLabel.Text = MetricText("metric.bottleneck", Localization.BottleneckName(value.Bottleneck));
                performanceReadLabel.Text = MetricText("metric.read_compare", Localization.Format("metric.read_compare.value", value.ReadSeconds, value.CompareSeconds));
                statusMessage.Text = Localization.Format("status.searching",
                    value.TotalBytes <= 0 ? 0 : value.ScannedBytes * 100.0 / value.TotalBytes,
                    FormatBytes((long)value.BytesPerSecond),
                    FormatDuration(value.EstimatedRemainingSeconds));
            });

            try
            {
                string path = currentPath;
                int alignment = currentEncoding.UnitSize;
                long found = await Task.Run(delegate
                {
                    return forward
                        ? ByteSearch.FindForward(path, pattern, startOffset, token, alignment, progress)
                        : ByteSearch.FindBackward(path, pattern, startOffset, token, alignment, progress);
                }, token);
                if (token.IsCancellationRequested) return;
                if (found < 0)
                {
                    statusMessage.Text = Localization.T(forward ? "status.no_match_forward" : "status.no_match_backward");
                    System.Media.SystemSounds.Beep.Play();
                    return;
                }

                currentMatchOffset = found;
                currentMatchQuery = query;
                await LoadWindowAsync(found, true, found, true);
                SelectActiveSearchResult(found);
                statusMessage.Text = Localization.Format("status.match_found", found);
            }
            catch (OperationCanceledException)
            {
                statusMessage.Text = Localization.T("status.search_cancelled");
            }
            catch (Exception exception)
            {
                ShowOperationError(Localization.T("error.search"), exception);
            }
            finally
            {
                searchRunning = false;
                cancelButton.Enabled = indexRunning;
                operationProgress.Value = 0;
                UpdateSearchControls();
                if (searchCancellation != null)
                {
                    searchCancellation.Dispose();
                    searchCancellation = null;
                }
            }
        }

        private async void StartFullSearch()
        {
            if (searchRunning || indexRunning || string.IsNullOrEmpty(currentPath) || currentEncoding == null || string.IsNullOrEmpty(searchBox.Text)) return;
            string query = searchBox.Text;
            byte[] pattern = currentEncoding.Encoding.GetBytes(query);
            if (pattern.Length == 0) return;

            string path = currentPath;
            int alignment = currentEncoding.UnitSize;
            long startOffset = currentEncoding.BomLength;
            FileInfo sourceBefore = new FileInfo(path);
            long sourceLength = sourceBefore.Length;
            long sourceWriteTicks = sourceBefore.LastWriteTimeUtc.Ticks;
            FullSearchResultStore pendingResults = null;

            ClearFullSearchResults();
            searchResultsList.Items.Clear();
            searchResultsCaption.Text = Localization.T("results.scan_initial");
            searchResultsPageLabel.Text = Localization.T("results.scan_running");
            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;
            searchRunning = true;
            cancelButton.Enabled = true;
            operationProgress.Value = 0;
            UpdateSearchControls();
            statusMessage.Text = Localization.T("status.scan_all_start");

            Progress<SearchProgress> progress = new Progress<SearchProgress>(delegate(SearchProgress value)
            {
                if (!searchRunning || !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;
                int progressValue = value.TotalBytes <= 0 ? 0 : (int)Math.Min(1000, value.ScannedBytes * 1000.0 / value.TotalBytes);
                operationProgress.Value = Math.Max(0, Math.Min(1000, progressValue));
                performanceSpeedLabel.Text = MetricText("metric.speed", FormatBytes((long)value.BytesPerSecond) + Localization.T("unit.per_second"));
                performanceEtaLabel.Text = MetricText("metric.eta", FormatDuration(value.EstimatedRemainingSeconds));
                performanceScannedLabel.Text = Localization.Format("metric.scanned_matches",
                    FormatBytes(value.ScannedBytes), FormatBytes(value.TotalBytes), value.TotalBytes <= 0 ? 0 : value.ScannedBytes * 100.0 / value.TotalBytes, value.MatchCount);
                performanceBottleneckLabel.Text = MetricText("metric.bottleneck", Localization.BottleneckName(value.Bottleneck));
                performanceReadLabel.Text = MetricText("metric.read_compare", Localization.Format("metric.read_compare.value", value.ReadSeconds, value.CompareSeconds));
                searchResultsCaption.Text = Localization.Format("results.scan_progress", value.MatchCount);
                statusMessage.Text = Localization.Format("status.scan_progress",
                    value.TotalBytes <= 0 ? 0 : value.ScannedBytes * 100.0 / value.TotalBytes,
                    value.MatchCount,
                    FormatBytes((long)value.BytesPerSecond),
                    FormatDuration(value.EstimatedRemainingSeconds));
            });

            try
            {
                pendingResults = FullSearchResultStore.Create();
                FullSearchResultStore storeForWorker = pendingResults;
                long matchCount = await Task.Run(delegate
                {
                    long count = ByteSearch.ScanAll(path, pattern, startOffset, token, alignment, progress, storeForWorker.Append);
                    token.ThrowIfCancellationRequested();
                    storeForWorker.Complete();
                    return count;
                }, token);

                if (token.IsCancellationRequested) return;
                if (!string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;

                FileInfo sourceAfter = new FileInfo(path);
                if (sourceAfter.Length != sourceLength || sourceAfter.LastWriteTimeUtc.Ticks != sourceWriteTicks)
                {
                    throw new IOException(Localization.T("error.source_changed"));
                }

                fullSearchResults = pendingResults;
                pendingResults = null;
                fullSearchResultPage = 0;
                fullSearchQuery = query;
                fullSearchPath = path;
                currentMatchOffset = -1;
                currentMatchQuery = query;
                ShowFullSearchResultsPage(0);
                operationProgress.Value = 1000;
                statusMessage.Text = Localization.Format("status.scan_complete",
                    matchCount, FormatBytes(fullSearchResults.StorageBytes));
                if (matchCount == 0) System.Media.SystemSounds.Beep.Play();
            }
            catch (OperationCanceledException)
            {
                statusMessage.Text = Localization.T("status.scan_cancelled");
                UpdateLocalSearchResults();
            }
            catch (Exception exception)
            {
                UpdateLocalSearchResults();
                ShowOperationError(Localization.T("error.scan_all"), exception);
            }
            finally
            {
                if (pendingResults != null) pendingResults.Dispose();
                searchRunning = false;
                cancelButton.Enabled = indexRunning;
                if (fullSearchResults == null) operationProgress.Value = 0;
                UpdateSearchControls();
                if (searchCancellation != null)
                {
                    searchCancellation.Dispose();
                    searchCancellation = null;
                }
            }
        }

        private bool HasFullSearchResults()
        {
            return fullSearchResults != null
                && string.Equals(fullSearchQuery, searchBox.Text, StringComparison.Ordinal)
                && string.Equals(fullSearchPath, currentPath, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearFullSearchResults()
        {
            if (fullSearchResults != null)
            {
                fullSearchResults.Dispose();
                fullSearchResults = null;
            }
            fullSearchResultPage = 0;
            fullSearchQuery = null;
            fullSearchPath = null;
            if (previousResultsPageButton != null) previousResultsPageButton.Enabled = false;
            if (nextResultsPageButton != null) nextResultsPageButton.Enabled = false;
            if (searchResultsPageLabel != null) searchResultsPageLabel.Text = Localization.T("results.local_view");
        }

        private void ShowFullSearchResultsPage(long requestedPage)
        {
            if (!HasFullSearchResults()) return;
            long count = fullSearchResults.Count;
            long pageCount = count <= 0 ? 1 : 1 + (count - 1) / FullSearchResultsPerPage;
            long page = Math.Max(0, Math.Min(requestedPage, pageCount - 1));
            long firstIndex = page * FullSearchResultsPerPage;
            int requested = (int)Math.Min((long)FullSearchResultsPerPage, Math.Max(0, count - firstIndex));
            long[] offsets;
            try
            {
                offsets = fullSearchResults.ReadRange(firstIndex, requested);
            }
            catch (Exception exception)
            {
                ShowOperationError(Localization.T("error.results_read"), exception);
                ClearFullSearchResults();
                UpdateLocalSearchResults();
                return;
            }

            searchResultsList.BeginUpdate();
            try
            {
                searchResultsList.Items.Clear();
                for (int i = 0; i < offsets.Length; i++)
                {
                    SearchResultListItem item = new SearchResultListItem();
                    item.IsFullFileResult = true;
                    item.MatchNumber = firstIndex + i + 1;
                    item.MatchOffset = offsets[i];
                    searchResultsList.Items.Add(item);
                }
            }
            finally
            {
                searchResultsList.EndUpdate();
            }

            fullSearchResultPage = page;
            long shownEnd = firstIndex + offsets.Length;
            searchResultsCaption.Text = Localization.Format("results.full",
                count, count == 0 ? 0 : firstIndex + 1, shownEnd);
            searchResultsPageLabel.Text = string.Format("{0:N0} / {1:N0}", page + 1, pageCount);
            previousResultsPageButton.Enabled = page > 0;
            nextResultsPageButton.Enabled = page + 1 < pageCount;
        }

        private void UpdateLocalSearchResults()
        {
            if (HasFullSearchResults()) return;
            previousResultsPageButton.Enabled = false;
            nextResultsPageButton.Enabled = false;
            searchResultsPageLabel.Text = Localization.T("results.local_view");
            searchResultsList.BeginUpdate();
            try
            {
                searchResultsList.Items.Clear();
                string query = searchBox.Text;
                if (string.IsNullOrEmpty(query))
                {
                    searchResultsCaption.Text = Localization.T("results.loaded.zero");
                    return;
                }

                StringComparison comparison = StringComparison.Ordinal;
                IList<ViewerLine> lines = lineViewer.Lines;
                for (int i = 0; i < lines.Count && searchResultsList.Items.Count < MaximumLocalSearchResults; i++)
                {
                    string text = lines[i].Text ?? string.Empty;
                    int start = 0;
                    while (start <= text.Length - query.Length && searchResultsList.Items.Count < MaximumLocalSearchResults)
                    {
                        int found = text.IndexOf(query, start, comparison);
                        if (found < 0) break;
                        SearchResultListItem item = new SearchResultListItem();
                        item.Line = lines[i];
                        item.CharacterIndex = found;
                        searchResultsList.Items.Add(item);
                        start = found + Math.Max(1, query.Length);
                    }
                }
                searchResultsCaption.Text = Localization.Format("results.loaded.count",
                    searchResultsList.Items.Count,
                    searchResultsList.Items.Count >= MaximumLocalSearchResults ? "+" : string.Empty);
            }
            finally
            {
                searchResultsList.EndUpdate();
            }
        }

        private async Task JumpToSelectedSearchResultAsync()
        {
            SearchResultListItem item = searchResultsList.SelectedItem as SearchResultListItem;
            if (item == null) return;

            if (item.IsFullFileResult)
            {
                currentMatchOffset = item.MatchOffset;
                currentMatchQuery = searchBox.Text;
                await LoadWindowAsync(item.MatchOffset, true, item.MatchOffset, true);
                SelectActiveSearchResult(item.MatchOffset);
                statusMessage.Text = Localization.Format("status.result", item.MatchNumber, item.MatchOffset);
                return;
            }

            if (item.Line == null) return;
            lineViewer.ScrollToOffset(item.Line.ByteOffset, true);
            lineViewer.ActiveMatchOffset = item.Line.ByteOffset;
            lineViewer.ActiveMatchCharIndex = item.CharacterIndex;
            long matchOffset = item.Line.ByteOffset;
            if (currentEncoding != null && item.CharacterIndex > 0 && item.CharacterIndex <= (item.Line.Text ?? string.Empty).Length)
            {
                matchOffset += currentEncoding.Encoding.GetByteCount(item.Line.Text.Substring(0, item.CharacterIndex));
            }
            currentMatchOffset = matchOffset;
            currentMatchQuery = searchBox.Text;
            navigationHistory.Record(item.Line.ByteOffset);
            UpdateHistoryButtons();
        }

        private void SelectActiveSearchResult(long offset)
        {
            lineViewer.ActiveMatchOffset = offset;
            int lineIndex = lineViewer.ActiveMatchLineIndex;
            if (lineIndex >= 0 && lineIndex < lineViewer.Lines.Count)
            {
                ViewerLine line = lineViewer.Lines[lineIndex];
                string text = line.Text ?? string.Empty;
                string query = searchBox.Text;
                int character = text.IndexOf(query, StringComparison.Ordinal);
                if (character >= 0 && currentEncoding != null && offset >= line.ByteOffset)
                {
                    long targetByte = offset - line.ByteOffset;
                    int bestCharacter = character;
                    long bestDistance = long.MaxValue;
                    int next = 0;
                    while (next <= text.Length - query.Length)
                    {
                        int candidate = text.IndexOf(query, next, StringComparison.Ordinal);
                        if (candidate < 0) break;
                        long candidateByte = currentEncoding.Encoding.GetByteCount(text.Substring(0, candidate));
                        long distance = Math.Abs(candidateByte - targetByte);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestCharacter = candidate;
                        }
                        if (candidateByte >= targetByte) break;
                        next = candidate + Math.Max(1, query.Length);
                    }
                    character = bestCharacter;
                }
                lineViewer.ActiveMatchCharIndex = Math.Max(0, character);
            }
        }

        private void ChangeDisplayMode()
        {
            if (suppressUiEvents || chunks.Count == 0) return;
            DisplayModeOption option = displayModeCombo.SelectedItem as DisplayModeOption;
            if (option == null || option.Mode == displayMode) return;
            long anchor = lineViewer.TopByteOffset;
            displayMode = option.Mode;
            if (currentSession != null) currentSession.DisplayMode = displayMode;
            for (int i = 0; i < chunks.Count; i++)
            {
                long first = displayMode == ViewerDisplayMode.Hex
                    ? chunks[i].Page.ContentOffset / 16L + 1L
                    : chunks[i].FirstLineNumber;
                chunks[i].Lines = ViewerLineBuilder.Build(chunks[i].Page, currentEncoding, displayMode, first);
            }
            RebuildCombinedLines(anchor, false);
            statusMessage.Text = Localization.Format("status.view", option.Name);
            if (currentEncoding != null) statusEncoding.Text = Localization.Format("status.encoding", Localization.EncodingName(currentEncoding.Choice), DisplayModeName(displayMode));
            ScheduleSessionSave();
        }

        private async void ChangeEncoding()
        {
            if (suppressUiEvents || string.IsNullOrEmpty(currentPath)) return;
            EncodingOptionV2 option = encodingCombo.SelectedItem as EncodingOptionV2;
            if (option == null) return;
            ClearFullSearchResults();
            UpdateLocalSearchResults();
            long offset = lineViewer.TopByteOffset;
            try
            {
                statusMessage.Text = Localization.T("status.encoding_changing");
                string path = currentPath;
                ResolvedEncoding resolved = await Task.Run(delegate { return EncodingResolver.Resolve(path, option.Choice); });
                if (!string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;
                currentEncoding = resolved;
                navigationIndex = NavigationIndexStore.TryLoad(path, resolved);
                if (currentSession != null) currentSession.SelectedEncoding = option.Choice;
                UpdateIndexUi();
                await LoadWindowAsync(offset, true, offset, false);
                ScheduleSessionSave();
            }
            catch (Exception exception)
            {
                ShowOperationError(Localization.T("error.encoding_change"), exception);
            }
        }

        private async void ToggleIndexBuild()
        {
            if (indexRunning)
            {
                if (indexCancellation != null) indexCancellation.Cancel();
                return;
            }
            if (searchRunning || string.IsNullOrEmpty(currentPath) || currentEncoding == null) return;

            indexCancellation = new CancellationTokenSource();
            CancellationToken token = indexCancellation.Token;
            indexRunning = true;
            indexButton.Text = Localization.T("index.cancel");
            cancelButton.Enabled = true;
            operationProgress.Value = 0;
            UpdateSearchControls();
            statusMessage.Text = Localization.T("status.index_building");
            Progress<IndexProgress> progress = new Progress<IndexProgress>(delegate(IndexProgress value)
            {
                int amount = value.TotalBytes <= 0 ? 0 : (int)Math.Min(1000, value.ProcessedBytes * 1000.0 / value.TotalBytes);
                operationProgress.Value = Math.Max(0, Math.Min(1000, amount));
                double remaining = value.BytesPerSecond > 0 ? (value.TotalBytes - value.ProcessedBytes) / value.BytesPerSecond : 0;
                performanceSpeedLabel.Text = MetricText("metric.speed", FormatBytes((long)value.BytesPerSecond) + Localization.T("unit.per_second"));
                performanceEtaLabel.Text = MetricText("metric.eta", FormatDuration(remaining));
                performanceScannedLabel.Text = MetricText("metric.scanned", string.Format("{0} / {1}", FormatBytes(value.ProcessedBytes), FormatBytes(value.TotalBytes)));
                performanceBottleneckLabel.Text = MetricText("metric.bottleneck", "Disk / I/O");
                indexInfoLabel.Text = Localization.Format("metric.index_counted", value.LinesCounted);
                statusMessage.Text = Localization.Format("status.index_progress",
                    value.TotalBytes <= 0 ? 0 : value.ProcessedBytes * 100.0 / value.TotalBytes,
                    value.LinesCounted,
                    FormatDuration(remaining));
            });

            try
            {
                string path = currentPath;
                ResolvedEncoding encoding = currentEncoding;
                NavigationIndexData built = await Task.Run(delegate
                {
                    return NavigationIndexStore.Build(path, encoding, token, progress);
                }, token);
                if (token.IsCancellationRequested) return;
                navigationIndex = built;
                statusMessage.Text = Localization.Format("status.index_ready", built.TotalLines);
                UpdateIndexUi();
                await LoadWindowAsync(lineViewer.TopByteOffset, true, lineViewer.TopByteOffset, false);
            }
            catch (OperationCanceledException)
            {
                statusMessage.Text = Localization.T("status.index_cancelled");
            }
            catch (Exception exception)
            {
                ShowOperationError(Localization.T("error.index_build"), exception);
            }
            finally
            {
                indexRunning = false;
                indexButton.Text = Localization.T(navigationIndex == null ? "index.create" : "index.refresh");
                cancelButton.Enabled = searchRunning;
                operationProgress.Value = 0;
                if (indexCancellation != null)
                {
                    indexCancellation.Dispose();
                    indexCancellation = null;
                }
                UpdateIndexUi();
                UpdateSearchControls();
            }
        }

        private async void GoToLine()
        {
            if (navigationIndex == null || currentEncoding == null || string.IsNullOrEmpty(currentPath)) return;
            long targetLine = (long)lineInput.Value;
            try
            {
                statusMessage.Text = Localization.Format("status.finding_line", targetLine);
                string path = currentPath;
                ResolvedEncoding encoding = currentEncoding;
                NavigationIndexData index = navigationIndex;
                long offset = await Task.Run(delegate
                {
                    return NavigationIndexStore.FindOffsetForLine(path, targetLine, encoding, index, CancellationToken.None);
                });
                await LoadWindowAsync(offset, true, offset, true);
            }
            catch (Exception exception)
            {
                ShowOperationError(Localization.T("error.go_line"), exception);
            }
        }

        private void ToggleSelectedBookmark()
        {
            ToggleBookmark(lineViewer.SelectedLine);
        }

        private void ToggleBookmark(ViewerLine line)
        {
            if (line == null || currentSession == null) return;
            if (currentSession.IsBookmarked(line.ByteOffset))
            {
                currentSession.RemoveBookmark(line.ByteOffset);
                statusMessage.Text = Localization.Format("status.bookmark_removed", line.ByteOffset);
            }
            else
            {
                currentSession.AddOrUpdateBookmark(line.ByteOffset, "Byte " + line.ByteOffset.ToString("N0"), string.Empty);
                statusMessage.Text = Localization.Format("status.bookmark_added", line.ByteOffset);
            }
            lineViewer.RefreshBookmarks();
            RefreshBookmarksList();
            UpdateSelectionUi(line);
            ScheduleSessionSave();
        }

        private void RefreshBookmarksList()
        {
            bookmarksList.BeginUpdate();
            try
            {
                bookmarksList.Items.Clear();
                if (currentSession == null) return;
                for (int i = 0; i < currentSession.Bookmarks.Count; i++)
                {
                    BookmarkListItem item = new BookmarkListItem();
                    item.Bookmark = currentSession.Bookmarks[i];
                    bookmarksList.Items.Add(item);
                }
            }
            finally
            {
                bookmarksList.EndUpdate();
            }
        }

        private async void JumpToSelectedBookmark()
        {
            BookmarkListItem item = bookmarksList.SelectedItem as BookmarkListItem;
            if (item == null || item.Bookmark == null) return;
            await LoadWindowAsync(item.Bookmark.Offset, true, item.Bookmark.Offset, true);
        }

        private void RemoveSelectedBookmark()
        {
            BookmarkListItem item = bookmarksList.SelectedItem as BookmarkListItem;
            if (item == null || item.Bookmark == null || currentSession == null) return;
            currentSession.RemoveBookmark(item.Bookmark.Offset);
            RefreshBookmarksList();
            lineViewer.RefreshBookmarks();
            ScheduleSessionSave();
        }

        private async void NavigateHistory(bool forward)
        {
            long offset;
            bool success = forward
                ? navigationHistory.TryGoForward(out offset)
                : navigationHistory.TryGoBack(out offset);
            if (!success) return;
            await LoadWindowAsync(offset, true, offset, false);
            UpdateHistoryButtons();
        }

        private async void JumpToPercent()
        {
            if (chunks.Count == 0) return;
            long length = chunks[0].Page.FileLength;
            long offset = (long)(percentInput.Value * length / 100M);
            await LoadWindowAsync(offset, true, offset, true);
        }

        private async void JumpFromTrackBar()
        {
            if (chunks.Count == 0) return;
            long length = chunks[0].Page.FileLength;
            long offset = (long)((decimal)positionTrackBar.Value * length / PositionUnits);
            await LoadWindowAsync(offset, true, offset, true);
        }

        private void ScheduleSessionSave()
        {
            if (currentSession == null) return;
            sessionTimer.Stop();
            sessionTimer.Start();
        }

        private void SaveCurrentSession()
        {
            if (currentSession == null) return;
            if (lineViewer != null && lineViewer.Lines.Count > 0) currentSession.LastOffset = lineViewer.TopByteOffset;
            currentSession.DisplayMode = displayMode;
            EncodingOptionV2 encodingOption = encodingCombo.SelectedItem as EncodingOptionV2;
            if (encodingOption != null) currentSession.SelectedEncoding = encodingOption.Choice;
            string error;
            ViewerSessionStore.TrySave(currentSession, out error);
        }

        private void UpdateLoadedWindowUi(PageData page, double milliseconds)
        {
            suppressUiEvents = true;
            try
            {
                decimal percent = page.FileLength <= 0 ? 0 : (decimal)page.StartOffset * 100M / page.FileLength;
                percentInput.Value = Math.Max(0, Math.Min(100, decimal.Round(percent, 3)));
                positionTrackBar.Value = OffsetToPositionUnit(page.StartOffset, page.FileLength);
            }
            finally
            {
                suppressUiEvents = false;
            }
            statusEncoding.Text = Localization.Format("status.encoding", Localization.EncodingName(currentEncoding.Choice), DisplayModeName(displayMode));
            statusMessage.Text = Localization.Format("status.ready_loaded", lineViewer.Lines.Count, milliseconds);
            UpdateLoadedRangeStatus();
            UpdateIndexUi();
        }

        private void UpdateLoadedRangeStatus()
        {
            if (chunks.Count == 0) return;
            long length = chunks[0].Page.FileLength;
            long top = lineViewer.TopByteOffset;
            double percent = length <= 0 ? 0 : top * 100.0 / length;
            statusPosition.Text = Localization.Format("status.position", FormatBytes(top), FormatBytes(length), percent);
            suppressUiEvents = true;
            try
            {
                positionTrackBar.Value = OffsetToPositionUnit(top, length);
                percentInput.Value = Math.Max(0, Math.Min(100, decimal.Round((decimal)percent, 3)));
            }
            finally
            {
                suppressUiEvents = false;
            }
        }

        private void UpdateSelectionUi(ViewerLine line)
        {
            bookmarkButton.Enabled = line != null && currentSession != null;
            if (line == null)
            {
                bookmarkButton.Text = Localization.T("bookmark.off");
                return;
            }
            bool marked = currentSession != null && currentSession.IsBookmarked(line.ByteOffset);
            bookmarkButton.Text = Localization.T(marked ? "bookmark.on" : "bookmark.off");
            statusPosition.Text = Localization.Format("status.byte_length", line.ByteOffset, line.ByteLength);
            if (currentSession != null)
            {
                currentSession.LastOffset = line.ByteOffset;
                ScheduleSessionSave();
            }
        }

        private void UpdateIndexUi()
        {
            bool available = navigationIndex != null;
            lineInput.Enabled = available && !indexRunning;
            goLineButton.Enabled = available && !indexRunning;
            indexButton.Enabled = !string.IsNullOrEmpty(currentPath) && !searchRunning;
            indexButton.Text = Localization.T(indexRunning ? "index.cancel" : available ? "index.refresh" : "index.create");
            indexInfoLabel.Text = available
                ? Localization.Format("index.available", navigationIndex.TotalLines)
                : Localization.T("index.unavailable");
            if (available)
            {
                decimal maximum = Math.Min(999999999999M, navigationIndex.TotalLines);
                lineInput.Maximum = Math.Max(1, maximum);
            }
        }

        private void UpdateHistoryButtons()
        {
            backButton.Enabled = navigationHistory.CanGoBack;
            forwardButton.Enabled = navigationHistory.CanGoForward;
        }

        private void UpdateSearchControls()
        {
            bool enabled = !searchRunning && !indexRunning && !string.IsNullOrEmpty(currentPath) && !string.IsNullOrEmpty(searchBox.Text);
            previousMatchButton.Enabled = enabled;
            nextMatchButton.Enabled = enabled;
            fullSearchButton.Enabled = enabled;
            indexButton.Enabled = !string.IsNullOrEmpty(currentPath) && !searchRunning;
            bool fileReady = fileControlsReady && !string.IsNullOrEmpty(currentPath);
            searchBox.Enabled = fileReady && !searchRunning && !indexRunning;
            encodingCombo.Enabled = fileReady && !searchRunning && !indexRunning;
            displayModeCombo.Enabled = fileReady && !searchRunning && !indexRunning;
            openButton.Enabled = !searchRunning && !indexRunning;
            languageCombo.Enabled = !searchRunning && !indexRunning;
        }

        private void SetFileControlsEnabled(bool enabled)
        {
            fileControlsReady = enabled;
            percentInput.Enabled = enabled;
            positionTrackBar.Enabled = enabled;
            encodingCombo.Enabled = enabled;
            displayModeCombo.Enabled = enabled;
            searchBox.Enabled = enabled;
            indexButton.Enabled = enabled;
            Control[] goControls = Controls.Find("goPercentButton", true);
            for (int i = 0; i < goControls.Length; i++) goControls[i].Enabled = enabled;
            if (!enabled)
            {
                backButton.Enabled = false;
                forwardButton.Enabled = false;
                bookmarkButton.Enabled = false;
                lineInput.Enabled = false;
                goLineButton.Enabled = false;
            }
            UpdateSearchControls();
        }

        private void SetEncodingCombo(EncodingChoice choice)
        {
            suppressUiEvents = true;
            try
            {
                for (int i = 0; i < encodingCombo.Items.Count; i++)
                {
                    EncodingOptionV2 option = encodingCombo.Items[i] as EncodingOptionV2;
                    if (option != null && option.Choice == choice)
                    {
                        encodingCombo.SelectedIndex = i;
                        return;
                    }
                }
                encodingCombo.SelectedIndex = 0;
            }
            finally
            {
                suppressUiEvents = false;
            }
        }

        private void SetDisplayModeCombo(ViewerDisplayMode mode)
        {
            suppressUiEvents = true;
            try
            {
                for (int i = 0; i < displayModeCombo.Items.Count; i++)
                {
                    DisplayModeOption option = displayModeCombo.Items[i] as DisplayModeOption;
                    if (option != null && option.Mode == mode)
                    {
                        displayModeCombo.SelectedIndex = i;
                        return;
                    }
                }
                displayModeCombo.SelectedIndex = 0;
            }
            finally
            {
                suppressUiEvents = false;
            }
        }

        private void RebuildEncodingOptions(EncodingChoice selected)
        {
            encodingCombo.Items.Clear();
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Automatic), EncodingChoice.Automatic));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Utf8), EncodingChoice.Utf8));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Windows1254), EncodingChoice.Windows1254));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Windows1252), EncodingChoice.Windows1252));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Latin1), EncodingChoice.Latin1));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Utf16LittleEndian), EncodingChoice.Utf16LittleEndian));
            encodingCombo.Items.Add(new EncodingOptionV2(Localization.EncodingName(EncodingChoice.Utf16BigEndian), EncodingChoice.Utf16BigEndian));
            SetEncodingCombo(selected);
        }

        private void RebuildDisplayModeOptions(ViewerDisplayMode selected)
        {
            displayModeCombo.Items.Clear();
            displayModeCombo.Items.Add(new DisplayModeOption(Localization.DisplayModeName(ViewerDisplayMode.Normal), ViewerDisplayMode.Normal));
            displayModeCombo.Items.Add(new DisplayModeOption(Localization.DisplayModeName(ViewerDisplayMode.VisibleCharacters), ViewerDisplayMode.VisibleCharacters));
            displayModeCombo.Items.Add(new DisplayModeOption(Localization.DisplayModeName(ViewerDisplayMode.Hex), ViewerDisplayMode.Hex));
            SetDisplayModeCombo(selected);
        }

        private void ChangeLanguage()
        {
            if (suppressUiEvents) return;
            LanguageOption option = languageCombo.SelectedItem as LanguageOption;
            if (option == null || option.Language == Localization.CurrentLanguage) return;
            Localization.SetLanguage(option.Language);
            ApplyLanguage();
            statusMessage.Text = Localization.T("language.changed");
            AppLanguage appliedLanguage = option.Language;
            BeginInvoke(new MethodInvoker(delegate
            {
                suppressUiEvents = true;
                try
                {
                    SelectLanguageInCombo(appliedLanguage);
                }
                finally
                {
                    suppressUiEvents = false;
                }
            }));
        }

        private void ApplyLanguage()
        {
            EncodingOptionV2 selectedEncoding = encodingCombo.SelectedItem as EncodingOptionV2;
            EncodingChoice encodingChoice = selectedEncoding == null ? EncodingChoice.Automatic : selectedEncoding.Choice;
            ViewerDisplayMode selectedMode = displayMode;

            ApplyLocalizedControlTexts(this);
            RebuildEncodingOptions(encodingChoice);
            RebuildDisplayModeOptions(selectedMode);
            toolTip.SetToolTip(backButton, Localization.T("history.back"));
            toolTip.SetToolTip(forwardButton, Localization.T("history.forward"));
            toolTip.SetToolTip(fullSearchButton, Localization.T("search.all.tip"));
            lineViewer.ApplyLanguage();

            if (string.IsNullOrEmpty(currentPath))
            {
                Text = Localization.T("window.title");
                fileNameLabel.Text = Localization.T("file.none");
                filePathLabel.Text = Localization.T("file.drop_hint");
                statusPosition.Text = Localization.T("status.position.empty");
                statusEncoding.Text = Localization.T("status.encoding.empty");
            }
            else
            {
                Text = Path.GetFileName(currentPath) + " — " + Localization.T("window.title");
                if (currentEncoding != null)
                {
                    statusEncoding.Text = Localization.Format("status.encoding", Localization.EncodingName(currentEncoding.Choice), DisplayModeName(displayMode));
                }
                UpdateLoadedRangeStatus();
            }

            performanceSpeedLabel.Text = MetricText("metric.speed", "—");
            performanceEtaLabel.Text = MetricText("metric.eta", "—");
            performanceScannedLabel.Text = MetricText("metric.scanned", "—");
            performanceBottleneckLabel.Text = MetricText("metric.bottleneck", "—");
            performanceReadLabel.Text = MetricText("metric.read_compare", "—");
            UpdateIndexUi();
            UpdateSelectionUi(lineViewer.SelectedLine);
            if (HasFullSearchResults()) ShowFullSearchResultsPage(fullSearchResultPage);
            else UpdateLocalSearchResults();
            UpdateSearchControls();
        }

        private void SelectLanguageInCombo(AppLanguage language)
        {
            for (int i = 0; i < languageCombo.Items.Count; i++)
            {
                LanguageOption candidate = languageCombo.Items[i] as LanguageOption;
                if (candidate != null && candidate.Language == language)
                {
                    languageCombo.SelectedIndex = i;
                    return;
                }
            }
            if (languageCombo.Items.Count > 0) languageCombo.SelectedIndex = 0;
        }

        private void ChangeViewerFont(float delta)
        {
            viewerFontSize = Math.Max(7.0f, Math.Min(28.0f, viewerFontSize + delta));
            Font old = lineViewer.Font;
            lineViewer.Font = new Font("Consolas", viewerFontSize);
            old.Dispose();
            statusMessage.Text = Localization.Format("status.font_size", viewerFontSize);
        }

        private void CancelActiveOperation()
        {
            if (searchCancellation != null && !searchCancellation.IsCancellationRequested) searchCancellation.Cancel();
            if (indexCancellation != null && !indexCancellation.IsCancellationRequested) indexCancellation.Cancel();
        }

        private void CancelAllOperations()
        {
            CancelActiveOperation();
            if (pageCancellation != null && !pageCancellation.IsCancellationRequested) pageCancellation.Cancel();
        }

        private async void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                await ChooseFileAsync();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                if (e.Shift)
                {
                    StartFullSearch();
                }
                else
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                StartSearch(!e.Shift);
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Home)
            {
                await LoadWindowAsync(0, true, 0, true);
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.End && chunks.Count > 0)
            {
                await LoadWindowAsync(chunks[0].Page.FileLength, true, chunks[0].Page.FileLength, true);
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Left)
            {
                NavigateHistory(false);
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                NavigateHistory(true);
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.B)
            {
                ToggleSelectedBookmark();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelActiveOperation();
            }
        }

        private void ShowOperationError(string title, Exception exception)
        {
            statusMessage.Text = title + ": " + exception.Message;
            MessageBox.Show(this, title + ":\r\n\r\n" + exception.Message, "Wordlist Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static int OffsetToPositionUnit(long offset, long length)
        {
            if (length <= 0) return 0;
            return (int)Math.Max(0, Math.Min(PositionUnits, (decimal)offset * PositionUnits / length));
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024.0 && unit < units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }
            return unit == 0
                ? string.Format("{0:N0} {1}", value, units[unit])
                : string.Format("{0:N2} {1}", value, units[unit]);
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "—";
            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.TotalDays >= 1) return Localization.Format("duration.days", Math.Floor(span.TotalDays), span);
            return span.ToString(@"hh\:mm\:ss");
        }

        private static string DisplayModeName(ViewerDisplayMode mode)
        {
            return Localization.DisplayModeName(mode);
        }
    }
}
