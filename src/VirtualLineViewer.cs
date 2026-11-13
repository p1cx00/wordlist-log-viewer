using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WordlistViewer
{
    internal sealed class ViewerLine
    {
        public long LocalLineNumber { get; set; }
        public long ByteOffset { get; set; }
        public long ByteLength { get; set; }
        public string Text { get; set; }
        public bool IsContinuation { get; set; }
        public bool IsTruncated { get; set; }
        public bool IsBookmarked { get; set; }

        public ViewerLine()
        {
            Text = string.Empty;
        }
    }

    internal sealed class ViewerLineEventArgs : EventArgs
    {
        public ViewerLine Line { get; private set; }
        public int Index { get; private set; }

        public ViewerLineEventArgs(ViewerLine line, int index)
        {
            Line = line;
            Index = index;
        }
    }

    internal sealed class ChunkRequestEventArgs : EventArgs
    {
        public bool Forward { get; private set; }
        public long EdgeByteOffset { get; private set; }

        public ChunkRequestEventArgs(bool forward, long edgeByteOffset)
        {
            Forward = forward;
            EdgeByteOffset = edgeByteOffset;
        }
    }

    internal sealed class VirtualLineViewer : UserControl
    {
        private const int BookmarkWidth = 27;
        private const int LineNumberWidth = 72;
        private const int ByteOffsetWidth = 136;
        private const int ByteLengthWidth = 72;
        private const int HeaderHeight = 27;
        private const int EdgeRequestThreshold = 8;
        private const int MaximumHighlightsPerLine = 256;

        private static readonly Color BackgroundColor = Color.FromArgb(14, 17, 23);
        private static readonly Color AlternateRowColor = Color.FromArgb(17, 21, 28);
        private static readonly Color HeaderColor = Color.FromArgb(30, 35, 45);
        private static readonly Color GutterColor = Color.FromArgb(23, 27, 35);
        private static readonly Color BorderColor = Color.FromArgb(54, 63, 79);
        private static readonly Color PrimaryTextColor = Color.FromArgb(218, 224, 234);
        private static readonly Color GutterTextColor = Color.FromArgb(132, 144, 163);
        private static readonly Color SelectionColor = Color.FromArgb(42, 76, 125);
        private static readonly Color SelectionInactiveColor = Color.FromArgb(42, 52, 68);
        private static readonly Color MatchColor = Color.FromArgb(142, 112, 31);
        private static readonly Color ActiveMatchColor = Color.FromArgb(197, 91, 42);
        private static readonly Color BookmarkColor = Color.FromArgb(245, 194, 66);
        private static readonly Color TruncatedColor = Color.FromArgb(231, 113, 113);

        private readonly VScrollBar verticalScrollBar;
        private readonly HScrollBar horizontalScrollBar;
        private readonly List<ViewerLine> lines;
        private readonly HashSet<int> selectedIndices;
        private readonly ContextMenuStrip contextMenu;
        private readonly ToolStripMenuItem copyItem;
        private readonly ToolStripMenuItem copyWithInfoItem;
        private readonly ToolStripMenuItem bookmarkItem;
        private readonly ToolStripMenuItem activateItem;
        private int rowHeight;
        private int gutterWidth;
        private int contentPixelWidth;
        private int selectionAnchor;
        private bool previousChunkRequested;
        private bool nextChunkRequested;
        private string searchText;
        private bool searchCaseSensitive;
        private int activeMatchLineIndex;
        private int activeMatchCharIndex;
        private long activeMatchOffset;
        private bool suppressChunkRequests;
        private Func<ViewerLine, bool> bookmarkPredicate;

        public event EventHandler<ChunkRequestEventArgs> ChunkRequested;
        public event EventHandler NearTop;
        public event EventHandler NearBottom;
        public event EventHandler SelectionChanged;
        public event EventHandler<ViewerLineEventArgs> SelectedLineChanged;
        public event EventHandler<ViewerLineEventArgs> BookmarkToggleRequested;
        public event EventHandler<ViewerLineEventArgs> LineActivated;

        public VirtualLineViewer()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            BackColor = BackgroundColor;
            ForeColor = PrimaryTextColor;
            Font = new Font("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
            TabStop = true;
            lines = new List<ViewerLine>();
            selectedIndices = new HashSet<int>();
            selectionAnchor = -1;
            activeMatchLineIndex = -1;
            activeMatchCharIndex = -1;
            activeMatchOffset = -1;
            searchText = string.Empty;
            gutterWidth = BookmarkWidth + LineNumberWidth + ByteOffsetWidth + ByteLengthWidth;

            verticalScrollBar = new VScrollBar();
            verticalScrollBar.SmallChange = 1;
            verticalScrollBar.ValueChanged += delegate
            {
                Invalidate();
                CheckChunkRequest();
            };

            horizontalScrollBar = new HScrollBar();
            horizontalScrollBar.SmallChange = 16;
            horizontalScrollBar.ValueChanged += delegate { Invalidate(); };

            Controls.Add(verticalScrollBar);
            Controls.Add(horizontalScrollBar);

            contextMenu = new ContextMenuStrip();
            copyItem = new ToolStripMenuItem(Localization.T("viewer.copy"));
            copyItem.ShortcutKeys = Keys.Control | Keys.C;
            copyItem.Click += delegate { CopySelectedLines(); };
            contextMenu.Items.Add(copyItem);

            copyWithInfoItem = new ToolStripMenuItem(Localization.T("viewer.copy_info"));
            copyWithInfoItem.Click += delegate { CopySelectedLinesWithInfo(); };
            contextMenu.Items.Add(copyWithInfoItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            bookmarkItem = new ToolStripMenuItem(Localization.T("viewer.toggle_bookmark"));
            bookmarkItem.Click += delegate { RaiseBookmarkForPrimarySelection(); };
            contextMenu.Items.Add(bookmarkItem);

            activateItem = new ToolStripMenuItem(Localization.T("viewer.open_line"));
            activateItem.Click += delegate { RaiseLineActivatedForPrimarySelection(); };
            contextMenu.Items.Add(activateItem);
            ContextMenuStrip = contextMenu;

            UpdateMetrics();
        }

        public void ApplyLanguage()
        {
            copyItem.Text = Localization.T("viewer.copy");
            copyWithInfoItem.Text = Localization.T("viewer.copy_info");
            bookmarkItem.Text = Localization.T("viewer.toggle_bookmark");
            activateItem.Text = Localization.T("viewer.open_line");
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderHeight));
        }

        public IList<ViewerLine> Lines
        {
            get { return lines.AsReadOnly(); }
        }

        public int SelectedLineIndex
        {
            get
            {
                if (selectedIndices.Count == 0) return -1;
                return selectedIndices.Min();
            }
        }

        public ViewerLine SelectedLine
        {
            get
            {
                int index = SelectedLineIndex;
                return index >= 0 && index < lines.Count ? lines[index] : null;
            }
        }

        public IList<int> SelectedLineIndices
        {
            get
            {
                List<int> result = selectedIndices.ToList();
                result.Sort();
                return result.AsReadOnly();
            }
        }

        public int TopLineIndex
        {
            get { return verticalScrollBar.Enabled ? verticalScrollBar.Value : 0; }
            set { SetTopLine(value); }
        }

        public long TopByteOffset
        {
            get
            {
                int index = TopLineIndex;
                return index >= 0 && index < lines.Count ? lines[index].ByteOffset : 0;
            }
        }

        public int VisibleLineCount
        {
            get
            {
                int height = Math.Max(0, ClientSize.Height - HeaderHeight - horizontalScrollBar.Height);
                return Math.Max(1, height / Math.Max(1, rowHeight));
            }
        }

        public string SearchText
        {
            get { return searchText; }
            set
            {
                searchText = value ?? string.Empty;
                Invalidate();
            }
        }

        public bool SearchCaseSensitive
        {
            get { return searchCaseSensitive; }
            set
            {
                searchCaseSensitive = value;
                Invalidate();
            }
        }

        public int ActiveMatchLineIndex
        {
            get { return activeMatchLineIndex; }
            set
            {
                activeMatchLineIndex = value;
                Invalidate();
            }
        }

        public int ActiveMatchCharIndex
        {
            get { return activeMatchCharIndex; }
            set
            {
                activeMatchCharIndex = value;
                Invalidate();
            }
        }

        public long ActiveMatchOffset
        {
            get { return activeMatchOffset; }
            set
            {
                activeMatchOffset = value;
                activeMatchLineIndex = FindLineIndexByOffset(value);
                Invalidate();
            }
        }

        public Func<ViewerLine, bool> BookmarkPredicate
        {
            get { return bookmarkPredicate; }
            set
            {
                bookmarkPredicate = value;
                Invalidate();
            }
        }

        public void SetLines(IList<ViewerLine> newLines, int selectedLineIndex, int topLineIndex)
        {
            lines.Clear();
            if (newLines != null)
            {
                for (int i = 0; i < newLines.Count; i++)
                {
                    if (newLines[i] != null) lines.Add(newLines[i]);
                }
            }

            selectedIndices.Clear();
            selectionAnchor = -1;
            if (selectedLineIndex >= 0 && selectedLineIndex < lines.Count)
            {
                selectedIndices.Add(selectedLineIndex);
                selectionAnchor = selectedLineIndex;
            }

            activeMatchLineIndex = -1;
            activeMatchCharIndex = -1;
            activeMatchOffset = -1;
            previousChunkRequested = false;
            nextChunkRequested = false;
            suppressChunkRequests = true;
            try
            {
                RecalculateContentWidth();
                UpdateScrollBars();
                SetTopLine(topLineIndex);
            }
            finally
            {
                suppressChunkRequests = false;
            }
            CheckChunkRequest();
            Invalidate();
        }

        public void SetLines(IList<ViewerLine> newLines)
        {
            SetLines(newLines, -1, 0);
        }

        public void ReplaceLinesPreservingState(IList<ViewerLine> newLines, long topByteOffset)
        {
            ViewerLine selectedLine = SelectedLine;
            long selectedByteOffset = selectedLine == null ? -1 : selectedLine.ByteOffset;
            long previousActiveMatchOffset = activeMatchOffset;
            int previousActiveMatchCharIndex = activeMatchCharIndex;

            SetLines(newLines);
            RestoreTopByteOffset(topByteOffset);
            if (selectedByteOffset >= 0)
            {
                SelectOffsetIfPresent(selectedByteOffset, false);
            }
            ActiveMatchOffset = previousActiveMatchOffset;
            ActiveMatchCharIndex = previousActiveMatchCharIndex;
        }

        public void AppendLines(IList<ViewerLine> additionalLines)
        {
            if (additionalLines == null || additionalLines.Count == 0) return;
            for (int i = 0; i < additionalLines.Count; i++)
            {
                if (additionalLines[i] != null) lines.Add(additionalLines[i]);
            }
            nextChunkRequested = false;
            RecalculateContentWidth();
            UpdateScrollBars();
            Invalidate();
        }

        public void PrependLines(IList<ViewerLine> additionalLines)
        {
            if (additionalLines == null || additionalLines.Count == 0) return;
            int originalTop = TopLineIndex;
            List<ViewerLine> filtered = new List<ViewerLine>();
            for (int i = 0; i < additionalLines.Count; i++)
            {
                if (additionalLines[i] != null) filtered.Add(additionalLines[i]);
            }
            if (filtered.Count == 0) return;

            lines.InsertRange(0, filtered);
            HashSet<int> shiftedSelection = new HashSet<int>();
            foreach (int index in selectedIndices) shiftedSelection.Add(index + filtered.Count);
            selectedIndices.Clear();
            foreach (int index in shiftedSelection) selectedIndices.Add(index);
            if (selectionAnchor >= 0) selectionAnchor += filtered.Count;
            if (activeMatchLineIndex >= 0) activeMatchLineIndex += filtered.Count;
            previousChunkRequested = false;
            RecalculateContentWidth();
            UpdateScrollBars();
            SetTopLine(originalTop + filtered.Count);
            Invalidate();
        }

        public void ScrollToLine(int index)
        {
            if (lines.Count == 0) return;
            index = Math.Max(0, Math.Min(lines.Count - 1, index));
            int top = TopLineIndex;
            int bottom = top + VisibleLineCount - 1;
            if (index < top)
            {
                SetTopLine(index);
            }
            else if (index > bottom)
            {
                SetTopLine(index - VisibleLineCount + 1);
            }
        }

        public void ScrollToOffset(long byteOffset, bool selectLine)
        {
            int index = FindLineIndexByOffset(byteOffset);
            if (index < 0) return;
            if (selectLine) SelectLine(index, true);
            else ScrollToLine(index);
        }

        public bool SelectOffsetIfPresent(long byteOffset, bool ensureVisible)
        {
            int index = FindLineIndexByOffset(byteOffset);
            if (index < 0 || index >= lines.Count) return false;
            ViewerLine line = lines[index];
            long end = line.ByteOffset + Math.Max(1, line.ByteLength);
            if (byteOffset < line.ByteOffset || byteOffset >= end) return false;
            SelectLine(index, ensureVisible);
            return true;
        }

        public long CaptureTopByteOffset()
        {
            return TopByteOffset;
        }

        public void RestoreTopByteOffset(long byteOffset)
        {
            int index = FindLineIndexByOffset(byteOffset);
            if (index >= 0) SetTopLine(index);
        }

        public void SelectLine(int index, bool ensureVisible)
        {
            if (index < 0 || index >= lines.Count) return;
            selectedIndices.Clear();
            selectedIndices.Add(index);
            selectionAnchor = index;
            if (ensureVisible) ScrollToLine(index);
            RaiseSelectedLineChanged(index);
            Invalidate();
        }

        public void RefreshBookmarks()
        {
            Invalidate(new Rectangle(0, HeaderHeight, BookmarkWidth, Math.Max(0, ClientSize.Height - HeaderHeight)));
        }

        public void CopySelectedLines()
        {
            IList<int> selected = SelectedLineIndices;
            if (selected.Count == 0) return;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0) builder.AppendLine();
                builder.Append(lines[selected[i]].Text);
            }
            TrySetClipboardText(builder.ToString());
        }

        public void CopySelectedLinesWithInfo()
        {
            IList<int> selected = SelectedLineIndices;
            if (selected.Count == 0) return;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                ViewerLine line = lines[selected[i]];
                if (i > 0) builder.AppendLine();
                builder.Append(line.LocalLineNumber.ToString("N0"));
                builder.Append('\t');
                builder.Append(line.ByteOffset.ToString("N0"));
                builder.Append('\t');
                builder.Append(line.ByteLength.ToString("N0"));
                builder.Append('\t');
                builder.Append(line.Text);
            }
            TrySetClipboardText(builder.ToString());
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (verticalScrollBar == null) return;
            UpdateMetrics();
            RecalculateContentWidth();
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (verticalScrollBar == null || horizontalScrollBar == null) return;
            LayoutScrollBars();
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.Clear(BackgroundColor);
            DrawHeader(graphics);
            DrawRows(graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int index = HitTestLine(e.Y);
            if (index < 0 || index >= lines.Count) return;

            if (e.X < BookmarkWidth)
            {
                RaiseBookmarkToggle(index);
                return;
            }

            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
            bool control = (ModifierKeys & Keys.Control) == Keys.Control;
            if (shift && selectionAnchor >= 0)
            {
                selectedIndices.Clear();
                int first = Math.Min(selectionAnchor, index);
                int last = Math.Max(selectionAnchor, index);
                for (int i = first; i <= last; i++) selectedIndices.Add(i);
            }
            else if (control)
            {
                if (!selectedIndices.Add(index)) selectedIndices.Remove(index);
                selectionAnchor = index;
            }
            else
            {
                selectedIndices.Clear();
                selectedIndices.Add(index);
                selectionAnchor = index;
            }
            RaiseSelectedLineChanged(index);
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int index = HitTestLine(e.Y);
            if (index >= 0 && index < lines.Count) RaiseLineActivated(index);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int linesToMove = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            int steps = Math.Max(1, Math.Abs(e.Delta) / SystemInformation.MouseWheelScrollDelta);
            int delta = linesToMove * steps * (e.Delta > 0 ? -1 : 1);
            int before = TopLineIndex;
            SetTopLine(before + delta);
            if (before == TopLineIndex)
            {
                if (delta < 0) RaiseChunkRequest(false);
                if (delta > 0) RaiseChunkRequest(true);
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Up || code == Keys.Down || code == Keys.PageUp || code == Keys.PageDown ||
                code == Keys.Home || code == Keys.End)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedLines();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                selectedIndices.Clear();
                for (int i = 0; i < lines.Count; i++) selectedIndices.Add(i);
                if (lines.Count > 0) selectionAnchor = 0;
                Invalidate();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                RaiseLineActivatedForPrimarySelection();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Space)
            {
                RaiseBookmarkForPrimarySelection();
                e.SuppressKeyPress = true;
                return;
            }

            int current = SelectedLineIndex;
            if (current < 0 && lines.Count > 0) current = 0;
            int next = current;
            switch (e.KeyCode)
            {
                case Keys.Up: next = current - 1; break;
                case Keys.Down: next = current + 1; break;
                case Keys.PageUp: next = current - VisibleLineCount; break;
                case Keys.PageDown: next = current + VisibleLineCount; break;
                case Keys.Home: next = e.Control ? 0 : TopLineIndex; break;
                case Keys.End: next = e.Control ? lines.Count - 1 : TopLineIndex + VisibleLineCount - 1; break;
                default: return;
            }

            if (lines.Count == 0) return;
            next = Math.Max(0, Math.Min(lines.Count - 1, next));
            if (e.Shift && selectionAnchor >= 0)
            {
                selectedIndices.Clear();
                int first = Math.Min(selectionAnchor, next);
                int last = Math.Max(selectionAnchor, next);
                for (int i = first; i <= last; i++) selectedIndices.Add(i);
            }
            else
            {
                selectedIndices.Clear();
                selectedIndices.Add(next);
                selectionAnchor = next;
            }
            ScrollToLine(next);
            RaiseSelectedLineChanged(next);
            Invalidate();
            e.SuppressKeyPress = true;
        }

        private void UpdateMetrics()
        {
            rowHeight = Math.Max(18, TextRenderer.MeasureText("Ag", Font, Size.Empty, TextFormatFlags.NoPadding).Height + 5);
            LayoutScrollBars();
        }

        private void LayoutScrollBars()
        {
            int verticalWidth = SystemInformation.VerticalScrollBarWidth;
            int horizontalHeight = SystemInformation.HorizontalScrollBarHeight;
            verticalScrollBar.Bounds = new Rectangle(
                Math.Max(0, ClientSize.Width - verticalWidth),
                HeaderHeight,
                verticalWidth,
                Math.Max(0, ClientSize.Height - HeaderHeight - horizontalHeight));
            horizontalScrollBar.Bounds = new Rectangle(
                gutterWidth,
                Math.Max(HeaderHeight, ClientSize.Height - horizontalHeight),
                Math.Max(0, ClientSize.Width - gutterWidth - verticalWidth),
                horizontalHeight);
        }

        private void UpdateScrollBars()
        {
            int visible = VisibleLineCount;
            verticalScrollBar.LargeChange = visible;
            verticalScrollBar.Maximum = Math.Max(0, lines.Count - 1);
            verticalScrollBar.Enabled = lines.Count > visible;
            int maximumVerticalValue = GetEffectiveMaximum(verticalScrollBar);
            if (verticalScrollBar.Value > maximumVerticalValue) verticalScrollBar.Value = maximumVerticalValue;

            int viewportWidth = Math.Max(1, ClientSize.Width - gutterWidth - verticalScrollBar.Width);
            horizontalScrollBar.LargeChange = viewportWidth;
            horizontalScrollBar.Maximum = Math.Max(0, contentPixelWidth - 1);
            horizontalScrollBar.Enabled = contentPixelWidth > viewportWidth;
            int maximumHorizontalValue = GetEffectiveMaximum(horizontalScrollBar);
            if (horizontalScrollBar.Value > maximumHorizontalValue) horizontalScrollBar.Value = maximumHorizontalValue;
        }

        private static int GetEffectiveMaximum(ScrollBar scrollBar)
        {
            return Math.Max(scrollBar.Minimum, scrollBar.Maximum - Math.Max(1, scrollBar.LargeChange) + 1);
        }

        private void SetTopLine(int value)
        {
            int clamped = Math.Max(0, Math.Min(GetEffectiveMaximum(verticalScrollBar), value));
            if (verticalScrollBar.Enabled)
            {
                if (verticalScrollBar.Value != clamped) verticalScrollBar.Value = clamped;
            }
            else
            {
                Invalidate();
            }
            CheckChunkRequest();
        }

        private void RecalculateContentWidth()
        {
            int averageWidth = Math.Max(5, TextRenderer.MeasureText("M", Font, Size.Empty, TextFormatFlags.NoPadding).Width);
            int maximumCharacters = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int length = lines[i].Text == null ? 0 : lines[i].Text.Length;
                if (length > maximumCharacters) maximumCharacters = length;
            }
            long estimated = (long)maximumCharacters * averageWidth + 24;
            contentPixelWidth = (int)Math.Min(100000000L, Math.Max(1L, estimated));
        }

        private void DrawHeader(Graphics graphics)
        {
            int drawWidth = Math.Max(0, ClientSize.Width - verticalScrollBar.Width);
            using (SolidBrush headerBrush = new SolidBrush(HeaderColor))
            using (Pen borderPen = new Pen(BorderColor))
            {
                graphics.FillRectangle(headerBrush, 0, 0, drawWidth, HeaderHeight);
                graphics.DrawLine(borderPen, 0, HeaderHeight - 1, drawWidth, HeaderHeight - 1);
                graphics.DrawLine(borderPen, BookmarkWidth, 0, BookmarkWidth, ClientSize.Height);
                graphics.DrawLine(borderPen, BookmarkWidth + LineNumberWidth, 0, BookmarkWidth + LineNumberWidth, ClientSize.Height);
                graphics.DrawLine(borderPen, BookmarkWidth + LineNumberWidth + ByteOffsetWidth, 0,
                    BookmarkWidth + LineNumberWidth + ByteOffsetWidth, ClientSize.Height);
                graphics.DrawLine(borderPen, gutterWidth, 0, gutterWidth, ClientSize.Height);
            }

            DrawCellText(graphics, "★", new Rectangle(0, 0, BookmarkWidth, HeaderHeight), GutterTextColor, HorizontalAlignment.Center);
            DrawCellText(graphics, Localization.T("viewer.header.line"), new Rectangle(BookmarkWidth, 0, LineNumberWidth, HeaderHeight), GutterTextColor, HorizontalAlignment.Right);
            DrawCellText(graphics, Localization.T("viewer.header.offset"), new Rectangle(BookmarkWidth + LineNumberWidth, 0, ByteOffsetWidth, HeaderHeight), GutterTextColor, HorizontalAlignment.Right);
            DrawCellText(graphics, Localization.T("viewer.header.length"), new Rectangle(BookmarkWidth + LineNumberWidth + ByteOffsetWidth, 0, ByteLengthWidth, HeaderHeight), GutterTextColor, HorizontalAlignment.Right);
            DrawCellText(graphics, Localization.T("viewer.header.text"), new Rectangle(gutterWidth + 6, 0, Math.Max(0, drawWidth - gutterWidth - 6), HeaderHeight), GutterTextColor, HorizontalAlignment.Left);
        }

        private void DrawRows(Graphics graphics)
        {
            if (lines.Count == 0) return;
            int top = TopLineIndex;
            int visible = VisibleLineCount + 1;
            int end = Math.Min(lines.Count, top + visible);
            int drawWidth = Math.Max(0, ClientSize.Width - verticalScrollBar.Width);
            int textViewportWidth = Math.Max(0, drawWidth - gutterWidth);

            using (SolidBrush gutterBrush = new SolidBrush(GutterColor))
            using (SolidBrush alternateBrush = new SolidBrush(AlternateRowColor))
            using (SolidBrush selectionBrush = new SolidBrush(Focused ? SelectionColor : SelectionInactiveColor))
            using (Pen separatorPen = new Pen(Color.FromArgb(31, 37, 47)))
            {
                for (int index = top; index < end; index++)
                {
                    int row = index - top;
                    int y = HeaderHeight + row * rowHeight;
                    Rectangle rowRectangle = new Rectangle(0, y, drawWidth, rowHeight);
                    if ((index & 1) == 1) graphics.FillRectangle(alternateBrush, rowRectangle);
                    if (selectedIndices.Contains(index)) graphics.FillRectangle(selectionBrush, rowRectangle);
                    graphics.FillRectangle(gutterBrush, new Rectangle(0, y, gutterWidth, rowHeight));
                    if (selectedIndices.Contains(index)) graphics.FillRectangle(selectionBrush, new Rectangle(0, y, gutterWidth, rowHeight));

                    ViewerLine line = lines[index];
                    bool bookmarked = bookmarkPredicate != null ? bookmarkPredicate(line) : line.IsBookmarked;
                    if (bookmarked)
                    {
                        DrawCellText(graphics, "★", new Rectangle(0, y, BookmarkWidth, rowHeight), BookmarkColor, HorizontalAlignment.Center);
                    }
                    else if (line.IsContinuation || line.IsTruncated)
                    {
                        DrawCellText(graphics, line.IsContinuation ? "↪" : "!", new Rectangle(0, y, BookmarkWidth, rowHeight), TruncatedColor, HorizontalAlignment.Center);
                    }

                    DrawCellText(graphics, line.LocalLineNumber.ToString("N0"),
                        new Rectangle(BookmarkWidth, y, LineNumberWidth, rowHeight), GutterTextColor, HorizontalAlignment.Right);
                    DrawCellText(graphics, line.ByteOffset.ToString("N0"),
                        new Rectangle(BookmarkWidth + LineNumberWidth, y, ByteOffsetWidth, rowHeight), GutterTextColor, HorizontalAlignment.Right);
                    DrawCellText(graphics, line.ByteLength.ToString("N0"),
                        new Rectangle(BookmarkWidth + LineNumberWidth + ByteOffsetWidth, y, ByteLengthWidth, rowHeight), GutterTextColor, HorizontalAlignment.Right);

                    string text = line.Text ?? string.Empty;
                    int textX = gutterWidth + 7 - horizontalScrollBar.Value;
                    Rectangle clipRectangle = new Rectangle(gutterWidth + 1, y, Math.Max(0, textViewportWidth - 1), rowHeight);
                    Region oldClip = graphics.Clip;
                    graphics.SetClip(clipRectangle);
                    DrawSearchHighlights(graphics, text, index, textX, y);
                    TextRenderer.DrawText(
                        graphics,
                        text,
                        Font,
                        new Point(textX, y + Math.Max(0, (rowHeight - Font.Height) / 2)),
                        PrimaryTextColor,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    graphics.Clip = oldClip;
                    oldClip.Dispose();

                    graphics.DrawLine(separatorPen, 0, y + rowHeight - 1, drawWidth, y + rowHeight - 1);
                }
            }
        }

        private void DrawSearchHighlights(Graphics graphics, string text, int lineIndex, int textX, int y)
        {
            if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(text)) return;
            StringComparison comparison = searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int start = 0;
            int matchCount = 0;
            while (start <= text.Length - searchText.Length && matchCount < MaximumHighlightsPerLine)
            {
                int match = text.IndexOf(searchText, start, comparison);
                if (match < 0) break;
                int prefixWidth = MeasureTextWidth(text.Substring(0, match));
                int matchWidth = Math.Max(2, MeasureTextWidth(text.Substring(match, searchText.Length)));
                bool active = lineIndex == activeMatchLineIndex && match == activeMatchCharIndex;
                using (SolidBrush brush = new SolidBrush(active ? ActiveMatchColor : MatchColor))
                {
                    graphics.FillRectangle(brush, textX + prefixWidth, y + 2, matchWidth, rowHeight - 4);
                }
                start = match + Math.Max(1, searchText.Length);
                matchCount++;
            }
        }

        private int MeasureTextWidth(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return TextRenderer.MeasureText(value, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        private void DrawCellText(Graphics graphics, string text, Rectangle rectangle, Color color, HorizontalAlignment alignment)
        {
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            if (alignment == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
            if (alignment == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
            Rectangle padded = new Rectangle(rectangle.X + 5, rectangle.Y, Math.Max(0, rectangle.Width - 10), rectangle.Height);
            TextRenderer.DrawText(graphics, text, Font, padded, color, flags);
        }

        private int HitTestLine(int y)
        {
            if (y < HeaderHeight || y >= ClientSize.Height - horizontalScrollBar.Height) return -1;
            int row = (y - HeaderHeight) / Math.Max(1, rowHeight);
            int index = TopLineIndex + row;
            return index >= 0 && index < lines.Count ? index : -1;
        }

        private void CheckChunkRequest()
        {
            if (suppressChunkRequests) return;
            if (lines.Count == 0) return;
            int top = TopLineIndex;
            int bottom = top + VisibleLineCount;
            if (top <= EdgeRequestThreshold) RaiseChunkRequest(false);
            else previousChunkRequested = false;
            if (bottom >= lines.Count - EdgeRequestThreshold) RaiseChunkRequest(true);
            else nextChunkRequested = false;
        }

        private void RaiseChunkRequest(bool forward)
        {
            if (forward)
            {
                if (nextChunkRequested) return;
                nextChunkRequested = true;
            }
            else
            {
                if (previousChunkRequested) return;
                previousChunkRequested = true;
            }

            EventHandler<ChunkRequestEventArgs> handler = ChunkRequested;
            long edgeOffset = 0;
            if (lines.Count > 0)
            {
                ViewerLine edge = forward ? lines[lines.Count - 1] : lines[0];
                edgeOffset = forward ? edge.ByteOffset + edge.ByteLength : edge.ByteOffset;
            }
            if (handler != null) handler(this, new ChunkRequestEventArgs(forward, edgeOffset));
            EventHandler edgeHandler = forward ? NearBottom : NearTop;
            if (edgeHandler != null) edgeHandler(this, EventArgs.Empty);
        }

        private void RaiseSelectedLineChanged(int index)
        {
            EventHandler simpleHandler = SelectionChanged;
            if (simpleHandler != null) simpleHandler(this, EventArgs.Empty);
            EventHandler<ViewerLineEventArgs> handler = SelectedLineChanged;
            if (handler != null && index >= 0 && index < lines.Count) handler(this, new ViewerLineEventArgs(lines[index], index));
        }

        private int FindLineIndexByOffset(long byteOffset)
        {
            if (lines.Count == 0) return -1;
            int low = 0;
            int high = lines.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                ViewerLine line = lines[middle];
                long end = line.ByteOffset + Math.Max(1, line.ByteLength);
                if (byteOffset < line.ByteOffset) high = middle - 1;
                else if (byteOffset >= end) low = middle + 1;
                else return middle;
            }
            if (low >= lines.Count) return lines.Count - 1;
            if (high < 0) return 0;
            long lowDistance = Math.Abs(lines[low].ByteOffset - byteOffset);
            long highDistance = Math.Abs(lines[high].ByteOffset - byteOffset);
            return lowDistance < highDistance ? low : high;
        }

        private void RaiseBookmarkToggle(int index)
        {
            EventHandler<ViewerLineEventArgs> handler = BookmarkToggleRequested;
            if (handler != null && index >= 0 && index < lines.Count) handler(this, new ViewerLineEventArgs(lines[index], index));
        }

        private void RaiseLineActivated(int index)
        {
            EventHandler<ViewerLineEventArgs> handler = LineActivated;
            if (handler != null && index >= 0 && index < lines.Count) handler(this, new ViewerLineEventArgs(lines[index], index));
        }

        private void RaiseBookmarkForPrimarySelection()
        {
            int index = SelectedLineIndex;
            if (index >= 0) RaiseBookmarkToggle(index);
        }

        private void RaiseLineActivatedForPrimarySelection()
        {
            int index = SelectedLineIndex;
            if (index >= 0) RaiseLineActivated(index);
        }

        private static void TrySetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (ExternalException)
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }
    }
}
