using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp;
using PgrVoice.AndroidApp.Platform;
using System.Reflection;
using System.Text.Json;

namespace PromptWindowDevice.Probe;

// 此接收器仅由独立测试项目显式编入，正式项目排除整个 Tests 目录。
[BroadcastReceiver(Name = "cn.pgrvoice.promptprobe.ProbeReceiver", Exported = true)]
[IntentFilter(new[] { "cn.pgrvoice.promptprobe.COMMAND", "cn.pgrvoice.promptprobe.SINK_EVENT" })]
public sealed class ProbeReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        if (intent.GetStringExtra("command") == "listening-setup")
        {
            var pending = GoAsync();
            ProbeState.Execute(context.ApplicationContext!, intent, () => pending?.Finish());
        }
        else ProbeState.Execute(context.ApplicationContext!, intent);
    }
}

static class ProbeState
{
    static readonly Handler main = new(Looper.MainLooper!);
    static readonly List<object> events = new();
    static readonly List<IDisposable> holds = new();
    static OverlayController? overlay;
    static PromptBarTouchLease? lease;
    static Context? context;
    static long session;
    static bool paused;
    static bool tapInProgress;
    static GameAdvanceAvailability? lastAvailability;
    static int commands, choices, pauses, sinkUps;
    static string kind = "none", request = "", lastResult = "", lastReason = "";
    const string Sink = "cn.pgrvoice.touchsink";
#if PROBE_BASELINE
    const string Variant = "baseline-25226cb";
#else
    const string Variant = "working-tree";
#endif
    public static void Execute(Context value, Intent i, Action? asynchronousDone = null)
    {
        context = value;
        try
        {
            if (i.Action == "cn.pgrvoice.promptprobe.SINK_EVENT")
            {
                sinkUps = i.GetIntExtra("count", 0);
                Record("sink-up", new { sinkUps, x = i.GetFloatExtra("x", -1), y = i.GetFloatExtra("y", -1), flags = i.GetStringExtra("flags") });
                return;
            }
            request = i.GetStringExtra("id") ?? DateTime.UtcNow.Ticks.ToString();
            var command = i.GetStringExtra("command") ?? "state";
            if (overlay == null)
            {
                overlay = new OverlayController(value);
                overlay.Error += message => Record("overlay-error", message);
                overlay.Command += command => { commands++; Record("human-command", command.ToString()); };
                lease = new PromptBarTouchLease(overlay.SetPromptBarsTouchable, (action, ms) => main.PostDelayed(action, ms));
                GameAdvanceAccessibilityService.BeforeTap = lease.Acquire;
#if !PROBE_BASELINE
                GameAdvanceAccessibilityService.PromptWindowIdsBeforeTap = overlay.GetPromptWindowIds;
#endif
                GameAdvanceAccessibilityService.SessionInvalidated += message => Record("session-invalidated", message);
            }
            switch (command)
            {
                case "listening-setup":
                    overlay.ClearPromptBar(); overlay.ClearBranchEntered(); overlay.Hide(); kind = "none";
                    ListeningProbe.Setup(value, i, Record, asynchronousDone ?? (() => { })); break;
                case "listening-state": case "listening-reopen": case "listening-page": case "listening-toggle": case "listening-service":
                    ListeningProbe.Command(value, i, Record); break;
                case "show": Show(i.GetStringExtra("kind") ?? "countdown"); break;
                case "clear": overlay.ClearPromptBar(); overlay.ClearBranchEntered(); overlay.Hide(); kind = "none"; break;
                case "arm": Arm(); break;
                case "tap": Tap(i); break;
                case "hold": holds.Add(lease!.Acquire()); break;
                case "release": if (holds.Count > 0) { holds[0].Dispose(); holds.RemoveAt(0); } break;
                case "pause": paused = !paused; Show(kind); break;
                case "cancel": GameAdvanceAccessibilityService.CancelSession("夹具明确取消"); break;
                case "capture": overlay.SetCaptureHidden(i.GetBooleanExtra("hidden", true)); break;
                case "line": overlay.UpdateCurrentLine(i.GetStringExtra("speaker") ?? "", i.GetStringExtra("text") ?? ""); break;            }
            Record(command, null);
            main.PostDelayed(() => Record(command + "-settled", null), 160);
        }
        catch (Exception ex) { Record("exception", ex.ToString()); asynchronousDone?.Invoke(); }
    }
    static void Show(string next)
    {
        overlay!.Show(); kind = next;
        if (next is "explore" or "explore-both")
            overlay.ShowExploreBar(new ExploreBarModel("device-menu", "29-1 营地 · 可交谈人物", "建议先找测试人物", new[] {
                new ExploreBarRow("a", "测试人物一", false, false, true), new ExploreBarRow("b", "测试人物二", false, false, true),
                new ExploreBarRow("c", "测试人物三", true, false, true), new ExploreBarRow("exit", "结束交谈", false, true, true)
            }), chosen => { choices++; Record("human-choice", chosen); });
        else if (next is "countdown" or "both" or "ending" or "order")
            overlay.ShowCountdownBar(new CountdownBarModel(next == "order", next == "ending", false, paused, 15, 1,
                "测试角色", "这是一段用于测量提示窗口的旧场景测试正文。", "已完成的测试选项",
                new[] { new PromptBarOption("a", "选项一：保持当前路线", true), new PromptBarOption("b", "选项二：查看其他路线", false) },
                "下一位角色", "按顺序继续的测试正文。", paused ? "已暂停" : "十五秒测试窗口"),
                chosen => { choices++; Record("human-choice", chosen); }, () => { choices++; Record("human-order", null); },
                () => { pauses++; paused = !paused; Show(kind); Record("human-pause", paused); });
        else overlay.ClearPromptBar();
        if (next is "selected" or "both" or "explore-both")
            overlay.ShowAutoSelectedBar("已选择：测试选项", "只读提示条与可操作窗口重叠测试");
        if (next == "top-hint") overlay.ShowTopHint("回到游戏后继续。此条用于验证真实只读提示窗口。");
    }
    static void Arm()
    {
        var available = GameAdvanceAccessibilityService.GetAvailability(Sink);
        lastAvailability = available;
        session = GameAdvanceAccessibilityService.BeginSession(Sink, available.DisplayWidth, available.DisplayHeight, available.Rotation);
        Record("armed", new { session, available });
    }
    static void Tap(Intent i)
    {
        if (i.GetBooleanExtra("arm", true)) Arm();
        float x = i.GetFloatExtra("x", -1), y = i.GetFloatExtra("y", -1);
        var field = i.GetStringExtra("window") == "selected" || kind is "selected" or "top-hint" ? "branchEnteredView" : "promptBarView";
        if ((x < 0 || y < 0) && GetView(field) is { } view)
        {
            int[] position = new int[2]; view.GetLocationOnScreen(position);
            x = position[0] + view.Width / 2f; y = position[1] + view.Height / 2f;
        }
        if (x < 0 || y < 0) { Record("tap-missing-target", null); return; }
        string requestId = request;
        int before = sinkUps;
        var token = session;
        tapInProgress = true;
        Record("tap-before", new { requestId, token, x, y, before });
        // 直接调用生产 TapAsync。没有 adb input、没有自写 DispatchGesture，也不为穿透插入额外等待。
        Task<GameAdvanceTapResult> task = i.GetBooleanExtra("background", false)
            ? Task.Run(() => GameAdvanceAccessibilityService.TapAsync(token, x, y))
            : GameAdvanceAccessibilityService.TapAsync(token, x, y);
        if (i.GetBooleanExtra("cancelImmediately", false)) GameAdvanceAccessibilityService.CancelSession("夹具在 TapAsync 返回后立即取消");
        int cancelAfterMs = i.GetIntExtra("cancelAfterMs", -1);
        if (cancelAfterMs >= 0) main.PostDelayed(() => GameAdvanceAccessibilityService.CancelSession("夹具延时取消"), cancelAfterMs);
        int blockMainMs = Math.Clamp(i.GetIntExtra("blockMainMs", 0), 0, 2000);
        if (blockMainMs > 0) main.PostDelayed(() =>
        {
            Record("test-main-block-start", new { requestId, blockMainMs });
            System.Threading.Thread.Sleep(blockMainMs);
            Record("test-main-block-end", new { requestId, blockMainMs });
        }, 1);
        main.PostDelayed(() => Record("tap-inflight", new { requestId }), 20);
        _ = task.ContinueWith(t => main.Post(() =>
        {
            tapInProgress = false;
            if (t.IsCompletedSuccessfully) { lastResult = t.Result.Status.ToString(); lastReason = t.Result.Reason; }
            else { lastResult = "exception"; lastReason = t.Exception?.ToString() ?? "cancelled"; }
            Record("tap-result", new { requestId, status = lastResult, reason = lastReason, before, sinkUps });
            main.PostDelayed(() => Record("tap-restored", new { requestId, before, sinkUps, deliveredDelta = sinkUps - before }), 250);
        }));
    }
    static View? GetView(string field) => typeof(OverlayController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(overlay) as View;
    static object AccessibilityWindows()
    {
        var service = typeof(GameAdvanceAccessibilityService).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as GameAdvanceAccessibilityService;
        if (service == null) return new { connected = false };
        var list = new List<object>();
        try
        {
            var windows = service.Windows;
            if (windows == null) return new { connected = true, windows = list };
            try
            {
                foreach (var window in windows)
                {
                    using var root = window.Root;
                    using var bounds = new Android.Graphics.Rect();
                    window.GetBoundsInScreen(bounds);
                    list.Add(new { id = window.Id, layer = window.Layer, type = window.Type.ToString(), focused = window.IsFocused,
                        active = window.IsActive, rootPackage = root?.PackageName, rootVisible = root?.VisibleToUser,
                        bounds = new { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom } });
                }
            }
            finally { foreach (var window in windows) window.Dispose(); }
            return new { connected = true, windows = list };
        }
        catch (Exception ex) { return new { connected = true, error = ex.ToString(), windows = list }; }
    }
    static object Window(string name)
    {
        var view = GetView(name);
        if (view == null) return new { name, attached = false };
        // .NET Android 未公开 View.getAccessibilityWindowId；其原生节点的 WindowId 为同一系统窗口身份。
        using var node = view.CreateAccessibilityNodeInfo();
        int[] position = new int[2]; view.GetLocationOnScreen(position);
        var lp = view.LayoutParameters as WindowManagerLayoutParams;
        var buttons = new List<object>();
        var texts = new List<object>();
        void Collect(View v)
        {
            if (v.Clickable) { int[] p = new int[2]; v.GetLocationOnScreen(p); buttons.Add(new { text = (v as TextView)?.Text, description = v.ContentDescription, x = p[0], y = p[1], width = v.Width, height = v.Height, enabled = v.Enabled }); }
            if (v is TextView text)
            {
                int[] p = new int[2]; v.GetLocationOnScreen(p);
                using var rect = new Android.Graphics.Rect(); bool visible = v.GetGlobalVisibleRect(rect);
                var layout = text.Layout;
                texts.Add(new { text = text.Text, x = p[0], y = p[1], width = v.Width, height = v.Height,
                    visible, visibility = v.Visibility.ToString(), visibleRect = new { left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom },
                    lineCount = text.LineCount, textSizePx = text.TextSize, ellipsis = layout == null ? 0 : Enumerable.Range(0, layout.LineCount).Sum(layout.GetEllipsisCount) });
            }
            if (v is ViewGroup group) for (int n = 0; n < group.ChildCount; n++) if (group.GetChildAt(n) is { } child) Collect(child);
        }
        Collect(view);
        return new { name, attached = view.IsAttachedToWindow, accessibilityWindowId = node?.WindowId, visibility = view.Visibility.ToString(), viewAlpha = view.Alpha,
            flags = lp?.Flags.ToString(), flagsNumber = (int?)lp?.Flags, type = lp?.Type.ToString(), alpha = lp?.Alpha,
            x = position[0], y = position[1], width = view.Width, height = view.Height, layoutX = lp?.X, layoutY = lp?.Y, buttons, texts };
    }
    static void Record(string phase, object? detail)
    {
        if (Looper.MyLooper() != Looper.MainLooper) { main.Post(() => Record(phase, detail)); return; }
        bool staticDiagnostics = !tapInProgress && phase is "state" or "state-settled" or "hold-settled";
        object? accessibilityWindows = staticDiagnostics ? AccessibilityWindows() : null;
        if (staticDiagnostics) lastAvailability = GameAdvanceAccessibilityService.GetAvailability(Sink);
        events.Add(new { phase, request, time = SystemClock.UptimeMillis(), detail, kind, paused, commands, choices, pauses, sinkUps,
            suspended = lease?.Suspended, windows = new[] { Window("promptBarView"), Window("branchEnteredView"), Window("root") },
            diagnosticSkippedDuringTap = tapInProgress || !staticDiagnostics, accessibilityWindows });
        if (events.Count > 80) events.RemoveRange(0, events.Count - 80);
        if (context == null) return;
        var metrics = context.Resources!.DisplayMetrics!;
        var data = new { variant = Variant, uid = Android.OS.Process.MyUid(), package = context.PackageName, session, kind, paused,
            runtime = new { api = (int)Build.VERSION.SdkInt, density = metrics.Density, densityDpi = (int)metrics.DensityDpi, fontScale = context.Resources.Configuration!.FontScale,
                screenWidthPixels = metrics.WidthPixels, screenHeightPixels = metrics.HeightPixels, orientation = context.Resources.Configuration.Orientation.ToString() },
            commands, choices, pauses, sinkUps, lastResult, lastReason, availability = lastAvailability, eventHistoryLimit = 80, events };
        string path = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, "prompt-probe.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(data));
        File.Move(path + ".tmp", path, overwrite: true);
        Android.Util.Log.Info("PromptWindowProbe", $"{phase}: {request} {lastResult} sinkUps={sinkUps}");
    }
}
