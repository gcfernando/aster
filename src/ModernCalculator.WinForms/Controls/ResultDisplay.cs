using ModernCalculator.WinForms.Themes;
using System.Runtime.InteropServices;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Selectable values with explicit paging when even compact text cannot fit; no hidden overflow.</summary>
public sealed class ResultDisplay : UserControl, IThemedControl
{
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Light);
    private readonly Label caption = Ui.Label("Expression", 9);
    private readonly TextBox number = new() { ReadOnly = true, Multiline = true, WordWrap = true, BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Right, TabStop = false, ShortcutsEnabled = true, ScrollBars = ScrollBars.None,
        Margin = new(2, 0, 2, 0), AccessibleName = "Result" };
    private readonly Label detail = Ui.Label("", 8);
    private readonly ResultLayout layout = new();
    private readonly TableLayoutPanel footer = Ui.Table(3, -100);
    private readonly CalcButton previous;
    private readonly CalcButton next;
    private Font? numberFont;
    private string value = "0";
    private bool error;
    private bool fitting;
    private int page;
    private int pageCount = 1;
    public string FullValue => value;
    public int PageIndex => page;
    public int PageCount => pageCount;
    public ResultDisplay()
    {
        SuspendLayout(); AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true; Dock = DockStyle.Fill; Margin = new(2); Padding = new(8, 2, 8, 2);
        caption.TextAlign = detail.TextAlign = ContentAlignment.MiddleRight; caption.AutoEllipsis = true;
        caption.Margin = detail.Margin = new(2, 0, 2, 0);
        footer.ColumnStyles.Clear(); footer.ColumnStyles.Add(new(SizeType.Absolute, 24)); footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.Absolute, 24));
        previous = Ui.Button("‹", () => { page--; FitNumber(); }, ButtonTone.Quiet, name: "Previous result digits");
        next = Ui.Button("›", () => { page++; FitNumber(); }, ButtonTone.Quiet, name: "Next result digits");
        previous.Margin = next.Margin = new(0); Ui.SetFont(previous, 9); Ui.SetFont(next, 9);
        footer.Controls.Add(previous, 0, 0); footer.Controls.Add(detail, 1, 0); footer.Controls.Add(next, 2, 0);
        layout.Controls.Add(caption, 0, 0); layout.Controls.Add(number, 0, 1); layout.Controls.Add(footer, 0, 2); Controls.Add(layout);
        SizeChanged += (_, _) => FitNumber(); number.SizeChanged += (_, _) => FitNumber(); number.HandleCreated += (_, _) => FitNumber();
        layout.AfterLayout = FitNumber;
        Set("Ready", "0"); ResumeLayout(true);
    }
    public void Set(string expressionText, string resultText, bool isError = false)
    {
        var changed = value != resultText || error != isError;
        value = resultText; error = isError; page = 0; caption.Text = expressionText;
        number.AccessibleName = (isError ? "Calculation error: " : "Result: ") + value;
        number.AccessibleDescription = expressionText + ". Full value is available with Copy result; use result-page buttons when shown.";
        FitNumber();
        if (changed) Ui.Announce(this, (isError ? "Calculation error. " : "Result. ") + (value.Length > 160 ? value[..160] + ". Copy result for the complete value." : value), isError);
    }
    public void ApplyTheme(Palette theme)
    {
        palette = theme; BackColor = theme.Surface; caption.ForeColor = detail.ForeColor = theme.Muted;
        number.BackColor = theme.Surface; number.ForeColor = error ? theme.Error : theme.Text;
    }
    private void FitNumber()
    {
        if (fitting || IsDisposed || Disposing || Width <= 20 || Height <= 8) return;
        fitting = true;
        try
        {
            var scale = DeviceDpi / 96f;
            var logicalHeight = ClientSize.Height / scale;
            var longValue = value.Length > 24;
            var captionHeight = logicalHeight >= 85 ? 22 * scale : 0;
            var footerHeight = longValue || error ? 18 * scale : 0;
            layout.RowStyles[0].Height = captionHeight; layout.RowStyles[2].Height = footerHeight;
            caption.Visible = captionHeight > 0; footer.Visible = footerHeight > 0;
            layout.PerformLayout();
            var family = longValue ? DesignTokens.NumberFontFamily : DesignTokens.FontFamily;
            var pointSize = error ? 12f : 40f;
            var width = Math.Max(1, number.ClientSize.Width - 4);
            var height = Math.Max(1, number.ClientSize.Height - 2);
            int columns = 1, lines = 1;
            while (true)
            {
                using var candidate = new Font(family, pointSize, FontStyle.Regular);
                var glyph = TextRenderer.MeasureText("0", candidate, Size.Empty, TextFormatFlags.NoPadding);
                // Native EDIT and TextRenderer use the same GDI font metrics. Converting these
                // again by DeviceDpi overestimates capacity on mixed-DPI desktops.
                var fontHeight = candidate.Height;
                var glyphWidth = glyph.Width;
                columns = Math.Max(1, (int)(width / Math.Max(1, glyphWidth)));
                lines = Math.Max(1, (int)(height / Math.Max(1, fontHeight)));
                if (fontHeight <= height && glyphWidth <= width && (long)columns * lines >= value.Length || pointSize <= 8) break;
                pointSize = Math.Max(8, pointSize - 1);
            }
            if (numberFont is null || numberFont.SizeInPoints != pointSize || numberFont.FontFamily.Name != family)
            {
                var replacement = new Font(family, pointSize, FontStyle.Regular);
                var old = numberFont; number.Font = replacement; numberFont = replacement; old?.Dispose();
            }
            // A containing form can scale a child font before its final layout. Restore the
            // requested font as well when our owned instance itself did not need replacement.
            if (!ReferenceEquals(number.Font, numberFont)) number.Font = numberFont;
            var capacity = Math.Max(1, columns * lines);
            pageCount = Math.Max(1, (value.Length + capacity - 1) / capacity);
            page = Math.Clamp(page, 0, pageCount - 1);
            previous.Visible = next.Visible = pageCount > 1;
            previous.Enabled = page > 0; next.Enabled = page + 1 < pageCount;
            var offset = page * capacity;
            var pageText = value.Substring(offset, Math.Min(capacity, value.Length - offset));
            // Explicit line breaks make all digits reachable without native edit scrolling.
            var visibleText = string.Join(Environment.NewLine, Enumerable.Range(0, Math.Max(1, (pageText.Length + columns - 1) / columns))
                .Select(index => pageText.Substring(index * columns, Math.Min(columns, pageText.Length - index * columns))));
            if (number.Text != visibleText) number.Text = visibleText;
            var digits = value.Count(char.IsAsciiDigit);
            detail.Text = pageCount > 1 ? $"Page {page + 1} / {pageCount} · Copy keeps the full value" : error ? "Correct the input to continue" : digits > 24 ? $"{digits} digits · full value shown" : "";
            number.ForeColor = error ? palette.Error : palette.Text;
            number.SelectionStart = number.SelectionLength = 0;
            if (number.IsHandleCreated)
            {
                var rectangle = new NativeRectangle { Right = number.ClientSize.Width, Bottom = number.ClientSize.Height };
                _ = SendMessage(number.Handle, 0x00b3 /* EM_SETRECT */, 0, ref rectangle);
                number.ScrollToCaret();
            }
        }
        finally { fitting = false; }
    }
    private sealed class ResultLayout : TableLayoutPanel
    {
        public Action? AfterLayout;
        public ResultLayout()
        {
            Dock = DockStyle.Fill; Margin = Padding.Empty; ColumnCount = 1; RowCount = 3;
            ColumnStyles.Add(new(SizeType.Percent, 100));
            RowStyles.Add(new(SizeType.Absolute, 22)); RowStyles.Add(new(SizeType.Percent, 100)); RowStyles.Add(new(SizeType.Absolute, 18));
        }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); AfterLayout?.Invoke(); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint parameter, ref NativeRectangle rectangle);
    protected override void Dispose(bool disposing) { if (disposing) { fitting = true; numberFont?.Dispose(); } base.Dispose(disposing); }
}
