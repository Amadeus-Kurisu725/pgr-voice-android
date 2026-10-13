using PgrVoice.Following;
using PgrVoice.Listening;
using Android.OS;
using PgrVoice.AndroidApp.Platform;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.7 游戏配音（自动播放）倒计时，与桌面 / 听书停下处同一规则（设计 08 / 10 / 11；规则见 core/Following/PlayAlongCountdown.cs）：
/// · 普通分支菜单（开启「默认分支」时）：15 秒倒计时，用户点选项优先；到点按默认分支第一 / 第二个自动选择并发出「已自动选择：xxx」；
///   结局分歧菜单同样倒计时（GameCountdownEnding 供界面显示 ENDING 标签 / 锁定说明，只读所选结局）；「分支自动选择」关闭时停下等待。单选项、探索 / 问答菜单不倒计时（与原来一样立即处理）。
/// · 未核出口（等待确认续接）：自动播放保持开启（WaitingForGame，不超时），15 秒后点击一次游戏「下一句」，从本节下一句按顺序继续配音。
/// · 暂停 / 继续：<see cref="ToggleGameCountdownPause"/>，剩余时间冻结。
/// 本文件只有规则与状态，不含界面；界面（卡片、倒计时环、提示条）绑定下方公开状态与事件。
/// </summary>
public sealed partial class AppSession
{
    sealed record GameCountdownContext(PlaybackEngine Engine, AdvanceTapTarget Target, string DisplayKey, string[] Visited);
    /// <summary>倒计时到点 / 用户点选后交给 BeginDefaultBranchWait 的选择：Number 为记录与显示用的 1 / 2；OptionId 为用户点选的选项（自动选择时为 null，按默认分支）。</summary>
    sealed record GameBranchPick(int Number, string? OptionId, bool Automatic);
    /// <summary>无音频句停留：节点、开始时刻、总时长（毫秒）、种类（待配音 / 无配音 / 仅字幕 / 标点停顿）。</summary>
    public sealed record GameSilentDwell(string NodeId, long StartMs, long DurationMs, string Kind);

    PlayAlongCountdown? gameCountdown;
    GameCountdownContext? gameCountdownContext;
    GameBranchPick? gameBranchPick;
    long gameCountdownToken;
    /// <summary>关闭「分支自动选择」时若倒计时已暂停，记住节点 / 种类 / 剩余毫秒；同一停下处再开启时恢复暂停态。</summary>
    string? gameCountdownFrozenNodeId;
    PlayAlongCountdownKind? gameCountdownFrozenKind;
    long gameCountdownFrozenRemaining;
    readonly HashSet<string> gameOrderVisited = new(StringComparer.Ordinal);
    static long CountdownNow => SystemClock.ElapsedRealtime();

    // ---------------- 界面可绑定的状态与事件 ----------------
    /// <summary>当前游戏配音倒计时（分支 / 按顺序续接）；没有时为 null。</summary>
    public PlayAlongCountdown? GameCountdown => gameCountdown;
    /// <summary>剩余秒数（15…1；停下等待或没有倒计时为 0；暂停时冻结）。</summary>
    public int GameCountdownRemaining => gameCountdown?.RemainingSeconds(CountdownNow) ?? 0;
    /// <summary>倒计时细轨比例（1 → 0）。</summary>
    public double GameCountdownFraction => gameCountdown?.Fraction(CountdownNow) ?? 0;
    public bool GameCountdownPaused => gameCountdown?.Paused == true;
    /// <summary>停下等待用户（「分支自动选择」关闭）。</summary>
    public bool GameCountdownWaiting => gameCountdown?.Waiting == true;
    /// <summary>结局分歧菜单（同样倒计时）：界面显示 ENDING 标签与「只收听所选结局」锁定说明。</summary>
    public bool GameCountdownEnding => gameCountdown?.Ending == true;
    /// <summary>到点将自动选择的选项 id / 文字（分支倒计时）。</summary>
    public string? GameCountdownOptionId => gameCountdown is { Kind: PlayAlongCountdownKind.Branch } c ? c.DefaultOptionId : null;
    public string? GameCountdownOptionLabel => gameCountdown is { Kind: PlayAlongCountdownKind.Branch } c ? c.DefaultOptionLabel : null;
    /// <summary>菜单之前的那一句（设计 08 / 10）；按顺序续接时为刚读完的当前句。</summary>
    public Node? GameCountdownPrecedingLine => gameCountdown?.PrecedingLineId is { } id && Engine?.Pack.ById.TryGetValue(id, out var n) == true ? n : null;
    /// <summary>按顺序续接：刚读完的所选选项文字（设计 11「已读完所选「…」」）。</summary>
    public string? GameCountdownPreviousOptionLabel => gameCountdown?.PreviousOptionLabel;
    /// <summary>最近一次选入提示「进入分支N：选项」（序号为实际选入的选项）。</summary>
    public string? LastBranchEnteredText { get; private set; }
    /// <summary>
    /// 0.4.7 无音频句停留（设计 05b：待配音 / 无配音 / 仅字幕 / 标点停顿）：自动播放正在停留时给出节点、开始时刻与总时长
    /// （毫秒，SystemClock.ElapsedRealtime 时基；进度 = (now - StartMs) / DurationMs）；不在停留时为 null。
    /// </summary>
    public GameSilentDwell? GameDwell => autoPlay.Phase == AutoPlaybackPhase.SilentPause && autoPlay.SilentPauseDuration > 0 &&
        Engine?.Pack.ById.TryGetValue(autoPlay.NodeId, out var node) == true
        ? new(node.Id, autoPlay.SilentPauseStarted, autoPlay.SilentPauseDuration,
            PendingVoice.IsSubtitleOnly(node) ? "仅字幕" : PendingVoice.IsFlagged(node) ? "待配音" : PassesWithoutVoice(node) ? "无配音" : "标点停顿")
        : null;
    /// <summary>正在读的探索菜单（设计 13）：当前句属于探索菜单的某个选项时给出菜单 id、选项与已读状态；否则 null。</summary>
    public ExplorationPanel? GameExplorationMenu => Engine is { } engine ? ExplorationPanel.Of(engine) : null;
    /// <summary>最近一次自动处理的提示：「已自动选择：xxx」或「此处分支续接未核实，已按顺序继续」。</summary>
    public string? GameAutoNotice { get; private set; }
    /// <summary>倒计时开始 / 每秒 / 暂停 / 继续 / 结束时触发。</summary>
    public event Action? GameCountdownChanged;
    /// <summary>倒计时到点自动处理后触发，参数为 <see cref="GameAutoNotice"/>（界面显示 3 秒提示条）。</summary>
    public event Action<string>? GameAutoSelected;

    // ---------------- 用户操作 ----------------
    /// <summary>暂停 / 继续倒计时；返回切换后的暂停状态。</summary>
    public bool ToggleGameCountdownPause()
    {
        if (gameCountdown is not { } c || c.Waiting) return false;
        long now = CountdownNow;
        bool paused = c.TogglePause(now);
        // 与听书一致：暂停 / 继续不写播放记录（旧实现误记为 Stop，导出 CSV 会把暂停算进去）。
        Status = c.StatusText(now);
        ScheduleGameCountdownTick();
        GameCountdownChanged?.Invoke(); Notify();
        return paused;
    }
    /// <summary>用户点选项（用户优先）：立即选入该选项。只在分支倒计时 / 等待中有效。</summary>
    public bool ChooseGameCountdownOption(string optionId) =>
        gameCountdown is { Kind: PlayAlongCountdownKind.Branch } c && c.Options.Any(o => o.Id == optionId) && CompleteGameCountdown(optionId, false);
    /// <summary>「按顺序继续」：立即从本节下一句接着配音（与到点相同）。只在未核出口倒计时 / 等待中有效。</summary>
    public bool ContinueGameInOrder() =>
        gameCountdown is { Kind: PlayAlongCountdownKind.OrderContinue } && CompleteGameCountdown(null, false);

    // ---------------- 内部 ----------------
    void CancelGameCountdown()
    {
        if (gameCountdown == null && gameCountdownContext == null && gameCountdownFrozenNodeId == null) return;
        gameCountdownToken++; gameCountdown?.Finish(); gameCountdown = null; gameCountdownContext = null;
        gameCountdownFrozenNodeId = null; gameCountdownFrozenKind = null; gameCountdownFrozenRemaining = 0;
        GameCountdownChanged?.Invoke();
    }

    void ScheduleGameCountdownTick()
    {
        long token = ++gameCountdownToken;
        if (gameCountdown is not { Counting: true } c) return;
        long remaining = c.RemainingMilliseconds(CountdownNow);
        long wait = Math.Clamp(remaining % 1000 == 0 ? 1000 : remaining % 1000, 1, 1000);
        main.PostDelayed(() => Post(() => GameCountdownTick(token)), wait);
    }

    void GameCountdownTick(long token)
    {
        if (token != gameCountdownToken || gameCountdown is not { Counting: true } c) return;
        long now = CountdownNow;
        if (c.Expired(now)) { CompleteGameCountdown(null, true); return; }
        Status = c.StatusText(now);
        GameCountdownChanged?.Invoke(); Notify();
        ScheduleGameCountdownTick();
    }

    void StartGameCountdown(PlayAlongCountdown countdown, GameCountdownContext context)
    {
        gameCountdown = countdown; gameCountdownContext = context;
        Status = countdown.StatusText(CountdownNow);
        ScheduleGameCountdownTick();
        GameCountdownChanged?.Invoke(); Notify();
    }

    /// <summary>普通分支菜单：开启倒计时（或停下等待）。单选项 / 探索 / 问答菜单、默认项不能自动选入时返回 false，交回原来的处理。</summary>
    bool TryBeginGameBranchCountdown(PlaybackEngine engine, AdvanceTapTarget target, string displayKey)
    {
        if (engine.Current is not { Kind: "choice" } menu || engine.Mode != RunMode.Choice) return false;
        var countdown = PlayAlongCountdown.ForMenu(engine.Pack, menu, engine.AvailableOptions, Settings.DefaultBranchOption, Settings.BranchAutoSelect, CountdownNow,
            PlayAlongCountdown.PrecedingLine(engine));
        if (countdown == null || !DefaultBranchPolicy.TryChoice(engine, Settings.DefaultBranchOption, out _, out _)) return false;
        var visited = defaultAutoVisited.ToArray();
        CancelAutoPlayback(); CancelClickFollow(); CancelBranchFollow(); audio.Stop();
        LogGame(PlaybackLog.Types.Stop, engine, menu.Id,
            (countdown.Ending ? "结局分歧·" : "") + (countdown.Waiting ? "分支自动选择已关闭·停下等待选择" :
            "分支倒计时 " + PlayAlongCountdown.Seconds + " 秒·默认分支" + ListeningText.OptionOrdinal(countdown.DefaultNumber)), countdown.DefaultOptionLabel ?? "");
        StartGameCountdown(countdown, new(engine, target, displayKey, visited));
        return true;
    }

    /// <summary>未核出口（等待确认续接）：自动播放转入等待（不超时、不点击），倒计时到点后按顺序续接。条件不满足时返回 false，交回原来的暂停。</summary>
    bool TryBeginGameOrderCountdown(PlaybackEngine engine, CommonAutoPlayDecision decision, long epoch)
    {
        if (!PlayAlongCountdown.IsUnverifiedExitCode(decision.Code) || GameDeadEnd(engine) ||
            engine.Current is not { Kind: "line", Archived: false } current || autoTarget is not { } target) return false;
        var visited = new HashSet<string>(defaultAutoVisited.Concat(gameOrderVisited), StringComparer.Ordinal);
        var countdown = PlayAlongCountdown.ForUnverifiedExit(engine.Pack, current, visited, Settings.BranchAutoSelect, CountdownNow);
        if (countdown?.OrderLineId is not { } nextId || !engine.Pack.ById.TryGetValue(nextId, out var next) || !CanAutoPlayLine(engine.Pack, next) ||
            !OrderProbe(engine, next, out _)) return false;
        if (!autoPlay.BeginWaiting(epoch, current.Id)) return false;
        audio.Stop();
        LogGame(PlaybackLog.Types.Stop, engine, current.Id,
            "等待确认续接·" + (countdown.Waiting ? "分支自动选择已关闭，停下等待" : "倒计时 " + PlayAlongCountdown.Seconds + " 秒后按顺序继续") + "·" + decision.Reason, next.Text);
        StartGameCountdown(countdown, new(engine, target, autoDisplay.RegionKey ?? "", visited.ToArray()));
        return true;
    }

    /// <summary>在导航器副本上按游戏画面定位到按顺序续接的台词，并确认可从那里继续自动播放。</summary>
    bool OrderProbe(PlaybackEngine engine, Node next, out PlaybackEngine? probe)
    {
        probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation()) || !probe.ConfirmGameLine(next.Id) || !CanStartConfirmedAutoRoute(probe, out _))
        { probe = null; return false; }
        return true;
    }

    bool CompleteGameCountdown(string? optionId, bool automatic)
    {
        if (gameCountdown is not { } c || gameCountdownContext is not { } ctx) return false;
        gameCountdownToken++; c.Finish(); gameCountdown = null; gameCountdownContext = null;
        gameCountdownFrozenNodeId = null; gameCountdownFrozenKind = null; gameCountdownFrozenRemaining = 0;
        GameCountdownChanged?.Invoke();
        var engine = ctx.Engine;
        if (c.Kind == PlayAlongCountdownKind.Branch)
        {
            if (!ReferenceEquals(Engine, engine) || engine.CurrentId != c.NodeId || engine.Mode != RunMode.Choice)
            { PauseAutoPlayback("分支位置已变化，倒计时已取消；请核对游戏位置。"); return false; }
            int ordinal = optionId == null ? 0 : engine.AvailableOptions.FindIndex(o => o.Id == optionId) + 1;
            defaultAutoVisited.UnionWith(ctx.Visited);
            gameBranchPick = automatic ? new(Settings.DefaultBranchOption, null, true) : new(ordinal is 1 or 2 ? ordinal : 1, optionId, false);
            string label = automatic ? c.DefaultOptionLabel ?? "" : c.Options.FirstOrDefault(o => o.Id == optionId)?.Label ?? "";
            if (!BeginDefaultBranchWait(engine, ctx.Target, ctx.DisplayKey))
            { gameBranchPick = null; PauseAutoPlayback("所选分支暂不能自动配音，请在游戏中继续并手动核对。"); return false; }
            gameBranchPick = null;
            if (automatic) { GameAutoNotice = ListeningText.AutoSelected + label; GameAutoSelected?.Invoke(GameAutoNotice); }
            Status = automatic ? GameAutoNotice! : "已选择：" + label;
            Notify();
            return true;
        }
        // 按顺序续接：点击一次游戏「下一句」，再从本节下一句继续配音。
        if (!ReferenceEquals(Engine, engine) || engine.CurrentId != c.NodeId || !autoPlay.WaitingForGame ||
            c.OrderLineId is not { } nextId || !engine.Pack.ById.TryGetValue(nextId, out var next) || !OrderProbe(engine, next, out _))
        { PauseAutoPlayback("续接位置已变化，倒计时已取消；请核对游戏位置。"); return false; }
        long now = CountdownNow, epoch = autoPlay.Epoch;
        if (!CheckAutoPlaybackEnvironment()) return false;
        var target = autoTarget ?? ctx.Target;
        if (overlay.ContainsPoint(target.CenterX, target.CenterY)) { PauseAutoPlayback("悬浮控制挡住了下一句区域，请挪开悬浮球后重新开始。"); return false; }
        if (!autoPlay.ResumeTapping(epoch, c.NodeId, now) || !autoPlay.BeginTap(epoch, next.Id, now))
        { PauseAutoPlayback("点击请求已变化，请重新定位。"); return false; }
        gameOrderVisited.Add(next.Id);
        _ = OrderContinueOnceAsync(engine, epoch, autoPlay.TapTicket, next, autoServiceToken, target, automatic);
        return true;
    }

    async Task OrderContinueOnceAsync(PlaybackEngine engine, long epoch, long tapTicket, Node next, long token, AdvanceTapTarget target, bool automatic)
    {
        try
        {
            var result = await GameAdvanceAccessibilityService.TapAsync(token, target.CenterX, target.CenterY);
            Post(() =>
            {
                if (autoPlay.Epoch != epoch || !autoPlay.Running) return;
                if (!result.Completed) { PauseAutoPlayback(result.Reason + " 不会重复补点，请核对游戏位置。"); return; }
                if (!ReferenceEquals(Engine, engine) || !CheckAutoPlaybackEnvironment()) return;
                if (!autoPlay.GestureCompleted(epoch, tapTicket, next.Id, SystemClock.ElapsedRealtime())) return;
                main.PostDelayed(() => Post(() =>
                {
                    if (autoPlay.Epoch != epoch || !autoPlay.Running || !CheckAutoPlaybackEnvironment()) return;
                    if (!OrderProbe(engine, next, out var probe) || probe == null)
                    { PauseAutoPlayback("按顺序续接的台词已不能继续自动播放，请手动核对；已点击游戏下一句一次。"); return; }
                    LogGame(PlaybackLog.Types.OrderContinue, engine, next.Id, (automatic ? "倒计时结束" : "用户确认") + "·未核出口按顺序续接", next.Text);
                    GameAutoNotice = ListeningText.OrderContinued;
                    ResumeAutoAtLocatedLine(engine, probe, next);
                    if (automatic) GameAutoSelected?.Invoke(GameAutoNotice);
                    Status = GameAutoNotice; Notify();
                }), 450);
            });
        }
        catch (Exception ex)
        { Post(() => { if (autoPlay.Epoch == epoch && autoPlay.Running) PauseAutoPlayback("点击未完成：" + ex.Message); }); }
    }

    /// <summary>
    /// 「分支自动选择」/ 默认分支设置改变时，按新设置更新当前倒计时（与听书 BranchAutoSelectChanged 同一类规则）：
    /// · 关闭自动选择 → 停下等待；若当时已暂停，记住剩余秒数，同一停下处再开启时恢复「已暂停」+ 剩余；
    /// · 开启自动选择 / 改默认分支：已暂停则保持暂停与剩余秒数，只更新到点将选的默认项；进行中则保持剩余秒数不重置为 15；
    /// · 不写额外的「停下」播放记录。
    /// </summary>
    void RefreshGameCountdownForSettings()
    {
        if (gameCountdown is not { } c || gameCountdownContext is not { } ctx) return;
        var engine = ctx.Engine; long now = CountdownNow;
        bool wasPaused = c.Paused;
        long remaining = c.RemainingMilliseconds(now);
        PlayAlongCountdown? fresh = c.Kind == PlayAlongCountdownKind.Branch
            ? engine.Current is { Kind: "choice" } menu && menu.Id == c.NodeId
                ? PlayAlongCountdown.ForMenu(engine.Pack, menu, engine.AvailableOptions, Settings.DefaultBranchOption, Settings.BranchAutoSelect, now, PlayAlongCountdown.PrecedingLine(engine)) : null
            : engine.Current is { Kind: "line" } line && line.Id == c.NodeId
                ? PlayAlongCountdown.ForUnverifiedExit(engine.Pack, line, ctx.Visited.ToHashSet(StringComparer.Ordinal), Settings.BranchAutoSelect, now) : null;
        if (fresh == null) { CancelGameCountdown(); return; }
        if (fresh.Waiting)
        {
            // 关闭自动选择：结束本次倒计时，停下等待；已暂停则记住秒数供再开启时恢复。
            if (wasPaused && remaining > 0)
            { gameCountdownFrozenNodeId = c.NodeId; gameCountdownFrozenKind = c.Kind; gameCountdownFrozenRemaining = remaining; }
            else { gameCountdownFrozenNodeId = null; gameCountdownFrozenKind = null; gameCountdownFrozenRemaining = 0; }
            gameCountdownToken++; c.Finish();
            StartGameCountdown(fresh, ctx);
            return;
        }
        // 开启自动选择或改默认分支：衔接暂停 / 剩余，不重置为全新 15 秒。
        bool restorePause = false; long restoreMs = 0;
        if (wasPaused && remaining > 0) { restorePause = true; restoreMs = remaining; }
        else if (gameCountdownFrozenNodeId == c.NodeId && gameCountdownFrozenKind == c.Kind && gameCountdownFrozenRemaining > 0)
        { restorePause = true; restoreMs = gameCountdownFrozenRemaining; }
        gameCountdownFrozenNodeId = null; gameCountdownFrozenKind = null; gameCountdownFrozenRemaining = 0;
        gameCountdownToken++; c.Finish();
        if (restorePause) fresh.RestorePause(now, restoreMs);
        else if (!c.Waiting && remaining > 0) fresh.ContinueWithRemaining(now, remaining);
        StartGameCountdown(fresh, ctx);
    }

    /// <summary>悬浮「下一句」：倒计时进行中时先探测是否真能挪动；挪不动则保持倒计时并返回 true。</summary>
    bool TryOverlayNextDuringGameCountdown()
    {
        if (gameCountdown is not { } c || gameCountdownContext is not { } || Engine is not { } engine) return false;
        var probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation())) return true;
        ExplorationMemory.CopyTo(engine, probe);
        string? beforeId = probe.CurrentId; var beforeMode = probe.Mode;
        try { probe.Next(true); }
        catch { Status = c.StatusText(CountdownNow); Notify(); return true; }
        if (probe.CurrentId == beforeId && probe.Mode == beforeMode)
        { Status = c.StatusText(CountdownNow); Notify(); return true; }
        return false;
    }
}
