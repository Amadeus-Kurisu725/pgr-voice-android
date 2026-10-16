using Android.OS;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.Following;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.8 界面层：把规则层状态抄给悬浮层顶边融合条（设计稿 05b / 06 / 08 / 09 / 10 / 11 / 13）。
/// 只读规则层公开接口（a577310）：GameCountdown*（含 PrecedingSpeaker/Text、PreviousOptionLabel）、GameDwell、GameExplorationMenu、
/// GameEndWaiting、LastBranchEnteredText；不改变规则、不自己计时。
/// Notify() 很频繁：按「状态签名」缓存，只有倒计时 / 停留 / 终点 / 当前节点或菜单变化时才重算；探索面板按导航器 + 当前节点 + 模式缓存。
/// </summary>
public sealed partial class AppSession
{
    int promptLastDefaultNumber = 1;
    string? promptSignature;
    (PlaybackEngine? Engine, string? Node, RunMode Mode, int Heard)? exploreCacheKey;
    ExplorationPanel? exploreCache;
    bool exploreCacheAtMenu;

    PromptBarTouchLease? promptTouchLease;

    /// <summary>设计稿 13 标题一行：「29-1 营地 · 可交谈人物」= 小节编号 + 菜单名（「营地 → 可交谈人物」各段以 · 连接，去掉「[…]」标记段）。
    /// 菜单名推不出来时用小节标题（去掉「[…]」标记）。与桌面版 ExploreMenuName 同规则。</summary>
    internal static string ExplorePanelTitle(string? menuText, string? sectionTitle)
    {
        static string Strip(string? t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", @"\s*[\[［【][^\]］】]*[\]］】]\s*", " ").Trim();
        var parts = (menuText ?? "").Split('→').Select(p => p.Trim()).Where(p => p.Length > 0 && !p.StartsWith("[", StringComparison.Ordinal)).ToList();
        string section = Strip(sectionTitle);
        if (parts.Count == 0 || parts.All(p => p == "选择路线")) return section.Length > 0 ? section : "探索";
        string name = string.Join(" · ", parts);
        var code = System.Text.RegularExpressions.Regex.Match(section, @"^\d+(?:-\d+)+");
        return code.Success && !name.StartsWith(code.Value, StringComparison.Ordinal) ? code.Value + " " + name : name;
    }

    /// <summary>「建议先找栗西」：取自本菜单选项的条件备注（与电脑版相同），只到第一个标点；建议的人物已听过后隐藏。</summary>
    internal static string? ExplorePanelHint(PlaybackEngine engine, string menuId, IReadOnlyList<ExploreBarRow> rows)
    {
        if (!engine.Pack.ById.TryGetValue(menuId, out var menu)) return null;
        string? hint = menu.Options.Select(o => o.ConditionNote ?? "").FirstOrDefault(n => n.StartsWith("建议先找", StringComparison.Ordinal));
        if (hint == null) return null;
        hint = hint.Split('（', '(', '；', ';', '，', '。')[0].Trim();
        string who = hint.Substring(4).Trim();
        if (who.Length > 0 && rows.Any(r => !r.Exit && r.Heard && r.Label.StartsWith(who, StringComparison.Ordinal))) return null;
        return hint;
    }

    void WirePromptBars()
    {
        // 自动点击游戏期间提示条不接收触碰；手势结束（完成 / 取消 / 失败）或 3 秒超时后恢复。
        promptTouchLease = new PromptBarTouchLease(overlay.SetPromptBarsTouchable, (action, ms) => main.PostDelayed(action, ms));
        GameAdvanceAccessibilityService.BeforeTap = promptTouchLease.Acquire;
        GameAdvanceAccessibilityService.PromptWindowIdsBeforeTap = overlay.GetPromptWindowIds;
        // 审稿 c1478eb：提示条避开字幕框与下一句点击区域（按整块物理屏幕的 0–1 比例）；条本身不计入区域判断。
        overlay.PromptAvoidAreas = () =>
        {
            var list = new List<(float, float, float, float)>(2);
            if (!Settings.OcrFullScreen && frameDisplayState.RegionKey is { } regionKey)
            {
                var r = Settings.RegionFor(regionKey).Clamp();
                list.Add((r.Left, r.Top, r.Left + r.Width, r.Top + r.Height));
            }
            if (Screen.DisplayState.RegionKey is { } key && Settings.AdvanceTargetFor(key) is { IsValid: true } t)
                list.Add((t.Left, t.Top, t.Left + t.Width, t.Top + t.Height));
            return list;
        };
        GameCountdownChanged += () => { promptSignature = null; RefreshPromptBars(); };
        GameAutoSelected += message =>
        {
            bool order = message.Contains("按顺序");
            overlay.ShowAutoSelectedBar(message,
                order ? "倒计时结束 · 按本节顺序接下一句 · 自动播放继续"
                      : "倒计时结束 · 默认分支" + ListeningText.OptionOrdinal(promptLastDefaultNumber) + " · 自动播放继续",
                order ? "ROUTE · IN ORDER" : "AUTO · SELECTED");
        };
    }

    /// <summary>探索面板（设计稿 13）：读回环时取规则层 GameExplorationMenu；停在探索菜单（手动 / 跟随）时按菜单形态列出，可点选。按节点缓存。</summary>
    (ExplorationPanel? Panel, bool AtMenu) PromptExploration(PlaybackEngine engine)
    {
        // 已听集合只在选入选项时变化，而选入必然先离开菜单节点，所以按「导航器 + 当前节点 + 模式」缓存即可。
        var key = (engine, engine.CurrentId, engine.Mode, 0);
        if (exploreCacheKey is { } k && k.Equals(key)) return (exploreCache, exploreCacheAtMenu);
        exploreCacheKey = key; exploreCache = null; exploreCacheAtMenu = false;
        if (engine is { Mode: RunMode.Choice, Current: { Kind: "choice" } menu } && MenuShape.Of(engine.Pack, menu) is { Exploration: true } shape)
        {
            var heardIds = ExplorationMemory.Heard(engine, menu.Id);
            exploreCache = new(menu.Id, menu.Text, shape.Options.Select(o => new ExplorationPanelOption(o.Id, o.Label,
                heardIds.Contains(o.Id) || shape.EmptyLoops.Contains(o), false, shape.Exits.Contains(o), shape.EmptyLoops.Contains(o))).ToList());
            exploreCacheAtMenu = true;
        }
        else exploreCache = GameExplorationMenu;
        return (exploreCache, exploreCacheAtMenu);
    }

    void RefreshPromptBars()
    {
        long now = SystemClock.ElapsedRealtime();
        var c = gameCountdown is { Finished: false } live ? live : null;
        var dwell = c == null ? GameDwell : null;
        var wait = c == null ? GameEndWaiting : null;
        var engine = Engine;
        string signature = c != null ? "c" + c.GetHashCode() + "|" + GameCountdownRemaining + "|" + c.Paused + "|" + c.Waiting
            : wait != null ? "w" + wait.GetHashCode()
            : dwell != null ? "d" + dwell.NodeId + "|" + dwell.StartMs
            : "e" + engine?.GetHashCode() + "|" + engine?.CurrentId + "|" + engine?.Mode + "|" + autoPlay.Running;
        signature += "|" + overlay.PromptBarEpoch;
        if (signature == promptSignature) return;
        promptSignature = signature;

        if (c != null && engine != null)
        {
            promptLastDefaultNumber = c.DefaultNumber is 1 or 2 ? c.DefaultNumber : 1;
            string? orderSpeaker = null;
            if (c.OrderLineId is { } orderId && engine.Pack.ById.TryGetValue(orderId, out var orderNode)) orderSpeaker = orderNode.Speaker;
            bool order = c.Kind == PlayAlongCountdownKind.OrderContinue;
            var model = new CountdownBarModel(order, c.Ending, c.Waiting, c.Paused, GameCountdownRemaining, (float)GameCountdownFraction,
                c.PrecedingSpeaker ?? "", c.PrecedingText ?? "", GameCountdownPreviousOptionLabel,
                c.Options.Select(o => new PromptBarOption(o.Id, o.Label, o.Id == c.DefaultOptionId)).ToList(),
                orderSpeaker, c.OrderLineText, c.StatusText(now));
            overlay.ShowCountdownBar(model,
                id => Post(() => ChooseGameCountdownOption(id)),
                () => Post(() => ContinueGameInOrder()),
                () => Post(() => ToggleGameCountdownPause()));
            return;
        }
        if (wait != null && engine != null)
        {
            overlay.ShowEndWaitBar(new EndWaitBarModel(BrowseSectionTitle(engine, wait.SectionId), wait.Hint, "游戏出现下一句后自动对齐继续；也可点「识别定位」"));
            return;
        }
        // 设计稿 05b：规则层 GameDwell 给出节点、开始时刻和总时长；标点停顿（1.2 秒）不弹条。
        if (dwell != null && dwell.Kind != "标点停顿" && engine != null && engine.Pack.ById.TryGetValue(dwell.NodeId, out var node))
        {
            overlay.ShowPendingVoiceBar(new PendingVoiceBarModel(node.Speaker ?? "", node.Text ?? "", dwell.Kind == "仅字幕" ? "" : dwell.Kind,
                dwell.StartMs, (int)Math.Max(1, dwell.DurationMs)));
            return;
        }
        // 设计稿 13：手动 / 点按跟随时（自动播放会自己逐项读，不弹）。
        if (!autoPlay.Running && engine != null && PromptExploration(engine) is { Panel: { } panel } explore)
        {
            var available = explore.AtMenu ? engine.AvailableOptions : null;
            var rows = panel.Options.Select(o => new ExploreBarRow(o.Id, o.Label, o.Heard && !o.Current, o.Exit,
                available?.Any(a => a.Id == o.Id) == true, o.Current)).ToList();
            string section = BrowseSectionTitle(engine, engine.Current?.SectionId);
            overlay.ShowExploreBar(new ExploreBarModel(engine.Pack.Id + ":" + panel.MenuId, ExplorePanelTitle(panel.MenuText, section),
                    ExplorePanelHint(engine, panel.MenuId, rows), rows),
                id => Post(() =>
                {
                    if (Engine is not { Mode: RunMode.Choice } e || !ReferenceEquals(e, engine)) return;
                    if (e.AvailableOptions.FirstOrDefault(o => o.Id == id) is { } option) SelectOverlayBranch(e, option);
                }));
            return;
        }
        overlay.ClearPromptBar();
    }
}
