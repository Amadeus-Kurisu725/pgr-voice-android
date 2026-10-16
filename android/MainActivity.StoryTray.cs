using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 05-B playback tray (story page only): the shared current-line card, 「确认当前句」 and the story Controls merged into a fixed
/// dark M tray above the tabs, with a collapse / expand handle (remembered in Settings.StoryTrayCollapsed). Wide landscape:
/// right 384dp L column (section, cast, 05L-1 SECTION PROGRESS rail, tray) with a left-edge 「收起」 handle, or a 78dp strip.
/// Every key calls the same commands as before; collapsing only changes what is shown.
/// </summary>
public sealed partial class MainActivity
{
    sealed class TrayLine
    {
        public TextView Meta = null!, Line = null!; public TextView? Status, Speaker, VoiceTag;
        public View Portrait = null!; public PortraitFrameDrawable Frame = null!; public string? Shown;
    }
    readonly List<TrayLine> trayLines = new();
    FrameLayout storyTrayHost = null!, storyColumn = null!;
    LinearLayout storyTrayExpanded = null!, storyTrayCollapsed = null!, storyColumnPanel = null!, storyColumnStrip = null!;
    View storyTrayHandle = null!; TextView storyTrayHandleText = null!, storyTrayHandleEn = null!; ImageView storyTrayHandleIcon = null!;
    TextView storyColumnEyebrow = null!, storyColumnSection = null!, storyColumnSource = null!, storyColumnCount = null!, storyColumnReadout = null!;
    FrameLayout storyColumnCast = null!; View storyColumnRail = null!; View storyColumnEdge = null!;
    string? storyTrayKey, storyCastKey;

    void CreateStoryTray()
    {
        float density = Resources!.DisplayMetrics!.Density;
        // Portrait: host overlaps the page scroll by the handle's transparent hit area.
        storyTrayHost = new FrameLayout(this) { Visibility = ViewStates.Gone };
        var body = new FrameLayout(this);
        storyTrayExpanded = BuildTrayExpanded(false);
        storyTrayCollapsed = BuildTrayCollapsed();
        body.AddView(storyTrayExpanded, new FrameLayout.LayoutParams(-1, -2));
        body.AddView(storyTrayCollapsed, new FrameLayout.LayoutParams(-1, Dp(68)));
        locateTrayExpanded = BuildLocateExpanded(false);
        body.AddView(locateTrayExpanded, new FrameLayout.LayoutParams(-1, -2));
        storyTrayHost.AddView(body, new FrameLayout.LayoutParams(-1, -2) { TopMargin = Dp(48) });
        var handle = new FrameLayout(this) { Clickable = true, Focusable = true, Background = new TrayHandleDrawable(density) };
        var handleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        handleRow.SetGravity(GravityFlags.Center);
        storyTrayHandleIcon = Icon("chevron_down", PgrTheme.OnPlate, 12);
        handleRow.AddView(storyTrayHandleIcon, new LinearLayout.LayoutParams(Dp(12), Dp(12)) { RightMargin = Dp(5) });
        storyTrayHandleText = new TextView(this) { Text = "收起", TextSize = 11 }; storyTrayHandleText.SetTextColor(Color.White); storyTrayHandleText.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        handleRow.AddView(storyTrayHandleText);
        storyTrayHandleEn = PgrUi.En(this, "COLLAPSE", 7.5f, PgrTheme.NavSub);
        handleRow.AddView(storyTrayHandleEn, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(5) });
        handle.AddView(handleRow, new FrameLayout.LayoutParams(-1, Dp(22), GravityFlags.Bottom));
        handle.Click += (_, _) => ToggleStoryTray();
        storyTrayHandle = handle;
        storyTrayHost.AddView(handle, new FrameLayout.LayoutParams(Dp(120), Dp(48), GravityFlags.Top | GravityFlags.CenterHorizontal));
        storyTrayHost.SetClipChildren(false);
        int at = root.IndexOfChild(tabDivider);
        root.AddView(storyTrayHost, at, new LinearLayout.LayoutParams(-1, -2) { TopMargin = -Dp(26) });

        // Wide landscape column.
        storyColumn = new FrameLayout(this) { Visibility = ViewStates.Gone };
        storyColumnPanel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        storyColumnPanel.Background = new DarkPanelDrawable(density, DecoTier.L) { Mark = "STORY", TideDp = 40, EmblemWidthDp = 170, EmblemRightDp = -30, EmblemBottomDp = 150 };
        storyColumnPanel.SetPadding(Dp(22), Dp(14), Dp(14), Dp(24));
        storyColumnEyebrow = PgrUi.En(this, "", 9.5f, PgrArt.GoldLine); storyColumnPanel.AddView(storyColumnEyebrow);
        storyColumnSection = new TextView(this) { TextSize = 19 }; storyColumnSection.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); storyColumnSection.SetTextColor(Color.White);
        storyColumnSection.SetSingleLine(true); storyColumnSection.Ellipsize = TextUtils.TruncateAt.End; storyColumnPanel.AddView(storyColumnSection, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        storyColumnSource = new TextView(this) { TextSize = 12 }; storyColumnSource.SetTextColor(PgrTheme.NavText); storyColumnPanel.AddView(storyColumnSource);
        storyColumnCast = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        storyColumnPanel.AddView(storyColumnCast, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8), BottomMargin = Dp(8) });
        var progressHead = new LinearLayout(this) { Orientation = Orientation.Horizontal }; progressHead.SetGravity(GravityFlags.CenterVertical);
        storyColumnCount = new TextView(this) { TextSize = 12 }; storyColumnCount.SetTextColor(Color.White); storyColumnCount.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); PgrTheme.Tabular(storyColumnCount);
        progressHead.AddView(storyColumnCount);
        progressHead.AddView(PgrUi.En(this, "SECTION PROGRESS", 8.5f, PgrTheme.NavSub), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(8) });
        storyColumnReadout = new TextView(this) { TextSize = 15 }; storyColumnReadout.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); storyColumnReadout.SetTextColor(Color.White); PgrTheme.Tabular(storyColumnReadout);
        progressHead.AddView(storyColumnReadout);
        storyColumnPanel.AddView(progressHead);
        storyColumnRail = new View(this) { Background = new SectionRailDrawable(density) };
        storyColumnPanel.AddView(storyColumnRail, new LinearLayout.LayoutParams(-1, Dp(34)) { TopMargin = Dp(4) });
        storyColumnPanel.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
        var columnTray = BuildTrayExpanded(true);
        storyColumnPanel.AddView(columnTray, new LinearLayout.LayoutParams(-1, -2));
        storyColumn.AddView(storyColumnPanel, new FrameLayout.LayoutParams(-1, -1) { LeftMargin = Dp(18) });
        locateColumnPanel = BuildLocateColumnPanel();
        storyColumn.AddView(locateColumnPanel, new FrameLayout.LayoutParams(-1, -1) { LeftMargin = Dp(18) });
        var edge = new FrameLayout(this) { Clickable = true, Focusable = true, ContentDescription = "收起播放托盘", Background = new EdgeHandleDrawable(density) };
        var edgeLabel = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        edgeLabel.SetGravity(GravityFlags.Center);
        edgeLabel.AddView(Icon("chevron_right", Color.White, 12), new LinearLayout.LayoutParams(Dp(12), Dp(12)) { BottomMargin = Dp(3) });
        var edgeText = new TextView(this) { Text = "收\n起", TextSize = 10, Gravity = GravityFlags.Center }; edgeText.SetTextColor(Color.White); edgeText.SetLineSpacing(0, .9f);
        edgeLabel.AddView(edgeText);
        edge.AddView(edgeLabel, new FrameLayout.LayoutParams(Dp(20), -1, GravityFlags.Start));
        edge.Click += (_, _) => ToggleStoryTray();
        storyColumnEdge = edge;
        storyColumn.AddView(edge, new FrameLayout.LayoutParams(Dp(48), Dp(96), GravityFlags.Start | GravityFlags.CenterVertical));
        storyColumnStrip = BuildTrayStrip();
        storyColumn.AddView(storyColumnStrip, new FrameLayout.LayoutParams(-1, -1));
        shell.AddView(storyColumn, new LinearLayout.LayoutParams(Dp(384), -1));
    }

    TrayLine NewTrayLine(int portraitDp)
    {
        var t = new TrayLine();
        t.Frame = new PortraitFrameDrawable(Resources!.DisplayMetrics!.Density) { ShowSerial = false };
        t.Portrait = new View(this) { Background = t.Frame, ImportantForAccessibility = ImportantForAccessibility.No };
        t.Meta = new TextView(this) { TextSize = 11 }; t.Meta.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); t.Meta.SetSingleLine(true); t.Meta.Ellipsize = TextUtils.TruncateAt.End;
        t.Line = new TextView(this) { TextSize = 15 }; t.Line.SetTextColor(Color.White); t.Line.SetLineSpacing(Dp(2), 1); t.Line.Ellipsize = TextUtils.TruncateAt.End;
        trayLines.Add(t);
        return t;
    }

    LinearLayout CurrentLineBlock(TrayLine t, int portraitDp, bool withStatus)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        row.ContentDescription = "当前台词和配音状态，点击查看完整内容";
        row.Click += (_, _) => Safe(CurrentCardClick);
        row.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 30)), null, new ColorDrawable(Color.White));
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(t.Portrait, new LinearLayout.LayoutParams(Dp(portraitDp), Dp(portraitDp)) { RightMargin = Dp(10) });
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        // 0.4.7（J/M）：当前句没有可用音频时，meta 行右侧显示「无配音 / 待配音」发丝签。
        var metaRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; metaRow.SetGravity(GravityFlags.CenterVertical);
        metaRow.AddView(t.Meta, new LinearLayout.LayoutParams(0, -2, 1));
        t.VoiceTag = StopVisuals.VoiceTag(this, false, true); t.VoiceTag.Visibility = ViewStates.Gone;
        metaRow.AddView(t.VoiceTag, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
        texts.AddView(metaRow);
        t.Line.SetMaxLines(withStatus ? 2 : 1); texts.AddView(t.Line, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(1) });
        if (withStatus)
        {
            t.Status = new TextView(this) { TextSize = 11 }; t.Status.SetTextColor(PgrTheme.NavText); t.Status.SetSingleLine(true); t.Status.Ellipsize = TextUtils.TruncateAt.End;
            texts.AddView(t.Status, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        }
        row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        return row;
    }

    void CurrentCardClick()
    {
        if (session.HasSettingsSaveWarning) Info("设置尚未保存", session.Status);
        else if (session.Listening.IsPlaying) ShowPage(5);
        else Info("当前配音状态", (session.Engine?.Current is { } currentNode ? AppSession.BrowseSpeaker(currentNode) + "：" + currentNode.Text + "\n\n" : "") + session.Status);
    }

    Button ConfirmHex(bool vertical = false)
    {
        var b = new Button(this) { Text = vertical ? "确\n认\n当\n前\n句" : "确认当前句", TextSize = vertical ? 14 : 16 };
        b.ContentDescription = "确认当前句";
        b.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 50)), new HexButtonDrawable(Resources!.DisplayMetrics!.Density, vertical), null);
        b.BackgroundTintList = null; b.SetTextColor(Color.White); b.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); b.SetAllCaps(false); b.StateListAnimator = null; b.Elevation = 0;
        b.SetMinHeight(Dp(48)); b.SetMinimumHeight(Dp(48)); b.SetMinWidth(0); b.SetMinimumWidth(0);
        b.SetPadding(Dp(vertical ? 4 : 18), Dp(vertical ? 18 : 4), Dp(vertical ? 4 : 18), Dp(vertical ? 18 : 4));
        b.SetLineSpacing(0, vertical ? .95f : 1f);
        if (vertical)
        {
            // 0.4.6: the 140dp strip key holds five stacked glyphs; at larger font scales trim the 18dp end padding
            // (≥4dp), then the size, instead of cutting 「句」. Font 1.0 keeps the 0.4.3 metrics.
            var fm = b.Paint!.GetFontMetrics()!; float avail = Dp(140);
            float need = (fm.Descent - fm.Ascent) * .95f * 4 + (fm.Bottom - fm.Top);
            if (need + 2 * Dp(18) > avail)
            {
                if (need + 2 * Dp(4) > avail) { b.SetTextSize(global::Android.Util.ComplexUnitType.Px, b.TextSize * (avail - 2 * Dp(4)) / need); need = avail - 2 * Dp(4); }
                int pad = (int)Math.Max(Dp(4), (avail - need) / 2); b.SetPadding(Dp(4), pad, Dp(4), pad);
            }
        }
        if (!vertical)
        {
            b.SetCompoundDrawablesRelativeWithIntrinsicBounds(new PgrIconDrawable("play", Color.White, Resources!.DisplayMetrics!.Density, 14), null, null, null); b.CompoundDrawablePadding = Dp(8);
            // ~731dp landscape column: drop the play glyph / trim padding / step down to 13sp so 「确认当前句」 stays on one line.
            FitSingleLine(b, 13f, 12f, () => (b.Text?.Length ?? 0) > 5 ? 14f : 16f);
        }
        b.Click += (_, _) => Safe(ConfirmFooterPosition);
        confirmHexes.Add((b, vertical));
        return b;
    }

    View DiamondKey(string icon, string label, Action action)
    {
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, ContentDescription = label };
        col.SetGravity(GravityFlags.CenterHorizontal);
        col.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 30)), null, new ColorDrawable(Color.White));
        var key = new FrameLayout(this) { Background = new DiamondKeyDrawable(Resources!.DisplayMetrics!.Density), DuplicateParentStateEnabled = true, ImportantForAccessibility = ImportantForAccessibility.No };
        key.AddView(Icon(icon, Color.White, 16), new FrameLayout.LayoutParams(Dp(16), Dp(16), GravityFlags.Center));
        col.AddView(key, new LinearLayout.LayoutParams(Dp(38), Dp(38)));
        var text = new TextView(this) { Text = label, TextSize = 11, ImportantForAccessibility = ImportantForAccessibility.No }; text.SetTextColor(PgrTheme.OnPlate);
        col.AddView(text);
        col.Click += (_, _) => Safe(action);
        col.SetMinimumWidth(Dp(56)); col.SetMinimumHeight(Dp(56));
        return col;
    }

    LinearLayout BuildTrayExpanded(bool column)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var tray = new LinearLayout(this) { Orientation = Orientation.Vertical };
        if (!column)
        {
            tray.Background = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 16, TideDp = 22, Inscription = false, Watermark = false, ShowEmblem = false };
            tray.SetPadding(Dp(14), Dp(8), Dp(14), Dp(8));
        }
        var t = NewTrayLine(48);
        tray.AddView(CurrentLineBlock(t, 44, true), new LinearLayout.LayoutParams(-1, -2));
        var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal }; keys.SetGravity(GravityFlags.CenterVertical);
        keys.AddView(DiamondKey("previous", "上一句", () => session.Command(e => e.Previous(), true)), new LinearLayout.LayoutParams(Dp(60), -2));
        keys.AddView(ConfirmHex(), new LinearLayout.LayoutParams(0, Dp(50), 1) { LeftMargin = Dp(6), RightMargin = Dp(6) });
        keys.AddView(DiamondKey("next", "下一句", () => session.Command(e => e.Next(true), true)), new LinearLayout.LayoutParams(Dp(60), -2));
        tray.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(4) });
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        void Key(string label, string icon, Action action)
        {
            var b = new Button(this) { Text = label }; PgrUi.Style(b, Xb.Dark, icon, center: true, textSp: 12.5f);
            b.SetPadding(Dp(4), 0, Dp(4), 0); b.CompoundDrawablePadding = Dp(3); b.SetSingleLine(true); b.SetMinWidth(0); b.SetMinimumWidth(0);
            DropIconWhenTight(b);
            b.Click += (_, _) => Safe(action);
            row.AddView(b, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(2), RightMargin = Dp(2) });
        }
        Key("重播", "replay", () => session.Command(e => e.Replay(), true));
        Key("暂停", "pause", () => session.Command(e => e.PauseForBrowse()));
        Key("分支", "branch", ShowBranches);
        Key("游戏原声", "original", () => session.Command(e => e.EnterOriginal()));
        tray.AddView(row);
        return tray;
    }

    LinearLayout BuildTrayCollapsed()
    {
        float density = Resources!.DisplayMetrics!.Density;
        var tray = new LinearLayout(this) { Orientation = Orientation.Horizontal, Visibility = ViewStates.Gone };
        tray.Background = new DarkPanelDrawable(density, DecoTier.S) { CutTopRightDp = 16, Inscription = false, Watermark = false };
        tray.SetPadding(Dp(12), Dp(6), Dp(8), Dp(6)); tray.SetGravity(GravityFlags.CenterVertical);
        var t = NewTrayLine(38);
        tray.AddView(CurrentLineBlock(t, 38, false), new LinearLayout.LayoutParams(0, -1, 1));
        tray.AddView(ConfirmHex(), new LinearLayout.LayoutParams(Dp(140), Dp(52)) { LeftMargin = Dp(8) });
        return tray;
    }

    LinearLayout BuildTrayStrip()
    {
        float density = Resources!.DisplayMetrics!.Density;
        var strip = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        strip.Background = new DarkPanelDrawable(density, DecoTier.S) { Inscription = false, Watermark = false, ShowTide = false };
        strip.SetGravity(GravityFlags.CenterHorizontal); strip.SetPadding(Dp(4), Dp(6), Dp(4), Dp(14));
        var expand = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, ContentDescription = "展开播放托盘" };
        expand.SetGravity(GravityFlags.Center);
        expand.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 30)), null, new ColorDrawable(Color.White));
        expand.AddView(Icon("chevron_left", Color.White, 14), new LinearLayout.LayoutParams(Dp(14), Dp(14)));
        var text = new TextView(this) { Text = "展开", TextSize = 11, ImportantForAccessibility = ImportantForAccessibility.No }; text.SetTextColor(Color.White); expand.AddView(text);
        expand.Click += (_, _) => ToggleStoryTray();
        strip.AddView(expand, new LinearLayout.LayoutParams(-1, Dp(52)));
        var t = NewTrayLine(44);
        var block = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, ContentDescription = "当前台词和配音状态，点击查看完整内容" };
        block.SetGravity(GravityFlags.CenterHorizontal);
        block.Click += (_, _) => Safe(CurrentCardClick);
        block.AddView(t.Portrait, new LinearLayout.LayoutParams(Dp(44), Dp(44)) { TopMargin = Dp(10), BottomMargin = Dp(6) });
        t.Meta.TextSize = 10; t.Meta.Gravity = GravityFlags.Center; t.Meta.SetSingleLine(false); t.Meta.SetMaxLines(2);
        block.AddView(t.Meta, new LinearLayout.LayoutParams(-1, -2));
        t.Speaker = new TextView(this) { TextSize = 12, Gravity = GravityFlags.Center }; t.Speaker.SetTextColor(Color.White); t.Speaker.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); t.Speaker.SetSingleLine(true); t.Speaker.Ellipsize = TextUtils.TruncateAt.End;
        block.AddView(t.Speaker, new LinearLayout.LayoutParams(-1, -2));
        t.Line.Visibility = ViewStates.Gone; block.AddView(t.Line);
        strip.AddView(block, new LinearLayout.LayoutParams(-1, -2));
        strip.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
        strip.AddView(ConfirmHex(true), new LinearLayout.LayoutParams(Dp(58), Dp(140)));
        return strip;
    }

    /// <summary>Narrow trays (731dp landscape column, small phones): hide a button's leading icon rather than wrap or cut its label.</summary>
    static void DropIconWhenTight(Button b) => FitSingleLine(b, 11f, 6f);

    /// <summary>
    /// Keeps a tray / console key on one line: first drops the leading icon, then trims the side padding (≥ <paramref name="minPadDp"/>),
    /// then steps the text size down (≥ <paramref name="minSp"/>). Re-runs when the width or the label changes; <paramref name="baseSp"/>
    /// gives the preferred size for the current label (defaults to the size at attach time).
    /// </summary>
    static void FitSingleLine(Button b, float minSp, float minPadDp, Func<float>? baseSp = null)
    {
        var icons = b.GetCompoundDrawablesRelative();
        var icon = icons.Length > 0 ? icons[0] : null;
        int padStart = b.PaddingStart, padEnd = b.PaddingEnd;
        var metrics = b.Resources!.DisplayMetrics!;
        float initialSp = b.TextSize / metrics.ScaledDensity;
        // 0.4.6: a painted English tag (RefinedButtonDrawable) is dropped after the icon, before padding/size are cut,
        // so the label never runs over it (e.g. 「切换大章」 vs CHAPTER at large font in a narrow column).
        var face = b.Background is global::Android.Graphics.Drawables.RippleDrawable rd && rd.NumberOfLayers > 0 ? rd.GetDrawable(0) as RefinedButtonDrawable : null;
        string? english = face?.English; int padEndNoTag = padEnd;
        if (face != null && english != null) { face.English = null; using var m = new Paint(); padEndNoTag = Math.Max((int)(12 * metrics.Density), (int)face.TrailingWidth(m)); face.English = english; }
        b.SetMaxLines(1);
        void Fit()
        {
            int w = b.Width; if (w <= 0) return;
            float dp = metrics.Density, sd = metrics.ScaledDensity;
            string text = b.Text ?? "";
            float maxPx = (baseSp?.Invoke() ?? initialSp) * sd, minPx = Math.Min(minSp * sd, maxPx);
            using var p = new Paint(b.Paint);
            float TextW(float px) { p.TextSize = px; return p.MeasureText(text); }
            float slack = 4 * dp, iconW = icon == null ? 0 : icon.IntrinsicWidth + b.CompoundDrawablePadding;
            bool showIcon = icon != null && TextW(maxPx) + iconW + padStart + padEnd + slack <= w;
            int ps = padStart, pe = padEnd; float size = maxPx;
            bool showTag = english == null || showIcon || TextW(maxPx) + padStart + padEnd + slack <= w;
            if (!showTag) pe = padEndNoTag;
            if (!showIcon && TextW(maxPx) + padStart + pe + slack > w)
            {
                int tight = (int)Math.Max(minPadDp * dp, Math.Min(Math.Min(padStart, pe), (w - TextW(maxPx) - slack) / 2));
                ps = pe = tight;
                while (size > minPx && TextW(size) + 2 * tight + slack > w) size = Math.Max(minPx, size - .5f * sd);
            }
            bool shown = b.GetCompoundDrawablesRelative()[0] != null;
            bool tagShown = face == null || face.English != null;
            if (shown == showIcon && tagShown == (showTag || face == null) && b.PaddingStart == ps && b.PaddingEnd == pe && Math.Abs(b.TextSize - size) < .5f) return;
            b.Post(() =>
            {
                if (face != null && english != null) { face.English = showTag ? english : null; face.InvalidateSelf(); }
                if (icon != null) b.SetCompoundDrawablesRelativeWithIntrinsicBounds(showIcon ? icon : null, icons[1], icons[2], icons[3]);
                b.SetPaddingRelative(ps, b.PaddingTop, pe, b.PaddingBottom);
                if (Math.Abs(b.TextSize - size) >= .5f) b.SetTextSize(global::Android.Util.ComplexUnitType.Px, size);
            });
        }
        b.LayoutChange += (_, e) => { if (e.Right - e.Left != e.OldRight - e.OldLeft) Fit(); };
        b.TextChanged += (_, _) => b.Post(Fit);
    }

    /// <summary>Wide tray column: 384dp per SPEC at 915dp; on narrower landscape (e.g. 731dp) share the width so the script stays readable.</summary>
    /// <remarks>Below 820dp the column takes 46% (min 280dp) so the left page keeps more reading width (owner review, batch 2).</remarks>
    int StoryColumnWidthDp
    {
        get
        {
            int w = Resources!.Configuration!.ScreenWidthDp - 80;
            return w + 80 < 820 ? Math.Clamp((int)(w * .46f), 280, 384) : Math.Clamp(w / 2, 300, 384);
        }
    }

    /// <summary>Page column under ~380dp (narrow landscape with a tray column, or a small phone): compact header variants.</summary>
    internal bool NarrowPage => WideLandscape && PageWidthDp < 380;

    /// <summary>Width of the scrolling page column (rail and tray column excluded in wide landscape).</summary>
    int PageWidthDp
    {
        get
        {
            int screen = Resources!.Configuration!.ScreenWidthDp;
            if (!WideLandscape || !(StoryTrayActive || ListeningTrayWanted)) return screen;
            bool collapsed = ListeningTrayWanted ? ListenCollapsed : TrayCollapsed;
            return screen - 80 - (collapsed ? 78 : StoryColumnWidthDp);
        }
    }

    void ToggleStoryTray()
    {
        bool next = !TrayCollapsed;
        if (!next) trayUserExpanded = true;
        switch (page)
        {
            case 2: session.Settings.LocateTrayCollapsed = next; break;
            case 3: session.Settings.HistoryTrayCollapsed = next; break;
            case 4: session.Settings.SettingsTrayCollapsed = next; break;
            case 6: session.Settings.BranchTrayCollapsed = next; break;
            default: session.Settings.StoryTrayCollapsed = next; break;
        }
        session.SaveSettings();
        UpdateStoryTrayVisibility(); UpdateStatus();
    }

    bool storyColumnShort;

    /// <summary>Story (05-B) and locate (07-B) pages share the tray host; content switches by page.</summary>
    bool StoryTrayActive => (page is 1 or 2 or 3 or 4 or 6) && session.Engine != null || BranchEmptyTray;
    /// <summary>08-B2 ③: no chapter imported / opened — the branch page (and, in the same language, 07 定位 / 09 历史) still shows the
    /// slim dark tray (「手动播放 / 当前台词 · 尚未选句」), fixed collapsed (no expand handle) instead of the old current-line card.</summary>
    bool BranchEmptyTray => (page is 2 or 3 or 6) && session.Engine == null;

    void UpdateStoryTrayVisibility()
    {
        if (storyTrayHost == null) return;
        bool active = StoryTrayActive, wide = WideLandscape, collapsed = TrayCollapsed, locate = page == 2;
        storyTrayHost.Visibility = active && !wide ? ViewStates.Visible : ViewStates.Gone;
        storyColumn.Visibility = active && wide ? ViewStates.Visible : ViewStates.Gone;
        storyTrayExpanded.Visibility = collapsed || locate ? ViewStates.Gone : ViewStates.Visible;
        locateTrayExpanded.Visibility = collapsed || !locate ? ViewStates.Gone : ViewStates.Visible;
        storyTrayCollapsed.Visibility = collapsed ? ViewStates.Visible : ViewStates.Gone;
        storyTrayHandle.Visibility = BranchEmptyTray ? ViewStates.Invisible : ViewStates.Visible;
        if (storyColumnStrip.ChildCount > 0) storyColumnStrip.GetChildAt(0)!.Visibility = BranchEmptyTray ? ViewStates.Invisible : ViewStates.Visible;
        storyTrayHandle.ContentDescription = collapsed ? "展开播放托盘" : "收起播放托盘";
        storyTrayHandleText.Text = collapsed ? "展开" : "收起"; storyTrayHandleEn.Text = collapsed ? "EXPAND" : "COLLAPSE";
        storyTrayHandleIcon.SetImageDrawable(new PgrIconDrawable(collapsed ? "chevron_up" : "chevron_down", PgrTheme.OnPlate, Resources!.DisplayMetrics!.Density, 12));
        storyColumnPanel.Visibility = collapsed || locate ? ViewStates.Gone : ViewStates.Visible;
        locateColumnPanel.Visibility = collapsed || !locate ? ViewStates.Gone : ViewStates.Visible;
        storyColumnEdge.Visibility = collapsed ? ViewStates.Gone : ViewStates.Visible;
        storyColumnStrip.Visibility = collapsed ? ViewStates.Visible : ViewStates.Gone;
        if (storyColumn.LayoutParameters is LinearLayout.LayoutParams lp)
        { int w = Dp(collapsed ? 78 : StoryColumnWidthDp); if (lp.Width != w) { lp.Width = w; storyColumn.LayoutParameters = lp; } }
        // 412dp-class heights: drop the source line and cast strip from the column header so the tray keeps its full button row.
        bool shortScreen = Resources!.Configuration!.ScreenHeightDp < 440;
        storyColumnCast.Visibility = shortScreen ? ViewStates.Gone : ViewStates.Visible;
        if (shortScreen) storyColumnSource.Visibility = ViewStates.Gone;
        storyColumnShort = shortScreen;
        if (active) { currentCard.Visibility = ViewStates.Gone; positionRow.Visibility = ViewStates.Gone; }
        UpdateListeningTrayVisibility();
    }

    /// <summary>Mirror the shared current-line state (UpdateStatus) into every tray variant; refresh the wide column header.</summary>
    void UpdateStoryTray()
    {
        if (storyTrayHost == null || !StoryTrayActive) return;
        var e = session.Engine;
        string meta = currentMeta.Text ?? "", line = current.Text ?? "", statusText = status.Text ?? "";
        Color metaColor = session.AutoPlaybackRunning || session.ClickFollowRunning ? PgrArt.TideRed : PgrTheme.OnPlate;
        int split = meta.IndexOf("  /  ", StringComparison.Ordinal);
        foreach (var t in trayLines)
        {
            if (t.Speaker != null)
            {
                string mode = split >= 0 ? meta[..split] : meta, who = split >= 0 ? meta[(split + 5)..] : "";
                if (t.Meta.Text != mode) t.Meta.Text = mode;
                if (t.Speaker.Text != who) t.Speaker.Text = who;
            }
            else if (t.Meta.Text != meta) t.Meta.Text = meta;
            t.Meta.SetTextColor(metaColor);
            if (t.Line.Text != line) t.Line.Text = line;
            if (t.VoiceTag != null)
            {
                string? tag = session.Listening.IsPlaying ? null : session.GameVoiceTag;
                if (tag == null) t.VoiceTag.Visibility = ViewStates.Gone;
                else
                {
                    if (t.VoiceTag.Text != tag) { var fresh = StopVisuals.VoiceTag(this, tag == "待配音", true); t.VoiceTag.Text = fresh.Text; t.VoiceTag.Background = fresh.Background; t.VoiceTag.SetTextColor(fresh.TextColors); t.VoiceTag.ContentDescription = fresh.ContentDescription; }
                    t.VoiceTag.Visibility = ViewStates.Visible;
                }
            }
            if (t.Status != null && t.Status.Text != statusText) t.Status.Text = statusText;
            UpdatePortrait(t.Portrait, t.Frame, session.Listening.IsPlaying ? session.Listening.Current?.Speaker : e?.Current?.Speaker, ref t.Shown);
        }
        string confirmLabel = page == 2 && session.Candidates.Count > 0 ? "采用识别结果" : "确认当前句";
        foreach (var (b, vertical) in confirmHexes)
        {
            string text = vertical ? string.Join("\n", confirmLabel.ToCharArray()) : confirmLabel;
            if (b.Text == text) continue;
            b.Text = text; b.ContentDescription = confirmLabel;
            bool longLabel = confirmLabel.Length > 5;
            b.TextSize = vertical ? (longLabel ? 12 : 14) : (longLabel ? 14 : 16);
        }
        if (e == null) return;
        if (page == 2 && locateColumnSub != null && e.Current is { } at)
        {
            string next = locateColumnHead + (!string.IsNullOrWhiteSpace(at.Speaker) ? " · " + at.Speaker : "");
            if (locateColumnSub.Text != next) locateColumnSub.Text = next;
        }
        bool brass = e.Mode == RunMode.Choice;
        if (storyTrayHandle.Background is TrayHandleDrawable handle && handle.Brass != brass) { handle.Brass = brass; handle.InvalidateSelf(); }
        if (storyColumnStrip.Background is DarkPanelDrawable stripDeco && stripDeco.BrassLead != brass) { stripDeco.BrassLead = brass; stripDeco.InvalidateSelf(); }
        if (storyTrayCollapsed.Background is DarkPanelDrawable barDeco && barDeco.BrassLead != brass) { barDeco.BrassLead = brass; barDeco.InvalidateSelf(); }
        // Wide column: section name / source, cast, SECTION PROGRESS (display only).
        string key = e.Pack.Id + ":" + session.SectionId + ":" + e.CurrentId;
        if (key == storyTrayKey) return;
        storyTrayKey = key;
        var sections = e.Pack.Chapters.SelectMany(c => c.Sections).ToList();
        string title = SectionDisplay.Title(sections, session.SectionId);
        var (number, name, source) = SectionParts(title);
        storyColumnEyebrow.Text = "剧情 / 第 " + ChapterNumber(e.Pack.Title) + " 章  ·  STORY";
        storyColumnSection.Text = (number.Length > 0 ? number + " " : "") + name;
        storyColumnSource.Text = source ?? ""; storyColumnSource.Visibility = source == null || storyColumnShort ? ViewStates.Gone : ViewStates.Visible;
        var lines = e.Pack.Nodes.Where(n => !n.Archived && n.Kind == "line" && n.SectionId == session.SectionId).ToList();
        int index = lines.FindIndex(n => n.Id == e.CurrentId);
        storyColumnCount.Text = $"台词 · {lines.Count} 句";
        storyColumnReadout.Text = (index >= 0 ? (index + 1).ToString("000") : "—") + " / " + lines.Count.ToString("000");
        if (storyColumnRail.Background is SectionRailDrawable rail) { rail.Total = lines.Count; rail.Index = index; rail.InvalidateSelf(); }
        string castKey = e.Pack.Id + ":" + session.SectionId;
        if (castKey != storyCastKey)
        {
            storyCastKey = castKey; storyColumnCast.RemoveAllViews();
            // 05L: chapter cover behind the column header (eyebrow → SECTION PROGRESS rail), fading into the deck.
            if (storyColumnPanel.Background is DarkPanelDrawable columnDeco)
            { columnDeco.CoverHeightDp = 200; LoadChapterCover(storyColumnPanel, image => columnDeco.Cover = image, e.Pack.Title, e.Pack.Id, Dp(480)); }
            if (locateColumnPanel.Background is DarkPanelDrawable locateDeco)
            { locateDeco.CoverHeightDp = 150; LoadChapterCover(locateColumnPanel, image => locateDeco.Cover = image, e.Pack.Title, e.Pack.Id, Dp(480)); }
            storyColumnCast.AddView(CastStrip(e.Pack, new[] { session.SectionId }), new FrameLayout.LayoutParams(-1, -2));
        }
    }

    /// <summary>05-B page header: chapter cover band with 「剧情 / 第 NN 章 · STORY」, chapter name, outlined number and the cast strip.</summary>
    void StoryCoverBand(PlaybackEngine e)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.S) { VeilAlpha = 205, CutTopRightDp = 16 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, e.Pack.Title, e.Pack.Id, Dp(560));
        string number = ChapterNumber(e.Pack.Title);
        if (number != "—") band.AddView(PgrUi.Outline(this, number, 58, PgrTheme.A(Color.White, 230), 1.4f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.Top) { RightMargin = Dp(14), TopMargin = Dp(8) });
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var eyebrow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; eyebrow.SetGravity(GravityFlags.CenterVertical);
        eyebrow.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var code = new TextView(this) { Text = "剧情 / 第 " + number + " 章  ·  STORY", TextSize = 11 }; code.SetTextColor(PgrTheme.NavText); code.LetterSpacing = .08f; eyebrow.AddView(code);
        code.SetSingleLine(true); code.Ellipsize = TextUtils.TruncateAt.End;
        col.AddView(eyebrow, new LinearLayout.LayoutParams(-1, -2) { RightMargin = Dp(number != "—" && WideLandscape ? 96 : 0) });
        var title = new TextView(this) { Text = ChapterTitle(e.Pack.Title), TextSize = CompactLayout ? 20 : 26 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White);
        title.SetSingleLine(true); title.Ellipsize = TextUtils.TruncateAt.End;
        col.AddView(title, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), RightMargin = Dp(90) });
        var cast = CastStrip(e.Pack, new[] { session.SectionId }); cast.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        col.AddView(cast, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8) });
        band.AddView(col, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Start | GravityFlags.CenterVertical) { LeftMargin = Dp(14), RightMargin = Dp(10) });
        var red = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No }; red.SetBackgroundColor(PgrTheme.Red);
        band.AddView(red, new FrameLayout.LayoutParams(Dp(44), Dp(3), GravityFlags.Bottom | GravityFlags.Start));
        band.ContentDescription = "剧情 / 第 " + number + " 章 · " + ChapterTitle(e.Pack.Title);
        content.AddView(band, new LinearLayout.LayoutParams(-1, Dp(CompactLayout ? 112 : 132)) { BottomMargin = Dp(8) });
    }
}
