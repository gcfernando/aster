// Developer ::> Gehan Fernando
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.WinForms.Forms;

public sealed partial class MainForm : BorderlessForm, ITooltipHost
{
    private readonly WorkspaceContext context = new();
    private readonly StateStore store;
    private readonly Icon? applicationIcon;
    private readonly TableLayoutPanel root = Ui.Table(1, 44, 60, -100, 22);
    private readonly TableLayoutPanel workspace = Ui.Table(2, -100);
    private readonly TableLayoutPanel header = Ui.Table(10, -100);
    private readonly TableLayoutPanel center = Ui.Table(1, -100);
    private readonly Panel viewHost = new() { Dock = DockStyle.Fill, Padding = new(0) };
    private readonly Label status = Ui.Label("Ready", 9);
    private readonly Dictionary<string, ModeView> views = [];
    private readonly SegmentedControl modeNavigation = new("Calculator mode", ["Standard", "Scientific", "Programmer", "Converter", "Dates"], [IconKind.Standard, IconKind.Scientific, IconKind.Programmer, IconKind.Converter, IconKind.Calendar]);
    private readonly CalcButton themeButton;
    private readonly CalcButton drawerButton;
    private readonly CalcButton maximizeButton;
    private bool layoutReady;
    private readonly ToolTip tooltips = new() { AutoPopDelay = 7000, InitialDelay = 500, ReshowDelay = 100 };
    private readonly Font tooltipFont = new("Segoe UI", 9);
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> tooltipControls = new();
    private string mode = "Standard";
    private Palette palette;
    private bool drawerVisible;
    private int drawerWidthExpansion;
    private string? persistenceWarning;
    private ModeView Current => views[mode];

    public MainForm(StateStore stateStore)
    {
        SuspendLayout();
        store = stateStore;
        var loaded = store.Load(); context.Settings = loaded.State.Settings; context.History.Restore(loaded.State.History);
        context.RestoreConversionFavorites(loaded.State.ConversionFavorites ?? []);
        palette = Palette.Resolve(context.Settings.Theme, context.Settings.HighContrast);
        tooltips.OwnerDraw = true;
        tooltips.Popup += (_, e) =>
        {
            if (e.AssociatedControl is null) return;
            // Updating a native tooltip from TTN_SHOW can synchronously reenter Popup.
            var text = tooltips.GetToolTip(e.AssociatedControl);
            e.ToolTipSize = TextRenderer.MeasureText(text, tooltipFont, new Size(500, 0), TextFormatFlags.WordBreak) + new Size(16, 10);
        };
        tooltips.Draw += (_, e) =>
        {
            using var background = new SolidBrush(palette.Key); e.Graphics.FillRectangle(background, e.Bounds);
            using var border = new Pen(palette.Border); e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, tooltipFont, Rectangle.Inflate(e.Bounds, -7, -4), palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        };
        Text = "Aster · Calculator"; Ui.SetFont(this, 10); KeyPreview = true;
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(480, 680); MinimumSize = new(380, 460); StartPosition = FormStartPosition.Manual; WindowState = FormWindowState.Normal;
        using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ModernCalculator.WinForms.Resources.aster.ico");
        if (iconStream is not null) { applicationIcon = new Icon(iconStream); Icon = applicationIcon; }
        header.Padding = new(6, 0, 0, 0);
        header.ColumnStyles.Clear(); header.ColumnStyles.Add(new(SizeType.Absolute, 104)); header.ColumnStyles.Add(new(SizeType.Percent, 100));
        for (var column = 2; column < 10; column++) header.ColumnStyles.Add(new(SizeType.Absolute, column < 7 ? 28 : 32));
        var brand = new BrandMark(); header.Controls.Add(brand, 0, 0); RegisterDragSurface(brand); RegisterDragSurface(header);
        var dragArea = new Panel { Dock = DockStyle.Fill, AccessibleName = "Drag window" }; header.Controls.Add(dragArea, 1, 0); RegisterDragSurface(dragArea);
        themeButton = Ui.Button("", ToggleTheme, ButtonTone.Quiet, IconKind.Sun, "Switch light/dark theme");
        drawerButton = Ui.Button("", () => SetDrawer(!drawerVisible), ButtonTone.Quiet, IconKind.History, "Show or hide history and memory");
        header.Controls.Add(Ui.Button("", CopyResult, ButtonTone.Quiet, IconKind.Copy, "Copy result (Ctrl+C)"), 2, 0);
        header.Controls.Add(Ui.Button("", PasteResult, ButtonTone.Quiet, IconKind.Paste, "Paste value (Ctrl+V)"), 3, 0);
        header.Controls.Add(themeButton, 4, 0); header.Controls.Add(drawerButton, 5, 0);
        header.Controls.Add(Ui.Button("", ShowSettings, ButtonTone.Quiet, IconKind.Settings, "Preferences"), 6, 0);
        header.Controls.Add(Ui.Button("", () => WindowState = FormWindowState.Minimized, ButtonTone.Quiet, IconKind.Minimize, "Minimize window"), 7, 0);
        maximizeButton = Ui.Button("", ToggleMaximize, ButtonTone.Quiet, IconKind.Maximize, "Maximize window");
        header.Controls.Add(maximizeButton, 8, 0);
        header.Controls.Add(Ui.Button("", Close, ButtonTone.Quiet, IconKind.Close, "Close window"), 9, 0);
        root.Controls.Add(header, 0, 0);
        modeNavigation.Margin = new(4, 3, 4, 4); root.Controls.Add(modeNavigation, 0, 1);
        workspace.ColumnStyles.Clear(); workspace.ColumnStyles.Add(new(SizeType.Percent, 100)); workspace.ColumnStyles.Add(new(SizeType.Absolute, 0));
        workspace.Padding = new(4, 3, 4, 3); center.Padding = new(0);
        center.Controls.Add(viewHost, 0, 0); workspace.Controls.Add(center, 0, 0);
        BuildDrawer(); drawer.Visible = false; workspace.Controls.Add(drawer, 1, 0); root.Controls.Add(workspace, 0, 2);
        status.Padding = new(6, 0, 6, 0); status.AutoEllipsis = true; root.Controls.Add(status, 0, 3); Controls.Add(root);
        modeNavigation.SelectedIndexChanged += (_, _) => SwitchMode(new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" }[modeNavigation.SelectedIndex]);
        context.JournalChanged += () => { CancelDrawerClear(); RefreshDrawer(); if (context.Settings.PersistHistory) SaveState(); };
        context.MemoryChanged += () => { CancelDrawerClear(); RefreshDrawer(); };
        context.StatusChanged += message =>
        {
            var text = persistenceWarning is not null && !message.Contains(persistenceWarning, StringComparison.Ordinal)
                ? message + " Storage warning: changes may not be saved." : message;
            status.Text = text; status.AccessibleName = text; status.AccessibleDescription = text; tooltips.SetToolTip(status, text);
            status.ForeColor = persistenceWarning is null ? palette.Muted : palette.Error; Ui.Announce(status, text, persistenceWarning is not null);
        };
        context.ShowMemoryRequested += () => { drawerTabs.SelectedIndex = 1; memoryTab = true; SetDrawer(true); RefreshDrawer(); };
        context.ShowHistoryRequested += () => { drawerTabs.SelectedIndex = 0; memoryTab = false; SetDrawer(true); RefreshDrawer(); };
        context.ConversionFavoritesChanged += SaveState;
        SwitchMode(context.Settings.Mode); ApplyPalette();
        if (loaded.Warning is not null) context.Status(loaded.Warning);
        SetTooltips(this);
        SystemEvents.UserPreferenceChanged += OnSystemPreference;
        FormClosing += (_, _) => SaveState();
        Shown += (_, _) => Current.FocusInput();
        layoutReady = true;
        viewHost.SizeChanged += (_, _) => UpdateViewportPadding();
        ResumeLayout(true);
    }

    private void SwitchMode(string name)
    {
        if (name is not ("Standard" or "Scientific" or "Programmer" or "Converter" or "Dates")) return;
        if (name == mode && views.TryGetValue(name, out var current) && current.Parent == viewHost)
        { if (Visible) current.FocusInput(); return; }
        if (!views.ContainsKey(name))
        {
            // First visits pay construction cost; subsequent visits retain input and subscriptions.
            views.Add(name, name switch
            {
                "Scientific" => new ScientificView(context), "Programmer" => new ProgrammerView(context),
                "Converter" => new ConverterView(context), "Dates" => new DateView(context), _ => new StandardView(context)
            });
        }
        keyboardActionNavigation = false;
        // Detach cached views without disposing them so mode changes preserve session input.
        viewHost.Controls.Clear(); mode = name; viewHost.Controls.Add(Current);
        var index = Array.IndexOf(new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" }, name);
        if (modeNavigation.SelectedIndex != index) modeNavigation.SelectedIndex = index;
        context.Settings = context.Settings with { Mode = mode };
        Ui.Theme(Current, palette); Current.RefreshFormatting(); if (Visible) Current.FocusInput();
        if (Visible) { ApplyWindowSizing(Screen.FromControl(this).WorkingArea, false); SetTooltips(Current); }
        context.Status(name switch { "Standard" => "100-digit input · Enter to calculate · F1 help",
            "Scientific" => "Approximate binary64 arithmetic · F1 explains precision and domains",
            "Programmer" => "Four representations of the same integer · Select a row to change input base",
            "Converter" => "Conversions update as you type",
            _ => "Dates use YYYY-MM-DD · Choose elapsed or inclusive counting · F1 help" });
    }
    private void ApplyPalette()
    {
        palette = Palette.Resolve(context.Settings.Theme, context.Settings.HighContrast); Ui.Theme(this, palette);
        PresentationSettings.ReducedMotion = context.Settings.ReducedMotion;
        BackColor = root.BackColor = header.BackColor = workspace.BackColor = palette.Window;
        foreach (Control child in header.Controls) child.BackColor = palette.Window;
        status.BackColor = palette.Window; status.ForeColor = persistenceWarning is null ? palette.Muted : palette.Error;
        foreach (var view in views.Values) Ui.Theme(view, palette);
        themeButton.Icon = palette.Dark ? IconKind.Sun : IconKind.Moon; themeButton.Invalidate();
        TopMost = context.Settings.AlwaysOnTop;
        var dark = palette.Dark ? 1 : 0;
        if (IsHandleCreated) _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        RefreshDrawer(); Invalidate(true);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); var dark = palette.Dark ? 1 : 0; _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); PrepareWindowLayout(Screen.FromControl(this).WorkingArea); }
    public void PrepareWindowLayout(Rectangle workingArea) { WindowState = FormWindowState.Normal; ApplyWindowSizing(workingArea, true); }
    private void ApplyWindowSizing(Rectangle area, bool centerWindow, int widthAdjustment = 0)
    {
        var requestedSize = new Size(Width + widthAdjustment, Height);
        var metrics = WindowLayoutPolicy.Calculate(mode, area.Size, DeviceDpi, drawerVisible, context.Settings.Density == DensityPreference.Compact);
        MinimumSize = metrics.MinimumSize;
        if (WindowState == FormWindowState.Normal)
        {
            // Startup uses the preferred size; subsequent changes respect the user's resize.
            Size = centerWindow ? metrics.InitialSize : new(
                Math.Clamp(requestedSize.Width, metrics.MinimumSize.Width, area.Width),
                Math.Clamp(requestedSize.Height, metrics.MinimumSize.Height, area.Height));
            if (centerWindow) Location = new(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            KeepWindowInWorkingArea(area);
        }
        UpdateResponsiveChrome();
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (!layoutReady) return;
        // The recorded expansion is in physical pixels, just like the scaled window bounds.
        drawerWidthExpansion = (int)Math.Round(drawerWidthExpansion * (double)e.DeviceDpiNew / e.DeviceDpiOld);
        ApplyWindowSizing(Screen.FromControl(this).WorkingArea, false);
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (layoutReady && IsHandleCreated) UpdateResponsiveChrome(); }
    private void UpdateResponsiveChrome()
    {
        if (!layoutReady) return;
        var scale = DeviceDpi / 96f; var compact = ClientSize.Height / scale < 520;
        root.RowStyles[0].Height = (compact ? 36 : 44) * scale;
        root.RowStyles[1].Height = (compact ? 34 : 60) * scale;
        root.RowStyles[3].Height = (compact ? 18 : 22) * scale;
        maximizeButton.Icon = WindowState == FormWindowState.Maximized ? IconKind.Restore : IconKind.Maximize;
        maximizeButton.AccessibleName = WindowState == FormWindowState.Maximized ? "Restore window" : "Maximize window";
        maximizeButton.AccessibleDescription = WindowState == FormWindowState.Maximized ? "Restore the window to its normal size" : "Maximize within the monitor working area";
        maximizeButton.Invalidate();
        UpdateViewportPadding();
    }
    private void UpdateViewportPadding()
    {
        if (!layoutReady) return;
        var scale = DeviceDpi / 96f;
        var maximumWidth = (mode == "Standard" ? 560 : mode == "Programmer" ? 800 : 760) * scale;
        var horizontal = Math.Max(0, (int)((viewHost.ClientSize.Width - maximumWidth) / 2));
        var vertical = Math.Max(0, (int)((viewHost.ClientSize.Height - 760 * scale) / 2));
        var spacing = new Padding(horizontal, vertical, horizontal, vertical);
        if (viewHost.Padding != spacing) viewHost.Padding = spacing;
        var navInset = Math.Max((int)(4 * scale), (int)((ClientSize.Width - 900 * scale) / 2));
        modeNavigation.Margin = new(navInset, (int)(3 * scale), navInset, (int)(4 * scale));
    }
    private void ToggleTheme()
    {
        context.Settings = context.Settings with { Theme = palette.Dark ? ThemePreference.Light : ThemePreference.Dark };
        ApplyPalette(); SaveState();
    }
    private void ShowSettings()
    {
        var inputFocus = CaptureInputFocus();
        using var settingsForm = new SettingsForm(context.Settings, palette) { Icon = Icon };
        var result = settingsForm.ShowDialog(this);
        // Apply the preferences draft only after the dialog is accepted.
        if (result == DialogResult.OK)
        {
            context.Settings = settingsForm.Result;
            if (settingsForm.ClearHistoryRequested) { context.History.Clear(); context.NotifyHistory(); }
            ApplyPalette(); foreach (var view in views.Values) view.RefreshFormatting(); ApplyWindowSizing(Screen.FromControl(this).WorkingArea, false);
            if (TrySaveState()) context.Status(settingsForm.ClearHistoryRequested ? "Preferences saved. Calculation history cleared." : "Preferences saved.");
        }
        RestoreInputFocus(inputFocus);
    }
    private void SetDrawer(bool visible)
    {
        if (!visible) CancelDrawerClear();
        var changed = drawerVisible != visible;
        var delta = DesignTokens.DrawerWidth * DeviceDpi / 96;
        drawerVisible = visible; drawer.Visible = visible; workspace.ColumnStyles[1].Width = visible ? delta : 0;
        var area = Screen.FromControl(this).WorkingArea;
        if (changed && Visible) ResizeForDrawer(area, visible);
        KeepWindowInWorkingArea(area);
        drawerButton.Selected = visible; drawerButton.Invalidate(); if (visible) RefreshDrawer();
    }
    private void ResizeForDrawer(Rectangle area, bool opening)
    {
        var previousWidth = Width;
        var adjustment = opening ? DesignTokens.DrawerWidth * DeviceDpi / 96 : -drawerWidthExpansion;
        ApplyWindowSizing(area, false, adjustment);
        // A monitor edge may limit expansion; closing returns only the space actually added.
        drawerWidthExpansion = opening ? Math.Max(0, Width - previousWidth) : 0;
    }
    private void KeepWindowInWorkingArea(Rectangle area)
    {
        if (WindowState != FormWindowState.Normal) return;
        Location = new(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }
    private void SaveState() => TrySaveState();
    private bool TrySaveState()
    {
        persistenceWarning = store.Save(context.Settings, context.History.Items, context.ConversionFavorites);
        if (persistenceWarning is null) return true;
        context.Status("Changes apply for this session. " + persistenceWarning + " Saved data may still be on disk; retry saving preferences.");
        return false;
    }
    private void OnSystemPreference(object sender, UserPreferenceChangedEventArgs e)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() => { if (!IsDisposed && !Disposing) ApplyPalette(); });
        }
        // SystemEvents is raised on a different thread; the handle can disappear after the check.
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
    }
    private void SetTooltips(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is ButtonBase or TextBox or FlatNumberInput && !string.IsNullOrEmpty(child.AccessibleName))
            {
                tooltips.SetToolTip(child, TooltipText(child));
                if (!tooltipControls.TryGetValue(child, out _))
                {
                    tooltipControls.Add(child, new object());
                    child.MouseEnter += RefreshTooltipBeforeHover;
                    child.MouseDown += ResetActionNavigation;
                }
            }
            SetTooltips(child);
        }
    }
    public void RefreshTooltips(Control scope) => SetTooltips(scope);
    private void RefreshTooltipBeforeHover(object? sender, EventArgs e)
    {
        if (sender is Control control && !control.IsDisposed)
        {
            var text = TooltipText(control);
            if (tooltips.GetToolTip(control) != text) tooltips.SetToolTip(control, text);
        }
    }
    private static string TooltipText(Control control) => (control.AccessibleName ?? control.Text) + (string.IsNullOrWhiteSpace(control.AccessibleDescription) ? "" : "\r\n" + control.AccessibleDescription);
    private void ShowHelp()
    {
        var inputFocus = CaptureInputFocus();
        using var help = new HelpForm(context.Settings, palette) { Icon = Icon };
        help.ShowDialog(this); RestoreInputFocus(inputFocus);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnSystemPreference; tooltips.Dispose(); tooltipFont.Dispose(); applicationIcon?.Dispose();
            foreach (var view in views.Values) view.Dispose();
        }
        base.Dispose(disposing);
    }
}
