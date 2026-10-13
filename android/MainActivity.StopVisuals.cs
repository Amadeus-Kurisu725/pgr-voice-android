using Android.Graphics;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Listening;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.7（M）听书停下处的视觉：倒计时铭文与黄铜细轨、将自动选择的选项高亮、未核续接 / 小节已读完 / 无文本死角卡、
/// 无配音 / 待配音签。样式全部取自 SPEC.md 既有语言（F-B 黄铜纸面卡、10-A3 按钮、17-A 提示条）。
/// </summary>
public partial class MainActivity
{
    View CountdownBadgeView(int seconds, string caption = "秒后自动选择", bool onDark = false)
    {
        var badge = new CountdownBadge(this, caption, onDark);
        badge.Update(seconds, session.Listening.CountdownFraction, session.Listening.CountdownPaused);
        listeningCountdownBadges.Add(badge);
        return badge;
    }

    View CountdownRailRow(float fraction, string caption, bool onDark = false)
    {
        var rail = new CountdownRail(this, caption, onDark);
        rail.Update(fraction, session.Listening.CountdownPaused);
        listeningCountdownRails.Add(rail);
        return rail;
    }

    /// <summary>0.4.8（设计稿 01 / 16）：「暂停 / 继续」说明行；点键冻结或恢复当前倒计时。</summary>
    View CountdownPauseRow()
    {
        var row = new PauseRow(this);
        row.Update(session.Listening.CountdownPaused);
        row.Toggle.Click += (_, _) => Safe(() => session.Listening.ToggleCountdownPause());
        listeningPauseRows.Add(row);
        return row;
    }

    /// <summary>旧接口保留：将被自动选择的选项下挂倒计时细轨（宽屏卡仍用自身行样式）。</summary>
    void MarkAutoOption(LinearLayout card, View option, string caption = "AUTO-PICK · 将自动选择")
    {
        int at = card.IndexOfChild(option);
        card.AddView(CountdownRailRow(session.Listening.CountdownFraction, caption), at + 1, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = Dp(4), RightMargin = Dp(4), TopMargin = Dp(4), BottomMargin = Dp(6) });
    }

    /// <summary>
    /// 0.4.8 竖屏待选支线卡（设计稿 01 / 03 / 16）：F-B 黄铜纸面卡（结局分歧为深色酒红卡头）、倒计时环、标题、分支前一句、
    /// 编号选项片（默认项黄铜 + DEFAULT / ENDING 尾标 + 细轨）、说明行、「暂停 / 继续」行；结局分歧另有「只读这一结局」锁定说明。
    /// </summary>
    void ListeningBranchCard(LinearLayout parent, ListeningSession player)
    {
        bool counting = player.CountdownRemaining > 0, ending = player.ChoiceIsEnding;
        var detail = player.ChoiceDetail;
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = PromptArt.BrassCard(this);
        string caption = ending ? "秒后选入结局" : "秒后自动选择";
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        body.SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
        if (ending)
        {
            var head = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var deco = new EndingHeadDrawable(Resources!.DisplayMetrics!.Density); head.Background = deco;
            head.SetPadding(Dp(16), Dp(10), Dp(12), Dp(14));
            var top = new LinearLayout(this) { Orientation = Orientation.Horizontal }; top.SetGravity(GravityFlags.CenterVertical);
            top.AddView(PgrUi.En(this, "ENDING · 结局分歧", 9.5f, PromptArt.EndingPink), new LinearLayout.LayoutParams(0, -2, 1));
            if (counting) top.AddView(CountdownBadgeView(player.CountdownRemaining, caption, true));
            head.AddView(top);
            var t = PromptArt.Label(this, counting ? "此处是结局分歧 · 只收听所选结局" : "此处是结局分歧 · 选择后只收听所选结局", 18, Color.White, true);
            t.LetterSpacing = .04f;
            head.AddView(t, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8) });
            if (detail is { PreviousLine.Length: > 0 })
            {
                // 设计稿 03：分支前一句在深色卡头内，说话人白色、正文暖灰。
                var said = new TextView(this) { TextSize = 12 }; said.SetLineSpacing(Dp(2), 1); said.SetMaxLines(3); said.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                said.TextFormatted = PromptArt.SpeakerLine(detail.PreviousSpeaker, detail.PreviousLine, Color.White, Color.ParseColor("#D8C9B0"));
                head.AddView(said, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
            }
            card.AddView(head, new LinearLayout.LayoutParams(-1, -2));
        }
        else
        {
            var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
            head.AddView(PgrUi.En(this, "BRANCH · 待选互动", 9.5f, PgrTheme.BrassText), new LinearLayout.LayoutParams(0, -2, 1));
            if (counting) head.AddView(CountdownBadgeView(player.CountdownRemaining, caption));
            body.AddView(head);
            var heading = PromptArt.Label(this, counting ? "分支自动选择 · 倒计时结束按默认项继续" : "分支已暂停 · 选择后继续", 16, PromptArt.Ink, true);
            body.AddView(heading, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        }
        if (!ending && detail is { PreviousLine.Length: > 0 })
        {
            var prompt = new TextView(this) { TextSize = 13 }; prompt.SetLineSpacing(Dp(2), 1); prompt.SetMaxLines(3); prompt.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
            prompt.TextFormatted = PromptArt.SpeakerLine(detail.PreviousSpeaker, detail.PreviousLine, PromptArt.Ink, PgrTheme.Secondary);
            body.AddView(prompt, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
        }
        else body.AddView(new View(this), new LinearLayout.LayoutParams(1, Dp(6)));
        var choices = player.Choices;
        for (int i = 0; i < choices.Count; i++)
        {
            string id = choices[i].Id, label = choices[i].Label; bool auto = id == player.CountdownOptionId;
            bool endingOption = label.Contains("开启结局");
            var tile = OptionTiles.Tile(this, i, label, auto, auto ? (endingOption ? OptionTail.Ending : OptionTail.Default) : OptionTail.Chevron, PromptArt.EndingName(label));
            tile.Click += (_, _) => Safe(() => { RequestNotifications(); player.Choose(id); });
            body.AddView(tile, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(auto ? 0 : 6) });
            if (auto) MarkAutoOption(body, tile, ending ? "AUTO-PICK · 将选入结局" : "AUTO-PICK · 将自动选择");
        }
        if (ending)
        {
            var lockRow = OptionTiles.Hint(this, "lock", PromptArt.EndingRed, "选入结局后只读这一结局，读完即停：「" + ListeningText.EndingFinished + "」", 11.5f);
            lockRow.Background = PromptArt.NoteBand(this, PgrTheme.A(PromptArt.EndingRed, 14), PromptArt.EndingRed); lockRow.SetPadding(Dp(8), Dp(6), Dp(8), Dp(6));
            body.AddView(lockRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), BottomMargin = Dp(6) });
        }
        else
        {
            string hint = counting ? "点任一选项立即选择；默认分支在分支页设定（当前：" + DefaultBranchOrdinal() + "）" : "点任一选项继续收听；分支自动选择已关闭，可在分支页开启。";
            body.AddView(OptionTiles.Hint(this, "branch", PgrTheme.Brass, hint), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), BottomMargin = Dp(6) });
        }
        if (counting) body.AddView(CountdownPauseRow(), new LinearLayout.LayoutParams(-1, -2));
        card.AddView(body, new LinearLayout.LayoutParams(-1, -2));
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
    }

    string DefaultBranchOrdinal() => ListeningText.OptionOrdinal(session.Settings.DefaultBranchOption);

    /// <summary>0.4.8 自动处理提示条（设计稿 02 / 11 THEN）：铭文 + 主文 + 副行，右侧 ✓「DONE · 已选择」。</summary>
    View ListeningAutoNotice(ListeningSession player, string notice)
    {
        bool feedback = notice.StartsWith("已记录反馈");
        string en = player.AutoNoticeEnglish;
        string icon = feedback ? "feedback" : en.EndsWith("SELECTED") ? "branch" : en.StartsWith("ROUTE · IN ORDER") || en.StartsWith("SECTION") ? "next" : "branch";
        var row = StopVisuals.Notice(this, notice, icon, en);
        // 设计稿 02：左块 52dp、主文 15sp、最小高 52dp（StopVisuals.Notice 是游戏模式共用的 44dp 版，这里只放大听书页这一条）。
        row.SetMinimumHeight(Dp(52));
        if (row.ChildCount > 0 && row.GetChildAt(0)?.LayoutParameters is LinearLayout.LayoutParams tileLp) { tileLp.Width = Dp(52); tileLp.RightMargin = Dp(12); }
        if (row.ChildCount > 1 && row.GetChildAt(1) is LinearLayout texts)
        {
            if (texts.ChildCount > 1 && texts.GetChildAt(1) is TextView main) { main.TextSize = 15; main.SetLineSpacing(Dp(1), 1); }
            if (player.AutoNoticeDetail.Length > 0)
            {
                var sub = new TextView(this) { Text = player.AutoNoticeDetail, TextSize = 11.5f }; sub.SetTextColor(PgrTheme.Secondary); sub.SetSingleLine(true); sub.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                texts.AddView(sub, new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(1) });
            }
        }
        if (player.AutoNoticeEnglish == "AUTO · SELECTED" && PageWidthDp >= 360)
        {
            var done = new CountdownBadge(this, "");
            done.ShowDone();
            row.AddView(done, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
        }
        return row;
    }

    /// <summary>
    /// 0.4.8 停下卡（设计稿 04 无文本死角 / 11 分支续接未核实 / 小节已读完）：F-B 黄铜纸面卡、倒计时环、标题、说明、细轨、操作键、
    /// 「暂停 / 继续」行；死角卡底部记录说明。
    /// </summary>
    void ListeningStopCard(LinearLayout parent, ListeningSession player, ListeningStop stop)
    {
        bool dead = stop.DeadEnd, counting = player.CountdownRemaining > 0, order = stop.Action == ListeningStopAction.OrderContinue;
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = PromptArt.BrassCard(this);
        card.SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
        string action = stop.Action switch
        {
            ListeningStopAction.OrderContinue => "按顺序继续",
            ListeningStopAction.NextSection when stop.SectionComplete => "进入下一小节",
            ListeningStopAction.NextSection => "跳到下一小节",
            _ => "结束本章",
        };
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        string eyebrow = order ? "ROUTE · 续接未核实" : stop.SectionComplete ? "SECTION · 小节已读完" : "END OF TEXT · 后续没有配音文本";
        head.AddView(PgrUi.En(this, eyebrow, 9.5f, PgrTheme.BrassText), new LinearLayout.LayoutParams(0, -2, 1));
        if (counting) head.AddView(CountdownBadgeView(player.CountdownRemaining, order ? "秒后按顺序继续" : "秒后" + action));
        card.AddView(head);
        string title = stop.Action switch
        {
            ListeningStopAction.OrderContinue => "此处分支续接未核实",
            _ when stop.SectionComplete => "本小节已读完 · 出口未核实",
            ListeningStopAction.NextSection => ListeningText.DeadEndHint,
            _ => ListeningText.DeadEndChapterEnd,
        };
        card.AddView(PromptArt.Label(this, title, 16, PromptArt.Ink, true), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        string detail = counting
            ? (order ? "倒计时结束后从本节下一句接着听，并显示「" + ListeningText.OrderContinued + "」。" : "倒计时结束后" + action + "。")
            : "分支自动选择已关闭，停在这里等待；点「" + action + "」继续。";
        card.AddView(PromptArt.Label(this, detail, 12, PgrTheme.Secondary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
        if (counting) card.AddView(CountdownRailRow(player.CountdownFraction, order ? "IN ORDER · 按顺序" : "NEXT · " + action), new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        card.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        // 设计稿 04 / 11 / 16：「按顺序继续」为黄铜主操作；死角 / 小节已读完的「跳到下一小节」为白底次级键，「反馈」黄铜描边。
        var go = new Button(this) { Text = action }; PgrUi.Style(go, order ? Xb.Brass : Xb.Normal, "next");
        go.Click += (_, _) => Safe(() => player.ApplyPendingStop());
        row.AddView(go, new LinearLayout.LayoutParams(0, Dp(48), 1.25f));
        if (order)
        {
            var next = new Button(this) { Text = "下一句" }; PgrUi.Style(next, Xb.Normal, "next");
            next.Click += (_, _) => Safe(() => player.Next());
            row.AddView(next, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(6) });
        }
        if (player.CanFeedback)
        {
            var fb = new Button(this) { Text = "反馈" }; PgrUi.Style(fb, Xb.Brass, "feedback"); fb.SetTextColor(PgrTheme.BrassText);
            fb.Click += (_, _) => Safe(player.Feedback);
            fb.ContentDescription = "反馈：记录这里后续没有配音文本，游戏里可能还有内容";
            row.AddView(fb, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(6) });
        }
        if (counting) card.AddView(CountdownPauseRow(), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8) });
        if (order) card.AddView(OptionTiles.Hint(this, "next", PgrTheme.Slate, "「下一句」始终可用", 11), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        else if (dead) card.AddView(OptionTiles.Hint(this, "feedback", PgrTheme.Brass, "反馈会记录这一处：后续没有配音文本，游戏里可能还有内容", 11), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        if (dead) card.ContentDescription = title;
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
    }

    void UpdateListeningCountdown(ListeningSession player)
    {
        int seconds = player.CountdownRemaining; float fraction = player.CountdownFraction; bool paused = player.CountdownPaused;
        foreach (var badge in listeningCountdownBadges) badge.Update(seconds, fraction, paused);
        foreach (var rail in listeningCountdownRails) rail.Update(fraction, paused);
        foreach (var row in listeningPauseRows) row.Update(paused);
        foreach (var toggle in listeningPauseToggles) toggle.Update(paused);
    }

    LinearLayout? listeningDwellRow; TextView? listeningDwellLabel, listeningDwellClock; CountdownRail? listeningDwellRail;
    bool listeningDwellTicking;

    /// <summary>0.4.8 设计稿 05：托盘停留行；停留时长 / 已停留取自 ListeningSession.Dwell（实际停留计时），界面只刷新显示。</summary>
    void UpdateListeningDwell(string? tag)
    {
        if (listeningDwellRow == null) return;
        var dwell = tag == null ? null : session.Listening.Dwell;
        if (dwell is not { } d) { listeningDwellRow.Visibility = ViewStates.Gone; return; }
        listeningDwellRow.Visibility = ViewStates.Visible;
        listeningDwellLabel!.Text = "◆ " + tag + " · 字幕停留后自动继续";
        listeningDwellRail!.Update(1f - (float)d.Elapsed / d.Total, false);
        listeningDwellClock!.Text = $"字幕停留 {d.Elapsed / 1000.0:00.0} / {d.Total / 1000.0:00.0} 秒";
        listeningDwellRow.ContentDescription = tag + "，字幕停留后自动继续，已停留 " + (d.Elapsed / 1000.0).ToString("0.0") + " 秒，共 " + (d.Total / 1000.0).ToString("0.0") + " 秒";
        if (listeningDwellTicking) return;
        listeningDwellTicking = true;
        var row = listeningDwellRow;
        row.PostDelayed(() => { listeningDwellTicking = false; if (alive && ReferenceEquals(row, listeningDwellRow) && page == 5) UpdateListeningDwell(session.Listening.VoiceTagFor(session.Listening.Current)); }, 100);
    }

    /// <summary>
    /// 0.4.8 设计稿 12：探索菜单逐项读卡（EXPLORE · 探索菜单逐项读 / 已听 x / n / 菜单文字 / 说明 / 行：已听 · 正在收听 · 未听 · THEN 出口最后读）。
    /// 行样式见设计稿 15（Ui/ExploreRows.cs）；只读显示，不提供选择（逐项读由计划自动进行）。不显示主线 / 闲聊标识。
    /// </summary>
    View ListeningExploreCard(ListeningExploreView view)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.White, 245), PgrTheme.BorderStrong, PgrTheme.CutMedium, Corners.Diagonal, PromptArt.SlateBlue, AccentEdge.Left),
            new FusionDeco(density, DecoTier.S) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium });
        card.SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(PgrUi.En(this, "EXPLORE · 探索菜单逐项读", 9.5f, PromptArt.SlateBlue), new LinearLayout.LayoutParams(0, -2, 1));
        var count = PromptArt.Label(this, $"已听 {view.HeardCount} / {view.LoopCount}", 11.5f, PromptArt.DoneTeal, true, 1);
        head.AddView(count);
        card.AddView(head);
        if (!string.IsNullOrWhiteSpace(view.MenuText))
            card.AddView(PromptArt.Label(this, view.MenuText, 15, PromptArt.Ink, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(3) });
        card.AddView(PromptArt.Label(this, "每位人物的对话读一次，读完打勾；离开选项最后读。", 11.5f, PgrTheme.Secondary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        var exits = view.Rows.Where(r => r.Exit).ToList();
        foreach (var r in view.Rows.Where(r => !r.Exit))
        {
            var state = r.Playing ? ExploreRowState.Playing : r.Heard ? ExploreRowState.Heard : ExploreRowState.Unheard;
            string sub = r.Playing ? "▶ 正在收听 · " + (string.IsNullOrWhiteSpace(view.Speaker) ? "" : view.Speaker + "：") + view.Line
                : (r.Lines > 0 ? r.Lines + " 句 · " : "") + (r.Heard ? "已读完" : "按列出顺序");
            card.AddView(ExploreRows.Row(this, ExploreRowKind.Chat, state, r.Label, sub, r.Playing ? "收听中" : r.Heard ? "✓ 已听" : "未听"),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        }
        if (exits.Count > 0)
        {
            card.AddView(PgrUi.En(this, "THEN · 最后", 8.5f, PgrTheme.BrassText), new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(8) });
            int rest = view.LoopCount - view.HeardCount;
            foreach (var r in exits)
                card.AddView(ExploreRows.Row(this, ExploreRowKind.Exit, ExploreRowState.Unheard, r.Label,
                    (rest > 0 ? $"其余 {rest} 段读完后自动选择" : "读完后自动选择") + (r.Lines > 0 ? $" · {r.Lines} 句" : ""), "最后读"),
                    new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        }
        return card;
    }

    /// <summary>0.4.8 设计稿 05：内容区「VOICE · 配音状态」说明卡（待配音 / 无配音两枚图例签 + 规则说明）。</summary>
    View ListeningVoiceCard(string tag)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(PromptArt.PaperWarm, 245), PgrArt.GoldLine, PgrTheme.CutMedium, Corners.Diagonal, PgrArt.GoldLine, AccentEdge.Left),
            new FusionDeco(density, DecoTier.S) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium });
        card.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(PgrUi.En(this, "VOICE · 配音状态", 9.5f, PgrTheme.BrassText), new LinearLayout.LayoutParams(0, -2, 1));
        head.AddView(StopVisuals.VoiceTag(this, true, false), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
        head.AddView(StopVisuals.VoiceTag(this, false, false), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
        card.AddView(head);
        string text = (tag == "待配音" ? "待配音：本句尚未配音" : "无配音：本句没有可用配音") +
            "——照常显示字幕，按字数停留（约每字 0.25 秒，至少 2 秒）后自动进入下一句；点「下一句」可立即越过。";
        card.AddView(PromptArt.Label(this, text, 12.5f, PromptArt.Note), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        card.ContentDescription = text;
        return card;
    }

    void UpdateListeningVoiceTag(PgrVoice.Node? node)
    {
        if (listeningVoiceTag == null) return;
        string? tag = session.Listening.VoiceTagFor(node);
        if (tag == null) { listeningVoiceTag.Visibility = ViewStates.Gone; return; }
        if (listeningVoiceTag.Text != tag)
        {
            var fresh = StopVisuals.VoiceTag(this, tag == "待配音", true);
            listeningVoiceTag.Text = fresh.Text; listeningVoiceTag.Background = fresh.Background; listeningVoiceTag.SetTextColor(fresh.TextColors);
            listeningVoiceTag.ContentDescription = fresh.ContentDescription;
        }
        listeningVoiceTag.Visibility = ViewStates.Visible;
    }

    /// <summary>游戏模式无文本死角卡（L）：同听书的 F-B 黄铜纸面卡；不倒计时、不跳小节，等待游戏；「反馈」写玩家反馈记录。</summary>
    void GameDeadEndCard(LinearLayout parent)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.ParseColor("#FBF7EC"), 245), PgrArt.GoldLine, PgrTheme.CutMedium, Corners.Diagonal, PgrArt.GoldLine, AccentEdge.Left),
            new FusionDeco(density, DecoTier.M) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium });
        card.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        card.AddView(PgrUi.En(this, "END OF TEXT · 后续没有配音文本", 9.5f, PgrTheme.BrassText));
        var heading = PgrTheme.Apply(Text(ListeningText.DeadEndHint, 15), PgrTheme.Role.Heading); heading.TextSize = 15; card.AddView(heading, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        var sub = new TextView(this) { Text = "配音停在这里等待游戏，请在游戏中继续。下面仍是完整台词，可按游戏画面确认一句。", TextSize = 12 }; sub.SetTextColor(PgrTheme.Secondary);
        card.AddView(sub, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), BottomMargin = Dp(4) });
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; card.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        var fb = Button(row, "反馈", () => session.GameFeedback()); ButtonIcon(fb, "feedback");
        row.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
    }

    /// <summary>0.4.7（M）：倒计时 / 停下等待期间，托盘说话人牌与正文跟随停下卡片的状态，不再显示旧的“已暂停”续接说明。</summary>
    (string Plate, string Text)? StopTrayText(ListeningSession player)
    {
        if (player.PendingStop is not { Action: not ListeningStopAction.Choose } stop) return null;
        bool counting = player.CountdownRemaining > 0;
        if (counting && player.CountdownPaused) return ("已暂停 · " + (stop.Action == ListeningStopAction.OrderContinue ? "分支续接未核实" : stop.SectionComplete ? "本小节已读完" : "后续没有配音文本"), player.Status);
        return stop.Action switch
        {
            ListeningStopAction.OrderContinue => ("分支续接未核实", counting ? "此处分支续接未核实，倒计时结束后从本节下一句按顺序继续。" : "此处分支续接未核实，点「按顺序继续」从本节下一句接着听。"),
            _ when stop.SectionComplete => ("本小节已读完", counting ? "本小节已读完，倒计时结束后进入下一小节。" : "本小节已读完，点「进入下一小节」继续。"),
            ListeningStopAction.NextSection => ("后续没有配音文本", counting ? ListeningText.DeadEndHint + "。" + player.CountdownRemaining + " 秒后跳到下一小节。" : ListeningText.DeadEndHint + "。点「跳到下一小节」继续。"),
            _ => ("后续没有配音文本", ListeningText.DeadEndChapterEnd),
        };
    }
}
