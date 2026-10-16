using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 仅覆盖用户指定的游戏点按范围。所有方法与回调均须在 UI 线程使用。
/// 轻点由本窗口消费；调用方先 Suspend，注入实际游戏点击，回调核验后再 Resume。
/// </summary>
public sealed class TapFollowOverlay : IDisposable
{
    readonly Context context;
    readonly IWindowManager windows;
    readonly TouchSurface surface;
    readonly WindowManagerLayoutParams parameters;
    bool wanted, suspended, attached, disposed;
    public bool IsVisible => attached;

    public TapFollowOverlay(Context context, int displayWidth, int displayHeight, ScreenRegion region, Action<float, float> tapped, bool showHint = true)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(region); ArgumentNullException.ThrowIfNull(tapped);
        if (displayWidth <= 0 || displayHeight <= 0 || !float.IsFinite(region.Left) || !float.IsFinite(region.Top) ||
            !float.IsFinite(region.Width) || !float.IsFinite(region.Height) || region.Left < 0 || region.Top < 0 ||
            region.Width <= 0 || region.Height <= 0 || region.Left + region.Width > 1.00001f || region.Top + region.Height > 1.00001f)
            throw new ArgumentOutOfRangeException(nameof(region), "点按范围须位于当前物理屏幕内。");
        this.context = context;
        windows = context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>();
        int x = Math.Clamp((int)(region.Left * displayWidth), 0, displayWidth - 1);
        int y = Math.Clamp((int)(region.Top * displayHeight), 0, displayHeight - 1);
        int width = Math.Clamp((int)(region.Width * displayWidth), 1, displayWidth - x);
        int height = Math.Clamp((int)(region.Height * displayHeight), 1, displayHeight - y);
        surface = new TouchSurface(context, tapped, showHint);
        parameters = new WindowManagerLayoutParams(width, height, WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutInScreen,
            Format.Translucent) { Gravity = GravityFlags.Top | GravityFlags.Left, X = x, Y = y };
        // 保存的是物理屏幕坐标；避免系统状态栏 inset 再把框往下平移一次。
        if (OperatingSystem.IsAndroidVersionAtLeast(30)) parameters.FitInsetsTypes = 0;
    }

    public void Show()
    {
        if (disposed) return;
        wanted = true; AttachIfNeeded();
    }

    public void Suspend()
    {
        if (disposed) return;
        suspended = true; surface.CancelGesture(); Detach();
    }

    public void Resume()
    {
        if (disposed) return;
        suspended = false; AttachIfNeeded();
    }

    public void Hide()
    {
        if (disposed) return;
        wanted = false; suspended = false; surface.CancelGesture(); Detach();
    }

    void AttachIfNeeded()
    {
        if (!wanted || suspended || attached) return;
        windows.AddView(surface, parameters); attached = true;
    }

    void Detach()
    {
        if (!attached) return;
        // 同步撤窗，调用方才可安排系统注入；不发送任何触摸事件。
        try { windows.RemoveViewImmediate(surface); }
        catch (Java.Lang.IllegalArgumentException) { /* 已由系统移除。 */ }
        attached = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        wanted = false; suspended = true; surface.CancelGesture(); Detach();
        disposed = true; surface.Dispose(); parameters.Dispose();
        // Context 与系统 WindowManager 由平台持有，不在此 Dispose。
        GC.KeepAlive(context);
    }

    [Register("cn/pgrvoice/player/TapFollowSurface")]
    public sealed class TouchSurface : View
    {
        readonly Paint paint = new(PaintFlags.AntiAlias);
        readonly TapFollowGestureGate? gestures;
        readonly Action<float, float>? tapped;
        readonly bool showHint = true;
        readonly float density = 1, textSize = 12;

        public TouchSurface(Context context, Action<float, float> tapped, bool showHint = true) : base(context)
        {
            this.tapped = tapped;
            this.showHint = showHint;
            density = context.Resources?.DisplayMetrics?.Density ?? 1;
            textSize = Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, 12, context.Resources!.DisplayMetrics);
            gestures = new TapFollowGestureGate(12 * density);
            ContentDescription = "点按跟随";
        }
        public TouchSurface(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

        public void CancelGesture() => gestures?.Cancel();

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            if (!showHint)
            {
                // 分支识别框位于 OCR 文字矩形之外，只画细线，不把提示字或底色送回 OCR。
                paint.Color = PgrTheme.A(PgrTheme.Cyan, 220); paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = 1.5f;
                canvas.DrawRect(.75f, .75f, Math.Max(.75f, Width - .75f), Math.Max(.75f, Height - .75f), paint);
                return;
            }
            // 14-B ③: faint wash + ticked frame line + four HUD corners + dark AA label pinned inside the top-left.
            // Everything is drawn inside this window, so the touch area is exactly the saved region (unchanged).
            var red = Color.ParseColor("#E0485C");
            canvas.DrawColor(PgrTheme.A(red, 9));
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = density; paint.Color = PgrTheme.A(red, 120);
            canvas.DrawRect(density / 2, density / 2, Math.Max(density, Width - density / 2), Math.Max(density, Height - density / 2), paint);
            paint.SetStyle(Paint.Style.Fill); paint.Color = PgrTheme.A(red, 204);
            float tick = 5 * density;
            foreach (float f in new[] { .25f, .5f, .75f })
            {
                float x = Width * f;
                canvas.DrawRect(x, 0, x + density, Math.Min(tick, Height / 2f), paint);
                canvas.DrawRect(x, Math.Max(Height / 2f, Height - tick), x + density, Height, paint);
            }
            float arm = Math.Min(14 * density, Math.Min(Width, Height) / 3f), e = 1.5f * density, sw = 2 * density;
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = sw;
            foreach (var (cx, cy, dx, dy) in new[] { (e, e, 1f, 1f), (Width - e, e, -1f, 1f), (e, Height - e, 1f, -1f), (Width - e, Height - e, -1f, -1f) })
            { canvas.DrawLine(cx, cy, cx + dx * arm, cy, paint); canvas.DrawLine(cx, cy, cx, cy + dy * arm, paint); }
            // Label chip: dark slate, crimson underline, chamfered top-right, crosshair icon + 「点按跟随」 (white bold, AA on any scene).
            paint.SetStyle(Paint.Style.Fill); paint.TextSize = textSize; paint.FakeBoldText = true;
            const string label = "点按跟随";
            float chipH = Math.Min(20 * density, Height), icon = 12 * density, gap = 4 * density;
            float chipW = Math.Min(Width, 6 * density + icon + gap + paint.MeasureText(label) + 8 * density), cut = 7 * density;
            using (var chip = new global::Android.Graphics.Path())
            {
                chip.MoveTo(0, 0); chip.LineTo(chipW - cut, 0); chip.LineTo(chipW, cut); chip.LineTo(chipW, chipH); chip.LineTo(0, chipH); chip.Close();
                paint.Color = Color.Argb(235, 42, 49, 57); canvas.DrawPath(chip, paint);
            }
            paint.Color = red; canvas.DrawRect(0, chipH - 2 * density, chipW, chipH, paint);
            float ix = 6 * density + icon / 2, iy = (chipH - 2 * density) / 2;
            var pink = Color.ParseColor("#FF9AA8");
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, density); paint.Color = pink;
            canvas.DrawCircle(ix, iy, icon * 3 / 24f, paint);
            float a = icon * 9 / 24f, b2 = icon * 5 / 24f;
            canvas.DrawLine(ix, iy - a, ix, iy - b2, paint); canvas.DrawLine(ix, iy + b2, ix, iy + a, paint);
            canvas.DrawLine(ix - a, iy, ix - b2, iy, paint); canvas.DrawLine(ix + b2, iy, ix + a, iy, paint);
            paint.SetStyle(Paint.Style.Fill); paint.Color = Color.White;
            var fm = paint.GetFontMetrics()!;
            canvas.DrawText(label, 6 * density + icon + gap, iy - (fm.Ascent + fm.Descent) / 2, paint);
            paint.FakeBoldText = false;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e == null || gestures == null) return false;
            int count = e.PointerCount;
            int pointer = count > 0 ? e.GetPointerId(0) : -1;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    gestures.Begin(pointer, count, e.RawX, e.RawY, e.EventTime);
                    break;
                case MotionEventActions.Move:
                    // Android 可合并移动事件；检查历史点，防止划出后返回被误作轻点。
                    if (count == 1)
                    {
                        float offsetX = e.RawX - e.GetX(0), offsetY = e.RawY - e.GetY(0);
                        for (int i = 0; i < e.HistorySize; i++)
                            gestures.Move(pointer, count, e.GetHistoricalX(0, i) + offsetX, e.GetHistoricalY(0, i) + offsetY, e.GetHistoricalEventTime(i));
                    }
                    gestures.Move(pointer, count, e.RawX, e.RawY, e.EventTime);
                    break;
                case MotionEventActions.Up:
                    if (gestures.TryEnd(pointer, count, e.RawX, e.RawY, e.EventTime, out var point))
                    {
                        PerformClick(); tapped?.Invoke(point.X, point.Y);
                    }
                    break;
                case MotionEventActions.PointerDown:
                case MotionEventActions.PointerUp:
                case MotionEventActions.Cancel:
                case MotionEventActions.Outside:
                    gestures.Cancel();
                    break;
            }
            return true;
        }

        public override bool PerformClick() { base.PerformClick(); return true; }
        protected override void OnDetachedFromWindow() { gestures?.Cancel(); base.OnDetachedFromWindow(); }
        protected override void Dispose(bool disposing) { if (disposing) { gestures?.Cancel(); paint.Dispose(); } base.Dispose(disposing); }
    }
}
