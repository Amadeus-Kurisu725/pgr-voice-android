using Android.Graphics;
using Android.Graphics.Drawables;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>Shared base for the small static drawables below: alpha/colour-filter plumbing and dp helper.</summary>
public abstract class PgrShapeDrawable : Drawable
{
    public override ConstantState? GetConstantState() => new SharedDrawableState(this);
    protected readonly Paint paint = new(PaintFlags.AntiAlias);
    protected readonly global::Android.Graphics.Path path = new();
    protected readonly float d;
    protected int alpha = 255;
    protected PgrShapeDrawable(float density) => d = density;
    protected float D(float dp) => dp * d;
    protected Color C(Color c, int a = -1) => Color.Argb((a < 0 ? c.A : a) * alpha / 255, c.R, c.G, c.B);
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
    public override int Opacity => (int)Format.Translucent;
    protected void Triangle(Canvas canvas, float x, float y, float w, float h, Color color, bool down = false)
    {
        path.Reset();
        if (down) { path.MoveTo(x, y); path.LineTo(x + w, y); path.LineTo(x + w / 2, y + h); }
        else { path.MoveTo(x, y); path.LineTo(x + w, y + h / 2); path.LineTo(x, y + h); }
        path.Close(); paint.SetStyle(Paint.Style.Fill); paint.Color = C(color); canvas.DrawPath(path, paint);
    }
}

/// <summary>
/// Script list row (desktop v7b language): index tick column, a per-character accent edge, faint alternating mist-blue
/// tint, the selected row lifted white (soft shadow) with a crimson index cell, branch rows washed brass. Static.
/// </summary>
public sealed class DirectoryRowDrawable(float density) : PgrShapeDrawable(density)
{
    public float IndexWidthDp { get; set; } = 40;
    public Color Fill { get; set; } = Color.Transparent;
    public Color? IndexFill { get; set; }
    public Color Accent { get; set; } = PgrTheme.Border;
    public Color? Outline { get; set; }
    public bool Lifted { get; set; }
    public bool Slash { get; set; }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float l = b.Left, t = b.Top, r = b.Right, bt = b.Bottom, ix = l + D(IndexWidthDp);
        paint.SetStyle(Paint.Style.Fill);
        if (Lifted)
        {
            float i = D(2);
            paint.Color = C(Fill); paint.SetShadowLayer(D(5), 0, D(1.5f), C(PgrTheme.CardShadow));
            canvas.DrawRect(l + i, t + i, r - i, bt - i, paint); paint.ClearShadowLayer();
            t += i; bt -= i; l += i; r -= i;
        }
        else if (Fill.A > 0) { paint.Color = C(Fill); canvas.DrawRect(l, t, r, bt, paint); }
        if (IndexFill is { } fill)
        {
            paint.Color = C(fill); canvas.DrawRect(l, t, ix, bt, paint);
            if (Slash)
            {
                // Blade-slash marker: one thin light diagonal cut across the crimson index cell plus a notch at its foot.
                int save = canvas.Save(); canvas.ClipRect(l, t, ix, bt);
                paint.Color = C(PgrTheme.OnAccent, 70);
                path.Reset(); path.MoveTo(ix - D(9), t); path.LineTo(ix - D(6.5f), t); path.LineTo(l + D(6.5f), bt); path.LineTo(l + D(4), bt); path.Close();
                canvas.DrawPath(path, paint);
                canvas.RestoreToCount(save);
                Triangle(canvas, l, bt - D(7), D(5), D(7), PgrTheme.RedDeep);
            }
        }
        // Index column rule with a short tick pointing at the row.
        paint.Color = C(IndexFill ?? PgrTheme.Border); canvas.DrawRect(ix - Math.Max(1, D(.75f)), t, ix, bt, paint);
        paint.Color = C(PgrTheme.BorderStrong); float cy = (t + bt) / 2; canvas.DrawRect(ix, cy - D(.5f), ix + D(5), cy + D(.5f), paint);
        // Per-character accent edge.
        paint.Color = C(Accent); canvas.DrawRect(ix + D(1), t + D(6), ix + D(4), bt - D(6), paint);
        if (Outline is { } line)
        {
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = C(line);
            canvas.DrawRect(l + D(.5f), t + D(.5f), r - D(.5f), bt - D(.5f), paint);
        }
        else if (!Lifted)
        { paint.Color = C(PgrTheme.Border, 150); canvas.DrawRect(l, bt - Math.Max(1, D(.75f)), r, bt, paint); }
    }
}

/// <summary>Bottom navigation base: angled dark-slate plate (top-right chamfer) with a thin diagonal line texture.</summary>
public sealed class NavBarDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cut = D(14), l = b.Left, t = b.Top, r = b.Right, bt = b.Bottom;
        path.Reset(); path.MoveTo(l, t); path.LineTo(r - cut, t); path.LineTo(r, t + cut); path.LineTo(r, bt); path.LineTo(l, bt); path.Close();
        int save = canvas.Save(); canvas.ClipPath(path);
        using (var g = new LinearGradient(l, 0, r, 0, C(PgrTheme.NamePlate), C(PgrTheme.NamePlateLight), Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(g); paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(l, t, r, bt, paint); paint.SetShader(null); }
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = C(PgrTheme.OnPlate, 10);
        float h = bt - t, step = D(9), slope = h * .58f;
        for (float x = l - slope; x < r; x += step) canvas.DrawLine(x, bt, x + slope, t, paint);
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// Navigation tab face. Selected: a light chamfered folder tab rising from the dark base with a crimson top tick and
/// a small crimson triangle marker. Optional status diamond (teal-grey = following, brass = pending branch).
/// </summary>
public sealed class NavTabDrawable(float density) : PgrShapeDrawable(density)
{
    public bool Selected { get; set; }
    public Color? Dot { get; set; }
    public bool Horizontal { get; set; }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float l = b.Left, t = b.Top, r = b.Right, bt = b.Bottom;
        if (Selected)
        {
            float cut = D(9);
            path.Reset(); path.MoveTo(l, t); path.LineTo(r - cut, t); path.LineTo(r, t + cut); path.LineTo(r, bt); path.LineTo(l, bt); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
            paint.Color = C(PgrTheme.Red); canvas.DrawRect(l, t, l + D(22), t + D(2), paint);
            Triangle(canvas, l + D(4), bt - D(10), D(5), D(7), PgrTheme.Red);
        }
        if (Dot is { } dot)
        {
            float cx = r - D(Selected ? 13 : 8), cy = t + D(9), s = D(3);
            path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(dot); canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Playback node track: a thin rail through the node centres with a short mist-blue lead-in.</summary>
public sealed class NodeTrackDrawable(float density) : PgrShapeDrawable(density)
{
    public float RailDp { get; set; } = 24;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float y = b.Top + D(RailDp), l = b.Left + D(4), r = b.Right - D(4);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.BorderStrong);
        canvas.DrawRect(l, y - D(.5f), r, y + D(.5f), paint);
        using (var g = new LinearGradient(l, 0, l + (r - l) * .3f, 0, C(PgrTheme.Slate), C(PgrTheme.Slate, 0), Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(g); canvas.DrawRect(l, y - D(1.5f), l + (r - l) * .3f, y + D(1.5f), paint); paint.SetShader(null); }
        Triangle(canvas, r - D(5), y - D(3.5f), D(5), D(7), PgrTheme.BorderStrong);
    }
}

/// <summary>Diamond playback node (prev / next) with a line icon inside; the hit area is the whole view.</summary>
public sealed class DiamondNodeDrawable : PgrShapeDrawable
{
    readonly Drawable icon; bool pressed, enabled = true;
    public float CenterDp { get; set; } = 24;
    public DiamondNodeDrawable(float density, string iconName) : base(density) => icon = new PgrIconDrawable(iconName, PgrTheme.Foreground, density, 15);
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    {
        bool p = state?.Contains(global::Android.Resource.Attribute.StatePressed) == true;
        bool e = state?.Contains(global::Android.Resource.Attribute.StateEnabled) == true;
        if (p == pressed && e == enabled) return false;
        pressed = p; enabled = e; InvalidateSelf(); return true;
    }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cx = b.CenterX(), cy = b.Top + D(CenterDp), s = D(20);
        path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(pressed ? PgrTheme.Pressed : PgrTheme.Raised); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.9f)); paint.Color = C(pressed ? PgrTheme.Slate : PgrTheme.BorderStrong); canvas.DrawPath(path, paint);
        int half = (int)D(7.5f);
        icon.SetBounds((int)cx - half, (int)cy - half, (int)cx + half, (int)cy + half); icon.Alpha = enabled ? alpha : alpha * 100 / 255; icon.Draw(canvas);
    }
}

/// <summary>Centred hexagonal crimson play/pause plate with a faint outer ring; dims when disabled.</summary>
public sealed class HexPlateDrawable(float density) : PgrShapeDrawable(density)
{
    bool pressed, enabled = true;
    public float CenterDp { get; set; } = 24;
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    {
        bool p = state?.Contains(global::Android.Resource.Attribute.StatePressed) == true;
        bool e = state?.Contains(global::Android.Resource.Attribute.StateEnabled) == true;
        if (p == pressed && e == enabled) return false;
        pressed = p; enabled = e; InvalidateSelf(); return true;
    }
    void Hex(float l, float t, float r, float bt, float tip)
    {
        float cy = (t + bt) / 2;
        path.Reset(); path.MoveTo(l + tip, t); path.LineTo(r - tip, t); path.LineTo(r, cy); path.LineTo(r - tip, bt); path.LineTo(l + tip, bt); path.LineTo(l, cy); path.Close();
    }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cy = b.Top + D(CenterDp), l = b.Left + D(2), r = b.Right - D(2);
        Hex(l, cy - D(23), r, cy + D(23), D(16));
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.9f)); paint.Color = C(PgrTheme.Red, enabled ? 90 : 40); canvas.DrawPath(path, paint);
        Hex(l + D(6), cy - D(18), r - D(6), cy + D(18), D(12));
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = C(!enabled ? PgrTheme.BorderStrong : pressed ? PgrTheme.RedDeep : PgrTheme.RedFill); canvas.DrawPath(path, paint);
        paint.Color = C(PgrTheme.OnAccent, 40); canvas.DrawRect(l + D(18), cy - D(18), r - D(18), cy - D(17), paint);
    }
}

/// <summary>
/// Chapter-card cover strip: the user's local chapter cover, centre-cropped into the trailing ~44% and faded towards the
/// text with a light scrim (AA kept). Without a cover: a mist-blue diagonal pattern and a faint original emblem.
/// </summary>
public sealed class CoverStripDrawable(float density) : PgrShapeDrawable(density)
{
    Bitmap? image;
    public Bitmap? Image { get => image; set { image = value; InvalidateSelf(); } }
    public float CutDp { get; set; } = PgrTheme.CutMedium;
    public float InsetDp { get; set; } = 3;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float i = D(InsetDp), l = b.Left + i, t = b.Top + i, r = b.Right - i, bt = b.Bottom - i, cut = D(CutDp);
        float sl = l + (r - l) * .56f;
        path.Reset(); path.MoveTo(sl, t); path.LineTo(r, t); path.LineTo(r, bt - cut); path.LineTo(r - cut, bt); path.LineTo(sl, bt); path.Close();
        int save = canvas.SaveLayer(sl, t, r, bt, null);
        canvas.ClipPath(path);
        if (image is { IsRecycled: false } img)
        {
            float w = r - sl, h = bt - t, scale = Math.Max(w / img.Width, h / img.Height);
            using var dst = new RectF(r - img.Width * scale, (t + bt) / 2 - img.Height * scale / 2, r, (t + bt) / 2 + img.Height * scale / 2);
            paint.Alpha = alpha; canvas.DrawBitmap(img, null, dst, paint); paint.Alpha = 255;
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Background, 70); canvas.DrawRect(sl, t, r, bt, paint);
        }
        else
        {
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = C(PgrTheme.Slate, 40);
            for (float x = sl - (bt - t); x < r; x += D(7)) canvas.DrawLine(x, bt, x + (bt - t), t, paint);
            PgrDeco.Emblem(canvas, r - D(30), (t + bt) / 2 - D(4), D(13), d, paint, alpha * 70 / 255);
        }
        // Fade into the card towards the text.
        using (var fade = new LinearGradient(sl, 0, sl + (r - sl) * .7f, 0, Color.Black, Color.Transparent, Shader.TileMode.Clamp!))
        {
            paint.Color = Color.White; paint.SetShader(fade); paint.SetStyle(Paint.Style.Fill); paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstOut!));
            canvas.DrawRect(sl, t, r, bt, paint); paint.SetXfermode(null); paint.SetShader(null);
        }
        canvas.RestoreToCount(save);
        // Thin slate frame tick on the strip edge.
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Slate, 150); canvas.DrawRect(r - D(26), t, r, t + D(1.5f), paint);
    }
}

/// <summary>Small static decoration layer: one corruption corner and/or a faint emblem, drawn on top of a card plate.</summary>
public sealed class CornerDecoDrawable(float density) : PgrShapeDrawable(density)
{
    public bool Corruption { get; set; } = true;
    public uint Seed { get; set; } = 0x5A17;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        if (Corruption)
        {
            using var area = new RectF(b.Right - D(46), b.Top + D(3), b.Right - D(3), b.Top + D(16));
            PgrDeco.Corruption(canvas, area, false, Seed, d, paint, alpha * 150 / 255);
        }
    }
}

/// <summary>Three small fading triangle markers on a crimson primary plate (leading side), drawn over the button face.</summary>
public sealed class TriangleRunDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cy = b.CenterY(), x = b.Left + D(18);
        int[] a = { 210, 130, 60 };
        for (int i = 0; i < 3; i++) Triangle(canvas, x + i * D(8), cy - D(4), D(5), D(8), PgrTheme.A(PgrTheme.OnAccent, a[i]));
    }
}

/// <summary>
/// Plate with angled (parallelogram) ends and an optional dark name-plate cap on the leading end with a crimson
/// underline — used for the section dropdown (wide cap holding the icon) and the action chips (thin slate cap).
/// </summary>
public sealed class AngledPlateDrawable(float density) : PgrShapeDrawable(density)
{
    public Color Fill { get; set; } = PgrTheme.Raised;
    public Color? Stroke { get; set; } = PgrTheme.Border;
    public float SlantDp { get; set; } = 7;
    public Color? Cap { get; set; }
    public float CapDp { get; set; }
    public bool CapUnderline { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float s = D(SlantDp), l = b.Left + .5f, t = b.Top + .5f, r = b.Right - .5f, bt = b.Bottom - .5f;
        path.Reset(); path.MoveTo(l + s, t); path.LineTo(r, t); path.LineTo(r - s, bt); path.LineTo(l, bt); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(Fill); canvas.DrawPath(path, paint);
        if (Cap is { } cap && CapDp > 0)
        {
            float cw = D(CapDp);
            using var capPath = new global::Android.Graphics.Path();
            capPath.MoveTo(l + s, t); capPath.LineTo(l + cw + s, t); capPath.LineTo(l + cw, bt); capPath.LineTo(l, bt); capPath.Close();
            paint.Color = C(cap); canvas.DrawPath(capPath, paint);
            if (CapUnderline) { paint.Color = C(PgrTheme.Red); canvas.DrawRect(l, bt - D(2), l + cw, bt, paint); }
        }
        if (Stroke is { } stroke)
        { paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = C(stroke); canvas.DrawPath(path, paint); }
    }
}

/// <summary>Tiny original squad emblem (same drawing as the page watermark) for header tags.</summary>
public sealed class EmblemDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        PgrDeco.Emblem(canvas, b.CenterX(), b.CenterY() - b.Height() * .08f, Math.Min(b.Width(), b.Height()) * .34f, d, paint, alpha);
    }
}

/// <summary>
/// Script-block decoration over the card plate: a slim chapter-cover strip behind the block header (user-local cover,
/// light scrim, faded towards the title) and a very faint crimson "Red Tide" wave watermark along the bottom. Static.
/// </summary>
public sealed class ScriptBlockDecoDrawable(float density) : PgrShapeDrawable(density)
{
    Bitmap? cover;
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    public float InsetDp { get; set; } = 3;
    public float HeaderDp { get; set; } = 34;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float i = D(InsetDp), l = b.Left + i, t = b.Top + i, r = b.Right - i, bt = b.Bottom - i;
        if (cover is { IsRecycled: false } img)
        {
            float sl = l + (r - l) * .42f, hb = t + D(HeaderDp);
            int save = canvas.SaveLayer(sl, t, r, hb, null);
            float w = r - sl, h = hb - t, scale = Math.Max(w / img.Width, h / img.Height);
            using (var dst = new RectF(r - img.Width * scale, t + (h - img.Height * scale) * .4f, r, t + (h - img.Height * scale) * .4f + img.Height * scale))
            { canvas.ClipRect(sl, t, r, hb); paint.Alpha = alpha; canvas.DrawBitmap(img, null, dst, paint); paint.Alpha = 255; }
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Background, 150); canvas.DrawRect(sl, t, r, hb, paint);
            using (var fade = new LinearGradient(sl, 0, sl + w * .65f, 0, Color.Black, Color.Transparent, Shader.TileMode.Clamp!))
            {
                paint.Color = Color.White; paint.SetShader(fade); paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstOut!));
                canvas.DrawRect(sl, t, r, hb, paint); paint.SetXfermode(null); paint.SetShader(null);
            }
            canvas.RestoreToCount(save);
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Slate, 120); canvas.DrawRect(sl + w * .4f, hb - D(.75f), r, hb, paint);
        }
        // Slate corner bracket on the square top-right corner, redrawn above the cover strip.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1.1f)); paint.Color = C(PgrTheme.Slate, 170);
        float arm = D(9); canvas.DrawLine(r - arm, t + D(.5f), r - D(.5f), t + D(.5f), paint); canvas.DrawLine(r - D(.5f), t, r - D(.5f), t + arm, paint);
        // Red Tide watermark: three faint crimson swells along the bottom edge.
        float baseY = bt - D(6);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeCap = Paint.Cap.Round;
        int[] alphas = { 34, 22, 13 };
        for (int k = 0; k < 3; k++)
        {
            float amp = D(4 + k * 1.5f), len = D(64 + k * 22), y0 = baseY - k * D(9), phase = k * D(19);
            path.Reset(); path.MoveTo(l, y0);
            for (float x = l; x <= r; x += D(3)) path.LineTo(x, y0 - amp * (float)Math.Sin((x - l + phase) / len * Math.PI * 2));
            paint.StrokeWidth = D(k == 0 ? 1.4f : 1); paint.Color = C(PgrTheme.Red, alphas[k]); canvas.DrawPath(path, paint);
        }
        paint.StrokeCap = Paint.Cap.Butt;
    }
}
