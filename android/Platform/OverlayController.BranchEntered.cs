using Android.Graphics;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp.Platform;

public sealed partial class OverlayController
{
    View? branchEnteredView;

    /// <summary>
    /// 0.4.8（设计稿 09）：自动选入分支后的提示改为「已自动选择」只读融合条，约 3 秒后收起（原为 1.5 秒的只读小条）。
    /// 倒计时到点时会话随后还会发 GameAutoSelected，用同一条替换文字。
    /// </summary>
    public void ShowBranchEntered(string message) => ShowAutoSelectedBar(message, "已进入分支 · 自动播放继续", "BRANCH · ENTERED", "已进入分支");

    /// <summary>
    /// SPEC C.5 / 17-A ⑥: the return-to-game sentences that used to be system Toasts, shown as a read-only top hint
    /// (top centre, NotTouchable, ~3.5 s, same style as 17-A ①). Returns false when overlays are unavailable so the
    /// caller falls back to a Toast.
    /// </summary>
    public bool ShowTopHint(string message)
    {
        if (disposed || !CanShow || string.IsNullOrWhiteSpace(message)) return false;
        OnMain(() => ShowReadOnlyHint(message, "open", PgrTheme.Red, 3500));
        return true;
    }

    /// <summary>17-A ①: dark icon chip (S) + cold-white paper text plate, top centre on the 16-C axis; never touchable.</summary>
    void ShowReadOnlyHint(string message, string icon, Color accent, int durationMs)
    {
        ClearBranchEntered();
        if (disposed || !CanShow || string.IsNullOrWhiteSpace(message)) return;
        var safe = ScreenArea();
        var strip = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var chip = new FrameLayout(context) { Background = new DarkPanelDrawable(density, DecoTier.S) { CutBottomRightDp = 8, ShowEmblem = false, Watermark = false, Inscription = false, ShowTide = false } };
        var glyph = new View(context) { Background = new PgrIconDrawable(icon, accent == PgrTheme.Brass ? PgrArt.GoldLine : Color.White, density, 20) };
        chip.AddView(glyph, new FrameLayout.LayoutParams(ReadDp(20), ReadDp(20), GravityFlags.Center));
        strip.AddView(chip, new LinearLayout.LayoutParams(ReadDp(48), -1));
        var text = new TextView(context) { Text = message, TextSize = 14, Gravity = GravityFlags.CenterVertical };
        text.SetTextColor(PgrTheme.Foreground);
        text.Background = PgrTheme.Plate(context, Color.Argb(248, 0xF2, 0xF3, 0xF5), PgrTheme.GlassLine, PgrTheme.CutSmall, Corners.BottomRight, accent, AccentEdge.Left);
        text.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal);
        text.SetPadding(ReadDp(12), ReadDp(8), ReadDp(12), ReadDp(8));
        text.SetMinHeight(ReadDp(48));
        text.SetMaxLines(2); text.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        strip.AddView(text, new LinearLayout.LayoutParams(0, -2, 1));
        int width = Math.Min(ReadDp(620), safe.Right - safe.Left - ReadDp(24));
        var layout = new WindowManagerLayoutParams(width, -2, WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable | WindowManagerFlags.LayoutInScreen,
            Format.Translucent) { Gravity = GravityFlags.Top | GravityFlags.Left,
            X = safe.Left + (safe.Right - safe.Left - width) / 2, Y = safe.Top + ReadDp(8) };
        branchEnteredView = strip;
        SetPromptWindowState(strip, layout, readOnly: true);
        try
        {
            windows.AddView(strip, layout);
            main.PostDelayed(() => { if (ReferenceEquals(branchEnteredView, strip)) ClearBranchEntered(); }, durationMs);
        }
        catch { ClearBranchEntered(); }
    }

    public void ClearBranchEntered() => OnMain(() =>
    {
        var old = branchEnteredView; branchEnteredView = null;
        if (old == null) return;
        try { windows.RemoveViewImmediate(old); } catch { }
        old.Dispose();
    });
}
