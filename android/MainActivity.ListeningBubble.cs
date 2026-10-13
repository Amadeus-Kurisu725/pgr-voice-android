using Android.Graphics;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Listening;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.8 设计稿 07（应用内气泡）：不在听书页时，听书倒计时到点自动选择后在页面顶边弹出融合条
/// 「AUTO · SELECTED · 听书 / 已自动选择：xxx / 倒计时结束 · 默认分支第 N 个选项 · 继续收听」，右侧「打开」回到听书页；约 4 秒收起。
/// 只读 ListeningSession.AutoNotice，不参与规则。
/// </summary>
public sealed partial class MainActivity
{
    PopupWindow? listeningBubble;
    string? listeningBubbleShown;

    void MaybeShowListeningBubble()
    {
        var player = session.Listening;
        string? notice = player.AutoNotice is { } n && player.AutoNoticeEnglish == "AUTO · SELECTED" ? n : null;
        if (notice == null) { listeningBubbleShown = null; return; }
        if (page == 5 || notice == listeningBubbleShown || shell.WindowToken == null) { listeningBubbleShown = notice; return; }
        listeningBubbleShown = notice;
        listeningBubble?.Dismiss();
        float density = Resources!.DisplayMetrics!.Density;
        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal, Background = new FusedBarDrawable(density) { LeftWidthDp = 150 } };
        bar.SetMinimumHeight(Dp(68));
        var left = new LinearLayout(this) { Orientation = Orientation.Vertical }; left.SetGravity(GravityFlags.CenterVertical);
        left.SetPadding(Dp(14), Dp(8), Dp(4), Dp(8));
        left.AddView(PgrUi.En(this, "AUTO · SELECTED", 9, PgrArt.GoldLine));
        left.AddView(PromptArt.Label(this, "听书", 15, Color.White, true, 1), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        bar.AddView(left, new LinearLayout.LayoutParams(Dp(128), -1));
        var right = new LinearLayout(this) { Orientation = Orientation.Vertical }; right.SetGravity(GravityFlags.CenterVertical);
        right.SetPadding(Dp(26), Dp(8), Dp(8), Dp(8));
        var title = PromptArt.Label(this, notice, 14.5f, PromptArt.Ink, true, 1); title.SetSingleLine(true); title.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        right.AddView(title);
        string detail = player.AutoNoticeDetail.Length > 0 ? player.AutoNoticeDetail + " · 继续收听" : "继续收听";
        right.AddView(PromptArt.Label(this, detail, 11, PgrTheme.Secondary, maxLines: 1), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        bar.AddView(right, new LinearLayout.LayoutParams(0, -2, 1));
        var open = OverlayKeys.Key(this, "打开", "open", true, 12);
        bar.AddView(open, new LinearLayout.LayoutParams(Dp(62), -1) { TopMargin = Dp(8), BottomMargin = Dp(8), RightMargin = Dp(8) });
        bar.ContentDescription = notice + "，" + detail;
        int width = Math.Min(Dp(560), shell.Width - Dp(24));
        var popup = new PopupWindow(bar, width, ViewGroup.LayoutParams.WrapContent, false) { OutsideTouchable = true };
        open.Click += (_, _) => Safe(() => { popup.Dismiss(); ShowPage(5); });
        listeningBubble = popup;
        try
        {
            popup.ShowAtLocation(shell, GravityFlags.Top | GravityFlags.CenterHorizontal, 0, Dp(28));
            shell.PostDelayed(() => { if (ReferenceEquals(listeningBubble, popup)) { popup.Dismiss(); listeningBubble = null; } }, 4000);
        }
        catch { listeningBubble = null; }
    }
}
