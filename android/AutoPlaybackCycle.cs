namespace PgrVoice.AndroidApp;

/// <summary>首次确认位置后按共同剧情顺序播放；每次自然完成最多授权一个手势。</summary>
/// <remarks>0.4.7：WaitingForGame = 到达 end 节点后保持自动播放、读取游戏画面等待下一句；不设超时、不点击游戏。</remarks>
public enum AutoPlaybackPhase { Off, CheckingCurrent, Playing, Tapping, StartingNext, SilentPause, WaitingForGame }

public sealed class AutoPlaybackCycle
{
    public AutoPlaybackPhase Phase { get; private set; }
    public bool Running => Phase != AutoPlaybackPhase.Off;
    public long Epoch { get; private set; }
    public string NodeId { get; private set; } = "";
    public string? ExpectedNext { get; private set; }
    public long AudioTicket { get; private set; }
    public long TapTicket { get; private set; }
    public long Deadline { get; private set; }
    public const int SilentPauseMilliseconds = 1200;
    public long SilentPauseUntil { get; private set; }
    /// <summary>0.4.7 无音频句停留（待配音 / 无配音 / 仅字幕 / 标点停顿）开始时刻与总时长（毫秒，SystemClock.ElapsedRealtime 时基）；不在停留时为 0。</summary>
    public long SilentPauseStarted { get; private set; }
    public long SilentPauseDuration { get; private set; }
    long nextTapTicket;
    long lastAudioTicket;
    public void Start(string nodeId, long now)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        NodeId=nodeId; Phase=AutoPlaybackPhase.CheckingCurrent; Deadline=now+15_000;
    }
    public void Stop()
    {
        Epoch++; Phase=AutoPlaybackPhase.Off; NodeId=""; ExpectedNext=null;
        AudioTicket=0; TapTicket=0; Deadline=0; lastAudioTicket=0; SilentPauseUntil=0; SilentPauseStarted=0; SilentPauseDuration=0;
    }
    public bool PreparePlayback(long epoch,string nodeId,long now)
    {
        if (epoch != Epoch || string.IsNullOrWhiteSpace(nodeId) || TimedOut(now)) return false;
        if (Phase==AutoPlaybackPhase.CheckingCurrent && nodeId==NodeId || Phase==AutoPlaybackPhase.StartingNext && nodeId==ExpectedNext)
        {
            // Preparing the node does not prove that the foreground service has returned a
            // usable audio request. Keep a bounded wait until BindAudio succeeds.
            NodeId=nodeId; ExpectedNext=null; AudioTicket=0; TapTicket=0; Deadline=now+10_000;
            Phase=AutoPlaybackPhase.Playing; return true;
        }
        return false;
    }
    // AndroidAudioPlayer.PlayTagged 的票据单调递增；同一次会话不能重新绑定已经用过的音频票据。
    public bool BindAudio(long epoch,string nodeId,long ticket,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Playing||NodeId!=nodeId||ticket<=lastAudioTicket||AudioTicket!=0||TimedOut(now))return false;
        AudioTicket=ticket;lastAudioTicket=ticket;Deadline=0;return true;
    }
    public bool CompleteAudio(long epoch,long ticket,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Playing||ticket<=0||ticket!=AudioTicket)return false;
        AudioTicket=0;TapTicket=0;ExpectedNext=null;Phase=AutoPlaybackPhase.Tapping;Deadline=now+4_000;return true;
    }
    /// <param name="milliseconds">0.4.7 待配音句按字数停留；省略时为纯标点短停顿。</param>
    public bool BeginSilentPause(long epoch,string nodeId,long now,int? milliseconds=null)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Playing||NodeId!=nodeId||AudioTicket!=0||TimedOut(now))return false;
        SilentPauseStarted=now;SilentPauseDuration=milliseconds??SilentPauseMilliseconds;
        SilentPauseUntil=now+SilentPauseDuration;Deadline=SilentPauseUntil+10_000;
        Phase=AutoPlaybackPhase.SilentPause;return true;
    }
    public bool CompleteSilentPause(long epoch,string nodeId,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.SilentPause||NodeId!=nodeId||now<SilentPauseUntil||TimedOut(now))return false;
        SilentPauseUntil=0;SilentPauseStarted=0;SilentPauseDuration=0;TapTicket=0;ExpectedNext=null;Phase=AutoPlaybackPhase.Tapping;Deadline=now+4_000;return true;
    }
    /// <summary>0.4.7 终点等待：本句已播完（Tapping 且尚未发出点击）后进入等待；不点击、不超时，定位到下一句后由 Start 换代续播。</summary>
    public bool BeginWaiting(long epoch,string nodeId)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Tapping||TapTicket!=0||NodeId!=nodeId)return false;
        ExpectedNext=null;Deadline=0;Phase=AutoPlaybackPhase.WaitingForGame;return true;
    }
    public bool WaitingForGame=>Phase==AutoPlaybackPhase.WaitingForGame;
    /// <summary>原配句：静音占位音频先播完时，保持本句待点击直到游戏原声按配置时长结束；不发点击、不换票据。</summary>
    public bool HoldTapping(long epoch,string nodeId,long until)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Tapping||TapTicket!=0||NodeId!=nodeId)return false;
        Deadline=until+4_000;return true;
    }
    /// <summary>0.4.7 游戏配音「等待确认续接」倒计时到点 / 用户「按顺序继续」：从等待回到可点击（未发出点击），之后照常 BeginTap。</summary>
    public bool ResumeTapping(long epoch,string nodeId,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.WaitingForGame||NodeId!=nodeId)return false;
        TapTicket=0;ExpectedNext=null;Phase=AutoPlaybackPhase.Tapping;Deadline=now+4_000;return true;
    }
    public bool BeginTap(long epoch,string expectedNext,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Tapping||TapTicket!=0||TimedOut(now)||
            string.IsNullOrWhiteSpace(expectedNext)||expectedNext==NodeId)return false;
        ExpectedNext=expectedNext;TapTicket=++nextTapTicket;Deadline=now+4_000;return true;
    }
    public bool GestureCompleted(long epoch,long tapTicket,string expectedNext,long now)
    {
        if(epoch!=Epoch||Phase!=AutoPlaybackPhase.Tapping||tapTicket<=0||tapTicket!=TapTicket||
            ExpectedNext!=expectedNext||TimedOut(now))return false;
        Phase=AutoPlaybackPhase.StartingNext;Deadline=now+10_000;return true;
    }
    public bool TimedOut(long now)=>Running&&Deadline>0&&now>=Deadline;
}
