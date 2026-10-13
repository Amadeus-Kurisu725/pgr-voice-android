using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 0.4.7 停下处视觉（M）：15 秒倒计时细轨、倒计时铭文数字、17-A 风格提示条、无配音 / 待配音发丝签。
/// 全部取自 PgrTheme 色板：黄铜 = 分支 / 路线状态，深石板 = 名牌 / 图标片，冷灰白 = 纸面。
/// </summary>
public sealed class CountdownRailDrawable : Drawable
{
    readonly Paint paint = new(PaintFlags.AntiAlias);
    readonly global::Android.Graphics.Path path = new();
    readonly float d;
    readonly bool onDark;
    float fraction = 1;
    public CountdownRailDrawable(float density, bool onDark = false) { d = density; this.onDark = onDark; }
    /// <summary>剩余比例 1 → 0：黄铜实线从右向左收短，末端菱形标记当前位置。</summary>
    public float Fraction { get => fraction; set { fraction = Math.Clamp(value, 0, 1); InvalidateSelf(); } }
    bool paused;
    /// <summary>暂停：进度与菱形转冷灰（设计稿 16），表示倒计时停住。</summary>
    public bool Paused { get => paused; set { if (paused == value) return; paused = value; InvalidateSelf(); } }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float cy = b.ExactCenterY(), left = b.Left + 1 * d, right = b.Right - 5 * d, w = right - left;
        // 发丝底轨 + 每 5 秒一格刻度（15 秒共 3 段）。
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = onDark ? PgrTheme.A(PgrTheme.NavSub, 90) : PgrTheme.Border;
        canvas.DrawRect(left, cy - .5f * d, right, cy + .5f * d, paint);
        for (int i = 0; i <= 3; i++) { float x = left + w * i / 3f; canvas.DrawRect(x - .5f * d, cy - 2.5f * d, x + .5f * d, cy + 2.5f * d, paint); }
        // 黄铜进度（剩余时间）。
        float end = left + w * fraction;
        var grey = Color.ParseColor("#9AA3AD");
        paint.Color = paused ? grey : PgrTheme.Brass; canvas.DrawRect(left, cy - 1f * d, end, cy + 1f * d, paint);
        // 末端菱形。
        float r = 3.5f * d; path.Reset(); path.MoveTo(end, cy - r); path.LineTo(end + r, cy); path.LineTo(end, cy + r); path.LineTo(end - r, cy); path.Close();
        paint.Color = paused ? grey : onDark ? PgrArt.GoldLine : PgrTheme.Brass; canvas.DrawPath(path, paint);
    }
    public override void SetAlpha(int alpha) { paint.Alpha = alpha; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); path.Dispose(); } base.Dispose(disposing); }
}

public static class StopVisuals
{
    /// <summary>深色托盘上的浅黄铜（对 #2A3139 ≥ 7:1）。</summary>
    public static readonly Color BrassOnDark = PgrArt.GoldLine;

    /// <summary>倒计时铭文：等宽大号秒数 + 「SEC · AUTO」窄体副标，黄铜发丝切角框。</summary>
    public static (LinearLayout Badge, TextView Number) CountdownBadge(Context context, int seconds, bool onDark = false)
    {
        var badge = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        badge.SetGravity(GravityFlags.CenterVertical);
        badge.Background = PgrTheme.Plate(context, onDark ? PgrTheme.A(PgrTheme.Brass, 34) : PgrTheme.A(Color.White, 230), PgrTheme.Brass, PgrTheme.CutSmall, Corners.Diagonal);
        int p = PgrTheme.Dp(context, 1);
        badge.SetPadding(8 * p, 2 * p, 8 * p, 2 * p);
        var number = PgrTheme.Apply(new TextView(context) { Text = seconds.ToString("00") }, PgrTheme.Role.Code, onDark ? BrassOnDark : PgrTheme.BrassText);
        number.TextSize = 17; number.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold); number.LetterSpacing = .06f;
        badge.AddView(number);
        var unit = PgrUi.En(context, "SEC · AUTO", 8.5f, onDark ? BrassOnDark : PgrTheme.BrassText);
        badge.AddView(unit, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = 6 * p });
        badge.ContentDescription = $"{seconds} 秒后自动处理";
        return (badge, number);
    }

    /// <summary>倒计时细轨视图（高 10dp，命中区不参与点击）。</summary>
    public static (View View, CountdownRailDrawable Rail) CountdownRail(Context context, float fraction, bool onDark = false)
    {
        var rail = new CountdownRailDrawable(context.Resources!.DisplayMetrics!.Density, onDark) { Fraction = fraction };
        var view = new View(context) { Background = rail, ImportantForAccessibility = ImportantForAccessibility.No };
        return (view, rail);
    }

    /// <summary>
    /// 17-A 风格提示条（应用内）：左侧深石板图标片（黄铜刻线），右侧冷灰白纸面文字；底边黄铜发丝。
    /// 用于「已自动选择：…」「此处分支续接未核实，已按顺序继续」「已进入下一小节」「已记录反馈」。
    /// </summary>
    public static LinearLayout Notice(Context context, string text, string icon = "branch", string english = "AUTO · ROUTE")
    {
        int p = PgrTheme.Dp(context, 1);
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.Background = PgrTheme.Plate(context, PgrTheme.A(Color.White, 236), PgrTheme.BorderStrong, PgrTheme.CutSmall, Corners.Diagonal, PgrTheme.Brass, AccentEdge.Bottom, shadow: true);
        row.SetPadding(0, 0, 10 * p, 0); row.SetMinimumHeight(44 * p);
        var tile = new FrameLayout(context);
        tile.Background = PgrTheme.Plate(context, PgrTheme.NamePlate, null, PgrTheme.CutSmall, Corners.TopLeft);
        var glyph = new View(context) { Background = new PgrIconDrawable(icon, BrassOnDark, context.Resources!.DisplayMetrics!.Density, 16), ImportantForAccessibility = ImportantForAccessibility.No };
        tile.AddView(glyph, new FrameLayout.LayoutParams(18 * p, 18 * p, GravityFlags.Center));
        row.AddView(tile, new LinearLayout.LayoutParams(44 * p, -1) { RightMargin = 10 * p });
        var texts = new LinearLayout(context) { Orientation = Orientation.Vertical };
        texts.AddView(PgrUi.En(context, english, 8.5f, PgrTheme.BrassText));
        var body = new TextView(context) { Text = text, TextSize = 13.5f }; body.SetTextColor(PgrTheme.Foreground); body.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        body.SetMaxLines(2); body.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        texts.AddView(body);
        row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1) { TopMargin = 5 * p, BottomMargin = 6 * p });
        row.ContentDescription = text;
        return row;
    }

    /// <summary>无配音 / 待配音发丝签：无配音为弱色发丝框，待配音为黄铜淡底 + 黄铜发丝。onDark 用于深石板托盘。</summary>
    public static TextView VoiceTag(Context context, bool draft, bool onDark)
    {
        int p = PgrTheme.Dp(context, 1);
        var tag = new TextView(context) { Text = draft ? "待配音" : "无配音", TextSize = 10.5f, Gravity = GravityFlags.Center };
        tag.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); tag.LetterSpacing = .08f; tag.SetIncludeFontPadding(false);
        tag.SetPadding(7 * p, 2 * p, 7 * p, 3 * p);
        Color stroke, fill, ink;
        if (draft) { stroke = PgrTheme.Brass; fill = onDark ? PgrTheme.A(PgrTheme.Brass, 46) : PgrTheme.BrassTint; ink = onDark ? BrassOnDark : PgrTheme.BrassText; }
        else { stroke = onDark ? PgrTheme.A(PgrTheme.NavSub, 170) : PgrTheme.BorderStrong; fill = Color.Transparent; ink = onDark ? PgrTheme.NavSub : PgrTheme.Muted; }
        tag.SetTextColor(ink);
        tag.Background = PgrTheme.Plate(context, fill, stroke, 4, Corners.Diagonal);
        tag.ContentDescription = draft ? "待配音：本句尚未配音，字幕停留后继续" : "无配音：本句没有可用音频，字幕停留后继续";
        return tag;
    }
}
