using Android.Content;
using Android.Graphics;
using Android.Views;

namespace PgrVoice.AndroidApp.Ui;

// 用户在当前捕获画面上拖出字幕框；编辑器不保留截图到磁盘。
public sealed class RegionEditor : View
{
    readonly Bitmap bitmap;
    readonly Paint paint = new(PaintFlags.AntiAlias);
    RectF displayed = new();
    ScreenRegion region;
    float startX, startY;
    public ScreenRegion Region => region.Clamp();
    public Bitmap Source => bitmap;
    /// <summary>14-B readout / zoom strip refresh (display only).</summary>
    public event Action? Changed;
    public RegionEditor(Context context, Bitmap bitmap, ScreenRegion initial) : base(context)
    { this.bitmap = bitmap; region = initial; SetMinimumHeight(320); }
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float scale = Math.Min(Width / (float)bitmap.Width, Height / (float)bitmap.Height);
        float w = bitmap.Width * scale, h = bitmap.Height * scale;
        displayed.Set((Width-w)/2,(Height-h)/2,(Width+w)/2,(Height+h)/2);
        canvas.DrawColor(PgrTheme.Background);
        canvas.DrawBitmap(bitmap, null, displayed, null);
        float l = displayed.Left+region.Left*w, t = displayed.Top+region.Top*h;
        float r = displayed.Left+(region.Left+region.Width)*w, b = displayed.Top+(region.Top+region.Height)*h;
        // 14-B: dim only outside the chosen box (42 %); the screenshot inside stays untouched.
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = Color.Argb(107, 0x0E, 0x12, 0x18);
        canvas.DrawRect(displayed.Left, displayed.Top, displayed.Right, t, paint);
        canvas.DrawRect(displayed.Left, b, displayed.Right, displayed.Bottom, paint);
        canvas.DrawRect(displayed.Left, t, l, b, paint);
        canvas.DrawRect(r, t, displayed.Right, b, paint);
        float dp = PgrTheme.Dp(Context!, 1);
        paint.Color = PgrTheme.Red; paint.StrokeWidth=2*dp; paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawRect(l,t,r,b,paint);
        // HUD corner brackets as visual handles.
        float arm = Math.Min(12*dp, Math.Min(r-l, b-t)/2);
        paint.Color = PgrTheme.Foreground; paint.StrokeWidth = 3*dp; paint.StrokeCap = Paint.Cap.Square;
        canvas.DrawLine(l,t,l+arm,t,paint); canvas.DrawLine(l,t,l,t+arm,paint);
        canvas.DrawLine(r,t,r-arm,t,paint); canvas.DrawLine(r,t,r,t+arm,paint);
        canvas.DrawLine(l,b,l+arm,b,paint); canvas.DrawLine(l,b,l,b-arm,paint);
        canvas.DrawLine(r,b,r-arm,b,paint); canvas.DrawLine(r,b,r,b-arm,paint);
    }
    public override bool OnTouchEvent(MotionEvent? e)
    {
        if(e==null || displayed.Width()<=0)return false;
        float x=Math.Clamp((e.GetX()-displayed.Left)/displayed.Width(),0,1);
        float y=Math.Clamp((e.GetY()-displayed.Top)/displayed.Height(),0,1);
        if(e.Action==MotionEventActions.Down){startX=x;startY=y;Parent?.RequestDisallowInterceptTouchEvent(true);return true;}
        if(e.Action is MotionEventActions.Move or MotionEventActions.Up)
        {region=new(Math.Min(startX,x),Math.Min(startY,y),Math.Max(.05f,Math.Abs(x-startX)),Math.Max(.05f,Math.Abs(y-startY)));Invalidate();Changed?.Invoke();if(e.Action==MotionEventActions.Up)PerformClick();return true;}
        return true;
    }
    public override bool PerformClick(){base.PerformClick();return true;}
    protected override void Dispose(bool disposing){if(disposing)paint.Dispose();base.Dispose(disposing);}
}


/// <summary>14-B 框内放大：the part of the screenshot that OCR will read, scaled up (display only).</summary>
public sealed class RegionZoomView(Context context, RegionEditor editor) : View(context)
{
    readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var bitmap = editor.Source; var r = editor.Region;
        if (bitmap.IsRecycled) return;
        using var src = new Rect((int)(r.Left * bitmap.Width), (int)(r.Top * bitmap.Height),
            (int)((r.Left + r.Width) * bitmap.Width), (int)((r.Top + r.Height) * bitmap.Height));
        if (src.Width() <= 0 || src.Height() <= 0) return;
        float scale = Math.Min(Width / (float)src.Width(), Height / (float)src.Height());
        float w = src.Width() * scale, h = src.Height() * scale;
        using var dst = new RectF(0, (Height - h) / 2, w, (Height + h) / 2);
        canvas.DrawBitmap(bitmap, src, dst, paint);
        paint.Color = PgrTheme.Red; paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Resources!.DisplayMetrics!.Density;
        canvas.DrawRect(dst, paint); paint.SetStyle(Paint.Style.Fill);
    }
    protected override void Dispose(bool disposing){if(disposing)paint.Dispose();base.Dispose(disposing);}
}
