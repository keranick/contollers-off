using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ControllersOff.Tools;

public static class IconWriter
{
    private static readonly int[] Sizes = [16, 24, 32, 48, 64, 256];

    public static void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var images = Sizes.Select(Encode).ToArray();

        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)Sizes.Length);

        var offset = 6 + 16 * Sizes.Length;
        foreach (var (size, bytes) in images)
        {
            writer.Write((byte)size);
            writer.Write((byte)size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(bytes.Length);
            writer.Write(offset);
            offset += bytes.Length;
        }

        foreach (var (_, bytes) in images)
        {
            writer.Write(bytes);
        }
    }

    private static (int Size, byte[] Bytes) Encode(int size)
    {
        using var bitmap = Render(size);
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, ImageFormat.Png);

        return (size, buffer.ToArray());
    }

    private static Bitmap Render(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var inset = Math.Max(1f, size * 0.06f);
            using (var body = new SolidBrush(Color.FromArgb(255, 31, 33, 38)))
            using (var shape = RoundedRectangle(new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset), size * 0.24f))
            {
                graphics.FillPath(body, shape);
            }

            using var pen = new Pen(Color.FromArgb(255, 96, 173, 255), Math.Max(1.6f, size * 0.085f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            var center = new PointF(size / 2f, size / 2f);
            var radius = size * 0.27f;
            graphics.DrawArc(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2, 135, 270);
            graphics.DrawLine(pen, center.X, center.Y - radius * 1.1f, center.X, center.Y - radius * 0.1f);
        }

        return bitmap;
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
