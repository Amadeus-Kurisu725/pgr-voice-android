using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 现稿续接（店主决定的策略 B）：出口或正文尚未逐句核实时，游戏伴音按配音包写好的连接继续；只在包内确实没有下一处时停。
/// 适用三处停顿：
///  1. 已核段落播完、导航器进入 v3 边界缺口（Gap，resumeMenuIds 为空）或停在段尾台词；
///  2. 其他缺口节点（Gap）自身写有同小节的 NextId；
///  3. 玩家/自动播放选了正文未核的选项，导航器拒绝进入（ReviewRoute）而包内写有该选项的目标。
/// 只经过同小节、未归档、无事实设置的 return/merge/gap 结构节点，终点为台词或菜单；用克隆导航器验证真实导航可按“定位”接受终点。
/// 只读：不改变导航、不播放、不点击游戏。背景：第38章 38-1 等配音包选项段落以 return→merge→下一菜单 相连，但未登记 exitVerified 与
/// resumeMenuIds，原先播完段落即停在待续接边界；浮点纪实等包的段落缺口也写有 NextId。
/// </summary>
public static class DeclaredContinuationPolicy
{
    /// <param name="Option">经过的未核选项（缺口不属于任何选项时为 null）。</param>
    public sealed record Continuation(ChoiceOption? Option, Node Target, Node From);
    const int MaxSteps = 32;

    public static bool TryResolve(PlaybackEngine engine, out Continuation? result)
    {
        result = null;
        var pack = engine.Pack;
        if (pack.SchemaVersion != 3 || engine.Mode == RunMode.Original || engine.Current is not { Archived: false } current) return false;
        // 选项正文“并联”：本句之后包内各句都直接接出口、后句无入边时，先按 lineIds 补读该孤立正文（见 OptionBodyOrder）。
        bool singleLine = engine.ExportNavigation().Current.SingleLine;
        if (engine.Mode is RunMode.Following or RunMode.Gap)
        {
            var read = new HashSet<string>(engine.History.Select(v => v.NodeId), StringComparer.Ordinal);
            Node? from = engine.Mode == RunMode.Following ? (current.Kind == "line" ? current : null) : PreviousLine(engine, current);
            if (from != null && OptionBodyOrder.NextOrphan(pack, from, read.Contains) is { } orphan && Accepts(engine, orphan))
            { result = new(OptionBodyOrder.Owner(pack, from), orphan, current); return true; }
        }
        ChoiceOption? option = null; string? start;
        if (engine.Mode == RunMode.Gap && current.Kind == "gap")
        {
            if (current.ResumeMenuIds.Count > 0) return false; // 有登记的续接菜单：沿用原有选择
            var owners = pack.Nodes.Where(n => !n.Archived && n.Kind == "choice" && n.SectionId == current.SectionId)
                .SelectMany(n => n.Options).Where(o => o.BoundaryId == current.Id && o.PathId == current.PathId).ToList();
            if (owners.Count == 1 && owners[0].BodyVerified && !owners[0].ExitVerified)
            {
                option = owners[0];
                // v3 边界缺口通常无入边：从已核段落最后一句写的下一处继续。
                start = option.SegmentIds.Select(id => pack.ById.GetValueOrDefault(id)).Where(n => n is { Kind: "line", Archived: false })
                    .Select(n => n!.NextId).LastOrDefault(next => next != null && !option.SegmentIds.Contains(next) && next != current.Id)
                    ?? current.NextId ?? option.ReturnId ?? option.MergeId; // 段落内含嵌套菜单时边界缺口不写 NextId：按选项登记的返回/汇合位置（与听书一致）
            }
            else start = current.NextId;
        }
        else if (engine.Mode == RunMode.Gap && current.Kind is "line" or "merge" or "return" && current.NextId != null)
        {
            // 导航器在台词上报“此连接尚未核实”（下一处为未核出口的 return/merge）：按现稿写的下一处继续。
            option = OptionBodyOrder.Owner(pack, current); start = current.NextId;
        }
        else if (engine.Mode == RunMode.Following && current.Kind == "line" && singleLine && current.NextId != null)
        {
            // 未核内容按单句定位播放，导航器下一步会回到菜单；现稿续接沿本句写的下一处继续（下一句仍按单句定位确认）。
            option = OptionBodyOrder.Owner(pack, current); start = current.NextId;
        }
        else if (engine.Mode == RunMode.Following && current.Kind == "line" && !singleLine &&
            current.NextId != null && pack.ById.TryGetValue(current.NextId, out var gap) && gap is { Kind: "gap", Archived: false } && gap.ResumeMenuIds.Count == 0)
        {
            // 下一处是写有 NextId 的缺口标记：导航器会停在缺口，这里提前按现稿越过。
            start = current.NextId;
        }
        else if (engine.Mode == RunMode.Following && current.Kind == "line" && current.PathId.Length > 0 && !singleLine)
        {
            var owners = pack.Nodes.Where(n => !n.Archived && n.Kind == "choice" && n.SectionId == current.SectionId)
                .SelectMany(n => n.Options).Where(o => o.PathId == current.PathId).ToList();
            if (owners.Count != 1) return false;
            option = owners[0];
            // 与导航器的段尾判断一致：已核段落最后一句，下一处已不在段落内（导航器会转入边界缺口）。
            if (!option.BodyVerified || option.ExitVerified || !option.SegmentIds.Contains(current.Id) || current.NextId == null ||
                option.SegmentIds.Contains(current.NextId) || current.NextId == option.BoundaryId) return false;
            start = current.NextId;
        }
        else if (engine.Mode == RunMode.Choice && current.Kind == "choice" && engine.ReviewRoute is { } review)
        {
            option = current.Options.FirstOrDefault(o => o.Id == review);
            if (option == null || option.BodyVerified) return false;
            start = option.TargetId;
        }
        else return false;
        if (start == null) return false;

        var seen = new HashSet<string>(StringComparer.Ordinal) { current.Id };
        string? id = start;
        for (int step = 0; step < MaxSteps && id != null && seen.Add(id); step++)
        {
            if (!pack.ById.TryGetValue(id, out var node) || node.Archived || node.SectionId != current.SectionId || node.SetFacts.Count != 0) return false;
            if (node.Kind is "return" or "merge") { id = node.NextId; continue; }
            if (node.Kind == "gap")
            {
                if (node.ResumeMenuIds.Count > 0)
                {
                    // 现稿链中途的“等待游戏交互”缺口：导航器不会停在这里出示续接菜单，按现稿写的下一处（须是登记菜单之一）或唯一登记菜单继续。
                    string? menu = node.NextId != null && node.ResumeMenuIds.Contains(node.NextId) ? node.NextId
                        : node.ResumeMenuIds.Count == 1 ? node.ResumeMenuIds[0] : null;
                    if (menu == null) return false; // 多个续接菜单且现稿未指明：交给玩家选择
                    id = menu; continue;
                }
                if (node.NextId == null) return false; // 真实缺口
                id = node.NextId; continue;
            }
            if (node.Kind is not ("choice" or "line") || !Accepts(engine, node)) return false;
            result = new(option, node, current); return true;
        }
        return false;
    }

    /// <summary>用克隆导航器验证真实导航可按“定位”接受该终点（菜单静音打开 / 台词确认）。</summary>
    static bool Accepts(PlaybackEngine engine, Node node)
    {
        var probe = new PlaybackEngine(engine.Pack);
        if (!probe.ImportNavigation(engine.ExportNavigation())) return false;
        bool accepted = node.Kind == "choice" ? probe.OpenGameMenu(node.Id, node.SectionId) && probe.Mode == RunMode.Choice
            : probe.ConfirmGameLine(node.Id) && probe.Mode == RunMode.Following;
        return accepted && probe.CurrentId == node.Id;
    }

    /// <summary>停在缺口时，导航历史中紧接其前、且以 nextId 指向该缺口的台词。</summary>
    static Node? PreviousLine(PlaybackEngine engine, Node gap)
    {
        int i = engine.HistoryPosition - 1;
        if (i < 0 || i >= engine.History.Count || !engine.Pack.ById.TryGetValue(engine.History[i].NodeId, out var line)) return null;
        return line.Kind == "line" && line.NextId == gap.Id ? line : null;
    }

    /// <summary>按现稿连接把真实导航定位到续接终点（菜单静音打开；台词按定位确认并播放）。</summary>
    public static bool Follow(PlaybackEngine engine, Continuation continuation)
    {
        var target = continuation.Target;
        return target.Kind == "choice" ? engine.OpenGameMenu(target.Id, target.SectionId) : engine.ConfirmGameLine(target.Id);
    }
}
