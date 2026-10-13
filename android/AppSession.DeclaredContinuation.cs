using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

public sealed partial class AppSession
{
    /// <summary>当前位置可按现稿续接时返回终点；供剧情页 / 悬浮页显示入口。只读。</summary>
    public DeclaredContinuationPolicy.Continuation? DeclaredContinuation =>
        Engine is { } engine && DeclaredContinuationPolicy.TryResolve(engine, out var continuation) ? continuation : null;

    // 现稿续接段：经现稿连接到达的台词所在路线；离开该路线、进入菜单或换小节即结束。
    (PlaybackEngine Engine, string Section, string Path)? draftContinuation;

    /// <summary>正在播放现稿续接段（状态行显示小标签“现稿续接”）。</summary>
    public bool DraftContinuationActive => draftContinuation is { } d && Engine is { } e && ReferenceEquals(d.Engine, e) &&
        e.Mode is RunMode.Following or RunMode.Paused or RunMode.Merge && e.Current is { } c && c.SectionId == d.Section && c.PathId == d.Path;

    /// <summary>状态行文字：现稿续接段前加短标签。</summary>
    public string StatusLine => !DraftContinuationActive ? Status
        : Status.StartsWith(UiText.PendingVoiceTag, StringComparison.Ordinal) ? UiText.PendingVoiceTag + UiText.DraftStatusTag + Status[UiText.PendingVoiceTag.Length..] // 0.4.7：待配音标签保持在最前
        : Status.StartsWith(PendingVoice.NoVoiceTag, StringComparison.Ordinal) ? PendingVoice.NoVoiceTag + UiText.DraftStatusTag + Status[PendingVoice.NoVoiceTag.Length..]
        : UiText.DraftStatusTag + Status;

    /// <summary>
    /// 现稿续接（策略 B）：停在待续接缺口、或所选未核正文被导航器拒绝时，按配音包写好的连接继续——菜单静音打开，台词按定位确认并播放。
    /// 不点击游戏、不代选；包内确实没有下一处时保持原样（真实缺口，需定位）。
    /// </summary>
    bool TryFollowDraftContinuation(PlaybackEngine engine)
    {
        if (engine.Mode is not (RunMode.Gap or RunMode.Choice) || !DeclaredContinuationPolicy.TryResolve(engine, out var continuation) ||
            continuation == null || !DeclaredContinuationPolicy.Follow(engine, continuation)) return false;
        MarkDraftContinuation(engine, continuation);
        return true;
    }

    static DeclaredContinuationPolicy.Continuation? DraftAfterSingleLine(PlaybackEngine engine) =>
        engine.Mode == RunMode.Following && engine.ExportNavigation().Current.SingleLine &&
        DeclaredContinuationPolicy.TryResolve(engine, out var c) ? c : null;

    /// <summary>单句定位的未核正文被推进回所属菜单时，按事先解析的现稿续接改到下一处。其他操作（定位别句、跳转等）不受影响。</summary>
    bool FollowDraftAfterSingleLine(PlaybackEngine engine, DeclaredContinuationPolicy.Continuation? pending, string? beforeId, string? beforePendingMenu = null)
    {
        if (pending == null || beforeId == null || engine.Mode != RunMode.Choice || !engine.Pack.ById.TryGetValue(beforeId, out var before)) return false;
        // 只处理“单句推进回到菜单”：回到的是该句所属选项的菜单，或导航器记下的待返回菜单。定位别句、跳转等不受影响。
        bool ownMenu = OptionBodyOrder.Owner(engine.Pack, before) is { } option && engine.Pack.Nodes.Any(n => n.Id == engine.CurrentId && n.Options.Contains(option));
        if (!ownMenu && (beforePendingMenu == null || engine.CurrentId != beforePendingMenu)) return false;
        if (!DeclaredContinuationPolicy.Follow(engine, pending)) return false;
        MarkDraftContinuation(engine, pending);
        return true;
    }

    void MarkDraftContinuation(PlaybackEngine engine, DeclaredContinuationPolicy.Continuation continuation)
    {
        var target = continuation.Target;
        draftContinuation = target.Kind == "line" ? (engine, target.SectionId, target.PathId) : null;
        Diagnostics.Log("现稿续接", (continuation.Option?.Id ?? continuation.From.Id) + " → " + target.Id);
    }

    /// <summary>剧情页 / 悬浮页入口：按现稿连接续接（与自动续接相同，供自动续接未触发时手动使用）。</summary>
    public void FollowDeclaredContinuation() => Command(engine =>
    {
        if (!DeclaredContinuationPolicy.TryResolve(engine, out var continuation) || continuation == null ||
            !DeclaredContinuationPolicy.Follow(engine, continuation))
            throw new InvalidOperationException(string.IsNullOrEmpty(engine.NavigationError) ? "续接位置已变化，请重新定位。" : engine.NavigationError);
        MarkDraftContinuation(engine, continuation);
        overlay.ClearAutoPlaybackPaused();
        SectionId = engine.Current!.SectionId;
    }, true);
}
