using System.Drawing.Drawing2D;

namespace QNB;

internal static class BrandAssets
{
    public static Bitmap CreateHummingbirdLogo(Color color)
    {
        var bitmap = new Bitmap(150, 190);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, 7F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var thinPen = new Pen(color, 4F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);

        using (var body = new GraphicsPath())
        {
            body.AddBezier(35, 86, 18, 112, 14, 158, 34, 174);
            body.AddBezier(34, 174, 55, 190, 93, 157, 119, 112);
            body.AddBezier(119, 112, 102, 155, 70, 181, 48, 170);
            body.AddBezier(48, 170, 25, 157, 28, 118, 35, 86);
            graphics.FillPath(brush, body);
        }

        graphics.DrawBezier(pen, 28, 112, 54, 91, 76, 67, 106, 57);
        graphics.DrawBezier(pen, 45, 133, 64, 100, 83, 77, 118, 69);
        graphics.DrawBezier(thinPen, 50, 115, 69, 73, 88, 38, 112, 20);
        graphics.DrawBezier(thinPen, 69, 91, 85, 55, 100, 31, 128, 3);
        graphics.FillEllipse(brush, 89, 46, 18, 15);

        using var beak = new GraphicsPath();
        beak.AddPolygon(new[] { new Point(103, 49), new Point(143, 35), new Point(106, 57) });
        graphics.FillPath(brush, beak);

        return bitmap;
    }
}
