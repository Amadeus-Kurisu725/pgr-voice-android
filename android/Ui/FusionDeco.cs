using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>Card role for the F-B fusion deco (SPEC 「融合方案 F-A / F-B」, previews src/dx.js fuse(true)).</summary>
public enum FxKind { Card, Hero, Empty, Listen, Cur, Line, Ctrl, Tap, Ref }

/// <summary>
/// 0.4.3 light-surface decoration, F-B fusion (D1 archive skeleton + D2 Babylonia orbit + D3 red tide), drawn as a layer
/// under the content and clipped to the plate. Geometry is a 1:1 port of dx.js fuse(true) in dp units:
/// L = left crimson tick ruler + vertical ARC-29 / NO DATA, corner brackets, fading partial F1b, orbit arcs + nodes +
/// BABYLONIA / ORBIT · 02, top-right node + brass ruler + bracket + R·096, brass SEQ ruler, bottom red-tide band + mini
/// fused badge (empty: full TIDE · NULL badge). M = brackets + serial + half orbit ring (or faint F1b corner) + brass
/// ruler. S = small crimson corner + short slate ruler + brass node. No grid. Recorded once per size into a Picture.
/// </summary>
public sealed class FusionDeco : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    // dx.js palette K
    internal static readonly Color Red = Color.ParseColor("#A3283A"), RedT = Color.ParseColor("#9C2536"), Crim = Color.ParseColor("#C23A4E"),
        Brass = Color.ParseColor("#B08A3E"), BrassT = Color.ParseColor("#7E6128"), Slate = Color.ParseColor("#4F6B8A"), Ink = Color.ParseColor("#1D232A"),
        Line2 = Color.ParseColor("#AEB6BF"), Serial = Color.ParseColor("#6F7A86"), NodeFill = Color.ParseColor("#FAFBFC"), HeadSerial = Color.ParseColor("#7A8591");
    internal static readonly Typeface MonoTf = Typeface.Create(Typeface.Monospace, TypefaceStyle.Normal)!;
    internal static readonly Typeface MonoBold = Typeface.Create(Typeface.Monospace, TypefaceStyle.Bold)!;

    /// <summary>System font scale (set by the activity); the L-card readouts give way earlier when the card's text grows.</summary>
    public static float FontScale { get; set; } = 1f;
    readonly float d;
    readonly Paint p = new(PaintFlags.AntiAlias);
    readonly global::Android.Graphics.Path path = new();
    int alpha = 255;
    Picture? cache; int cw, ch, ca = -1;

    public DecoTier Tier { get; set; }
    public FxKind Kind { get; set; }
    public string? Code { get; set; }
    public float CutTL { get; set; }
    public float CutTR { get; set; }
    public float CutBR { get; set; }
    public float CutBL { get; set; }
    public float InsetDp { get; set; }
    /// <summary>Left offset (dp) of the light area when a dark cap sits at the plate's start (category bar).</summary>
    public float LeftDp { get; set; }
    /// <summary>M ring around an icon / avatar: centre x, centre y (≤0 = vertical centre), radius (dp).</summary>
    public (float cx, float cy, float r)? Ring { get; set; }
    public bool Paper { get; set; } = true;
    /// <summary>L archive serials: chapter number, section line count, SEQ index / count (SPEC: ARC-29, ARC—29 / 0404 · SEQ 01/02).</summary>
    public string ArcNo { get; set; } = "29";
    public string ArcLines { get; set; } = "0404";
    public string Seq { get; set; } = "01/02";

    public FusionDeco(float density, DecoTier tier, FxKind kind = FxKind.Card) { d = density; Tier = tier; Kind = kind; }

    public static Drawable Over(Drawable plate, FusionDeco deco) => new LayerDrawable(new[] { plate, deco });

    protected override void OnBoundsChange(Rect bounds) { base.OnBoundsChange(bounds); cache = null; }
    public void Invalidate() { cache = null; InvalidateSelf(); }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; int w = b.Width(), h = b.Height(); if (w <= 0 || h <= 0) return;
        if (cache == null || cw != w || ch != h || ca != alpha)
        {
            cache?.Dispose(); cache = new Picture(); cw = w; ch = h; ca = alpha;
            var rc = cache.BeginRecording(w, h)!;
            try { Render(rc, w, h); } catch (Exception ex) { global::Android.Util.Log.Warn("FusionDeco", ex.ToString()); }
            cache.EndRecording();
        }
        int s = canvas.Save(); canvas.Translate(b.Left, b.Top); canvas.DrawPicture(cache); canvas.RestoreToCount(s);
    }

    void Render(Canvas c, int wpx, int hpx)
    {
        float ins = InsetDp * d;
        using var r = new RectF(ins, ins, wpx - ins, hpx - ins);
        if (r.Width() < 8 * d || r.Height() < 8 * d) return;
        PgrArt.Chamfer(path, r, CutTL * d, CutTR * d, CutBR * d, CutBL * d);
        int save = c.Save(); c.ClipPath(path);
        if (Paper) FxArt.PaperTexture(c, r, Tier != DecoTier.S, d, p, alpha);
        c.Translate(r.Left + LeftDp * d, r.Top); c.Scale(d, d);
        float w = r.Width() / d - LeftDp, h = r.Height() / d;
        var a = new FxArt(c, p, path, alpha);
        switch (Tier)
        {
            case DecoTier.L: a.CardL(w, h, Kind == FxKind.Empty, d, ArcNo, ArcLines, Seq); break;
            case DecoTier.M: a.CardM(w, h, Kind, Code, Ring, d); break;
            default: a.CardS(w, h); break;
        }
        c.RestoreToCount(save);
    }

    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); cache = null; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { p.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { cache?.Dispose(); p.Dispose(); path.Dispose(); } base.Dispose(disposing); }
}

/// <summary>F-B page header underline (dx.js fuse header()): hairline + crimson lead, ink tick ruler + brass node,
/// SV-0.4 · ARCHIVE 0029, short red-tide line. Drawn along the bottom of the header row.</summary>
public sealed class FusionHeaderDeco(float density) : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    readonly float d = density;
    readonly Paint p = new(PaintFlags.AntiAlias);
    readonly global::Android.Graphics.Path path = new();
    int alpha = 255;
    Picture? cache; int cw, ch, ca = -1;
    /// <summary>Distance (dp) of the hairline above the drawable's bottom edge.</summary>
    public float BaselineDp { get; set; } = 4.5f;
    public bool Serial { get; set; } = true;
    public bool TideLine { get; set; } = true;

    protected override void OnBoundsChange(Rect bounds) { base.OnBoundsChange(bounds); cache = null; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; int w = b.Width(), h = b.Height(); if (w <= 0 || h <= 0) return;
        if (cache == null || cw != w || ch != h || ca != alpha)
        {
            cache?.Dispose(); cache = new Picture(); cw = w; ch = h; ca = alpha;
            var c = cache.BeginRecording(w, h)!;
            try
            {
                c.Scale(d, d);
                float W = w / d, y = h / d - BaselineDp; var a = new FxArt(c, p, path, alpha);
                a.Line(0, y, W, y, FusionDeco.Line2, 1, .8f);
                a.Rect(0, y - 1, 28, y + 1, FusionDeco.Red, 1);
                c.Save(); c.Translate(W - 96, y - 7.5f); a.TicksH(96, 7, FusionDeco.Ink, .45f, 4, 6, .8f); c.Restore();
                a.NodeO(W - 100, y, 2.2f, FusionDeco.Brass, 1);
                if (Serial) a.Text("SV-0.4 · ARCHIVE 0029", W - 108, y - 3.2f, 8, .12f, FusionDeco.HeadSerial, 1, FxAlign.Right);
                if (TideLine) { c.Save(); c.Translate(34, y - 4); a.TideLine(120, 8, .5f, 6); c.Restore(); }
            }
            catch (Exception ex) { global::Android.Util.Log.Warn("FusionDeco", ex.ToString()); }
            cache.EndRecording();
        }
        int s = canvas.Save(); canvas.Translate(b.Left, b.Top); canvas.DrawPicture(cache); canvas.RestoreToCount(s);
    }
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); cache = null; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { p.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
}

public enum FxAlign { Left, Right }

/// <summary>dx.js primitives in dp units (the canvas is pre-scaled by density).</summary>
internal sealed class FxArt(Canvas c, Paint p, global::Android.Graphics.Path path, int alpha)
{
    Color A(Color col, float op) => Color.Argb((int)Math.Clamp(op * alpha, 0, 255), col.R, col.G, col.B);

    public void Line(float x0, float y0, float x1, float y1, Color col, float op, float sw, float[]? dash = null)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw; p.Color = A(col, op);
        using var fx = dash == null ? null : new DashPathEffect(dash, 0); p.SetPathEffect(fx);
        c.DrawLine(x0, y0, x1, y1, p); p.SetPathEffect(null);
    }
    public void Rect(float l, float t, float r, float b, Color col, float op)
    { p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Fill); p.Color = A(col, op); c.DrawRect(l, t, r, b, p); }
    public void NodeF(float x, float y, float r, Color col)
    { p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Fill); p.Color = A(col, 1); c.DrawCircle(x, y, r, p); }
    public void NodeO(float x, float y, float r, Color col, float sw = 1)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Fill); p.Color = A(FusionDeco.NodeFill, 1); c.DrawCircle(x, y, r, p);
        p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw; p.Color = A(col, 1); c.DrawCircle(x, y, r, p);
    }
    static (float x, float y) Pt(float cx, float cy, float r, float a) => (cx + r * (float)Math.Cos(a * Math.PI / 180), cy + r * (float)Math.Sin(a * Math.PI / 180));
    /// <summary>SVG arc from a0 to a1 (degrees, clockwise from +x; a1 &lt; a0 runs counter-clockwise).</summary>
    public void Arc(float cx, float cy, float r, float a0, float a1, Color col, float op, float sw, float[]? dash = null)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw; p.Color = A(col, op);
        using var fx = dash == null ? null : new DashPathEffect(dash, 0); p.SetPathEffect(fx);
        path.Reset(); using var oval = new RectF(cx - r, cy - r, cx + r, cy + r);
        path.AddArc(oval, a0, a1 - a0); c.DrawPath(path, p); p.SetPathEffect(null);
    }
    public void Text(string t, float x, float baseline, float size, float spacing, Color col, float op, FxAlign align, bool bold = false)
    {
        p.Reset(); p.AntiAlias = true; p.SubpixelText = true; p.SetStyle(Paint.Style.Fill); p.Color = A(col, op);
        p.SetTypeface(bold ? FusionDeco.MonoBold : FusionDeco.MonoTf); p.TextSize = size; p.LetterSpacing = spacing;
        p.TextAlign = align == FxAlign.Right ? Paint.Align.Right : Paint.Align.Left;
        c.DrawText(t, x, baseline, p);
    }
    public void TicksH(float w, float h, Color col, float op, float step, int major, float sw)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw;
        int i = 0; for (float x = .5f; x <= w; i++, x += step) { bool M = i % major == 0; float l = M ? h : h * .45f; p.Color = A(col, M ? op : op * .55f); c.DrawLine(x, h, x, h - l, p); }
    }
    public void TicksV(float h, float w, Color col, float op, float step, int major, float sw)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw;
        int i = 0; for (float y = .5f; y <= h; i++, y += step) { bool M = i % major == 0; float l = M ? w : w * .45f; p.Color = A(col, M ? op : op * .55f); c.DrawLine(0, y, l, y, p); }
    }
    public void Bracket(string pos, float x, float y, float L, Color col, float sw)
    {
        float q = sw / 2; path.Reset();
        switch (pos)
        {
            case "tl": path.MoveTo(x + q, y + L); path.LineTo(x + q, y + q); path.LineTo(x + L, y + q); break;
            case "tr": path.MoveTo(x, y + q); path.LineTo(x + L - q, y + q); path.LineTo(x + L - q, y + L); break;
            case "br": path.MoveTo(x + L - q, y); path.LineTo(x + L - q, y + L - q); path.LineTo(x, y + L - q); break;
            default: path.MoveTo(x + q, y); path.LineTo(x + q, y + L - q); path.LineTo(x + L, y + L - q); break;
        }
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw; p.Color = A(col, 1); p.StrokeJoin = Paint.Join.Miter; c.DrawPath(path, p);
    }
    // seeded LCG, identical to dx.js rnd()
    sealed class Rnd(long seed) { long s = seed; public double Next() { s = (s * 9301 + 49297) % 233280; return s / 233280.0; } }
    public void Tide(float w, float h, float op, float fill, int seed, float amp)
    {
        var r = new Rnd(seed); path.Reset(); path.MoveTo(0, h); path.LineTo(0, h * .7f); float x = 0;
        while (x < w) { float k = 3 + (float)r.Next() * 5, a = (2 + (float)r.Next() * (h * .55f)) * amp;
            path.LineTo(x + k * .4f, h * .7f - a); path.LineTo(x + k * .75f, h * .7f + a * .3f); path.LineTo(x + k, h * .7f); x += k; }
        path.LineTo(w, h); path.Close();
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Fill); p.Color = A(FusionDeco.Crim, fill); c.DrawPath(path, p);
        p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = .9f; p.StrokeJoin = Paint.Join.Miter; p.Color = A(FusionDeco.Crim, op); c.DrawPath(path, p);
    }
    public void TideLine(float w, float h, float op, int seed)
    {
        var r = new Rnd(seed); path.Reset(); path.MoveTo(0, h / 2); float x = 0;
        while (x < w) { float k = 4 + (float)r.Next() * 6, a = 1 + (float)r.Next() * (h / 2 - 1);
            path.LineTo(x + k * .45f, h / 2 - a); path.LineTo(x + k * .8f, h / 2 + a * .4f); path.LineTo(x + k, h / 2); x += k; }
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = .9f; p.Color = A(FusionDeco.Crim, op); c.DrawPath(path, p);
    }
    public void GradErosion(float x0, float y0, float w, float h, float cell, float gap, int seed, float dens)
    {
        var r = new Rnd(seed); float st = cell + gap; int cols = (int)Math.Floor(w / st), rows = (int)Math.Floor(h / st);
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Fill);
        for (int j = 0; j < rows; j++) for (int i = 0; i < cols; i++)
        {
            double dx = (cols - 1 - i) / (double)cols, dy = j / (double)rows, dd = Math.Min(1, Math.Sqrt(dx * 1.05 * dx * 1.05 + dy * 1.25 * dy * 1.25));
            if (r.Next() > Math.Pow(1 - dd, 1.6) * 1.15 * dens) continue;
            float a = (float)(.12 + .78 * Math.Pow(1 - dd, 1.4)), cs = dd > .55 ? cell - 1 : cell, off = (cell - cs) / 2;
            p.Color = A(FusionDeco.Crim, a); c.DrawRect(x0 + i * st + off, y0 + j * st + off, x0 + i * st + off + cs, y0 + j * st + off + cs, p);
        }
    }
    void Hatch(float w, float h, float op, float step, float sw)
    {
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = sw; p.Color = A(FusionDeco.Crim, op);
        for (float x = -h; x < w; x += step) c.DrawLine(x, h, x + h, 0, p);
    }
    /// <summary>Fused corner badge 140×40 (empty states), origin = its top-left.</summary>
    public void CornerBadge(string label, int seed)
    {
        c.Save(); c.Translate(0, 4); Hatch(30, 3, .5f, 2.6f, .6f); c.Restore();
        Text(label, 0, 19, 7.5f, 1.1f / 7.5f, FusionDeco.RedT, 1, FxAlign.Left);
        Line(0, 30, 86, 30, FusionDeco.Line2, 1, .7f);
        int i = 0; for (float x = .5f; x <= 84; i++, x += 3) { bool M = i % 5 == 0; Line(x, 30, x, M ? 24 : 27, FusionDeco.Red, M ? .7f : .4f, .7f); }
        Arc(88, 4, 26, 90, 48, FusionDeco.Brass, .75f, .8f);
        var (ax, ay) = Pt(88, 4, 26, 48);
        NodeF(88, 30, 2, FusionDeco.Brass); NodeO(ax, ay, 1.7f, FusionDeco.Slate, .8f);
        GradErosion(100, 3, 36, 26, 3, 1, seed, 1);
        path.Reset(); path.MoveTo(128, .6f); path.LineTo(139.4f, .6f); path.LineTo(139.4f, 12);
        p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = 1.1f; p.Color = A(FusionDeco.Brass, 1); c.DrawPath(path, p);
    }
    /// <summary>Mini fused badge 72×14 (L-card bottom-right), origin = its top-left.</summary>
    public void MiniBadge(int seed)
    {
        int i = 0; for (float x = .5f; x <= 22; i++, x += 3) Line(x, 13, x, i % 4 == 0 ? 8.5f : 10.5f, FusionDeco.Red, i % 4 == 0 ? .7f : .4f, .7f);
        Arc(28, -8, 19, 90, 50, FusionDeco.Brass, .75f, .8f);
        var (ax, ay) = Pt(28, -8, 19, 50);
        NodeF(28, 11, 1.8f, FusionDeco.Brass); NodeO(ax, ay, 1.5f, FusionDeco.Slate, .8f);
        GradErosion(42, 0, 30, 14, 2.5f, 1, seed, 1.7f);
    }

    public void CardL(float w, float h, bool E, float density, string arc, string lines, string seq)
    {
        float ocx = w + 14, ocy = -26, r1 = 96, r2 = r1 + 18, tideH = 14;
        // D1 skeleton: left tick ruler + vertical serial
        c.Save(); c.Translate(5, 14); TicksV(Math.Max(0, h - 100), 7, FusionDeco.Red, .55f, 4, 5, .8f); c.Restore();
        c.Save(); c.Translate(4 + 7.5f, h - tideH - 10); c.Rotate(-90);
        Text(E ? "NO DATA" : "ARC-" + arc, 0, 0, 7.5f, .14f, FusionDeco.RedT, 1, FxAlign.Left, true); c.Restore();
        // partial F1b, radial fade (cached bitmap)
        FxArt.F1bRadial(c, w + 52 - 160, -40, 160, .34f, density, p, alpha);
        Bracket("tl", 0, 0, 12, FusionDeco.Red, 1.6f); Bracket("bl", 0, h - 8, 8, FusionDeco.Slate, 1);
        Bracket("br", w - 12, h - 12, 12, FusionDeco.Brass, 1.6f);
        // D2 orbit around F1b
        var (n1x, n1y) = Pt(ocx, ocy, r1, 132); var (n2x, n2y) = Pt(ocx, ocy, r2, 152); var (n3x, n3y) = Pt(ocx, ocy, r2, 108);
        Arc(ocx, ocy, r1, 104, 176, FusionDeco.Slate, .5f, .9f);
        Arc(ocx, ocy, r2, 100, 170, FusionDeco.Brass, .7f, .9f, new[] { 4f, 4f });
        Line(n1x, n1y, n2x, n2y, FusionDeco.Brass, .5f, .8f);
        NodeF(n1x, n1y, 2.8f, FusionDeco.Brass); NodeO(n2x, n2y, 3.2f, FusionDeco.Brass, 1.1f);
        NodeO(n3x, n3y, 2.3f, FusionDeco.Slate);
        Arc(ocx, ocy, r1 - 22, 118, 166, FusionDeco.Slate, .25f, .8f);
        float ix = n2x - 8, iy = n2y - 7;
        // 0.4.6: on narrow cards (344dp fold outer, 360dp at larger font) the orbit / SEQ readouts would run into the
        // heading block (「剧情档案 · STORY ARCHIVE」, 「本地配音库」): drop the orbit labels and shorten SEQ; arcs, nodes, rulers stay.
        bool tight = w < 175 * Math.Max(1f, FusionDeco.FontScale) + 160;
        if (!tight)
        {
            Text(E ? "ORBIT 00" : "BABYLONIA", ix, iy + 7.5f, 7.5f, .14f, FusionDeco.BrassT, 1, FxAlign.Right);
            Text(E ? "NO SIGNAL" : "ORBIT · 02", ix, iy + 7.5f + 10.1f, 7.5f, .14f, FusionDeco.BrassT, 1, FxAlign.Right);
        }
        // brass SEQ ruler (archive card only)
        if (!E)
        {
            float rx = w - 40 - 150, ry = 84;
            Line(rx, ry + 17.5f, rx + 150, ry + 17.5f, FusionDeco.Line2, 1, .8f);
            c.Save(); c.Translate(rx, ry + 10); TicksH(150, 7, FusionDeco.Brass, .85f, 4, 6, .8f); c.Restore();
            Text(tight ? "SEQ " + seq : "ARC—" + arc + " / " + lines + " · SEQ " + seq, w - 40, ry + 4.5f, 7.5f, .12f, FusionDeco.BrassT, 1, FxAlign.Right);
        }
        // top-right corner piece: node on the outer orbit + brass ruler along the top edge + bracket + R·096
        {
            float yy = 3.5f, sa = 180 - (float)(Math.Asin((yy - ocy) / r1) * 180 / Math.PI); var (ax, _) = Pt(ocx, ocy, r1, sa); float x1 = w - 20;
            Line(ax, yy, x1, yy, FusionDeco.Brass, .6f, .7f);
            int k = 0; for (float x = ax + 6; x <= x1; k++, x += 3) Line(x, yy, x, yy + (k % 5 == 0 ? 5.5f : 3), FusionDeco.Brass, k % 5 == 0 ? .85f : .55f, .7f);
            NodeF(ax, yy, 2.2f, FusionDeco.Brass);
            path.Reset(); path.MoveTo(w - 13, 1.6f); path.LineTo(w - 1.6f, 1.6f); path.LineTo(w - 1.6f, 13);
            p.Reset(); p.AntiAlias = true; p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = 1.3f; p.Color = A(FusionDeco.Brass, 1); c.DrawPath(path, p);
            Text("R·096", w - 20, 12 + 7, 7, .14f, FusionDeco.Slate, 1, FxAlign.Right);
        }
        // D3: red-tide band along the bottom + mini fused badge
        c.Save(); c.Translate(18, h - 1 - tideH); Tide(w - 120, tideH, .45f, .05f, 9, .6f); c.Restore();
        c.Save(); c.Translate(w - 26 - 72, h - 2 - 14); MiniBadge(E ? 47 : 43); c.Restore();
        if (E) { c.Save(); c.Translate(w - 14 - 140, h - 114); CornerBadge("TIDE · NULL", 41); c.Restore(); }
    }

    public void CardM(float w, float h, FxKind kind, string? code, (float cx, float cy, float r)? ring, float density)
    {
        Bracket("tl", 0, 0, 9, FusionDeco.Red, 1.4f); Bracket("br", w - 9, h - 9, 9, FusionDeco.Brass, 1.4f);
        if (code != null)
        {
            if (kind is FxKind.Ctrl or FxKind.Tap) Text(code, 16, h - 4 - 2, 8, .12f, FusionDeco.Serial, 1, FxAlign.Left);
            else Text(code, w - 16, 6 + 7.2f, 8, .12f, FusionDeco.Serial, 1, FxAlign.Right);
        }
        if (ring is { } g)
        {
            float cy = g.cy <= 0 ? h / 2 : g.cy;
            Arc(g.cx, cy, g.r, 200, 410, FusionDeco.Brass, .6f, .9f);
            var (fx, fy) = Pt(g.cx, cy, g.r, 410); NodeF(fx, fy, 2.3f, FusionDeco.Brass);
            Arc(g.cx, cy, g.r + 6, 130, 230, FusionDeco.Slate, .4f, .8f, new[] { 2f, 3f });
            var (ox, oy) = Pt(g.cx, cy, g.r + 6, 230); NodeO(ox, oy, 2, FusionDeco.Slate);
        }
        else FxArt.F1bLinear(c, w + 40 - 110, -26, 110, .2f, density, p, alpha);
        float rw = (float)Math.Round(w * .28f);
        c.Save(); c.Translate(w - 14 - rw, h - 3 - 7); TicksH(rw, 6, FusionDeco.Brass, .75f, 4, 6, .8f); c.Restore();
    }

    public void CardS(float w, float h)
    {
        Bracket("tl", 0, 0, 6, FusionDeco.Red, 1.2f);
        if (w <= 120) return;
        c.Save(); c.Translate(w - 9 - 26, h - 3 - 5); TicksH(22, 5, FusionDeco.Slate, .6f, 4, 3, .8f); NodeF(24.5f, 2.5f, 1.6f, FusionDeco.Brass); c.Restore();
    }

    // ---------- cached textures ----------
    static Bitmap? grain;
    static readonly Dictionary<string, Bitmap> f1b = new();

    /// <summary>Paper grain (fractal-noise look, ≤5.5% slate) + M/L top-right slate glow. Pixel space.</summary>
    public static void PaperTexture(Canvas c, RectF r, bool glow, float d, Paint p, int alpha)
    {
        if (grain == null)
        {
            const int n = 128; var px = new int[n * n]; uint s = 0x2F6E2B1;
            for (int i = 0; i < px.Length; i++) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; int a = (int)(s % 15); px[i] = Color.Argb(a, 71, 84, 102).ToArgb(); }
            grain = Bitmap.CreateBitmap(px, n, n, Bitmap.Config.Argb8888!)!;
        }
        p.Reset(); p.AntiAlias = true;
        using (var sh = new BitmapShader(grain, Shader.TileMode.Repeat!, Shader.TileMode.Repeat!)) { p.SetShader(sh); p.Alpha = alpha; c.DrawRect(r, p); p.SetShader(null); }
        if (!glow) return;
        float rx = r.Width() * .9f, ry = r.Height() * .7f; if (rx <= 0 || ry <= 0) return;
        int s2 = c.Save(); c.Translate(r.Right, r.Top); c.Scale(1, ry / rx);
        using (var g = new RadialGradient(0, 0, rx, new int[] { Color.Argb((int)(.09f * alpha), 79, 107, 138).ToArgb(), Color.Argb(0, 79, 107, 138).ToArgb() }, new float[] { 0, .6f }, Shader.TileMode.Clamp!))
        { p.Reset(); p.SetShader(g); c.DrawRect(-rx, 0, 0, rx, p); p.SetShader(null); }
        c.RestoreToCount(s2);
    }

    static Bitmap? Masked(string key, int wpx, bool radial)
    {
        string k = key + wpx; if (f1b.TryGetValue(k, out var hit) && !hit.IsRecycled) return hit;
        var src = PgrArt.F1b; if (src == null) return null;
        int hpx = (int)Math.Round(wpx * (double)src.Height / src.Width);
        var bmp = Bitmap.CreateBitmap(wpx, hpx, Bitmap.Config.Argb8888!)!;
        using var cv = new Canvas(bmp); using var pt = new Paint(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
        using (var dst = new RectF(0, 0, wpx, hpx)) cv.DrawBitmap(src, null, dst, pt);
        Shader mask;
        if (radial)
        {   // radial-gradient(circle at 30% 62%, #000 22%, transparent 60%) — circle reaches the farthest corner
            float cx = wpx * .3f, cy = hpx * .62f, rr = (float)Math.Sqrt(Math.Pow(Math.Max(cx, wpx - cx), 2) + Math.Pow(Math.Max(cy, hpx - cy), 2));
            mask = new RadialGradient(cx, cy, rr, new int[] { Color.Black.ToArgb(), Color.Black.ToArgb(), Color.Transparent.ToArgb() }, new float[] { 0, .22f, .6f }, Shader.TileMode.Clamp!);
        }
        else mask = new LinearGradient(0, 0, 0, hpx, new int[] { Color.Black.ToArgb(), Color.Black.ToArgb(), Color.Transparent.ToArgb() }, new float[] { 0, .2f, .6f }, Shader.TileMode.Clamp!);
        using (mask) { pt.SetShader(mask); pt.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstIn!)); cv.DrawRect(0, 0, wpx, hpx, pt); }
        f1b[k] = bmp; return bmp;
    }
    static void DrawF1b(Canvas c, string key, bool radial, float x, float y, float wdp, float op, float d, Paint p, int alpha)
    {
        var bmp = Masked(key, (int)Math.Round(wdp * d), radial); if (bmp == null) return;
        p.Reset(); p.AntiAlias = true; p.FilterBitmap = true; p.Alpha = (int)Math.Clamp(op * alpha, 0, 255);
        using var dst = new RectF(x, y, x + wdp, y + wdp * bmp.Height / bmp.Width); c.DrawBitmap(bmp, null, dst, p); p.Alpha = 255;
    }
    public static void F1bRadial(Canvas c, float x, float y, float wdp, float op, float d, Paint p, int alpha) => DrawF1b(c, "r", true, x, y, wdp, op, d, p, alpha);
    public static void F1bLinear(Canvas c, float x, float y, float wdp, float op, float d, Paint p, int alpha) => DrawF1b(c, "l", false, x, y, wdp, op, d, p, alpha);
}

/// <summary>One-call helpers to layer the fusion deco over existing plates.</summary>
public static class Fx
{
    static float Den(Context c) => c.Resources!.DisplayMetrics!.Density;
    public static FusionDeco Deco(Context c, DecoTier tier, FxKind kind = FxKind.Card, string? code = null, float tl = 0, float tr = 0, float br = 0, float bl = 0, float inset = 0) =>
        new(Den(c), tier, kind) { Code = code, CutTL = tl, CutTR = tr, CutBR = br, CutBL = bl, InsetDp = inset };
    public static Drawable Over(Context c, Drawable plate, DecoTier tier, FxKind kind = FxKind.Card, string? code = null, float tl = 0, float tr = 0, float br = 0, float bl = 0, float inset = 0) =>
        FusionDeco.Over(plate, Deco(c, tier, kind, code, tl, tr, br, bl, inset));
    /// <summary>Swap a RippleDrawable's content layer for content + deco (keeps the mask; ripple colour given).</summary>
    public static void Under(View v, FusionDeco deco, Color ripple)
    {
        if (v.Background is RippleDrawable rd && rd.NumberOfLayers > 0)
        {
            Drawable? content = null, mask = null;
            for (int i = 0; i < rd.NumberOfLayers; i++) { if (rd.GetId(i) == global::Android.Resource.Id.Mask) mask = rd.GetDrawable(i); else content ??= rd.GetDrawable(i); }
            if (content == null) { v.Background = FusionDeco.Over(rd, deco); return; }
            content.SetCallback(null); mask?.SetCallback(null);
            v.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(ripple), FusionDeco.Over(content, deco), mask);
        }
        else if (v.Background != null) v.Background = FusionDeco.Over(v.Background, deco);
        else v.Background = deco;
    }
}

/// <summary>SPEC 切角开关 track (dx.js sw): 42×22dp chamfered plate; ON = crimson 18% fill + crimson edge, OFF = #DDE2E7 + line2 edge.</summary>
public sealed class ChamferSwitchTrack(float density) : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    readonly float d = density; readonly Paint p = new(PaintFlags.AntiAlias); readonly global::Android.Graphics.Path path = new();
    bool on, enabled = true; int alpha = 255;
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    {
        bool c = state?.Contains(global::Android.Resource.Attribute.StateChecked) == true, e = state?.Contains(global::Android.Resource.Attribute.StateEnabled) == true;
        if (c == on && e == enabled) return false; on = c; enabled = e; InvalidateSelf(); return true;
    }
    public override int IntrinsicWidth => (int)(42 * d);
    public override int IntrinsicHeight => (int)(22 * d);
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float h = 22 * d, t = b.CenterY() - h / 2, l = b.Left, r = b.Right, bo = t + h, k = 4 * d;
        path.Reset(); path.MoveTo(l + k, t); path.LineTo(r, t); path.LineTo(r, bo - k); path.LineTo(r - k, bo); path.LineTo(l, bo); path.LineTo(l, t + k); path.Close();
        int a = enabled ? alpha : alpha / 2;
        p.SetStyle(Paint.Style.Fill); p.Color = on ? Color.Argb(46 * a / 255, 163, 40, 58) : Color.Argb(a, 0xDD, 0xE2, 0xE7); canvas.DrawPath(path, p);
        p.SetStyle(Paint.Style.Stroke); p.StrokeWidth = d; p.Color = on ? Color.Argb(a, 0xA3, 0x28, 0x3A) : Color.Argb(a, 0xAE, 0xB6, 0xBF);
        canvas.Save(); canvas.ClipPath(path); canvas.DrawPath(path, p); canvas.Restore();
    }
    public override void SetAlpha(int value) { alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? cf) { p.SetColorFilter(cf); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
}

/// <summary>SPEC 菱形滑块: 11dp diamond, crimson when ON, #7C8792 when OFF; 21dp intrinsic so the track stays 42dp.</summary>
public sealed class DiamondSwitchThumb(float density) : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    readonly float d = density; readonly Paint p = new(PaintFlags.AntiAlias); readonly global::Android.Graphics.Path path = new();
    bool on, enabled = true; int alpha = 255;
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    {
        bool c = state?.Contains(global::Android.Resource.Attribute.StateChecked) == true, e = state?.Contains(global::Android.Resource.Attribute.StateEnabled) == true;
        if (c == on && e == enabled) return false; on = c; enabled = e; InvalidateSelf(); return true;
    }
    public override int IntrinsicWidth => (int)(21 * d);
    public override int IntrinsicHeight => (int)(22 * d);
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; float cx = b.ExactCenterX(), cy = b.ExactCenterY(), r = 5.6f * d;
        path.Reset(); path.MoveTo(cx, cy - r); path.LineTo(cx + r, cy); path.LineTo(cx, cy + r); path.LineTo(cx - r, cy); path.Close();
        int a = enabled ? alpha : alpha / 2;
        p.SetStyle(Paint.Style.Fill); p.Color = on ? Color.Argb(a, 0xA3, 0x28, 0x3A) : Color.Argb(a, 0x7C, 0x87, 0x92); canvas.DrawPath(path, p);
    }
    public override void SetAlpha(int value) { alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? cf) { p.SetColorFilter(cf); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
}
