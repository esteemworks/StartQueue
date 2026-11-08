using System.Drawing;

namespace StartQueue.UI;

/// <summary>
/// StartQueue's mark on the family tile (<see cref="AppIcons"/>): a play arrow leading a short queue of bars, in white.
/// </summary>
static class Mark
{
    public static void Paint(Graphics g, float s, Brush white)
    {
        var bar = s * 0.105f;
        var rows = new[] { 0.28f, 0.445f, 0.61f };
        var lengths = new[] { 0.40f, 0.30f, 0.20f };
        for (var i = 0; i < rows.Length; i++)
        {
            var y = s * rows[i];
            var x = s * 0.42f;
            using var barPath = AppIcons.RoundedRect(new RectangleF(x, y, s * lengths[i], bar), bar / 2);
            g.FillPath(white, barPath);
        }

        // Play arrow in front of the first bar.
        var top = s * 0.255f;
        g.FillPolygon(white, new[]
        {
            new PointF(s * 0.20f, top),
            new PointF(s * 0.35f, top + s * 0.075f),
            new PointF(s * 0.20f, top + s * 0.15f),
        });
    }

    /// <summary>
    /// Writes the .ico for the exe, the tile as png\StartQueue-&lt;size&gt;.png up to 2048 px and the logos for the
    /// context menu package (build step).
    /// </summary>
    public static void WriteAssets(string folder)
    {
        AppIcons.WriteAssets(folder, "StartQueue");
        foreach (var (name, size) in new[] { ("StoreLogo.png", 50), ("Square44x44Logo.png", 44), ("Square150x150Logo.png", 150) })
            AppIcons.WritePng(Path.Combine(folder, name), size);
    }
}
