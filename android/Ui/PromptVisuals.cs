using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using ICharSequence = Java.Lang.ICharSequence;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 0.4.8（batch-047 提示界面设计稿 01–16）共用控件：倒计时环形铭文、15 秒细轨（含暂停态）、「暂停 / 继续」次级键与说明行、
/// 编号选项片（默认分支 / 结局尾标）、F-B 黄铜纸面卡底、结局分歧深色头、横屏融合条（深石板左块 + 冷灰白纸面）。
/// 颜色全部取自设计稿源码 p047.js / p047b.js：黄铜 #B08A3E / 字 #7E6128 / 深底金 #E2C27E，暂停灰 #9AA3AD，石板蓝 #4F6B8A。
/// </summary>
public static class PromptArt
{
    public static readonly Color Brass = PgrTheme.Brass, BrassText = PgrTheme.BrassText, Gold = PgrArt.GoldLine;
    public static readonly Color PausedGrey = Color.ParseColor("#9AA3AD");
    public static readonly Color PausedInk = Color.ParseColor("#7A8591");
    public static readonly Color SlateBlue = Color.ParseColor("#4F6B8A");
    public static readonly Color PaperWarm = Color.ParseColor("#FBF7EC");
    public static readonly Color Ink = Color.ParseColor("#1D232A");
    public static readonly Color Note = Color.ParseColor("#3A4450");
    public static readonly Color EndingRed = Color.ParseColor("#9C2536");
    public static readonly Color EndingPink = Color.ParseColor("#F0B8C0");
    public static readonly Color DoneTeal = Color.ParseColor("#2F7F7A");
    public static readonly Color OverlayPaper = Color.Argb(247, 0xEE, 0xF1, 0xF4);
    public const string PauseHint = "点「暂停」停住倒计时，不自动续接；再点「继续」恢复。";
    public const string PausedHint = "：倒计时停住，不自动续接；点「继续」恢复。";

    static float Den(Context c) => c.Resources!.DisplayMetrics!.Density;
    static int Dp(Context c, float v) => (int)(v * Den(c) + .5f);

    /// <summary>窄体大写英文铭文（设计稿 en47）。</summary>
    public static TextView En(Context c, string text, float sp, Color color) => PgrUi.En(c, text, sp, color);

    public static TextView Label(Context c, string text, float sp, Color color, bool bold = false, int maxLines = 0)
    {
        var v = new TextView(c) { Text = text, TextSize = sp }; v.SetTextColor(color); v.SetIncludeFontPadding(false);
        v.SetTypeface(bold ? PgrTheme.SansBold : PgrTheme.Sans, bold ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        v.SetLineSpacing(Dp(c, 2), 1);
        if (maxLines == 1) { v.SetSingleLine(true); v.Ellipsize = TextUtils.TruncateAt.End; }
        else if (maxLines > 1) { v.SetMaxLines(maxLines); v.Ellipsize = TextUtils.TruncateAt.End; }
        return v;
    }

    /// <summary>「洛莎：指挥官，……」：说话人粗体深墨，正文次级色。</summary>
    public static ICharSequence SpeakerLine(string? speaker, string text, Color ink, Color body)
    {
        var sb = new SpannableStringBuilder();
        if (!string.IsNullOrWhiteSpace(speaker))
        {
            string head = speaker.Trim() + "：";
            sb.Append(head);
            sb.SetSpan(new ForegroundColorSpan(ink), 0, head.Length, SpanTypes.ExclusiveExclusive);
            sb.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, head.Length, SpanTypes.ExclusiveExclusive);
        }
        int start = sb.Length(); sb.Append(text);
        sb.SetSpan(new ForegroundColorSpan(body), start, sb.Length(), SpanTypes.ExclusiveExclusive);
        return sb;
    }

    /// <summary>结局选项：「（开启结局·明天）」部分暗红粗体（设计稿 03 / 10）。</summary>
    public static ICharSequence OptionLabel(string label)
    {
        var m = System.Text.RegularExpressions.Regex.Match(label, @"[（(][^（()）]*开启结局[^（()）]*[)）]");
        if (!m.Success) return new Java.Lang.String(label);
        var sb = new SpannableStringBuilder(label);
        sb.SetSpan(new ForegroundColorSpan(EndingRed), m.Index, m.Index + m.Length, SpanTypes.ExclusiveExclusive);
        sb.SetSpan(new StyleSpan(TypefaceStyle.Bold), m.Index, m.Index + m.Length, SpanTypes.ExclusiveExclusive);
        return sb;
    }

    /// <summary>从选项文字取结局名：「…（开启结局·明天）」→「明天」；没有名字时为空串。</summary>
    public static string EndingName(string? label)
    {
        if (string.IsNullOrEmpty(label)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(label, @"开启结局\s*[·・:：\-—\s]*([^）)]*)");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    /// <summary>F-B 黄铜纸面卡底：暖白纸面 + 黄铜发丝 + 左侧金线 + 22dp 网格刻线 + 右上路线弧。</summary>
    public static Drawable BrassCard(Context c, float cut = 14)
    {
        var plate = PgrTheme.Plate(c, PgrTheme.A(PaperWarm, 248), PgrTheme.A(Brass, 190), cut, Corners.Diagonal, Gold, AccentEdge.Left, shadow: true);
        var deco = new FusionDeco(Den(c), DecoTier.M) { CutTL = cut, CutBR = cut };
        return new LayerDrawable(new Drawable[] { plate, deco, new RouteArcsDrawable(Den(c)) });
    }

    /// <summary>石板蓝 / 黄铜 左线说明行底（暂停行、提示行）。</summary>
    public static Drawable NoteBand(Context c, Color tint, Color edge) =>
        PgrTheme.Plate(c, tint, null, 0, Corners.None, edge, AccentEdge.Left);
}

/// <summary>卡片右上角的路线弧：黄铜实线 + 石板蓝虚线 + 黄铜节点（设计稿 brassCard 的 svg）。纯装饰。</summary>
public sealed class RouteArcsDrawable(float density) : PgrShapeDrawable(density)
{
    public bool OnDark { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float w = Math.Min(D(190), b.Width() * .6f), h = Math.Min(D(70), b.Height());
        float x0 = b.Right - w, y0 = b.Top; float sx = w / 190f, sy = h / 70f;
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .9f); paint.SetPathEffect(null);
        path.Reset(); path.MoveTo(x0 + 40 * sx, y0 + 72 * sy); path.CubicTo(x0 + 80 * sx, y0 + 30 * sy, x0 + 130 * sx, y0 + 10 * sy, x0 + 192 * sx, y0 + 8 * sy);
        paint.Color = C(OnDark ? PgrArt.GoldLine : PgrTheme.Brass, 115); canvas.DrawPath(path, paint);
        path.Reset(); path.MoveTo(x0 + 60 * sx, y0 + 72 * sy); path.CubicTo(x0 + 100 * sx, y0 + 40 * sy, x0 + 140 * sx, y0 + 26 * sy, x0 + 192 * sx, y0 + 24 * sy);
        using (var dash = new DashPathEffect(new[] { D(4), D(4) }, 0)) { paint.SetPathEffect(dash); paint.Color = C(OnDark ? PgrArt.MistLine : PgrTheme.Slate, 76); canvas.DrawPath(path, paint); paint.SetPathEffect(null); }
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Brass, 204); canvas.DrawCircle(x0 + 150 * sx, y0 + 12.6f * sy, D(2.4f), paint);
    }
}

/// <summary>倒计时环：发丝底环 + 黄铜进度弧（剩余比例）+ 三处刻度；暂停转冷灰。Done = 完成态（满环）。</summary>
public sealed class CountdownRingDrawable(float density, bool dark) : PgrShapeDrawable(density)
{
    float fraction = 1; bool paused;
    public float Fraction { get => fraction; set { fraction = Math.Clamp(value, 0, 1); InvalidateSelf(); } }
    public bool Paused { get => paused; set { paused = value; InvalidateSelf(); } }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float cx = b.ExactCenterX(), cy = b.ExactCenterY(), r = Math.Min(b.Width(), b.Height()) / 2f - D(3);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeCap = Paint.Cap.Butt;
        paint.StrokeWidth = D(2); paint.Color = dark ? C(Color.Argb(89, 170, 180, 191)) : C(Color.ParseColor("#D8DDE2"));
        canvas.DrawCircle(cx, cy, r, paint);
        paint.StrokeWidth = D(2.4f);
        paint.Color = C(paused ? (dark ? Color.ParseColor("#8C96A1") : PromptArt.PausedGrey) : dark ? PgrArt.GoldLine : PgrTheme.Brass);
        using (var oval = new RectF(cx - r, cy - r, cx + r, cy + r)) canvas.DrawArc(oval, -90, 360 * fraction, false, paint);
        paint.StrokeWidth = Math.Max(1, d); paint.Color = dark ? C(Color.Argb(178, 170, 180, 191)) : C(PgrTheme.BorderStrong);
        for (int i = 0; i < 3; i++)
        {
            double a = -Math.PI / 2 + i * 2 * Math.PI / 3;
            canvas.DrawLine(cx + (float)Math.Cos(a) * (r - D(3)), cy + (float)Math.Sin(a) * (r - D(3)), cx + (float)Math.Cos(a) * (r + D(3)), cy + (float)Math.Sin(a) * (r + D(3)), paint);
        }
    }
}

/// <summary>
/// 倒计时铭文（设计稿 ring47）：环 + 等宽秒数，右侧「SEC · AUTO」+ 中文说明（如「秒后自动选择」）。
/// 暂停：环与数字转冷灰、标「PAUSED · 已暂停 / 倒计时已停住」、底改冷灰。完成态（Done）显示 ✓ 与「DONE · 已选择」。
/// </summary>
public sealed class CountdownBadge : LinearLayout
{
    readonly bool dark; readonly string caption;
    readonly CountdownRingDrawable ring; readonly TextView number, en, cap;
    bool? shownPaused;
    public CountdownBadge(Context c, string caption, bool dark = false, float radiusDp = 15, float numberSp = 15) : base(c)
    {
        this.dark = dark; this.caption = caption;
        Orientation = Orientation.Horizontal; SetGravity(GravityFlags.CenterVertical);
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        int s = Dp(radiusDp * 2 + 6);
        var face = new FrameLayout(c);
        ring = new CountdownRingDrawable(den, dark);
        face.AddView(new View(c) { Background = ring, ImportantForAccessibility = ImportantForAccessibility.No }, new FrameLayout.LayoutParams(s, s));
        number = new TextView(c) { TextSize = numberSp, Gravity = GravityFlags.Center }; number.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold); number.SetIncludeFontPadding(false);
        face.AddView(number, new FrameLayout.LayoutParams(s, s));
        AddView(face, new LayoutParams(s, s));
        var col = new LinearLayout(c) { Orientation = Orientation.Vertical };
        en = PgrUi.En(c, "SEC · AUTO", 8.5f, PgrTheme.BrassText);
        col.AddView(en);
        cap = new TextView(c) { Text = caption, TextSize = 10.5f }; cap.SetIncludeFontPadding(false); cap.SetSingleLine(true);
        col.AddView(cap, new LayoutParams(-2, -2) { TopMargin = Dp(2) });
        AddView(col, new LayoutParams(-2, -2) { LeftMargin = Dp(7) });
        SetPadding(Dp(4), Dp(2), Dp(10), Dp(2));
        SetMinimumHeight(s + Dp(4));
        Update(15, 1, false);
    }
    public void Update(int seconds, float fraction, bool paused)
    {
        number.Text = Math.Max(0, seconds).ToString("00");
        ring.Fraction = fraction; ring.Paused = paused;
        if (shownPaused == paused) { ContentDescription = paused ? "倒计时已暂停，剩余 " + seconds + " 秒" : seconds + " " + caption; return; }
        shownPaused = paused;
        var ctx = Context!;
        number.SetTextColor(paused ? (dark ? PgrTheme.NavSub : PromptArt.PausedInk) : dark ? PgrArt.GoldLine : PgrTheme.BrassText);
        en.Text = paused ? "PAUSED · 已暂停" : "SEC · AUTO";
        en.SetTextColor(paused ? (dark ? PgrTheme.NavText : PromptArt.SlateBlue) : dark ? PgrArt.GoldLine : PgrTheme.BrassText);
        cap.Text = paused ? "倒计时已停住" : caption; cap.SetTextColor(dark ? PgrTheme.NavText : PgrTheme.Secondary);
        Background = PgrTheme.Plate(ctx,
            paused ? (dark ? Color.Argb(89, 20, 24, 29) : Color.Argb(242, 236, 239, 242)) : dark ? PgrTheme.A(PgrTheme.Brass, 36) : PgrTheme.A(Color.White, 235),
            paused ? (dark ? Color.Argb(153, 170, 180, 191) : PgrTheme.BorderStrong) : PgrTheme.Brass, 7, Corners.TopRight | Corners.BottomLeft);
        ContentDescription = paused ? "倒计时已暂停，剩余 " + seconds + " 秒" : seconds + " " + caption;
    }
    /// <summary>完成态（设计稿 02 / 09）：满环 + ✓ +「DONE · 已选择 / 已按默认继续」。</summary>
    public void ShowDone(string doneCaption = "已按默认继续")
    {
        number.Text = "✓"; ring.Fraction = 1; ring.Paused = false; shownPaused = null;
        number.SetTextColor(dark ? PgrArt.GoldLine : PgrTheme.BrassText);
        en.Text = "DONE · 已选择"; en.SetTextColor(dark ? PgrArt.GoldLine : PgrTheme.BrassText);
        cap.Text = doneCaption; cap.SetTextColor(dark ? PgrTheme.NavText : PgrTheme.Secondary);
        Background = PgrTheme.Plate(Context!, dark ? PgrTheme.A(PgrTheme.Brass, 36) : PgrTheme.A(Color.White, 235), PgrTheme.Brass, 7, Corners.TopRight | Corners.BottomLeft);
        ContentDescription = "已自动选择，" + doneCaption;
    }
}

/// <summary>15 秒细轨 + 右侧铭文（设计稿 rail47）；暂停时轨转冷灰、铭文改「PAUSED · 已暂停」。</summary>
public sealed class CountdownRail : LinearLayout
{
    readonly CountdownRailDrawable rail; readonly TextView? caption; readonly string text; readonly bool dark;
    bool? shownPaused;
    public CountdownRail(Context c, string caption, bool dark = false) : base(c)
    {
        this.text = caption; this.dark = dark;
        Orientation = Orientation.Horizontal; SetGravity(GravityFlags.CenterVertical);
        ImportantForAccessibility = ImportantForAccessibility.No;
        float den = c.Resources!.DisplayMetrics!.Density;
        rail = new CountdownRailDrawable(den, dark);
        AddView(new View(c) { Background = rail }, new LayoutParams(0, (int)(12 * den + .5f), 1));
        if (caption.Length > 0)
        {
            this.caption = PgrUi.En(c, caption, 8.5f, dark ? PgrArt.GoldLine : PgrTheme.BrassText);
            AddView(this.caption, new LayoutParams(-2, -2) { LeftMargin = (int)(8 * den + .5f) });
        }
    }
    public void Update(float fraction, bool paused)
    {
        rail.Fraction = fraction; rail.Paused = paused;
        if (caption == null || shownPaused == paused) return;
        shownPaused = paused;
        caption.Text = paused ? "PAUSED · 已暂停" : text;
        caption.SetTextColor(paused ? (dark ? PgrTheme.NavText : PromptArt.SlateBlue) : dark ? PgrArt.GoldLine : PgrTheme.BrassText);
    }
}

/// <summary>「暂停 / 继续」键（设计稿 pzBtn）：白底石板蓝描边 → 暂停后暖白底黄铜描边「继续」；右下角刻线。横排或竖排（横屏条）。</summary>
public sealed class PauseToggle : LinearLayout
{
    readonly ImageView icon; readonly TextView label; readonly bool vertical; readonly float den;
    bool? shown;
    public PauseToggle(Context c, bool vertical = false, float textSp = 14) : base(c)
    {
        this.vertical = vertical; den = c.Resources!.DisplayMetrics!.Density;
        Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal; SetGravity(GravityFlags.Center);
        Clickable = true; Focusable = true;
        icon = new ImageView(c) { ImportantForAccessibility = ImportantForAccessibility.No };
        int isz = (int)((vertical ? 18 : 16) * den + .5f);
        AddView(icon, new LayoutParams(isz, isz));
        label = new TextView(c) { TextSize = textSp }; label.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); label.SetIncludeFontPadding(false); label.SetSingleLine(true);
        var lp = new LayoutParams(-2, -2); if (vertical) lp.TopMargin = (int)(3 * den + .5f); else lp.LeftMargin = (int)(6 * den + .5f);
        AddView(label, lp);
        SetMinimumHeight((int)(PgrTheme.Touch * den + .5f) - (vertical ? 0 : (int)(10 * den)));
        Update(false);
    }
    public void Update(bool paused)
    {
        if (shown == paused) return; shown = paused;
        var c = Context!;
        Color ink = paused ? PgrTheme.BrassText : Color.ParseColor("#2A3139"), line = paused ? PgrTheme.Brass : PromptArt.SlateBlue;
        label.Text = paused ? "继续" : "暂停"; label.SetTextColor(ink);
        icon.SetImageDrawable(new PgrIconDrawable(paused ? "play" : "pause", paused ? PgrTheme.Brass : PromptArt.SlateBlue, den, vertical ? 18 : 16));
        var plate = PgrTheme.Plate(c, paused ? PgrTheme.A(PromptArt.PaperWarm, 252) : Color.White, line, 8, Corners.TopRight | Corners.BottomLeft,
            glow: paused ? PgrTheme.A(PgrTheme.Brass, 56) : null);
        var tick = new CornerTickDrawable(den, line);
        Background = new RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(line, 60)), new LayerDrawable(new Drawable[] { plate, tick }), null);
        ContentDescription = paused ? "继续倒计时" : "暂停倒计时，不自动续接";
    }
}

/// <summary>右下角 L 形刻线（按钮 / 卡片角饰）。</summary>
public sealed class CornerTickDrawable(float density, Color color) : PgrShapeDrawable(density)
{
    public float SizeDp { get; set; } = 6; public float InsetDp { get; set; } = 3;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.4f); paint.Color = C(color); paint.StrokeCap = Paint.Cap.Square;
        float x = b.Right - D(InsetDp), y = b.Bottom - D(InsetDp);
        canvas.DrawLine(x - D(SizeDp), y, x, y, paint); canvas.DrawLine(x, y - D(SizeDp), x, y, paint);
    }
}

/// <summary>暂停说明行（设计稿 pzRow）：「暂停 / 继续」键 + 说明；暂停后底与左线转黄铜，说明改「已暂停：……」。</summary>
public sealed class PauseRow : LinearLayout
{
    public readonly PauseToggle Toggle; readonly TextView note;
    bool? shown;
    public PauseRow(Context c) : base(c)
    {
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        Orientation = Orientation.Horizontal; SetGravity(GravityFlags.CenterVertical);
        SetPadding(Dp(8), Dp(7), Dp(10), Dp(7));
        Toggle = new PauseToggle(c);
        AddView(Toggle, new LayoutParams(Dp(96), Dp(40)));
        note = new TextView(c) { TextSize = 11.5f }; note.SetTextColor(PromptArt.Note); note.SetLineSpacing(Dp(2), 1);
        AddView(note, new LayoutParams(0, -2, 1) { LeftMargin = Dp(10) });
        Update(false);
    }
    public void Update(bool paused)
    {
        Toggle.Update(paused);
        if (shown == paused) return; shown = paused;
        Background = PromptArt.NoteBand(Context!, paused ? PgrTheme.A(PgrTheme.Brass, 20) : PgrTheme.A(PromptArt.SlateBlue, 18), paused ? PgrTheme.Brass : PromptArt.SlateBlue);
        if (!paused) { note.Text = PromptArt.PauseHint; return; }
        var sb = new SpannableStringBuilder("已暂停" + PromptArt.PausedHint);
        sb.SetSpan(new ForegroundColorSpan(PgrTheme.BrassText), 0, 3, SpanTypes.ExclusiveExclusive);
        sb.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, 3, SpanTypes.ExclusiveExclusive);
        note.TextFormatted = sb;
    }
}

/// <summary>选项片尾标。</summary>
public enum OptionTail { Chevron, Default, Ending, None }

public static class OptionTiles
{
    /// <summary>
    /// 编号选项片（设计稿 opt47 / lopt）：01 / 02 编号方块 + 文字 + 尾标。auto = 倒计时到点将选择的项：暖白底、黄铜描边、左侧金线、编号实心。
    /// accent = 自动项描边 / 编号色（结局用暗红）。compact = 横屏悬浮条（高 40dp、单行）。
    /// </summary>
    /// marker = 编号块文字（默认 01 / 02；宽屏听书卡用 A / B）；trailing = 文字与尾标之间的附加块（上次选择 / 已听 / 句数时长）。
    public static LinearLayout Tile(Context c, int index, string label, bool auto, OptionTail tail, string? endingName = null, Color? accent = null, bool compact = false,
        string? marker = null, View? trailing = null)
    {
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        Color ac = accent ?? PgrTheme.Brass;
        var row = new LinearLayout(c) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true }; row.SetGravity(GravityFlags.CenterVertical);
        var face = auto
            ? PgrTheme.Plate(c, PgrTheme.A(PromptArt.PaperWarm, 252), ac, compact ? 8 : 9, Corners.TopRight | Corners.BottomLeft, accent == null ? PgrArt.GoldLine : ac, AccentEdge.Left, glow: PgrTheme.A(PgrTheme.Brass, 46))
            : PgrTheme.Plate(c, PgrTheme.A(Color.White, 242), compact ? Color.ParseColor("#C3CBD4") : PgrTheme.Border, compact ? 8 : 9, Corners.TopRight | Corners.BottomLeft);
        row.Background = new RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Brass, 50)), face, null);
        row.SetPadding(Dp(compact ? 12 : 14), Dp(compact ? 0 : 6), Dp(compact ? 10 : 12), Dp(compact ? 0 : 6));
        row.SetMinimumHeight(Dp(compact ? 40 : 50));
        var no = new TextView(c) { Text = marker ?? (index + 1).ToString("00"), TextSize = marker != null ? (compact ? 11 : 12) : compact ? 9.5f : 10, Gravity = GravityFlags.Center };
        no.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); no.SetIncludeFontPadding(false);
        no.SetTextColor(auto ? Color.White : PgrTheme.Muted);
        no.Background = auto ? PgrTheme.Plate(c, ac, null, 0, Corners.None) : PgrTheme.Plate(c, Color.Transparent, PgrTheme.BorderStrong, 0, Corners.None);
        int ns = Dp(compact ? 20 : 22);
        row.AddView(no, new LinearLayout.LayoutParams(ns, ns) { RightMargin = Dp(compact ? 8 : 10) });
        var text = new TextView(c) { TextSize = compact ? 14 : 15 }; text.TextFormatted = PromptArt.OptionLabel(label);
        text.SetTextColor(PromptArt.Ink); text.SetTypeface(auto ? PgrTheme.SansBold : PgrTheme.SansMedium, auto ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        text.SetLineSpacing(Dp(2), 1);
        if (compact) { text.SetSingleLine(true); text.Ellipsize = TextUtils.TruncateAt.End; } else { text.SetMaxLines(3); text.Ellipsize = TextUtils.TruncateAt.End; }
        row.AddView(text, new LinearLayout.LayoutParams(0, -2, 1));
        if (trailing != null) row.AddView(trailing, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        if (tail is OptionTail.Default or OptionTail.Ending)
        {
            bool ending = tail == OptionTail.Ending;
            var col = new LinearLayout(c) { Orientation = Orientation.Vertical }; col.SetGravity(GravityFlags.End);
            Color ink = ending ? PromptArt.EndingRed : PgrTheme.BrassText;
            col.AddView(PgrUi.En(c, ending ? "ENDING" : "DEFAULT", 8.5f, ink), new LinearLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.End });
            var cn = new TextView(c) { Text = ending ? (string.IsNullOrEmpty(endingName) ? "结局" : "结局 · " + endingName) : "默认分支", TextSize = compact ? 10 : 10.5f };
            cn.SetTextColor(ink); cn.SetIncludeFontPadding(false); cn.SetSingleLine(true);
            col.AddView(cn, new LinearLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.End, TopMargin = Dp(2) });
            row.AddView(col, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        }
        else if (tail == OptionTail.Chevron)
        {
            var chev = new ImageView(c) { ImportantForAccessibility = ImportantForAccessibility.No };
            chev.SetImageDrawable(new PgrIconDrawable("chevron_right", Color.ParseColor("#8A96A3"), den, compact ? 14 : 16));
            row.AddView(chev, new LinearLayout.LayoutParams(Dp(compact ? 14 : 16), Dp(compact ? 14 : 16)) { LeftMargin = Dp(6) });
        }
        row.ContentDescription = label + (auto ? "（倒计时结束后自动选择）" : "");
        return row;
    }

    /// <summary>小号说明行：图标 + 文字（设计稿 hint47 / 卡片底部提示）。</summary>
    public static LinearLayout Hint(Context c, string icon, Color iconColor, string text, float sp = 11.5f, int maxLines = 0)
    {
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        var row = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        var glyph = new ImageView(c) { ImportantForAccessibility = ImportantForAccessibility.No };
        glyph.SetImageDrawable(new PgrIconDrawable(icon, iconColor, den, 13));
        row.AddView(glyph, new LinearLayout.LayoutParams(Dp(13), Dp(13)) { RightMargin = Dp(6), TopMargin = Dp(2) });
        var t = PromptArt.Label(c, text, sp, PgrTheme.Secondary, maxLines: maxLines);
        row.AddView(t, new LinearLayout.LayoutParams(0, -2, 1));
        return row;
    }
}

/// <summary>
/// 结局分歧卡头（设计稿 03）：深酒红→深石板渐变，左侧红→黄铜竖线，底边在 62% 处下折切口；可叠章节封面（左侧淡出）。
/// </summary>
public sealed class EndingHeadDrawable(float density) : PgrShapeDrawable(density)
{
    Bitmap? cover;
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    public float CutTopRightDp { get; set; } = 16;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float l = b.Left, t = b.Top, r = b.Right, bt = b.Bottom, notch = D(10), x = l + (r - l) * .62f, cut = D(CutTopRightDp);
        path.Reset(); path.MoveTo(l, t); path.LineTo(r - cut, t); path.LineTo(r, t + cut); path.LineTo(r, bt - notch); path.LineTo(x, bt - notch); path.LineTo(x - notch, bt); path.LineTo(l, bt); path.Close();
        int save = canvas.Save(); canvas.ClipPath(path);
        using (var g = new LinearGradient(0, t, 0, bt, new int[] { C(Color.ParseColor("#3A2A30")).ToArgb(), C(Color.ParseColor("#2A2228")).ToArgb(), C(Color.ParseColor("#272D35")).ToArgb() }, new float[] { 0, .6f, 1 }, Shader.TileMode.Clamp!))
        { paint.SetStyle(Paint.Style.Fill); paint.Color = Color.White; paint.SetShader(g); canvas.DrawRect(l, t, r, bt, paint); paint.SetShader(null); }
        if (cover is { IsRecycled: false } art)
        {
            float scale = Math.Max((r - l) / art.Width, (bt - t) / art.Height), w = art.Width * scale, h = art.Height * scale;
            using var dst = new RectF(l + (r - l - w) / 2, t + (bt - t - h) * .3f, l + (r - l - w) / 2 + w, t + (bt - t - h) * .3f + h);
            int layer = canvas.SaveLayer(l, t, r, bt, null);
            paint.Reset(); paint.AntiAlias = true; paint.FilterBitmap = true; paint.Alpha = 56 * alpha / 255; canvas.DrawBitmap(art, null, dst, paint); paint.Alpha = 255;
            using (var mask = new LinearGradient(l, 0, r, 0, new int[] { Color.Argb(0, 0, 0, 0).ToArgb(), Color.Argb(0, 0, 0, 0).ToArgb(), Color.Black.ToArgb() }, new float[] { 0, .2f, 1 }, Shader.TileMode.Clamp!))
            using (var xfer = new PorterDuffXfermode(PorterDuff.Mode.DstIn!))
            { paint.SetShader(mask); paint.SetXfermode(xfer); canvas.DrawRect(l, t, r, bt, paint); paint.SetXfermode(null); paint.SetShader(null); }
            canvas.RestoreToCount(layer);
        }
        using (var rr = new RectF(l, t, r, bt)) { PgrArt.Grid(canvas, rr, D(16), PgrArt.MistLine, .06f, d, paint, alpha); }
        using (var g = new LinearGradient(0, t, 0, bt, C(Color.ParseColor("#E0485C")), C(PgrTheme.Brass), Shader.TileMode.Clamp!))
        { paint.SetStyle(Paint.Style.Fill); paint.SetShader(g); canvas.DrawRect(l, t, l + D(3), bt, paint); paint.SetShader(null); }
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// 横屏融合条（设计稿 fuse47 / 16-C）：冷灰白纸面（22dp 网格）+ 左侧深石板块（右缘斜切 22dp）+ 虚线接缝 + 左侧竖线 + 右下黄铜短刻；
/// 外轮廓右上 / 左下切角 12dp。LeftColor 可改（结局用酒红渐变）。纯绘制，不拦截点击。
/// </summary>
public sealed class FusedBarDrawable(float density) : PgrShapeDrawable(density)
{
    public float LeftWidthDp { get; set; } = 214;
    public float SlantDp { get; set; } = 22;
    public float CutDp { get; set; } = 12;
    public Color LeftTop { get; set; } = PgrArt.DeckTop;
    public Color LeftBottom { get; set; } = Color.ParseColor("#272D35");
    public Color EdgeTop { get; set; } = PgrArt.GoldLine;
    public Color EdgeBottom { get; set; } = PgrTheme.Brass;
    public Color Seam { get; set; } = Color.Argb(230, 226, 194, 126);
    public bool Arcs { get; set; }
    readonly DarkPanelDrawable deck = new(density, DecoTier.S) { ShowTide = false, Inscription = false, Watermark = false, ShowEmblem = false };
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var r = new RectF(b);
        float cut = D(CutDp);
        PgrArt.Chamfer(path, r, 0, cut, 0, cut);
        int save = canvas.Save(); canvas.ClipPath(path);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PromptArt.OverlayPaper); canvas.DrawRect(r, paint);
        PgrArt.Grid(canvas, r, D(22), PgrTheme.Ink, .03f, d, paint, alpha);
        float lw = Math.Min(D(LeftWidthDp), r.Width() * .6f), slant = D(SlantDp);
        using var left = new global::Android.Graphics.Path();
        left.MoveTo(r.Left, r.Top); left.LineTo(r.Left + lw, r.Top); left.LineTo(r.Left + lw - slant, r.Bottom); left.LineTo(r.Left, r.Bottom); left.Close();
        int inner = canvas.Save(); canvas.ClipPath(left);
        deck.FillTop = LeftTop; deck.FillBottom = LeftBottom; deck.SetBounds(b.Left, b.Top, (int)(r.Left + lw), b.Bottom); deck.Draw(canvas);
        using (var g = new LinearGradient(0, r.Top, 0, r.Bottom, C(EdgeTop), C(EdgeBottom), Shader.TileMode.Clamp!))
        { paint.SetStyle(Paint.Style.Fill); paint.SetShader(g); canvas.DrawRect(r.Left, r.Top, r.Left + D(3), r.Bottom, paint); paint.SetShader(null); }
        canvas.RestoreToCount(inner);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f); paint.Color = C(Seam);
        using (var dash = new DashPathEffect(new[] { D(3), D(2) }, 0)) { paint.SetPathEffect(dash); canvas.DrawLine(r.Left + lw - D(1), r.Top, r.Left + lw - slant + D(1), r.Bottom, paint); paint.SetPathEffect(null); }
        if (Arcs)
        {
            paint.StrokeWidth = D(1.2f); paint.Color = C(Color.Argb(217, 201, 162, 74));
            path.Reset(); float ax = r.Left + lw - D(60), ay = r.Bottom - D(46);
            path.MoveTo(ax, ay + D(38)); path.CubicTo(ax + D(40), ay + D(13), ax + D(96), ay + D(3), ax + D(160), ay + D(5)); canvas.DrawPath(path, paint);
        }
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(Color.ParseColor("#C9A24A"));
        canvas.DrawRect(r.Right - D(40), r.Bottom - D(2), r.Right - D(12), r.Bottom, paint);
        canvas.RestoreToCount(save);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .75f); paint.Color = C(Color.Argb(36, 255, 255, 255));
        PgrArt.Chamfer(path, r, 0, cut, 0, cut); canvas.DrawPath(path, paint);
    }
    public override void SetAlpha(int value) { base.SetAlpha(value); deck.SetAlpha(value); }
}

/// <summary>横屏融合条上的纸面小键（设计稿 lBtn）：白底发丝切角，可竖排（图标在上）。primaryBrass = 黄铜主操作（按顺序继续）。</summary>
public static class OverlayKeys
{
    public static Button Key(Context c, string text, string? icon, bool vertical, float sp = 12, bool brass = false, bool quiet = false)
    {
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        var b = new Button(c) { Text = text, TextSize = sp }; b.SetAllCaps(false); b.StateListAnimator = null; b.Elevation = 0; b.BackgroundTintList = null;
        b.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); b.SetTextColor(brass ? PgrTheme.BrassText : PromptArt.Ink);
        var face = PgrTheme.Plate(c, brass ? PgrTheme.A(PromptArt.PaperWarm, 252) : quiet ? Color.Argb(128, 255, 255, 255) : Color.White,
            brass ? PgrTheme.Brass : Color.ParseColor("#C3CBD4"), 8, Corners.TopRight | Corners.BottomLeft, shadow: !quiet);
        b.Background = new RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 36)), face, null);
        b.SetMinHeight(Dp(30)); b.SetMinimumHeight(Dp(30)); b.SetMinWidth(0); b.SetMinimumWidth(0);
        b.SetPadding(Dp(6), Dp(4), Dp(6), Dp(4)); b.SetSingleLine(!vertical); b.Gravity = GravityFlags.Center;
        if (icon != null)
        {
            var d = new PgrIconDrawable(icon, brass ? PgrTheme.Brass : PromptArt.Ink, den, vertical ? 16 : 14);
            if (vertical) b.SetCompoundDrawablesWithIntrinsicBounds(null, d, null, null); else b.SetCompoundDrawablesWithIntrinsicBounds(d, null, null, null);
            b.CompoundDrawablePadding = Dp(vertical ? 5 : 6);
        }
        return b;
    }
}
