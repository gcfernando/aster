// Developer ::> Gehan Fernando
using System.Drawing.Drawing2D;

namespace ModernCalculator.WinForms.Controls;

public enum IconKind { None, Aster, Standard, Scientific, Programmer, Converter, Calendar, History, Memory, Settings, Sun, Moon, Copy, Paste, Trash, Backspace, Swap, Chevron, Close, Check, Minimize, Maximize, Restore }

/// <summary>Original offline vector icon set on a consistent 24-unit grid.</summary>
public static class VectorIcon
{
    public static void Draw(Graphics graphics, IconKind kind, RectangleF bounds, Color color)
    {
        var saved = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 24, bounds.Height / 24);
        using var pen = new Pen(color, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        void Line(float x1, float y1, float x2, float y2) => graphics.DrawLine(pen, x1, y1, x2, y2);
        void Box(float x, float y, float width, float height) => graphics.DrawRectangle(pen, x, y, width, height);
        switch (kind)
        {
            case IconKind.Minimize: Line(5, 12, 19, 12); break;
            case IconKind.Maximize: Box(5, 5, 14, 14); break;
            case IconKind.Restore: Box(4, 8, 12, 12); graphics.DrawLines(pen, [new(8, 8), new(8, 4), new(20, 4), new(20, 16), new(16, 16)]); break;
            case IconKind.Aster:
                graphics.FillPolygon(brush, [new(12, 1), new(15, 9), new(23, 12), new(15, 15), new(12, 23), new(9, 15), new(1, 12), new(9, 9)]);
                break;
            case IconKind.Standard:
                Box(5, 2.5f, 14, 19); Box(8, 6, 8, 4);
                for (var row = 0; row < 2; row++) for (var col = 0; col < 3; col++) graphics.FillEllipse(brush, 8 + col * 3.5f, 13 + row * 4, 1.7f, 1.7f);
                break;
            case IconKind.Scientific:
                Line(8, 3, 16, 3); Line(10, 3, 10, 10); Line(14, 3, 14, 10);
                graphics.DrawLines(pen, [new(10, 10), new(4, 19), new(5, 21), new(19, 21), new(20, 19), new(14, 10)]);
                Line(7, 16, 17, 16); break;
            case IconKind.Programmer:
                graphics.DrawLines(pen, [new(8, 5), new(2, 12), new(8, 19)]);
                graphics.DrawLines(pen, [new(16, 5), new(22, 12), new(16, 19)]); Line(14, 3, 10, 21); break;
            case IconKind.Converter:
            case IconKind.Swap:
                Line(3, 7, 20, 7); graphics.DrawLines(pen, [new(16, 3), new(20, 7), new(16, 11)]);
                Line(21, 17, 4, 17); graphics.DrawLines(pen, [new(8, 13), new(4, 17), new(8, 21)]); break;
            case IconKind.Calendar:
                Box(3, 5, 18, 16); Line(3, 10, 21, 10); Line(8, 2, 8, 7); Line(16, 2, 16, 7);
                graphics.FillEllipse(brush, 7, 13, 3, 3); graphics.FillEllipse(brush, 14, 13, 3, 3); break;
            case IconKind.History:
                graphics.DrawArc(pen, 4, 4, 17, 17, 205, 310); Line(3, 3, 3, 9); Line(3, 9, 9, 9); Line(12, 7, 12, 13); Line(12, 13, 16, 15); break;
            case IconKind.Memory:
                Box(5, 5, 14, 14); Line(9, 15, 9, 9); Line(9, 9, 12, 12); Line(12, 12, 15, 9); Line(15, 9, 15, 15);
                for (var x = 8; x <= 16; x += 4) { Line(x, 2, x, 5); Line(x, 19, x, 22); } break;
            case IconKind.Settings:
                Line(3, 6, 21, 6); Line(3, 12, 21, 12); Line(3, 18, 21, 18);
                graphics.FillRectangle(brush, 7, 3, 3, 6); graphics.FillRectangle(brush, 15, 9, 3, 6); graphics.FillRectangle(brush, 10, 15, 3, 6); break;
            case IconKind.Sun:
                graphics.DrawEllipse(pen, 7, 7, 10, 10);
                for (var i = 0; i < 8; i++) { var a = i * Math.PI / 4; Line(12 + (float)Math.Cos(a) * 8, 12 + (float)Math.Sin(a) * 8, 12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10); } break;
            case IconKind.Moon:
                graphics.DrawArc(pen, 3, 3, 18, 18, 65, 285); graphics.DrawArc(pen, 10, 1, 14, 16, 80, 180); break;
            case IconKind.Copy:
                Box(8, 7, 12, 14); graphics.DrawLines(pen, [new(15, 4), new(4, 4), new(4, 17)]); break;
            case IconKind.Paste:
                Box(5, 5, 14, 16); graphics.FillRectangle(brush, 9, 2, 6, 5); Line(9, 12, 15, 12); Line(9, 16, 15, 16); break;
            case IconKind.Trash:
                Line(4, 6, 20, 6); Line(9, 3, 15, 3); graphics.DrawLines(pen, [new(6, 6), new(7, 21), new(17, 21), new(18, 6)]); Line(10, 10, 10, 17); Line(14, 10, 14, 17); break;
            case IconKind.Backspace:
                graphics.DrawPolygon(pen, [new(9, 5), new(22, 5), new(22, 19), new(9, 19), new(2, 12)]); Line(12, 9, 18, 15); Line(18, 9, 12, 15); break;
            case IconKind.Chevron: Line(9, 6, 15, 12); Line(15, 12, 9, 18); break;
            case IconKind.Close: Line(6, 6, 18, 18); Line(18, 6, 6, 18); break;
            case IconKind.Check: graphics.DrawLines(pen, [new(4, 12), new(10, 18), new(20, 6)]); break;
        }
        graphics.Restore(saved);
    }

    public static GraphicsPath Rounded(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }
}
