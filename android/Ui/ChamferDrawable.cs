using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Runtime;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>Which corners of a panel are cut diagonally (PGR-style chamfer).</summary>
[Flags]
public enum Corners { None = 0, TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8, Diagonal = TopLeft | BottomRight, All = 15 }

/// <summary>Where the short signal bar of a panel is drawn.</summary>
public enum AccentEdge { None, Left, Bottom, Top }

/// <summary>
/// Immutable geometry/colour recipe for a chamfered panel. All lengths are already in pixels so a
/// recipe can be shared between drawables (ripple masks, state clones) without a Context.
/// </summary>
[Register("cn/pgrvoice/player/ui/ChamferState")]
public sealed class ChamferState : Drawable.ConstantState
{
    public Color Fill { get; init; } = Color.Transparent;
    public Color Stroke { get; init; } = Color.Transparent;
    public float StrokeWidth { get; init; } = 1;
    public Corners Cuts { get; init; } = Corners.Diagonal;
    public float Cut { get; init; }
    public Color Accent { get; init; } = Color.Transparent;
    public AccentEdge AccentEdge { get; init; } = AccentEdge.None;
    public float AccentThickness { get; init; }
    /// <summary>0 = span the edge (minus chamfers); otherwise a short centred tick.</summary>
    public float AccentLength { get; init; }
    /// <summary>Thin HUD corner brackets drawn at the corners that are not cut.</summary>
    public Color Brackets { get; init; } = Color.Transparent;
    public float BracketLength { get; init; }
    /// <summary>Very faint diagonal hatching band at the trailing edge.</summary>
    public Color Hatch { get; init; } = Color.Transparent;
    public float HatchWidth { get; init; }
    public float HatchGap { get; init; }
    /// <summary>Neon bloom colour for the stroke and signal bar (drawn with a shadow layer, HW-accelerated on API 28+).</summary>
    public Color Glow { get; init; } = Color.Transparent;
    public float GlowRadius { get; init; }
    /// <summary>Glass sheen: light gradient from the top edge, fading out by ~45% height.</summary>
    public Color Sheen { get; init; } = Color.Transparent;
    /// <summary>Horizontal scanlines inside the shape.</summary>
    public Color Scan { get; init; } = Color.Transparent;
    public float ScanGap { get; init; }
    /// <summary>Notched frame: a shallow step cut into the top edge (offset from the left, width, depth).</summary>
    public float NotchOffset { get; init; }
    public float NotchWidth { get; init; }
    public float NotchDepth { get; init; }
    /// <summary>Shape inset inside the bounds so the bloom has room inside the view's own clip.</summary>
    public float Inset { get; init; }
    /// <summary>Soft drop shadow drawn only outside the shape (so translucent fills stay clean). Needs Inset room.</summary>
    public Color Shadow { get; init; } = Color.Transparent;
    public float ShadowRadius { get; init; }
    public float ShadowDy { get; init; }

    public ChamferState() { }
    public override global::Android.Content.PM.ConfigChanges ChangingConfigurations => 0;
    public override Drawable NewDrawable() => new ChamferDrawable(this);
    public override Drawable NewDrawable(Resources? resources) => new ChamferDrawable(this);
}

/// <summary>Canvas-drawn cut-corner surface. Original geometry; no bitmap or game-derived asset.</summary>
[Register("cn/pgrvoice/player/ui/ChamferDrawable")]
public sealed class ChamferDrawable : Drawable
{
    private readonly ChamferState state;
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly global::Android.Graphics.Path path = new();
    private int alpha = 255;

    public ChamferDrawable(ChamferState state) => this.state = state;
    public ChamferState Spec => state;
    public override ConstantState GetConstantState() => state;

    private global::Android.Graphics.Path Shape(global::Android.Graphics.Path path, RectF r, float cut, Corners cuts) =>
        Shape(path, r, cut, cuts, state.NotchOffset, state.NotchWidth, state.NotchDepth);

    private static global::Android.Graphics.Path Shape(global::Android.Graphics.Path path, RectF r, float cut, Corners cuts,
        float notchOffset, float notchWidth, float notchDepth)
    {
        path.Reset();
        float c = Math.Min(cut, Math.Min(r.Width(), r.Height()) / 2f);
        float tl = cuts.HasFlag(Corners.TopLeft) ? c : 0, tr = cuts.HasFlag(Corners.TopRight) ? c : 0;
        float br = cuts.HasFlag(Corners.BottomRight) ? c : 0, bl = cuts.HasFlag(Corners.BottomLeft) ? c : 0;
        path.MoveTo(r.Left + tl, r.Top);
        if (notchWidth > 0 && notchDepth > 0 && r.Left + tl + notchOffset + notchWidth + notchDepth * 2 < r.Right - tr)
        {
            // Notched frame: slanted step down, flat run, slanted step back up.
            float x = r.Left + tl + notchOffset;
            path.LineTo(x, r.Top); path.LineTo(x + notchDepth, r.Top + notchDepth);
            path.LineTo(x + notchDepth + notchWidth, r.Top + notchDepth); path.LineTo(x + notchDepth * 2 + notchWidth, r.Top);
        }
        path.LineTo(r.Right - tr, r.Top); if (tr > 0) path.LineTo(r.Right, r.Top + tr);
        path.LineTo(r.Right, r.Bottom - br); if (br > 0) path.LineTo(r.Right - br, r.Bottom);
        path.LineTo(r.Left + bl, r.Bottom); if (bl > 0) path.LineTo(r.Left, r.Bottom - bl);
        path.LineTo(r.Left, r.Top + tl); if (tl > 0) path.LineTo(r.Left + tl, r.Top);
        path.Close();
        return path;
    }

    private int Mix(Color color) => Color.Argb(color.A * alpha / 255, color.R, color.G, color.B).ToArgb();

    public override void Draw(Canvas canvas)
    {
        var b = Bounds;
        if (b.Width() <= 0 || b.Height() <= 0) return;
        using var rect = new RectF(b);
        if (state.Inset > 0) rect.Inset(state.Inset, state.Inset);
        if (rect.Width() <= 0 || rect.Height() <= 0) return;
        float c = Math.Min(state.Cut, Math.Min(rect.Width(), rect.Height()) / 2f);
        bool glow = state.Glow.A > 0 && state.GlowRadius > 0;
        paint.SetStyle(Paint.Style.Fill);
        if (state.Shadow.A > 0 && state.ShadowRadius > 0)
        {
            var outline = Shape(path, rect, c, state.Cuts);
            int clip = canvas.Save();
            canvas.ClipOutPath(outline);
            paint.Color = new Color(Mix(Color.Argb(255, state.Shadow.R, state.Shadow.G, state.Shadow.B)));
            paint.SetShadowLayer(state.ShadowRadius, 0, state.ShadowDy, new Color(Mix(state.Shadow)));
            canvas.DrawPath(outline, paint);
            paint.ClearShadowLayer();
            canvas.RestoreToCount(clip);
        }
        if (state.Fill.A > 0)
        {
            paint.Color = new Color(Mix(state.Fill));
            if (glow && state.Stroke.A == 0) paint.SetShadowLayer(state.GlowRadius, 0, 0, new Color(Mix(state.Glow)));
            canvas.DrawPath(Shape(path, rect, c, state.Cuts), paint);
            paint.ClearShadowLayer();
        }
        if (state.Sheen.A > 0 || (state.Scan.A > 0 && state.ScanGap > 0))
        {
            int clip = canvas.Save();
            canvas.ClipPath(Shape(path, rect, c, state.Cuts));
            if (state.Sheen.A > 0)
            {
                using var sheen = new LinearGradient(0, rect.Top, 0, rect.Top + rect.Height() * .45f,
                    new Color(Mix(state.Sheen)), new Color(Mix(Color.Argb(0, state.Sheen.R, state.Sheen.G, state.Sheen.B))), Shader.TileMode.Clamp!);
                paint.Color = Color.White; paint.SetShader(sheen); canvas.DrawRect(rect, paint); paint.SetShader(null);
            }
            if (state.Scan.A > 0 && state.ScanGap > 0)
            {
                paint.Color = new Color(Mix(state.Scan)); float line = Math.Max(1, state.ScanGap / 3f);
                for (float y = rect.Top + state.ScanGap; y < rect.Bottom; y += state.ScanGap) canvas.DrawRect(rect.Left, y, rect.Right, y + line, paint);
            }
            canvas.RestoreToCount(clip);
        }
        if (state.Hatch.A > 0 && state.HatchWidth > 0)
        {
            int save = canvas.Save();
            canvas.ClipPath(Shape(path, rect, c, state.Cuts));
            paint.Color = new Color(Mix(state.Hatch)); paint.StrokeWidth = Math.Max(1, state.StrokeWidth);
            paint.SetStyle(Paint.Style.Stroke);
            float gap = Math.Max(3, state.HatchGap), start = Math.Max(rect.Left, rect.Right - state.HatchWidth);
            canvas.ClipRect(start, rect.Top, rect.Right, rect.Bottom);
            for (float x = start - rect.Height(); x < rect.Right; x += gap)
                canvas.DrawLine(x, rect.Bottom, x + rect.Height(), rect.Top, paint);
            canvas.RestoreToCount(save);
            paint.SetStyle(Paint.Style.Fill);
        }
        if (state.Stroke.A > 0 && state.StrokeWidth > 0)
        {
            float half = state.StrokeWidth / 2f;
            using var inner = new RectF(rect.Left + half, rect.Top + half, rect.Right - half, rect.Bottom - half);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = state.StrokeWidth; paint.Color = new Color(Mix(state.Stroke));
            if (glow) paint.SetShadowLayer(state.GlowRadius, 0, 0, new Color(Mix(state.Glow)));
            canvas.DrawPath(Shape(path, inner, Math.Max(0, c - half * .4f), state.Cuts), paint);
            paint.ClearShadowLayer();
            paint.SetStyle(Paint.Style.Fill);
        }
        if (state.Brackets.A > 0 && state.BracketLength > 0)
        {
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, state.StrokeWidth * 1.5f);
            paint.Color = new Color(Mix(state.Brackets));
            float l = Math.Min(state.BracketLength, Math.Min(rect.Width(), rect.Height()) / 3f), o = paint.StrokeWidth / 2f;
            if (!state.Cuts.HasFlag(Corners.TopLeft)) { canvas.DrawLine(rect.Left + o, rect.Top + o, rect.Left + l, rect.Top + o, paint); canvas.DrawLine(rect.Left + o, rect.Top + o, rect.Left + o, rect.Top + l, paint); }
            if (!state.Cuts.HasFlag(Corners.TopRight)) { canvas.DrawLine(rect.Right - o, rect.Top + o, rect.Right - l, rect.Top + o, paint); canvas.DrawLine(rect.Right - o, rect.Top + o, rect.Right - o, rect.Top + l, paint); }
            if (!state.Cuts.HasFlag(Corners.BottomRight)) { canvas.DrawLine(rect.Right - o, rect.Bottom - o, rect.Right - l, rect.Bottom - o, paint); canvas.DrawLine(rect.Right - o, rect.Bottom - o, rect.Right - o, rect.Bottom - l, paint); }
            if (!state.Cuts.HasFlag(Corners.BottomLeft)) { canvas.DrawLine(rect.Left + o, rect.Bottom - o, rect.Left + l, rect.Bottom - o, paint); canvas.DrawLine(rect.Left + o, rect.Bottom - o, rect.Left + o, rect.Bottom - l, paint); }
            paint.SetStyle(Paint.Style.Fill);
        }
        if (state.Accent.A > 0 && state.AccentThickness > 0 && state.AccentEdge != AccentEdge.None)
        {
            paint.Color = new Color(Mix(state.Accent));
            if (glow) paint.SetShadowLayer(state.GlowRadius, 0, 0, new Color(Mix(state.Glow)));
            float t = state.AccentThickness;
            switch (state.AccentEdge)
            {
                case AccentEdge.Left:
                {
                    float top = rect.Top + (state.Cuts.HasFlag(Corners.TopLeft) ? c : 0), bottom = rect.Bottom - (state.Cuts.HasFlag(Corners.BottomLeft) ? c : 0);
                    if (state.AccentLength > 0) { float mid = rect.CenterY(), h = Math.Min(state.AccentLength, bottom - top) / 2f; top = mid - h; bottom = mid + h; }
                    canvas.DrawRect(rect.Left, top, rect.Left + t, bottom, paint); break;
                }
                case AccentEdge.Bottom:
                {
                    float left = rect.Left + (state.Cuts.HasFlag(Corners.BottomLeft) ? c : 0), right = rect.Right - (state.Cuts.HasFlag(Corners.BottomRight) ? c : 0);
                    if (state.AccentLength > 0) { float mid = rect.CenterX(), w = Math.Min(state.AccentLength, right - left) / 2f; left = mid - w; right = mid + w; }
                    canvas.DrawRect(left, rect.Bottom - t, right, rect.Bottom, paint); break;
                }
                case AccentEdge.Top:
                {
                    float left = rect.Left + (state.Cuts.HasFlag(Corners.TopLeft) ? c : 0), right = rect.Right - (state.Cuts.HasFlag(Corners.TopRight) ? c : 0);
                    if (state.AccentLength > 0) { float mid = rect.CenterX(), w = Math.Min(state.AccentLength, right - left) / 2f; left = mid - w; right = mid + w; }
                    canvas.DrawRect(left, rect.Top, right, rect.Top + t, paint); break;
                }
            }
            paint.ClearShadowLayer();
        }
    }

    public override void GetOutline(Outline outline)
    {
        var b = Bounds;
        if (b.Width() <= 0 || b.Height() <= 0) { base.GetOutline(outline); return; }
        using var rect = new RectF(b);
        if (state.Inset > 0) rect.Inset(state.Inset, state.Inset);
        var shape = Shape(new global::Android.Graphics.Path(), rect, state.Cut, state.Cuts, 0, 0, 0);
        if (OperatingSystem.IsAndroidVersionAtLeast(30)) outline.SetPath(shape);
        else
        {
#pragma warning disable CA1422
            outline.SetConvexPath(shape);
#pragma warning restore CA1422
        }
        outline.Alpha = state.Fill.A / 255f;
    }

    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); path.Dispose(); } base.Dispose(disposing); }
}
