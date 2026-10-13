using Android.Graphics;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 15-B2 frame ① floating ball (58dp, fusion style): cold-white archive paper body with a faint 22-unit grid, a dark
/// diagonal top plate (22 → 12 units), a brass orbit arc crossing the seam (desktop 02A motif: brass end nodes and a
/// station diamond), crimson left-top bar and crimson base tick, chamfered top-right / bottom-left (12) with a dark
/// hairline so the ball separates from both dark and bright game scenes. Drawn in a 58-unit box scaled to the bounds;
/// only the skin changes (size, drag and tap behaviour stay in OverlayController).
/// </summary>
public sealed class FloatingBallDrawable(float density) : PgrShapeDrawable(density)
{
    static readonly Color Paper = Color.Argb(248, 238, 241, 244);
    static readonly Color GridInk = Color.Argb(14, 29, 35, 42);
    static readonly Color PlateTop = Color.ParseColor("#38414C"), PlateBottom = Color.ParseColor("#272D35");
    static readonly Color Brass = Color.Argb(230, 201, 162, 74), BrassNode = Color.ParseColor("#C9A24A");
    static readonly Color RedBar = Color.ParseColor("#E0485C"), RedBase = Color.ParseColor("#B33144");
    static readonly Color Edge = Color.Argb(150, 29, 35, 42);

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float s = Math.Min(b.Width(), b.Height()) / 58f;
        float ox = b.Left + (b.Width() - 58 * s) / 2, oy = b.Top + (b.Height() - 58 * s) / 2;
        float X(float v) => ox + v * s; float Y(float v) => oy + v * s;
        using var r = new RectF(X(0), Y(0), X(58), Y(58));
        PgrArt.Chamfer(path, r, 0, 12 * s, 0, 12 * s);
        int save = canvas.Save(); canvas.ClipPath(path);
        paint.Reset(); paint.AntiAlias = true; paint.SetStyle(Paint.Style.Fill);
        paint.Color = C(Paper); canvas.DrawRect(r, paint);
        paint.Color = C(GridInk); float hair = Math.Max(1, D(.6f));
        for (float g = 22; g < 58; g += 22) { canvas.DrawRect(X(g), r.Top, X(g) + hair, r.Bottom, paint); canvas.DrawRect(r.Left, Y(g), r.Right, Y(g) + hair, paint); }
        // Dark diagonal top plate.
        using (var plate = new global::Android.Graphics.Path())
        {
            plate.MoveTo(X(0), Y(0)); plate.LineTo(X(58), Y(0)); plate.LineTo(X(58), Y(12)); plate.LineTo(X(0), Y(22)); plate.Close();
            using var g = new LinearGradient(0, Y(0), 0, Y(22), C(PlateTop), C(PlateBottom), Shader.TileMode.Clamp!);
            paint.SetShader(g); paint.Color = Color.White; canvas.DrawPath(plate, paint); paint.SetShader(null);
            // S-tier cue on the plate: faint mist grid tick + brass corner tick under the chamfer.
            paint.Color = C(PgrArt.MistLine, 22); canvas.DrawRect(X(22), Y(0), X(22) + hair, Y(18), paint);
            paint.Color = C(PgrArt.GoldLine, 200); canvas.DrawRect(X(36), Y(2), X(44), Y(2) + Math.Max(1, D(1)), paint);
        }
        // Brass orbit arc across the seam (M6 44 C16 26 34 16 54 16), end nodes and the station diamond.
        using (var arc = new global::Android.Graphics.Path())
        {
            arc.MoveTo(X(6), Y(44)); arc.CubicTo(X(16), Y(26), X(34), Y(16), X(54), Y(16));
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, 1.2f * s); paint.Color = C(Brass); canvas.DrawPath(arc, paint);
        }
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(BrassNode);
        canvas.DrawCircle(X(6), Y(44), Math.Max(1, .9f * s), paint);
        canvas.DrawCircle(X(54), Y(16), Math.Max(1, .9f * s), paint);
        using (var diamond = new global::Android.Graphics.Path())
        {
            float cx = X(44), cy = Y(17.5f), k = 2.6f * s;
            diamond.MoveTo(cx, cy - k); diamond.LineTo(cx + k, cy); diamond.LineTo(cx, cy + k); diamond.LineTo(cx - k, cy); diamond.Close();
            canvas.DrawPath(diamond, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, .8f * s); paint.Color = C(RedBar); canvas.DrawPath(diamond, paint);
        }
        // Crimson left-top bar and base tick.
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = C(RedBar); canvas.DrawRect(X(0), Y(0), X(3), Y(22), paint);
        paint.Color = C(RedBase); canvas.DrawRect(X(0), Y(55), X(30), Y(58), paint);
        canvas.RestoreToCount(save);
        // Dark hairline around the chamfered outline.
        using var edge = new RectF(r.Left + hair / 2, r.Top + hair / 2, r.Right - hair / 2, r.Bottom - hair / 2);
        PgrArt.Chamfer(path, edge, 0, 12 * s, 0, 12 * s);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = hair; paint.Color = C(Edge); canvas.DrawPath(path, paint);
    }
}
