using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Listening;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 06-B 听书托盘: fixed dark slate tray above the tabs (position / 52dp portrait + name plate + SQUAD / two lines / status +
/// remaining / spectrum + seek + time / node track / 小节目录 · 记下这句 · 反馈这句), brass on a pending branch, collapse handle
/// (68dp bar) remembered in Settings.ListeningTrayCollapsed. Wide landscape: right 384dp column / 78dp strip with left-edge handle.
/// The tray views are the same fields ListeningPage() always used (rebuilt per Render); commands and texts are unchanged.
/// </summary>
public sealed partial class MainActivity
{
    FrameLayout listenTrayHost = null!, listenTraySlot = null!, listenColumn = null!, listenColumnSlot = null!, listenStripSlot = null!;
    View listenTrayHandle = null!, listenColumnEdge = null!;
    TextView listenHandleText = null!, listenHandleEn = null!; ImageView listenHandleIcon = null!;
    bool listenTrayBuilt;
    // Collapsed bar / strip views (rebuilt per render, updated by UpdateListeningPage).
    TextView? listenMiniName, listenMiniLine, listenStripName, listenStripState;
    View? listenMiniPortrait, listenStripPortrait, listenMiniProgress;
    PortraitFrameDrawable? listenMiniFrame, listenStripFrame;
    string? listenMiniShown, listenStripShown;
    Button? listenMiniPlay, listenStripPlay;
    readonly List<DarkPanelDrawable> listenBrassPanels = new();

    void CreateListeningTray()
    {
        float density = Resources!.DisplayMetrics!.Density;
        listenTrayHost = new FrameLayout(this) { Visibility = ViewStates.Gone };
        listenTrayHost.SetClipChildren(false);
        listenTraySlot = new FrameLayout(this);
        listenTrayHost.AddView(listenTraySlot, new FrameLayout.LayoutParams(-1, -2) { TopMargin = Dp(48) });
        var handle = new FrameLayout(this) { Clickable = true, Focusable = true, Background = new TrayHandleDrawable(density) };
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        row.SetGravity(GravityFlags.Center);
        listenHandleIcon = Icon("chevron_down", PgrTheme.OnPlate, 12);
        row.AddView(listenHandleIcon, new LinearLayout.LayoutParams(Dp(12), Dp(12)) { RightMargin = Dp(5) });
        listenHandleText = new TextView(this) { Text = "收起", TextSize = 11 }; listenHandleText.SetTextColor(Color.White); listenHandleText.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        row.AddView(listenHandleText);
        listenHandleEn = PgrUi.En(this, "COLLAPSE", 7.5f, PgrTheme.NavSub);
        row.AddView(listenHandleEn, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(5) });
        handle.AddView(row, new FrameLayout.LayoutParams(-1, Dp(22), GravityFlags.Bottom));
        handle.Click += (_, _) => ToggleListeningTray();
        listenTrayHandle = handle;
        listenTrayHost.AddView(handle, new FrameLayout.LayoutParams(Dp(120), Dp(48), GravityFlags.Top | GravityFlags.CenterHorizontal));
        root.AddView(listenTrayHost, root.IndexOfChild(tabDivider), new LinearLayout.LayoutParams(-1, -2) { TopMargin = -Dp(26) });

        listenColumn = new FrameLayout(this) { Visibility = ViewStates.Gone };
        listenColumnSlot = new FrameLayout(this);
        listenColumn.AddView(listenColumnSlot, new FrameLayout.LayoutParams(-1, -1) { LeftMargin = Dp(18) });
        var edge = new FrameLayout(this) { Clickable = true, Focusable = true, ContentDescription = "收起播放托盘", Background = new EdgeHandleDrawable(density) };
        var edgeLabel = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        edgeLabel.SetGravity(GravityFlags.Center);
        edgeLabel.AddView(Icon("chevron_right", Color.White, 12), new LinearLayout.LayoutParams(Dp(12), Dp(12)) { BottomMargin = Dp(3) });
        var edgeText = new TextView(this) { Text = "收\n起", TextSize = 10, Gravity = GravityFlags.Center }; edgeText.SetTextColor(Color.White); edgeText.SetLineSpacing(0, .9f);
        edgeLabel.AddView(edgeText);
        edge.AddView(edgeLabel, new FrameLayout.LayoutParams(Dp(20), -1, GravityFlags.Start));
        edge.Click += (_, _) => ToggleListeningTray();
        listenColumnEdge = edge;
        listenColumn.AddView(edge, new FrameLayout.LayoutParams(Dp(48), Dp(96), GravityFlags.Start | GravityFlags.CenterVertical));
        listenStripSlot = new FrameLayout(this);
        listenColumn.AddView(listenStripSlot, new FrameLayout.LayoutParams(-1, -1));
        shell.AddView(listenColumn, new LinearLayout.LayoutParams(Dp(384), -1));
    }

    bool ListeningTrayWanted => page == 5 && session.Listening.Pack != null && session.Listening.Chapter != null;
    bool ListeningTrayActive => ListeningTrayWanted && listenTrayBuilt;

    bool ListenCollapsed => (ShortScreen && !listenUserExpanded) || session.Settings.ListeningTrayCollapsed;

    void ToggleListeningTray()
    {
        bool next = !ListenCollapsed;
        if (!next) listenUserExpanded = true;
        session.Settings.ListeningTrayCollapsed = next;
        session.SaveSettings();
        Render();
    }

    void UpdateListeningTrayVisibility()
    {
        if (listenTrayHost == null) return;
        bool active = ListeningTrayActive, wide = WideLandscape, collapsed = ListenCollapsed;
        listenTrayHost.Visibility = active && !wide ? ViewStates.Visible : ViewStates.Gone;
        listenColumn.Visibility = active && wide ? ViewStates.Visible : ViewStates.Gone;
        listenTrayHandle.ContentDescription = collapsed ? "展开播放托盘" : "收起播放托盘";
        listenHandleText.Text = collapsed ? "展开" : "收起"; listenHandleEn.Text = collapsed ? "EXPAND" : "COLLAPSE";
        listenHandleIcon.SetImageDrawable(new PgrIconDrawable(collapsed ? "chevron_up" : "chevron_down", PgrTheme.OnPlate, Resources!.DisplayMetrics!.Density, 12));
        listenColumnSlot.Visibility = collapsed ? ViewStates.Gone : ViewStates.Visible;
        listenColumnEdge.Visibility = collapsed ? ViewStates.Gone : ViewStates.Visible;
        listenStripSlot.Visibility = collapsed ? ViewStates.Visible : ViewStates.Gone;
        if (listenColumn.LayoutParameters is LinearLayout.LayoutParams lp)
        { int w = Dp(collapsed ? 78 : StoryColumnWidthDp); if (lp.Width != w) { lp.Width = w; listenColumn.LayoutParameters = lp; } }
        if (active) { currentCard.Visibility = ViewStates.Gone; positionRow.Visibility = ViewStates.Gone; }
    }

    /// <summary>Builds the tray for this render (called from ListeningPage when a chapter is open).</summary>
    void BuildListeningTray()
    {
        listenTrayBuilt = true; listenBrassPanels.Clear();
        listenTraySlot.RemoveAllViews(); listenColumnSlot.RemoveAllViews(); listenStripSlot.RemoveAllViews();
        listenMiniName = listenMiniLine = listenStripName = listenStripState = null; listenMiniPortrait = listenStripPortrait = listenMiniProgress = null; listenMiniGlyph = null; listenMiniGlyphShown = null;
        listenMiniFrame = listenStripFrame = null; listenMiniShown = listenStripShown = null; listenMiniPlay = listenStripPlay = null;
        bool collapsed = ListenCollapsed, wide = WideLandscape;
        float density = Resources!.DisplayMetrics!.Density;
        if (!wide)
        {
            // Collapsed: the full tray is still built (detached) so the shared listening fields, ticks and updates keep working.
            if (collapsed) { ListeningTrayBody(); listenTraySlot.AddView(ListeningMiniBar(), new FrameLayout.LayoutParams(-1, Dp(68))); }
            else
            {
                var tray = ListeningTrayBody();
                var deco = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 16, TideDp = 22, Inscription = false, Watermark = false, ShowEmblem = false };
                tray.Background = deco; listenBrassPanels.Add(deco);
                tray.SetPadding(Dp(14), Dp(6), Dp(14), Dp(6));
                listenTraySlot.AddView(tray, new FrameLayout.LayoutParams(-1, -2));
            }
        }
        else if (!collapsed)
        {
            var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var deco = new DarkPanelDrawable(density, DecoTier.L) { Mark = "LISTENING", TideDp = 40, EmblemWidthDp = 170, EmblemRightDp = -30, EmblemBottomDp = 150 };
            panel.Background = deco; listenBrassPanels.Add(deco);
            deco.CoverHeightDp = 150; LoadChapterCover(panel, image => deco.Cover = image, session.Listening.Pack?.Title, session.Listening.Pack?.Id, Dp(480));
            panel.SetPadding(Dp(20), Dp(12), Dp(14), Dp(24));
            var player = session.Listening;
            var eyebrow = PgrUi.En(this, "LISTENING · 第 " + ChapterNumber(player.Pack!.Title) + " 章", 9.5f, PgrArt.GoldLine); panel.AddView(eyebrow);
            var title = new TextView(this) { Text = player.CurrentItem?.SectionTitle ?? player.Chapter!.Title, TextSize = 18 };
            title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White); title.SetSingleLine(true); title.Ellipsize = TextUtils.TruncateAt.End;
            panel.AddView(title);
            panel.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
            panel.AddView(ListeningTrayBody(), new LinearLayout.LayoutParams(-1, -2));
            listenColumnSlot.AddView(panel, new FrameLayout.LayoutParams(-1, -1));
        }
        else { ListeningTrayBody(); listenStripSlot.AddView(ListeningStrip(), new FrameLayout.LayoutParams(-1, -1)); }
        UpdateListeningTrayVisibility();
    }

    LinearLayout ListeningTrayBody()
    {
        var player = session.Listening; float density = Resources!.DisplayMetrics!.Density;
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        listeningPosition = new TextView(this) { TextSize = 10.5f }; listeningPosition.SetTextColor(PgrTheme.NavText); PgrTheme.Tabular(listeningPosition);
        listeningPosition.SetSingleLine(true); listeningPosition.Ellipsize = TextUtils.TruncateAt.End; body.AddView(listeningPosition);
        // Speaker row: 52dp portrait + name plate, SQUAD small avatars on the right.
        var who = new LinearLayout(this) { Orientation = Orientation.Horizontal }; who.SetGravity(GravityFlags.CenterVertical);
        listeningPortraitFrame = new PortraitFrameDrawable(density);
        listeningPortrait = new View(this) { Background = listeningPortraitFrame, ImportantForAccessibility = ImportantForAccessibility.No };
        who.AddView(listeningPortrait, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { RightMargin = Dp(10) });
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var plateRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; plateRow.SetGravity(GravityFlags.CenterVertical);
        listeningSpeaker = new TextView(this) { TextSize = 14 }; listeningSpeaker.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        listeningSpeaker.Background = new NamePlateDrawable(density); listeningSpeaker.SetTextColor(PgrTheme.OnPlate); listeningSpeaker.SetSingleLine(true);
        listeningSpeaker.Ellipsize = TextUtils.TruncateAt.End; listeningSpeaker.SetPadding(Dp(10), Dp(2), Dp(20), Dp(4)); listeningSpeaker.TextSize = 13;
        plateRow.AddView(listeningSpeaker, new LinearLayout.LayoutParams(0, -2, 1) { RightMargin = Dp(6) });
        // 0.4.7（J/M）无配音 / 待配音发丝签：紧跟名牌，有音频时隐藏。
        listeningVoiceTag = StopVisuals.VoiceTag(this, false, true); listeningVoiceTag.Visibility = ViewStates.Gone;
        plateRow.AddView(listeningVoiceTag, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(6) });
        listeningCallsign = null;
        listeningSquad = new LinearLayout(this) { Orientation = Orientation.Horizontal }; renderedSquadKey = null; listeningSquadCompact = true;
        // SQUAD lists every speaker of the section; beyond ~5 avatars (the old fixed footprint) it scrolls sideways.
        var squadScroll = CastScroll.Wrap(listeningSquad); squadScroll.MaxWidthPx = Dp(160); squadScroll.ReservePx = Dp(110); squadScroll.ReserveFor = listeningSpeaker; squadScroll.MinWidthPx = Dp(84); // 0.4.6: plate keeps its full name when the row is tight (squad scrolls); keeps ≥110dp for the speaker plate; 0.4.3 footprint elsewhere
        plateRow.AddView(squadScroll, new LinearLayout.LayoutParams(-2, -2));
        texts.AddView(plateRow);
        listeningDialogue = new TextView(this) { TextSize = 14.5f }; listeningDialogue.SetTextColor(Color.White); listeningDialogue.SetMaxLines(2);
        listeningDialogue.Ellipsize = TextUtils.TruncateAt.End; listeningDialogue.SetLineSpacing(Dp(1), 1);
        texts.AddView(listeningDialogue, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        who.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        body.AddView(who, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(3) });
        listeningStandee = null; listeningPortraitSpeaker = listeningStandeeSpeaker = null;
        // Status + remaining.
        var state = new LinearLayout(this) { Orientation = Orientation.Horizontal }; state.SetGravity(GravityFlags.CenterVertical);
        listeningStatus = new TextView(this) { TextSize = 11 }; listeningStatus.SetTextColor(PgrTheme.NavText); listeningStatus.SetSingleLine(true); listeningStatus.Ellipsize = TextUtils.TruncateAt.End;
        state.AddView(listeningStatus, new LinearLayout.LayoutParams(0, -2, 1));
        listeningRemainingTime = new TextView(this) { TextSize = 11 }; listeningRemainingTime.SetTextColor(PgrTheme.NavText); PgrTheme.Tabular(listeningRemainingTime);
        state.AddView(listeningRemainingTime, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        body.AddView(state, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(3) });
        // 0.4.8 设计稿 05：没有可用配音时「◆ 待配音 · 字幕停留后自动继续」+ 黄铜细轨「字幕停留 03.1 / 05.0 秒」。
        listeningDwellRow = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        listeningDwellLabel = new TextView(this) { TextSize = 11 }; listeningDwellLabel.SetTextColor(PgrArt.GoldLine); listeningDwellLabel.SetSingleLine(true);
        listeningDwellRow.AddView(listeningDwellLabel);
        var dwellLine = new LinearLayout(this) { Orientation = Orientation.Horizontal }; dwellLine.SetGravity(GravityFlags.CenterVertical);
        listeningDwellRail = new CountdownRail(this, "", dark: true);
        dwellLine.AddView(listeningDwellRail, new LinearLayout.LayoutParams(0, -2, 1));
        listeningDwellClock = new TextView(this) { TextSize = 10.5f }; listeningDwellClock.SetTextColor(PgrArt.GoldLine); PgrTheme.Tabular(listeningDwellClock);
        dwellLine.AddView(listeningDwellClock, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        listeningDwellRow.AddView(dwellLine, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        body.AddView(listeningDwellRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(3) });
        // Spectrum + seek + time.
        var wave = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No };
        listeningWave = new HudWaveDrawable(density); wave.Background = listeningWave;
        var meter = new FrameLayout(this);
        meter.AddView(wave, new FrameLayout.LayoutParams(-1, Dp(16), GravityFlags.Top));
        listeningSeek = new SeekBar(this) { Max = 1000 }; PgrTheme.StyleSeekBar(listeningSeek);
        listeningSeek.ContentDescription = "当前这一句的播放进度";
        var seek = listeningSeek;
        listeningSeek.StartTrackingTouch += (_, _) =>
        { if (!ReferenceEquals(listeningSeek, seek)) return; listeningSeeking = true; listeningSeekNode = player.Current?.Id; listeningSeekDuration = player.DurationMilliseconds; };
        listeningSeek.StopTrackingTouch += (_, _) => Safe(() =>
        {
            if (!ReferenceEquals(listeningSeek, seek)) return;
            listeningSeeking = false;
            if (listeningSeekDuration > 0 && listeningSeekNode == player.Current?.Id)
                player.SeekMilliseconds(listeningSeekDuration * seek.Progress / 1000);
            UpdateListeningProgress();
        });
        meter.AddView(listeningSeek, new FrameLayout.LayoutParams(-1, Dp(26), GravityFlags.Bottom));
        body.AddView(meter, new LinearLayout.LayoutParams(-1, Dp(32)) { TopMargin = Dp(1) });
        listeningTime = new TextView(this) { TextSize = 10, Gravity = GravityFlags.End }; listeningTime.SetIncludeFontPadding(false); listeningTime.SetTextColor(PgrTheme.NavText); PgrTheme.Tabular(listeningTime);
        body.AddView(listeningTime);
        // Node track: ◇ 上一句 — ⬡ 播放 / 暂停 — 下一句 ◇.
        var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal }; keys.SetGravity(GravityFlags.CenterVertical);
        keys.AddView(DiamondKey("previous", "上一句", () => session.Listening.Previous()), new LinearLayout.LayoutParams(Dp(60), -2));
        listeningPlay = ListeningHexPlay(false); listeningPlayPauseIcon = null;
        keys.AddView(listeningPlay, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(6), RightMargin = Dp(6) });
        keys.AddView(DiamondKey("next", "下一句", () => session.Listening.Next()), new LinearLayout.LayoutParams(Dp(60), -2));
        body.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        Button Key(string label, string icon, Action action)
        {
            var b = new Button(this) { Text = label }; PgrUi.Style(b, Xb.Dark, icon, center: true, textSp: 12.5f);
            b.SetPadding(Dp(4), 0, Dp(4), 0); b.CompoundDrawablePadding = Dp(3); b.SetSingleLine(true); b.SetMinWidth(0); b.SetMinimumWidth(0);
            DropIconWhenTight(b);
            b.Click += (_, _) => Safe(action);
            row.AddView(b, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(2), RightMargin = Dp(2) });
            return b;
        }
        Key("小节目录", "story", ShowListeningSections);
        listeningBookmark = Key("记下这句", "bookmark", AddListeningBookmark);
        Key("反馈这句", "feedback", FeedbackCurrentListeningLine);
        body.AddView(row);
        return body;
    }

    Button ListeningHexPlay(bool vertical)
    {
        var b = new Button(this) { Text = "播放", TextSize = vertical ? 13 : 15 };
        b.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 50)), new HexButtonDrawable(Resources!.DisplayMetrics!.Density, vertical), null);
        b.BackgroundTintList = null; b.SetTextColor(Color.White); b.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); b.SetAllCaps(false); b.StateListAnimator = null; b.Elevation = 0;
        b.SetMinHeight(Dp(48)); b.SetMinimumHeight(Dp(48)); b.SetMinWidth(0); b.SetMinimumWidth(0);
        b.SetPadding(Dp(vertical ? 2 : 18), Dp(vertical ? 14 : 2), Dp(vertical ? 2 : 18), Dp(vertical ? 14 : 2));
        b.Click += (_, _) => Safe(ToggleListeningPlayback);
        return b;
    }

    LinearLayout ListeningMiniBar()
    {
        float density = Resources!.DisplayMetrics!.Density;
        var bar = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var deco = new DarkPanelDrawable(density, DecoTier.S) { CutTopRightDp = 16, Inscription = false, Watermark = false };
        bar.Background = deco; listenBrassPanels.Add(deco);
        var line = new FrameLayout(this);
        listenMiniProgress = new View(this) { PivotX = 0 }; listenMiniProgress.SetBackgroundColor(PgrArt.TideRed);
        line.AddView(listenMiniProgress, new FrameLayout.LayoutParams(-1, -1));
        bar.AddView(line, new LinearLayout.LayoutParams(-1, Dp(2)));
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical); row.SetPadding(Dp(12), 0, Dp(6), 0);
        var block = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true, ContentDescription = "当前台词和配音状态，点击查看完整内容" };
        block.SetGravity(GravityFlags.CenterVertical); block.Click += (_, _) => Safe(CurrentCardClick);
        listenMiniFrame = new PortraitFrameDrawable(density) { ShowSerial = false, Active = true };
        listenMiniPortrait = new View(this) { Background = listenMiniFrame, ImportantForAccessibility = ImportantForAccessibility.No };
        // 设计稿 01 / 03 / 04 / 16：待选支线 / 停下卡 / 倒计时暂停时，头像位换成黄铜描边图标块（分支 / 反馈 / 按顺序 / 暂停）。
        var lead = new FrameLayout(this);
        lead.AddView(listenMiniPortrait, new FrameLayout.LayoutParams(-1, -1));
        listenMiniGlyph = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No, Visibility = ViewStates.Gone };
        lead.AddView(listenMiniGlyph, new FrameLayout.LayoutParams(-1, -1));
        block.AddView(lead, new LinearLayout.LayoutParams(Dp(38), Dp(38)) { RightMargin = Dp(10) });
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        listenMiniName = new TextView(this) { TextSize = 11 }; listenMiniName.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); listenMiniName.SetTextColor(PgrTheme.OnPlate); listenMiniName.SetSingleLine(true);
        texts.AddView(listenMiniName);
        listenMiniLine = new TextView(this) { TextSize = 14 }; listenMiniLine.SetTextColor(Color.White); listenMiniLine.SetSingleLine(true); listenMiniLine.Ellipsize = TextUtils.TruncateAt.End;
        texts.AddView(listenMiniLine);
        block.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(block, new LinearLayout.LayoutParams(0, -1, 1));
        row.AddView(DiamondKey("previous", "上一句", () => session.Listening.Previous()), new LinearLayout.LayoutParams(Dp(48), Dp(56)));
        listenMiniPlay = ListeningHexPlay(false); listenMiniPlay.Text = ""; listenMiniPlay.SetPadding(Dp(24), 0, Dp(8), 0);
        row.AddView(listenMiniPlay, new LinearLayout.LayoutParams(Dp(64), Dp(48)) { LeftMargin = Dp(2), RightMargin = Dp(2) });
        row.AddView(DiamondKey("next", "下一句", () => session.Listening.Next()), new LinearLayout.LayoutParams(Dp(48), Dp(56)));
        bar.AddView(row, new LinearLayout.LayoutParams(-1, 0, 1));
        return bar;
    }

    LinearLayout ListeningStrip()
    {
        float density = Resources!.DisplayMetrics!.Density;
        var strip = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var deco = new DarkPanelDrawable(density, DecoTier.S) { Inscription = false, Watermark = false, ShowTide = false };
        strip.Background = deco; listenBrassPanels.Add(deco);
        strip.SetGravity(GravityFlags.CenterHorizontal); strip.SetPadding(Dp(4), Dp(6), Dp(4), Dp(10));
        var expand = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, ContentDescription = "展开播放托盘" };
        expand.SetGravity(GravityFlags.Center);
        expand.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 30)), null, new ColorDrawable(Color.White));
        expand.AddView(Icon("chevron_left", Color.White, 14), new LinearLayout.LayoutParams(Dp(14), Dp(14)));
        var text = new TextView(this) { Text = "展开", TextSize = 11, ImportantForAccessibility = ImportantForAccessibility.No }; text.SetTextColor(Color.White); expand.AddView(text);
        expand.Click += (_, _) => ToggleListeningTray();
        strip.AddView(expand, new LinearLayout.LayoutParams(-1, Dp(52)));
        var block = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, ContentDescription = "当前台词和配音状态，点击查看完整内容" };
        block.SetGravity(GravityFlags.CenterHorizontal); block.Click += (_, _) => Safe(CurrentCardClick);
        listenStripFrame = new PortraitFrameDrawable(density) { ShowSerial = false, Active = true };
        listenStripPortrait = new View(this) { Background = listenStripFrame, ImportantForAccessibility = ImportantForAccessibility.No };
        block.AddView(listenStripPortrait, new LinearLayout.LayoutParams(Dp(44), Dp(44)) { TopMargin = Dp(6), BottomMargin = Dp(4) });
        listenStripState = new TextView(this) { TextSize = 10, Gravity = GravityFlags.Center }; listenStripState.SetTextColor(PgrTheme.OnPlate); listenStripState.SetMaxLines(8); listenStripState.SetLineSpacing(0, .92f);
        block.AddView(listenStripState, new LinearLayout.LayoutParams(-1, -2));
        listenStripName = new TextView(this) { TextSize = 12, Gravity = GravityFlags.Center }; listenStripName.SetTextColor(Color.White); listenStripName.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        listenStripName.SetSingleLine(true); listenStripName.Ellipsize = TextUtils.TruncateAt.End;
        block.AddView(listenStripName, new LinearLayout.LayoutParams(-1, -2));
        strip.AddView(block, new LinearLayout.LayoutParams(-1, -2));
        strip.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
        strip.AddView(DiamondKey("previous", "上一句", () => session.Listening.Previous()), new LinearLayout.LayoutParams(Dp(56), Dp(52)));
        listenStripPlay = ListeningHexPlay(true); listenStripPlay.Text = ""; listenStripPlay.SetPadding(Dp(20), 0, Dp(4), 0);
        strip.AddView(listenStripPlay, new LinearLayout.LayoutParams(Dp(56), Dp(72)) { TopMargin = Dp(2), BottomMargin = Dp(2) });
        strip.AddView(DiamondKey("next", "下一句", () => session.Listening.Next()), new LinearLayout.LayoutParams(Dp(56), Dp(52)));
        return strip;
    }

    /// <summary>Mirror the shared listening state into the collapsed bar / strip and switch the brass (pending branch) state.</summary>
    View? listenMiniGlyph; string? listenMiniGlyphShown;

    /// <summary>迷你条左侧图标：倒计时暂停 → pause；待选支线 → branch；未核续接 / 小节已读完 → next；无文本死角 → feedback。</summary>
    static string MiniGlyphFor(ListeningSession player, bool branch)
    {
        if (player.CountdownPaused && player.CountdownRemaining > 0) return "pause";
        if (branch) return "branch";
        return player.PendingStop is { Action: ListeningStopAction.OrderContinue } or { SectionComplete: true } ? "next" : "feedback";
    }

    void UpdateListeningMiniGlyph(string? icon)
    {
        if (listenMiniGlyph == null || listenMiniPortrait == null) return;
        if (listenMiniGlyphShown == icon) return;
        listenMiniGlyphShown = icon;
        listenMiniGlyph.Visibility = icon == null ? ViewStates.Gone : ViewStates.Visible;
        listenMiniPortrait.Visibility = icon == null ? ViewStates.Visible : ViewStates.Invisible;
        if (icon == null) return;
        float density = Resources!.DisplayMetrics!.Density;
        // 设计稿 slim47：金色 10% 底 + 60% 金线描边 + 18dp 金色图标（暂停态同色，靠图标区分）。
        var plate = PgrTheme.Plate(this, PgrTheme.A(PgrArt.GoldLine, 26), PgrTheme.A(PgrArt.GoldLine, 153), 0, Corners.None);
        var glyph = new InsetDrawable(new PgrIconDrawable(icon, PgrArt.GoldLine, density, 18), Dp(10));
        listenMiniGlyph.Background = new LayerDrawable(new Drawable[] { plate, glyph });
    }

    /// <summary>待选支线时托盘说话人牌（设计稿 01 / 03 / 16）：暂停 →「已暂停 · 选择下一段故事」，结局分歧 →「选择下一段故事 · 结局分歧」。</summary>
    static string BranchTrayPlate(ListeningSession player) =>
        player.CountdownPaused && player.CountdownRemaining > 0 ? "已暂停 · 选择下一段故事" : player.ChoiceIsEnding ? "选择下一段故事 · 结局分歧" : "选择下一段故事";

    /// <param name="stopPlate">停下卡（未核续接 / 小节已读完 / 无文本死角）时的托盘牌（设计稿 04：迷你条同样转黄铜态）；否则为 null。</param>
    void UpdateListeningMini(string? speaker, string line, bool branch, string? stopPlate = null)
    {
        var player = session.Listening;
        bool brass = branch || stopPlate != null;
        string name = branch ? BranchTrayPlate(player) : stopPlate ?? (string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim());
        if (listenMiniName != null)
        {
            listenMiniName.Text = brass ? name : name + "  ·  " + (player.IsPlaying ? "正在收听" : "已暂停");
            listenMiniName.SetTextColor(brass ? PgrArt.GoldLine : PgrTheme.OnPlate);
            listenMiniLine!.Text = branch ? player.Status : line;
            if (listenMiniFrame != null) UpdatePortrait(listenMiniPortrait!, listenMiniFrame, speaker, ref listenMiniShown);
            UpdateListeningMiniGlyph(brass ? MiniGlyphFor(player, branch) : null);
            if (listenMiniProgress != null)
            {
                // 设计稿 01 / 03 / 04：倒计时期间迷你条顶线显示剩余比例（黄铜），否则为播放进度（潮红）。
                bool countdown = brass && player.CountdownRemaining > 0;
                listenMiniProgress.SetBackgroundColor(countdown ? (player.CountdownPaused ? PromptArt.PausedGrey : PgrTheme.Brass) : PgrArt.TideRed);
                if (countdown) listenMiniProgress.ScaleX = player.CountdownFraction;
                else if (listeningSeek != null) listenMiniProgress.ScaleX = listeningSeek.Progress / 1000f;
            }
        }
        if (listenStripName != null)
        {
            listenStripName.Text = brass ? "" : name;
            string stripText = branch ? "选择下一段故事" : stopPlate?.Replace("已暂停 · ", "") ?? "";
            listenStripState!.Text = brass ? string.Join("\n", stripText.Replace(" · ", "").ToCharArray()) : player.IsPlaying ? "正在收听" : "已暂停";
            listenStripState.SetTextColor(brass ? PgrArt.GoldLine : PgrTheme.OnPlate);
            if (listenStripFrame != null) UpdatePortrait(listenStripPortrait!, listenStripFrame, speaker, ref listenStripShown);
        }
        foreach (var b in new[] { listenMiniPlay, listenStripPlay })
        {
            if (b == null) continue;
            // 迷你条 / 竖条六角键与主播放键同一状态（设计稿 16）：倒计时中「暂停」、已暂停「继续」。
            b.Enabled = player.PlayEnabled;
            b.ContentDescription = player.PlayTogglesCountdown ? (player.CountdownPaused ? "继续倒计时" : "暂停倒计时，不自动续接") : player.PlayCaption;
            b.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable(player.PlayShowsPauseIcon ? "pause" : "play", Color.White, Resources!.DisplayMetrics!.Density, 16), null, null, null);
        }
        foreach (var deco in listenBrassPanels) if (deco.BrassLead != brass) { deco.BrassLead = brass; deco.InvalidateSelf(); }
        if (listenTrayHandle.Background is TrayHandleDrawable handle && handle.Brass != brass) { handle.Brass = brass; handle.InvalidateSelf(); }
    }

    /// <summary>06-B page header: chapter cover band (「听书 · LISTENING」, chapter title, 「NN 个小节 / 按顺序连续播放」, outlined number).</summary>
    void ListeningCoverBand()
    {
        var player = session.Listening; float density = Resources!.DisplayMetrics!.Density;
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.S) { VeilAlpha = 205, CutTopRightDp = 16 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, player.Pack!.Title, player.Pack.Id, Dp(560));
        string number = ChapterNumber(player.Pack.Title);
        bool narrow = NarrowPage;
        if (number != "—") band.AddView(PgrUi.Outline(this, number, narrow ? 40 : 58, PgrTheme.A(Color.White, 230), 1.4f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(14) });
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var eyebrow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; eyebrow.SetGravity(GravityFlags.CenterVertical);
        eyebrow.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var code = new TextView(this) { Text = "听书 · LISTENING", TextSize = 11 }; code.SetTextColor(PgrTheme.NavText); code.LetterSpacing = .08f; eyebrow.AddView(code);
        col.AddView(eyebrow);
        var title = new TextView(this) { Text = player.Chapter!.Title, TextSize = narrow ? 18 : CompactLayout ? 20 : 24 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White);
        title.SetSingleLine(true); title.Ellipsize = TextUtils.TruncateAt.End;
        col.AddView(title, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), RightMargin = Dp(number != "—" ? (narrow ? 64 : 96) : 0) });
        var detail = new TextView(this) { Text = $"{SectionDisplay.Groups(player.Chapter.Sections).Count:00} 个小节  /  按顺序连续播放", TextSize = 11 }; detail.SetTextColor(PgrTheme.NavText); PgrTheme.Tabular(detail);
        col.AddView(detail, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        band.AddView(col, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Start | GravityFlags.CenterVertical) { LeftMargin = Dp(14), RightMargin = Dp(10) });
        var red = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No }; red.SetBackgroundColor(PgrTheme.Red);
        band.AddView(red, new FrameLayout.LayoutParams(Dp(44), Dp(3), GravityFlags.Bottom | GravityFlags.Start));
        band.ContentDescription = "听书 · " + player.Chapter.Title;
        content.AddView(band, new LinearLayout.LayoutParams(-1, Dp(CompactLayout ? 100 : 116)) { BottomMargin = Dp(8), TopMargin = Dp(4) });
    }

    /// <summary>
    /// 0.4.8 宽屏待选支线卡（设计稿 16 / 08 / 10 的横屏融合版式）：左侧深石板块（铭文、标题、小节位置、倒计时环），
    /// 右侧冷灰白纸面（分支前一句、编号选项片、细轨、结局锁定说明、提示行），最右竖排「暂停 / 继续」。
    /// 结局分歧左块改酒红渐变；倒计时与否、暂停态都来自 ListeningSession。
    /// </summary>
    void ListeningBranchCardWide(LinearLayout parent)
    {
        var player = session.Listening; var detail = player.ChoiceDetail; if (detail == null) return;
        float density = Resources!.DisplayMetrics!.Density;
        bool counting = player.CountdownRemaining > 0, ending = player.ChoiceIsEnding;
        float leftDp = ending ? 236 : 214;
        var bar = new FusedBarDrawable(density) { LeftWidthDp = leftDp };
        if (ending)
        {
            bar.LeftTop = Color.ParseColor("#3A2A30"); bar.LeftBottom = Color.ParseColor("#272D35");
            bar.EdgeTop = Color.ParseColor("#E0485C"); bar.EdgeBottom = PgrTheme.Brass; bar.Seam = Color.Argb(217, 224, 106, 121);
        }
        var card = new LinearLayout(this) { Orientation = Orientation.Horizontal, Background = bar };
        card.SetMinimumHeight(Dp(138));
        // 左块
        var left = new LinearLayout(this) { Orientation = Orientation.Vertical }; left.SetGravity(GravityFlags.CenterVertical);
        left.SetPadding(Dp(14), Dp(11), Dp(4), Dp(10));
        left.AddView(PgrUi.En(this, ending ? "ENDING · 结局分歧" : "BRANCH · 待选互动", 9, ending ? PromptArt.EndingPink : PgrArt.GoldLine));
        var title = PromptArt.Label(this, ending ? "此处是结局分歧" : counting ? "分支自动选择" : "分支已暂停 · 选择后继续", ending ? 16 : 15, Color.White, true, maxLines: 2);
        left.AddView(title, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        if (ending) left.AddView(PromptArt.Label(this, "只收听所选结局", 11.5f, Color.ParseColor("#D8C9B0"), maxLines: 1), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        string where = detail.SectionTitle + (detail.AfterLine > 0 ? $" · 第 {detail.AfterLine} 句之后" : "") + $" · CHOICE {detail.Index}/{detail.Total}";
        left.AddView(PromptArt.Label(this, where, 10.5f, PgrTheme.NavSub, maxLines: 1), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        if (counting) left.AddView(CountdownBadgeView(player.CountdownRemaining, ending ? "秒后选入结局" : "秒后自动选择", true), new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(8) });
        card.AddView(left, new LinearLayout.LayoutParams(Dp((int)leftDp - 22), -1));
        // 右侧纸面
        var right = new LinearLayout(this) { Orientation = Orientation.Vertical }; right.SetGravity(GravityFlags.CenterVertical);
        right.SetPadding(Dp(28), Dp(10), Dp(12), Dp(10));
        if (detail.PreviousLine.Length > 0)
        {
            var prev = new TextView(this) { TextSize = 12 }; prev.SetSingleLine(true); prev.Ellipsize = TextUtils.TruncateAt.End;
            prev.TextFormatted = PromptArt.SpeakerLine(detail.PreviousSpeaker, detail.PreviousLine, PromptArt.Ink, PgrTheme.Secondary);
            right.AddView(prev, new LinearLayout.LayoutParams(-1, -2));
        }
        // 审稿 c1478eb：A / B 字母；「上次选择」「已听」上屏；有空间时加句数与时长。
        // 两项并排只在页宽 ≥ 760dp 时用（否则每片不到 180dp，放不下字母 + 文字 + 标记 + 尾标），其余竖排。
        bool inline = detail.Options.Count <= 2 && PageWidthDp >= 760;
        bool showCount = inline ? PageWidthDp >= 1000 : PageWidthDp >= 720;
        var tiles = new LinearLayout(this) { Orientation = inline ? Orientation.Horizontal : Orientation.Vertical };
        for (int i = 0; i < detail.Options.Count; i++)
        {
            var o = detail.Options[i]; string id = o.Id; bool auto = id == player.CountdownOptionId, endingOption = o.Label.Contains("开启结局");
            LinearLayout? marks = null;
            if (o.LastChosen || o.Heard || showCount && o.Lines > 0)
            {
                marks = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants }; marks.SetGravity(GravityFlags.CenterVertical);
                if (showCount && o.Lines > 0)
                {
                    var count = new TextView(this) { Text = o.DurationMs is { } dms ? $"{o.Lines} 句 · {(dms + 999) / 1000 / 60}:{(dms + 999) / 1000 % 60:00}" : $"{o.Lines} 句", TextSize = 10.5f };
                    count.SetTextColor(PgrTheme.Secondary); count.SetSingleLine(true); PgrTheme.Tabular(count); marks.AddView(count);
                }
                if (o.LastChosen || o.Heard)
                    marks.AddView(PgrUi.Chip(this, o.LastChosen ? "上次选择" : "已听", o.LastChosen ? PgrUi.Status.Brass : PgrUi.Status.Teal), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = marks.ChildCount > 0 ? Dp(6) : 0 });
            }
            var tile = OptionTiles.Tile(this, i, o.Label, auto, auto ? (endingOption ? OptionTail.Ending : OptionTail.Default) : OptionTail.Chevron,
                PromptArt.EndingName(o.Label), auto && endingOption ? PromptArt.EndingRed : null, compact: true,
                marker: ((char)('A' + Math.Min(i, 25))).ToString(), trailing: marks);
            string extra = o.Lines > 0 ? (o.DurationMs is { } ms ? $"，{o.Lines} 句，约 {(ms + 999) / 1000} 秒" : $"，{o.Lines} 句") : "";
            tile.ContentDescription = o.Label + extra + (o.LastChosen ? "，上次选择" : o.Heard ? "，已听" : "") + (auto ? "（倒计时结束后自动选择）" : "");
            tile.Click += (_, _) => Safe(() => { RequestNotifications(); player.Choose(id); });
            float weight = inline && ending ? (endingOption ? 2.1f : .9f) : 1;
            tiles.AddView(tile, inline ? new LinearLayout.LayoutParams(0, -2, weight) { LeftMargin = i > 0 ? Dp(8) : 0 } : new LinearLayout.LayoutParams(-1, -2) { TopMargin = i > 0 ? Dp(6) : 0 });
        }
        right.AddView(tiles, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(7) });
        if (counting) right.AddView(CountdownRailRow(player.CountdownFraction, ending ? "AUTO-PICK · 将选入结局" : "AUTO-PICK · 将自动选择"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(7) });
        if (ending)
        {
            var lockRow = OptionTiles.Hint(this, "lock", PromptArt.EndingRed, "选入结局后只读这一结局，读完即停；如需其他结局，请回到结局选项前重新选择。", 11, maxLines: 1);
            lockRow.Background = PromptArt.NoteBand(this, PgrTheme.A(PromptArt.EndingRed, 14), PromptArt.EndingRed); lockRow.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            right.AddView(lockRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        }
        var hints = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        hints.AddView(OptionTiles.Hint(this, "branch", PgrTheme.Brass, counting ? "点任一选项立即选择" : "点任一选项继续收听", 11, maxLines: 1), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(14) });
        if (counting) hints.AddView(OptionTiles.Hint(this, "pause", PromptArt.SlateBlue, PromptArt.PauseHint, 11, maxLines: 1), new LinearLayout.LayoutParams(0, -2, 1));
        right.AddView(hints, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        if (PageWidthDp >= 640)
        {
            // Mini route map (display only): section line axis, played part crimson, choice points brass, current solid.
            var map = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.No };
            map.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.White, 200), PgrTheme.A(PgrTheme.BorderStrong, 160), PgrTheme.CutSmall), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall, Paper = false });
            map.SetPadding(Dp(10), Dp(6), Dp(10), Dp(6));
            var mapHead = new LinearLayout(this) { Orientation = Orientation.Horizontal }; mapHead.SetGravity(GravityFlags.CenterVertical);
            var routeTitle = new TextView(this) { Text = "路线", TextSize = 11 }; routeTitle.SetTextColor(PgrTheme.Foreground); routeTitle.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); mapHead.AddView(routeTitle);
            mapHead.AddView(PgrUi.En(this, "ROUTE MAP", 8.5f, PgrTheme.Muted), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
            map.AddView(mapHead);
            map.AddView(new View(this) { Background = new RouteMapDrawable(density) { Total = Math.Max(1, detail.SectionLines), CurrentLine = detail.AfterLine, Points = detail.PointLines.ToArray(), Branches = detail.Options.Count } }, new LinearLayout.LayoutParams(-1, Dp(46)));
            map.Background = null; map.SetPadding(0, Dp(6), 0, 0);
            right.AddView(map);
        }
        card.AddView(right, new LinearLayout.LayoutParams(0, -2, 1));
        if (counting)
        {
            var toggle = new PauseToggle(this, vertical: true, textSp: 12.5f);
            toggle.Update(player.CountdownPaused);
            toggle.Click += (_, _) => Safe(() => session.Listening.ToggleCountdownPause());
            listeningPauseToggles.Add(toggle);
            card.AddView(toggle, new LinearLayout.LayoutParams(Dp(62), -1) { TopMargin = Dp(10), BottomMargin = Dp(10), RightMargin = Dp(10) });
        }
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
    }
}
