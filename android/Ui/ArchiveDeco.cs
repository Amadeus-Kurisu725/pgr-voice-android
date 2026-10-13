using Android.Graphics;
using Android.Graphics.Drawables;
using Path = Android.Graphics.Path;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>Decoration size tier for dark slate panels (SPEC「深色面板装饰 = 必须」): L large panels, M trays / cards, S slim bars.</summary>
public enum DecoTier { L, M, S }

/// <summary>
/// Shared PGR decoration painters (archive grid, scan lines, Babylonia orbit arcs, F1b line-art emblem, Red Tide band,
/// crimson / brass corner ticks). Geometry follows the approved preview sources (previews/android/src/deco.js, p01.js):
/// SVG view boxes are mapped non-uniformly onto the target rect, stroke widths stay in dp. Everything is static.
/// </summary>
public static class PgrArt
{
    // Dark-panel decoration inks (tuned for #38414C → #2A3139).
    public static readonly Color DeckTop = Color.ParseColor("#38414C");
    public static readonly Color DeckBottom = Color.ParseColor("#2A3139");
    public static readonly Color DeckButton = Color.ParseColor("#363E48");    // opaque secondary button on dark
    public static readonly Color MistLine = Color.Argb(255, 143, 180, 210);  // grid / orbit on dark
    public static readonly Color GoldLine = Color.Argb(255, 226, 194, 126);  // brass on dark
    public static readonly Color TideRed = Color.Argb(255, 224, 106, 121);   // crimson on dark
    public static readonly Color PaperTideRed = Color.Argb(255, 142, 30, 48);
    public static readonly Color PaperRule = Color.Argb(255, 203, 209, 215);

    static Bitmap? f1b;
    /// <summary>F1b line-art emblem (assets/ui/f1b.png, 600×760, rendered from previews/android/src/emb-f1b.svg). Not an official logo.</summary>
    public static Bitmap? F1b
    {
        get
        {
            if (f1b is { IsRecycled: false }) return f1b;
            try
            {
                using var stream = global::Android.App.Application.Context.Assets!.Open("ui/f1b.png");
                f1b = BitmapFactory.DecodeStream(stream);
            }
            catch (Exception) { f1b = null; }
            return f1b;
        }
    }
    /// <summary>CSS filter invert(1) brightness(1.25): light line art for dark panels.</summary>
    static readonly ColorMatrixColorFilter InvertFilter = new(new ColorMatrix(new float[]
    {
        -1.25f, 0, 0, 0, 318.75f,
        0, -1.25f, 0, 0, 318.75f,
        0, 0, -1.25f, 0, 318.75f,
        0, 0, 0, 1, 0
    }));

    static Color Al(Color c, float opacity, int alpha) => Color.Argb((int)Math.Clamp(opacity * 255f * alpha / 255f, 0, 255), c.R, c.G, c.B);

    /// <summary>Draws the F1b emblem with its width in px; anchored by its right/bottom edge (may overflow the clip).</summary>
    public static void Emblem(Canvas canvas, float right, float bottom, float width, float opacity, bool onDark, Paint paint, int alpha = 255)
    {
        var bmp = F1b; if (bmp == null || width <= 0) return;
        float h = width * bmp.Height / bmp.Width;
        using var dst = new RectF(right - width, bottom - h, right, bottom);
        paint.Reset(); paint.AntiAlias = true; paint.FilterBitmap = true;
        paint.Alpha = (int)Math.Clamp(opacity * alpha, 0, 255);
        if (onDark) paint.SetColorFilter(InvertFilter);
        canvas.DrawBitmap(bmp, null, dst, paint);
        paint.SetColorFilter(null); paint.Alpha = 255;
    }

    public static void Grid(Canvas canvas, RectF r, float step, Color color, float opacity, float d, Paint paint, int alpha = 255)
    {
        paint.SetStyle(Paint.Style.Fill); paint.Color = Al(color, opacity, alpha);
        float w = Math.Max(1, d * .75f);
        for (float x = r.Left; x < r.Right; x += step) canvas.DrawRect(x, r.Top, x + w, r.Bottom, paint);
        for (float y = r.Top; y < r.Bottom; y += step) canvas.DrawRect(r.Left, y, r.Right, y + w, paint);
    }

    public static void Scan(Canvas canvas, RectF r, float gap, Color color, float opacity, float d, Paint paint, int alpha = 255)
    {
        paint.SetStyle(Paint.Style.Fill); paint.Color = Al(color, opacity, alpha);
        float w = Math.Max(1, d * .6f);
        for (float y = r.Top; y < r.Bottom; y += gap) canvas.DrawRect(r.Left, y, r.Right, y + w, paint);
    }

    static void Stroke(Canvas canvas, Path p, RectF r, float vbW, float vbH, Color c, float op, float width, float[]? dash, Paint paint)
    {
        using var m = new Matrix(); m.SetScale(r.Width() / vbW, r.Height() / vbH); m.PostTranslate(r.Left, r.Top);
        p.Transform(m);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = width; paint.Color = Al(c, op, 255);
        using var effect = dash == null ? null : new DashPathEffect(dash, 0);
        paint.SetPathEffect(effect); canvas.DrawPath(p, paint); paint.SetPathEffect(null);
    }

    static Path Curve(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
    { var p = new Path(); p.MoveTo(x0, y0); p.CubicTo(x1, y1, x2, y2, x3, y3); return p; }

    static void Dot(Canvas canvas, RectF r, float vbW, float vbH, float cx, float cy, float radius, Color c, float op, Paint paint)
    {
        paint.SetStyle(Paint.Style.Fill); paint.Color = Al(c, op, 255);
        canvas.DrawCircle(r.Left + cx * r.Width() / vbW, r.Top + cy * r.Height() / vbH, radius, paint);
    }

    /// <summary>Babylonia orbit arcs across a whole panel (light paper: brass + slate; dark: gold + mist).</summary>
    public static void Orbits(Canvas canvas, RectF r, float op, bool onDark, float d, Paint paint, int alpha = 255)
    {
        op *= alpha / 255f;
        if (!onDark)
        {
            using (var p = Curve(-30, 520, 60, 380, 250, 300, 420, 330)) Stroke(canvas, p, r, 380, 600, PgrTheme.Brass, .5f * op, 1.2f * d, null, paint);
            using (var p = Curve(-30, 560, 90, 430, 260, 370, 420, 400)) Stroke(canvas, p, r, 380, 600, PgrTheme.Brass, .3f * op, d, new[] { 6 * d, 5 * d }, paint);
            using (var p = Curve(-30, 470, 70, 350, 230, 270, 420, 270)) Stroke(canvas, p, r, 380, 600, PgrTheme.Slate, .28f * op, d, null, paint);
            Dot(canvas, r, 380, 600, 306, 318, 3 * d, PgrTheme.Brass, .8f * op, paint);
        }
        else
        {
            using (var p = Curve(-40, 580, 80, 400, 260, 320, 440, 360)) Stroke(canvas, p, r, 380, 700, GoldLine, .42f * op, 1.2f * d, null, paint);
            using (var p = Curve(-40, 620, 100, 460, 270, 400, 440, 440)) Stroke(canvas, p, r, 380, 700, GoldLine, .26f * op, d, new[] { 6 * d, 5 * d }, paint);
            using (var p = Curve(-40, 520, 90, 360, 250, 280, 440, 290)) Stroke(canvas, p, r, 380, 700, MistLine, .28f * op, d, null, paint);
            Dot(canvas, r, 380, 700, 298, 348, 3.5f * d, GoldLine, .75f * op, paint);
        }
    }

    /// <summary>Small orbit arc pair for M / S panels (dkArc), drawn in the given top-right region.</summary>
    public static void Arc(Canvas canvas, RectF r, float op, bool onDark, float d, Paint paint, int alpha = 255)
    {
        op *= alpha / 255f;
        Color gold = onDark ? GoldLine : PgrTheme.Brass, mist = onDark ? MistLine : PgrTheme.Slate;
        using (var p = Curve(120, 110, 180, 40, 290, 10, 400, 22)) Stroke(canvas, p, r, 380, 100, gold, .45f * op, 1.1f * d, null, paint);
        using (var p = Curve(150, 112, 210, 60, 300, 38, 400, 46)) Stroke(canvas, p, r, 380, 100, mist, .3f * op, d, new[] { 5 * d, 4 * d }, paint);
        Dot(canvas, r, 380, 100, 318, 24, 2.4f * d, gold, .8f * op, paint);
    }

    /// <summary>Red Tide band: two mist/slate swells and a crimson zigzag crest (dkTide / tideBand).</summary>
    public static void Tide(Canvas canvas, RectF r, float o, bool onDark, float d, Paint paint, Path path, int alpha = 255)
    {
        o *= alpha / 255f;
        float w = r.Width(), h = r.Height(), sx = w / 380f; if (w <= 0 || h <= 0) return;
        Color mist = onDark ? MistLine : PgrTheme.Slate, red = onDark ? TideRed : PaperTideRed;
        float[] fills = onDark ? new[] { .16f, .28f, .2f } : new[] { .08f, .08f, .14f };
        float[] strokes = onDark ? new[] { .7f, .85f, .65f } : new[] { .38f, .46f, .46f };
        void Wave(float y, float a, float period, float phase)
        {
            path.Reset(); path.MoveTo(r.Left, r.Bottom); path.LineTo(r.Left, r.Top + y);
            for (float x = 0; x <= 380; x += 4) path.LineTo(r.Left + x * sx, r.Top + y + (float)Math.Sin((x + phase) / period) * a * d);
            path.LineTo(r.Right, r.Bottom); path.Close();
        }
        void Paint2(Color c, float fill, float stroke)
        {
            paint.SetStyle(Paint.Style.Fill); paint.Color = Al(c, fill * o, 255); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .8f); paint.Color = Al(c, stroke * o, 255); canvas.DrawPath(path, paint);
        }
        Wave(h * .36f, 5, 40, 0); Paint2(mist, fills[0], strokes[0]);
        int[] up = { 3, 9, 4, 14, 5, 7, 3 }, down = { 2, 1, 3, 1, 2, 1, 2 };
        float baseY = r.Top + h * (onDark ? .58f : .6f), k = onDark ? h / 64f : d;
        path.Reset(); path.MoveTo(r.Left, r.Bottom); path.LineTo(r.Left, baseY);
        for (int x = 0, i = 0; x <= 380; x += 12, i++)
        {
            int j = i % 7;
            path.LineTo(r.Left + (x + 4) * sx, baseY - up[j] * k);
            path.LineTo(r.Left + (x + 9) * sx, baseY + down[j] * (onDark ? 1 : d));
            path.LineTo(r.Left + (x + 12) * sx, baseY);
        }
        path.LineTo(r.Right, r.Bottom); path.Close(); Paint2(red, fills[1], strokes[1]);
        Wave(h * .8f, 3.5f, 28, 20); Paint2(mist, fills[2], strokes[2]);
    }

    /// <summary>Thin Red Tide hairline (8dp tall zigzag) for slim bars.</summary>
    public static void TideLine(Canvas canvas, RectF r, float d, Paint paint, Path path, int alpha = 255)
    {
        float sx = r.Width() / 380f, mid = r.Top + r.Height() * .75f, k = r.Height() / 8f;
        float[] up = { 1, 3, 1.5f, 4, 2 };
        path.Reset(); path.MoveTo(r.Left, mid);
        for (int x = 0, i = 0; x <= 380; x += 10, i++)
        { path.LineTo(r.Left + (x + 3) * sx, mid - up[i % 5] * k); path.LineTo(r.Left + (x + 7) * sx, mid + k); path.LineTo(r.Left + (x + 10) * sx, mid); }
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .8f); paint.Color = Al(TideRed, .7f, alpha); canvas.DrawPath(path, paint);
    }

    /// <summary>Slim strips: one hairline centred in the band instead of the zigzag.</summary>
    public static void TideHairline(Canvas canvas, RectF r, float d, Paint paint, int alpha = 255)
    {
        float y = r.CenterY(), half = Math.Max(1, d * .8f) / 2;
        paint.SetStyle(Paint.Style.Fill); paint.Color = Al(TideRed, .7f, alpha);
        canvas.DrawRect(r.Left, y - half, r.Right, y + half, paint);
    }

    /// <summary>Corner ticks: top-left L (crimson or brass), bottom-right L (brass), short brass bar near the top-right.</summary>
    public static void Ticks(Canvas canvas, RectF r, bool brassLead, bool onDark, float arm, float width, float inset, float d, Paint paint, int alpha = 255, bool topBar = true)
    {
        paint.SetStyle(Paint.Style.Fill);
        Color lead = brassLead ? (onDark ? GoldLine : PgrTheme.Brass) : (onDark ? TideRed : PgrTheme.Red);
        Color tail = onDark ? GoldLine : PgrTheme.Brass;
        float l = r.Left + inset, t = r.Top + inset, rr = r.Right - inset, b = r.Bottom - inset;
        paint.Color = Al(lead, 1, alpha); canvas.DrawRect(l, t, l + arm, t + width, paint); canvas.DrawRect(l, t, l + width, t + arm, paint);
        paint.Color = Al(tail, 1, alpha); canvas.DrawRect(rr - arm, b - width, rr, b, paint); canvas.DrawRect(rr - width, b - arm, rr, b, paint);
        if (topBar) canvas.DrawRect(r.Right - 52 * d, r.Top, r.Right - 26 * d, r.Top + 2 * d, paint);
    }

    /// <summary>Chamfered outline path: each corner cut by its own length (px).</summary>
    public static void Chamfer(Path path, RectF r, float tl, float tr, float br, float bl)
    {
        path.Reset();
        path.MoveTo(r.Left + tl, r.Top); path.LineTo(r.Right - tr, r.Top); path.LineTo(r.Right, r.Top + tr);
        path.LineTo(r.Right, r.Bottom - br); path.LineTo(r.Right - br, r.Bottom); path.LineTo(r.Left + bl, r.Bottom);
        path.LineTo(r.Left, r.Bottom - bl); path.LineTo(r.Left, r.Top + tl); path.Close();
    }
}

/// <summary>
/// Light archive paper (01-B / 02-B list areas, bottom sheets): grid + scan lines, slate glow, brass / slate orbit arcs,
/// F1b line-art emblem bleeding from a corner, Red Tide band along the bottom and crimson / brass / slate corner ticks.
/// </summary>
public sealed class ArchivePaperDrawable(float density) : PgrShapeDrawable(density)
{
    public Color Fill { get; set; } = Color.Transparent;
    public float CutTopRightDp { get; set; }
    public float CutBottomRightDp { get; set; }
    public float EmblemWidthDp { get; set; } = 150;
    public float EmblemRightDp { get; set; } = 10;   // distance from right edge (negative = bleeds out)
    public float EmblemBottomDp { get; set; } = 20;
    public float EmblemOpacity { get; set; } = .9f;
    public float TideDp { get; set; } = 56;
    public float OrbitOpacity { get; set; } = 1;
    public bool Ticks { get; set; } = true;
    public bool TopRedBar { get; set; }
    /// <summary>Bottom-sheet grab handle (40 × 4dp, centred at the top).</summary>
    public bool ShowHandle { get; set; }
    public float GridDp { get; set; } = 24;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b);
        PgrArt.Chamfer(path, r, 0, D(CutTopRightDp), D(CutBottomRightDp), 0);
        int save = canvas.Save(); canvas.ClipPath(path);
        if (Fill.A > 0) { paint.SetStyle(Paint.Style.Fill); paint.Color = C(Fill); canvas.DrawRect(r, paint); }
        PgrArt.Grid(canvas, r, D(GridDp), PgrTheme.Slate, .07f, d, paint, alpha);
        PgrArt.Scan(canvas, r, D(4), PgrTheme.Slate, .05f, d, paint, alpha);
        using (var glow = new RadialGradient(r.Right - D(90), r.Bottom - D(100), D(130), C(PgrTheme.Slate, 46), C(PgrTheme.Slate, 0), Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(glow); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(r, paint); paint.SetShader(null); }
        PgrArt.Orbits(canvas, r, OrbitOpacity, false, d, paint, alpha);
        PgrArt.Emblem(canvas, r.Right - D(EmblemRightDp), r.Bottom - D(EmblemBottomDp), D(EmblemWidthDp), EmblemOpacity, false, paint, alpha);
        paint.AntiAlias = true;
        if (TideDp > 0) using (var band = new RectF(r.Left, r.Bottom - D(TideDp), r.Right, r.Bottom)) PgrArt.Tide(canvas, band, 1, false, d, paint, path, alpha);
        if (Ticks)
        {
            paint.SetStyle(Paint.Style.Fill);
            paint.Color = C(PgrTheme.Red); canvas.DrawRect(r.Left, r.Top, r.Left + D(18), r.Top + D(3), paint); canvas.DrawRect(r.Left, r.Top, r.Left + D(3), r.Top + D(18), paint);
            paint.Color = C(PgrTheme.Brass); canvas.DrawRect(r.Right - D(18), r.Bottom - D(3), r.Right, r.Bottom, paint); canvas.DrawRect(r.Right - D(3), r.Bottom - D(18), r.Right, r.Bottom, paint);
            paint.Color = C(PgrTheme.Slate); canvas.DrawRect(r.Left, r.Bottom - D(2), r.Left + D(18), r.Bottom, paint); canvas.DrawRect(r.Left, r.Bottom - D(18), r.Left + D(2), r.Bottom, paint);
        }
        if (TopRedBar) { paint.Color = C(PgrTheme.Red); canvas.DrawRect(r.Left, r.Top, r.Left + D(44), r.Top + D(3), paint); }
        if (ShowHandle) { paint.Color = C(Color.ParseColor("#C3CAD2")); canvas.DrawRect(r.CenterX() - D(20), r.Top + D(7), r.CenterX() + D(20), r.Top + D(11), paint); }
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// Dark slate panel with mandatory PGR decoration (11-B2 language), tiered L / M / S. Decoration is painted under the
/// content (it is the background), max ~0.5 opacity hairlines; interactive children keep opaque plates.
/// </summary>
public sealed class DarkPanelDrawable(float density, DecoTier tier) : PgrShapeDrawable(density)
{
    public DecoTier Tier { get; set; } = tier;
    public float CutTopLeftDp { get; set; }
    public float CutTopRightDp { get; set; }
    public float CutBottomRightDp { get; set; }
    public float CutBottomLeftDp { get; set; }
    /// <summary>Panel name used by the L-tier inscription「GRAY RAVEN ARCHIVE · …」and the vertical watermark.</summary>
    public string? Mark { get; set; }
    public bool Inscription { get; set; } = true;
    public bool Watermark { get; set; } = true;
    public bool ShowEmblem { get; set; } = true;     // L default; M only when requested
    public float EmblemWidthDp { get; set; }
    public float EmblemRightDp { get; set; } = float.NaN;
    public float EmblemBottomDp { get; set; } = float.NaN;
    public float EmblemOpacity { get; set; } = float.NaN;
    public float TideDp { get; set; } = float.NaN;
    public bool ShowTide { get; set; } = true;
    /// <summary>S tier only: &gt; 0 draws a hairline band of this height (plus 1dp below) instead of the 8dp zigzag.</summary>
    public float SlimTideDp { get; set; }
    public bool TideOnTop { get; set; }
    public bool BrassLead { get; set; }
    public bool Ticks { get; set; } = true;
    public Color? FillTop { get; set; }
    public Color? FillBottom { get; set; }
    /// <summary>Opacity of the fill (cover bands draw a translucent veil over an image underneath).</summary>
    void DrawCover(Canvas canvas, RectF r, Bitmap art)
    {
        float h = CoverHeightDp > 0 ? Math.Min(r.Height(), D(CoverHeightDp)) : r.Height();
        using var area = new RectF(r.Left, r.Top, r.Right, r.Top + h);
        int save = canvas.Save(); canvas.ClipRect(area);
        float scale = Math.Max(area.Width() / art.Width, area.Height() / art.Height);
        float w = art.Width * scale, ih = art.Height * scale;
        float left = area.Left + (area.Width() - w) * .85f, topY = area.Top + (area.Height() - ih) * .3f;
        using (var dst = new RectF(left, topY, left + w, topY + ih))
        { paint.Reset(); paint.AntiAlias = true; paint.FilterBitmap = true; paint.Alpha = alpha; canvas.DrawBitmap(art, null, dst, paint); paint.Alpha = 255; }
        paint.SetStyle(Paint.Style.Fill); paint.Color = Color.White;
        // Left-to-right scrim (title side ~92% → ~48%), then a fade into the deck fill at the bottom of the cover area.
        using (var side = new LinearGradient(area.Left, 0, area.Right, 0,
            new int[] { C(PgrArt.DeckBottom, 236).ToArgb(), C(PgrArt.DeckBottom, 214).ToArgb(), C(PgrArt.DeckBottom, 150).ToArgb(), C(PgrArt.DeckBottom, 122).ToArgb() },
            new float[] { 0f, .4f, .72f, 1f }, Shader.TileMode.Clamp!))
        { paint.SetShader(side); canvas.DrawRect(area, paint); }
        var deck = FillBottom ?? PgrArt.DeckBottom;
        using (var foot = new LinearGradient(0, area.Bottom - Math.Min(h * .55f, D(120)), 0, area.Bottom, C(deck, 0), C(deck, 255), Shader.TileMode.Clamp!))
        { paint.SetShader(foot); canvas.DrawRect(area, paint); }
        paint.SetShader(null);
        canvas.RestoreToCount(save);
    }

    public int FillAlpha { get; set; } = 255;
    Bitmap? cover;
    /// <summary>Optional chapter cover behind the deco (chapter-specific dark headers). Right-weighted crop with a dark scrim
    /// from the left; null = plain deck fill (fallback).</summary>
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    /// <summary>Height of the cover area from the top (dp); 0 = whole panel. The cover fades into the deck at its bottom edge.</summary>
    public float CoverHeightDp { get; set; }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b);
        PgrArt.Chamfer(path, r, D(CutTopLeftDp), D(CutTopRightDp), D(CutBottomRightDp), D(CutBottomLeftDp));
        int save = canvas.Save(); canvas.ClipPath(path);
        var top = FillTop ?? PgrArt.DeckTop; var bottom = FillBottom ?? PgrArt.DeckBottom;
        using (var g = new LinearGradient(0, r.Top, 0, r.Bottom, C(top, top.A * FillAlpha / 255), C(bottom, bottom.A * FillAlpha / 255), Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(g); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(r, paint); paint.SetShader(null); }
        if (cover is { IsRecycled: false } art) DrawCover(canvas, r, art);
        bool s = Tier == DecoTier.S, l = Tier == DecoTier.L;
        PgrArt.Grid(canvas, r, D(s ? 16 : 22), PgrArt.MistLine, s ? .06f : .07f, d, paint, alpha);
        if (!s) PgrArt.Scan(canvas, r, D(4), Color.White, .035f, d, paint, alpha);
        if (l) PgrArt.Orbits(canvas, r, 1, true, d, paint, alpha);
        // Small arc pair in the top-right; capped so tall slim panels (nav rail) keep it near the top like the mockups.
        else using (var arc = new RectF(r.Left + r.Width() * (s ? .38f : .3f), r.Top, r.Right, r.Top + Math.Min(r.Height() * (s ? 1f : .6f), D(s ? 90 : 160)))) PgrArt.Arc(canvas, arc, 1, true, d, paint, alpha);
        if (ShowEmblem && !s && (l || EmblemWidthDp > 0))
        {
            float w = EmblemWidthDp > 0 ? EmblemWidthDp : l ? 200 : 120;
            float right = float.IsNaN(EmblemRightDp) ? (l ? -36 : -24) : EmblemRightDp;
            float bottomDp = float.IsNaN(EmblemBottomDp) ? (l ? 78 : 4) : EmblemBottomDp;
            float op = float.IsNaN(EmblemOpacity) ? (l ? .46f : .3f) : EmblemOpacity;
            PgrArt.Emblem(canvas, r.Right - D(right), r.Bottom - D(bottomDp), D(w), op, true, paint, alpha);
            paint.AntiAlias = true;
        }
        if (ShowTide)
        {
            if (l)
            {
                float th = D(float.IsNaN(TideDp) ? 64 : TideDp);
                using (var g = new LinearGradient(0, r.Bottom - th, 0, r.Bottom, new int[] { Color.Transparent.ToArgb(), C(PgrArt.TideRed, 20).ToArgb(), C(PgrArt.DeckBottom, 140).ToArgb() }, new float[] { 0, .4f, 1 }, Shader.TileMode.Clamp!))
                { paint.Color = Color.White; paint.SetShader(g); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(r.Left, r.Bottom - th, r.Right, r.Bottom, paint); paint.SetShader(null); }
                using (var band = new RectF(r.Left, r.Bottom - th, r.Right, r.Bottom)) PgrArt.Tide(canvas, band, 1, true, d, paint, path, alpha);
                if (Inscription && Mark != null)
                {
                    paint.SetStyle(Paint.Style.Fill); paint.TextSize = D(8.5f); paint.SetTypeface(PgrTheme.Condensed); paint.LetterSpacing = .26f;
                    paint.TextAlign = Paint.Align.Center; paint.Color = C(PgrArt.GoldLine, 200);
                    canvas.DrawText("GRAY RAVEN ARCHIVE · " + Mark, r.CenterX(), r.Bottom - D(8), paint);
                    paint.TextAlign = Paint.Align.Left; paint.LetterSpacing = 0;
                }
            }
            else if (Tier == DecoTier.M)
            {
                float th = D(float.IsNaN(TideDp) ? 26 : TideDp);
                using var band = new RectF(r.Left, r.Bottom - th, r.Right, r.Bottom); PgrArt.Tide(canvas, band, .8f, true, d, paint, path, alpha);
            }
            else if (SlimTideDp > 0)
            {
                using var band = new RectF(r.Left, r.Bottom - D(SlimTideDp + 1), r.Right, r.Bottom - D(1));
                PgrArt.TideHairline(canvas, band, d, paint, alpha);
            }
            else
            {
                using var band = TideOnTop ? new RectF(r.Left, r.Top + D(2), r.Right, r.Top + D(10)) : new RectF(r.Left, r.Bottom - D(9), r.Right, r.Bottom - D(1));
                PgrArt.TideLine(canvas, band, d, paint, path, alpha);
            }
        }
        if (l && Watermark && Mark != null)
        {
            paint.SetStyle(Paint.Style.Fill); paint.TextSize = D(54); paint.SetTypeface(PgrTheme.Condensed); paint.LetterSpacing = .08f;
            paint.Color = C(Color.White, 12);
            int ws = canvas.Save(); canvas.Translate(r.Right - D(18) - D(40), r.Top + D(72)); canvas.Rotate(90);
            canvas.DrawText(Mark.Split(' ')[0], 0, 0, paint); canvas.RestoreToCount(ws); paint.LetterSpacing = 0;
        }
        if (Ticks) PgrArt.Ticks(canvas, r, BrassLead, true, D(10), D(1.4f), D(3), d, paint, alpha);
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// Chapter-cover band: user-local cover (center-crop, focus 30% from top) under a dark slate veil with S-tier deco.
/// Without a cover it falls back to the plain decorated slate. Used by 01-B hero, 02-B segment / script headers.
/// </summary>
public sealed class CoverBandDrawable : PgrShapeDrawable
{
    Bitmap? cover;
    readonly DarkPanelDrawable veil;
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    /// <summary>Scrim strength at the title side (0–255; the left stop is this + 30). Hero uses a bottom-only gradient instead (VeilGradient).</summary>
    public int VeilAlpha { get; set; } = 205;
    public bool BottomShade { get; set; }
    public float CutTopRightDp { get => veil.CutTopRightDp; set => veil.CutTopRightDp = value; }
    public float CutBottomLeftDp { get => veil.CutBottomLeftDp; set => veil.CutBottomLeftDp = value; }
    public float FocusY { get; set; } = .3f;
    /// <summary>Horizontal crop anchor (0 = left edge, 1 = right edge); bands weight the art to the right.</summary>
    public float FocusX { get; set; } = .85f;
    public DarkPanelDrawable Deco => veil;

    public CoverBandDrawable(float density, DecoTier tier = DecoTier.S) : base(density)
    { veil = new DarkPanelDrawable(density, tier) { ShowEmblem = false }; }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b);
        PgrArt.Chamfer(path, r, D(veil.CutTopLeftDp), D(veil.CutTopRightDp), D(veil.CutBottomRightDp), D(veil.CutBottomLeftDp));
        int save = canvas.Save(); canvas.ClipPath(path);
        if (cover is { IsRecycled: false } img)
        {
            // Full-bleed crop, right-weighted (FocusX): the art reads on the right while the title column sits on the left.
            float scale = Math.Max(r.Width() / img.Width, r.Height() / img.Height);
            float w = img.Width * scale, h = img.Height * scale;
            float left = r.Left + (r.Width() - w) * FocusX, top = r.Top + (r.Height() - h) * FocusY;
            using var dst = new RectF(left, top, left + w, top + h);
            paint.Reset(); paint.AntiAlias = true; paint.FilterBitmap = true; paint.Alpha = alpha;
            canvas.DrawBitmap(img, null, dst, paint); paint.Alpha = 255;
            veil.FillAlpha = 0;
            if (!BottomShade)
            {
                // Dark scrim from the left keeps the title / cast (white, AA) crisp; it never drops below ~45% so the
                // outlined chapter number on the right stays legible on bright art.
                int strong = Math.Clamp(VeilAlpha + 30, 0, 250);
                using var side = new LinearGradient(r.Left, 0, r.Right, 0,
                    new int[] { C(PgrArt.DeckBottom, strong).ToArgb(), C(PgrArt.DeckBottom, strong - 20).ToArgb(), C(PgrArt.DeckBottom, 150).ToArgb(), C(PgrArt.DeckBottom, 118).ToArgb() },
                    new float[] { 0f, .38f, .7f, 1f }, Shader.TileMode.Clamp!);
                paint.Color = Color.White; paint.SetShader(side); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(r, paint);
                using var foot = new LinearGradient(0, r.Bottom, 0, r.Top + r.Height() * .4f, C(PgrTheme.Ink, 120), C(PgrTheme.Ink, 0), Shader.TileMode.Clamp!);
                paint.SetShader(foot); canvas.DrawRect(r, paint); paint.SetShader(null);
            }
        }
        else veil.FillAlpha = 255;
        if (BottomShade && cover is { IsRecycled: false })
        {
            using var g = new LinearGradient(0, r.Bottom, 0, r.Top + r.Height() * .45f, C(PgrTheme.Ink, 107), C(PgrTheme.Ink, 0), Shader.TileMode.Clamp!);
            paint.Color = Color.White; paint.SetShader(g); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(r, paint); paint.SetShader(null);
        }
        // The hero cover keeps the art clean (bottom shade only); veiled bands carry the S-tier slate deco.
        if (!(BottomShade && cover is { IsRecycled: false })) { veil.Bounds = b; veil.SetAlpha(alpha); veil.Draw(canvas); }
        canvas.RestoreToCount(save);
    }
}
