using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 18-B common dialogs (Info / Confirm / Prompt / Choose). Portrait (and landscape &lt; 600dp): 96dp dark M title area with an
/// outlined English watermark + archive-paper body + dark bottom bar (primary = red). Wide landscape: 640dp, left 208dp dark
/// side title block + paper body with the buttons on the paper (primary = deep ink + red underline); height ≤ 384dp, body scrolls,
/// buttons stay. Eyebrows NOTICE / CONFIRM / INPUT / SELECT / DELETE (approved). Texts and callbacks unchanged.
/// </summary>
public sealed partial class MainActivity
{
    /// <summary>Keep = the key does not dismiss the dialog (e.g. 取消导入 while the import winds down).</summary>
    record struct DialogKey(string Text, Xb Kind, string? Icon, Action? Click, bool Keep = false);

    /// <param name="scrollBody">false for bodies that handle their own drags (region editor): no ScrollView, fixed height.</param>
    /// <param name="buttons">Receives the created key buttons in order (to relabel / disable them later).</param>
    AlertDialog PgrDialog(string eyebrow, string title, View body, DialogKey[] keys, Action? cancelled = null,
        bool cancelable = true, bool scrollBody = true, List<Button>? buttons = null, int fixedBodyHeight = 0)
    {
        float density = Resources!.DisplayMetrics!.Density;
        bool side = WideLandscape;
        int screenW = Resources.DisplayMetrics.WidthPixels, screenH = Resources.DisplayMetrics.HeightPixels;
        int totalWidth = side ? Math.Min(Dp(640), screenW - Dp(28)) : Math.Min(Dp(384), screenW - Dp(28));
        int headWidth = side ? Dp(208) : totalWidth;
        int paperWidth = side ? totalWidth - headWidth : totalWidth;
        AlertDialog? dialog = null;

        var shell = new LinearLayout(this) { Orientation = side ? Orientation.Horizontal : Orientation.Vertical };
        // Title block (dark M).
        var head = new FrameLayout(this)
        {
            Background = new DarkPanelDrawable(density, DecoTier.M)
            { CutTopRightDp = side ? 0 : 14, CutBottomRightDp = side ? 18 : 0, ShowEmblem = false, Inscription = false, Watermark = false, TideDp = side ? 28 : 16 }
        };
        var watermark = PgrUi.Outline(this, eyebrow, side ? 40 : 46, PgrTheme.A(Color.White, 46), 1f);
        watermark.ImportantForAccessibility = ImportantForAccessibility.No;
        head.AddView(watermark, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.End) { RightMargin = Dp(10), BottomMargin = Dp(side ? 30 : 8) });
        var headText = new LinearLayout(this) { Orientation = Orientation.Vertical };
        headText.SetPadding(Dp(18), Dp(16), Dp(16), Dp(14));
        var eb = new TextView(this) { Text = eyebrow, TextSize = 10 };
        eb.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); eb.LetterSpacing = .14f;
        eb.SetTextColor(eyebrow == "DELETE" ? Color.Argb(255, 255, 150, 160) : PgrArt.GoldLine);
        headText.AddView(eb);
        var t = new TextView(this) { Text = title, TextSize = 18 };
        t.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); t.SetTextColor(Color.White); t.SetMaxLines(side ? 5 : 3);
        t.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        headText.AddView(t, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        head.AddView(headText, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Top));
        if (side) shell.AddView(head, new LinearLayout.LayoutParams(headWidth, -1));
        else shell.AddView(head, new LinearLayout.LayoutParams(-1, -2) { Height = -2 });
        head.SetMinimumHeight(Dp(96));

        // Paper body + buttons.
        var paper = new LinearLayout(this) { Orientation = Orientation.Vertical };
        paper.Background = new ArchivePaperDrawable(density) { Fill = PgrTheme.Background, TideDp = side ? 20 : 0, EmblemWidthDp = 0, CutBottomRightDp = side ? 18 : 0 };
        var bodyBox = new FrameLayout(this);
        bodyBox.SetPadding(Dp(18), Dp(14), Dp(18), Dp(10));
        if (!scrollBody)
        {
            // Fixed-height body (its own touch handling); the dialog grows to fit up to the screen.
            int maxFixed = screenH - Dp(side ? 28 + 48 + 30 : 96 + 64 + 28);
            bodyBox.AddView(body, new FrameLayout.LayoutParams(-1, -1));
            paper.AddView(bodyBox, new LinearLayout.LayoutParams(-1, Math.Max(Dp(120), Math.Min(fixedBodyHeight + Dp(24), maxFixed))));
        }
        else
        {
            var scroll = new ScrollView(this) { FillViewport = false, VerticalFadingEdgeEnabled = true };
            scroll.SetFadingEdgeLength(Dp(24));
            bodyBox.AddView(body, new FrameLayout.LayoutParams(-1, -2));
            scroll.AddView(bodyBox, new ScrollView.LayoutParams(-1, -2));
            bodyBox.Measure(View.MeasureSpec.MakeMeasureSpec(paperWidth, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
            int maxBody = side ? Math.Min(Dp(384), screenH - Dp(28)) - Dp(48) - Dp(30) : (int)(screenH * .58f) - Dp(96) - Dp(64);
            int bodyHeight = Math.Max(Dp(48), Math.Min(bodyBox.MeasuredHeight, maxBody));
            paper.AddView(scroll, new LinearLayout.LayoutParams(-1, bodyHeight));
        }

        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        bar.SetGravity(GravityFlags.CenterVertical | GravityFlags.End);
        if (!side)
        {
            bar.Background = new DarkPanelDrawable(density, DecoTier.S) { CutBottomRightDp = 14, ShowEmblem = false, Inscription = false, Watermark = false, TideDp = 8 };
            bar.SetPadding(Dp(10), Dp(8), Dp(10), Dp(14));
        }
        else bar.SetPadding(Dp(14), Dp(4), Dp(14), Dp(16));
        foreach (var key in keys)
        {
            var b = new Button(this) { Text = key.Text };
            // Portrait dark bar: primary red, secondary white; wide landscape on paper: primary deep ink, secondary white outline.
            var kind = key.Kind == Xb.Primary && side ? Xb.Dark : key.Kind;
            PgrUi.Style(b, kind, key.Icon, center: true, textSp: 14);
            b.SetSingleLine(true); b.SetMinWidth(Dp(88)); b.SetMinimumWidth(Dp(88));
            var click = key.Click; bool keep = key.Keep;
            b.Click += (_, _) => { if (!keep) dialog?.Dismiss(); if (click != null) Safe(click); };
            buttons?.Add(b);
            bar.AddView(b, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(4), RightMargin = Dp(4) });
        }
        paper.AddView(bar, new LinearLayout.LayoutParams(-1, -2));
        if (side) shell.AddView(paper, new LinearLayout.LayoutParams(paperWidth, -2));
        else shell.AddView(paper, new LinearLayout.LayoutParams(-1, -2));

        dialog = new AlertDialog.Builder(this).SetView(shell)!.SetCancelable(cancelable)!.Create();
        if (cancelled != null) dialog.CancelEvent += (_, _) => Safe(cancelled);
        dialog.Show();
        dialog.Window?.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialog.Window?.SetLayout(totalWidth, ViewGroup.LayoutParams.WrapContent);
        return dialog;
    }

    TextView DialogMessage(string message)
    {
        var text = new TextView(this) { Text = message, TextSize = 15 };
        text.SetTextColor(PgrTheme.Foreground); text.SetLineSpacing(Dp(4), 1);
        return text;
    }

    void Info(string title, string message) =>
        PgrDialog("NOTICE", title, DialogMessage(message), new[] { new DialogKey("知道了", Xb.Primary, null, null) });

    void Confirm(string title, string message, Action action)
    {
        // 危险一级（应用内数据：删除书签 / 删除听书书签）= white with crimson outline + trash; file deletions use 12-A DangerConfirm.
        bool delete = title.StartsWith("删除");
        PgrDialog(delete ? "DELETE" : "CONFIRM", title, DialogMessage(message), new[]
        {
            new DialogKey("取消", Xb.Normal, "close", null),
            delete ? new DialogKey("确认", Xb.DangerOutline, "trash", action) : new DialogKey("确认", Xb.Primary, null, action)
        });
    }

    void Prompt(string title, string initial, Action<string> submit)
    {
        var edit = new EditText(this) { Text = initial }; PgrTheme.StyleInput(edit);
        edit.SetMinHeight(Dp(48)); edit.SetMinimumHeight(Dp(48));
        PgrDialog("INPUT", title, edit, new[]
        {
            new DialogKey("取消", Xb.Normal, "close", null),
            new DialogKey("保存", Xb.Primary, null, () => submit(edit.Text ?? ""))
        });
    }

    void Choose(string title, string[] items, Action<int> selected)
    {
        if (items.Length == 0) { Info(title, "当前没有可选内容。"); return; }
        AlertDialog? dialog = null;
        var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var adapter = new PgrTheme.ChoiceAdapter(this, items);
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            var row = adapter.GetView(i, null, list);
            row.Clickable = true; row.Focusable = true;
            row.Click += (_, _) => { dialog?.Dismiss(); Safe(() => selected(index)); };
            list.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(2) });
        }
        dialog = PgrDialog("SELECT", title, list, new[] { new DialogKey("取消", Xb.Normal, "close", null) });
    }
}
