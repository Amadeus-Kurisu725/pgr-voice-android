using Android.Content;
using Android.OS;
using PgrVoice.AndroidApp.Contracts;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.Listening;
using CoreSession = PgrVoice.Listening.ListeningSession;
using Path = System.IO.Path;

namespace PgrVoice.AndroidApp;

/// <summary>0.4.8 设计稿 12：探索菜单一行。</summary>
public sealed record ListeningExploreRow(string Label, int Lines, bool Heard, bool Playing, bool Exit);
/// <summary>0.4.8 设计稿 12：探索菜单逐项读视图。HeardCount / LoopCount 为已听 / 应听的人物段数。</summary>
public sealed record ListeningExploreView(string MenuText, string Speaker, string Line, IReadOnlyList<ListeningExploreRow> Rows, int HeardCount, int LoopCount);
public sealed record ListeningBookmarkView(string Id, string Label, string PositionText);
public sealed record ListeningChoiceView(string Id, string Label);
/// <summary>06L-2 landscape branch card (display only): one option row.</summary>
/// <remarks>DurationMs = sum of the option's spoken-line audio durations; null while loading or when any spoken line has no readable audio.</remarks>
public sealed record ListeningChoiceOptionDetail(string Id, string Label, int Lines, string FirstSpeaker, string FirstLine, bool LastChosen, bool Heard, long? DurationMs = null);
/// <summary>06L-2: CHOICE n / m, 「小节 · 第 N 句之后」, remaining choice points, the line before the branch, options and a mini route (resolved flags per point).</summary>
public sealed record ListeningChoiceDetail(int Index, int Total, int Remaining, string SectionTitle, int AfterLine,
    string PreviousSpeaker, string PreviousLine, int PreviousNumber, int SectionLines,
    IReadOnlyList<ListeningChoiceOptionDetail> Options, IReadOnlyList<int> PointLines);
public sealed class ListeningPreferences
{
    public string? LastPackId { get; set; }
    public string? LastChapterId { get; set; }
    public float Speed { get; set; } = 1;
    public bool SmartResumeEnabled { get; set; } = true;
}

/// <summary>由主线程调用的独立听书会话。所有回调绑定代次，永不推进游戏存档。</summary>
public sealed class ListeningSession
{
    readonly Context context;
    readonly IChapterStorage packages;
    readonly Action beforePlay;
    readonly Func<Node?, float> volumeForNode;
    readonly Func<int> defaultBranchOption;
    readonly Func<bool> branchAutoSelect;
    // 0.4.7 停下处倒计时（G/I/L）：代次绑定当前项目；用户暂停、跳转、选择、关闭章节时作废。
    long countdownEpoch;
    ListeningItem? countdownItem;
    ListeningStop? pendingStop; ListeningItem? stopItem;
    /// <summary>当前停下处的处理；位置一旦变化（跳转、选择、重建）即失效。</summary>
    public ListeningStop? PendingStop
    {
        get => core != null && stopItem != null && ReferenceEquals(core.Current, stopItem) ? pendingStop : null;
        private set { pendingStop = value; stopItem = value == null ? null : core?.Current; }
    }
    public int CountdownRemaining { get; private set; }
    /// <summary>倒计时剩余比例（1 → 0），供细轨显示。</summary>
    public float CountdownFraction => CountdownRemaining <= 0 ? 0 : CountdownRemaining / (float)ListeningText.CountdownSeconds;
    /// <summary>倒计时到点将自动选择的选项（仅待选支线）。</summary>
    public string? CountdownOptionId => CountdownRemaining > 0 && PendingStop is { Action: ListeningStopAction.Choose } stop ? stop.OptionId : null;
    /// <summary>17-A 风格的短提示（自动处理后约 6 秒内显示）。</summary>
    public string? AutoNotice => autoNotice != null && SystemClock.ElapsedRealtime() - autoNoticeAt < 6000 ? autoNotice : null;
    public string AutoNoticeEnglish { get; private set; } = "AUTO · ROUTE";
    string? autoNotice; long autoNoticeAt;
    void ShowAutoNotice(string text, string english, string detail = "") { autoNotice = text; autoNoticeAt = SystemClock.ElapsedRealtime(); AutoNoticeEnglish = english; AutoNoticeDetail = detail; }
    /// <summary>0.4.8（设计稿 02）：提示条副行，如「倒计时结束 · 默认分支第一个选项」；没有时为空串。</summary>
    public string AutoNoticeDetail { get; private set; } = "";
    /// <summary>当前待选支线是结局分歧（选项含「开启结局」，设计稿 03）。只用于界面样式。</summary>
    public bool ChoiceIsEnding => Choices.Any(c => c.Label.Contains("开启结局"));
    /// <summary>当前句没有可用音频时的签：「待配音」/「无配音」；有音频、不需要停留或仅字幕时为 null。</summary>
    public string? VoiceTagFor(Node? node)
    {
        if (core == null || node == null || !PendingVoice.IsFlagged(node)) return null;
        string? file = core.Pack.ResolveAudio(node);
        if (file != null && File.Exists(file)) return null;
        return PendingVoice.VoiceTag(node);
    }
    /// <summary>
    /// 0.4.8 设计稿 12：探索菜单逐项读时的只读视图（行按菜单列出顺序；已听 / 正在收听 / 未听 / 出口最后读）。
    /// 由已排好的收听计划推出（最近一次「探索菜单逐项读」选择点 + 选项 LineIds / PathId），不改变计划；当前句不属于该菜单的回环时为 null。
    /// </summary>
    public ListeningExploreView? Explore
    {
        get
        {
            if (core == null || previewing || core.Current is not { Node: { } now }) return null;
            int pos = core.Position;
            var point = core.ChoicePoints.Where(p => p.AutoKind == PlaybackLog.Types.Exploration && p.Position <= pos).OrderBy(p => p.Order).LastOrDefault();
            if (point?.Item.Node is not { } menu || point.Item.Options is not { Count: > 1 } options) return null;
            var shape = MenuShape.Of(core.Pack, menu);
            if (!shape.Exploration) return null;
            bool Owns(ChoiceOption o, Node n) => o.LineIds.Contains(n.Id) || o.PathId.Length > 0 && n.PathId == o.PathId;
            var loops = options.Where(o => shape.Loops.Any(l => l.Id == o.Id)).ToList();
            var playing = loops.FirstOrDefault(o => Owns(o, now));
            if (playing == null) return null;
            var items = core.Items;
            var rows = new List<ListeningExploreRow>();
            foreach (var o in options)
            {
                bool exit = !loops.Contains(o);
                bool heard = !exit && o != playing && (shape.EmptyLoops.Any(e => e.Id == o.Id) || Enumerable.Range(point.Position, Math.Max(0, pos - point.Position)).Any(i => items[i].Node is { } n && Owns(o, n)));
                int lines = o.LineIds.Count(id => core.Pack.ById.TryGetValue(id, out var n) && n.Kind == "line" && !n.Archived);
                rows.Add(new(o.Label, lines, heard, o == playing, exit));
            }
            int done = rows.Count(r => r.Heard);
            return new(menu.Text, now.Speaker, now.Text, rows, done, loops.Count);
        }
    }
    Node? dwellNode; int dwellTotal;
    /// <summary>0.4.8 设计稿 05：没有可用配音的句子正在按字数停留时的已停留 / 总停留毫秒（界面显示「字幕停留 03.1 / 05.0 秒」）；不在停留时为 null。</summary>
    public (int Elapsed, int Total)? Dwell => running && ticket == 0 && dwellNode != null && core != null && ReferenceEquals(core.Current?.Node, dwellNode)
        ? ((int)Math.Clamp(SystemClock.ElapsedRealtime() - startedAt, 0, dwellTotal), dwellTotal) : null;
    readonly HashSet<string> loggedChoicePoints = new(StringComparer.Ordinal);
    readonly SessionDiagnostics diagnostics;
    readonly Handler main = new(Looper.MainLooper!);
    readonly ListeningProgressStore store;
    readonly string settingsPath;
    readonly ListeningPreferences preferences;
    readonly ListeningRemainingTimeCache remainingTime = new(ReadRemainingDuration);
    readonly Action timerTick;
    AndroidAudioPlayer? audio;
    AndroidAudioPlayer? previewAudio;
    Node? previewNode;
    long previewEpoch, previewTicket, previewStartedAt;
    bool previewing;
    CoreSession? core;
    ListeningProgressDocument? document;
    long epoch, ticket, offset, sleepDeadline, startedAt, lastSavedAt;
    long knownDuration;
    string? durationNodeId;
    bool running, timerQueued, resumeNeedsSelection;
    DateTimeOffset? pausedUtc;
    bool exactResumePosition;
    int skipped, notices, pendingVoiceItems;
    string? resumeNotice;
    string status = "选择大章节，开始离线听书。";
    string? saveWarning;
    string? preferencesSaveWarning;
    public event Action? Changed;
    public Pack? Pack => core?.Pack;
    public Chapter? Chapter => core?.Chapter;
    public Node? Current => core?.Current?.Node;
    public ListeningItem? CurrentItem => core?.Current;
    public IReadOnlyList<ListeningItem> Items => core?.Items ?? Array.Empty<ListeningItem>();
    public ListeningDirectory? ReadDirectory(string sectionId) => core == null ? null : ListeningDirectory.Create(core, sectionId);
    public bool IsPlaying => running || previewing;
    public bool IsPreviewing => previewing;
    public Node? PreviewNode => previewNode;
    public bool HasBlockingNotice => core?.HasBlockingNotice == true;
    string WaitingText => "此处等待确认续接，尚未听完本章。" + (core?.Current?.Notice ?? "") + " 可在下方完整台词中选择一句，只试听该句。";
    public string Status { get => status + (saveWarning == null ? "" : "；" + saveWarning) +
        (preferencesSaveWarning == null ? "" : "；" + preferencesSaveWarning); private set => status = value; }
    public ListeningBranchPolicy Policy => core?.Policy ?? DefaultPolicy;
    /// <summary>“默认分支”对应的收听策略，随分支页的默认分支设置（第一 / 第二个选项；只有一个选项时按第一个）。</summary>
    public ListeningBranchPolicy DefaultPolicy => defaultBranchOption() == 2 ? ListeningBranchPolicy.Second : ListeningBranchPolicy.First;
    public static bool IsDefaultPolicy(ListeningBranchPolicy policy) => policy is ListeningBranchPolicy.First or ListeningBranchPolicy.Second;
    public IReadOnlySet<string> AvailableNodeIds => core?.Items.Where(x => x.Kind == ListeningItemKind.Line)
        .Select(x => x.NodeId).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
    public float Speed => preferences.Speed;
    public string RemainingTimeText => core == null ? "" : remainingTime.Estimate(core.Pack,
        core.Items, core.Position, PositionMilliseconds, Speed).DisplayText;
    public void InvalidateRemainingTime() { remainingTime.Invalidate(); Changed?.Invoke(); }

    static long? ReadRemainingDuration(string path)
    {
#if ANDROID
        // Metadata retrieval does not create a MediaPlayer or request audio focus.
        using var reader = new global::Android.Media.MediaMetadataRetriever();
        reader.SetDataSource(path);
        return long.TryParse(reader.ExtractMetadata(global::Android.Media.MetadataKey.Duration), out long duration) && duration > 0
            ? duration : null;
#else
        return null;
#endif
    }
    public bool SmartResumeEnabled => preferences.SmartResumeEnabled;
    public long PositionMilliseconds => ticket != 0 && audio != null ? audio.PositionMilliseconds : offset;
    public long DurationMilliseconds
    {
        get
        {
            if (durationNodeId != Current?.Id) { durationNodeId = Current?.Id; knownDuration = 0; }
            if (ticket != 0 && audio?.DurationMilliseconds > 0) knownDuration = audio.DurationMilliseconds;
            return knownDuration;
        }
    }
    public string PositionText => previewing ? "单句试听 · 原收听位置已保留" : core?.Current is { } item
        ? $"{Chapter!.Title} · {DisplaySectionTitle(item.SectionId,item.SectionTitle)}" + (item.LineNumber > 0 ? $" · 第 {item.LineNumber} 句" : "") + (item.BranchLabel.Length > 0 ? $" · {item.BranchLabel}" : "")
        : core?.Completed == true ? $"{Chapter!.Title} · 已听完" : "尚未选择大章节";
    string DisplaySectionTitle(string id,string fallback) => Chapter?.Sections.Any(s=>s.Id==id)==true
        ? SectionDisplay.Title(Chapter.Sections,id) : fallback;
    public string ResumeText
    {
        get
        {
            if (resumeNeedsSelection) return "上回听书记录与新版章节不匹配，原记录已保留。请选小节，或点击播放从开头重听。";
            if (HasBlockingNotice) return "收听停在待确认续接处，原位置与选择已保留。";
            var resume = ChapterProgress?.Resume;
            return resume == null ? "还没有收听记录，从本章开头开始。" :
                resume.Completed ? "上回已听完本章，可从目录选择小节重听。" :
                $"上回听到：{DisplaySectionTitle(resume.SectionId,resume.SectionTitle)} · 第 {resume.LineNumber} 句" +
                (resume.PositionMs > 0 ? $" · {resume.PositionMs / 1000} 秒" : "") +
                (resume.Text.Length > 0 ? $"\n{AppSession.SpeakerLabel(resume.Speaker)}：{resume.Text}" : "");
        }
    }
    public string SleepText => sleepDeadline == 0 ? "定时关闭未开启" :
        $"约 {Math.Max(1, (sleepDeadline - SystemClock.ElapsedRealtime() + 59_999) / 60_000)} 分钟后暂停";
    ListeningChapterProgress? ChapterProgress => document != null && Chapter != null ? document.ForChapter(Chapter.Id) : null;
    public IReadOnlyList<ListeningBookmarkView> Bookmarks => ChapterProgress?.Bookmarks.Select(x =>
        new ListeningBookmarkView(x.Id, x.Label, $"{DisplaySectionTitle(x.Snapshot.SectionId,x.Snapshot.SectionTitle)} · 第 {x.Snapshot.LineNumber} 句")).ToArray() ?? Array.Empty<ListeningBookmarkView>();
    public IReadOnlyList<ListeningChoiceView> Choices => core?.Current?.Kind == ListeningItemKind.Choice
        ? core.Current.Options?.Select(x => new ListeningChoiceView(x.Id, x.Label)).ToArray() ?? Array.Empty<ListeningChoiceView>()
        : Array.Empty<ListeningChoiceView>();

    /// <summary>Read-only detail for the pending choice (06L-2); null when no choice is pending. Never changes playback.</summary>
    public ListeningChoiceDetail? ChoiceDetail
    {
        get
        {
            var c = core?.Current;
            if (core == null || c?.Kind != ListeningItemKind.Choice || c.Options == null) return null;
            var items = core.Items; int at = core.Position;
            int LineBefore(int position)
            { for (int i = Math.Min(position, items.Count) - 1; i >= 0; i--) if (items[i].Kind == ListeningItemKind.Line && items[i].SectionId == c.SectionId) return items[i].LineNumber; return 0; }
            // Choice points of this section in plan order (a returning menu counts once); the pending one is the current item.
            var points = core.ChoicePoints.Where(p => p.SectionId == c.SectionId).GroupBy(p => p.ChoiceKey).Select(g => g.OrderBy(p => p.Order).First()).OrderBy(p => p.Position).ToList();
            int index = points.FindIndex(p => p.ChoiceKey == c.ChoiceKey) + 1;
            if (index == 0) { points.Add(new ListeningChoicePoint(c, at, null, false, int.MaxValue)); points = points.OrderBy(p => p.Position).ToList(); index = points.FindIndex(p => p.ChoiceKey == c.ChoiceKey) + 1; }
            ListeningItem? previous = null;
            for (int i = at - 1; i >= 0; i--) if (items[i].Kind == ListeningItemKind.Line && items[i].SectionId == c.SectionId) { previous = items[i]; break; }
            string? chosen = null; ChapterProgress?.Resume?.Choices.TryGetValue(c.ChoiceKey ?? "", out chosen);
            var pack = core.Pack;
            var options = c.Options.Select(o =>
            {
                Node? first = null;
                foreach (var id in new[] { o.TargetId }.Concat(o.LineIds))
                    if (pack.ById.TryGetValue(id, out var n) && n.Kind == "line" && !n.Archived) { first = n; break; }
                bool heard = core.ChoicePoints.Any(p => p.ChoiceKey == c.ChoiceKey && p.IsResolved && p.SelectedOptionId == o.Id);
                return new ListeningChoiceOptionDetail(o.Id, o.Label, o.LineIds.Count, first?.Speaker ?? "", first?.Text ?? "", chosen == o.Id, heard,
                    OptionDuration(pack, c.ChoiceKey ?? "", o));
            }).ToList();
            int sectionLines = items.Where(i => i.Kind == ListeningItemKind.Line && i.SectionId == c.SectionId).Select(i => i.LineNumber).DefaultIfEmpty(0).Max();
            var pointLines = points.Select(p => LineBefore(p.Position)).ToList();
            return new ListeningChoiceDetail(index, points.Count, Math.Max(0, points.Count - index), c.SectionTitle,
                previous?.LineNumber ?? 0, previous?.Node?.Speaker ?? "", previous?.Node?.Text ?? "", previous?.LineNumber ?? 0, sectionLines, options, pointLines);
        }
    }

    // 06L-2 「N 句 · 约 m:ss」: per-option audio duration, read off the UI thread once per pack instance / choice / option.
    readonly Dictionary<string, long?> optionDurations = new(StringComparer.Ordinal);
    readonly HashSet<string> optionDurationsPending = new(StringComparer.Ordinal);
    /// <summary>Bumped (on the main thread) whenever an option duration finishes loading.</summary>
    public int OptionDurationVersion { get; private set; }

    long? OptionDuration(Pack pack, string choiceKey, ChoiceOption option)
    {
        string key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pack) + "|" + choiceKey + "|" + option.Id;
        if (optionDurations.TryGetValue(key, out var known)) return known;
        if (!optionDurationsPending.Add(key)) return null;
        var ids = option.LineIds.ToArray();
        Task.Run(() =>
        {
            long? total = null;
            try
            {
                long sum = 0; int spoken = 0; bool complete = true;
                foreach (var id in ids)
                {
                    if (!pack.ById.TryGetValue(id, out var node) || node.Kind != "line" || node.Archived || node.AudioStatus == "not-spoken") continue;
                    spoken++;
                    string? path = null; try { path = pack.ResolveAudio(node); } catch { }
                    long? ms = path != null && File.Exists(path) ? AudioDurationMetadata.ReadWaveMilliseconds(path) ?? ReadRemainingDuration(path) : null;
                    if (ms is not > 0) { complete = false; break; }
                    sum += ms.Value;
                }
                if (complete && spoken > 0) total = sum;
            }
            catch { total = null; }
            main.Post(() => { optionDurations[key] = total; optionDurationsPending.Remove(key); OptionDurationVersion++; Changed?.Invoke(); });
        });
        return null;
    }

    public ListeningSession(Context context, IChapterStorage packages, Action beforePlay, SessionDiagnostics diagnostics,
        Func<Node?, float>? volumeForNode = null, Func<int>? defaultBranchOption = null, Func<bool>? branchAutoSelect = null)
    {
        this.context = context; this.packages = packages; this.beforePlay = beforePlay; this.diagnostics = diagnostics;
        this.volumeForNode = volumeForNode ?? (_ => 1);
        this.defaultBranchOption = defaultBranchOption ?? (() => 1);
        this.branchAutoSelect = branchAutoSelect ?? (() => true);
        string root = Path.Combine(context.FilesDir!.AbsolutePath, "listening");
        store = new(root); settingsPath = Path.Combine(root, "preferences.json");
        try { preferences = Json.ReadWithBackup<ListeningPreferences>(settingsPath, out _); } catch { preferences = new(); }
        preferences.Speed = float.IsFinite(preferences.Speed) ? Math.Clamp(preferences.Speed, .75f, 2f) : 1;
        timerTick = Tick;
        remainingTime.Updated += () => main.Post(() => Changed?.Invoke());
    }
    public void OpenLast()
    {
        if (core != null || preferences.LastPackId == null || preferences.LastChapterId == null) return;
        try { Open(preferences.LastPackId, preferences.LastChapterId); }
        catch (Exception ex) { Fail("上次章节暂不可用，请重新选择大章。" + ex.Message); }
    }
    public void Open(string packId, string chapterId)
    {
        Pause();
        // 先完整加载，失败时仍保留此前可用章节。
        var pack = packages.Load(packId);
        var next = new CoreSession(pack, chapterId);
        var nextDocument = store.Load(packId, out var readNotice);
        var saved = nextDocument.ForChapter(chapterId).Resume;
        long nextOffset = 0;
        resumeNotice = null; resumeNeedsSelection = false;
        if (saved != null)
        {
            if (next.TryRestore(saved, out var reason)) nextOffset = Math.Max(0, next.ResumePositionMs);
            else { resumeNeedsSelection = true; resumeNotice = "剧情已更新，上次位置无法直接恢复，请从目录重新选择。"; }
        }
        // 存档里的“默认分支”按当前设置的第一 / 第二个选项重建；保留当前句时仍保留句内位置。
        if (IsDefaultPolicy(next.Policy) && next.Policy != DefaultPolicy)
        {
            string? before = next.Current?.Id;
            next.SetPolicy(DefaultPolicy);
            if (next.Current?.Id != before) nextOffset = 0;
        }
        // 换章 / 重开 / 配音包更新：Pause() 只冻结倒计时，这里连同暂停状态与停下处一起清掉，避免残留「已暂停」卡片。
        ReleaseCountdown(); PendingStop = null;
        core = next; document = nextDocument; offset = nextOffset; skipped = notices = pendingVoiceItems = 0; loggedChoicePoints.Clear();
        exactResumePosition = !resumeNeedsSelection && saved?.ExactResumePosition == true;
        pausedUtc = !resumeNeedsSelection && saved != null && !exactResumePosition
            ? saved.PausedUtc ?? saved.UpdatedUtc : null;
        preferences.LastPackId = packId; preferences.LastChapterId = chapterId; SavePreferences();
        Status = resumeNotice ?? (HasBlockingNotice ? WaitingText : string.IsNullOrEmpty(readNotice) ? "已载入听书位置，点击继续收听后才发声。" : readNotice);
        Notify();
        // 0.4.7（I）：恢复到未核出口时立刻进入停下处理（倒计时 / 下一句可点），不必先点播放。
        if (HasBlockingNotice) BeginStop();
    }
    public void PackageUpdated(string packId)
    {
        if (Pack?.Id != packId || Chapter == null) return;
        string chapterId = Chapter.Id;
        try { Open(packId, chapterId); }
        catch (Exception ex)
        {
            ReleaseCountdown(); PendingStop = null; core = null; document = null; offset = 0;
            Status = "章节内容已更新，请重新选择听书大章。"; diagnostics.Log("听书章节更新", ex.Message); Notify();
        }
    }
    public void UnloadPackage(string packId)
    {
        if (Pack?.Id == packId)
        {
            Stop(); core = null; document = null; offset = 0; knownDuration = 0; durationNodeId = null;
            resumeNeedsSelection = false; resumeNotice = null;
            Status = "章节已卸载，听书进度和书签已保留。";
        }
        if (preferences.LastPackId == packId)
        { preferences.LastPackId = null; preferences.LastChapterId = null; SavePreferences(); }
        Notify();
    }
    AndroidAudioPlayer Audio
    {
        get
        {
            if (audio != null) return audio;
            audio = new(context) { Strategy = AudioStrategy.Listening };
            audio.SetVolume(volumeForNode(Current)); audio.SetSpeed(Speed);
            audio.PlaybackCompleted += OnCompleted;
            audio.Error += Fail;
            audio.Interrupted += Fail;
            return audio;
        }
    }
    public void Play()
    {
        if (core == null) { Status = "请先选择大章节。"; Notify(); return; }
        StopPreview();
        if (running) return;
        if (HasBlockingNotice) { BeginStop(); return; }
        if (core.Completed) { Status = "本章已听完，请从目录选择小节重听。"; Notify(); return; }
        if (Choices.Count > 0) { BeginStop(); return; }
        beforePlay();
        resumeNeedsSelection = false;
        if (!exactResumePosition)
            offset = ListeningResumePolicy.ResolvePosition(offset, pausedUtc, DateTimeOffset.UtcNow, SmartResumeEnabled);
        // 本次恢复已经消费；重绘、重复暂停不能把同一次离开再应用一次。
        pausedUtc = null; exactResumePosition = false;
        running = true; long request = ++epoch; startedAt = SystemClock.ElapsedRealtime();
        SaveResume();
        Status = "正在准备听书…"; Notify(); QueueTick();
        try { ListeningForegroundService.EnsureStarted(context, () => { if (running && epoch == request) PlayCurrent(request); }); }
        catch (Exception ex) { Fail("无法开始后台播放：" + ex.Message); }
    }
    void PlayCurrent(long request)
    {
        if (!running || core == null || request != epoch) return;
        if (sleepDeadline > 0 && SystemClock.ElapsedRealtime() >= sleepDeadline) { SleepExpired(); return; }
        // 分批跨过无音频节点，不递归、不堵塞主线程；每批检查取消代次。
        for (int i = 0; i < 32; i++)
        {
            if (core.Completed || core.Current == null)
            { PauseInternal(false); offset = 0; SaveResume(); Status = "本章已听完。" + SkippedText; Notify(); return; }
            var item = core.Current;
            LogPassedChoicePoints();
            if (item.IsBlocking || item.Kind == ListeningItemKind.Choice)
            { PauseInternal(false); offset = 0; SaveResume(); BeginStop(); return; }
            if (item.Kind == ListeningItemKind.Notice)
            { notices++; diagnostics.Log("听书提示", item.Notice); core.MoveNext(); offset = 0; continue; }
            string? file = item.Node == null ? null : core.Pack.ResolveAudio(item.Node);
            if ((file == null || !File.Exists(file)) && PendingVoice.IsFlagged(item.Node))
            {
                // 0.4.7（J）没有可用音频的台词：不跳过、不暂停——显示字幕并按字数停留后进入下一句（倍速同样缩短停留）。
                // draft-missing 标「待配音 · 」，仅字幕（subtitle-only）不加签、记「仅字幕越过」，其余（not-found、未设置等）标「无配音 · 」。
                bool subtitleOnly = PendingVoice.IsSubtitleOnly(item.Node);
                Log(PendingVoice.PassType(item.Node), item, item.Node!.AudioStatus ?? "无音频");
                Status = PendingVoice.Tag(item.Node) + "正在收听" + (item.DraftContinuation ? ListeningText.DraftStatusPart : "") + (item.BranchLabel.Length > 0 ? " · " + item.BranchLabel : "") + SkippedText;
                SaveResume(); startedAt = SystemClock.ElapsedRealtime(); ticket = 0; offset = 0;
                if (subtitleOnly) diagnostics.Log("听书仅字幕", item.Node!.Id); else { pendingVoiceItems++; diagnostics.Log("听书待配音", item.Node!.Id); }
                int dwell = (int)(PendingVoice.DwellMilliseconds(item.Node!.Text) / Math.Max(.5f, Speed));
                var node = item.Node;
                dwellNode = node; dwellTotal = Math.Max(1, dwell);
                main.PostDelayed(() =>
                {
                    if (!running || request != epoch || core == null || !ReferenceEquals(core.Current?.Node, node)) return;
                    core.MoveNext(); SaveResume(); PlayCurrent(request);
                }, dwell);
                Notify(); QueueTick(); return;
            }
            // not-spoken（动作 / 战斗提示）与纯标点句：本来不朗读，静默越过（不计入“未收录配音”）。
            if (file == null || !File.Exists(file))
            { if (item.Node?.AudioStatus != "not-spoken") skipped++; core.MoveNext(); offset = 0; continue; }
            // 现稿续接：本句经未逐句核实的出口 / 正文按配音包现有连接到达，状态行加短标签。
            Status = "正在收听" + (item.DraftContinuation ? ListeningText.DraftStatusPart : "") + (item.BranchLabel.Length > 0 ? " · " + item.BranchLabel : "") + SkippedText;
            SaveResume(); startedAt = SystemClock.ElapsedRealtime();
            Audio.SetVolume(volumeForNode(item.Node));
            long audioTicket = Audio.PlayTagged(file, offset);
            // PlayTagged 可同步报告音频焦点拒绝/文件错误；失败后不能重新绑定旧回调。
            if (request != epoch || !running) return;
            ticket = audioTicket;
            Notify(); QueueTick(); return;
        }
        main.Post(() => PlayCurrent(request));
    }
    // ---- 0.4.7 停下处（G/I/L）与播放记录（K） ----
    const string Mode = "听书";
    void Log(string type, ListeningItem? item, string reason, string option = "")
    {
        if (core == null) return;
        PlaybackLog.Write(type, Mode, core.Pack, core.Chapter.Title, item?.SectionTitle ?? "", item?.NodeId ?? "", reason, option);
    }
    /// <summary>计划中已经越过的自动选择点（单选项 / 探索菜单 / 默认分支）各记一条，选择点按键 + 所选项去重。</summary>
    void LogPassedChoicePoints()
    {
        if (core == null) return;
        int position = core.Position;
        foreach (var point in core.ChoicePoints)
        {
            if (point.AutoKind == null || point.Position > position || point.SelectedOptionId == null) continue;
            if (!loggedChoicePoints.Add(point.ChoiceKey + "\u001f" + point.SelectedOptionId)) continue;
            // AutoKind 即播放记录类型（单选项自动继续 / 探索菜单自动读 / 自动选择）。
            string type = point.AutoKind;
            Log(type, point.Item, type == PlaybackLog.Types.AutoSelect ? "默认分支" + ListeningText.OptionOrdinal(defaultBranchOption()) : type == PlaybackLog.Types.SingleOption ? "仅一个选项" : "探索菜单逐项读完后离开", point.AutoLabel ?? "");
            // 0.4.7 空出口：所取出口没有台词，记为已读，从汇合后第一句继续（单独一条，便于核对）。
            if (point.EmptyExitLabel != null) Log(PlaybackLog.Types.Exploration, point.Item, ListeningText.EmptyExitContinue, point.EmptyExitLabel);
        }
    }
    /// <summary>开关改变：停在停下处时立即按新设置重新提示（关闭则取消倒计时并等待）。</summary>
    public void BranchAutoSelectChanged()
    {
        if (core == null || running || PendingStop == null) return;
        // 0.4.8（审稿必须改 2）：已暂停的倒计时不因开关而变成不暂停——关闭时结束这次倒计时（停下等待）并记住停住的秒数；
        // 在同一停下处重新开启时恢复「已暂停」与剩余秒数，只有「继续」/ 选选项 / 上一句 / 跳小节才解除暂停。
        var item = core.Current;
        if (CountdownPaused && CountdownRemaining > 0 && ReferenceEquals(countdownItem, item)) { frozenItem = item; frozenRemaining = CountdownRemaining; }
        CancelCountdown(); BeginStop();
        if (frozenItem != null && ReferenceEquals(frozenItem, item) && CountdownRemaining > 0 && ReferenceEquals(countdownItem, item) && PendingStop is { } stop)
        {
            CountdownRemaining = Math.Min(CountdownRemaining, frozenRemaining); CountdownPaused = true; countdownEpoch++;
            frozenItem = null; Status = StopText(stop, CountdownRemaining); Notify();
        }
    }
    ListeningItem? frozenItem; int frozenRemaining;
    void CancelCountdown() { countdownEpoch++; countdownItem = null; CountdownRemaining = 0; CountdownPaused = false; }
    /// <summary>解除暂停的入口（「继续」以外：选选项、上一句、跳小节 / 定位、换章）也清掉开关关闭时记住的秒数。</summary>
    void ReleaseCountdown() { frozenItem = null; CancelCountdown(); }
    /// <summary>停在待选支线 / 未核续接 / 无文本死角：记录停下；「分支自动选择」开启时 15 秒倒计时后自动处理，关闭则等待。</summary>
    void BeginStop()
    {
        if (core?.Current is not { } item) return;
        var stop = core.ResolveStop(defaultBranchOption());
        bool sameStop = ReferenceEquals(countdownItem, item) && CountdownRemaining > 0;
        PendingStop = stop;
        if (stop == null) { Status = WaitingText; Notify(); return; }
        // 最后一个小节已读完（出口未核实）：正常结束本章，不倒计时、不提示反馈。
        if (stop is { Action: ListeningStopAction.ChapterEnd, SectionComplete: true }) { ApplyPendingStop(true); return; }
        if (!sameStop) Log(PlaybackLog.Types.Stop, item, StopReason(stop), stop.OptionLabel ?? "");
        if (!branchAutoSelect())
        {
            CancelCountdown(); Status = StopText(stop, 0); Notify(); return;
        }
        if (sameStop) { Status = StopText(stop, CountdownRemaining); Notify(); return; }
        countdownItem = item; CountdownRemaining = ListeningText.CountdownSeconds; CountdownPaused = false;
        Status = StopText(stop, CountdownRemaining); Notify();
        StartTicking(item);
    }
    /// <summary>
    /// 每次开始 / 继续都换新 token（countdownEpoch）：暂停、取消、换章、跳转都会让旧回调失效；
    /// 回调还核对同一 core、同一停下项、停下动作仍在，避免对已经换掉的会话动作。
    /// </summary>
    void StartTicking(ListeningItem item)
    {
        long token = ++countdownEpoch; var owner = core;
        void TickDown()
        {
            if (token != countdownEpoch || CountdownPaused || core == null || !ReferenceEquals(core, owner) || !ReferenceEquals(core.Current, item) || !ReferenceEquals(countdownItem, item) || PendingStop is not { } stop) return;
            CountdownRemaining--;
            if (CountdownRemaining <= 0) { ApplyPendingStop(true); return; }
            Status = StopText(stop, CountdownRemaining); Notify();
            main.PostDelayed(TickDown, 1000);
        }
        main.PostDelayed(TickDown, 1000);
    }
    /// <summary>0.4.8（设计稿 16）：倒计时「暂停」后停住剩余秒数，不自动处理；直到「继续」或用户直接选择。</summary>
    public bool CountdownPaused { get; private set; }
    /// <summary>「暂停 / 继续」：冻结或从停住的秒数恢复倒计时。只作用于当前停下处的倒计时，不影响音频播放状态。</summary>
    public void ToggleCountdownPause()
    {
        if (core?.Current is not { } item || PendingStop is not { } stop || CountdownRemaining <= 0 || !ReferenceEquals(countdownItem, item)) return;
        if (!CountdownPaused)
        {
            CountdownPaused = true; countdownEpoch++;
            Status = StopText(stop, CountdownRemaining); Notify(); return;
        }
        CountdownPaused = false;
        Status = StopText(stop, CountdownRemaining); Notify();
        StartTicking(item);
    }
    /// <summary>
    /// 0.4.8（设计稿 16，与电脑版 Listening047 一致）：停下处倒计时进行中或已暂停时，主播放按钮与卡片「暂停 / 继续」是同一状态、同一操作——
    /// 进行中显示「暂停」并冻结秒数，已暂停显示「继续」并从停住的秒数接着倒计时；不取消倒计时、不跳过。其他情况保持原行为。
    /// </summary>
    public bool PlayTogglesCountdown => !previewing && !running && core != null && PendingStop != null && CountdownRemaining > 0 && ReferenceEquals(countdownItem, core.Current);
    /// <summary>主播放按钮文字：倒计时中「暂停」/ 已暂停「继续」；收听或试听中「暂停」；否则「播放」。</summary>
    public string PlayCaption => PlayTogglesCountdown ? (CountdownPaused ? "继续" : "暂停") : IsPlaying ? "暂停" : "播放";
    /// <summary>主播放按钮图标是否为「暂停」形（否则为播放三角）。</summary>
    public bool PlayShowsPauseIcon => PlayTogglesCountdown ? !CountdownPaused : IsPlaying;
    /// <summary>主播放按钮可用：试听中、没有待选支线，或停下处倒计时可暂停 / 继续。</summary>
    public bool PlayEnabled => IsPreviewing || Choices.Count == 0 || PlayTogglesCountdown;
    /// <summary>主播放按钮点击。倒计时中走 <see cref="ToggleCountdownPause"/>（与卡片按钮完全相同）；否则收听中暂停、不在收听则播放。返回是否开始了播放。</summary>
    public bool PlayButton()
    {
        if (PlayTogglesCountdown) { ToggleCountdownPause(); return false; }
        if (IsPlaying) { Pause(); return false; }
        Play(); return true;
    }
    static string StopReason(ListeningStop stop) => stop.Action switch
    {
        ListeningStopAction.Choose => "支线待选",
        ListeningStopAction.OrderContinue => "分支续接未核实：" + stop.Reason,
        _ when stop.SectionComplete => "小节已读完，出口未核实：" + stop.Reason,
        ListeningStopAction.NextSection => "后续没有配音文本：" + stop.Reason,
        _ => "后续没有配音文本（最后一个小节）：" + stop.Reason,
    };
    string StopText(ListeningStop stop, int seconds)
    {
        string wait = seconds > 0 ? $"，{seconds} 秒后" : "";
        if (seconds > 0 && CountdownPaused)
            return stop.Action == ListeningStopAction.OrderContinue
                ? "已暂停：倒计时停住，不自动续接；点「继续」恢复。也可直接点「按顺序继续」或「下一句」。"
                : stop.Action == ListeningStopAction.Choose
                    ? "已暂停：倒计时停住，不自动续接；点「继续」恢复，或直接点选项。"
                    : "已暂停：倒计时停住，不自动续接；点「继续」恢复。";
        return stop.Action switch
        {
            ListeningStopAction.Choose => seconds > 0
                ? $"遇到支线，{seconds} 秒后自动选择：{stop.OptionLabel}（分支自动选择；可直接点选项）"
                : "遇到支线，请手动选择后继续收听（分支自动选择已关闭）。",
            ListeningStopAction.OrderContinue => seconds > 0
                ? $"此处分支续接未核实{wait}按顺序继续（可点「按顺序继续」或暂停）。"
                : "此处分支续接未核实。可点「按顺序继续」从本节下一句接着听（分支自动选择已关闭）。",
            ListeningStopAction.NextSection when stop.SectionComplete => seconds > 0
                ? $"本小节已读完（出口未核实）{wait}进入下一小节（可点「进入下一小节」或暂停）。"
                : "本小节已读完（出口未核实）。可点「进入下一小节」继续（分支自动选择已关闭）。",
            ListeningStopAction.ChapterEnd when stop.SectionComplete => "本章已听完。",
            ListeningStopAction.NextSection => ListeningText.DeadEndHint + (seconds > 0 ? wait + "跳到下一小节。" : "。可点「跳到下一小节」继续（分支自动选择已关闭）。"),
            _ => ListeningText.DeadEndChapterEnd + (seconds > 0 ? wait + "结束本章。" : "。可点「结束本章」（分支自动选择已关闭）。"),
        };
    }
    /// <summary>执行停下处的自动处理（倒计时到点，或用户点「按顺序继续」/「跳到下一小节」/「结束本章」）。</summary>
    public void ApplyPendingStop(bool automatic = false)
    {
        if (core == null || PendingStop is not { } stop) return;
        var item = core.Current;
        ReleaseCountdown(); PendingStop = null;
        PauseInternal(true);
        if (!core.ApplyStop(stop)) { Status = "当前位置已变化，请重新核对。"; Notify(); return; }
        offset = 0; SetExactPosition(); SaveResume();
        string how = automatic ? "倒计时结束" : "用户确认";
        switch (stop.Action)
        {
            case ListeningStopAction.Choose:
                Log(PlaybackLog.Types.AutoSelect, item, how + "·默认分支" + ListeningText.OptionOrdinal(defaultBranchOption()), stop.OptionLabel ?? "");
                Status = ListeningText.AutoSelected + stop.OptionLabel; ShowAutoNotice(Status, automatic ? "AUTO · SELECTED" : "ROUTE · SELECTED", automatic ? "倒计时结束 · 默认分支" + ListeningText.OptionOrdinal(defaultBranchOption()) : ""); break;
            case ListeningStopAction.OrderContinue:
                Log(PlaybackLog.Types.OrderContinue, item, how + "·" + stop.Reason, stop.LineId ?? "");
                Status = ListeningText.OrderContinued; ShowAutoNotice(Status, "ROUTE · IN ORDER", automatic ? "倒计时结束 · 从本节下一句接着听" : "从本节下一句接着听"); break;
            case ListeningStopAction.NextSection when stop.SectionComplete:
                Log(PlaybackLog.Types.OrderContinue, item, how + "·小节已读完·" + stop.Reason, core.Current?.SectionTitle ?? "");
                Status = "已进入下一小节：" + (core.Current?.SectionTitle ?? ""); ShowAutoNotice(Status, "SECTION · NEXT"); break;
            case ListeningStopAction.ChapterEnd when stop.SectionComplete:
                Status = "本章已听完。" + SkippedText; Notify(); return;
            case ListeningStopAction.NextSection:
                Log(PlaybackLog.Types.DeadEndJump, item, how + "·" + stop.Reason, core.Current?.SectionTitle ?? "");
                Status = "已跳到下一小节：" + (core.Current?.SectionTitle ?? ""); ShowAutoNotice(Status, "SECTION · JUMP"); break;
            default:
                Log(PlaybackLog.Types.DeadEndChapterEnd, item, how + "·" + stop.Reason);
                Status = ListeningText.DeadEndChapterEnd + "。本章已听完。"; Notify(); return;
        }
        string shown = Status; Notify(); Play();
        if (!running) return;
        Status = shown; Notify();
    }
    /// <summary>「反馈」：写一条玩家反馈记录（配音包、章节 + 小节、节点、时间）并提示；不取消倒计时，除非用户暂停。</summary>
    public void Feedback()
    {
        if (core?.Current is not { } item) return;
        Log(PlaybackLog.Types.PlayerFeedback, item, "后续没有配音文本，游戏里可能还有内容：" + item.Notice);
        string note = "已记录反馈（" + item.SectionTitle + "）。可在设置 →「存档 · 诊断」→「导出播放记录」导出后发给作者。"; // 与 UiText.ExportPlaybackLogHint 一致（测试工程不含 UiText）
        ShowAutoNotice("已记录反馈 · " + item.SectionTitle, "FEEDBACK · LOGGED");
        Status = note + (CountdownRemaining > 0 && PendingStop != null ? " " + StopText(PendingStop, CountdownRemaining) : "");
        Notify();
    }
    public bool CanFeedback => PendingStop is { } stop && (stop.DeadEnd || stop.Action == ListeningStopAction.OrderContinue);

    string SkippedText => (skipped > 0 ? $"；本次已跳过 {skipped} 句未收录配音" : "") +
        (notices > 0 ? $"；本章有 {notices} 处路线资料提示，补充片段单独标注" : "");
    void OnCompleted(long completedTicket)
    {
        if (!running || completedTicket != ticket || core == null) return;
        ticket = 0; offset = 0;
        core.MoveNext(); SaveResume();
        long request = epoch;
        main.Post(() => PlayCurrent(request));
    }
    /// <summary>
    /// 暂停。0.4.8（审稿必须改 1 / 2）：停下处倒计时进行中时等同「暂停」倒计时（冻结秒数），已暂停时保持暂停——
    /// 通知栏、耳机键、目录试听前的暂停都走这里，不再取消倒计时（否则下次播放会重开一个不暂停的 15 秒）。
    /// </summary>
    /// <summary>开始游戏配音前，也冻结尚未发声的听书倒计时；已暂停的秒数和位置保持。</summary>
    public void PauseForGame()
    {
        if (IsPlaying || PlayTogglesCountdown) Pause();
    }
    public void Pause()
    {
        if (PlayTogglesCountdown) { if (!CountdownPaused) ToggleCountdownPause(); return; }
        PauseInternal(true); Status = HasBlockingNotice ? WaitingText : "听书已暂停，位置已保存。" + SkippedText; Notify();
    }
    /// <summary>通知栏 / 锁屏 / 耳机「播放」：倒计时已暂停时 =「继续」，倒计时进行中不动；否则开始收听。</summary>
    public void MediaPlay()
    {
        if (PlayTogglesCountdown) { if (CountdownPaused) ToggleCountdownPause(); return; }
        Play();
    }
    /// <summary>通知栏 / 锁屏 / 耳机「暂停」：倒计时进行中 =「暂停」，已暂停保持；否则暂停收听。</summary>
    public void MediaPause() => Pause();
    /// <summary>通知栏中间键：与主播放按钮完全相同。</summary>
    public void MediaToggle() => PlayButton();
    void PauseInternal(bool captureOffset)
    {
        StopPreview();
        _ = DurationMilliseconds;
        if (captureOffset && ticket != 0 && audio != null) offset = audio.PositionMilliseconds;
        if (running) pausedUtc = DateTimeOffset.UtcNow;
        epoch++; running = false; ticket = 0; audio?.Stop(); SaveResume();
        main.RemoveCallbacks(timerTick); timerQueued = false;
        if (sleepDeadline != 0) QueueTick();
    }
    public void Stop()
    {
        ReleaseCountdown(); sleepDeadline = 0; PauseInternal(true);
        audio?.Dispose(); audio = null;
        Status = "听书已停止，续听位置和书签已保存。"; Notify(); ListeningForegroundService.StopService();
    }
    public void ServiceStopped()
    {
        ReleaseCountdown(); sleepDeadline = 0; PauseInternal(true); audio?.Dispose(); audio = null;
        Status = "后台听书已停止，可点击继续收听。"; Notify();
    }
    public void Fail(string message)
    { PauseInternal(true); Status = message + "；已保留听书位置。"; diagnostics.Log("听书暂停", message); Notify(); }
    /// <summary>0.4.7：下一句在「等待确认续接」处保持可点。未核出口 → 立刻按顺序续接（与倒计时 /「按顺序继续」相同）；小节已读完 / 无文本死角 → 立刻跳下一小节或结束本章；普通句仍 MoveNext。支线选项处不经由下一句自动选。</summary>
    public void Next()
    {
        if (core == null) return;
        if (previewing) { Move(() => false); return; }
        if (HasBlockingNotice)
        {
            EnsurePendingStop();
            if (PendingStop is { Action: ListeningStopAction.OrderContinue or ListeningStopAction.NextSection or ListeningStopAction.ChapterEnd })
            { ApplyPendingStop(false); return; }
            Status = WaitingText; Notify(); return;
        }
        Move(() => core.MoveNext());
    }
    public void Previous() => Move(() => core!.Previous());
    void Move(Func<bool> move)
    {
        if (core == null) return;
        // 0.4.8（审稿必须改 2）：试听中上一句 / 下一句只停试听；移动失败（如支线处「下一句」）不动倒计时；真正移动才结束这次倒计时。
        if (previewing)
        { PauseInternal(true); Status = "单句试听已停止，原收听位置与选择保持。"; Notify(); return; }
        bool wasPlaying = running; PauseInternal(true);
        var previous = core.Current;
        bool moved = move() || !ReferenceEquals(previous, core.Current);
        if (moved) ReleaseCountdown();
        if (moved) { offset = 0; resumeNeedsSelection = false; SetExactPosition(); SaveResume(); Status = "已定位，点击继续收听。"; }
        else Status = HasBlockingNotice ? WaitingText : Choices.Count > 0 ? "请先选择支线，或从目录跳到其他小节。" : "当前位置无法按此方式跳转，请选择目录中的小节。";
        Notify();
        if (HasBlockingNotice) BeginStop();
        else if (wasPlaying && moved) Play();
    }
    /// <summary>打开章节或已停在未核出口时准备停下动作，使倒计时与「下一句」不依赖先点播放。</summary>
    void EnsurePendingStop()
    {
        if (core?.Current is not { } item) return;
        if (PendingStop != null && ReferenceEquals(countdownItem, item)) return;
        PendingStop = core.ResolveStop(defaultBranchOption());
        countdownItem = item;
    }
    public void JumpToSection(string id)
    { if (core != null) { Pause(); ReleaseCountdown(); Move(() => core.SeekSection(id)); } }
    public void JumpToNode(string id)
    { if (core != null) { Pause(); Move(() => core.SeekNode(id)); } }
    /// <summary>测试与目录定位：跳到计划中的某一项（含未核出口 Notice）。</summary>
    public bool SeekListeningItem(string itemId)
    {
        if (core == null) return false;
        Pause();
        bool ok = false;
        Move(() => ok = core.SeekItem(itemId));
        return ok && core.Current?.Id == itemId;
    }
    public bool LocateDirectoryEntry(string sectionId, string key)
    {
        if (core == null) return false;
        // 每次点击按最新计划核对；预览只能打开本节待选点，不能直接跳入未选路线。
        string? target = ListeningDirectory.Create(core, sectionId).TargetFor(key);
        if (target == null)
        {
            Status = "这句尚未接入当前收听路线。可选中后点“只试听选中台词”，试听结束后停住，原位置与选择保持。";
            Notify(); return false;
        }
        Pause(); Move(() => core.SeekItem(target));
        return core.Current?.Id == target;
    }
    public bool PreviewDirectoryEntry(string sectionId, string key)
    {
        if (core == null) return false;
        var entry = ListeningDirectory.Create(core, sectionId).Entries.FirstOrDefault(x => x.Key == key);
        if (entry?.IsPreview != true || entry.NodeId == null || !core.Pack.ById.TryGetValue(entry.NodeId, out var node)
            || node.Archived || node.Kind != "line" || node.SectionId != sectionId) return false;
        PauseInternal(true);
        string? file = core.Pack.ResolveAudio(node);
        if (file == null || !File.Exists(file))
        { Status = (PendingVoice.IsSubtitleOnly(node) ? "这句仅字幕，无需配音。" : "这句暂时没有可用配音。" + core.Pack.AudioNotice(node)) + " 原收听位置已保留。"; Notify(); return false; }
        beforePlay();
        long request = ++previewEpoch;
        var output = new AndroidAudioPlayer(context) { Strategy = AudioStrategy.Listening };
        previewAudio = output; previewNode = node; previewing = true; previewTicket = 0;
        previewStartedAt = SystemClock.ElapsedRealtime();
        bool Active() => previewing && request == previewEpoch && ReferenceEquals(previewAudio, output);
        void End(string message)
        {
            if (!Active()) return;
            StopPreview(); Status = message + " 原收听位置与选择保持，未自动续播。"; Notify();
        }
        output.PlaybackCompleted += completed => { if (Active() && completed == previewTicket) End("单句试听已结束。"); };
        output.Error += message => End("单句试听未完成：" + message);
        output.Interrupted += message => End("单句试听已暂停：" + message);
        output.SetSpeed(Speed); output.SetVolume(volumeForNode(node));
        Status = "只试听：" + AppSession.SpeakerLabel(node.Speaker) + "：" + node.Text + "；结束后停住。"; Notify(); QueueTick();
        try
        {
            ListeningForegroundService.EnsureStarted(context, () =>
            {
                if (!Active()) return;
                long started = output.PlayTagged(file, 0);
                // 焦点/文件失败可能同步回调；不能复活已经取消的试听。
                if (Active()) previewTicket = started;
            });
        }
        catch (Exception ex) { End("无法开始单句试听：" + ex.Message); }
        return Active();
    }
    void StopPreview()
    {
        previewEpoch++; previewing = false; previewTicket = 0; previewNode = null;
        var output = previewAudio; previewAudio = null;
        output?.Stop(); output?.Dispose();
    }
    public void Choose(string optionId)
    {
        if (core == null) return;
        ReleaseCountdown();
        PauseInternal(true);
        if (!core.Choose(optionId)) { Status = "支线选择已变化，请重新打开选择列表。"; Notify(); return; }
        offset = 0; SetExactPosition(); SaveResume(); Notify(); Play();
    }
    /// <summary>分支页修改默认分支后同步：只在正使用“默认分支”时重建路线，暂停并保留当前句。</summary>
    public void ApplyDefaultBranchOption(int number)
    {
        if (core == null || !IsDefaultPolicy(Policy)) return;
        var target = number == 2 ? ListeningBranchPolicy.Second : ListeningBranchPolicy.First;
        if (Policy == target) return;
        SetPolicy(target);
        Status = "默认分支已改为" + ListeningText.OptionOrdinal(number) + "，已暂停；核对当前位置后继续收听。"; Notify();
    }
    public void SetPolicy(ListeningBranchPolicy policy)
    {
        if (core == null || Policy == policy) return;
        ReleaseCountdown(); PauseInternal(true); core.SetPolicy(policy); offset = 0; SetExactPosition(); SaveResume();
        Status = "支线方式已更改，已暂停；核对当前位置后继续收听。"; Notify();
    }
    public void AddBookmark(string label)
    {
        if (previewing || HasBlockingNotice || core?.Current == null || ChapterProgress == null) return;
        ChapterProgress.Bookmarks.Add(new ListeningBookmark
        { Label = string.IsNullOrWhiteSpace(label) ? core.Current.PositionLabel : label.Trim(), Snapshot = core.Capture(PositionMilliseconds) });
        SaveDocument(); Status = "已添加听书书签。"; Notify();
    }
    public void RemoveBookmark(string id)
    { ChapterProgress?.Bookmarks.RemoveAll(x => x.Id == id); SaveDocument(); Notify(); }
    public void RestoreBookmark(string id)
    {
        var mark = ChapterProgress?.Bookmarks.FirstOrDefault(x => x.Id == id);
        if (core == null || mark == null) return;
        PauseInternal(true);
        if (core.TryRestore(mark.Snapshot, out var reason))
        { offset = Math.Max(0, core.ResumePositionMs); resumeNeedsSelection = false; SetExactPosition(); SaveResume(); Status = "已回到书签，点击继续收听。"; }
        else Status = "此书签对应的剧情已变化，请从目录重新选择。";
        Notify();
    }
    public void SetSpeed(float speed)
    { preferences.Speed = float.IsFinite(speed) ? Math.Clamp(speed, .75f, 2f) : 1; audio?.SetSpeed(Speed); previewAudio?.SetSpeed(Speed); SavePreferences(); Notify(); }
    public void SetSmartResume(bool enabled)
    { preferences.SmartResumeEnabled = enabled; SavePreferences(); Notify(); }
    public void RefreshVolume() { audio?.SetVolume(volumeForNode(Current)); previewAudio?.SetVolume(volumeForNode(previewNode)); }
    void SetExactPosition() { exactResumePosition = true; pausedUtc = null; }
    public void SetSleepMinutes(int minutes)
    { sleepDeadline = minutes <= 0 ? 0 : SystemClock.ElapsedRealtime() + Math.Clamp(minutes, 1, 180) * 60_000L; QueueTick(); Notify(); }
    public void SeekMilliseconds(long position)
    {
        if (previewing)
        { PauseInternal(true); Status = "单句试听已停止，原收听位置与选择保持。"; Notify(); return; }
        if (core == null || core.Current?.Kind != ListeningItemKind.Line) return;
        bool wasPlaying = running;
        long duration = DurationMilliseconds;
        PauseInternal(false); offset = Math.Clamp(position, 0, duration > 0 ? Math.Max(0, duration - 100) : Math.Max(0, position));
        resumeNeedsSelection = false; SetExactPosition(); SaveResume(); Notify(); if (wasPlaying) Play();
    }
    void QueueTick()
    { if (!timerQueued && (IsPlaying || sleepDeadline != 0)) { timerQueued = true; main.PostDelayed(timerTick, 1000); } }
    void Tick()
    {
        timerQueued = false;
        long now = SystemClock.ElapsedRealtime();
        if (sleepDeadline > 0 && now >= sleepDeadline) { SleepExpired(); return; }
        if (previewing && now - previewStartedAt > 30_000 && previewAudio?.Playing != true)
        { StopPreview(); Status = "单句试听未能开始，原位置已保留。请重试或检查配音文件。"; Notify(); return; }
        if (running && now - startedAt > 20_000 && ticket == 0) { Fail("后台播放启动等待超时，请重试"); return; }
        if (running && now - startedAt > 30_000 && audio?.Playing != true)
        { Fail("此句音频无法准备完成，请点下一句或更换配音包"); return; }
        if (running && now - lastSavedAt >= 5000) { SaveResume(); ListeningForegroundService.Refresh(); }
        QueueTick();
    }
    void SleepExpired()
    { sleepDeadline = 0; PauseInternal(true); Status = "睡眠定时已到，已暂停并保存当前位置。"; Notify(); }
    public void SaveResume()
    {
        if (core == null || ChapterProgress == null || resumeNeedsSelection) return;
        var snapshot = core.Capture(PositionMilliseconds);
        snapshot.PausedUtc = pausedUtc;
        snapshot.ExactResumePosition = exactResumePosition;
        ChapterProgress.Resume = snapshot; lastSavedAt = SystemClock.ElapsedRealtime(); SaveDocument();
    }
    void SaveDocument()
    {
        if (document == null) return;
        try { store.Save(document); saveWarning = null; }
        catch (Exception ex) { diagnostics.Log("听书存档失败", ex.Message); saveWarning = "听书进度尚未写入，请检查手机可用存储空间"; }
    }
    void SavePreferences()
    {
        try { Json.Save(settingsPath, preferences); preferencesSaveWarning = null; }
        catch (Exception ex)
        {
            preferencesSaveWarning = "听书设置尚未保存，本次已生效；请检查手机可用存储空间后重新设置";
            diagnostics.Log("听书设置", ex.Message);
        }
    }
    void Notify() { Changed?.Invoke(); ListeningForegroundService.Refresh(); }
}
