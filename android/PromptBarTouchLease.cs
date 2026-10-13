namespace PgrVoice.AndroidApp;

/// <summary>
/// 自动点击游戏期间暂时隐藏提示窗口并禁用触碰，手势完成 / 取消 / 异常后恢复。
/// 不论条在哪里都这样做，自动点击不会落在条上。
/// · 可重入：同时有多次点击时，第一次开始设为不可触碰，最后一次结束才恢复。
/// · 每次占用都有超时兜底（默认 3 秒，远长于 60 ms 手势 + 回调），到点强制归还，条不会永久不可点。
/// · 归还幂等：同一占用多次 Dispose、超时后再 Dispose 都只算一次。
/// 纯逻辑，不依赖 Android：setTouchable 由悬浮层实现，schedule 由主线程 Handler.PostDelayed 实现。
/// </summary>
public sealed class PromptBarTouchLease
{
    public const int DefaultTimeoutMs = 3000;
    readonly Action<bool> setTouchable;
    readonly Action<Action, int> schedule;
    readonly int timeoutMs;
    readonly object gate = new();
    int holds;

    public PromptBarTouchLease(Action<bool> setTouchable, Action<Action, int> schedule, int timeoutMs = DefaultTimeoutMs)
    { this.setTouchable = setTouchable; this.schedule = schedule; this.timeoutMs = timeoutMs; }

    /// <summary>当前是否处于“条不可触碰”状态。</summary>
    public bool Suspended { get { lock (gate) return holds > 0; } }

    public IDisposable Acquire()
    {
        // 通知与计数同序：旧 Release 的恢复不能晚于新 Acquire 的隐藏。
        lock (gate)
            if (++holds == 1) Safe(() => setTouchable(false));
        var hold = new Hold(this);
        try { schedule(hold.Dispose, timeoutMs); } catch { }
        return hold;
    }

    void Release()
    {
        lock (gate)
        {
            if (holds == 0) return;
            if (--holds == 0) Safe(() => setTouchable(true));
        }
    }

    static void Safe(Action action) { try { action(); } catch { } }

    sealed class Hold(PromptBarTouchLease owner) : IDisposable
    {
        int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) owner.Release(); }
    }
}
