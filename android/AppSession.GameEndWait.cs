using PgrVoice.AndroidApp.Platform;
using PgrVoice.Following;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.7 游戏伴音的终点规则：自动播放到达未归档 end 节点（选项出口或小节正文末尾）时不关闭，
/// 显示终点自己的文字（兜底「请按游戏画面定位后续」），继续 OCR 读取游戏画面；唯一定位到下一句（含下一小节首句）后自动续播。
/// 每次进入等待写一条播放记录（含终点文字）。听书模式不受影响。
/// </summary>
public sealed partial class AppSession
{
    GameEndWait? gameEndWait;
    /// <summary>正在终点等待时的票据；未等待时为 null。</summary>
    public GameEndWait? GameEndWaiting => autoPlay.WaitingForGame ? gameEndWait : null;

    bool TryBeginGameEndWait(PlaybackEngine engine, CommonAutoPlayDecision decision, long epoch)
    {
        if (!GameEndWaitPolicy.TryBegin(engine, decision, out var wait) || wait == null) return false;
        if (!autoPlay.BeginWaiting(epoch, wait.SourceId)) return false;
        gameEndWait = wait;
        // 旧画面上的末句仍可能被识别；换代后只接受与末句不同、唯一且连续两次一致的新台词。
        ResetAutoConsensus(); ocrEpoch++; ocrSchedule.Reset(); pendingFrame.Clear(); manualOcr = false;
        LogGame(PlaybackLog.Types.EndWait, engine, wait.EndId ?? wait.SourceId, GameEndWaitPolicy.LogReason(wait));
        Diagnostics.Log("自动播放终点等待", $"{wait.SourceId} → {wait.EndId ?? "（无终点节点）"}；{wait.Hint}；搜索 {string.Join(",", wait.SearchSections)}");
        Status = wait.Hint + " · 等待游戏画面定位，自动播放保持开启";
        overlay.ClearAutoPlaybackPaused();
        overlay.ShowGameEndWaiting(wait.Hint, GameEndWaitPolicy.Detail);
        Notify();
        return true;
    }

    void ClearGameEndWait()
    {
        if (gameEndWait == null) return;
        gameEndWait = null;
        overlay.ClearGameEndWaiting();
    }

    void HandleGameEndWaitObservation(PlaybackEngine engine, FollowObservation observation)
    {
        if (gameEndWait is not { } wait || !CheckAutoPlaybackEnvironment()) return;
        if (!observation.IsStable || observation.Sequence <= autoLastSequence) return;
        autoLastSequence = observation.Sequence;
        var blocks = observation.Blocks.Where(b => double.IsFinite(b.Score) && b.Score >= .80).ToList();
        var all = wait.SearchSections.SelectMany(section => Matcher.FindAll(engine, section, blocks)).ToList();
        if (all.Any(c => c.Node.Kind == "choice" && c.Score >= .94))
        {
            ResetAutoConsensus();
            SetEndWaitStatus(wait, "画面出现选项：请在游戏中选择，选后台词出现后自动继续。");
            return;
        }
        var exact = all.Where(c => c.Node.Kind == "line" && c.Node.Id != wait.SourceId &&
            Matcher.Normalize(c.Evidence) == Matcher.Normalize(c.Node.Text) && Matcher.Normalize(c.Node.Text).Length >= 2 &&
            (Matcher.Normalize(c.Node.Text).Length > 3 || blocks.Any(b => !string.IsNullOrWhiteSpace(c.Node.Speaker) && Matcher.Normalize(b.Text) == Matcher.Normalize(c.Node.Speaker))))
            .GroupBy(c => c.Node.Id).Select(g => g.First().Node).ToList();
        // 同文多句时只认下一小节首句（例如 38-2 开头与节内另一句同文），否则继续等待。
        if (GameEndWaitPolicy.Pick(engine.Pack, wait, exact) is not { } node) { ResetAutoConsensus(); return; }
        if (autoPendingNode == node.Id && autoPendingFrame == observation.FrameKey) autoConsensus++;
        else { autoPendingNode = node.Id; autoPendingFrame = observation.FrameKey; autoConsensus = 1; }
        if (autoConsensus < 2) { SetEndWaitStatus(wait, "已识别到新台词，正在核对。"); return; }
        if (!GameEndWaitPolicy.CanResume(engine, wait, node, out var probe, out var why) || probe == null)
        { ResetAutoConsensus(); SetEndWaitStatus(wait, $"识别到「{Clip(node.Text)}」，{why}；继续等待。"); return; }
        if (autoTarget is { } sourceTarget && autoDisplay.RegionKey is { } sourceKey &&
            TryWaitAtSourceChoicePreview(engine, probe, sourceTarget, sourceKey)) return;
        if (!CanStartConfirmedAutoRoute(probe, out var routeReason))
        { ResetAutoConsensus(); SetEndWaitStatus(wait, routeReason + " 继续等待。"); return; }
        if (!CanAutoPlayLine(engine.Pack, node))
        { ResetAutoConsensus(); SetEndWaitStatus(wait, "这句没有可用配音；继续等待下一句。"); return; }
        ClearGameEndWait();
        Diagnostics.Log("终点等待恢复", $"{wait.EndId ?? wait.SourceId} → {node.Id}");
        ResumeAutoAtLocatedLine(engine, probe, node);
    }

    void SetEndWaitStatus(GameEndWait wait, string detail)
    {
        Status = wait.Hint + " · " + detail;
        overlay.ShowGameEndWaiting(wait.Hint, GameEndWaitPolicy.Detail + "\n" + detail);
    }

    static string Clip(string text) => text.Length <= 16 ? text : text[..16] + "…";
}
