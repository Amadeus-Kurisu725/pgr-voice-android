using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>设计稿 15 行状态。</summary>
public enum ExploreRowState { Unheard, Playing, Heard, Disabled }
/// <summary>设计稿 15 行类型：对话（人物）/ 出口（黄铜）/ 回环（回到营地继续探索，不是出口）。</summary>
public enum ExploreRowKind { Chat, Exit, Loop }

/// <summary>
/// 0.4.8 设计稿 12 / 13 / 15：探索菜单行。触控行高 ≥ 40dp。
/// · 对话行：未听（发丝框 + 「未听」）/ 正在收听（红色左线 + ▶）/ 已听（青绿 ✓ + 「已听」，整体变灰）/ 不可用（锁）。
/// · 出口行：黄铜底 + EXIT 图标（「出口 · 随时可选」/「已选」/ 不可用）。
/// · 回环行：回放图标「回到营地继续探索（不是出口）」。
/// 不显示任何主线 / 闲聊标识。
/// </summary>
public static class ExploreRows
{
    /// <param name="compact">游戏内探索卡（方案 A）：行高 32dp、字号缩小。</param>
    public static LinearLayout Row(Context c, ExploreRowKind kind, ExploreRowState state, string title, string sub, string tail, bool onPaper = true, bool compact = false)
    {
        float den = c.Resources!.DisplayMetrics!.Density; int Dp(float v) => (int)(v * den + .5f);
        bool exit = kind == ExploreRowKind.Exit, heard = state == ExploreRowState.Heard, playing = state == ExploreRowState.Playing, disabled = state == ExploreRowState.Disabled;
        var row = new LinearLayout(c) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical);
        row.SetMinimumHeight(Dp(compact ? 32 : 44)); row.SetPadding(Dp(compact ? 8 : 10), Dp(compact ? 2 : 5), Dp(compact ? 6 : 10), Dp(compact ? 2 : 5));
        Color line = exit ? PgrTheme.Brass : playing ? PgrTheme.Red : heard ? Color.ParseColor("#C9D0D7") : Color.ParseColor("#C3CBD4");
        Color fill = exit ? PgrTheme.A(PromptArt.PaperWarm, disabled ? 160 : 250) : playing ? PgrTheme.A(Color.White, 250) : heard ? Color.Argb(200, 236, 239, 242) : PgrTheme.A(Color.White, disabled ? 150 : 242);
        Drawable face = playing || exit
            ? PgrTheme.Plate(c, fill, line, 8, Corners.TopRight | Corners.BottomLeft, line, AccentEdge.Left)
            : PgrTheme.Plate(c, fill, line, 8, Corners.TopRight | Corners.BottomLeft);
        row.Background = new RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Brass, 50)), face, null);
        string icon = exit ? "exit" : kind == ExploreRowKind.Loop ? "replay" : disabled ? "lock" : heard ? "check" : playing ? "play" : "headphones";
        Color iconColor = exit ? PgrTheme.Brass : heard ? PromptArt.DoneTeal : playing ? PgrTheme.Red : disabled ? PromptArt.PausedGrey : PromptArt.SlateBlue;
        var glyph = new ImageView(c) { ImportantForAccessibility = ImportantForAccessibility.No };
        int glyphDp = compact ? 13 : 16;
        glyph.SetImageDrawable(new PgrIconDrawable(icon, iconColor, den, glyphDp));
        row.AddView(glyph, new LinearLayout.LayoutParams(Dp(glyphDp), Dp(glyphDp)) { RightMargin = Dp(compact ? 7 : 9) });
        var texts = new LinearLayout(c) { Orientation = Orientation.Vertical };
        var t = new TextView(c) { Text = title, TextSize = compact ? 11.5f : 13.5f }; t.SetSingleLine(true); t.Ellipsize = TextUtils.TruncateAt.End;
        t.SetTextColor(heard || disabled ? PromptArt.PausedInk : PromptArt.Ink);
        t.SetTypeface(playing || exit ? PgrTheme.SansBold : PgrTheme.SansMedium, playing || exit ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        texts.AddView(t);
        if (sub.Length > 0)
        {
            var s = new TextView(c) { Text = sub, TextSize = 11 }; s.SetSingleLine(true); s.Ellipsize = TextUtils.TruncateAt.End;
            s.SetTextColor(playing ? PgrTheme.Red : heard || disabled ? PromptArt.PausedGrey : PgrTheme.Secondary);
            texts.AddView(s, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(1) });
        }
        row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        if (tail.Length > 0)
        {
            var tag = new TextView(c) { Text = tail, TextSize = compact ? 9.5f : 10.5f, Gravity = GravityFlags.Center }; tag.SetSingleLine(true);
            Color tc = exit ? PgrTheme.BrassText : heard ? PromptArt.DoneTeal : playing ? PgrTheme.Red : PgrTheme.Muted;
            tag.SetTextColor(tc);
            tag.Background = PgrTheme.Plate(c, PgrTheme.A(tc, 20), PgrTheme.A(tc, 140), 4, Corners.TopRight | Corners.BottomLeft);
            tag.SetPadding(Dp(6), Dp(1), Dp(6), Dp(1));
            row.AddView(tag, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        }
        row.ContentDescription = title + (sub.Length > 0 ? "，" + sub : "") + (tail.Length > 0 ? "，" + tail : "");
        if (disabled) row.Alpha = .75f;
        return row;
    }
}
