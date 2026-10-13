using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Path = Android.Graphics.Path;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>10-A3 refined button variants (SPEC 10-A3; danger two-tier per 12-A).</summary>
public enum Xb { Normal, Primary, Dark, Brass, DangerOutline, DangerSolid }

/// <summary>
/// 10-A3 refined button face: cut top-right / bottom-left corners, outer hairline + inset fill, trapezoid left notch,
/// tiny crimson / brass corner ticks and a condensed English tag on the right (painted here so the Button text stays the
/// plain Chinese label for TalkBack). Tracks enabled / pressed state itself.
/// </summary>
public sealed class RefinedButtonDrawable(float density, Xb kind) : PgrShapeDrawable(density)
{
    public Xb Kind { get; set; } = kind;
    public string? English { get; set; }
    public float EnglishPx { get; set; } = 9 * density;
    public bool Tail { get; set; }
    public bool Wing { get; set; }
    /// <summary>0.4.3 fusion deco drawn inside the face (light kinds only).</summary>
    public FusionDeco? Deco { get; set; }
    bool enabled = true, pressed;

    public override bool IsStateful => true;
    protected override bool OnStateChange(int[] state)
    {
        bool e = state.Contains(global::Android.Resource.Attribute.StateEnabled);
        bool p = state.Contains(global::Android.Resource.Attribute.StatePressed);
        if (e == enabled && p == pressed) return false;
        enabled = e; pressed = p; InvalidateSelf(); return true;
    }

    /// <summary>Width the face paints on the right (English tag + tail chevron), for the Button's end padding.</summary>
    public float TrailingWidth(Paint measure)
    {
        float w = D(14);
        if (!string.IsNullOrEmpty(English)) { Setup(measure); w += measure.MeasureText(English) + D(8); }
        if (Wing) w += D(22);
        if (Tail) w += D(18);
        return w;
    }

    void Setup(Paint p) { p.TextSize = EnglishPx; p.SetTypeface(PgrTheme.Condensed); p.LetterSpacing = .16f; }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        int a0 = alpha;
        // 0.4.7 P2: a disabled solid key (primary / danger-solid) is drawn as a light normal face with a gray label, like a
        // disabled 停止自动播放, instead of a faded crimson plate under white text.
        var fk = !enabled && Kind is Xb.Primary or Xb.DangerSolid ? Xb.Normal : Kind;
        if (!enabled && fk == Kind) alpha = alpha * 120 / 255;
        using var r = new RectF(b);
        float cut = D(8), one = Math.Max(1, D(1));
        Color line, fillTop, fillMid, fillBottom;
        switch (fk)
        {
            case Xb.Primary: line = Color.ParseColor("#7E1E2C"); fillTop = Color.ParseColor("#B83445"); fillMid = Color.ParseColor("#A3283A"); fillBottom = Color.ParseColor("#8F2232"); break;
            case Xb.DangerSolid: line = PgrTheme.RedDeep; fillTop = Color.ParseColor("#9C2536"); fillMid = Color.ParseColor("#8F2232"); fillBottom = Color.ParseColor("#7E1E2C"); break;
            case Xb.Dark: line = PgrTheme.Ink; fillTop = PgrArt.DeckTop; fillMid = Color.ParseColor("#313942"); fillBottom = PgrArt.DeckBottom; break;
            case Xb.Brass: line = PgrTheme.Brass; fillTop = fillMid = fillBottom = Color.ParseColor("#FBF8F1"); break;
            case Xb.DangerOutline: line = PgrTheme.Red; fillTop = fillMid = fillBottom = Color.White; break;
            default: line = Color.ParseColor("#AAB4BE"); fillTop = fillMid = fillBottom = Color.ParseColor("#F9FAFB"); break;
        }
        PgrArt.Chamfer(path, r, 0, cut, 0, cut);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(line); canvas.DrawPath(path, paint);
        using var inner = new RectF(r.Left + one, r.Top + one, r.Right - one, r.Bottom - one);
        PgrArt.Chamfer(path, inner, 0, cut - one * .6f, 0, cut - one * .6f);
        int save = canvas.Save(); canvas.ClipPath(path);
        using (var g = new LinearGradient(0, inner.Top, 0, inner.Bottom, new int[] { C(fillTop).ToArgb(), C(fillMid).ToArgb(), C(fillBottom).ToArgb() }, new float[] { 0, .55f, 1 }, Shader.TileMode.Clamp!))
        { paint.Color = Color.White; paint.SetShader(g); canvas.DrawRect(inner, paint); paint.SetShader(null); }
        bool solid = fk is Xb.Primary or Xb.DangerSolid;
        if (solid)
        {
            // Scanlines + one diagonal sheen; danger-solid adds hazard stripes on the trailing end.
            paint.Color = C(Color.White, 13);
            for (float y = inner.Top; y < inner.Bottom; y += D(3)) canvas.DrawRect(inner.Left, y, inner.Right, y + Math.Max(1, D(.6f)), paint);
            paint.Color = C(Color.White, 20); path.Reset();
            float sx = inner.Left + inner.Width() * .58f; path.MoveTo(sx, inner.Top); path.LineTo(sx + D(26), inner.Top); path.LineTo(sx + D(6), inner.Bottom); path.LineTo(sx - D(20), inner.Bottom); path.Close();
            canvas.DrawPath(path, paint);
            if (fk == Xb.DangerSolid)
            {
                paint.Color = C(Color.White, 26);
                for (float sx2 = inner.Right - D(60); sx2 < inner.Right; sx2 += D(8))
                { path.Reset(); path.MoveTo(sx2, inner.Bottom); path.LineTo(sx2 + D(4), inner.Bottom); path.LineTo(sx2 + D(4) + inner.Height() * .5f, inner.Top); path.LineTo(sx2 + inner.Height() * .5f, inner.Top); path.Close(); canvas.DrawPath(path, paint); }
            }
            // Gold inner hairline inset 2dp.
            using var gold = new RectF(inner.Left + D(2), inner.Top + D(2), inner.Right - D(2), inner.Bottom - D(2));
            PgrArt.Chamfer(path, gold, 0, cut - D(2), 0, cut - D(2));
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.75f)); paint.Color = C(Color.Argb(158, 214, 178, 104)); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Fill);
        }
        if (fk == Xb.Dark)
        { paint.Color = C(PgrTheme.Red); canvas.DrawRect(inner.Left + D(10), inner.Bottom - D(2), inner.Right - D(14), inner.Bottom, paint); }
        if (Deco != null && !solid && fk != Xb.Dark)
        {
            // 0.4.3 F-B light deco under the label (S: crimson corner + short ruler + brass node; M: brackets / serial / ring).
            int ds = canvas.Save(); canvas.ClipPath(path); Deco.SetBounds(b.Left, b.Top, b.Right, b.Bottom); Deco.Draw(canvas); canvas.RestoreToCount(ds);
        }
        if (pressed) { paint.Color = C(solid || fk == Xb.Dark ? Color.Black : PgrTheme.Ink, solid || fk == Xb.Dark ? 40 : 18); canvas.DrawRect(inner, paint); }
        canvas.RestoreToCount(save);
        // Left notch (trapezoid 3 × 18dp).
        Color notch = fk switch { Xb.Primary => Color.ParseColor("#D6B268"), Xb.Dark => PgrArt.TideRed, Xb.Brass => PgrTheme.Brass, Xb.DangerOutline or Xb.DangerSolid => fk == Xb.DangerSolid ? Color.ParseColor("#E6CD94") : PgrTheme.Red, _ => PgrTheme.Slate };
        float cy = r.CenterY(); path.Reset();
        path.MoveTo(r.Left + one, cy - D(9)); path.LineTo(r.Left + one + D(3), cy - D(7)); path.LineTo(r.Left + one + D(3), cy + D(7)); path.LineTo(r.Left + one, cy + D(9)); path.Close();
        paint.Color = C(notch); canvas.DrawPath(path, paint);
        // Corner ticks: top-left crimson, bottom-right brass (6dp arms, 1dp, inset 3dp).
        float t = D(3), arm = D(6);
        paint.Color = C(solid ? Color.Argb(220, 255, 236, 238) : PgrTheme.Red);
        canvas.DrawRect(r.Left + t, r.Top + t, r.Left + t + arm, r.Top + t + one, paint); canvas.DrawRect(r.Left + t, r.Top + t, r.Left + t + one, r.Top + t + arm, paint);
        paint.Color = C(solid ? Color.ParseColor("#E6CD94") : PgrTheme.Brass);
        canvas.DrawRect(r.Right - t - arm, r.Bottom - t - one, r.Right - t, r.Bottom - t, paint); canvas.DrawRect(r.Right - t - one, r.Bottom - t - arm, r.Right - t, r.Bottom - t, paint);
        float x = r.Right - D(14);
        if (Tail)
        {
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.5f); paint.StrokeCap = Paint.Cap.Round!;
            paint.Color = C(solid || fk == Xb.Dark ? PgrTheme.OnPlate : PgrTheme.Muted);
            path.Reset(); path.MoveTo(x - D(5), cy - D(5)); path.LineTo(x, cy); path.LineTo(x - D(5), cy + D(5)); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Fill); x -= D(18);
        }
        if (!string.IsNullOrEmpty(English))
        {
            Setup(paint); paint.TextAlign = Paint.Align.Right;
            paint.Color = C(fk switch { Xb.Primary or Xb.DangerSolid => Color.Argb(230, 255, 236, 238), Xb.Dark => PgrTheme.NavSub, Xb.Brass => PgrTheme.BrassText, Xb.DangerOutline => PgrTheme.RedText, _ => PgrTheme.Muted });
            var fm = paint.GetFontMetrics()!; canvas.DrawText(English, x, cy - (fm.Ascent + fm.Descent) / 2, paint);
            x -= paint.MeasureText(English) + D(8); paint.TextAlign = Paint.Align.Left; paint.LetterSpacing = 0;
        }
        if (Wing)
        {
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f); paint.Color = C(Color.ParseColor("#E6CD94"));
            path.Reset(); path.MoveTo(x - D(16), cy - D(3)); path.QuadTo(x - D(10), cy + D(3), x - D(8), cy + D(2)); path.QuadTo(x - D(6), cy + D(3), x, cy - D(3));
            path.MoveTo(x - D(13), cy - D(4)); path.LineTo(x - D(10), cy - D(1)); path.MoveTo(x - D(3), cy - D(4)); path.LineTo(x - D(6), cy - D(1));
            canvas.DrawPath(path, paint); paint.SetStyle(Paint.Style.Fill);
        }
        alpha = a0;
    }
}

/// <summary>
/// Numbered grid cell (category / section cells, segment cards). Light: white plate with hairline; optional index
/// column tint. Current: crimson outline + left bar + bottom-right triangle; SolidIndex fills the index column crimson.
/// </summary>
public sealed class GridCellDrawable(float density) : PgrShapeDrawable(density)
{
    public bool IsCurrent { get; set; }
    public bool SolidIndex { get; set; }
    public float IndexWidthDp { get; set; }
    public Color Fill { get; set; } = Color.Argb(240, 255, 255, 255);
    public Color Line { get; set; } = PgrTheme.Border;
    public Color? Accent { get; set; }
    bool pressed;
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[] state)
    {
        bool p = state.Contains(global::Android.Resource.Attribute.StatePressed);
        if (p == pressed) return false; pressed = p; InvalidateSelf(); return true;
    }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b.Left + D(.5f), b.Top + D(.5f), b.Right - D(.5f), b.Bottom - D(.5f));
        float cut = D(7);
        PgrArt.Chamfer(path, r, 0, 0, cut, 0);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(Fill); canvas.DrawPath(path, paint);
        if (pressed) { paint.Color = C(PgrTheme.Ink, 24); canvas.DrawPath(path, paint); }
        int save = canvas.Save(); canvas.ClipPath(path);
        if (IndexWidthDp > 0)
        {
            paint.Color = C(IsCurrent && SolidIndex ? PgrTheme.RedFill : PgrTheme.A(PgrTheme.Slate, 16));
            canvas.DrawRect(r.Left, r.Top, r.Left + D(IndexWidthDp), r.Bottom, paint);
            paint.Color = C(IsCurrent && SolidIndex ? PgrTheme.RedDeep : PgrTheme.Border);
            canvas.DrawRect(r.Left + D(IndexWidthDp), r.Top, r.Left + D(IndexWidthDp) + Math.Max(1, D(.75f)), r.Bottom, paint);
        }
        if (IsCurrent) { paint.Color = C(PgrTheme.Red); canvas.DrawRect(r.Left, r.Top, r.Left + D(3), r.Bottom, paint); }
        else if (Accent is { } accent) { paint.Color = C(accent); canvas.DrawRect(r.Left, r.Top, r.Left + D(2), r.Bottom, paint); }
        canvas.RestoreToCount(save);
        PgrArt.Chamfer(path, r, 0, 0, cut, 0);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(IsCurrent ? 1.2f : .8f)); paint.Color = C(IsCurrent ? PgrTheme.Red : Line);
        canvas.DrawPath(path, paint); paint.SetStyle(Paint.Style.Fill);
        if (IsCurrent)
        {
            path.Reset(); path.MoveTo(r.Right - cut - D(12), r.Bottom); path.LineTo(r.Right, r.Bottom - cut - D(12)); path.LineTo(r.Right, r.Bottom - cut); path.LineTo(r.Right - cut, r.Bottom); path.Close();
            paint.Color = C(PgrTheme.Red); canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Left nav-rail item face (wide landscape): selected = light chamfered plate with a crimson vertical bar.</summary>
public sealed class RailTabDrawable(float density) : PgrShapeDrawable(density)
{
    public bool Selected { get; set; }
    public Color? Dot { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        using var r = new RectF(b.Left + D(4), b.Top + D(1), b.Right, b.Bottom - D(1));
        if (Selected)
        {
            PgrArt.Chamfer(path, r, 0, D(8), 0, 0);
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
            paint.Color = C(PgrTheme.Red); canvas.DrawRect(r.Left, r.Top, r.Left + D(3), r.Bottom, paint);
        }
        if (Dot is { } dot)
        {
            float cx = r.Right - D(14), cy = r.Top + D(10), s = D(3.5f);
            path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(dot); canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Small factory helpers for the PGR batch-1 controls.</summary>
public static class PgrUi
{
    static float Den(Context c) => c.Resources!.DisplayMetrics!.Density;
    static int Dp(Context c, float v) => PgrTheme.Dp(c, v);

    /// <summary>
    /// Restyle an existing Button as a 10-A3 refined button. The Button keeps its Text (TalkBack label), click handler
    /// and Enabled semantics; the optional sub line is appended in a smaller muted span.
    /// </summary>
    public static RefinedButtonDrawable Style(Button button, Xb kind, string? icon = null, string? english = null, string? sub = null,
        bool tail = false, bool wing = false, bool center = false, float textSp = 14)
    {
        var context = button.Context!; float d = Den(context);
        var face = new RefinedButtonDrawable(d, kind) { English = english, Tail = tail, Wing = wing, EnglishPx = global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp, 9, context.Resources!.DisplayMetrics) };
        bool onDark = kind is Xb.Primary or Xb.Dark or Xb.DangerSolid;
        if (!onDark) face.Deco = new FusionDeco(d, DecoTier.S) { CutTR = 8, CutBL = 8, Paper = false };
        button.BackgroundTintList = null;
        var mask = new ColorDrawable(Color.White);
        button.Background = new RippleDrawable(ColorStateList.ValueOf(onDark ? PgrTheme.A(Color.White, 50) : PgrTheme.A(PgrTheme.Ink, 30)), face, mask);
        Color text = kind switch { Xb.Primary or Xb.DangerSolid => Color.White, Xb.Dark => PgrTheme.OnPlate, Xb.DangerOutline => PgrTheme.RedText, _ => PgrTheme.Foreground };
        bool solidKind = kind is Xb.Primary or Xb.DangerSolid;
        button.SetTextColor(new ColorStateList(new[] { new[] { -global::Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
            new[] { (solidKind ? PgrTheme.DisabledText : PgrTheme.A(text, 150)).ToArgb(), text.ToArgb() })); // 0.4.7 P2
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0; button.LetterSpacing = .02f;
        button.TextSize = textSp;
        bool bold = kind is Xb.Primary or Xb.DangerSolid;
        button.SetTypeface(bold ? PgrTheme.SansBold : PgrTheme.SansMedium, bold ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        string label = button.Text ?? "";
        if (!string.IsNullOrEmpty(sub))
        {
            var span = new SpannableString(label + "\n" + sub);
            int start = label.Length + 1, end = span.Length();
            span.SetSpan(new RelativeSizeSpan(11f / textSp), start, end, SpanTypes.ExclusiveExclusive);
            span.SetSpan(new ForegroundColorSpan(onDark ? PgrTheme.NavSub : PgrTheme.Muted), start, end, SpanTypes.ExclusiveExclusive);
            span.SetSpan(new TypefaceSpan(PgrTheme.Sans), start, end, SpanTypes.ExclusiveExclusive);
            button.TextFormatted = span; button.ContentDescription ??= label;
            button.SetLineSpacing(Dp(context, 1), 1);
        }
        button.SetMinHeight(Dp(context, PgrTheme.Touch)); button.SetMinimumHeight(Dp(context, PgrTheme.Touch));
        button.SetMinWidth(0); button.SetMinimumWidth(0);
        using var measure = new Paint();
        int end2 = (int)face.TrailingWidth(measure);
        button.SetPadding(Dp(context, 15), Dp(context, 6), Math.Max(Dp(context, 12), end2), Dp(context, 6));
        button.Gravity = (center ? GravityFlags.Center : GravityFlags.Start | GravityFlags.CenterVertical);
        if (icon != null)
        {
            Color iconColor = kind switch { Xb.Primary or Xb.DangerSolid => Color.White, Xb.Dark => PgrTheme.OnPlate, Xb.Brass => PgrTheme.Brass, Xb.DangerOutline => PgrTheme.Red, _ => PgrTheme.Slate };
            button.SetCompoundDrawablesRelativeWithIntrinsicBounds(solidKind ? PgrTheme.StateIcon(icon, iconColor, d, 18) : new PgrIconDrawable(icon, iconColor, d, 18), null, null, null);
            button.CompoundDrawablePadding = Dp(context, 9);
        }
        else button.SetCompoundDrawablesRelativeWithIntrinsicBounds(null, null, null, null);
        return face;
    }

    /// <summary>Large outlined numeral (stroke only); decorative, hidden from TalkBack.</summary>
    public static TextView Outline(Context context, string text, float sp, Color color, float strokeDp = 1.1f)
    {
        var v = new TextView(context) { Text = text, TextSize = sp, ImportantForAccessibility = ImportantForAccessibility.No };
        v.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); v.SetTextColor(color); v.SetIncludeFontPadding(false);
        v.Paint!.SetStyle(Paint.Style.Stroke); v.Paint.StrokeWidth = Math.Max(1f, strokeDp * Den(context)); v.LetterSpacing = .02f;
        PgrTheme.Tabular(v);
        return v;
    }

    /// <summary>Condensed uppercase tag (English labels next to Chinese ones).</summary>
    public static TextView En(Context context, string text, float sp = 9, Color? color = null, bool dropIfClipped = false)
    {
        var v = dropIfClipped ? new WordFitText(context) { WholeLine = true } : new TextView(context);
        v.Text = text; v.TextSize = sp; v.ImportantForAccessibility = ImportantForAccessibility.No;
        v.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); v.SetTextColor(color ?? PgrTheme.Muted); v.LetterSpacing = .14f;
        v.SetAllCaps(true); v.SetIncludeFontPadding(false); v.SetSingleLine(true);
        return v;
    }

    public enum Status { Current, Resume, NotStarted, Teal, Brass, Source, Alert, Steel }

    /// <summary>Status chip (当前 / 续听 / 未开始, segment type, source tag): 10sp condensed bold on a tinted plate.</summary>
    public static TextView Chip(Context context, string text, Status kind)
    {
        var v = new TextView(context) { Text = text, TextSize = 10 };
        v.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); v.LetterSpacing = .06f; v.SetSingleLine(true); v.SetIncludeFontPadding(false);
        Color fill, line, ink;
        switch (kind)
        {
            case Status.Current: fill = PgrTheme.RedFill; line = PgrTheme.RedFill; ink = Color.White; break;
            case Status.Resume: case Status.Teal: fill = PgrTheme.A(PgrTheme.Cyan, 31); line = PgrTheme.A(PgrTheme.Cyan, 140); ink = PgrTheme.TealText; break;
            case Status.Brass: fill = PgrTheme.A(PgrTheme.Brass, 31); line = PgrTheme.A(PgrTheme.Brass, 170); ink = PgrTheme.BrassText; break;
            case Status.Source: fill = PgrTheme.NamePlate; line = PgrTheme.NamePlate; ink = PgrTheme.OnPlate; break;
            case Status.Alert: fill = PgrTheme.A(PgrTheme.Red, 18); line = PgrTheme.A(PgrTheme.Red, 170); ink = PgrTheme.RedText; break;
            case Status.Steel: fill = PgrTheme.A(PgrTheme.Slate, 22); line = PgrTheme.A(PgrTheme.Slate, 150); ink = PgrTheme.Steel; break;
            default: fill = PgrTheme.A(PgrTheme.Muted, 20); line = PgrTheme.A(PgrTheme.Muted, 90); ink = PgrTheme.Muted; break;
        }
        v.SetTextColor(ink);
        v.Background = PgrTheme.Plate(context, fill, line, 4, Corners.BottomRight);
        v.SetPadding(Dp(context, 6), Dp(context, 3), Dp(context, 7), Dp(context, 3));
        if (kind is Status.Resume or Status.Teal or Status.Brass)
        {
            v.SetCompoundDrawablesRelativeWithIntrinsicBounds(new DiamondDot(Den(context), ink), null, null, null);
            v.CompoundDrawablePadding = Dp(context, 4);
        }
        return v;
    }

    sealed class DiamondDot(float density, Color color) : PgrShapeDrawable(density)
    {
        public override int IntrinsicWidth => (int)D(6);
        public override int IntrinsicHeight => (int)D(6);
        public override void Draw(Canvas canvas)
        {
            var b = Bounds; float cx = b.ExactCenterX(), cy = b.ExactCenterY(), s = D(3);
            path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(color); canvas.DrawPath(path, paint);
        }
    }

    /// <summary>Zone header like 01-B: crimson triangle, Chinese title, condensed English, fading rule, optional readout.</summary>
    public static LinearLayout Zone(Context context, string title, string english, string? readout = null, float titleSp = 15, string? shortTitle = null)
    {
        // FitRow: on narrow widths drop the English label first, then shrink nothing else — the readout chip never gets clipped.
        var row = new FitRow(context);
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(new View(context) { Background = PgrTheme.Triangle(context), ImportantForAccessibility = ImportantForAccessibility.No },
            new LinearLayout.LayoutParams(Dp(context, 6), Dp(context, 8)) { RightMargin = Dp(context, 8) });
        var zh = new TextView(context) { Text = title, TextSize = titleSp }; zh.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zh.SetTextColor(PgrTheme.Foreground);
        row.AddView(zh);
        var en = En(context, english, 9.5f);
        row.AddView(en, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(context, 8) });
        var rule = new View(context) { ImportantForAccessibility = ImportantForAccessibility.No };
        rule.Background = new GradientDrawable(GradientDrawable.Orientation.LeftRight!, new int[] { PgrTheme.BorderStrong.ToArgb(), PgrTheme.A(PgrTheme.BorderStrong, 0).ToArgb() });
        row.AddView(rule, new LinearLayout.LayoutParams(0, Math.Max(1, Dp(context, .75f)), 1) { LeftMargin = Dp(context, 8), RightMargin = Dp(context, 8) });
        TextView? chip = readout != null ? Readout(context, readout) : null;
        if (chip != null) row.AddView(chip);
        // 0.4.6: level 2 drops the readout chip too (803×344 fold outer: it was squeezed to an empty sliver beside 导入 ZIP).
        int baseLevels = chip != null ? 2 : 1;
        // 0.4.6: optional last level swaps a long title for its short form (e.g. 「内置剧情文本 · 当前小节」 → 「内置剧情文本」 in the
        // 640dp landscape sheet column) instead of wrapping one orphan character onto a second line.
        row.Levels = baseLevels + (shortTitle != null ? 1 : 0);
        row.ApplyLevel = level =>
        {
            en.Visibility = level == 0 ? ViewStates.Visible : ViewStates.Gone; if (chip != null) chip.Visibility = level >= 2 ? ViewStates.Gone : ViewStates.Visible;
            if (shortTitle != null) { string want = level > baseLevels ? shortTitle : title; if (zh.Text != want) zh.Text = want; }
        };
        // Exact: the rule may shrink to 0 (only its margins count), so the English label drops only where 0.4.3 clipped the readout.
        row.NeedWidth = () => { int s = 0; for (int i = 0; i < row.ChildCount; i++) s += row.Natural(row.GetChildAt(i)!); return s; };
        return row;
    }

    /// <summary>Boxed condensed readout (「05 PACKS」「CH.05」「12」).</summary>
    public static TextView Readout(Context context, string text, bool onDark = false)
    {
        var v = new TextView(context) { Text = text, TextSize = 10, ImportantForAccessibility = ImportantForAccessibility.No };
        v.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); v.LetterSpacing = .1f; v.SetTextColor(onDark ? PgrTheme.OnPlate : PgrTheme.Steel); PgrTheme.Tabular(v);
        v.Background = PgrTheme.Plate(context, onDark ? PgrTheme.A(Color.White, 20) : PgrTheme.Raised, onDark ? PgrTheme.A(PgrTheme.OnPlate, 90) : PgrTheme.A(PgrTheme.Slate, 120), 4, Corners.BottomRight);
        v.SetPadding(Dp(context, 6), Dp(context, 2), Dp(context, 6), Dp(context, 2)); v.SetSingleLine(true);
        return v;
    }
}

/// <summary>
/// PGR sheet: bottom panel in portrait (and narrow landscape), right-side panel in wide landscape (≥600dp). Archive-paper
/// surface with a 45% scrim; content is rebuilt on rotation so an open sheet swaps bottom ⇄ right without closing.
/// </summary>
public sealed class PgrSheet
{
    readonly Activity activity;
    readonly Action<PgrSheet> build;
    Dialog? dialog;
    FrameLayout? frame;
    LinearLayout? panel;
    public static PgrSheet? Open { get; private set; }
    public LinearLayout Header { get; private set; } = null!;
    public LinearLayout Body { get; private set; } = null!;
    public LinearLayout Footer { get; private set; } = null!;
    public ScrollView Scroll { get; private set; } = null!;
    /// <summary>Wide-landscape panel width; ≤0 = full width minus the 80dp nav rail.</summary>
    public float WideWidthDp { get; set; } = 450;
    /// <summary>When true the sheet body handles its own scrolling (two-column layouts); Body is then not wrapped in Scroll.</summary>
    public bool CustomBody { get; set; }
    /// <summary>11-B2 style: dark L-tier panel (grid, orbit arcs, F1b, tide band, 「GRAY RAVEN ARCHIVE · mark」) instead of archive paper.</summary>
    public string? DarkMark { get; set; }
    public event Action? Dismissed;

    public PgrSheet(Activity activity, Action<PgrSheet> build) { this.activity = activity; this.build = build; }

    int Dp(float v) => PgrTheme.Dp(activity, v);
    public bool Wide
    {
        get { var c = activity.Resources!.Configuration!; return c.Orientation == global::Android.Content.Res.Orientation.Landscape && c.ScreenWidthDp >= 600; }
    }

    public void Show()
    {
        Open?.Dismiss();
        var d = new Dialog(activity, global::Android.Resource.Style.ThemeDeviceDefaultLightNoActionBar);
        dialog = d; Open = this;
        frame = new FrameLayout(activity) { Clickable = true };
        frame.SetBackgroundColor(Color.Argb(115, 0x1D, 0x23, 0x2A));
        frame.Click += (_, _) => Dismiss();
        d.SetContentView(frame);
        d.Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        d.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        d.Window.ClearFlags(WindowManagerFlags.DimBehind);
        if (!OperatingSystem.IsAndroidVersionAtLeast(30)) d.Window.SetSoftInputMode(SoftInput.AdjustResize);
        d.SetCanceledOnTouchOutside(true);
        d.DismissEvent += (_, _) => { if (ReferenceEquals(Open, this)) Open = null; Dismissed?.Invoke(); };
        Relayout();
        d.Show();
    }

    public void Dismiss() { try { dialog?.Dismiss(); } catch (Exception) { } }
    public bool IsShowing => dialog?.IsShowing == true;

    /// <summary>Rebuild the panel for the current orientation (called on show and from OnConfigurationChanged).</summary>
    public void Relayout()
    {
        if (frame == null) return;
        frame.RemoveAllViews();
        bool wide = Wide; float density = activity.Resources!.DisplayMetrics!.Density;
        var p = panel = new LinearLayout(activity) { Orientation = Orientation.Vertical, Clickable = true };
        var paper = new ArchivePaperDrawable(density)
        {
            Fill = PgrTheme.Background, CutTopRightDp = wide ? 0 : 22, TopRedBar = true, ShowHandle = !wide,
            EmblemWidthDp = 210, EmblemRightDp = -28, EmblemBottomDp = wide ? 60 : 300, EmblemOpacity = .5f, TideDp = 48
        };
        if (DarkMark != null) p.Background = new DarkPanelDrawable(density, DecoTier.L) { Mark = DarkMark, CutTopRightDp = wide ? 0 : 22 };
        else p.Background = paper;
        p.SetPadding(Dp(16), Dp(wide ? 14 : 18), Dp(16), Dp(12));
        Header = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        // 0.4.3 F-B: light sheets get the header underline (hairline + crimson lead + tick ruler + brass node + tide line).
        if (DarkMark == null) { Header.Background = new FusionHeaderDeco(density) { Serial = false, BaselineDp = 3 }; Header.SetPadding(0, 0, 0, Dp(10)); }
        Body = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        Footer = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        p.AddView(Header, new LinearLayout.LayoutParams(-1, -2));
        build(this);
        if (CustomBody) p.AddView(Body, new LinearLayout.LayoutParams(-1, 0, 1));
        else
        {
            Scroll = new ScrollView(activity) { VerticalScrollBarEnabled = false };
            Scroll.AddView(Body, new FrameLayout.LayoutParams(-1, -2));
            p.AddView(Scroll, new LinearLayout.LayoutParams(-1, wide ? 0 : -2, wide ? 1 : 0));
        }
        if (Footer.ChildCount > 0) p.AddView(Footer, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8) });
        var c = activity.Resources!.Configuration!;
        FrameLayout.LayoutParams lp;
        if (wide)
        {
            int width = WideWidthDp > 0 ? Dp(WideWidthDp) : Math.Max(Dp(450), Dp(c.ScreenWidthDp - 80));
            lp = new FrameLayout.LayoutParams(Math.Min(width, Dp(c.ScreenWidthDp)), -1, GravityFlags.End);
        }
        else
        {
            // Tablet portrait: keep the bottom sheet at the same 680dp reading width as the page column.
            lp = c.ScreenWidthDp >= 720 ? new FrameLayout.LayoutParams(Dp(680), -2, GravityFlags.Bottom | GravityFlags.CenterHorizontal) : new FrameLayout.LayoutParams(-1, -2, GravityFlags.Bottom);
            if (!CustomBody) p.LayoutParameters = lp;
        }
        frame.AddView(p, lp);
        if (wide)
        {
            // The dialog frame can be narrower than Configuration.ScreenWidthDp (nav bar / cutout side): never let the panel
            // overflow off the left edge.
            frame.ViewTreeObserver!.GlobalLayout += Fit;
            void Fit(object? s, EventArgs e)
            {
                int avail = frame.Width - frame.PaddingLeft - frame.PaddingRight;
                if (avail > 0 && p.Width > avail && p.LayoutParameters is FrameLayout.LayoutParams l && l.Width != avail) { l.Width = avail; p.LayoutParameters = l; }
            }
        }
        if (!wide)
        {
            p.SetOnTouchListener(new DragToDismiss(this, p, ViewConfiguration.Get(activity)!.ScaledTouchSlop, Dp(96)));
            // Cap the bottom sheet at ~90% of the screen height; the body scrolls inside.
            int max = (int)(Dp(c.ScreenHeightDp) * .9f);
            p.ViewTreeObserver!.GlobalLayout += Cap;
            void Cap(object? s, EventArgs e)
            {
                // 系统栏会使真实对话框小于 Configuration 高度；先按实际视口预留页脚。
                int available = frame.Height - frame.PaddingTop - frame.PaddingBottom;
                int limit = Math.Min(max, available);
                if (limit <= 0 || p.LayoutParameters is not FrameLayout.LayoutParams l) return;
                if (p.Height >= limit && l.Height != limit)
                {
                    l.Height = limit; p.LayoutParameters = l;
                    if (!CustomBody && Scroll.LayoutParameters is LinearLayout.LayoutParams sl) { sl.Height = 0; sl.Weight = 1; Scroll.LayoutParameters = sl; }
                }
                else if (l.Height > 0 && l.Height < limit)
                {
                    // 键盘收起后视口变高：放开按键盘高度压缩过的面板，内容不足时回到自适应高度。
                    if (CustomBody) l.Height = limit;
                    else
                    {
                        l.Height = ViewGroup.LayoutParams.WrapContent;
                        if (Scroll.LayoutParameters is LinearLayout.LayoutParams sl) { sl.Height = ViewGroup.LayoutParams.WrapContent; sl.Weight = 0; Scroll.LayoutParameters = sl; }
                    }
                    p.LayoutParameters = l;
                }
            }
            if (CustomBody && p.LayoutParameters is FrameLayout.LayoutParams fixedLp) { fixedLp.Height = max; p.LayoutParameters = fixedLp; }
        }
        frame.SetOnApplyWindowInsetsListener(new Insets());
        frame.RequestApplyInsets();
    }

    /// <summary>顶部把手区域与面板空白处可下拉：跟手移动，超过阈值滑出并关闭，否则回弹。按钮、输入框和滚动列表自行处理触摸。</summary>
    sealed class DragToDismiss(PgrSheet owner, View panel, int slop, int minDistance) : Java.Lang.Object, View.IOnTouchListener
    {
        float startY; bool dragging;
        public bool OnTouch(View? v, MotionEvent? e)
        {
            if (e == null) return false;
            float dy = e.RawY - startY;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    startY = e.RawY; dragging = false; panel.Animate()?.Cancel(); return true;
                case MotionEventActions.Move:
                    if (!dragging && dy > slop) dragging = true;
                    if (dragging) panel.TranslationY = Math.Max(0, dy);
                    return true;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (!dragging) return true;
                    dragging = false;
                    if (e.ActionMasked == MotionEventActions.Up && dy > Math.Max(minDistance, panel.Height * .25f))
                        panel.Animate()?.TranslationY(panel.Height)?.SetDuration(160)?.WithEndAction(new Java.Lang.Runnable(owner.Dismiss));
                    else panel.Animate()?.TranslationY(0)?.SetDuration(160);
                    return true;
            }
            return false;
        }
    }

    sealed class Insets : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
        {
            if (view == null || insets == null) return insets!;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                int ime = insets.GetInsets(WindowInsets.Type.Ime()).Bottom;
                view.SetPadding(bars.Left, bars.Top, bars.Right, Math.Max(bars.Bottom, ime));
                if (ime > 0 && view.FindFocus() is EditText focused)
                    focused.PostDelayed(() => { var r = new Rect(); focused.GetDrawingRect(r); focused.RequestRectangleOnScreen(r, false); }, 120);
            }
            else
            {
#pragma warning disable CA1422
                view.SetPadding(insets.SystemWindowInsetLeft, insets.SystemWindowInsetTop, insets.SystemWindowInsetRight, insets.SystemWindowInsetBottom);
#pragma warning restore CA1422
            }
            return insets;
        }
    }

    /// <summary>Sheet title row: crimson triangle, title, condensed English, rule, optional readout; optional back arrow.</summary>
    public LinearLayout Title(string title, string english, string? readout = null, Action? back = null)
    {
        var row = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        if (back != null)
        {
            var arrow = new ImageButton(activity) { ContentDescription = "返回上一步" };
            arrow.SetImageDrawable(new PgrIconDrawable("back", PgrTheme.Foreground, activity.Resources!.DisplayMetrics!.Density, 20));
            arrow.Background = new RippleDrawable(ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)), null, new ColorDrawable(Color.White));
            arrow.Click += (_, _) => back();
            row.AddView(arrow, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { LeftMargin = -Dp(12) });
        }
        var zone = PgrUi.Zone(activity, title, english, readout, 17);
        row.AddView(zone, new LinearLayout.LayoutParams(0, -2, 1));
        Header.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        return row;
    }

    public TextView Subtitle(string text)
    {
        var v = new TextView(activity) { Text = text, TextSize = 12 }; v.SetTextColor(PgrTheme.Secondary);
        v.SetPadding(Dp(14), Dp(2), 0, Dp(8)); Header.AddView(v); return v;
    }

    /// <summary>「取消 CANCEL」footer button (closes the sheet).</summary>
    public Button Cancel(LinearLayout? parent = null)
    {
        var cancel = new Button(activity) { Text = "取消" };
        PgrUi.Style(cancel, Xb.Normal, "close", "CANCEL", center: true);
        cancel.Click += (_, _) => Dismiss();
        (parent ?? Footer).AddView(cancel, new LinearLayout.LayoutParams(-1, Dp(PgrTheme.Touch)));
        return cancel;
    }
}
