using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>在游戏上方编辑已捕获的字幕截图，不切换前台应用；持有并统一释放截图。</summary>
public sealed class SubtitleRegionOverlay : IDisposable
{
    readonly Context context;
    readonly Bitmap bitmap;
    readonly ScreenRegion initial;
    IWindowManager? windows;
    LinearLayout? root;
    RegionEditor? editor;
    bool showing, disposed;
    public event Action<ScreenRegion>? Saved;
    public event Action? Closed;

    public SubtitleRegionOverlay(Context context, Bitmap bitmap, ScreenRegion initial)
    {
        this.context = context;
        this.bitmap = bitmap;
        this.initial = initial;
    }

    public void Show()
    {
        if (disposed || showing) return;
        windows = (context.ApplicationContext ?? context).GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>();
        var metrics = context.Resources!.DisplayMetrics!;
        float density = metrics.Density;
        bool side = metrics.WidthPixels > metrics.HeightPixels;
        // 14-B: left = screenshot + 框内放大 strip; right = 300dp dark M calibration column (title, readout, actions).
        root = new LinearLayout(context) { Orientation = side ? Orientation.Horizontal : Orientation.Vertical, Clickable = true };
        int padding = PgrTheme.Dp(context, 12);
        root.SetPadding(padding, padding, padding, padding);
        root.Background = new HudBackdropDrawable(density);
        var shotColumn = new LinearLayout(context) { Orientation = Orientation.Vertical };
        editor = new RegionEditor(context, bitmap, initial);
        // RegionEditor also serves a dialog; here the weighted area must fit short landscape screens.
        editor.SetMinimumHeight(0);
        shotColumn.AddView(editor, new LinearLayout.LayoutParams(-1, 0, 1));
        var zoomBand = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        zoomBand.SetGravity(GravityFlags.CenterVertical);
        zoomBand.Background = PgrTheme.Plate(context, PgrTheme.GlassStrong, PgrTheme.GlassLine, PgrTheme.CutSmall, Corners.BottomRight);
        zoomBand.SetPadding(PgrTheme.Dp(context, 8), PgrTheme.Dp(context, 4), PgrTheme.Dp(context, 8), PgrTheme.Dp(context, 4));
        var zoomLabel = new TextView(context) { Text = "框内放大 · 识别会读到的画面", TextSize = 11 };
        zoomLabel.SetTextColor(PgrTheme.Secondary); zoomLabel.SetMaxLines(2);
        zoomBand.AddView(zoomLabel, new LinearLayout.LayoutParams(PgrTheme.Dp(context, 96), -2));
        var zoom = new RegionZoomView(context, editor) { ImportantForAccessibility = ImportantForAccessibility.No };
        zoomBand.AddView(zoom, new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = PgrTheme.Dp(context, 8) });
        shotColumn.AddView(zoomBand, new LinearLayout.LayoutParams(-1, PgrTheme.Dp(context, side ? 64 : 56)) { TopMargin = PgrTheme.Dp(context, 6) });

        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };
        column.SetPadding(PgrTheme.Dp(context, 14), PgrTheme.Dp(context, 12), PgrTheme.Dp(context, 14), PgrTheme.Dp(context, 24));
        column.Background = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 14, ShowEmblem = false };
        var code = PgrTheme.Eyebrow(context, "SUBTITLE REGION");
        code.ImportantForAccessibility = ImportantForAccessibility.No; code.SetTextColor(PgrArt.GoldLine);
        column.AddView(code, new LinearLayout.LayoutParams(-1, -2));
        var title = new TextView(context)
        {
            Text = "框选角色名和完整对白",
            TextSize = 16,
            ContentDescription = "在截图上拖动以设置字幕识别范围"
        };
        title.SetTextColor(Color.White);
        title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        column.AddView(title, new LinearLayout.LayoutParams(-1, -2));
        var hint = new TextView(context) { Text = "避开 NEXT 和波形。保存前请保持游戏屏幕方向不变。", TextSize = 12 };
        hint.SetTextColor(PgrTheme.A(Color.White, 205));
        hint.SetPadding(0, PgrTheme.Dp(context, 4), 0, PgrTheme.Dp(context, 8));
        column.AddView(hint, new LinearLayout.LayoutParams(-1, -2));
        var readHead = new TextView(context) { Text = "当前范围 · REGION", TextSize = 10 };
        readHead.SetTextColor(PgrArt.GoldLine); readHead.LetterSpacing = .08f; readHead.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold);
        column.AddView(readHead);
        var grid = new GridLayout(context) { ColumnCount = 2 };
        var values = new TextView[4];
        string[] names = { "左 LEFT", "上 TOP", "宽 WIDTH", "高 HEIGHT" };
        for (int i = 0; i < 4; i++)
        {
            var cell = new LinearLayout(context) { Orientation = Orientation.Vertical };
            var n = new TextView(context) { Text = names[i], TextSize = 10 }; n.SetTextColor(PgrTheme.A(Color.White, 170));
            values[i] = new TextView(context) { TextSize = 16 }; values[i].SetTextColor(Color.White); PgrTheme.Tabular(values[i]);
            values[i].SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold);
            cell.AddView(n); cell.AddView(values[i]);
            grid.AddView(cell, new GridLayout.LayoutParams(GridLayout.InvokeSpec(i / 2), GridLayout.InvokeSpec(i % 2, 1f)) { Width = 0, TopMargin = PgrTheme.Dp(context, 4) });
        }
        column.AddView(grid, new LinearLayout.LayoutParams(-1, -2));
        var valid = new TextView(context) { Text = "范围有效 · 最小宽、高 5%", TextSize = 11 };
        valid.SetTextColor(PgrArt.MistLine); valid.SetPadding(0, PgrTheme.Dp(context, 6), 0, 0);
        column.AddView(valid);
        void Refresh()
        {
            var r = editor!.Region;
            values[0].Text = $"{r.Left * 100:0.0}%"; values[1].Text = $"{r.Top * 100:0.0}%";
            values[2].Text = $"{r.Width * 100:0.0}%"; values[3].Text = $"{r.Height * 100:0.0}%";
            zoom.Invalidate();
        }
        editor.Changed += Refresh; Refresh();
        column.AddView(new Space(context), new LinearLayout.LayoutParams(-1, 0, 1));
        var cancel = new Button(context) { Text = "取消", TextSize = 14 };
        PgrUi.Style(cancel, Xb.Dark, "close", center: true);
        cancel.Click += (_, _) => Dispose();
        var save = new Button(context) { Text = "保存字幕区域", TextSize = 14 };
        PgrUi.Style(save, Xb.Primary, "bookmark", center: true);
        save.Click += (_, _) => Save();
        int buttonHeight = PgrTheme.Dp(context, 48);
        if (side)
        {
            column.AddView(save, new LinearLayout.LayoutParams(-1, buttonHeight) { TopMargin = PgrTheme.Dp(context, 8) });
            column.AddView(cancel, new LinearLayout.LayoutParams(-1, buttonHeight) { TopMargin = PgrTheme.Dp(context, 6) });
            root.AddView(shotColumn, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = PgrTheme.Dp(context, 10) });
            root.AddView(column, new LinearLayout.LayoutParams(PgrTheme.Dp(context, 300), -1));
        }
        else
        {
            var actions = new LinearLayout(context) { Orientation = Orientation.Horizontal };
            actions.AddView(cancel, new LinearLayout.LayoutParams(0, buttonHeight, 1) { RightMargin = PgrTheme.Dp(context, 5) });
            actions.AddView(save, new LinearLayout.LayoutParams(0, buttonHeight, 1) { LeftMargin = PgrTheme.Dp(context, 5) });
            column.AddView(actions, new LinearLayout.LayoutParams(-1, -2) { TopMargin = PgrTheme.Dp(context, 8) });
            root.AddView(column, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = PgrTheme.Dp(context, 8) });
            root.AddView(shotColumn, new LinearLayout.LayoutParams(-1, 0, 1));
        }

        // The full window consumes touches, including blank areas; system bars remain outside it.
        // NotFocusable keeps the game as the focused application and does not request accessibility.
        var parameters = new WindowManagerLayoutParams(-1, -1, WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable, Format.Translucent) { Gravity = GravityFlags.Top | GravityFlags.Left };
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P) parameters.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Never;
        windows.AddView(root, parameters);
        showing = true;
    }

    void Save()
    {
        if (disposed || editor == null) return;
        var region = editor.Region;
        var saved = Saved;
        // Close first so session validation messages in the normal controls remain visible afterwards.
        Dispose();
        saved?.Invoke(region);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (root != null && (showing || root.IsAttachedToWindow)) windows?.RemoveViewImmediate(root);
        }
        catch (Java.Lang.IllegalArgumentException) { }
        finally
        {
            showing = false;
            root?.RemoveAllViews();
            editor?.Dispose(); editor = null;
            bitmap.Dispose();
            root?.Dispose(); root = null;
            var closed = Closed;
            Saved = null; Closed = null;
            closed?.Invoke();
        }
    }
}
