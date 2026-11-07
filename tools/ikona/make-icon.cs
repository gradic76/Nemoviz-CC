// tools\ikona\make-icon.cs -- builds NemovizCC.ico: a speech bubble holding the Nemoviz sign.
//
// The author, 2026-11-07: a speech bubble (it is a chat window), red, with the family's sign
// in it. THE SIGN is made exactly as Book Reader's and The Bell's make-icon.cs make it: the
// source is a JPEG that has chewed the geometry, so the red RING and SLASH are redrawn at every
// size from ratios read off the original, and only the hand-drawn EYE is carried across, with
// the red removed and the mottle on the white snapped back. The eye only from 32 px up.
//
// THE BUBBLE: a white rounded body with a tail at the bottom left, one closed outline in dark
// ink; the whole sign sits inside it, as large as fits.
//
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -r:System.Drawing.dll -out:make-icon.exe make-icon.cs
//   make-icon.exe <output folder>
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.IO;

class BubbleIcon
{
    const string Src = @"D:\Claude\Nemoviz Book Reader\Nemoviz\oko zabrana prozirna kvadratna.jpg";
    static readonly Rectangle Art = new Rectangle(183, 184, 216, 217);
    static readonly Color Red = Color.FromArgb(196, 21, 28);
    static readonly Color Ink = Color.FromArgb(255, 26, 26, 28);
    const double ROuter = 107.0 / 216.0, RRing = 24.0 / 216.0;
    static readonly double WSlash = 25.0 / 216.0 / Math.Sqrt(2);
    static Bitmap eye;

    static void Prep()
    {
        using (var s = new Bitmap(Src))
        using (var c = s.Clone(Art, PixelFormat.Format32bppArgb))
        {
            eye = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    Color p = c.GetPixel(x, y);
                    bool red = p.R > 100 && p.R > p.G * 3 / 2 && p.R > p.B * 3 / 2;
                    int lum = (p.R * 30 + p.G * 59 + p.B * 11) / 100;
                    eye.SetPixel(x, y, (red || lum > 236) ? Color.White : Color.FromArgb(255, p.R, p.G, p.B));
                }
        }
    }

    /// The family sign, unchanged: white disc, eye, red ring, red slash — in a D-sized square.
    static void Sign(Graphics g, float D, bool withEye)
    {
        float c = D / 2f, ro = (float)(ROuter * D), ring = (float)(RRing * D), ri = ro - ring, sl = (float)(WSlash * D);
        g.FillEllipse(Brushes.White, c - ri, c - ri, ri * 2, ri * 2);
        if (withEye)
        {
            var cl = new GraphicsPath(); cl.AddEllipse(c - ri, c - ri, ri * 2, ri * 2);
            var old = g.Clip; g.SetClip(cl, CombineMode.Intersect); g.DrawImage(eye, 0, 0, D, D); g.Clip = old;
        }
        using (var pen = new Pen(Red, ring))
            g.DrawEllipse(pen, c - ro + ring / 2, c - ro + ring / 2, (ro - ring / 2) * 2, (ro - ring / 2) * 2);
        var st = g.Save(); g.TranslateTransform(c, c); g.RotateTransform(45);
        g.FillRectangle(new SolidBrush(Red), -ro, -sl / 2f, ro * 2, sl); g.Restore(st);
    }

    /// The bubble as one closed outline in a D-sized square: rounded body, tail bottom left.
    static GraphicsPath Bubble(float D, float inset)
    {
        float x0 = 0.03f * D + inset, x1 = 0.97f * D - inset, y0 = 0.03f * D + inset, y1 = 0.80f * D - inset;
        float r = 0.24f * D;
        var p = new GraphicsPath();
        p.AddArc(x0, y0, 2 * r, 2 * r, 180, 90);
        p.AddArc(x1 - 2 * r, y0, 2 * r, 2 * r, 270, 90);
        p.AddArc(x1 - 2 * r, y1 - 2 * r, 2 * r, 2 * r, 0, 90);
        // bottom edge, right to left, with the tail between 0.40 and 0.20 pointing down-left
        p.AddLine(x1 - r, y1, 0.42f * D, y1);
        p.AddLine(0.42f * D, y1, 0.10f * D + inset, 0.97f * D - inset);
        p.AddLine(0.10f * D + inset, 0.97f * D - inset, 0.24f * D, y1);
        p.AddArc(x0, y1 - 2 * r, 2 * r, 2 * r, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap Render(int S)
    {
        bool big = S >= 32;
        var outp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(outp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
            float w = Math.Max(1f, S * 0.045f);
            using (var bubble = Bubble(S, w / 2f))
            {
                g.FillPath(Brushes.White, bubble);
                // The sign: centred in the body, as large as the body's height allows.
                float d = S * (big ? 0.64f : 0.70f), cx = S * 0.5f, cy = S * 0.415f;
                var st = g.Save();
                g.TranslateTransform(cx - d / 2f, cy - d / 2f);
                Sign(g, d, big);
                g.Restore(st);
                using (var pen = new Pen(Ink, w) { LineJoin = LineJoin.Round }) g.DrawPath(pen, bubble);
            }
        }
        return outp;
    }

    [STAThread]
    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(dir);
        Prep();
        int[] sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

        // A ladder to look at: every size on white and on a dark strip, then 4x.
        using (var sh = new Bitmap(1100, 700, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sh))
        {
            g.Clear(Color.White);
            var f = new Font("Segoe UI", 9f);
            int x = 20;
            foreach (int s in sizes)
            {
                if (s > 128) continue;
                g.DrawString(s + " px", f, Brushes.DimGray, x, 8);
                using (var b = Render(s)) g.DrawImage(b, x, 30, s, s);
                g.FillRectangle(new SolidBrush(Color.FromArgb(28, 28, 30)), x - 4, 40 + s, s + 8, s + 8);
                using (var b = Render(s)) g.DrawImage(b, x, 44 + s, s, s);
                x += s + 40;
            }
            using (var b = Render(256)) g.DrawImage(b, 20, 330, 256, 256);
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            int mx = 320;
            foreach (int s in new[] { 16, 24, 32, 48 })
            {
                using (var b = Render(s)) g.DrawImage(b, mx, 330, s * 4, s * 4);
                g.DrawString(s + " px x4", f, Brushes.DimGray, mx, 334 + s * 4);
                mx += s * 4 + 20;
            }
            sh.Save(Path.Combine(dir, "ladder.png"), ImageFormat.Png);
        }

        var pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (var b = Render(s)) { var ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (var fs = File.Create(Path.Combine(dir, "NemovizCC.ico")))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int off = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(off); off += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
        Console.WriteLine("ladder.png + NemovizCC.ico -> " + Path.GetFullPath(dir));
    }
}
