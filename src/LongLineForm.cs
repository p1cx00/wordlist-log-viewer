using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordlistViewer
{
    internal sealed class LongLineForm : Form
    {
        private readonly string path;
        private readonly ResolvedEncoding encoding;
        private readonly ViewerDisplayMode displayMode;
        private long offset;
        private PageData page;
        private CancellationTokenSource cancellation;

        private readonly RichTextBox textBox;
        private readonly Label information;
        private readonly Button previousButton;
        private readonly Button nextButton;

        public LongLineForm(string path, long offset, ResolvedEncoding encoding, ViewerDisplayMode displayMode)
        {
            this.path = path;
            this.offset = Math.Max(0, offset);
            this.encoding = encoding;
            this.displayMode = displayMode;

            Text = Localization.T("long_line.title");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 650);
            MinimumSize = new Size(700, 450);
            BackColor = Color.FromArgb(20, 23, 30);
            ForeColor = Color.FromArgb(235, 239, 247);
            Font = new Font("Segoe UI", 9.0f);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 45;
            toolbar.Padding = new Padding(8, 6, 8, 5);
            toolbar.BackColor = Color.FromArgb(29, 34, 44);

            previousButton = CreateButton(Localization.T("long_line.previous"));
            nextButton = CreateButton(Localization.T("long_line.next"));
            Button copyButton = CreateButton(Localization.T("long_line.copy"));
            information = new Label();
            information.AutoSize = true;
            information.ForeColor = Color.FromArgb(155, 166, 184);
            information.Margin = new Padding(15, 8, 0, 0);

            toolbar.Controls.Add(previousButton);
            toolbar.Controls.Add(nextButton);
            toolbar.Controls.Add(copyButton);
            toolbar.Controls.Add(information);

            textBox = new RichTextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.ReadOnly = true;
            textBox.WordWrap = false;
            textBox.DetectUrls = false;
            textBox.BackColor = Color.FromArgb(14, 17, 23);
            textBox.ForeColor = Color.FromArgb(218, 224, 234);
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = new Font("Consolas", 10.5f);
            textBox.ScrollBars = RichTextBoxScrollBars.ForcedBoth;

            Controls.Add(textBox);
            Controls.Add(toolbar);

            previousButton.Click += async delegate { await MovePrevious(); };
            nextButton.Click += async delegate { await MoveNext(); };
            copyButton.Click += delegate
            {
                if (!string.IsNullOrEmpty(textBox.SelectedText)) Clipboard.SetText(textBox.SelectedText);
            };
            Shown += async delegate { await LoadSegment(offset); };
            FormClosing += delegate { if (cancellation != null) cancellation.Cancel(); };
        }

        private Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(54, 63, 79);
            button.BackColor = Color.FromArgb(29, 34, 44);
            button.ForeColor = Color.FromArgb(235, 239, 247);
            return button;
        }

        private async Task LoadSegment(long requestedOffset)
        {
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            textBox.Text = Localization.T("long_line.reading");
            try
            {
                PageData loaded = await Task.Run(delegate
                {
                    return HugeFileReader.ReadLineSegment(path, requestedOffset, encoding, token);
                }, token);
                if (token.IsCancellationRequested) return;
                page = loaded;
                offset = loaded.StartOffset;
                if (displayMode == ViewerDisplayMode.Hex)
                {
                    textBox.Text = DisplayFormatter.FormatHex(loaded.ContentBytes, 0, loaded.ContentBytes.Length, loaded.ContentOffset);
                }
                else
                {
                    textBox.Text = DisplayFormatter.Decode(loaded.ContentBytes, 0, loaded.ContentBytes.Length, encoding.Encoding, displayMode);
                }
                information.Text = Localization.Format(
                    "long_line.info",
                    loaded.StartOffset,
                    loaded.EndOffset,
                    loaded.ContentBytes.Length,
                    loaded.TruncatedAtStart || loaded.TruncatedAtEnd ? Localization.T("long_line.continues") : string.Empty);
                previousButton.Enabled = loaded.StartOffset > 0;
                nextButton.Enabled = loaded.EndOffset < loaded.FileLength;
                Text = Path.GetFileName(path) + " — " + Localization.T("long_line.title");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                textBox.Text = Localization.Format("long_line.error", exception.Message);
            }
        }

        private async Task MovePrevious()
        {
            if (page == null || page.StartOffset <= 0) return;
            await LoadSegment(Math.Max(0, page.StartOffset - HugeFileReader.PageBytes));
        }

        private async Task MoveNext()
        {
            if (page == null || page.EndOffset >= page.FileLength) return;
            await LoadSegment(page.EndOffset);
        }
    }
}
