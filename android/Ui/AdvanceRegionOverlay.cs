using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>在物理屏幕上取点，所有选择动作由本窗口消费，不透传给游戏。</summary>
public sealed class AdvanceRegionOverlay : IDisposable
{
    readonly IWindowManager windows;
    readonly FrameLayout root;
    readonly SelectionView selection;
    bool showing,disposed;
    public AdvanceRegionOverlay(Context context,int width,int height,Action<ScreenRegion?> completed)
    {
        windows=context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>();
        root=new FrameLayout(context);
        selection=new SelectionView(context,width,height);
        root.AddView(selection,new FrameLayout.LayoutParams(-1,-1));
        // 14-B: floating dark S card at the top left (clear of NEXT / dialogue at the lower right); readout of the box size.
        var panel=new LinearLayout(context){Orientation=Orientation.Vertical};
        panel.SetPadding(PgrTheme.Dp(context,14),PgrTheme.Dp(context,10),PgrTheme.Dp(context,14),PgrTheme.Dp(context,16));
        panel.Background=new DarkPanelDrawable(context.Resources!.DisplayMetrics!.Density,DecoTier.S){CutBottomRightDp=14,ShowEmblem=false,Inscription=false,Watermark=false};
        var code=PgrTheme.Eyebrow(context,"ADVANCE AREA");code.ImportantForAccessibility=ImportantForAccessibility.No;code.SetTextColor(PgrArt.GoldLine);panel.AddView(code);
        var title=new TextView(context){Text="拖出游戏“下一句”的触碰范围",TextSize=15};
        title.SetTextColor(Color.White);title.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);panel.AddView(title);
        var help=new TextView(context){Text="点按跟随：你点框内哪里就触碰哪里。自动播放：点击框中心。避开选项和系统栏。",TextSize=13};
        help.SetTextColor(PgrTheme.A(Color.White,215));help.SetLineSpacing(PgrTheme.Dp(context,3),1);panel.AddView(help);
        var readout=new TextView(context){TextSize=12,Visibility=ViewStates.Gone};readout.SetTextColor(PgrArt.GoldLine);PgrTheme.Tabular(readout);
        readout.SetPadding(0,PgrTheme.Dp(context,4),0,0);panel.AddView(readout);
        selection.Changed=r=>{readout.Visibility=r==null?ViewStates.Gone:ViewStates.Visible;if(r!=null)readout.Text=$"框中心 = 自动播放点击位置 · {r.Width*100:0}% × {r.Height*100:0}%";};
        var row=new LinearLayout(context){Orientation=Orientation.Horizontal};panel.AddView(row);
        var save=new Button(context){Text="保存区域"};var cancel=new Button(context){Text="取消"};
        PgrTheme.StyleButton(save,primary:true);PgrTheme.StyleButton(cancel);
        PgrUi.Style(save,Xb.Primary,"bookmark",center:true);PgrUi.Style(cancel,Xb.Dark,"close",center:true);
        row.AddView(save,new LinearLayout.LayoutParams(0,PgrTheme.Dp(context,48),1){RightMargin=PgrTheme.Dp(context,4),TopMargin=PgrTheme.Dp(context,8)});
        row.AddView(cancel,new LinearLayout.LayoutParams(0,PgrTheme.Dp(context,48),1){LeftMargin=PgrTheme.Dp(context,4),TopMargin=PgrTheme.Dp(context,8)});
        save.Click+=(_,_)=>{if(selection.Region is { } region){Dispose();completed(region);}else{help.Text="请先在游戏的下一句区域拖出一个方框。";help.SetTextColor(Color.Argb(255,255,170,180));}};
        cancel.Click+=(_,_)=>{Dispose();completed(null);};
        int cardWidth=Math.Min(PgrTheme.Dp(context,400),(int)(width*.48f));
        root.AddView(panel,new FrameLayout.LayoutParams(cardWidth,-2,GravityFlags.Top|GravityFlags.Left){LeftMargin=PgrTheme.Dp(context,40),TopMargin=PgrTheme.Dp(context,28)});
    }
    public void Show()
    {
        var p=new WindowManagerLayoutParams(-1,-1,WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable|WindowManagerFlags.LayoutInScreen,Format.Translucent){Gravity=GravityFlags.Top|GravityFlags.Left};
        windows.AddView(root,p);showing=true;
    }
    public void Dispose(){if(disposed)return;disposed=true;try{if(showing)windows.RemoveView(root);}finally{showing=false;selection.Dispose();root.Dispose();}}

    [Register("cn/pgrvoice/player/AdvanceSelectionView")]
    public sealed class SelectionView : View
    {
        readonly Paint paint=new(PaintFlags.AntiAlias);
        readonly int displayWidth,displayHeight;
        float startX,startY;
        public ScreenRegion? Region{get;private set;}
        public Action<ScreenRegion?>? Changed;
        public SelectionView(Context context,int width,int height):base(context){displayWidth=width;displayHeight=height;}
        public SelectionView(IntPtr handle,JniHandleOwnership ownership):base(handle,ownership){}
        protected override void OnDraw(Canvas canvas)
        {
            // 14-B: dim outside the box at 42 % (whole screen while nothing is drawn); rulers only on the screen's top / left edges.
            var dim=Color.Argb(107,0x0E,0x12,0x18);
            if(Region is not { } r){canvas.DrawColor(dim);return;}
            int[] pos=new int[2];GetLocationOnScreen(pos);
            using var bounds=new RectF(r.Left*displayWidth-pos[0],r.Top*displayHeight-pos[1],(r.Left+r.Width)*displayWidth-pos[0],(r.Top+r.Height)*displayHeight-pos[1]);
            float dp=Resources!.DisplayMetrics!.Density;
            paint.Color=dim;paint.SetStyle(Paint.Style.Fill);
            canvas.DrawRect(0,0,Width,bounds.Top,paint);canvas.DrawRect(0,bounds.Bottom,Width,Height,paint);
            canvas.DrawRect(0,bounds.Top,bounds.Left,bounds.Bottom,paint);canvas.DrawRect(bounds.Right,bounds.Top,Width,bounds.Bottom,paint);
            paint.Color=PgrArt.GoldLine;paint.StrokeWidth=Math.Max(2,1.5f*dp);
            canvas.DrawLine(bounds.Left,0,bounds.Left,10*dp,paint);canvas.DrawLine(bounds.Right,0,bounds.Right,10*dp,paint);
            canvas.DrawLine(0,bounds.Top,10*dp,bounds.Top,paint);canvas.DrawLine(0,bounds.Bottom,10*dp,bounds.Bottom,paint);
            paint.StrokeWidth=Math.Max(1,dp);paint.Color=PgrTheme.A(PgrArt.GoldLine,140);
            canvas.DrawLine(bounds.Left,5*dp,bounds.Right,5*dp,paint);canvas.DrawLine(5*dp,bounds.Top,5*dp,bounds.Bottom,paint);
            paint.Color=Color.Argb(36,PgrTheme.Red.R,PgrTheme.Red.G,PgrTheme.Red.B);paint.SetStyle(Paint.Style.Fill);canvas.DrawRect(bounds,paint);
            paint.Color=PgrTheme.Red;paint.SetStyle(Paint.Style.Stroke);paint.StrokeWidth=Math.Max(3,2*dp);
            canvas.DrawRect(bounds,paint);
            // Centre crosshair = the point auto play taps.
            float cx=bounds.CenterX(),cy=bounds.CenterY(),arm=9*dp;
            paint.Color=PgrTheme.Foreground;paint.StrokeWidth=Math.Max(2,1.5f*dp);
            canvas.DrawLine(cx-arm,cy,cx-3*dp,cy,paint);canvas.DrawLine(cx+3*dp,cy,cx+arm,cy,paint);
            canvas.DrawLine(cx,cy-arm,cx,cy-3*dp,paint);canvas.DrawLine(cx,cy+3*dp,cx,cy+arm,paint);
            paint.Color=PgrTheme.Red;canvas.DrawCircle(cx,cy,Math.Max(9,6*dp),paint);
        }
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if(e==null||displayWidth<=0||displayHeight<=0)return false;
            float x=Math.Clamp(e.RawX/displayWidth,0,1),y=Math.Clamp(e.RawY/displayHeight,0,1);
            if(e.Action==MotionEventActions.Down){startX=x;startY=y;return true;}
            if(e.Action is MotionEventActions.Move or MotionEventActions.Up)
            {
                float w=Math.Abs(x-startX),h=Math.Abs(y-startY);
                Region=w*displayWidth>=12&&h*displayHeight>=12?new(Math.Min(x,startX),Math.Min(y,startY),w,h):null;
                Invalidate();Changed?.Invoke(Region);if(e.Action==MotionEventActions.Up)PerformClick();
            }
            return true;
        }
        public override bool PerformClick(){base.PerformClick();return true;}
        protected override void Dispose(bool disposing){if(disposing)paint.Dispose();base.Dispose(disposing);}
    }
}
