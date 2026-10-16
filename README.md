# Aster Calculator

A native Windows calculator built with C# 14, .NET 10 and Windows Forms. Aster uses flat paper/charcoal surfaces, a blue accent, compact horizontal navigation with original vector icons, and selectable numeric results across five modes. The borderless window has custom minimize, maximize/restore and close controls, native window dragging and edge/corner resizing. All calculation features work offline. Calculator options use visible buttons, radio segments and paged unit choices; there are no application dropdowns, popup function menus or scrolling panels.

## Build and run

Requirements: Windows 10/11 and the **.NET 10 SDK**. For the Visual Studio designer/build tools, use **Visual Studio 2026 (18+)** with the **.NET desktop development** workload. Visual Studio 2022 cannot officially target .NET 10; see [Microsoft's SDK/Visual Studio compatibility documentation](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

Open `ModernCalculator.sln`. In **Solution Explorer**, right-click **ModernCalculator.WinForms** and select **Set as Startup Project**, then press **F5**. The Start dropdown should show `ModernCalculator.WinForms`, not `ModernCalculator.Core`. The executable project appears first in the solution file for fresh opens; Visual Studio can retain a previously selected startup project in its per-user settings.

If Visual Studio reports that a class library cannot be started directly, dismiss the message and select `ModernCalculator.WinForms` as described above. Core and Infrastructure are libraries used by the app and should keep their library output type. See [Microsoft's instructions for running a multi-project solution](https://learn.microsoft.com/en-us/visualstudio/get-started/csharp/run-program?view=visualstudio).

Or run from this directory:

```powershell
dotnet restore ModernCalculator.sln
dotnet build ModernCalculator.sln -c Release -m:1
dotnet run --project src/ModernCalculator.WinForms -c Release --no-build
```

`global.json` accepts the latest installed .NET 10 feature band at or above 10.0.100. Application code has no third-party dependencies. Only the test project restores NuGet packages.

## Modes

| Mode | Included behavior |
| --- | --- |
| Standard | 100-digit decimal input, exact bounded arithmetic, persistent expression, immediate execution, percentages, square/root/reciprocal, sign, CE/C, backspace, repeated equals, memory, undo/redo |
| Scientific | Editable expressions, operand templates, Ans, precedence, right-associative powers, roots, factorial, constants, logarithms, trig/inverse trig, hyperbolic/inverse hyperbolic functions, degrees/radians, exponent notation |
| Programmer | Editable selected-base representation, synchronized DEC/HEX/OCT/BIN, 8/16/32/64-bit integers, signed/unsigned wrapping arithmetic, bitwise operations, shifts, remainder, paged bit editor |
| Converter | Twelve categories, paired value/unit groups, searchable names/symbols/aliases, live results, favorites, recent conversions, swapping and explicit history saving |
| Dates | Live elapsed or inclusive difference, calendar breakdown, inline calendars, Today/Swap/Reset, signed interval adjustment and month-end clamping |

Scientific functions are available on the keypad, in the visible **Powers & logs / Trigonometry / Hyperbolic** banks, and by typing:

```text
sqrt(144) + 2^3
sin(30) + logbase(81,3)
root(-32,5)
asinh(2) + acosh(3) + atanh(0.5)
```

In comma-decimal cultures, use `;` between function arguments: `logbase(81;3)`. Decimal input accepts the locale separator; scientific expression literals also accept a dot. Grouping separators are intentionally excluded from scientific expressions to keep argument syntax unambiguous. Use explicit multiplication (`2*pi`, not `2pi`). `%` means remainder in Scientific and Programmer; Standard uses percentage semantics.

Standard places its memory bar above the keypad; Scientific places it below the keypad. Both provide **MA**, **MR**, **MS**, **M+**, **M−** and **MC**. MA opens all memory slots in the secondary panel, MS creates a new slot, MR recalls the newest slot, M+/M− update that slot, and MC clears all memory. Up to 20 exact decimal slots are retained for the current session, including bounded Standard results longer than 100 digits. Use the panel's previous/next buttons to inspect slots and **Use result** to recall one into the active mode where representable. Scientific recall uses binary64 precision; the saved memory value remains unchanged.

Programmer shows **Decimal, Hexadecimal, Octal and Binary** as four representations of the same integer. Choose a base, or press Alt+1–4, to edit that row directly; the other rows remain calculated. The active base has an underline and accent, and unsupported keypad digits are disabled. Each valid representation has a copy button; incomplete input cannot copy a stale value. Binary is grouped in four-bit blocks and padded to the selected word size. Decimal **255** is hex **FF**, octal **377** and binary **11111111** (with leading zeroes to fill the word). **Keypad / Bit editor** selects the workspace; labeled bit ranges expose every bit without scrolling.

Converter shows all twelve categories as buttons. Search each From/To group by name, symbol or common English alias, such as `feet`, `meter` or `°C`; Enter chooses the first visible match on the current page. Three matches appear normally, or one in the compact layout. **Back / More**, arrows, Home/End and Page Up/Down reach every match. **Swap units** reverses the pair. **Save pair** stores the category and units in Favorites independently of calculation history. **Save conversion**, or Enter in the value field, records a completed conversion in Recent/history. Recent restores its value and pair. Swap/save actions are disabled while browsing Favorites or Recent. The factor or temperature formula and the display/calculation precision appear below the controls.

Dates separates **Days between dates** from **Adjust a date**. The populated dates immediately show a live difference; saving is explicit. Choose elapsed days (same date = 0) or include both dates (same date = 1). ISO entry, Today buttons and inline calendars all work without dropdowns. Compact calendars page through weeks with explicit controls and keyboard navigation. Adjustment accepts signed years/months/weeks/days, applies Add or Subtract in that order, and preserves the separate difference state.

## Everyday controls

| Shortcut | Action |
| --- | --- |
| Digits, `+ - * /` | Standard and Programmer input |
| Enter or `=` | Calculate in Standard, Scientific and Programmer |
| Enter in Converter / Dates | Save a conversion / calculate a date difference |
| Escape | Clear Standard/Scientific/Programmer |
| Delete | Clear entry in Standard; clear Programmer |
| Backspace | Remove last entered character; native text editing in expression/value/date fields |
| F9 | Change sign in Standard |
| `.` or locale decimal separator | Decimal input in Standard |
| Ctrl+C | Copy full stored result; copy a text selection when one exists |
| Ctrl+V | Paste into the focused text field, or parse a numeric value into Standard/Programmer |
| Ctrl+H | Show/hide history and memory panel |
| Ctrl+1 … Ctrl+5 | Switch between the five modes |
| Ctrl+Z / Ctrl+Y | Undo/redo Standard input and calculation state; editable text fields retain native editing |
| Alt+1 … Alt+4 | Select Programmer Decimal / Hexadecimal / Octal / Binary input |
| F1 | Open the built-in paged help and shortcut guide |
| Tab / Shift+Tab / Space | Navigate controls / activate the focused button; Enter also activates actions reached by keyboard navigation |

Header copy/paste icons operate on the active mode. Invalid numeric clipboard input leaves the current value intact. Paste parsing gives the current culture priority (for example, `1.234` means 1234 in German); invariant dot-decimal notation is a fallback. Programmer paste uses the selected base. Date results can be reused only if they are in `YYYY-MM-DD` format; day counts remain human-readable history entries.

Completed calculations appear in the searchable history panel. One full result card is shown at a time, with previous/next buttons and a position/count indicator; all 200 retained calculations are reachable. Search covers the full journal and pages through the matching entries. Use **Use result**, copy or delete on the current card, or clear the journal. Reused history/memory integers preserve their numeric value even when Programmer displays a different base. Repeated intentional equals presses produce separate entries; a single Enter press is routed once. Conversions record only on explicit save or Enter, never on every keystroke.

The sun/moon button switches dark/light appearance. Fresh installs default to Light; saved preferences are preserved. Preferences has four visible sections: Appearance, Numbers, History and Window. It includes follow-system appearance, Automatic/Compact density, reduced hover motion, system high-contrast colors, 3–15 significant display digits for Scientific and Converter with a rounding example, grouping, local history persistence and always-on-top. Choose Ctrl+1–5 or Ctrl+Alt+1–5 for mode shortcuts. Save applies the draft; Cancel, close or Escape discards it. Restore defaults is staged. Clearing saved history requires confirmation and Save. Standard retains its full decimal result independently of the display-digit preference. History starts collapsed and can expand the window within the monitor's working area.

The app opens centered in a normal window, with mode-specific sizes clamped to the monitor's working area. Drag the brand or empty header space to move it, drag an edge/corner to resize, and double-click the drag surface or use the maximize button to maximize/restore. Maximizing respects the current monitor's taskbar. Proportional layouts and compact control states adapt to the available space without AutoScroll. Long results use explicit digit pages when needed; the header and card copy actions always preserve the complete stored value. Windows 11 receives a DWM rounded-corner hint; Windows 10 uses a square window fallback. System rendering can retain square corners when maximized or snapped.

## Numeric contracts and limitations

- Standard uses a bounded base-10 `BigDecimal` backed by `BigInteger`. Typed and pasted mantissas accept up to **100 significant digits**; leading zeroes do not count, while entered trailing zeroes do. Addition, subtraction and multiplication preserve exact results, including results longer than 100 digits. `0.1 + 0.2` is exactly `0.3`. Division, reciprocal and square root round to **100 significant digits, midpoint-to-even**; exact terminating results need no padding. Percentages use the same decimal engine. Excess input precision is rejected instead of silently rounded.
- Standard's normalized coefficient is limited to **2,000 digits**, its scale to **−1,000 through 2,000**, and its nonzero adjusted decimal exponent to **−1,000 through +1,000**. The adjusted exponent is the exponent in normalized scientific notation, so `1e-1000` and `9e1000` are supported. All bounds apply to intermediate and final values; exceeding them produces an explicit error. Memory and history preserve full bounded Standard results, and recalling those stored results can exceed the 100-digit limit for new input. Pasting a copied result follows the new-input limit.
- Standard evaluates sequentially: `2 + 3 × 4 =` gives **20**. Replacing a pending operator does not execute it. Repeated equals uses the last operation/right operand. `200 + 10 % =` gives **220**; subtraction gives **180**; multiplication gives **20**; division gives **2000**. A unary/percentage result is a complete operand; the next digit replaces that operand. A new digit after equals starts a new calculation. Backspace applies only to an editable input; CE preserves a pending binary operation; C/Escape resets it.
- Scientific uses binary64, approximately 15 reliable significant decimal digits. Rounding, cancellation and subnormal values follow that model. Results are not arbitrary precision or symbolic. Complex numbers are unsupported. Overflow, nonfinite results, invalid domains and detected underflow raise readable errors. Factorial supports integers 0–170. `0^0` is rejected. Powers associate right to left; `-2^2` is **−4**, and `2^-2` is **0.25**. Tangent is rejected when `|cos(angle)| < 10^-14` near singularities. Hyperbolic functions are dimensionless. Expression limits: 2048 characters and 64 recursive levels.
- Scientific expressions remain editable after equals; Enter reevaluates the current expression. Editing an expression or changing DEG/RAD invalidates the displayed/copyable result and prompts evaluation, preventing a stale value from being presented under a new angle unit. Keypad operators continue from the last computed value. Keypad numeric input after equals begins a fresh expression. The formatted result uses 3–15 significant digits while copy/history use round-trip (`G17`) precision. Scientific memory stores that decimal round-trip representation of the approximate binary64 result. Recalling an exact Standard memory value into Scientific converts it to binary64 and may round it; nonfinite or underflowing conversions are rejected, and the saved value remains exact.
- Programmer masks every result modulo `2^width`, including parsed out-of-range integers. Signed decimal values use two's complement; other bases show raw bits. Right shift is arithmetic when signed and logical when unsigned. Shift counts must be `0..width−1`, with errors outside that range. Division truncates toward zero. Widening sign-extends signed negative values; narrowing masks. Changing width or signedness cancels a pending operation.
- Conversion factors use binary64, approximately 15 reliable significant decimal digits, with 3–15 significant digits available for display. Validated identity conversions preserve their input; offset-free conversions use the direct scale ratio to avoid unnecessary base-unit overflow/underflow. Temperature conversions retain offset/domain validation. International foot/pound, thermochemical calorie, BTU (IT), mechanical/metric horsepower, US/Imperial volume and decimal/binary data prefixes are explicit. The Julian year is exactly 365.25 days. Kelvin input below zero is rejected; Celsius/Fahrenheit cancellation within four machine epsilons of the offset is normalized at absolute zero. Currency rates are not included.
- Date operations are date-only Gregorian calculations from year 0001 to 9999, without time zones or daylight saving. Default elapsed differences are absolute/exclusive: identical dates are zero days. Inclusive counting adds one. Intervals apply signed years, then months, then weeks/days, clamping at each month/year boundary.
- Numeric result fields are selectable/read-only. `ResultDisplay` fits text to the available space and inserts explicit line breaks; if the complete value still does not fit, previous/next digit-page buttons and a page indicator expose every character. Copying selected display text copies that selection; **Copy result** and each card's copy button copy the full stored value. There are no native result scrollbars. Long expression captions can be abbreviated. The programmer bit grid exposes all 64 bits. Display precision does not change the underlying arithmetic.

## Privacy and persistence

Preferences and up to 20 favorite conversion pairs are saved to `%LOCALAPPDATA%\AsterCalculator\state.json` using a temporary file in the same directory followed by replacement. History persistence defaults to **off**. Disabling it removes saved history on the next successful save while retaining this session's list and favorites. Memory never persists. Missing/corrupt files use defaults. Save failures remain visible alongside later status messages; preferences apply for the session and can be saved again to retry.

Set `ASTER_DATA_DIRECTORY` before launch to choose a portable/testing data directory. Multiple running instances share a last-writer-wins state file; they do not merge histories. State is ordinary local JSON, not encrypted storage. No telemetry, network services or live-rate dependencies are included.

## Solution structure

```text
ModernCalculator.sln
src/
  ModernCalculator.Core/
    Calculations/  Expressions/  Modes/  Conversions/  Memory/  History/
  ModernCalculator.Infrastructure/
    Persistence/  Settings/
  ModernCalculator.WinForms/
    Forms/  Views/  Controls/  Themes/  Resources/  Program.cs
tests/
  ModernCalculator.Tests/    # engine and persistence regression tests
  ModernCalculator.UiChecks/ # hidden native component layout/render checks
tools/Generate-Icon.ps1
docs/IMPLEMENTATION.md
docs/VERIFICATION.md
```

Core has no Windows Forms references. The UI routes mouse and keyboard through the same engine methods. The parser is deterministic and never dynamically evaluates code. Original icons use a shared 24-unit vector grid; the bundled executable icon includes 16/32/48/64/128/256-pixel artwork. Regenerate it with `./tools/Generate-Icon.ps1` on Windows. No third-party artwork is bundled.

## Test and package

The independent review fixed answer-dependent Scientific memory, Programmer unary chaining and leading-sign editing, finite conversion boundary handling, regional history search, Gregorian date consistency, invalid/hidden save actions, paged unit-search Enter behavior, and keyboard/dialog focus routing. Findings, priorities, removals/additions, acceptance criteria and verification limits are in [docs/INDEPENDENT_REVIEW.md](docs/INDEPENDENT_REVIEW.md).

For a live walkthrough with isolated data, build `tests/ModernCalculator.DesktopAudit/ModernCalculator.DesktopAudit.csproj` and run its executable. It hosts the production MainForm and engines, sends no automated input, and stores each new session beneath its output folder's `sessions/` directory. It does not read or modify the user's normal Aster data directory.

```powershell
dotnet test tests/ModernCalculator.Tests -c Release --logger "console;verbosity=minimal"
dotnet build tests/ModernCalculator.UiChecks -c Release -m:1
dotnet run --project tests/ModernCalculator.UiChecks -c Release --no-build -- artifacts/UiChecks
dotnet publish src/ModernCalculator.WinForms -c Release --self-contained false -o artifacts/Aster-Reviewed
```

The framework-dependent output is `artifacts/Aster-Reviewed/Aster.exe` and requires the **.NET 10 Desktop Runtime** on the destination Windows machine. The separate output directory preserves earlier builds. Delivery archives are `artifacts/Aster-reviewed-win-x64.zip` for the runnable files and `artifacts/Aster-reviewed-source.zip` for source. To bundle the runtime for a particular architecture instead, restore/publish with the desired runtime identifier and `--self-contained true`, for example `-r win-x64`. This downloads the matching runtime pack.

Tests exercise arithmetic/state, scientific grammar/domains, base/bit operations, all unit-pair round trips, date boundaries, strict culture-aware input, memory/history, undo/redo and persistence recovery. Engine tests do not open a window. The component harness emits PNGs and JSON diagnostics for both themes, minimum sizes and relative scale simulations; it does not change native Windows DPI. The actual verification record and remaining interactive checks are in [docs/VERIFICATION.md](docs/VERIFICATION.md). [The audit resolution ledger](docs/AUDIT_RESOLUTION.md) maps every source-audit line to implementation, evidence or an explicit external validation limit. Full numeric/design decisions and official design references are in [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md).

