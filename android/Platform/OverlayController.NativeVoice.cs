using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp.Platform;

public sealed partial class OverlayController
{
    bool nativeVoice;

    /// <summary>当前句由游戏播原配、配音包静音占位时为 true；只影响显示。</summary>
    public void SetNativeVoice(bool on) => OnMain(() =>
    {
        if (disposed || nativeVoice == on) return;
        nativeVoice = on;
        RefreshCurrentLine();
    });

    /// <summary>台词条：原配句文字换青色并前置小声波；读屏补一句“正在听游戏原配”。</summary>
    void StyleCurrentLine(TextView view, string text)
    {
        view.SetTextColor(nativeVoice ? PgrTheme.TealText : PgrTheme.Foreground);
        view.SetCompoundDrawables(nativeVoice ? Wave() : null, null, null, null);
        view.CompoundDrawablePadding = nativeVoice ? Dp(4) : 0;
        view.ContentDescription = "当前台词，" + text + (nativeVoice ? "，" + AppSession.NativeVoiceStatus : "");
    }

    /// <summary>状态行：原配句换青色、左侧青线、前置小声波；其余照旧。</summary>
    void StyleStatus(TextView view, string text, bool paused)
    {
        bool native = text.Contains(AppSession.NativeVoiceStatus, StringComparison.Ordinal);
        view.Text = text;
        view.SetTextColor(native ? PgrTheme.TealText : paused ? PgrTheme.Secondary : PgrTheme.Foreground);
        view.SetCompoundDrawables(native ? Wave() : null, null, null, null);
        view.CompoundDrawablePadding = native ? Dp(5) : 0;
        view.Background = native ? new LeftRuleDrawable(PgrTheme.Cyan, Dp(3)) : null;
        view.SetPadding(native ? Dp(9) : Dp(3), 0, Dp(3), Dp(8));
    }

    Drawable Wave()
    {
        var wave = new NativeVoiceWaveDrawable(PgrTheme.Cyan);
        wave.SetBounds(0, 0, Dp(13), Dp(10));
        return wave;
    }

    sealed class NativeVoiceWaveDrawable(Color color) : Drawable
    {
        static readonly float[] Heights = { .45f, 1f, .65f, .85f };
        readonly Paint paint = new(PaintFlags.AntiAlias) { Color = color };
        public override void Draw(Canvas canvas)
        {
            var b = Bounds;
            float gap = b.Width() / (Heights.Length * 2f - 1);
            for (int i = 0; i < Heights.Length; i++)
            {
                float left = b.Left + i * 2 * gap, top = b.Bottom - b.Height() * Heights[i];
                canvas.DrawRect(left, top, left + gap, b.Bottom, paint);
            }
        }
        public override void SetAlpha(int alpha) => paint.Alpha = alpha;
        public override void SetColorFilter(ColorFilter? colorFilter) => paint.SetColorFilter(colorFilter);
        [Obsolete] public override int Opacity => (int)Format.Translucent;
    }

    sealed class LeftRuleDrawable(Color color, int width) : Drawable
    {
        readonly Paint paint = new() { Color = color };
        public override void Draw(Canvas canvas) => canvas.DrawRect(Bounds.Left, Bounds.Top, Bounds.Left + width, Bounds.Bottom, paint);
        public override void SetAlpha(int alpha) => paint.Alpha = alpha;
        public override void SetColorFilter(ColorFilter? colorFilter) => paint.SetColorFilter(colorFilter);
        [Obsolete] public override int Opacity => (int)Format.Translucent;
    }
}
