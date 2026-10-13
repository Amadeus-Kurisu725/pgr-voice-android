using System.Linq;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp.Platform;

/// <summary>游戏模式顶边融合条里的一个选项（设计稿 08 / 10）。</summary>
public sealed record PromptBarOption(string Id, string Label, bool Auto);

/// <summary>
/// 设计稿 08 / 10 / 11 的游戏配音倒计时条：全部字段由会话从规则层（AppSession.GameCountdown*）抄过来，界面不自己计时。
/// Waiting = 「分支自动选择」关闭时停下等待（不显示秒数、环和细轨）。
/// </summary>
public sealed record CountdownBarModel(bool Order, bool Ending, bool Waiting, bool Paused, int Seconds, float Fraction,
    string PrevSpeaker, string PrevLine, string? PreviousOption, IReadOnlyList<PromptBarOption> Options, string? OrderSpeaker, string? OrderLine, string Status);

/// <summary>设计稿 05b：自动播放中待配音 / 无配音句按字数停留。StartedAt 为 SystemClock.ElapsedRealtime。</summary>
public sealed record PendingVoiceBarModel(string Speaker, string Line, string Tag, long StartedAt, int TotalMs);

/// <summary>设计稿 13：游戏模式探索菜单一行。Choosable = 可点选播放（当前菜单仍列出该项）。</summary>
public sealed record ExploreBarRow(string Id, string Label, bool Heard, bool Exit, bool Choosable, bool Current = false);
/// <summary>设计稿 13：游戏模式探索菜单面板（「EXPLORE · 探索中」）。</summary>
/// <summary>设计稿 13：Title 一行（「29-1 营地 · 可交谈人物」）；Hint =「建议先找…」（无或已听过时为 null）。</summary>
public sealed record ExploreBarModel(string MenuKey, string Title, string? Hint, IReadOnlyList<ExploreBarRow> Rows);

/// <summary>设计稿 06：原表终点 / 等待游戏画面定位。</summary>
public sealed record EndWaitBarModel(string Section, string Hint, string Detail);

/// <summary>
/// 0.4.8 游戏模式顶边融合条（设计稿 05b / 06 / 08 / 09 / 10 / 11）：屏幕顶边居中，宽不超过 620dp；
/// 左侧深石板块（状态铭文 + 标题 + 倒计时环），右侧冷灰白纸面（台词、选项片、细轨、按键）。
/// · 可操作条（05b / 06 / 08 / 10 / 11）同一时间只有一条，窗口只包住条本身，条外触摸照常落到游戏。
/// · 09「已自动选择」为只读条（NotTouchable），约 3 秒后收起。
/// · 截图识别期间两种条都隐藏（不进入 OCR 画面）。条不计入 ContainsPoint / IntersectsRectangle（审稿 c1478eb），
/// · 与字幕框、下一句点击区域重叠时自动挪到区域下方 / 上方（PromptBarY）。
/// </summary>
public sealed partial class OverlayController
{
    /// <summary>悬浮层显示 / 隐藏时变化：会话据此重新下发顶边条（会话按状态签名缓存）。</summary>
    public int PromptBarEpoch { get; private set; }
    View? promptBarView; string? promptBarKey; Action? promptBarRefresh;
    CountdownBadge? promptBadge; CountdownRail? promptRail; PauseToggle? promptPause;
    Java.Lang.Runnable? promptDwellTicker;

    /// <summary>
    /// 审稿（c1478eb）：提示条不计入 ContainsPoint / IntersectsRectangle，不影响字幕 / 选项区域有效性与自动点击；
    /// 条只拦截自己窗口范围内的触碰（窗口宽高 = 条本身，NotTouchModal）。为此条要避开这些区域：
    /// 会话提供字幕框、下一句点击区域（按整块物理屏幕的 0–1 比例），条与之重叠时挪到区域下方 / 上方。
    /// </summary>
    public Func<IReadOnlyList<(float Left, float Top, float Right, float Bottom)>>? PromptAvoidAreas { get; set; }

    (int Width, int Height) FullDisplaySize()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30)) { var b = windows.CurrentWindowMetrics.Bounds; return (b.Width(), b.Height()); }
        var m = context.Resources!.DisplayMetrics!; return (m.WidthPixels, m.HeightPixels);
    }

    /// <summary>条的纵向位置：默认贴顶；与避让区域重叠时依次试区域下方、区域上方；都放不下时留在顶边（区域判断本就不计入条）。</summary>
    int PromptBarY(int x, int width, int height)
    {
        var safe = ScreenArea();
        int preferred = safe.Top + ReadDp(6), gap = ReadDp(6);
        IReadOnlyList<(float Left, float Top, float Right, float Bottom)> areas;
        try { areas = PromptAvoidAreas?.Invoke() ?? Array.Empty<(float, float, float, float)>(); } catch { return preferred; }
        if (areas.Count == 0) return preferred;
        var (dw, dh) = FullDisplaySize();
        var rects = areas.Select(a => (L: (int)(a.Left * dw), T: (int)(a.Top * dh), R: (int)Math.Ceiling(a.Right * dw), B: (int)Math.Ceiling(a.Bottom * dh)))
            .Where(r => r.R > r.L && r.B > r.T).ToList();
        bool Clear(int y) => y >= safe.Top && y + height <= safe.Bottom &&
            rects.All(r => !(x < r.R && x + width > r.L && y < r.B && y + height > r.T));
        if (Clear(preferred)) return preferred;
        foreach (int y in rects.Select(r => r.B + gap).Concat(rects.Select(r => r.T - gap - height)).OrderBy(y => Math.Abs(y - preferred)))
            if (Clear(y)) return y;
        return preferred;
    }

    int MeasureBarHeight(View view, int width)
    {
        view.Measure(View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        return view.MeasuredHeight;
    }

    void RepositionPromptBar()
    {
        if (promptBarView is not { IsAttachedToWindow: true } view || view.LayoutParameters is not WindowManagerLayoutParams lp || view.Height <= 0) return;
        int y = PromptBarY(lp.X, lp.Width, view.Height);
        if (y == lp.Y) return;
        lp.Y = y;
        try { windows.UpdateViewLayout(view, lp); } catch { }
    }

    /// <summary>左侧探索卡的顶边：台词悬浮条在上半屏且横向重叠时放到它下方 6dp，否则贴安全区顶部 8dp。</summary>
    int PanelY(int x, int width)
    {
        var safe = ScreenArea();
        int y = safe.Top + ReadDp(8);
        // 台词条与探索卡可能在同一次更新里先后加入，此时它还没挂到屏幕；按窗口参数判断，不等挂上。
        if (PlacementMode() != "bar" || !showing || parameters == null || root == null || root.Visibility != ViewStates.Visible) return y;
        int barWidth = parameters.Width, barHeight = parameters.Height;
        bool overlapsX = x < parameters.X + barWidth && x + width > parameters.X;
        bool upperHalf = parameters.Y + barHeight / 2 < (safe.Top + safe.Bottom) / 2;
        return overlapsX && upperHalf ? Math.Max(y, parameters.Y + barHeight + ReadDp(6)) : y;
    }

    /// <summary>悬浮条拖动、展开收起或台词全文展开后，探索卡跟着让开。</summary>
    void RepositionExplorePanel()
    {
        if (!promptBarIsPanel || promptBarView is not { IsAttachedToWindow: true } view || view.LayoutParameters is not WindowManagerLayoutParams lp) return;
        int y = PanelY(lp.X, lp.Width);
        if (y == lp.Y) return;
        lp.Y = y;
        try { windows.UpdateViewLayout(view, lp); } catch { }
    }

    void ApplyPromptBarCaptureVisibility()
    {
        ApplyPromptBarTouchable();
    }

    /// <summary>自动点击进行中（PromptBarTouchLease 占用）时条不接收触碰。</summary>
    bool promptTapSuspended;

    /// <summary>自动点击开始 / 结束时由 PromptBarTouchLease 调用：false = 条不接收触碰，true = 恢复。主线程上立即生效。</summary>
    public void SetPromptBarsTouchable(bool touchable) => OnMain(() => { promptTapSuspended = !touchable; ApplyPromptBarTouchable(); });

    /// <summary>主线程在隐藏前取得本控制器两类提示窗的无障碍窗口身份，用于等待系统移除旧快照。</summary>
    public IReadOnlyList<int> GetPromptWindowIds()
    {
        var ids = new List<int>(2);
        foreach (var view in new[] { promptBarView, branchEnteredView })
        {
            if (view is not { IsAttachedToWindow: true }) continue;
            using var info = view.CreateAccessibilityNodeInfo();
            if (info != null && info.WindowId >= 0) ids.Add(info.WindowId);
        }
        return ids;
    }

    void ApplyPromptBarTouchable()
    {
        ApplyPromptWindowState(promptBarView, readOnly: false);
        ApplyPromptWindowState(branchEnteredView, readOnly: true);
    }

    // NotTouchable 仍可能留在无障碍窗口列表，多层 ApplicationOverlay 的不透明度也会叠加。
    // 隐藏根视图并把窗口 Alpha 置零，保留原位置、列表与计时器；恢复时仍服从当前截图隐藏状态。
    void SetPromptWindowState(View view, WindowManagerLayoutParams lp, bool readOnly)
    {
        bool hidden = captureHidden || promptTapSuspended;
        view.Visibility = hidden ? ViewStates.Invisible : ViewStates.Visible;
        lp.Alpha = hidden ? 0f : 1f;
        lp.Flags = hidden || readOnly ? lp.Flags | WindowManagerFlags.NotTouchable : lp.Flags & ~WindowManagerFlags.NotTouchable;
    }

    void ApplyPromptWindowState(View? view, bool readOnly)
    {
        if (view?.LayoutParameters is WindowManagerLayoutParams lp && view.IsAttachedToWindow)
        {
            var flags = lp.Flags; float alpha = lp.Alpha;
            SetPromptWindowState(view, lp, readOnly);
            if (flags != lp.Flags || alpha != lp.Alpha)
                try { windows.UpdateViewLayout(view, lp); } catch { }
        }
    }

    // ---------------- 公开入口 ----------------

    /// <summary>设计稿 08 / 10 / 11。每次规则层倒计时变化时调用；结构不变时只刷新秒数、环、细轨和暂停键。</summary>
    public void ShowCountdownBar(CountdownBarModel m, Action<string> choose, Action inOrder, Action togglePause) => OnMain(() =>
    {
        string key = "cd|" + m.Order + "|" + m.Ending + "|" + m.Waiting + "|" + (m.Order && m.Paused) + "|" + m.PrevSpeaker + "|" + m.PrevLine + "|" +
            string.Join("/", m.Options.Select(o => o.Id + (o.Auto ? "*" : ""))) + "|" + m.OrderLine;
        SetPromptBar(key, () => m.Order ? BuildOrderBar(m, inOrder, togglePause) : BuildBranchBar(m, choose, togglePause), () =>
        {
            promptBadge?.Update(m.Seconds, m.Fraction, m.Paused);
            promptRail?.Update(m.Fraction, m.Paused);
            promptPause?.Update(m.Paused);
        });
    });

    /// <summary>设计稿 05b。</summary>
    public void ShowPendingVoiceBar(PendingVoiceBarModel m) => OnMain(() =>
        SetPromptBar("pv|" + m.StartedAt + "|" + m.Line, () => BuildPendingVoiceBar(m), null));

    /// <summary>设计稿 06。用户点「收起」后同一终点不再弹出（面板里的等待卡仍在）。</summary>
    public void ShowEndWaitBar(EndWaitBarModel m) => OnMain(() =>
    {
        string key = "ew|" + m.Section + "|" + m.Hint;
        if (key == endWaitDismissedKey) { ClearPromptBarCore(); return; }
        SetPromptBar(key, () => BuildEndWaitBar(m, key), null);
    });
    string? endWaitDismissedKey;

    /// <summary>设计稿 13。点人物行 = 选入该选项开始播放（会话走原有的悬浮层选分支流程）；「收起」后同一菜单不再弹出。</summary>
    public void ShowExploreBar(ExploreBarModel m, Action<string> choose) => OnMain(() =>
    {
        exploreLast = (m, choose);
        if (m.MenuKey == exploreCollapsedKey) ShowExploreTab(m);
        else ShowExplorePanel(m, choose);
    });
    /// <summary>方案 A：「收起」后同一菜单只留左上小签；换了菜单照常展开。</summary>
    string? exploreCollapsedKey;
    (ExploreBarModel Model, Action<string> Choose)? exploreLast;

    static int ExploreRest(ExploreBarModel m) => m.Rows.Count(r => !r.Exit && !r.Heard);

    void ShowExplorePanel(ExploreBarModel m, Action<string> choose)
    {
        string key = "ex|" + m.MenuKey + "|" + m.Hint + "|" + string.Join(",", m.Rows.Select(r => r.Id + (r.Heard ? "1" : "0") + (r.Choosable ? "c" : "") + (r.Current ? "p" : "")));
        SetPromptBar(key, () => BuildExploreBar(m, choose), null, leftPanel: true, panelWidthDp: 220);
    }

    void ShowExploreTab(ExploreBarModel m) =>
        SetPromptBar("ext|" + m.MenuKey + "|" + ExploreRest(m), () => BuildExploreTab(m), null, leftPanel: true, panelWidthDp: 120);

    public void ClearPromptBar() => OnMain(ClearPromptBarCore);

    /// <summary>设计稿 09：「已自动选择：xxx」只读融合条，3 秒后收起。detail 为第二行说明（如「倒计时结束 · 默认分支第一个选项 · 自动播放继续」）。</summary>
    public void ShowAutoSelectedBar(string message, string? detail, string eyebrow = "AUTO · SELECTED", string doneCaption = "已按默认继续") => OnMain(() =>
    {
        ClearBranchEntered();
        if (disposed || !CanShow || string.IsNullOrWhiteSpace(message)) return;
        var view = BuildSelectedBar(message.Trim(), detail, eyebrow, doneCaption);
        var safe = ScreenArea();
        int width = Math.Min(ReadDp(620), safe.Right - safe.Left - ReadDp(24));
        var layout = new WindowManagerLayoutParams(width, -2, WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable | WindowManagerFlags.LayoutInScreen, Format.Translucent)
        { Gravity = GravityFlags.Top | GravityFlags.Left, X = safe.Left + (safe.Right - safe.Left - width) / 2, Y = safe.Top + ReadDp(8) };
        layout.Y = PromptBarY(layout.X, width, MeasureBarHeight(view, width));
        branchEnteredView = view;
        SetPromptWindowState(view, layout, readOnly: true);
        try
        {
            windows.AddView(view, layout);
            main.PostDelayed(() => { if (ReferenceEquals(branchEnteredView, view)) ClearBranchEntered(); }, 3000);
        }
        catch { ClearBranchEntered(); }
    });

    // ---------------- 窗口 ----------------

    void SetPromptBar(string key, Func<View> build, Action? refresh, bool leftPanel = false, int panelWidthDp = 340)
    {
        if (disposed || !CanShow || !showing) { ClearPromptBarCore(); return; }
        if (key == promptBarKey && promptBarView is { IsAttachedToWindow: true })
        { promptBarRefresh = refresh; refresh?.Invoke(); if (!promptBarIsPanel) RepositionPromptBar(); return; }
        ClearPromptBarCore();
        var view = build();
        var safe = ScreenArea();
        int safeWidth = safe.Right - safe.Left, safeHeight = safe.Bottom - safe.Top;
        WindowManagerLayoutParams layout;
        if (leftPanel)
        {
            // 方案 A：左侧紧凑卡 220dp，放在顶部悬浮条下方（不再压住台词和按钮），到约半高为止，避开左下角摇杆。
            int width = Math.Min(ReadDp(panelWidthDp), safeWidth - ReadDp(16)), x = safe.Left + ReadDp(12), y = PanelY(x, width);
            layout = new WindowManagerLayoutParams(width, -2, WindowManagerTypes.ApplicationOverlay,
                WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutInScreen, Format.Translucent)
            { Gravity = GravityFlags.Top | GravityFlags.Left, X = x, Y = y };
            FitPanelHeight(view, width, Math.Max(ReadDp(120), Math.Min((int)(safeHeight * .55f), safe.Bottom - y - ReadDp(8))));
        }
        else
        {
            int width = Math.Min(ReadDp(620), safeWidth - ReadDp(16)); // 定稿：最宽 620dp
            layout = new WindowManagerLayoutParams(width, -2, WindowManagerTypes.ApplicationOverlay,
                WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutInScreen, Format.Translucent)
            { Gravity = GravityFlags.Top | GravityFlags.Left, X = safe.Left + (safeWidth - width) / 2, Y = safe.Top + ReadDp(6) };
            layout.Y = PromptBarY(layout.X, width, MeasureBarHeight(view, width));
        }
        promptBarIsPanel = leftPanel;
        promptBarView = view; promptBarKey = key; promptBarRefresh = refresh;
        SetPromptWindowState(view, layout, readOnly: false);
        try { windows.AddView(view, layout); refresh?.Invoke(); }
        catch (Exception ex) { ClearPromptBarCore(); Error?.Invoke("提示条未能显示：" + ex.Message); }
    }

    bool promptBarIsPanel;
    ScrollView? promptPanelList;

    /// <summary>左侧面板总高不超过 maxHeight：列表框（本身 ≤ 196dp）按超出部分缩短，至少留一行半。</summary>
    void FitPanelHeight(View view, int width, int maxHeight)
    {
        if (promptPanelList?.LayoutParameters is not LinearLayout.LayoutParams lp) return;
        int over = MeasureBarHeight(view, width) - maxHeight;
        if (over <= 0) return;
        lp.Height = Math.Max(ReadDp(66), lp.Height - over);
        promptPanelList.LayoutParameters = lp;
    }

    void ClearPromptBarCore()
    {
        if (promptDwellTicker != null) { main.RemoveCallbacks(promptDwellTicker); promptDwellTicker = null; }
        var old = promptBarView; promptBarView = null; promptBarKey = null; promptBarRefresh = null;
        promptBadge = null; promptRail = null; promptPause = null; promptPanelList = null; promptBarIsPanel = false;
        if (old == null) return;
        try { windows.RemoveViewImmediate(old); } catch { }
        old.Dispose();
    }

    // ---------------- 公共部件 ----------------

    LinearLayout FusedShell(float leftDp, bool ending, out LinearLayout left, out LinearLayout right)
    {
        var bar = new FusedBarDrawable(density) { LeftWidthDp = leftDp };
        if (ending)
        {
            bar.LeftTop = Color.ParseColor("#3A2A30"); bar.LeftBottom = Color.ParseColor("#272D35");
            bar.EdgeTop = Color.ParseColor("#E0485C"); bar.EdgeBottom = PgrTheme.Brass; bar.Seam = Color.Argb(217, 224, 106, 121);
        }
        var shell = new LinearLayout(context) { Orientation = Orientation.Horizontal, Background = bar };
        shell.SetMinimumHeight(ReadDp(72));
        left = new LinearLayout(context) { Orientation = Orientation.Vertical }; left.SetGravity(GravityFlags.CenterVertical);
        left.SetPadding(ReadDp(14), ReadDp(9), ReadDp(4), ReadDp(9));
        shell.AddView(left, new LinearLayout.LayoutParams(ReadDp(leftDp - 22), -1));
        right = new LinearLayout(context) { Orientation = Orientation.Vertical }; right.SetGravity(GravityFlags.CenterVertical);
        right.SetPadding(ReadDp(26), ReadDp(8), ReadDp(10), ReadDp(8));
        shell.AddView(right, new LinearLayout.LayoutParams(0, -2, 1));
        return shell;
    }

    /// <summary>定稿 06：青色菱形 ◆ + #9FD6D2 文字（自动播放保持开启）。</summary>
    TextView TealKeep(string text)
    {
        // 定稿 p047.js:173 / p047b.js:44：7px 菱形 #6FB3B0，文字 #9FD6D2。
        var sb = new SpannableStringBuilder("◆ " + text);
        sb.SetSpan(new Android.Text.Style.ForegroundColorSpan(Color.ParseColor("#6FB3B0")), 0, 1, SpanTypes.ExclusiveExclusive);
        var t = Light("", 11.5f, Color.ParseColor("#9FD6D2")); t.TextFormatted = sb;
        return t;
    }

    TextView Light(string text, float sp, Color color, bool bold = false, int maxLines = 1)
    {
        var t = PromptArt.Label(context, text, sp, color, bold, maxLines);
        if (maxLines == 1) { t.SetSingleLine(true); t.Ellipsize = TextUtils.TruncateAt.End; }
        return t;
    }

    View SpeakerRow(string speaker, string line, float sp = 12)
    {
        var t = new TextView(context) { TextSize = sp }; t.SetSingleLine(true); t.Ellipsize = TextUtils.TruncateAt.End;
        t.TextFormatted = PromptArt.SpeakerLine(speaker, line, PromptArt.Ink, PgrTheme.Secondary);
        return t;
    }

    View Keys(params View[] keys)
    {
        var col = new LinearLayout(context) { Orientation = Orientation.Vertical }; col.SetGravity(GravityFlags.CenterVertical);
        col.SetPadding(0, ReadDp(8), ReadDp(10), ReadDp(8));
        for (int i = 0; i < keys.Length; i++) col.AddView(keys[i], new LinearLayout.LayoutParams(ReadDp(62), 0, 1) { TopMargin = i > 0 ? ReadDp(6) : 0 });
        return col;
    }

    Button CommandKey(string text, string icon, OverlayCommand command, bool vertical = true, bool brass = false, bool quiet = false)
    {
        var key = OverlayKeys.Key(context, text, icon, vertical, 12, brass, quiet);
        key.SetMinHeight(ReadDp(40)); key.SetMinimumHeight(ReadDp(40));
        key.Click += (_, _) => Command?.Invoke(command);
        return key;
    }

    PauseToggle PromptPauseKey(bool paused, Action togglePause)
    {
        var toggle = new PauseToggle(context, vertical: true, textSp: 12.5f);
        toggle.Update(paused);
        toggle.Click += (_, _) => togglePause();
        promptPause = toggle;
        return toggle;
    }

    // ---------------- 08 / 10 ----------------

    View BuildBranchBar(CountdownBarModel m, Action<string> choose, Action togglePause)
    {
        bool counting = !m.Waiting;
        var shell = FusedShell(m.Ending ? 236 : 214, m.Ending, out var left, out var right);
        left.AddView(PgrUi.En(context, m.Ending ? "ENDING · 结局分歧" : "BRANCH · 待选互动", 9, m.Ending ? PromptArt.EndingPink : PgrArt.GoldLine));
        left.AddView(Light(m.Ending ? "此处是结局分歧" : counting ? "分支自动选择" : "等待选择", 15, Color.White, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        if (m.Ending) left.AddView(Light("只收听所选结局", 11.5f, Color.ParseColor("#D8C9B0")), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(2) });
        if (counting)
        {
            promptBadge = new CountdownBadge(context, m.Ending ? "秒后选入结局" : "秒后自动选择", dark: true, radiusDp: 13, numberSp: 13);
            left.AddView(promptBadge, new LinearLayout.LayoutParams(-2, -2) { TopMargin = ReadDp(7) });
        }
        else left.AddView(Light("分支自动选择已关闭", 11, PgrTheme.NavSub, maxLines: 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });

        if (m.PrevLine.Length > 0) right.AddView(SpeakerRow(m.PrevSpeaker, m.PrevLine), new LinearLayout.LayoutParams(-1, -2));
        bool inline = m.Options.Count <= 2;
        var tiles = new LinearLayout(context) { Orientation = inline ? Orientation.Horizontal : Orientation.Vertical };
        for (int i = 0; i < m.Options.Count; i++)
        {
            var o = m.Options[i]; string id = o.Id; bool auto = counting && o.Auto, endingOption = o.Label.Contains("开启结局");
            var tile = OptionTiles.Tile(context, i, o.Label, auto, auto ? (m.Ending || endingOption ? OptionTail.Ending : OptionTail.Default) : OptionTail.Chevron,
                PromptArt.EndingName(o.Label), auto && m.Ending ? PromptArt.EndingRed : null, compact: true);
            tile.Click += (_, _) => choose(id);
            float weight = inline && m.Ending ? (auto ? 2.1f : .9f) : 1;
            tiles.AddView(tile, inline ? new LinearLayout.LayoutParams(0, -2, weight) { LeftMargin = i > 0 ? ReadDp(8) : 0 }
                : new LinearLayout.LayoutParams(-1, -2) { TopMargin = i > 0 ? ReadDp(5) : 0 });
        }
        right.AddView(tiles, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(6) });
        if (counting)
        {
            promptRail = new CountdownRail(context, m.Ending ? "AUTO-PICK · 将选入结局" : "AUTO-PICK · 将自动选择");
            right.AddView(promptRail, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(6) });
        }
        if (m.Ending)
            right.AddView(OptionTiles.Hint(context, "lock", PromptArt.EndingRed, "选入结局后只读这一结局，读完即停；如需其他结局，请回到结局选项前重新选择。", 10.5f, 2),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        // 定稿 p047b.js:50（08）：「点任一选项立即选择」+ PZ_TXT；p047b.js:75（10）：只有 PZ_TXT。620dp 宽下两条分两行。
        // 「分支自动选择」关闭（停下等待）时显示规则层文案（AutoSelectOffText / EndingAutoSelectOffText）。
        var hints = new LinearLayout(context) { Orientation = Orientation.Vertical };
        if (!counting)
            hints.AddView(OptionTiles.Hint(context, "branch", PgrTheme.Brass, m.Ending ? PgrVoice.Following.PlayAlongCountdown.EndingAutoSelectOffText : PgrVoice.Following.PlayAlongCountdown.AutoSelectOffText, 10.5f, 2));
        else
        {
            if (!m.Ending) hints.AddView(OptionTiles.Hint(context, "branch", PgrTheme.Brass, "点任一选项立即选择", 10.5f, 1));
            hints.AddView(OptionTiles.Hint(context, "pause", PromptArt.SlateBlue, PromptArt.PauseHint, 10.5f, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = m.Ending ? 0 : ReadDp(3) });
        }
        right.AddView(hints, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        if (counting)
        {
            var toggle = PromptPauseKey(m.Paused, togglePause);
            shell.AddView(toggle, new LinearLayout.LayoutParams(ReadDp(62), -1) { TopMargin = ReadDp(10), BottomMargin = ReadDp(10), RightMargin = ReadDp(10) });
        }
        shell.ContentDescription = m.Status;
        return shell;
    }

    // ---------------- 11 ----------------

    View BuildOrderBar(CountdownBarModel m, Action inOrder, Action togglePause)
    {
        bool counting = !m.Waiting;
        var shell = FusedShell(214, false, out var left, out var right);
        left.AddView(PgrUi.En(context, "ROUTE · 续接未核实", 9, PgrArt.GoldLine));
        left.AddView(Light("此处分支\n续接未核实", 15, Color.White, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        if (counting)
        {
            promptBadge = new CountdownBadge(context, "秒后按顺序继续", dark: true, radiusDp: 13, numberSp: 13);
            left.AddView(promptBadge, new LinearLayout.LayoutParams(-2, -2) { TopMargin = ReadDp(7) });
        }
        else left.AddView(Light("分支自动选择已关闭", 11, PgrTheme.NavSub, maxLines: 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });

        // 定稿 p047b.js:81：「已读完所选「选项」：说话人：台词」（选项文字取规则层 GameCountdownPreviousOptionLabel，共同线时省略）。
        if (m.PrevLine.Length > 0)
        {
            var read = new TextView(context) { TextSize = 12 }; read.SetSingleLine(true); read.Ellipsize = TextUtils.TruncateAt.End;
            var sb = new SpannableStringBuilder(string.IsNullOrWhiteSpace(m.PreviousOption) ? "已读完：" : "已读完所选「" + m.PreviousOption + "」：");
            sb.SetSpan(new Android.Text.Style.ForegroundColorSpan(PgrTheme.Secondary), 0, sb.Length(), SpanTypes.ExclusiveExclusive);
            sb.Append(PromptArt.SpeakerLine(m.PrevSpeaker, m.PrevLine, PromptArt.Ink, PgrTheme.Secondary));
            read.TextFormatted = sb;
            right.AddView(read, new LinearLayout.LayoutParams(-1, -2));
        }
        // 定稿 p047b.js:82：未暂停 =「倒计时结束后…」+ PZ_TXT；已暂停 =「已暂停：…也可直接点「按顺序继续」或「下一句」。」；关闭自动选择时取规则层文案。
        var explain = new TextView(context) { TextSize = 12 }; explain.SetTextColor(PromptArt.Ink); explain.SetLineSpacing(ReadDp(1), 1); explain.SetMaxLines(3);
        if (!counting) explain.Text = PgrVoice.Following.PlayAlongCountdown.OrderAutoSelectOffText;
        else if (m.Paused)
        {
            var sb = new SpannableStringBuilder("已暂停");
            sb.SetSpan(new Android.Text.Style.ForegroundColorSpan(PgrTheme.BrassText), 0, sb.Length(), SpanTypes.ExclusiveExclusive);
            sb.SetSpan(new Android.Text.Style.StyleSpan(TypefaceStyle.Bold), 0, sb.Length(), SpanTypes.ExclusiveExclusive);
            sb.Append("：倒计时停住，不自动续接；点「继续」恢复。也可直接点「按顺序继续」或「下一句」。");
            explain.TextFormatted = sb;
        }
        else explain.Text = "倒计时结束后从本节下一句接着听，并显示「此处分支续接未核实，已按顺序继续」。" + PromptArt.PauseHint;
        right.AddView(explain, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(4) });
        if (counting)
        {
            promptRail = new CountdownRail(context, "IN ORDER · 按顺序");
            right.AddView(promptRail, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        }
        if (!string.IsNullOrWhiteSpace(m.OrderLine))
        {
            var next = new TextView(context) { TextSize = 11.5f }; next.SetSingleLine(true); next.Ellipsize = TextUtils.TruncateAt.End;
            var sb = new SpannableStringBuilder("本节下一句 ▸ ");
            sb.SetSpan(new Android.Text.Style.ForegroundColorSpan(PgrTheme.BrassText), 0, sb.Length(), SpanTypes.ExclusiveExclusive);
            sb.Append(PromptArt.SpeakerLine(m.OrderSpeaker, m.OrderLine!, PromptArt.Ink, PgrTheme.Secondary));
            next.TextFormatted = sb;
            right.AddView(next, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        }
        // 按键格（宽 150）：按顺序继续（黄铜整宽）；下一句 + 暂停；反馈（弱）；「下一句」始终可用。
        var grid = new LinearLayout(context) { Orientation = Orientation.Vertical };
        grid.SetPadding(0, ReadDp(8), ReadDp(10), ReadDp(8));
        var go = OverlayKeys.Key(context, "按顺序继续", "next", false, 12.5f, brass: true);
        go.SetMinHeight(ReadDp(40)); go.SetMinimumHeight(ReadDp(40));
        go.Click += (_, _) => inOrder();
        grid.AddView(go, new LinearLayout.LayoutParams(-1, -2));
        var pair = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        pair.AddView(CommandKey("下一句", "next", OverlayCommand.Next), new LinearLayout.LayoutParams(0, ReadDp(60), 1));
        if (counting) pair.AddView(PromptPauseKey(m.Paused, togglePause), new LinearLayout.LayoutParams(0, ReadDp(60), 1) { LeftMargin = ReadDp(6) });
        grid.AddView(pair, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(6) });
        grid.AddView(CommandKey("反馈", "feedback", OverlayCommand.Feedback, vertical: false, quiet: true), new LinearLayout.LayoutParams(-1, ReadDp(40)) { TopMargin = ReadDp(6) });
        grid.AddView(OptionTiles.Hint(context, "check", Color.ParseColor("#9FD6D2"), "「下一句」始终可用", 10, 1), new LinearLayout.LayoutParams(-2, -2) { TopMargin = ReadDp(3), Gravity = GravityFlags.CenterHorizontal });
        shell.AddView(grid, new LinearLayout.LayoutParams(ReadDp(150), -2));
        shell.ContentDescription = m.Status;
        return shell;
    }

    // ---------------- 05b ----------------

    View BuildPendingVoiceBar(PendingVoiceBarModel m)
    {
        var shell = FusedShell(170, false, out var left, out var right);
        left.AddView(PgrUi.En(context, "AUTO · ON", 9, PgrArt.GoldLine));
        left.AddView(Light("自动播放", 15, Color.White, true), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        left.AddView(TealKeep("保持开启"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(4) });
        var head = new LinearLayout(context) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        if (m.Speaker.Length > 0) head.AddView(Light(m.Speaker, 12.5f, PromptArt.Ink, true), new LinearLayout.LayoutParams(-2, -2) { RightMargin = ReadDp(8) });
        if (m.Tag.Length > 0)
        {
            var tag = Light(m.Tag, 10, PgrTheme.BrassText, true);
            tag.Background = PgrTheme.Plate(context, PgrTheme.A(PgrTheme.Brass, 30), PgrTheme.Brass, 4, Corners.TopRight | Corners.BottomLeft);
            tag.SetPadding(ReadDp(6), ReadDp(1), ReadDp(6), ReadDp(1));
            head.AddView(tag, new LinearLayout.LayoutParams(-2, -2) { RightMargin = ReadDp(8) });
        }
        head.AddView(Light("字幕停留后自动继续", 10.5f, PgrTheme.Muted), new LinearLayout.LayoutParams(0, -2, 1));
        right.AddView(head, new LinearLayout.LayoutParams(-1, -2));
        right.AddView(Light(m.Line, 13, PromptArt.Ink, maxLines: 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(3) });
        var railRow = new LinearLayout(context) { Orientation = Orientation.Horizontal }; railRow.SetGravity(GravityFlags.CenterVertical);
        var rail = new CountdownRail(context, "");
        railRow.AddView(rail, new LinearLayout.LayoutParams(0, -2, 1));
        var clock = Light("", 10.5f, PgrTheme.BrassText); clock.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold);
        railRow.AddView(clock, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(8) });
        right.AddView(railRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        shell.AddView(Keys(CommandKey("下一句", "next", OverlayCommand.Next), CommandKey("暂停", "pause", OverlayCommand.Pause)), new LinearLayout.LayoutParams(-2, -1));
        // 停留时长由会话给出（规则层实际停留时长）；这里只按时间走细轨，不决定何时继续。
        void Tick()
        {
            if (!ReferenceEquals(promptBarView, shell)) return;
            long elapsed = Math.Clamp(SystemClock.ElapsedRealtime() - m.StartedAt, 0, m.TotalMs);
            rail.Update(1f - (float)elapsed / Math.Max(1, m.TotalMs), false);
            clock.Text = $"{elapsed / 1000.0:00.0} / {m.TotalMs / 1000.0:00.0} 秒";
            if (elapsed < m.TotalMs) main.PostDelayed(promptDwellTicker!, 100);
        }
        promptDwellTicker = new Java.Lang.Runnable(Tick);
        main.Post(promptDwellTicker);
        shell.ContentDescription = (m.Tag.Length > 0 ? m.Tag + "，" : "") + "字幕停留后自动继续：" + m.Speaker + "，" + m.Line;
        return shell;
    }

    // ---------------- 06 ----------------

    View BuildEndWaitBar(EndWaitBarModel m, string key)
    {
        var shell = FusedShell(200, false, out var left, out var right);
        left.AddView(PgrUi.En(context, "WAITING · 等待游戏画面定位", 9, PgrArt.GoldLine));
        left.AddView(Light("等待游戏画面定位", 15, Color.White, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        left.AddView(TealKeep("自动播放 · 保持开启"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(4) });
        var head = new LinearLayout(context) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(Light("END · " + m.Section, 11.5f, PromptArt.Ink, true), new LinearLayout.LayoutParams(0, -2, 1));
        var tag = Light("原表终点", 10, PgrTheme.BrassText, true);
        tag.Background = PgrTheme.Plate(context, PgrTheme.A(PgrTheme.Brass, 30), PgrTheme.Brass, 4, Corners.TopRight | Corners.BottomLeft);
        tag.SetPadding(ReadDp(6), ReadDp(1), ReadDp(6), ReadDp(1));
        head.AddView(tag, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(6) });
        right.AddView(head, new LinearLayout.LayoutParams(-1, -2));
        right.AddView(Light(m.Hint.Length > 0 ? m.Hint : "请按游戏画面定位后续", 13.5f, PromptArt.Ink, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(3) });
        right.AddView(Light(m.Detail.Length > 0 ? m.Detail : "游戏出现下一句后自动对齐继续；也可点「识别定位」", 11, PgrTheme.Secondary, maxLines: 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(2) });
        var hide = OverlayKeys.Key(context, "收起", "hide", true, 12, quiet: true);
        hide.Click += (_, _) => { endWaitDismissedKey = key; ClearPromptBarCore(); };
        shell.AddView(Keys(CommandKey("识别定位", "locate", OverlayCommand.Ocr), hide), new LinearLayout.LayoutParams(-2, -1));
        return shell;
    }

    // ---------------- 13 ----------------

    View BuildExploreBar(ExploreBarModel m, Action<string> choose)
    {
        // 方案 A（2026-10-12 用户选定）：紧凑卡 220dp。深色头一行（标题 +「剩 N」），纸面人物行 32dp，
        // 底部「识别定位」+「收起」；收起后变成左上小签，点小签展开。原说明文字和建议提示只留在无障碍描述里。
        var panel = new LinearLayout(context) { Orientation = Orientation.Vertical };
        panel.Background = PgrTheme.Plate(context, PromptArt.OverlayPaper, PgrTheme.A(Color.White, 20), 10, Corners.TopRight | Corners.BottomLeft, shadow: true);
        panel.ContentDescription = "探索面板，" + m.Title + (m.Hint == null ? "" : "，" + m.Hint) + "。走到人物面前开始对话后，点对应的人物开始播放。";
        int rest = ExploreRest(m);
        var head = new LinearLayout(context) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.Background = PgrTheme.Plate(context, Color.ParseColor("#232A32"), null, 0, Corners.None, PgrArt.GoldLine, AccentEdge.Left);
        head.SetPadding(ReadDp(10), ReadDp(5), ReadDp(8), ReadDp(5));
        head.AddView(Light(m.Title, 12, Color.White, true), new LinearLayout.LayoutParams(0, -2, 1));
        if (rest > 0) head.AddView(RestChip(rest), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(6) });
        panel.AddView(head, new LinearLayout.LayoutParams(-1, -2));

        var list = new LinearLayout(context) { Orientation = Orientation.Vertical };
        bool suggested = false;
        foreach (var r in m.Rows.Where(r => !r.Exit).Concat(m.Rows.Where(r => r.Exit)))
        {
            string id = r.Id;
            ExploreRowState state = r.Current ? ExploreRowState.Playing : r.Heard ? ExploreRowState.Heard : ExploreRowState.Unheard;
            bool suggest = !r.Exit && !r.Heard && !r.Current && r.Choosable && !suggested;
            if (suggest) suggested = true;
            string tail = r.Exit ? "随时可选" : r.Current ? "正在收听" : r.Heard ? "✓ 已听" : suggest ? "点这里播放" : "";
            var row = ExploreRows.Row(context, r.Exit ? ExploreRowKind.Exit : ExploreRowKind.Chat, state, r.Label, "", tail, compact: true);
            row.Clickable = r.Choosable;
            if (r.Choosable) row.Click += (_, _) => choose(id);
            list.AddView(row, new LinearLayout.LayoutParams(-1, -2) { TopMargin = list.ChildCount > 0 ? ReadDp(3) : 0 });
        }
        var scroll = new ScrollView(context) { VerticalScrollBarEnabled = true };
        scroll.SetPadding(ReadDp(6), ReadDp(5), ReadDp(6), 0); scroll.SetClipToPadding(false);
        scroll.AddView(list);
        promptPanelList = scroll;
        // 列表框最高 4 行（行 32dp + 间距 3dp）。
        panel.AddView(scroll, new LinearLayout.LayoutParams(-1, Math.Min(ReadDp(145), ReadDp(35) * Math.Max(1, m.Rows.Count) + ReadDp(5))));

        var foot = new LinearLayout(context) { Orientation = Orientation.Horizontal }; foot.SetGravity(GravityFlags.CenterVertical);
        foot.SetPadding(ReadDp(6), ReadDp(4), ReadDp(6), ReadDp(5));
        var locate = CommandKey("识别定位", "locate", OverlayCommand.Ocr, vertical: false);
        locate.TextSize = 11;
        foot.AddView(locate, new LinearLayout.LayoutParams(ReadDp(92), ReadDp(30)));
        foot.AddView(new View(context), new LinearLayout.LayoutParams(0, 1, 1));
        var collapse = OverlayKeys.Key(context, "收起", "chevron_up", false, 11);
        collapse.ContentDescription = "收起探索面板，左上角留一个小签，点它重新展开";
        collapse.Click += (_, _) => { exploreCollapsedKey = m.MenuKey; ShowExploreTab(m); };
        foot.AddView(collapse, new LinearLayout.LayoutParams(ReadDp(68), ReadDp(30)) { LeftMargin = ReadDp(6) });
        panel.AddView(foot, new LinearLayout.LayoutParams(-1, -2));
        return panel;
    }

    /// <summary>方案 A 收起后的小签：「探索中 剩 N ⌄」，一直可见，点它展开探索卡。</summary>
    View BuildExploreTab(ExploreBarModel m)
    {
        var tab = new LinearLayout(context) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        tab.SetGravity(GravityFlags.CenterVertical);
        tab.Background = PgrTheme.Plate(context, Color.ParseColor("#232A32"), null, 6, Corners.BottomRight, PgrArt.GoldLine, AccentEdge.Left, shadow: true);
        tab.SetPadding(ReadDp(10), 0, ReadDp(8), 0);
        tab.SetMinimumHeight(ReadDp(32));
        tab.AddView(Light("探索中", 11.5f, Color.White, true), new LinearLayout.LayoutParams(-2, -2));
        int rest = ExploreRest(m);
        if (rest > 0) tab.AddView(RestChip(rest), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(6) });
        var chevron = new View(context) { Background = new PgrIconDrawable("chevron_down", Color.ParseColor("#C8CDD4"), density, 14), ImportantForAccessibility = ImportantForAccessibility.No };
        tab.AddView(chevron, new LinearLayout.LayoutParams(ReadDp(14), ReadDp(14)) { LeftMargin = ReadDp(6) });
        tab.ContentDescription = "探索面板已收起" + (rest > 0 ? "，还有 " + rest + " 段没听" : "") + "，点这里展开";
        tab.Click += (_, _) =>
        {
            exploreCollapsedKey = null;
            if (exploreLast is { } last && last.Model.MenuKey == m.MenuKey) ShowExplorePanel(last.Model, last.Choose);
        };
        return tab;
    }

    TextView RestChip(int rest)
    {
        var chip = Light("剩 " + rest, 10, PgrArt.GoldLine, true);
        chip.Background = PgrTheme.Plate(context, PgrTheme.A(PgrTheme.Brass, 46), PgrTheme.A(PgrArt.GoldLine, 178), 0, Corners.None);
        chip.SetPadding(ReadDp(6), ReadDp(1), ReadDp(6), ReadDp(1));
        return chip;
    }

    // ---------------- 09 ----------------

    View BuildSelectedBar(string message, string? detail, string eyebrow, string doneCaption)
    {
        var shell = FusedShell(150, false, out var left, out var right);
        var ok = new LinearLayout(context) { Orientation = Orientation.Horizontal }; ok.SetGravity(GravityFlags.CenterVertical);
        var glyph = new View(context) { Background = new PgrIconDrawable("check", PgrArt.GoldLine, density, 18) };
        ok.AddView(glyph, new LinearLayout.LayoutParams(ReadDp(18), ReadDp(18)) { RightMargin = ReadDp(6) });
        ok.AddView(PgrUi.En(context, "AUTO", 10, PgrArt.GoldLine));
        left.AddView(ok);
        left.AddView(Light("已选择", 15, Color.White, true), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(4) });
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical);
        var texts = new LinearLayout(context) { Orientation = Orientation.Vertical };
        texts.AddView(PgrUi.En(context, eyebrow, 9, PgrTheme.BrassText));
        texts.AddView(Light(message, 14.5f, PromptArt.Ink, true, 2), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(2) });
        if (!string.IsNullOrWhiteSpace(detail)) texts.AddView(Light(detail!, 10.5f, PgrTheme.Secondary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(2) });
        row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        // 定稿 p047b.js:61：✓ +「DONE · 已选择」+ 说明「已按默认继续」（不重复「已选择」）。
        var done = new CountdownBadge(context, doneCaption, radiusDp: 12, numberSp: 12); done.ShowDone(doneCaption);
        row.AddView(done, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(8) });
        right.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        // AUTO-HIDE · 3 秒后收起：细线随时间淡出。
        var fade = new LinearLayout(context) { Orientation = Orientation.Horizontal }; fade.SetGravity(GravityFlags.CenterVertical);
        var line = new View(context) { Background = PgrTheme.Plate(context, PgrTheme.A(PgrTheme.Brass, 160), null, 0, Corners.None) };
        fade.AddView(line, new LinearLayout.LayoutParams(0, ReadDp(2), 1));
        fade.AddView(PgrUi.En(context, "AUTO-HIDE · 3 秒后收起", 8.5f, PgrTheme.Muted), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = ReadDp(8) });
        right.AddView(fade, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(5) });
        line.PivotX = 0;
        line.Animate()!.ScaleX(0).SetDuration(3000).Start();
        shell.ContentDescription = message + (string.IsNullOrWhiteSpace(detail) ? "" : "，" + detail);
        return shell;
    }
}
