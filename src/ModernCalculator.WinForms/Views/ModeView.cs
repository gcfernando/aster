// Developer ::> Gehan Fernando
using ModernCalculator.Core.Calculations;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Views;

public abstract class ModeView : UserControl, IThemedControl
{
    protected WorkspaceContext Context { get; }
    protected Palette Palette { get; private set; } = Palette.Resolve(Infrastructure.Settings.ThemePreference.Dark);
    protected ModeView(WorkspaceContext context)
    {
        SuspendLayout();
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Context = context; Dock = DockStyle.Fill; Margin = Padding.Empty;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    // Defer the first DPI layout until the derived view has added its design-time rows.
    protected void FinishLayout() => ResumeLayout(true);
    protected void FitMemoryRow(TableLayoutPanel layout, int row)
    {
        void ResizeRow()
        {
            var scale = DeviceDpi / 96f; var height = (int)((ClientSize.Height / scale < 360 ? 24 : 34) * scale);
            layout.RowStyles[row].SizeType = SizeType.Absolute; layout.RowStyles[row].Height = height;
            if (layout.GetControlFromPosition(0, row) is { } bar)
            { bar.MinimumSize = Size.Empty; bar.MaximumSize = new(0, height); bar.MinimumSize = new(0, height); }
        }
        SizeChanged += (_, _) => ResizeRow(); ResizeRow();
    }
    public virtual void ApplyTheme(Palette palette) { Palette = palette; BackColor = palette.Surface; }
    public abstract string CopyText { get; }
    public abstract void Reuse(string value);
    public virtual bool Character(char character) => false;
    public virtual bool Command(Keys key) => false;
    public virtual void RefreshFormatting() { }
    public virtual void FocusInput() { SelectNextControl(null, true, true, true, false); }
    protected void Attempt(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is CalculationException or OverflowException or FormatException or ArgumentOutOfRangeException)
        { Context.Status(ex is OverflowException ? "The value is outside the supported range." : ex.Message); }
    }
    protected TableLayoutPanel MemoryControls(Func<BigDecimal> current, Action<BigDecimal> recall)
    {
        var row = Ui.Table(6, -100); row.AccessibleName = "Calculator memory";
        void Add(string label, Action action, string name)
        {
            var button = Ui.Button(label, () => Attempt(action), ButtonTone.Normal, name: name);
            Ui.SetFont(button, 9); row.Controls.Add(button, row.Controls.Count, 0);
        }
        Add("MA", () => Context.ShowMemory(), "Open memory list");
        Add("MR", () => recall(Context.Memory.RecallExact()), "Recall newest memory");
        Add("MS", () => { Context.Memory.Store(current()); Context.NotifyMemory(); Context.Status("Stored a new memory value. MA opens all slots."); }, "Store in new memory slot");
        Add("M+", () => { Context.Memory.Add(current()); Context.NotifyMemory(); Context.Status("Added to the newest memory value."); }, "Add to newest memory");
        Add("M−", () => { Context.Memory.Add(-current()); Context.NotifyMemory(); Context.Status("Subtracted from the newest memory value."); }, "Subtract from newest memory");
        Add("MC", () => { Context.Memory.Clear(); Context.NotifyMemory(); Context.Status("Cleared session memory."); }, "Clear all memory");
        void UpdateMemory()
        {
            var count = Context.Memory.Items.Count;
            row.Controls[0].Text = "MA · " + count;
            row.Controls[0].AccessibleDescription = $"All memory slots. {count} stored. Opens the memory panel. Memory lasts for this session.";
            foreach (Control button in row.Controls) if (button.AccessibleName is "Recall newest memory" or "Clear all memory") button.Enabled = count > 0;
            row.AccessibleDescription = $"{count} stored memory values. MA means all memory, MR recall, MS store, M+ add, M− subtract, MC clear.";
        }
        // Release the context subscription when the memory row is disposed.
        Context.MemoryChanged += UpdateMemory; row.Disposed += (_, _) => Context.MemoryChanged -= UpdateMemory; UpdateMemory();
        var sizing = false;
        row.Layout += (_, _) =>
        {
            if (sizing || row.ClientSize.Width <= 0) return;
            sizing = true;
            try
            {
                var width = row.ClientSize.Width / 6;
                var remainder = row.ClientSize.Width - width * 6;
                row.Padding = new(remainder / 2, 0, remainder - remainder / 2, 0);
                foreach (ColumnStyle column in row.ColumnStyles) { column.SizeType = SizeType.Absolute; column.Width = width; }
            }
            finally { sizing = false; }
        };
        return row;
    }
}
