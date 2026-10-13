using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    TextView? listeningPosition, listeningSpeaker, listeningDialogue, listeningStatus, listeningResume;
    bool? listeningPlayPauseIcon;
    Button? listeningPlay, listeningPolicy, listeningSpeed, listeningSleep, listeningBookmark;
    SeekBar? listeningSeek;
    TextView? listeningTime, listeningRemainingTime;
    HudWaveDrawable? listeningWave;
    LinearLayout? listeningChoices, listeningBookmarks;
    // 0.4.7（M）停下处倒计时视觉：每秒只更新数字与细轨，不重建卡片。
    readonly List<CountdownBadge> listeningCountdownBadges = new();
    readonly List<CountdownRail> listeningCountdownRails = new();
    readonly List<PauseRow> listeningPauseRows = new();
    readonly List<PauseToggle> listeningPauseToggles = new();
    TextView? listeningVoiceTag;
    bool listeningNoticeShown;
    string? renderedListeningPack, renderedListeningChapter;
    string renderedListeningChoices = "", renderedListeningBookmarks = "";
    Action? listeningProgressTick;
    bool listeningUiVisible, listeningTickScheduled, listeningSeeking;
    string? listeningSeekNode;
    long listeningSeekDuration;

    void ClearListeningViews()
    {
        ClearListeningDirectoryViews();
        listeningPosition = listeningSpeaker = listeningDialogue = listeningStatus = listeningResume = null;
        listeningPlay = listeningPolicy = listeningSpeed = listeningSleep = listeningBookmark = null; listeningPlayPauseIcon = null;
        listeningSeek = null; listeningTime = listeningRemainingTime = null; listeningSeeking = false; listeningWave = null;
        listeningChoices = listeningBookmarks = null; listeningDwellRow = null;
        ClearPortraitViews();
        renderedListeningPack = renderedListeningChapter = null;
        renderedListeningChoices = renderedListeningBookmarks = "";
        // 页面重建后视图和滚动位置已重置，同一选项也需重新露出暂停行。
        revealedBranchCard = null;
        if (listeningProgressTick != null) pageScroll.RemoveCallbacks(listeningProgressTick);
        listeningTickScheduled = false;
    }

    void OnListeningChanged()
    {
        if (!alive) return;
        RunOnUiThread(() =>
        {
            if (!alive) return;
            UpdateStatus();
            MaybeShowListeningBubble();
            if (page != 5) return;
            if (renderedListeningPack != session.Listening.Pack?.Id || renderedListeningChapter != session.Listening.Chapter?.Id)
                Render();
            else UpdateListeningPage();
        });
    }

    void ListeningEntrance()
    {
        // 10-A3 button: headphones icon, label + hint sub line, LISTENING tag and tail chevron. Text and action unchanged.
        string hint = session.Listening.Pack == null ? "整章连播 · 独立进度 · 随手书签" : session.Listening.ResumeText;
        var entry = new Button(this) { Text = "听书 / 让故事继续" };
        PgrUi.Style(entry, Xb.Normal, "headphones", "LISTENING", sub: hint, tail: true, textSp: 15).Deco =
            new FusionDeco(Resources!.DisplayMetrics!.Density, DecoTier.M, FxKind.Listen) { Code = "LSN · 01", CutTR = 8, CutBL = 8, Ring = (15 + 9, 0, 17) };
        entry.ContentDescription = "听书，按大章节连续收听，记住上次位置";
        entry.SetMaxLines(3); entry.SetPadding(entry.PaddingLeft, Dp(9), entry.PaddingRight, Dp(9));
        entry.Click += (_, _) => Safe(() => ShowPage(5));
        content.AddView(entry, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
    }

    string? revealedBranchCard;

    /// <summary>
    /// 审稿 c1478eb 布局自查：竖屏待选支线卡出现时（同一处只做一次），把页面滚到卡片可见；
    /// 卡片比可视区高时以底部「暂停 / 继续」行可见为准（卡底贴可视区底）。
    /// </summary>
    void RevealListeningBranchCard(ListeningSession player)
    {
        string key = (player.ChoiceDetail?.SectionTitle ?? "") + "\u001d" + string.Join("\u001f", player.Choices.Select(c => c.Id));
        if (key == revealedBranchCard) return;
        revealedBranchCard = key;
        var holder = listeningChoices;
        pageScroll.Post(() =>
        {
            if (!alive || holder == null || !ReferenceEquals(holder, listeningChoices) || holder.ChildCount == 0 || pageScroll.Height <= 0) return;
            var card = holder.GetChildAt(holder.ChildCount - 1)!;
            int offset = 0;
            for (View? v = holder; v != null && !ReferenceEquals(v, pageScroll); v = v.Parent as View) offset += v.Top;
            int top = offset + card.Top - Dp(4), bottom = offset + card.Bottom + Dp(8);
            int view = pageScroll.Height - pageScroll.PaddingTop - pageScroll.PaddingBottom;
            int target = bottom - top <= view ? top : bottom - view;
            if (pageScroll.ScrollY > top || pageScroll.ScrollY + view < bottom) pageScroll.SmoothScrollTo(0, Math.Max(0, target));
        });
    }

    void ListeningPage()
    {
        var player = session.Listening;
        renderedListeningPack = player.Pack?.Id; renderedListeningChapter = player.Chapter?.Id;
        var navigation = Row(content);
        bool narrowNav = player.Pack != null && NarrowPage;
        var back = Button(navigation, "返回章节", () => ShowPage(0)); PgrUi.Style(back, Xb.Normal, "previous", narrowNav ? null : "BACK", textSp: 14);
        var change = Button(navigation, "切换大章", ChooseListeningChapter); PgrUi.Style(change, Xb.Normal, "archive", narrowNav ? null : "CHAPTER", textSp: 14);
        foreach (var fit in new[] { back, change }) { fit.SetSingleLine(true); fit.SetMinWidth(0); fit.SetMinimumWidth(0); DropIconWhenTight(fit); }
        ListeningHero();
        if (player.Pack == null || player.Chapter == null)
        {
            var empty = Card(content);
            empty.AddView(PgrTheme.Eyebrow(this, "STANDBY · 未选择章节"));
            empty.AddView(PgrTheme.Apply(Text("选择一章，听完一段故事", 19), PgrTheme.Role.Heading));
            var hint = Text("同一大章的小节会按顺序衔接。听书位置单独保存，再次打开时从上次那一句继续。", 14); hint.SetTextColor(PgrTheme.Secondary); empty.AddView(hint);
            ButtonIcon(Button(empty, "选择大章节", ChooseListeningChapter), "play", true);
            Line("使用已导入的章节配音，无需打开游戏、屏幕捕获或自动点击权限。", 13);
            return;
        }

        // 06-B: branch card first (pending choices), then the directory; the player itself lives in the fixed tray
        // (portrait, above the tabs) or the right column (wide landscape).
        listeningChoices = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.AddView(listeningChoices);
        BuildListeningDirectory();
        BuildListeningTray();

        var memory = new LinearLayout(this) { Orientation = Orientation.Vertical };
        memory.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.Panel, PgrTheme.Border, PgrTheme.CutMedium, Corners.Diagonal, PgrTheme.Steel, AccentEdge.Left),
            new FusionDeco(Resources!.DisplayMetrics!.Density, DecoTier.M) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium }); memory.Tag = "fx";
        memory.SetPadding(Dp(16), Dp(10), Dp(14), Dp(12));
        content.AddView(memory, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(10) });
        var memoryTitle = PgrTheme.Eyebrow(this, "收听记录 · RESUME", PgrTheme.Steel); memoryTitle.SetAllCaps(false); memoryTitle.SetPadding(0, Dp(2), 0, Dp(2)); memory.AddView(memoryTitle);
        listeningResume = Text("", 13); memory.AddView(listeningResume);
        var memoryHelp = PgrTheme.Apply(Text("自动记住小节与句子。恢复、跳转后保持暂停，点播放再继续。", 12), PgrTheme.Role.Caption); memory.AddView(memoryHelp);

        Section("收听设置", "PLAYBACK SETTINGS");
        var settings = Card(content);
        listeningPolicy = Button(settings, "", ChooseListeningPolicy); PgrUi.Style(listeningPolicy, Xb.Normal, "branch", "POLICY", textSp: 14);
        listeningPolicy.SetSingleLine(true);
        var policyHelp = PgrTheme.Apply(Text("全部听：收听所有已收录分支，未接入路线的台词以“小节补充片段”标注；手动选择：遇分支暂停等你点选；默认分支：按分支页设定的第一或第二个选项继续，只有一个选项时按第一个。", 12), PgrTheme.Role.Caption); settings.AddView(policyHelp);
        var options = Row(settings);
        listeningSpeed = Button(options, "", ChooseListeningSpeed); PgrUi.Style(listeningSpeed, Xb.Normal, "speed", "SPEED", textSp: 14); PgrTheme.Tabular(listeningSpeed);
        listeningSleep = Button(options, "", ChooseListeningSleep); PgrUi.Style(listeningSleep, Xb.Normal, "timer", "SLEEP", textSp: 14); PgrTheme.Tabular(listeningSleep);
        foreach (var fit in new[] { listeningSpeed, listeningSleep }) { fit.SetSingleLine(true); fit.SetMinWidth(0); fit.SetMinimumWidth(0); fit.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; }
        var smartResume = new Switch(this) { Text = "智能续听", Checked = player.SmartResumeEnabled, TextSize = 14 };
        PgrTheme.StyleSwitch(smartResume);
        smartResume.CheckedChange += (_, e) => player.SetSmartResume(e.IsChecked); settings.AddView(smartResume, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(2) });
        var smartHelp = PgrTheme.Apply(Text("暂停满 5 分钟后，从本句开头继续；短暂停留保持原位置。手动定位和恢复书签仍按你选的位置播放。", 12), PgrTheme.Role.Caption);
        settings.AddView(smartHelp);
        PgrUi.Style(Button(settings, "角色音量", ShowSpeakerVolumes), Xb.Normal, "volume", "VOICE VOLUME", tail: true);

        var bookmarkHeading = Row(content); bookmarkHeading.SetGravity(GravityFlags.Bottom);
        bookmarkHeading.AddView(PgrTheme.SectionHeader(this, "我的书签", "BOOKMARKS", CompactLayout), new LinearLayout.LayoutParams(0, -2, 1));
        var manage = new Button(this) { Text = "全部 / 管理", TextSize = 12 }; PgrUi.Style(manage, Xb.Normal, textSp: 12); manage.SetSingleLine(true); manage.SetPadding(Dp(14), 0, Dp(14), 0); manage.Click += (_, _) => Safe(ManageListeningBookmarks); bookmarkHeading.AddView(manage, new LinearLayout.LayoutParams(-2, Dp(PgrTheme.Touch)) { LeftMargin = Dp(10), BottomMargin = Dp(CompactLayout ? 4 : 8) });
        listeningBookmarks = new LinearLayout(this) { Orientation = Orientation.Vertical }; content.AddView(listeningBookmarks);
        PgrUi.Style(Button(content, "停止听书并保留位置", player.Stop), Xb.DangerOutline, "close", "STOP");
        PgrUi.Style(Button(content, "听书使用说明", () => Info("听书使用说明", "按大章节收听，章内小节连续衔接，到大章末尾停止。\n\n小节台词目录显示完整正文，翻阅或选中台词不影响收听。点“定位到选中台词”后暂停，点播放再继续；“回到正在听”只返回当前句的显示位置。待选分支的后文可以阅读，需要打开选项后再收听。\n\n听书进度和书签独立保存，不会改变游戏中的剧情进度。重新打开、跳转小节或恢复书签后，点播放才会发声。\n\n分支可设为全部听、手动选择或默认分支（第一或第二个选项，在分支页设置，与游戏配音共用）。分支的出口或正文尚未逐句核实时，默认分支和手动选择按配音包现有连接继续收听（状态行标“现稿续接”），只在包内确实没有下一句处暂停。手动选择遇到分支会暂停，回到听书页点选后继续。全部听仅依据配音包已有的路线内容，分支衔接以包内资料为准。\n\n支持后台收听、倍速与定时关闭。通知栏可控制播放；系统限制后台活动时，请在手机的应用电池设置中允许后台运行。\n\n听书无需屏幕捕获、悬浮窗或无障碍权限。游戏配音和听书共用音频输出，开始一种播放时会暂停另一种。")), Xb.Normal, "story", "GUIDE", tail: true);
        content.AddView(new View(this) { Background = new ArchiveFooterDrawable(Resources!.DisplayMetrics!.Density), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(-1, Dp(150)) { TopMargin = Dp(8) });
        UpdateListeningPage();
        QueueListeningUiTick();
    }

    void ListeningHero()
    {
        if (session.Listening.Pack != null && session.Listening.Chapter != null) { ListeningCoverBand(); return; }
        if (CompactLayout)
        {
            Header("听书 · LISTENING", session.Listening.Chapter?.Title ?? "故事，在耳边继续");
            return;
        }
        var hero = new FrameLayout(this); int height = CompactLayout ? 116 : 150;
        // Same Canvas HUD plate as the archive; never a waveform or fake playback level.
        var heroDrawable = new HudHeroDrawable(Resources!.DisplayMetrics!.Density, "", "AUDIO//ARCHIVE · OFFLINE"); hero.Background = heroDrawable;
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical }; body.SetPadding(Dp(20), Dp(16), Dp(14), Dp(14));
        var caption = PgrTheme.Eyebrow(this, "声音档案  /  离线听书", PgrTheme.Steel); caption.SetAllCaps(false); caption.SetPadding(0, 0, 0, Dp(8)); body.AddView(caption);
        var heading = PgrTheme.Apply(Text(session.Listening.Chapter?.Title ?? "故事，在耳边继续", CompactLayout ? 22 : 25), PgrTheme.Role.Display); heading.TextSize = CompactLayout ? 22 : 25; heading.SetMaxLines(2); heading.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; heading.SetPadding(0, 0, 0, Dp(6)); body.AddView(heading);
        var detail = PgrTheme.Apply(Text(session.Listening.Chapter is { } chapter ? $"{SectionDisplay.Groups(chapter.Sections).Count:00} 个小节  /  按顺序连续播放" : "选一章 · 戴上耳机 · 从上回继续", 11), PgrTheme.Role.Code, PgrTheme.Secondary); detail.SetPadding(0, 0, 0, 0); body.AddView(detail);
        hero.AddView(body, new FrameLayout.LayoutParams(-1, -1)); content.AddView(hero, new LinearLayout.LayoutParams(-1, Dp(height)) { TopMargin = Dp(6), BottomMargin = Dp(6) });
    }

    void ListeningPlaybackControls(LinearLayout parent)
    {
        // Node track (desktop v7c-2): prev / next as diamond nodes on a thin rail, play/pause as a centred crimson hex plate.
        float density = Resources!.DisplayMetrics!.Density;
        int height = CompactLayout ? 66 : 74;
        var track = new LinearLayout(this) { Orientation = Orientation.Horizontal, Background = new NodeTrackDrawable(density) { RailDp = 25 } };
        track.SetPadding(Dp(4), 0, Dp(4), 0);
        Button Node(string label, string icon, Action action)
        {
            var node = new Button(this) { Text = label, TextSize = 11 };
            node.Background = new DiamondNodeDrawable(density, icon) { CenterDp = 25 };
            node.BackgroundTintList = null; node.StateListAnimator = null; node.Elevation = 0; node.SetAllCaps(false);
            node.SetTextColor(PgrTheme.Secondary); node.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal);
            node.Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal; node.SetPadding(0, 0, 0, Dp(3));
            node.SetMinHeight(Dp(height)); node.SetMinimumHeight(Dp(height)); node.SetMinWidth(0); node.SetMinimumWidth(0);
            node.Click += (_, _) => Safe(action);
            track.AddView(node, new LinearLayout.LayoutParams(0, Dp(height), 1));
            return node;
        }
        Node("上一句", "previous", session.Listening.Previous);
        listeningPlay = new Button(this) { Text = "播放", TextSize = 14 }; listeningPlayPauseIcon = null;
        listeningPlay.Background = new HexPlateDrawable(density) { CenterDp = 25 };
        listeningPlay.BackgroundTintList = null; listeningPlay.StateListAnimator = null; listeningPlay.Elevation = 0; listeningPlay.SetAllCaps(false);
        listeningPlay.SetTextColor(new global::Android.Content.Res.ColorStateList(
            new[] { new[] { -global::Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
            new[] { PgrTheme.Foreground.ToArgb(), PgrTheme.OnAccent.ToArgb() }));
        listeningPlay.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        listeningPlay.Gravity = GravityFlags.Center; listeningPlay.SetPadding(Dp(18), 0, Dp(18), Dp(height - 50));
        listeningPlay.SetMinHeight(Dp(height)); listeningPlay.SetMinimumHeight(Dp(height)); listeningPlay.SetMinWidth(0); listeningPlay.SetMinimumWidth(0);
        listeningPlay.Click += (_, _) => Safe(ToggleListeningPlayback);
        SetListeningPlayIcon(false);
        track.AddView(listeningPlay, new LinearLayout.LayoutParams(0, Dp(height), 1.5f) { LeftMargin = Dp(6), RightMargin = Dp(6) });
        Node("下一句", "next", session.Listening.Next);
        parent.AddView(track, new LinearLayout.LayoutParams(-1, Dp(height)) { TopMargin = Dp(4), BottomMargin = Dp(6) });
    }

    void SetListeningPlayIcon(bool playing)
    {
        if (listeningPlay == null) return;
        listeningPlay.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable(playing ? "pause" : "play", PgrTheme.OnAccent, Resources!.DisplayMetrics!.Density, 16), null, null, null);
        listeningPlay.CompoundDrawablePadding = Dp(7);
    }

    void UpdateListeningPage()
    {
        var player = session.Listening;
        if (listeningPosition == null) return;
        UpdateListeningDirectory();
        listeningPosition.Text = player.PositionText;
        var displayNode = player.PreviewNode ?? (player.HasBlockingNotice ? null : player.Current);
        var trayStop = displayNode == null ? StopTrayText(player) : null;
        listeningSpeaker!.Text = displayNode is { Kind: "line" } node ? (string.IsNullOrWhiteSpace(node.Speaker) ? "旁白" : node.Speaker) : player.Choices.Count > 0 ? BranchTrayPlate(player) : trayStop?.Plate ?? (player.HasBlockingNotice ? "等待确认续接" : "声音档案");
        UpdateListeningSpeakerDecor(displayNode is { Kind: "line" } speakerNode ? speakerNode.Speaker : null);
        listeningDialogue!.Text = displayNode?.Text ?? trayStop?.Text ?? (player.HasBlockingNotice ? player.CurrentItem?.Notice : "选择一段故事，准备开始收听。");
        listeningStatus!.Text = player.Choices.Count == 0 && player.PendingStop == null && player.IsPlaying && player.Explore is { } ex
            ? $"正在收听 · 探索菜单自动读 · 已听 {ex.HeardCount} / {ex.LoopCount}" : player.Status;
        listeningResume!.Text = player.ResumeText;
        string playText = player.PlayCaption; bool pauseIcon = player.PlayShowsPauseIcon;
        if (listeningPlay!.Text != playText || listeningPlayPauseIcon != pauseIcon) { listeningPlay.Text = playText; listeningPlayPauseIcon = pauseIcon; SetListeningPlayIcon(pauseIcon); }
        listeningPlay.ContentDescription = player.PlayTogglesCountdown ? (player.CountdownPaused ? "继续倒计时" : "暂停倒计时，不自动续接") : playText;
        listeningPlay.Enabled = player.PlayEnabled;
        listeningBookmark!.Enabled = !player.IsPreviewing && !player.HasBlockingNotice && player.Current?.Kind == "line";
        listeningPolicy!.Text = "分支 · " + ListeningPolicyLabel(player.Policy);
        listeningSpeed!.Text = $"倍速 · {player.Speed:0.##}×";
        UpdateListeningProgress();
        UpdateListeningMini(displayNode is { Kind: "line" } miniNode ? miniNode.Speaker : null, listeningDialogue.Text ?? "", player.Choices.Count > 0 && !player.IsPreviewing, player.Choices.Count == 0 ? trayStop?.Plate : null);

        string choicesKey = string.Join("\u001f", player.Choices.Select(c => c.Id + "\u001e" + c.Label)) + "\u001d" + (WideLandscape ? player.OptionDurationVersion : 0) + "\u001d" + (player.PendingStop is { Action: not ListeningStopAction.Choose } ps ? ps.Action.ToString() + ps.SectionComplete : "")
            + "\u001d" + player.CountdownOptionId + "\u001d" + (player.CountdownRemaining > 0) + "\u001d" + player.AutoNotice;
        string? voiceTag = player.IsPreviewing ? null : player.VoiceTagFor(displayNode);
        choicesKey += "\u001d" + voiceTag;
        var explore = player.Choices.Count == 0 && player.PendingStop == null ? player.Explore : null;
        if (explore != null) choicesKey += "\u001dX" + string.Join(",", explore.Rows.Select(r => (r.Heard ? 1 : 0) + (r.Playing ? 2 : 0))) + explore.Line;
        if (renderedListeningChoices != choicesKey)
        {
            renderedListeningChoices = choicesKey; listeningChoices!.RemoveAllViews();
            listeningCountdownBadges.Clear(); listeningCountdownRails.Clear(); listeningPauseRows.Clear(); listeningPauseToggles.Clear(); listeningNoticeShown = player.AutoNotice != null;
            if (player.AutoNotice is { } notice)
                listeningChoices.AddView(ListeningAutoNotice(player, notice), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
            if (player.Choices.Count > 0 && WideLandscape && player.ChoiceDetail != null) ListeningBranchCardWide(listeningChoices);
            else if (player.Choices.Count > 0) { ListeningBranchCard(listeningChoices, player); RevealListeningBranchCard(player); }
            else if (player.PendingStop is { Action: not ListeningStopAction.Choose } stop)
            {
                // 0.4.7（I/L，M）：未核续接 / 小节已读完 / 无文本死角——黄铜 F-B 纸面卡：铭文 + 倒计时 + 细轨 + 操作。
                ListeningStopCard(listeningChoices, player, stop);
            }
            else if (explore != null)
                listeningChoices.AddView(ListeningExploreCard(explore), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
            else if (voiceTag != null)
                listeningChoices.AddView(ListeningVoiceCard(voiceTag), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
        }
        UpdateListeningCountdown(player);
        UpdateListeningVoiceTag(displayNode);
        UpdateListeningDwell(voiceTag);
        string bookmarksKey = string.Join("\u001f", player.Bookmarks.Select(b => b.Id + "\u001e" + b.Label + "\u001e" + b.PositionText));
        // Include an empty-state sentinel so the first render still displays a helpful message.
        bookmarksKey = player.Bookmarks.Count + ":" + bookmarksKey;
        if (renderedListeningBookmarks != bookmarksKey)
        {
            renderedListeningBookmarks = bookmarksKey; listeningBookmarks!.RemoveAllViews();
            if (player.Bookmarks.Count == 0)
            { var empty = Text("还没有手动书签。点“记下这句”，保留想再听的一刻。", 13); empty.SetTextColor(PgrTheme.Muted); listeningBookmarks.AddView(empty); }
            foreach (var bookmark in player.Bookmarks.Take(5))
            {
                string id = bookmark.Id;
                var button = Button(listeningBookmarks, bookmark.Label + "\n" + bookmark.PositionText, () => OpenListeningBookmark(id));
                button.TextSize = 13; ButtonIcon(button, "bookmark");
            }
            if (player.Bookmarks.Count > 5)
            { var more = PgrTheme.Apply(Text($"另有 {player.Bookmarks.Count - 5} 个书签，点“全部 / 管理”查看。", 12), PgrTheme.Role.Caption); listeningBookmarks.AddView(more); }
        }
    }

    void ResumeListeningUi() { listeningUiVisible = true; QueueListeningUiTick(); }
    void PauseListeningUi()
    {
        listeningUiVisible = false;
        if (listeningProgressTick != null) pageScroll?.RemoveCallbacks(listeningProgressTick);
        listeningTickScheduled = false;
    }
    void QueueListeningUiTick()
    {
        if (!alive || !listeningUiVisible || page != 5 || listeningTime == null || listeningTickScheduled) return;
        listeningProgressTick ??= () => { listeningTickScheduled = false; if (!alive || !listeningUiVisible || page != 5) return; UpdateListeningProgress();
            // 0.4.7（M）：17-A 提示条约 6 秒后收起。
            if (listeningNoticeShown && session.Listening.AutoNotice == null) UpdateListeningPage();
            QueueListeningUiTick(); };
        listeningTickScheduled = true; pageScroll.PostDelayed(listeningProgressTick, 1000);
    }
    static string ListeningClock(long milliseconds)
    { long seconds = Math.Max(0, milliseconds) / 1000; return $"{seconds / 60:00}:{seconds % 60:00}"; }
    void UpdateListeningProgress()
    {
        if (listeningTime == null || listeningSeek == null) return;
        var player = session.Listening;
        long duration = player.DurationMilliseconds, position = player.PositionMilliseconds;
        if (listeningRemainingTime != null) listeningRemainingTime.Text = player.RemainingTimeText;
        if (!listeningSeeking)
        {
            listeningSeek.Enabled = !player.IsPreviewing && !player.HasBlockingNotice && duration > 0;
            listeningSeek.Progress = duration > 0 ? (int)Math.Clamp(position * 1000 / duration, 0, 1000) : 0;
            if (listeningWave != null) { listeningWave.SetSeed(player.Current?.Id.ToString()); listeningWave.Progress = listeningSeek.Progress / 1000f; }
            if (listenMiniProgress != null) listenMiniProgress.ScaleX = listeningSeek.Progress / 1000f;
            listeningTime.Text = "本句  " + ListeningClock(position) + (duration > 0 ? " / " + ListeningClock(duration) : " · 播放后可拖动进度");
            if (player.IsPreviewing) listeningTime.Text = "单句试听中 · 结束后停住，原位置保持";
        }
        if (listeningSleep != null) listeningSleep.Text = "定时 · " + (player.SleepText == "定时关闭未开启" ? "未开启" : player.SleepText);
    }

    void ToggleListeningPlayback()
    {
        // 0.4.8（设计稿 16）：倒计时中主按钮与卡片「暂停 / 继续」同一操作（ListeningSession.PlayButton → ToggleCountdownPause）。
        var player = session.Listening;
        if (!player.PlayTogglesCountdown && !player.IsPlaying) RequestNotifications();
        player.PlayButton();
    }

    void ChooseListeningChapter()
    {
        var packages = session.Packages.List();
        var categories = ChapterCategories(packages);
        Choose("选择分类", categories, category =>
        {
            var chapters = packages.Where(p => ChapterCatalog.Category(p.PackId, p.Title) == categories[category]).ToArray();
            if (chapters.Length == 0) { Info(categories[category], "暂无章节"); return; }
            Choose(categories[category], chapters.Select(p => PackTitle(p.Title)).ToArray(), i =>
            {
                var pack = session.Packages.Load(chapters[i].PackId);
                if (pack.Chapters.Count == 0) { Info("没有章节", "这个配音包中没有可收听的大章节。"); return; }
                void OpenChapter(int index) { session.Listening.Open(pack.Id, pack.Chapters[index].Id); ShowPage(5); }
                if (pack.Chapters.Count == 1) OpenChapter(0);
                else Choose("选择大章节", pack.Chapters.Select(c => c.Title).ToArray(), OpenChapter);
            });
        });
    }

    void ShowListeningSections()
    {
        var player = session.Listening;
        if (player.Chapter == null) return;
        var owner = player.Pack; var chapter = player.Chapter;
        var sections = player.Chapter.Sections;
        ChooseDisplayedSection(owner!, sections, "浏览小节 · 不改变收听位置",
            () => ReferenceEquals(owner, player.Pack) && ReferenceEquals(chapter, player.Chapter), section =>
        {
            listeningBrowsePinned = true; listeningBrowseSection = section.Id; listeningSelectedKey = null;
            UpdateListeningDirectory(true); ScrollToListeningDirectory();
        }, listeningBrowseSection ?? player.CurrentItem?.SectionId);
    }

    static string ListeningPolicyLabel(ListeningBranchPolicy policy) => policy switch
    {
        ListeningBranchPolicy.All => "全部听取", ListeningBranchPolicy.Manual => "手动选择",
        ListeningBranchPolicy.Second => "默认第二个", _ => "默认第一个"
    };

    // “默认分支”的第一 / 第二个选项在分支页设置，与游戏配音共用；只有一个选项时按第一个。
    void ChooseListeningPolicy() => Choose("遇到支线怎么听", new[] { "全部听取：依次收听可用分支", "手动选择：遇分支暂停，点选后继续",
        UiText.ListeningDefaultBranchLabel(session.Settings.DefaultBranchOption) }, i =>
        session.Listening.SetPolicy(i == 0 ? ListeningBranchPolicy.All : i == 1 ? ListeningBranchPolicy.Manual : session.Listening.DefaultPolicy));

    void ChooseListeningSpeed()
    {
        float[] speeds = { .75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
        Choose("收听倍速", speeds.Select(s => $"{s:0.##}×" + (s == 1 ? " · 原速" : "")).ToArray(), i => session.Listening.SetSpeed(speeds[i]));
    }

    void ChooseListeningSleep()
    {
        int[] minutes = { 0, 15, 30, 45, 60, 90 };
        Choose("定时关闭", minutes.Select(m => m == 0 ? "关闭定时" : $"{m} 分钟后暂停").ToArray(), i => session.Listening.SetSleepMinutes(minutes[i]));
    }

    void AddListeningBookmark()
    {
        if (session.Listening.Current?.Kind != "line") return;
        // Pin the displayed sentence while the name dialog is open; natural completion
        // must not silently attach the bookmark to a later line.
        session.Listening.Pause();
        var node = session.Listening.Current;
        string excerpt = node.Text.Length > 24 ? node.Text[..24] + "…" : node.Text;
        Prompt("记下这一句 · 已暂停", (string.IsNullOrWhiteSpace(node.Speaker) ? "" : node.Speaker + " · ") + excerpt, label =>
        {
            int before = session.Listening.Bookmarks.Count;
            session.Listening.AddBookmark(label);
            if (session.Listening.Bookmarks.Count > before) Toast.MakeText(this, "已记下这句，可在听书页“我的书签”里找到", ToastLength.Short)?.Show();
        });
    }

    void ManageListeningBookmarks()
    {
        var bookmarks = session.Listening.Bookmarks.ToArray();
        Choose("本章听书书签", bookmarks.Select(b => b.Label + "\n" + b.PositionText).ToArray(), i => OpenListeningBookmark(bookmarks[i].Id));
    }

    void OpenListeningBookmark(string id)
    {
        var bookmark = session.Listening.Bookmarks.FirstOrDefault(b => b.Id == id);
        if (bookmark == null) return;
        Choose(bookmark.Label, new[] { "恢复到这里（暂停，点播放再继续）", "删除这个书签" }, i =>
        {
            if (i == 0) session.Listening.RestoreBookmark(id);
            else Confirm("删除听书书签", bookmark.Label + "\n" + bookmark.PositionText, () => session.Listening.RemoveBookmark(id));
        });
    }
}
