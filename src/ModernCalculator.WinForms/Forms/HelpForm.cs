// Developer ::> Gehan Fernando
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Forms;

public sealed class HelpForm : BorderlessForm
{
    protected override bool ResizableFrame => false;
    private readonly TableLayoutPanel body = Ui.Table(1, -1, -1, -1, -1);
    private readonly SegmentedControl sections = new("Help topic", ["Keyboard", "Functions", "Numbers", "Window", "Support"]);
    private readonly Label pageLabel = Ui.Label("", 9);
    private readonly CalcButton previous;
    private readonly CalcButton next;
    private readonly string[][] topics;
    private readonly Palette palette;
    private readonly Label supportFeedback = Ui.Label("", 8);
    private int page;
    public HelpForm(AppSettings settings, Palette palette)
    {
        SuspendLayout(); this.palette = palette; Text = "Aster · Help & shortcuts"; AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Ui.SetFont(this, 10); ClientSize = new(620, 520); MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
        var modifier = settings.ModeShortcuts == ModeShortcutPreference.ControlAltNumbers ? "Ctrl+Alt" : "Ctrl";
        topics = [
            ["Enter or =|Calculate; Enter explicitly saves Converter/Date calculations to history.",
             "Digits and + − × ÷|Type numbers and operators in Standard/Programmer. Scientific has direct expression editing.",
             "Backspace / Delete / Escape|Delete the last input digit / clear the current entry / clear the calculation. CE preserves a pending Standard operation.",
             "Ctrl+Z / Ctrl+Y|Undo/redo Standard actions. Editable expressions and text fields retain native editing/undo keys.",
             "Ctrl+C / Ctrl+V|Copy the complete result, or selected text. Paste into a field, or parse a value in Standard/Programmer.",
             modifier + "+1 through +5|Switch Standard, Scientific, Programmer, Converter, Dates. Change this mapping in Preferences → Window.",
             "Ctrl+H|Open or close searchable history and session memory. MA opens all memory slots.",
             "Alt+1 through +4|Programmer input base: Decimal, Hexadecimal, Octal, Binary.",
             "Tab / Shift+Tab / Space|Move keyboard focus and activate a focused button or checkbox. Arrows/Home/End choose segments.",
             "F1 / Escape|Open this guide. Escape closes help/preferences or cancels an inline date calendar.",
             "Expression editing|Select a portion of an expression and choose a function to wrap it. Use parentheses and explicit multiplication.",
             "Calendar keyboard|Arrows move a day/week; Page Up/Down changes month. Today and Cancel are explicit controls."],
            ["Scientific function arguments|root(x,n) is the nth root; logbase(x,a) is logarithm of x in base a. Both require two arguments.",
             "Argument separators|Use a comma in dot-decimal locales; use a semicolon when the decimal separator is a comma.",
             "EXP versus eˣ|EXP enters a decimal exponent: 1.2 EXP 3 means 1.2 × 10³. eˣ calculates the exponential function.",
             "Angles and function category|DEG/RAD controls trigonometric input. Choose Trigonometry or Hyperbolic for inverse functions as well.",
             "Answer recall|Ans inserts the last successful Scientific answer without changing the stored answer when an edit is invalid.",
             "Percentages|Standard: 200 + 10 % = 220; 200 × 10 % = 20. Scientific/Programmer % means remainder.",
             "Operator precedence|Standard evaluates immediately. Scientific applies parentheses and precedence: 2 + 3 × 4 = 14.",
             "Memory actions|MA: all slots; MR: recall newest; MS: store a new slot; M+/M−: update newest; MC: clear session memory."],
            ["Input capacity|Standard accepts 100 significant input digits. Leading zeros do not count. Excess input is rejected with an explanation.",
             "Computation precision|Standard uses bounded decimal arithmetic. Scientific and Converter use binary64 (about 15–17 representation digits); accuracy depends on the calculation and input conditioning.",
             "Display rounding|Preferences → Numbers sets 3–15 displayed significant digits for Scientific and Converter. It does not change stored values.",
             "Long results|The result fits and wraps; digit-page controls expose any remaining text. Copy result always copies the complete stored value.",
             "Programmer integers|8/16/32/64-bit arithmetic wraps to the selected width. Signed values use two’s complement. Signed right shift extends the sign bit.",
             "Regional input|Decimal separators and grouping follow the current region. Date inputs use unambiguous YYYY-MM-DD.",
             "Validation|Invalid input, domains, division by zero and unsupported ranges produce explanatory errors. Correct input or clear to continue."],
            ["Window controls|Drag the brand or empty header. Resize at edges/corners. Double-click the drag surface or use maximize/restore.",
             "Startup and density|Aster opens centered in a normal working-area-sized window. Preferences offers Automatic or Compact density.",
             "Theme and accessibility|System follows Windows. High contrast uses system colors. Reduce hover motion disables hover animation.",
             "Predictable memory placement|Standard places memory above its keypad. Scientific places it below to keep expression controls together.",
             "History and privacy|History holds 200 calculations. Optional persistence stores expressions/results/modes/timestamps locally; memory is session-only.",
             "Managing history|Open History to reuse, copy, search or delete. Preferences → History stages clearing; Save commits it and Cancel discards it.",
             "Saved conversion pairs|Save pair stores source/destination units. Favorites reopens the pair; Recent opens previous conversions.",
             "Preferences Save/Cancel|Changes are drafts. Save applies them; Cancel, X and Escape discard them. Restore defaults also stays a draft."],
            ["Aster Calculator " + SupportInfo.Version + "|Build: " + SupportInfo.Build,
             "Local environment|" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + " · " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + " · " + System.Globalization.CultureInfo.CurrentCulture.Name,
             "Private by default|Copy includes version, runtime, OS, culture, mode, theme and DPI. Calculations, history, memory, usernames and profile paths are excluded. Nothing is sent automatically.",
             "Reporting a problem|" + SupportInfo.IssueTracker + "\r\nDescribe the steps and expected result; use sample input instead of private calculations. Aster is fully enabled and requires no account or activation."]
        ];
        var root = Ui.Table(1, 42, 40, -100, 0, 34); root.Padding = new(14, 6, 14, 10);
        var heading = Ui.Table(2, -100); heading.ColumnStyles[0].Width = 90; heading.ColumnStyles[1].Width = 10;
        var title = Ui.Label("Help & keyboard shortcuts", 14, true); RegisterDragSurface(title); RegisterDragSurface(heading);
        var close = Ui.Button("", () => { DialogResult = DialogResult.Cancel; Close(); }, ButtonTone.Quiet, IconKind.Close, "Close help");
        heading.Controls.Add(title, 0, 0); heading.Controls.Add(close, 1, 0); CancelButton = close;
        root.Controls.Add(heading, 0, 0); root.Controls.Add(sections, 0, 1); root.Controls.Add(body, 0, 2);
        var pager = Ui.Table(3, -100); pager.ColumnStyles[0].Width = 25; pager.ColumnStyles[1].Width = 50; pager.ColumnStyles[2].Width = 25;
        previous = Ui.Button("Previous", () => { page--; Rebuild(); }, name: "Previous help page");
        next = Ui.Button("Next", () => { page++; Rebuild(); }, name: "Next help page");
        pageLabel.TextAlign = ContentAlignment.MiddleCenter; pager.Controls.Add(previous, 0, 0); pager.Controls.Add(pageLabel, 1, 0); pager.Controls.Add(next, 2, 0);
        var supportActions = Ui.Table(3, -100); supportActions.Visible = false;
        supportActions.Controls.Add(Ui.Button("Copy support information", () =>
        {
            var message = SupportInfo.Copy(SupportInfo.Capture(settings, palette, DeviceDpi));
            supportFeedback.Text = message.StartsWith("Support information copied", StringComparison.Ordinal) ? "Copied. Nothing was sent." : "Clipboard busy. Try again.";
            supportFeedback.AccessibleDescription = message; Ui.Announce(supportFeedback, message);
        }, name: "Copy private support information"), 0, 0);
        supportActions.Controls.Add(Ui.Button("Support site", () =>
        {
            var message = SupportInfo.OpenIssueTracker();
            supportFeedback.Text = message.StartsWith("Support site opened", StringComparison.Ordinal) ? "Opened. Nothing was sent." : "Open the address shown above.";
            supportFeedback.AccessibleDescription = message; Ui.Announce(supportFeedback, message);
        }, name: "Open Aster issue tracker in browser"), 1, 0);
        supportFeedback.AccessibleName = "Support copy feedback"; supportActions.Controls.Add(supportFeedback, 2, 0);
        root.Controls.Add(supportActions, 0, 3); root.Controls.Add(pager, 0, 4); Controls.Add(root);
        sections.SelectedIndexChanged += (_, _) =>
        {
            var support = sections.SelectedIndex == 4;
            supportActions.Visible = support; root.RowStyles[3].Height = support ? 40 * DeviceDpi / 96f : 0;
            page = 0; Rebuild();
        }; body.SizeChanged += (_, _) => Rebuild();
        ResumeLayout(true); Rebuild(); Ui.Theme(this, palette);
    }
    private bool rebuilding;
    // Adjust the number of help items per page to keep every topic reachable without scrolling.
    private void Rebuild()
    {
        if (rebuilding || IsDisposed) return; rebuilding = true;
        try
        {
            var compact = body.ClientSize.Height < 300 * DeviceDpi / 96;
            var count = sections.SelectedIndex == 4 ? compact ? 1 : 2 : compact ? 2 : 4;
            var items = topics[sections.SelectedIndex]; var pageCount = (items.Length + count - 1) / count; page = Math.Clamp(page, 0, pageCount - 1);
            body.SuspendLayout(); while (body.Controls.Count > 0) { var control = body.Controls[0]; body.Controls.RemoveAt(0); control.Dispose(); }
            body.RowCount = count; body.RowStyles.Clear(); for (var index = 0; index < count; index++) body.RowStyles.Add(new(SizeType.Percent, 100f / count));
            foreach (var item in items.Skip(page * count).Take(count))
            {
                var parts = item.Split('|', 2); var row = Ui.Table(1, 24 * DeviceDpi / 96f, -100); row.Margin = new(4, 3, 4, 3);
                row.Controls.Add(Ui.Label(parts[0], 10, true), 0, 0); var detail = Ui.Label(parts[1], 9); detail.TextAlign = ContentAlignment.TopLeft; row.Controls.Add(detail, 0, 1);
                body.Controls.Add(row, 0, body.Controls.Count);
            }
            previous.Enabled = page > 0; next.Enabled = page + 1 < pageCount; pageLabel.Text = $"{page + 1} / {pageCount}";
            Ui.Theme(body, palette); body.ResumeLayout(true); Ui.AttachTooltips(this);
        }
        finally { rebuilding = false; }
    }
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); var area = Screen.FromControl(Owner ?? this).WorkingArea; var inset = 16 * DeviceDpi / 96;
        Size = new(Math.Min(Width, area.Width - inset * 2), Math.Min(Height, area.Height - inset * 2));
        Location = new(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }
}
