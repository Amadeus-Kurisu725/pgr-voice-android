using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.Listening;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace PromptWindowDevice.Probe;

// 仅测试构建：导入旧合成夹具，所有渲染、倒计时和导航都走真实 AppSession / MainActivity。
static class ListeningProbe
{
    static readonly Handler main = new(Looper.MainLooper!);
    static AppSession? session;
    static ActivityTracker? tracker;
    static WeakReference<MainActivity>? activity;
    static bool activityResumed;
    static Action<string, object?>? report;
    const string PackId = "synthetic-play-along", ChapterId = "pa";

    public static async void Setup(Context context, Intent intent, Action<string, object?> record, Action done)
    {
        report = record;
        try
        {
            if (tracker == null)
            {
                tracker = new ActivityTracker();
                ((Application)context.ApplicationContext!).RegisterActivityLifecycleCallbacks(tracker);
            }
            session = AppSession.Get(context);
            session.Onboarding.Exit(skip: true);
            session.Settings.OnboardingShown = true;
            session.Settings.BranchAutoSelect = true;
            session.Settings.DefaultBranchOption = 1;
            session.Settings.MainOrientation = "auto";
            session.SaveSettings();
            using var fixture = typeof(ListeningProbe).Assembly.GetManifestResourceStream("PromptWindowDevice.PlayAlong.pack.json")
                ?? throw new InvalidOperationException("未嵌入旧 PlayAlong.pack.json");
            using var zip = new MemoryStream();
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
            using (var entry = archive.CreateEntry("synthetic-play-along/pack.json").Open()) fixture.CopyTo(entry);
            zip.Position = 0;
            await session.Packages.ImportAsync(zip);
            main.Post(() =>
            {
                try
                {
                    var player = session.Listening;
                    player.Open(PackId, ChapterId);
                    player.SetPolicy(ListeningBranchPolicy.Manual);
                    string requested = intent.GetStringExtra("node") ?? "pa-sec-m1";
                    var choice = player.Items.FirstOrDefault(item => item.Kind == ListeningItemKind.Choice && item.Node?.Id == requested)
                        ?? throw new InvalidOperationException("旧夹具当前路线中没有要求的选择点：" + requested);
                    if (!player.SeekListeningItem(choice.Id)) throw new InvalidOperationException("真实 SeekListeningItem 未定位到选择点。");
                    player.Play(); // 真实 Play 在选择点调用 BeginStop，不播音频。
                    if (player.CountdownRemaining <= 0 || player.Choices.Count == 0) throw new InvalidOperationException("真实选择点没有启动倒计时。");
                    if (!player.CountdownPaused) player.ToggleCountdownPause();
                    OpenPage(context, "Listening");
                    Save(context, "listening-setup-complete");
                    main.PostDelayed(() => Save(context, "listening-layout-settled"), 800);
                }
                catch (Exception ex) { report?.Invoke("listening-setup-error", ex.ToString()); }
                finally { done(); }
            });
        }
        catch (Exception ex) { record("listening-setup-error", ex.ToString()); done(); }
    }

    public static void Command(Context context, Intent intent, Action<string, object?> record)
    {
        report = record;
        if (session == null) { record("listening-not-ready", "请先运行 listening-setup。"); return; }
        switch (intent.GetStringExtra("command"))
        {
            case "listening-service": StartMediaService(context, record); return;
            case "listening-reopen": session.Listening.Open(PackId, ChapterId); break;
            case "listening-page": OpenPage(context, intent.GetStringExtra("page") ?? "Listening"); break;
            case "listening-toggle": session.Listening.PlayButton(); break;
        }
        Save(context, "listening-state");
        main.PostDelayed(() => Save(context, "listening-state-settled"), 500);
    }

    static void StartMediaService(Context context, Action<string, object?> record)
    {
        MainActivity? current = null; activity?.TryGetTarget(out current);
        if (!activityResumed || current == null || current.IsDestroyed || current.IsFinishing)
        {
            record("listening-service-not-ready", "请等待真实 MainActivity 回到前台后再运行 listening-service。");
            return;
        }
        try
        {
            // 选择点夹具没有自然音频播放；明确启动真实服务，供后续系统媒体键测试使用。
            ListeningForegroundService.EnsureStarted(context, () =>
            {
                Save(context, "listening-service-ready");
                main.PostDelayed(() => Save(context, "listening-service-settled"), 500);
            });
        }
        catch (Exception ex) { record("listening-service-error", ex.ToString()); }
    }

    static void OpenPage(Context context, string page)
    {
        var open = new Intent(context, typeof(MainActivity));
        open.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        open.PutExtra("page", page);
        context.StartActivity(open);
    }

    static object ViewState(View v, string path)
    {
        int[] point = new int[2]; v.GetLocationOnScreen(point);
        using var bounds = new Android.Graphics.Rect(); bool visible = v.GetGlobalVisibleRect(bounds);
        var text = v as TextView;
        return new { path, type = v.GetType().Name, text = text?.Text, description = v.ContentDescription,
            x = point[0], y = point[1], width = v.Width, height = v.Height, visibility = v.Visibility.ToString(),
            visible, shown = v.IsShown, enabled = v.Enabled, clickable = v.Clickable,
            visibleRect = new { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom },
            scrollX = v.ScrollX, scrollY = v.ScrollY, canScrollUp = v.CanScrollVertically(-1), canScrollDown = v.CanScrollVertically(1),
            childHeight = v is ScrollView scroll ? scroll.GetChildAt(0)?.Height : null,
            lines = text?.LineCount, ellipsis = text?.Layout is { } layout ? Enumerable.Range(0, layout.LineCount).Sum(layout.GetEllipsisCount) : 0 };
    }

    static void Save(Context context, string phase)
    {
        if (Looper.MyLooper() != Looper.MainLooper) { main.Post(() => Save(context, phase)); return; }
        try
        {
            var views = new List<object>();
            var named = new List<object>();
            MainActivity? current = null; activity?.TryGetTarget(out current);
            if (current?.Window?.DecorView is { } decor)
            {
                void Walk(View v, string path)
                {
                    if (v is TextView or ScrollView || v.Clickable) views.Add(ViewState(v, path));
                    if (v is ViewGroup group) for (int n = 0; n < group.ChildCount; n++) if (group.GetChildAt(n) is { } child) Walk(child, path + "/" + n);
                }
                Walk(decor, "decor");
                foreach (string field in new[] { "pageScroll", "splitLeftScroll", "splitRightScroll", "listeningChoices", "listeningPlay", "listeningStatus", "content" })
                    if (typeof(MainActivity).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(current) is View v)
                        named.Add(ViewState(v, field));
            }
            var player = session?.Listening;
            var metrics = context.Resources!.DisplayMetrics!;
            var state = new { phase, time = SystemClock.UptimeMillis(), package = context.PackageName, uid = Android.OS.Process.MyUid(),
                source = "Tests/ListeningDefaultBranch/PlayAlong.pack.json; exact embedded old synthetic fixture; no audio generation",
                activityAlive = current != null && !current.IsDestroyed,
                activityResumed,
                page = current == null ? null : typeof(MainActivity).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(current),
                runtime = new { density = metrics.Density, fontScale = context.Resources.Configuration!.FontScale,
                    orientation = context.Resources.Configuration.Orientation.ToString(), widthPixels = metrics.WidthPixels, heightPixels = metrics.HeightPixels },
                listening = player == null ? null : new { pack = player.Pack?.Id, chapter = player.Chapter?.Id, currentItem = player.CurrentItem?.Id,
                    currentNode = player.Current?.Id, policy = player.Policy.ToString(), playing = player.IsPlaying, previewing = player.IsPreviewing,
                    countdown = player.CountdownRemaining, paused = player.CountdownPaused, playCaption = player.PlayCaption,
                    playEnabled = player.PlayEnabled, playTogglesCountdown = player.PlayTogglesCountdown, status = player.Status,
                    pendingAction = player.PendingStop?.Action.ToString(), choices = player.Choices }, namedViews = named, views };
            string path = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, "listening-probe.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state)); File.Move(path + ".tmp", path, overwrite: true);
            report?.Invoke(phase, new { stateFile = "listening-probe.json", currentItem = player?.CurrentItem?.Id, countdown = player?.CountdownRemaining,
                paused = player?.CountdownPaused, actualPage = current == null ? null : typeof(MainActivity).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(current) });
        }
        catch (Exception ex) { report?.Invoke("listening-state-error", ex.ToString()); }
    }

    sealed class ActivityTracker : Java.Lang.Object, Application.IActivityLifecycleCallbacks
    {
        public void OnActivityCreated(Activity a, Bundle? savedInstanceState) { if (a is MainActivity m) { activity = new(m); activityResumed = false; } }
        public void OnActivityResumed(Activity a) { if (a is MainActivity m) { activity = new(m); activityResumed = true; main.PostDelayed(() => Save(a.ApplicationContext!, "listening-activity-resumed"), 500); } }
        public void OnActivityDestroyed(Activity a) { if (activity?.TryGetTarget(out var current) == true && ReferenceEquals(current, a)) { activity = null; activityResumed = false; } }
        public void OnActivityStarted(Activity a) { }
        public void OnActivityPaused(Activity a) { if (activity?.TryGetTarget(out var current) == true && ReferenceEquals(current, a)) activityResumed = false; }
        public void OnActivityStopped(Activity a) { }
        public void OnActivitySaveInstanceState(Activity a, Bundle outState) { }
    }
}
