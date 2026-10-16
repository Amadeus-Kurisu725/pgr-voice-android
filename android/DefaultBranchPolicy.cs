using System.Runtime.CompilerServices;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

/// <summary>安卓显式默认分支的有限预览；不改共享导航权限，不代选人物或条件话题。</summary>
public static class DefaultBranchPolicy
{
    /// <param name="heard">0.4.7 探索菜单：本次自动播放中此菜单已读过的选项编号；回环选项按列出顺序各读一次，读完后在出口中按默认序号选择。</param>
    /// <param name="optionId">0.4.7 游戏配音倒计时：用户点选的选项（普通菜单）；为 null 时按默认序号。number 仍为记录 / 显示用的 1 或 2。</param>
    public static bool TryChoice(PlaybackEngine engine, int number, out ChoiceOption? option, out Node? first, IReadOnlySet<string>? heard = null, string? optionId = null)
    {
        option = null; first = null;
        if (number is not (1 or 2) || engine.Pack.SchemaVersion != 3 || engine.Mode != RunMode.Choice ||
            engine.Current is not { Kind: "choice" } menu || !AutoMenuType(engine.Pack, menu) || menu.Archived ||
            menu.Options.Count == 0 || !engine.Allowed(menu) || engine.ReviewRoute != null ||
            menu.SetFacts.Count != 0 || !string.IsNullOrEmpty(menu.CompleteRoute) ||
            menu.Options.Any(o => string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.PathId) ||
                o.Requires.Count != 0 || o.Excludes.Count != 0 || !string.IsNullOrWhiteSpace(o.ConditionNote)) ||
            menu.Options.Select(o => o.Id).Distinct().Count() != menu.Options.Count || menu.Options.Select(o => o.PathId).Distinct().Count() != menu.Options.Count)
            return false;
        // 设为第二个时只在确有第二项处生效；只有一个选项的菜单按第一个。
        var shape = MenuShape.Of(engine.Pack, menu);
        var chosen = shape.Exploration
            ? MenuShape.NextExploration(shape, heard ?? ExplorationMemory.Heard(engine, menu.Id), number)
            : optionId != null ? menu.Options.FirstOrDefault(o => o.Id == optionId)
            : menu.Options[EffectiveNumber(menu, number) - 1];
        // 同回应隐藏后只剩一项：取它（不论默认序号）。
        if (heard == null && HiddenOptions(engine).Count > 0 && shape.VisibleAfter(ExplorationMemory.Last(engine, menu.Id)) is { Count: 1 } only) chosen = only[0];
        if (chosen == null) return false;
        // 0.4.7 空出口：探索菜单的出口没有台词（只经 return / merge 到汇合后共同线），不交给手动——首句取汇合后第一句。
        if (shape.Exploration && shape.IsEmptyExit(chosen))
            return TryEmptyExit(engine, menu, chosen, out option, out first);
        if (!chosen.BodyVerified || chosen.BodyEvidence.Count == 0 || chosen.BodyEvidence.Any(string.IsNullOrWhiteSpace) ||
            !engine.AvailableOptions.Any(o => o.Id == chosen.Id) ||
            !engine.Pack.ById.TryGetValue(chosen.TargetId, out var node) || node.Kind != "line" || node.Archived ||
            node.SectionId != menu.SectionId || node.PathId != chosen.PathId || !chosen.SegmentIds.Contains(node.Id) ||
            node.SetFacts.Count != 0 || node.Options.Count != 0 || !string.IsNullOrEmpty(node.CompleteRoute)) return false;
        var probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation())) return false;
        probe.Commit(menu.Id);
        probe.SelectBranch(probe.AvailableOptions.FindIndex(o => o.Id == chosen.Id));
        if (probe.CurrentId != node.Id || probe.Mode != RunMode.Following || !probe.Allowed(node)) return false;
        option = chosen; first = node; return true;
    }

    /// <summary>
    /// 空出口：用真实导航器副本选入该出口，只越过 return / merge（不读、不跳过任何台词），必须正好落在 <see cref="MenuShape.EmptyExitLanding"/> 的那句台词上。
    /// </summary>
    static bool TryEmptyExit(PlaybackEngine engine, Node menu, ChoiceOption chosen, out ChoiceOption? option, out Node? first)
    {
        option = null; first = null;
        if (!chosen.BodyVerified || !engine.AvailableOptions.Any(o => o.Id == chosen.Id) ||
            MenuShape.EmptyExitLanding(engine.Pack, menu, chosen) is not { } landing || landing.SetFacts.Count != 0 ||
            landing.Options.Count != 0 || !string.IsNullOrEmpty(landing.CompleteRoute)) return false;
        var probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation())) return false;
        probe.Commit(menu.Id);
        if (!Enter(probe, chosen, landing) || !probe.Allowed(landing)) return false;
        option = chosen; first = landing; return true;
    }

    /// <summary>
    /// 在导航器上选入默认 / 探索选项并停在首句。普通选项即 SelectBranch；空出口在选入后越过汇合标记（最多 8 步，只允许 Merge 模式），
    /// 落到汇合后第一句。不在首句则返回 false（调用方按位置变化处理）。
    /// </summary>
    public static bool Enter(PlaybackEngine engine, ChoiceOption option, Node first)
    {
        engine.SelectBranch(engine.AvailableOptions.FindIndex(o => o.Id == option.Id));
        for (int i = 0; i < 8 && engine.CurrentId != first.Id && engine.Mode == RunMode.Merge && engine.Current?.Kind == "merge"; i++) engine.Next();
        return engine.CurrentId == first.Id && engine.Mode == RunMode.Following;
    }

    /// <summary>
    /// 0.4.7 选入提示「进入分支N：选项」：序号取<b>实际选入</b>的选项在菜单中的位置（用户在倒计时中点第三项时为「分支三」），
    /// 不取默认分支序号。
    /// </summary>
    public static string EnteredText(Node menu, ChoiceOption option)
    {
        int index = menu.Options.FindIndex(o => o.Id == option.Id);
        return "进入" + BranchName(index < 0 ? 1 : index + 1) + "：" + option.Label;
    }
    public static string BranchName(int number) =>
        "分支" + (number is >= 1 and <= 10 ? "一二三四五六七八九十"[number - 1].ToString() : number.ToString());

    /// <summary>可自动选择的菜单：互斥菜单；或没有互动导航证据、只有一个选项的互动菜单（0.4.7 单选项自动继续）。</summary>
    static bool AutoMenuType(Pack pack, Node menu) => menu.MenuType == "exclusive" ||
        menu.MenuType == "interaction" && menu.MenuNavigationEvidence.Count == 0 && menu.Options.Count == 1 && MenuShape.Of(pack, menu).Single;

    /// <summary>0.4.7：只有一个有效选项、无解锁条件的菜单（相当于“继续”）。自动播放不停下等待选择。</summary>
    public static bool IsSingleOptionMenu(PlaybackEngine engine) =>
        engine.Current is { Kind: "choice", Archived: false } menu && engine.Mode == RunMode.Choice && menu.Options.Count == 1 &&
        AutoMenuType(engine.Pack, menu) && MenuShape.Of(engine.Pack, menu).Single && !MenuShape.Of(engine.Pack, menu).Ending &&
        menu.Options.All(o => o.Requires.Count == 0 && o.Excludes.Count == 0 && string.IsNullOrWhiteSpace(o.ConditionNote))
        || IsSingleAfterHiding(engine);

    /// <summary>
    /// 0.4.7 同回应隐藏：回到菜单时与刚选项回应相同的选项已隐藏（如 19-14 答错后只剩正确答案），只剩一个可见选项——
    /// 与单选项菜单相同，自动播放不停下，取该项（<see cref="TryChoice"/> 按探索记忆同样取它）。
    /// </summary>
    public static bool IsSingleAfterHiding(PlaybackEngine engine) =>
        engine.Current is { Kind: "choice", Archived: false } menu && engine.Mode == RunMode.Choice && menu.MenuType == "exclusive" &&
        HiddenOptions(engine).Count > 0 && MenuShape.Of(engine.Pack, menu) is { Ending: false } shape &&
        shape.VisibleAfter(ExplorationMemory.Last(engine, menu.Id)) is { Count: 1 } visible &&
        visible[0].Requires.Count == 0 && visible[0].Excludes.Count == 0 && string.IsNullOrWhiteSpace(visible[0].ConditionNote);

    /// <summary>当前菜单因同回应隐藏而不显示的选项（刚选项没有同回应的其他选项时为空）。</summary>
    public static IReadOnlyList<ChoiceOption> HiddenOptions(PlaybackEngine engine)
    {
        if (engine.Current is not { Kind: "choice" } menu || engine.Mode != RunMode.Choice) return Array.Empty<ChoiceOption>();
        var shape = MenuShape.Of(engine.Pack, menu);
        var visible = shape.VisibleAfter(ExplorationMemory.Last(engine, menu.Id));
        return shape.Options.Where(o => visible.All(v => v.Id != o.Id)).ToList();
    }

    /// <summary>实际采用的默认序号：设为第二个而菜单只有一个选项时回落到第一个。</summary>
    public static int EffectiveNumber(Node? menu, int number) => number == 2 && menu != null && menu.Options.Count < 2 ? 1 : number;

    public static bool TryMatchFirst(PlaybackEngine engine, int number, IReadOnlyList<OcrBlock> blocks,
        int width, int height, out string? nodeId)
    {
        nodeId = null;
        if (!TryChoice(engine, number, out _, out var first) || first == null) return false;
        string text = Matcher.Normalize(first.Text);
        if (text.Length >= 4)
        {
            if (!BranchFollowPolicy.TryMatchMenuLine(engine, engine.CurrentId!, blocks, width, height, out var found, out _) || found != first.Id) return false;
        }
        else
        {
            // 短首句额外依赖调用方保存的完整菜单与新位置证据；这里不允许纯标点或一字应答。
            string speaker = Matcher.Normalize(first.Speaker);
            var valid = blocks.Where(b => double.IsFinite(b.Score) && b.Score >= .85 && b.Box != null && b.Box.Length >= 4 &&
                b.Box.All(p => p != null && p.Length == 2 && p.All(double.IsFinite) && p[0] >= 0 && p[1] >= 0 && p[0] <= width && p[1] <= height)).ToArray();
            if (text.Length < 2 || !text.Any(char.IsLetterOrDigit) || speaker.Length < 2 ||
                valid.Count(b => Matcher.Normalize(b.Text) == text) != 1 || !valid.Any(b => Matcher.Normalize(b.Text) == speaker) ||
                engine.Pack.Nodes.Count(n => !n.Archived && n.Kind == "line" && n.SectionId == first.SectionId && Matcher.Normalize(n.Text) == text) != 1 ||
                engine.Pack.Nodes.Count(n => !n.Archived && n.Kind == "line" && n.SectionId == first.SectionId &&
                    Matcher.Normalize(n.Text).Length >= 2 && valid.Any(b => Matcher.Normalize(b.Text) == Matcher.Normalize(n.Text))) != 1 ||
                engine.Current!.Options.All(o => valid.Any(b => Matcher.Normalize(b.Text) == Matcher.Normalize(o.Label)))) return false;
        }
        nodeId = first.Id; return true;
    }

    public static BranchFollowMenuMatch? ReadMenu(PlaybackEngine engine, IReadOnlyList<OcrBlock> blocks, int width, int height)
    {
        if (engine.Current is not { Kind: "choice", MenuType: "exclusive" } menu || menu.Options.Count == 0 || menu.Options.Count > 16) return null;
        var hits = new List<BranchFollowOptionHit>();
        foreach (var option in menu.Options)
        {
            string text = Matcher.Normalize(option.Label);
            if (text.Length < 2 || menu.Options.Count(o => Matcher.Normalize(o.Label) == text) != 1) return null;
            var matches = blocks.Where(b => double.IsFinite(b.Score) && b.Score >= .85 && Matcher.Normalize(b.Text) == text &&
                b.Box != null && b.Box.Length >= 4 && b.Box.All(p => p != null && p.Length == 2 && p.All(double.IsFinite))).ToArray();
            if (matches.Length != 1) return null;
            var box = matches[0].Box;
            var bounds = new BranchFollowRect(box.Min(p => p[0]), box.Min(p => p[1]), box.Max(p => p[0]), box.Max(p => p[1]));
            if (bounds.Width < 2 || bounds.Height < 2 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > width || bounds.Bottom > height) return null;
            if (hits.Any(h => Overlap(h.Bounds, bounds))) return null;
            hits.Add(new(option.Id, option.PathId, option.Label, bounds));
        }
        return new(menu.Id, hits);
    }

    public static bool Overlap(BranchFollowRect a, BranchFollowRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    public static bool TryNext(PlaybackEngine engine, out Node? next, out int steps, bool includeChoice = false)
    {
        next = null; steps = 0;
        if (engine.Pack.SchemaVersion != 3 || engine.Mode != RunMode.Following ||
            engine.Current is not { Kind: "line" } current || !engine.Allowed(current) ||
            engine.ReviewRoute != null || engine.ExportNavigation().Current.SingleLine) return false;
        var seen = new HashSet<string> { current.Id };
        var previous = current;
        string? id = current.NextId;
        while (id != null && seen.Add(id))
        {
            if (!engine.Pack.ById.TryGetValue(id, out var node) || node.Archived || node.SectionId != current.SectionId ||
                !engine.Allowed(node) || node.SetFacts.Count != 0 ||
                (node.Options.Count != 0 && !(includeChoice && node.Kind == "choice")) ||
                (node.Kind is not ("line" or "return" or "merge") && !(includeChoice && node.Kind == "choice"))) return false;
            if (!VerifiedExit(engine, previous, node)) return false;
            if (node.Kind == "line" || includeChoice && node.Kind == "choice")
            {
                if (!string.IsNullOrEmpty(node.CompleteRoute)) return false;
                var probe = new PlaybackEngine(engine.Pack);
                if (!probe.ImportNavigation(engine.ExportNavigation()) || !probe.ConfirmCurrentPosition()) return false;
                for (int i = 0; i < 8; i++)
                {
                    probe.Next(); steps++;
                    if (probe.CurrentId == node.Id && probe.Mode == (node.Kind == "choice" ? RunMode.Choice : RunMode.Following)) { next = node; return true; }
                    if (probe.Mode != RunMode.Merge || probe.Current?.Kind != "merge") return false;
                }
                return false;
            }
            if (node.Kind == "return" && !VerifiedReturn(engine, node)) return false;
            previous = node; id = node.NextId;
        }
        return false;
    }

    static bool VerifiedReturn(PlaybackEngine engine, Node node)
    {
        var owners = engine.Pack.Nodes.Where(n => !n.Archived).SelectMany(n => n.Options).Where(o => o.PathId == node.PathId).ToArray();
        return owners.Length == 1 && owners[0].BodyVerified && owners[0].ExitVerified &&
            owners[0].ExitEvidence.Count > 0 && owners[0].ExitEvidence.All(e => !string.IsNullOrWhiteSpace(e));
    }

    public static bool IsVerifiedEnd(PlaybackEngine engine)
    {
        if (engine.Current is not { Kind: "line" } current || engine.Mode != RunMode.Following ||
            engine.ReviewRoute != null || engine.ExportNavigation().Current.SingleLine || !engine.Allowed(current)) return false;
        if (current.NextId == null) return engine.EvaluateCommonAutoPlayNext() is { CurrentIsCommon: true, Code: "end" };
        if (!engine.Pack.ById.TryGetValue(current.NextId, out var end) || end.Kind != "end" || end.Archived ||
            end.SectionId != current.SectionId || !engine.Allowed(end) || !VerifiedExit(engine, current, end) ||
            end.SetFacts.Count != 0 || end.Options.Count != 0) return false;
        // 未核出口不能因为边界节点恰好叫end就冒充完整结束。
        if (current.PathId.Length > 0 && !VerifiedReturn(engine, current)) return false;
        var probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation()) || !probe.ConfirmCurrentPosition()) return false;
        probe.Next();
        return probe.CurrentId == end.Id && probe.Mode == RunMode.End;
    }

    static bool VerifiedExit(PlaybackEngine engine, Node previous, Node next)
    {
        if (previous.PathId.Length == 0 || previous.PathId == next.PathId) return true;
        var owners = engine.Pack.Nodes.Where(n => !n.Archived).SelectMany(n => n.Options).Where(o => o.PathId == previous.PathId).ToArray();
        return owners.Length == 1 && owners[0].BodyVerified && owners[0].ExitVerified &&
            owners[0].ExitEvidence.Count > 0 && owners[0].ExitEvidence.All(e => !string.IsNullOrWhiteSpace(e)) &&
            (owners[0].ReturnId == next.Id || owners[0].MergeId == next.Id || previous.Kind == "return");
    }
}

/// <summary>0.4.7 探索菜单记忆：每个导航器（一次游戏伴音经历）内，各菜单已自动读过的选项。</summary>
public static class ExplorationMemory
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlaybackEngine, Dictionary<string, HashSet<string>>> table = new();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlaybackEngine, Dictionary<string, string>> last = new();
    /// <summary>本菜单最近一次选的项（同回应隐藏只看刚选项，与游戏 xmovieactionselection OnEnter 相同）。</summary>
    public static string? Last(PlaybackEngine engine, string menuId)
    {
        var map = last.GetValue(engine, _ => new(StringComparer.Ordinal));
        lock (map) return map.TryGetValue(menuId, out var id) ? id : null;
    }
    /// <summary>导航器克隆（探测）沿用原导航器的探索记忆，使探测与实际选择一致。</summary>
    public static void CopyTo(PlaybackEngine from, PlaybackEngine to)
    {
        var src = table.GetValue(from, _ => new(StringComparer.Ordinal)); var dst = table.GetValue(to, _ => new(StringComparer.Ordinal));
        lock (src) lock (dst) foreach (var (menu, set) in src) dst[menu] = new(set, StringComparer.Ordinal);
        var srcLast = last.GetValue(from, _ => new(StringComparer.Ordinal)); var dstLast = last.GetValue(to, _ => new(StringComparer.Ordinal));
        lock (srcLast) lock (dstLast) foreach (var (menu, id) in srcLast) dstLast[menu] = id;
        ExplorationPanel.Invalidate(to);
    }
    /// <summary>记下当前菜单所选项（任何方式选入：默认分支、点按 / OCR 跟随、手动）。不在菜单时忽略。</summary>
    public static void RecordCurrent(PlaybackEngine engine, ChoiceOption option)
    {
        if (engine.Mode == RunMode.Choice && engine.Current is { Kind: "choice" } menu && menu.Options.Any(o => o.Id == option.Id)) Add(engine, menu.Id, option.Id);
    }
    public static IReadOnlySet<string> Heard(PlaybackEngine engine, string menuId)
    {
        var map = table.GetValue(engine, _ => new(StringComparer.Ordinal));
        lock (map) return map.TryGetValue(menuId, out var set) ? new HashSet<string>(set, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
    }
    public static void Add(PlaybackEngine engine, string menuId, string optionId)
    {
        var map = table.GetValue(engine, _ => new(StringComparer.Ordinal));
        lock (map) { if (!map.TryGetValue(menuId, out var set)) map[menuId] = set = new(StringComparer.Ordinal); set.Add(optionId); }
        var lastMap = last.GetValue(engine, _ => new(StringComparer.Ordinal));
        lock (lastMap) lastMap[menuId] = optionId;
        ExplorationPanel.Invalidate(engine);
    }
}

/// <summary>0.4.7 游戏配音探索面板（设计 13）的一个选项：已读、是否正在读、是否出口 / 空回环。</summary>
public sealed record ExplorationPanelOption(string Id, string Label, bool Heard, bool Current, bool Exit, bool EmptyLoop);
/// <summary>正在读的探索菜单（菜单 id、菜单文字、全部选项与已读状态）。</summary>
public sealed record ExplorationPanel(string MenuId, string MenuText, IReadOnlyList<ExplorationPanelOption> Options)
{
    public int HeardCount => Options.Count(o => o.Heard);
    /// <summary>按导航器缓存最近一次 Of 结果（pack/section/menu/line + 已读指纹）；探索记忆变更或配音包重载后失效。</summary>
    static readonly ConditionalWeakTable<PlaybackEngine, Slot> cache = new();
    sealed class Slot { public string? Key; public ExplorationPanel? Panel; }
    /// <summary>探索记忆写入后清掉该导航器的面板缓存（下次 Of 按新已读重算）。</summary>
    public static void Invalidate(PlaybackEngine engine)
    {
        if (cache.TryGetValue(engine, out var slot)) { slot.Key = null; slot.Panel = null; }
    }
    /// <summary>当前句属于某个探索菜单的回环 / 出口选项时返回该菜单面板；否则 null（共同线、普通分支、非台词）。
    /// OwningOption / MenuShape 已按配音包索引；此处再按导航器缓存完整面板，探索记忆变更时 <see cref="Invalidate"/>。</summary>
    public static ExplorationPanel? Of(PlaybackEngine engine)
    {
        if (engine.Current is not { Kind: "line", Archived: false } line) return null;
        if (PgrVoice.Following.PlayAlongCountdown.OwningOption(engine.Pack, line, out var menu) is not { } current || menu == null) return null;
        var shape = MenuShape.Of(engine.Pack, menu);
        if (!shape.Exploration) return null;
        var heard = ExplorationMemory.Heard(engine, menu.Id);
        string heardKey = string.Join("", heard.OrderBy(id => id, StringComparer.Ordinal));
        string key = engine.Pack.Id + "" + menu.SectionId + "" + menu.Id + "" + line.Id + "" + current.Id + "" + heardKey;
        var slot = cache.GetValue(engine, _ => new Slot());
        lock (slot)
        {
            if (slot.Key == key) return slot.Panel;
            var panel = new ExplorationPanel(menu.Id, menu.Text, shape.Options.Select(o => new ExplorationPanelOption(o.Id, o.Label,
                heard.Contains(o.Id) || o.Id == current.Id, o.Id == current.Id, shape.Exits.Any(x => x.Id == o.Id), shape.EmptyLoops.Any(x => x.Id == o.Id))).ToList());
            slot.Key = key; slot.Panel = panel;
            return panel;
        }
    }
}
