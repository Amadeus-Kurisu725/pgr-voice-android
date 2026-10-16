using Android.Graphics;
using Android.Graphics.Drawables;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// Page backdrop (palette C1): cool grey-white base; a header band with a subtle diagonal gradient and fine
/// diagonal line texture fading out downward; faint grain over the page (a tiny tiled noise bitmap generated at
/// runtime, no assets); a faint original emblem watermark and one sparing corruption edge. Static.
/// </summary>
public sealed class HudBackdropDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly float density;
    private int alpha = 255;
    private static Bitmap? grain;

    public HudBackdropDrawable(float density) => this.density = density;

    static Bitmap Grain()
    {
        if (grain != null) return grain;
        const int n = 96; var pixels = new int[n * n]; uint s = 0x9E3779B9;
        for (int i = 0; i < pixels.Length; i++)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            int a = (int)(s % 100) < 22 ? (int)(s >> 24) % 12 : 0;   // ~22% of pixels, alpha 0–11
            pixels[i] = PgrTheme.A(PgrTheme.Ink, a).ToArgb();
        }
        grain = Bitmap.CreateBitmap(pixels, n, n, Bitmap.Config.Argb8888!)!;
        return grain;
    }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds;
        if (b.Width() <= 0 || b.Height() <= 0) return;
        float d = density, band = Math.Min(b.Height(), 240 * d);
        paint.SetShader(null); paint.SetStyle(Paint.Style.Fill); paint.Alpha = 255;
        paint.Color = PgrTheme.Void; canvas.DrawRect(b, paint);
        // Header band: diagonal gradient, faded into the base at its bottom edge.
        int layer = canvas.SaveLayer(b.Left, b.Top, b.Right, b.Top + band, null);
        using (var diag = new LinearGradient(b.Left, b.Top, b.Right, b.Top + band, PgrTheme.HeaderStart, PgrTheme.HeaderEnd, Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(diag); canvas.DrawRect(b.Left, b.Top, b.Right, b.Top + band, paint); paint.SetShader(null); }
        // Fine diagonal line texture.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .5f);
        paint.Color = PgrTheme.A(PgrTheme.Ink, 12 * alpha / 255);
        for (float x = b.Left - band; x < b.Right; x += 6 * d) canvas.DrawLine(x, b.Top + band, x + band, b.Top, paint);
        paint.SetStyle(Paint.Style.Fill);
        using (var fade = new Paint())
        using (var mask = new LinearGradient(0, b.Top + band * .45f, 0, b.Top + band, Color.Black, Color.Transparent, Shader.TileMode.Clamp!))
        {
            fade.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstIn!)); fade.SetShader(mask);
            canvas.DrawRect(b.Left, b.Top, b.Right, b.Top + band, fade);
        }
        canvas.RestoreToCount(layer);
        // Faint grain over the whole page.
        using (var shader = new BitmapShader(Grain(), Shader.TileMode.Repeat!, Shader.TileMode.Repeat!))
        { paint.Color = Color.White; paint.SetShader(shader); paint.Alpha = alpha; canvas.DrawRect(b, paint); paint.SetShader(null); paint.Alpha = 255; }
        // Original station emblem as a faint watermark, lower trailing side.
        PgrDeco.Emblem(canvas, b.Right - 74 * d, b.Bottom - 210 * d, 40 * d, d, paint, 26 * alpha / 255);
        // One sparing corruption edge, bottom-left.
        using (var rot = new RectF(b.Left, b.Bottom - 26 * d, b.Left + b.Width() * .36f, b.Bottom))
            PgrDeco.Corruption(canvas, rot, true, 0x5EED, d, paint, 90 * alpha / 255);
    }

    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
}

/// <summary>
/// Hero panel used by the archive and listening headers: chamfered dark plate, fine grid, an abstract
/// ring/arc structure, crimson slash, hatch band, ruler ticks and an outlined watermark figure.
/// Original vector geometry only; it carries no playback state.
/// </summary>
public sealed class HudHeroDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly global::Android.Graphics.Path path = new();
    private readonly float density;
    private readonly string watermark, code;
    private int alpha = 255;

    public HudHeroDrawable(float density, string watermark, string code)
    { this.density = density; this.watermark = watermark; this.code = code; }

    private float D(float v) => v * density;
    private Color A(Color c, int a) => Color.Argb(a * alpha / 255, c.R, c.G, c.B);

    /// <summary>User-local chapter cover (章节封面); null draws the styled header only.</summary>
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    private Bitmap? cover;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds;
        if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b);
        float cut = D(PgrTheme.CutLarge);
        path.Reset();
        path.MoveTo(r.Left + cut, r.Top); path.LineTo(r.Right, r.Top); path.LineTo(r.Right, r.Bottom - cut);
        path.LineTo(r.Right - cut, r.Bottom); path.LineTo(r.Left, r.Bottom); path.LineTo(r.Left, r.Top + cut); path.Close();
        int save = canvas.Save();
        canvas.ClipPath(path);
        paint.SetStyle(Paint.Style.Fill);
        using (var shade = new LinearGradient(r.Left, r.Top, r.Right, r.Bottom, A(PgrTheme.HeaderEnd, 255), A(PgrTheme.HeaderStart, 255), Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(shade); canvas.DrawRect(r, paint); paint.SetShader(null); }

        if (cover is { IsRecycled: false } img)
        {
            // Cover: centre-crop, anchored right; a light scrim from the leading side keeps the dark title AA.
            float scale = Math.Max(r.Width() / img.Width, r.Height() / img.Height), w = img.Width * scale, h = img.Height * scale;
            using (var dst = new RectF(r.Right - w, r.CenterY() - h / 2, r.Right, r.CenterY() + h / 2))
            { paint.Alpha = alpha; canvas.DrawBitmap(img, null, dst, paint); paint.Alpha = 255; }
            using var scrim = new LinearGradient(r.Left, 0, r.Right, 0,
                new int[] { A(PgrTheme.HeaderEnd, 242).ToArgb(), A(PgrTheme.HeaderEnd, 214).ToArgb(), A(PgrTheme.HeaderEnd, 0).ToArgb() },
                new float[] { 0, .5f, .9f }, Shader.TileMode.Clamp!);
            paint.Color = Color.White; paint.SetShader(scrim); canvas.DrawRect(r, paint); paint.SetShader(null);
        }
        else
        {
            // Styled header: broken slate rings and a large outlined numeral on the trailing side.
            float cx = r.Right - r.Height() * .38f, cy = r.CenterY() + r.Height() * .08f;
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1);
            for (int i = 0; i < 3; i++)
            {
                float radius = r.Height() * (.3f + i * .16f);
                using var oval = new RectF(cx - radius, cy - radius, cx + radius, cy + radius);
                paint.Color = A(PgrTheme.Slate, 60 - i * 14);
                canvas.DrawArc(oval, -150 + i * 18, 200 - i * 22, false, paint);
            }
            if (watermark.Length > 0)
            {
                paint.SetTypeface(PgrTheme.Condensed); paint.TextSize = r.Height() * .78f; paint.TextAlign = Paint.Align.Right;
                paint.FontFeatureSettings = "tnum"; paint.StrokeWidth = D(1.2f); paint.Color = A(PgrTheme.Ink, 60);
                canvas.DrawText(watermark, r.Right - D(40), r.Bottom - r.Height() * .1f, paint);
                paint.TextAlign = Paint.Align.Left; paint.FontFeatureSettings = null;
            }
        }

        // Fine diagonal line texture on the leading half.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.5f)); paint.Color = A(PgrTheme.Ink, 10);
        for (float x = r.Left - r.Height(); x < r.Left + r.Width() * .55f; x += D(6)) canvas.DrawLine(x, r.Bottom, x + r.Height(), r.Top, paint);
        // Dark slanted bar (name-plate colour) on the trailing edge.
        paint.SetStyle(Paint.Style.Fill); paint.Color = A(PgrTheme.NamePlate, 230);
        using (var slash = new global::Android.Graphics.Path())
        {
            float w = D(7), x0 = r.Right - D(26);
            slash.MoveTo(x0, r.Top); slash.LineTo(x0 + w, r.Top); slash.LineTo(x0 + w - r.Height() * .35f, r.Bottom); slash.LineTo(x0 - r.Height() * .35f, r.Bottom); slash.Close();
            canvas.DrawPath(slash, paint);
        }
        // Bottom ruler.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f));
        for (int i = 0; ; i++)
        {
            float x = r.Left + D(18) + i * D(8);
            if (x > r.Left + r.Width() * .45f) break;
            paint.Color = A(PgrTheme.Ink, i % 5 == 0 ? 70 : 34);
            canvas.DrawLine(x, r.Bottom - D(1), x, r.Bottom - D(i % 5 == 0 ? 8 : 5), paint);
        }
        // Sparing corruption along the trailing bottom edge.
        using (var rot = new RectF(r.Left + r.Width() * .6f, r.Bottom - D(12), r.Right, r.Bottom))
            PgrDeco.Corruption(canvas, rot, true, PortraitStore.Hash(watermark + code), density, paint, 120 * alpha / 255);
        // Original station emblem + micro code label, top-trailing.
        PgrDeco.Emblem(canvas, r.Right - D(58), r.Top + D(22), D(9), density, paint, alpha);
        if (code.Length > 0)
        {
            paint.SetStyle(Paint.Style.Fill); paint.Color = A(PgrTheme.Steel, 230);
            paint.SetTypeface(PgrTheme.Mono); paint.TextSize = D(8.5f);
            paint.LetterSpacing = .12f; paint.TextAlign = Paint.Align.Right;
            canvas.DrawText(code, r.Right - D(76), r.Top + D(16), paint);
            paint.LetterSpacing = 0; paint.TextAlign = Paint.Align.Left;
        }
        canvas.RestoreToCount(save);

        // Hairline outline, crimson triangle marker in the leading cut corner, slate brackets on the uncut corners.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = A(PgrTheme.Border, 255);
        canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Fill); paint.Color = A(PgrTheme.Red, 255);
        using (var tri = new global::Android.Graphics.Path())
        { tri.MoveTo(r.Left, r.Top + cut + D(3)); tri.LineTo(r.Left + cut + D(3), r.Top); tri.LineTo(r.Left + cut + D(3) - D(7), r.Top); tri.LineTo(r.Left, r.Top + cut + D(3) - D(7)); tri.Close(); canvas.DrawPath(tri, paint); }
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.5f); paint.Color = A(PgrTheme.Slate, 200);
        float l = D(10), o = D(.75f);
        canvas.DrawLine(r.Right - o, r.Top + o, r.Right - l, r.Top + o, paint); canvas.DrawLine(r.Right - o, r.Top + o, r.Right - o, r.Top + l, paint);
        canvas.DrawLine(r.Left + o, r.Bottom - o, r.Left + l, r.Bottom - o, paint); canvas.DrawLine(r.Left + o, r.Bottom - o, r.Left + o, r.Bottom - l, paint);
    }

    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); path.Dispose(); } base.Dispose(disposing); }
}

/// <summary>HUD divider: hairline with a crimson lead tick and sparse scale marks.</summary>
public sealed class HudRuleDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly float density;
    private int alpha = 255;
    public HudRuleDrawable(float density) => this.density = density;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float y = b.CenterY();
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = Color.Argb(alpha, PgrTheme.Border.R, PgrTheme.Border.G, PgrTheme.Border.B);
        canvas.DrawRect(b.Left, y, b.Right, y + Math.Max(1, density * .75f), paint);
        // Crimson triangle marker at the leading end.
        paint.Color = Color.Argb(alpha, PgrTheme.Red.R, PgrTheme.Red.G, PgrTheme.Red.B);
        using (var tri = new global::Android.Graphics.Path())
        { tri.MoveTo(b.Left, y - 3 * density); tri.LineTo(b.Left + 6 * density, y + .4f * density); tri.LineTo(b.Left, y + 3.8f * density); tri.Close(); canvas.DrawPath(tri, paint); }
    }
    public override int IntrinsicHeight => (int)(6 * density);
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
}

/// <summary>
/// Segmented holographic bar: evenly spaced cells, vertically centred. Used as SeekBar/ProgressBar track
/// (dim cyan) and, clipped, as the progress layer (crimson with bloom). Static, level-driven only.
/// </summary>
public sealed class HudSegmentDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly float density;
    private readonly Color color, glow;
    private int alpha = 255;
    public HudSegmentDrawable(float density, Color color, Color glow) { this.density = density; this.color = color; this.glow = glow; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float h = 5 * density, cell = 7 * density, gap = 2.5f * density, top = b.CenterY() - h / 2;
        paint.Color = Color.Argb(color.A * alpha / 255, color.R, color.G, color.B);
        if (glow.A > 0) paint.SetShadowLayer(4 * density, 0, 0, glow);
        for (float x = b.Left; x < b.Right; x += cell + gap) canvas.DrawRect(x, top, Math.Min(b.Right, x + cell), top + h, paint);
        paint.ClearShadowLayer();
    }
    public override int IntrinsicHeight => (int)(12 * density);
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
}

/// <summary>
/// Spectrum-style progress strip for the playing line. Bar heights are a fixed pattern seeded by the line id
/// (it is NOT a live audio level meter); bars left of <see cref="Progress"/> glow crimson, the rest stay cyan-dim.
/// Redrawn only when progress changes.
/// </summary>
public sealed class HudWaveDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly float density;
    private float[] heights = Array.Empty<float>();
    private int seed;
    private float progress;
    private int alpha = 255;
    public HudWaveDrawable(float density) => this.density = density;
    public float Progress
    {
        get => progress;
        set { float next = Math.Clamp(value, 0, 1); if (Math.Abs(next - progress) < .004f) return; progress = next; InvalidateSelf(); }
    }
    public void SetSeed(string? key)
    {
        int next = key?.GetHashCode() ?? 0; if (next == seed && heights.Length > 0) return;
        seed = next; var random = new Random(seed); heights = new float[64];
        for (int i = 0; i < heights.Length; i++)
        {
            double envelope = .35 + .65 * Math.Sin(Math.PI * (i + .5) / heights.Length);
            heights[i] = (float)Math.Clamp(envelope * (.35 + .65 * random.NextDouble()), .12, 1);
        }
        InvalidateSelf();
    }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        if (heights.Length == 0) SetSeed(null);
        float step = 4.5f * density, bar = 2.5f * density, mid = b.CenterY(), maxH = b.Height() * .92f;
        int count = Math.Max(1, (int)(b.Width() / step));
        // Centre line.
        paint.Color = Color.Argb(70 * alpha / 255, PgrTheme.Slate.R, PgrTheme.Slate.G, PgrTheme.Slate.B);
        canvas.DrawRect(b.Left, mid - density * .25f, b.Right, mid + density * .25f, paint);
        for (int i = 0; i < count; i++)
        {
            float x = b.Left + i * step, h = Math.Max(2 * density, heights[i * heights.Length / count] * maxH);
            bool played = (i + .5f) / count <= progress;
            if (played) { paint.Color = Color.Argb(alpha, PgrTheme.Red.R, PgrTheme.Red.G, PgrTheme.Red.B); }
            else { paint.ClearShadowLayer(); paint.Color = Color.Argb(110 * alpha / 255, PgrTheme.Slate.R, PgrTheme.Slate.G, PgrTheme.Slate.B); }
            canvas.DrawRect(x, mid - h / 2, x + bar, mid + h / 2, paint);
        }
        paint.ClearShadowLayer();
    }
    public override int IntrinsicHeight => (int)(30 * density);
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
}
