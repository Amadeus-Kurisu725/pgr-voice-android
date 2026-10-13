using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Display;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp.Platform;

public enum OverlayCommand { Pause, Replay, Previous, Next, Branch, Original, Ocr, OpenApp, History, Chapters, Hide, AutoPlay, AdvanceRegion, ConfirmPosition, ClickFollow, Feedback }

/// <summary>Owns only our controls; it never intercepts or injects touches outside its window.</summary>
public sealed partial class OverlayController : IDisposable
{
    private readonly Context context;
    private readonly IWindowManager windows;
    private readonly Handler main = new(Looper.MainLooper!);
    private float density;
    private float uiScale = 1;
    private float displayFontScale = 1;
    private int displayWidth, displayHeight;
    private readonly DisplayManager? displays;
    private readonly DisplayListener displayListener;
    private readonly ConfigurationListener configurationListener;
    private LinearLayout? root;
    private LinearLayout? panel;
    private LinearLayout? controlPanel, browsePanel, browseRows;
    private LinearLayout? browseShell;
    private TextView? browseHeading;
    private LinearLayout? compactPanel;
    private LinearLayout? compactActions;
    private FrameLayout? captureStopControl;
    private readonly CaptureCancelGestureGate captureCancelGesture = new();
    private ScrollView? browseScroll;
    private Button? browsePrevious, browseNext, browseCurrent;
    private readonly List<Button> browseTabs = new();
    private readonly List<Button> browseSideTabs = new();
    private OverlayBrowsePage? browsePage;
    private OverlayBrowseModel? browseModel;
    private int browsePageIndex;
    private int browseRenderSerial;
    private bool browseHintExpanded;
    private const int BrowsePageSize = 12;
    private Dialog? confirmationDialog;
    private ContextThemeWrapper? confirmationTheme;
    private Action? confirmationCancelled;
    private bool confirmationIsManualBranch;
    private bool confirmationIsPauseNotice;
    private bool confirmationIsAutoStart;
    private ScrollView? actionScroll;
    private LinearLayout? autoPlaybackPausedCard;
    // 0.4.7 终点等待卡：自动播放保持开启、读取游戏画面等待下一句（黄铜纸面，与游戏模式死角卡同一风格）。
    private LinearLayout? gameEndWaitCard;
    private string gameEndWaitHint = "", gameEndWaitDetail = "";
    private string autoPlaybackPausedReason = "";
    private long autoPlaybackPausedRevision;
    private TextView? statusView;
    private TextView? playbackModeView;
    private TextView? handleView;
    private TextView? currentLineView;
    private View? overlayPortrait;
    private PortraitFrameDrawable? overlayPortraitFrame;
    private string? overlayPortraitSpeaker;
    private Button? pauseButton;
    private Button? autoPlayButton;
    private Button? clickFollowButton;
    private Button? compactControlsButton;
    private Button? compactButtonsButton;
    private WindowManagerLayoutParams? parameters;
    private bool showing, expanded, disposed, captureHidden, captureKeepStopControl;
    private bool playbackPaused;
    private bool autoPlaying;
    private bool clickFollowing;
    private bool compactControlsVisible = true;
    private bool compactButtonsVisible = true;
    private bool appForeground;
    /// <summary>15-B2 landscape layout (≥ 600dp wide): bar 580×60 top centre, console 820×250 top centre, reader 500×360 right.</summary>
    private bool wideConsole;
    private LinearLayout? consoleHeader;
    private string status = "请选择章节，再开启配音";
    private string currentSpeaker = "", currentText = "";
    public event Action<OverlayCommand>? Command;
    public event Action<OverlayBrowsePage>? BrowseRequested;
    public event Action? BrowseRefreshRequested;
    public event Action<long, string>? BrowseItemSelected;
    public event Action<bool>? CompactButtonsChanged;
    public event Action<bool>? CompactControlsChanged;
    public event Action<long>? CaptureCancelRequested;
    public event Action<string>? CaptureInteraction;
    public event Action<string>? Error;
    public bool IsShowing => showing;
    public long CurrentCaptureControlGeneration => captureCancelGesture.Generation;
    public bool CompactControlsVisible => compactControlsVisible;
    public bool CompactButtonsVisible => compactButtonsVisible;
    public bool IsBrowseVisible => showing && expanded && browsePage.HasValue;
    public OverlayBrowsePage? CurrentBrowsePage => browsePage;
    public bool CanShow => Settings.CanDrawOverlays(context);

    public OverlayController(Context context)
    {
        this.context = context.ApplicationContext!;
        windows = this.context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>();
        density = this.context.Resources!.DisplayMetrics!.Density;
        displays = this.context.GetSystemService(Context.DisplayService) as DisplayManager;
        displayListener = new DisplayListener(this);
        configurationListener = new ConfigurationListener(this);
        displays?.RegisterDisplayListener(displayListener, main);
        this.context.RegisterComponentCallbacks(configurationListener);
        RefreshDisplay();
    }

    public void Show() => OnMain(() =>
    {
        if (disposed || showing) return;
        if (!CanShow) { Error?.Invoke("请先允许“显示在其他应用上层”，再开启悬浮控制。"); return; }
        try
        {
            RefreshDisplay();
            BuildView();
            ApplyCaptureVisibility();
            windows.AddView(root, parameters);
            showing = true; PromptBarEpoch++;
            ClampPosition();
        }
        catch (Exception ex) { showing = false; Error?.Invoke("无法显示悬浮控制：" + ex.Message); }
    });

    public void Hide() => OnMain(() => HideCore());

    /// <summary>
    /// 截图期间隐藏正文，按需保留纯图形取消入口；保留页、展开状态、位置与用户偏好。
    /// 恢复仅恢复原来的可见性，不主动显示原本已关闭的窗口或展开面板。
    /// </summary>
    public void SetCaptureHidden(bool hidden, bool keepStopControl = false) => OnMain(() =>
    {
        if (disposed) return;
        captureHidden = hidden;
        if (!hidden) ClearDefaultBranchWait();
        captureKeepStopControl = hidden && keepStopControl;
        if (CaptureStopVisible)
        {
            long generation = captureCancelGesture.Begin();
            CaptureInteraction?.Invoke($"generation={generation}; phase=begin; uptime={SystemClock.UptimeMillis()}");
            captureStopControl?.Invalidate();
        }
        else captureCancelGesture.End();
        if (hidden) DismissConfirmation();
        RefreshPresentation();
        ClampPosition();
    });

    private bool CaptureStopVisible => captureHidden && captureKeepStopControl;

    private void ApplyCaptureVisibility()
    {
        // Invisible 保留列表测量和滚动位置；NotTouchable 防止隐藏期间透明窗口挡住游戏。
        bool fullyHidden = captureHidden && !captureKeepStopControl;
        ApplyPromptBarCaptureVisibility();
        if (root != null) root.Visibility = fullyHidden ? ViewStates.Invisible : ViewStates.Visible;
        if (parameters != null)
        {
            if (fullyHidden) parameters.Flags |= WindowManagerFlags.NotTouchable;
            else parameters.Flags &= ~WindowManagerFlags.NotTouchable;
        }
    }

    public void ShowBrowsePage(OverlayBrowsePage page) => OnMain(() =>
    {
        if (disposed) return;
        browsePage = page;
        browseModel = null;
        browsePageIndex = 0;
        browseHintExpanded = false;
        Show();
        if (!showing) return;
        SetExpandedCore(true, refreshBrowse: false);
        // 先让会话接管有效的分支等待，再关闭旧提示；关闭回调不能抢先丢掉原跟随意图。
        BrowseRequested?.Invoke(page);
        DismissConfirmation();
        RenderBrowse();
    });

    public void ShowControlPage() => OnMain(() =>
    {
        if (disposed) return;
        DismissConfirmation();
        browsePage = null;
        browseModel = null;
        Show();
        SetExpanded(true);
        actionScroll?.ScrollTo(0, 0);
    });

    public void UpdateBrowse(OverlayBrowseModel model) => OnMain(() =>
    {
        if (disposed || browsePage != model.Page) return;
        bool changed = browseModel?.Revision != model.Revision;
        bool newSection = browseModel == null || browseModel.Title != model.Title;
        bool dialogueList = model.Page == OverlayBrowsePage.Lines || model.Items.Any(i => i.IsDialogue);
        bool changedLine = dialogueList && browseModel?.Items.FirstOrDefault(i => i.Selected)?.Title != model.Items.FirstOrDefault(i => i.Selected)?.Title;
        if (changed) DismissConfirmation();
        browseModel = model;
        if (newSection || changedLine) browsePageIndex = dialogueList ? SelectedBrowsePage(model) : 0;
        if (newSection) browseHintExpanded = false;
        RenderBrowse(newSection || changedLine);
    });

    private static int SelectedBrowsePage(OverlayBrowseModel model)
    {
        for (int i = 0; i < model.Items.Count; i++) if (model.Items[i].Selected) return i / BrowsePageSize;
        return 0;
    }

    private void RenderBrowse(bool positionContent = true, bool preferSelected = true)
    {
        if (browseRows == null || !browsePage.HasValue) return;
        int serial = ++browseRenderSerial;
        browseRows.RemoveAllViews();
        if (browseModel is not { } model)
        {
            if (browseHeading != null) browseHeading.Text = "读取内容";
            AddBrowseText("正在读取内容…", 14, PgrTheme.Foreground);
            if (browsePrevious != null) browsePrevious.Enabled = false;
            if (browseNext != null) browseNext.Enabled = false;
            if (browseCurrent != null) { browseCurrent.Text = "等待内容"; browseCurrent.Enabled = false; }
            return;
        }
        int pageCount = Math.Max(1, (model.Items.Count + BrowsePageSize - 1) / BrowsePageSize);
        browsePageIndex = Math.Clamp(browsePageIndex, 0, pageCount - 1);
        if (browseHeading != null) browseHeading.Text = model.Title;
        if (!string.IsNullOrWhiteSpace(model.Hint))
        {
            var hint = ReadingButton(browseHintExpanded ? "收起说明 ▴" : model.Page == OverlayBrowsePage.Locate ? "识别原文与当前位置 ▾" : $"{model.Items.Count} 条 · 查看说明 ▾", () =>
            { browseHintExpanded = !browseHintExpanded; RenderBrowse(false); });
            hint.Gravity = GravityFlags.Left | GravityFlags.CenterVertical;
            browseRows.AddView(hint, new LinearLayout.LayoutParams(-1, Touch()));
            if (browseHintExpanded) AddBrowseText(model.Hint, 12, PgrTheme.Secondary);
        }
        if (model.Actions != null)
        {
            var actionStrip = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
            var actions = new LinearLayout(context) { Orientation = Orientation.Horizontal };
            foreach (var item in model.Actions)
            {
                var button = ReadingButton(item.Title, () => SelectBrowseItem(model, item));
                button.ContentDescription = item.Title + "，" + item.Detail;
                actions.AddView(button, new LinearLayout.LayoutParams(-2, Touch()) { RightMargin = ReadDp(5) });
            }
            actionStrip.AddView(actions, new HorizontalScrollView.LayoutParams(-2, -2));
            browseRows.AddView(actionStrip, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = ReadDp(6) });
        }
        if (model.Items.Count == 0) AddBrowseText("当前没有可选条目，可先使用上方操作。", 14, PgrTheme.Secondary);
        View? firstItem = null, selectedItem = null;
        foreach (var item in model.Items.Skip(browsePageIndex * BrowsePageSize).Take(BrowsePageSize))
        {
            var card = AddBrowseItem(model, item);
            firstItem ??= card;
            if (item.Selected) selectedItem = card;
        }
        if (browsePrevious != null) browsePrevious.Enabled = browsePageIndex > 0;
        if (browseNext != null) browseNext.Enabled = browsePageIndex + 1 < pageCount;
        if (browseCurrent != null)
        {
            browseCurrent.Text = model.Items.Any(i => i.Selected) ? $"{browsePageIndex + 1}/{pageCount} · 当前" : $"{browsePageIndex + 1}/{pageCount}";
            browseCurrent.Enabled = model.Items.Any(i => i.Selected);
        }
        if (positionContent) ScrollBrowseAfterLayout(model.Page == OverlayBrowsePage.Lines || model.Items.Any(i => i.IsDialogue) ? (preferSelected ? selectedItem ?? firstItem : firstItem) : null, serial);
    }

    private void ScrollBrowseAfterLayout(View? target, int serial)
    {
        var scroll = browseScroll; var rows = browseRows;
        if (scroll == null || rows == null) return;
        var observer = rows.ViewTreeObserver;
        EventHandler? laidOut = null;
        bool completed = false;
        void Apply()
        {
            if (completed) return;
            completed = true;
            var liveObserver = observer?.IsAlive == true ? observer : rows.ViewTreeObserver;
            if (liveObserver?.IsAlive == true && laidOut != null) liveObserver.GlobalLayout -= laidOut;
            if (!disposed && serial == browseRenderSerial && ReferenceEquals(rows, browseRows)) scroll.ScrollTo(0, target?.Top ?? 0);
        }
        laidOut = (_, _) => Apply();
        if (observer != null) observer.GlobalLayout += laidOut;
        scroll.Post(() => { if (target == null || target.IsLaidOut) Apply(); });
    }

    private void AddBrowseText(string text, int size, Color color)
    {
        var view = new TextView(context) { Text = text, TextSize = size };
        view.SetTextColor(color);
        view.SetPadding(ReadDp(3), ReadDp(3), ReadDp(3), ReadDp(7));
        browseRows!.AddView(view, new LinearLayout.LayoutParams(-1, -2));
    }

    private View AddBrowseItem(OverlayBrowseModel model, OverlayBrowseItem item)
    {
        var card = new LinearLayout(context) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, Selected = item.Selected,
            ContentDescription = (item.Selected ? "当前项，" : "") + item.Title + "，点击核对详情" };
        card.SetPadding(ReadDp(11), ReadDp(6), ReadDp(11), ReadDp(6));
        card.Background = PgrTheme.PressableCard(context, item.Selected);
        if (model.Page == OverlayBrowsePage.Lines || item.IsDialogue)
        {
            string text = System.Text.RegularExpressions.Regex.Replace(item.Title, @"^\s*\d+\.\s*", "");
            int separator = text.IndexOf('：');
            if (separator < 0) separator = text.IndexOf(':');
            string speaker = separator >= 0 ? text[..separator] : "台词";
            string dialogue = separator >= 0 ? text[(separator + 1)..] : text;
            AddBubbleText(card, (item.Selected ? "当前 · " : "") + speaker, 11, 1, item.Selected ? PgrTheme.RedText : PgrTheme.Steel);
            AddBubbleText(card, dialogue, 15, 2, PgrTheme.Foreground);
        }
        else
        {
            AddBubbleText(card, (item.Selected ? "当前 · " : "") + item.Title, 14, 2, PgrTheme.Foreground);
            if (!string.IsNullOrWhiteSpace(item.Detail)) AddBubbleText(card, item.Detail.ReplaceLineEndings(" "), 11, 1, PgrTheme.Secondary);
        }
        card.Click += (_, _) => SelectBrowseItem(model, item);
        browseRows!.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = ReadDp(5) });
        return card;
    }

    private void AddBubbleText(LinearLayout card, string text, int size, int lines, Color color)
    {
        var view = new TextView(context) { Text = text, TextSize = size };
        view.SetTextColor(color); view.SetMaxLines(lines); view.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        card.AddView(view, new LinearLayout.LayoutParams(-1, -2));
    }

    private bool BrowseItemIsCurrent(OverlayBrowseModel model, OverlayBrowseItem item) =>
        !disposed && !captureHidden && IsBrowseVisible && browsePage == model.Page && browseModel is { } current && current.Revision == model.Revision &&
        (current.Items.Any(i => i.Id == item.Id) || current.Actions?.Any(i => i.Id == item.Id) == true);

    private void SelectBrowseItem(OverlayBrowseModel model, OverlayBrowseItem item)
    {
        if (disposed || captureHidden) return;
        if (!BrowseItemIsCurrent(model, item)) { if (browsePage is { } page) ShowBrowsePage(page); return; }
        DismissConfirmation();
        if (!item.RequiresConfirmation) { BrowseItemSelected?.Invoke(model.Revision, item.Id); return; }
        try
        {
            string message = string.IsNullOrWhiteSpace(item.ConfirmationText) ? item.Title + "\n\n" + item.Detail : item.ConfirmationText;
            ShowGameNotification(message, new[] { message }, _ =>
            {
                // Only this consumed notification may confirm a still-current frozen browse model.
                // The reading panel is intentionally collapsed, so its visibility is not a validity condition here.
                bool valid = !disposed && !captureHidden && showing && browsePage == model.Page &&
                    browseModel is { } current && ReferenceEquals(current, model) && current.Revision == model.Revision &&
                    (current.Items.Any(i => i.Id == item.Id) || current.Actions?.Any(i => i.Id == item.Id) == true);
                if (valid) BrowseItemSelected?.Invoke(model.Revision, item.Id);
                else if (browsePage is { } page) ShowBrowsePage(page);
            }, item.ConfirmationTitle, null, null, null, null, null, singleConfirmationLabel: "确认采用", previewText: item.Title);
        }
        catch (Exception ex) { DismissConfirmation(); Error?.Invoke("无法显示确认通知：" + ex.Message); }
    }

    public void ShowConfirmedBranchSelection(string text, IReadOnlyList<string> labels, Action<int> selected, Action cancelled) =>
        ShowManualBranchSelection(text, labels, selected, cancelled,
            title: "请对照游戏确认当前台词",
            instruction: "先在游戏中选择，等对白出现，再点对应卡片确认。将从这句继续自动播放。",
            buttonPrefix: "确认并继续配音：", anchorCards: true, choiceConfirmationLabel: "确认并继续配音", dialogueCards: true);

    public void ShowManualBranchSelection(string text, IReadOnlyList<string> optionLabels, Action<int> selected, Action cancelled,
        string? title = null, string? instruction = null, string? buttonPrefix = null,
        string? secondaryActionLabel = null, Action? secondaryAction = null, bool anchorCards = false,
        string? additionalActionLabel = null, Action? additionalAction = null, string? choiceConfirmationLabel = null,
        bool dialogueCards = false) => OnMain(() =>
    {
        if (disposed || captureHidden) { NotifyConfirmationCancelled(cancelled); return; }
        Show();
        if (!showing) { NotifyConfirmationCancelled(cancelled); return; }
        DismissConfirmation();
        confirmationIsManualBranch = true;
        confirmationCancelled = cancelled;
        try
        {
            ShowGameNotification(text, optionLabels, selected, title ?? "游戏中选择后，再确认同项",
                instruction ?? "先在游戏中选好，再确认相同人物、话题或选项。", secondaryActionLabel,
                secondaryAction, additionalActionLabel, additionalAction, anchorCards: anchorCards,
                choiceActionLabel: choiceConfirmationLabel ?? (anchorCards || buttonPrefix == "确认并开启跟随：" ? "确认并开启点按跟随" : null),
                dialogueCards: dialogueCards);
        }
        catch (Exception ex) { DismissConfirmation(); Error?.Invoke("无法显示分支通知：" + ex.Message); }
    });

    private void ShowGameNotification(string section, IReadOnlyList<string> labels, Action<int> selected,
        string? heading, string? instruction, string? optionActionLabel, Action? optionAction,
        string? ocrActionLabel, Action? ocrAction, bool anchorCards = false, string? singleConfirmationLabel = null, string? previewText = null,
        string? choiceActionLabel = null, bool dialogueCards = false)
    {
        // Collapse only the old reading/control surface. Do not call SetExpanded(false),
        // because that would cancel the notification callback installed by our caller.
        expanded = false;
        RefreshPresentation(); UpdateSize(); UpdateLayout(); ClampPosition();
        var themed = new ContextThemeWrapper(context, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        confirmationTheme = themed;
        var safe = ScreenArea();
        // 16-C: 820dp, top-centre slot shared with 17-A (was full safe width); dragged position remembered per rotation.
        int width = Math.Min(ReadDp(820), safe.Right - safe.Left);
        float fontScale = Math.Max(1f, context.Resources?.Configuration?.FontScale ?? 1f);
        int barHeight = ReadDp(48);
        int rowHeight = ReadDp(dialogueCards ? Math.Max(72f, 13f * fontScale * 4.1f + 10f) : Math.Max(56f, 13f * fontScale * 2.6f + 10f));
        int cardRows = singleConfirmationLabel != null ? 1 : dialogueCards ? Math.Clamp((labels.Count + 1) / 2, 1, 2) : 2;
        bool showInstruction = !string.IsNullOrWhiteSpace(instruction);
        int instructionHeight = showInstruction ? ReadDp(Math.Max(26f, 12f * fontScale * 1.6f + 8f)) : 0;
        int fullHeight = barHeight + instructionHeight + rowHeight * cardRows + ReadDp(16);
        int compactHeight = ReadDp(Math.Max(64f, 13f * fontScale * 2.6f + 12f));
        var body = new LinearLayout(themed) { Orientation = Orientation.Vertical };
        body.SetPadding(0, 0, 0, ReadDp(4));
        // 16-C fusion: cold-white archive paper card area (97%) under a dark title row (S collapsed / M expanded).
        body.Background = FusionDeco.Over(new ArchivePaperDrawable(density) { Fill = Color.Argb(248, 0xF2, 0xF3, 0xF5), TideDp = 12, EmblemWidthDp = 0, CutBottomRightDp = 14 }, new FusionDeco(density, DecoTier.M) { CutBR = 14, Paper = false });
        Dialog? dialog = null;
        bool collapsed = true;
        bool detailsShown = false;
        int page = 0;
        string expandedTitle = heading ?? "请对照游戏确认";
        string preview = previewText ?? string.Join(" / ", labels.Take(2).Select(value => dialogueCards
            ? string.Join(" ", value.ReplaceLineEndings("\n").Split('\n').Take(2))
            : value.ReplaceLineEndings(" ")));
        string compactHead = expandedTitle;
        string compactTitle = compactHead + "\n" + preview;
        bool Current() => !disposed && !captureHidden && dialog != null && ReferenceEquals(confirmationDialog, dialog);
        void ResizeNotice(int height)
        {
            var currentWindow = dialog?.Window;
            if (currentWindow == null) return;
            var bounds = ScreenArea();
            int shownHeight = Math.Min(height, bounds.Bottom - bounds.Top);
            int shownWidth = Math.Min(width, bounds.Right - bounds.Left);
            currentWindow.SetLayout(shownWidth, shownHeight);
            var position = currentWindow.Attributes!;
            position.X = Math.Clamp(position.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - shownWidth));
            position.Y = Math.Clamp(position.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - shownHeight));
            currentWindow.Attributes = position;
        }
        Button SmallButton(string label, Action action)
        {
            var button = new Button(themed) { Text = label, TextSize = 12 };
            PgrTheme.StyleOverlayButton(button, PgrTheme.OverlayTone.Glass);
            // SPEC C.2: 48dp tall; widths (≥ 72dp tools, 52dp 全文) come from each LayoutParams.
            button.SetMinHeight(ReadDp(48)); button.SetMinimumHeight(ReadDp(48)); button.SetMinWidth(0); button.SetMinimumWidth(0);
            button.SetPadding(ReadDp(4), 0, ReadDp(4), 0); button.SetMaxLines(1);
            button.Click += (_, _) => { if (Current()) action(); };
            return button;
        }
        void Confirm(int choice)
        {
            if (!Current()) return;
            DismissConfirmation(notifyCancellation: false);
            try { selected(choice); }
            catch (Exception ex) { Error?.Invoke("确认未完成：" + ex.Message); }
        }
        void RunAction(Action action)
        {
            if (!Current()) return;
            DismissConfirmation(notifyCancellation: false);
            try { action(); }
            catch (Exception ex) { Error?.Invoke("切换对话未完成：" + ex.Message); }
        }
        var toolbar = new LinearLayout(themed) { Orientation = Orientation.Horizontal };
        var dragTitle = new TextView(themed) { Text = compactTitle, TextSize = 13, Gravity = GravityFlags.CenterVertical };
        dragTitle.SetTextColor(Color.White); dragTitle.SetMaxLines(2);
        dragTitle.SetPadding(ReadDp(8), 0, ReadDp(6), 0);
        dragTitle.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        toolbar.AddView(dragTitle, new LinearLayout.LayoutParams(0, -1, 1));
        Button? collapseButton = null;
        var titleDeco = new DarkPanelDrawable(density, DecoTier.S) { CutBottomRightDp = 10, ShowEmblem = false, Watermark = false, Inscription = false };
        toolbar.Background = titleDeco;
        var instructionView = new TextView(themed) { Text = instruction ?? "", TextSize = 12, Gravity = GravityFlags.CenterVertical };
        instructionView.SetTextColor(PgrTheme.Secondary); instructionView.SetSingleLine(true);
        instructionView.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        instructionView.SetPadding(ReadDp(10), 0, ReadDp(10), 0);
        var content = new LinearLayout(themed) { Orientation = Orientation.Vertical };
        content.SetPadding(ReadDp(4), ReadDp(2), ReadDp(4), 0);
        var footer = new LinearLayout(themed) { Orientation = Orientation.Horizontal };
        var toolsScroll = new HorizontalScrollView(themed) { HorizontalScrollBarEnabled = true, FillViewport = false };
        toolsScroll.AddView(footer, new HorizontalScrollView.LayoutParams(-2, -1));
        toolbar.AddView(toolsScroll, new LinearLayout.LayoutParams(0, -1, 1));
        void RefreshExpansion()
        {
            content.Visibility = toolsScroll.Visibility = collapsed ? ViewStates.Gone : ViewStates.Visible;
            instructionView.Visibility = !collapsed && !detailsShown && showInstruction ? ViewStates.Visible : ViewStates.Gone;
            titleDeco.Tier = collapsed ? DecoTier.S : DecoTier.M; titleDeco.InvalidateSelf();
            collapseButton!.Text = collapsed ? "展开" : "收起";
            dragTitle.LayoutParameters = collapsed ? new LinearLayout.LayoutParams(0, -1, 1) : new LinearLayout.LayoutParams(ReadDp(300), -1);
            dragTitle.SetMaxLines(collapsed ? 2 : 1);
            if (collapsed) dragTitle.SetText(PgrTheme.TitleWithPreview(compactHead, preview, Color.White, PgrTheme.A(Color.White, 190)), TextView.BufferType.Spannable);
            else dragTitle.Text = detailsShown ? "核对完整内容 · 可拖动" : expandedTitle;
            dragTitle.SetTypeface(collapsed ? PgrTheme.SansMedium : PgrTheme.SansBold, collapsed ? TypefaceStyle.Normal : TypefaceStyle.Bold);
            toolbar.LayoutParameters = new LinearLayout.LayoutParams(-1, collapsed ? compactHeight - ReadDp(4) : barHeight);
            ResizeNotice(collapsed ? compactHeight : detailsShown ? Math.Min(ReadDp(280), (int)((safe.Bottom - safe.Top) * .6f)) : fullHeight);
        }
        collapseButton = SmallButton("展开", () =>
        {
            collapsed = !collapsed;
            RefreshExpansion();
        });
        toolbar.AddView(collapseButton, new LinearLayout.LayoutParams(ReadDp(Math.Max(72, 24 * fontScale + 12)), -1) { TopMargin = ReadDp(4), BottomMargin = ReadDp(4), LeftMargin = ReadDp(2) });
        toolbar.AddView(SmallButton("取消", () => DismissConfirmation()), new LinearLayout.LayoutParams(ReadDp(Math.Max(72, 24 * fontScale + 12)), -1) { TopMargin = ReadDp(4), BottomMargin = ReadDp(4), LeftMargin = ReadDp(2), RightMargin = ReadDp(4) });
        body.AddView(toolbar, new LinearLayout.LayoutParams(-1, compactHeight - ReadDp(4)));
        body.AddView(instructionView, new LinearLayout.LayoutParams(-1, Math.Max(1, instructionHeight)));
        body.AddView(content, new LinearLayout.LayoutParams(-1, 0, 1));
        void FooterButton(string label, Action action, bool enabled = true, bool primary = false)
        {
            var button = SmallButton(label, action); button.Enabled = enabled;
            if (primary) PgrTheme.StyleOverlayButton(button, PgrTheme.OverlayTone.Primary);
            footer.AddView(button, new LinearLayout.LayoutParams(ReadDp(Math.Max(72, label.Length * 12 * fontScale + 16)), -1) { TopMargin = ReadDp(4), BottomMargin = ReadDp(4), LeftMargin = ReadDp(2) });
        }
        void ShowDetail(int choice)
        {
            if (!Current()) return;
            detailsShown = true;
            content.RemoveAllViews(); footer.RemoveAllViews();
            toolsScroll.ScrollTo(0, 0);
            var scroll = new ScrollView(themed);
            var detail = new TextView(themed) { Text = labels[choice] + (labels[choice] == section ? "" : "\n\n" + section) + "\n" + instruction, TextSize = 15 };
            detail.SetTextColor(PgrTheme.Foreground); detail.SetPadding(ReadDp(10), ReadDp(5), ReadDp(8), ReadDp(5));
            detail.SetLineSpacing(ReadDp(3), 1);
            scroll.AddView(detail); content.AddView(scroll, new LinearLayout.LayoutParams(-1, -1));
            FooterButton("返回通知", () => { collapsed = true; RenderCards(); });
            FooterButton(singleConfirmationLabel ?? choiceActionLabel ?? "确认就是这句", () => Confirm(choice), primary: true);
            RefreshExpansion();
        }
        void RenderCards()
        {
            detailsShown = false;
            content.RemoveAllViews(); footer.RemoveAllViews();
            toolsScroll.ScrollTo(0, 0);
            expandedTitle = (heading ?? "请对照游戏确认") + (labels.Count > 4 ? $" · {page + 1}/{(labels.Count + 3) / 4}" : "");
            int start = page * 4;
            for (int row = 0; row < cardRows; row++)
            {
                var cardRow = new LinearLayout(themed) { Orientation = Orientation.Horizontal };
                content.AddView(cardRow, new LinearLayout.LayoutParams(-1, 0, 1));
                for (int column = 0; column < (singleConfirmationLabel != null ? 1 : 2); column++)
                {
                    int choice = start + row * 2 + column;
                    var cell = new LinearLayout(themed) { Orientation = Orientation.Horizontal };
                    cell.SetPadding(ReadDp(2), ReadDp(1), ReadDp(2), ReadDp(1));
                    cardRow.AddView(cell, new LinearLayout.LayoutParams(0, -1, 1));
                    if (choice >= labels.Count) continue;
                    if (dialogueCards)
                    {
                        // 选项和实际对白各占自己的空间，长选项不能挤掉要核对的对白。
                        var parts = labels[choice].Split('\n', 3);
                        var dialogueCard = new LinearLayout(themed)
                        {
                            Orientation = Orientation.Vertical,
                            Clickable = true, LongClickable = true,
                            ContentDescription = labels[choice] + "。确认并继续配音。长按或点全文核对完整台词。"
                        };
                        dialogueCard.SetGravity(GravityFlags.CenterVertical);
                        dialogueCard.Background = new Android.Graphics.Drawables.RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Red, 60)),
                            PgrTheme.Plate(themed, Color.White, PgrTheme.Red, PgrTheme.CutSmall, Corners.BottomRight, PgrTheme.Red, AccentEdge.Left),
                            PgrTheme.Plate(themed, Color.White, null, PgrTheme.CutSmall, Corners.BottomRight));
                        dialogueCard.SetPadding(ReadDp(6), ReadDp(3), ReadDp(6), ReadDp(3));
                        var optionText = new TextView(themed) { Text = parts[0], TextSize = 11 };
                        optionText.SetTextColor(PgrTheme.Steel); optionText.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); optionText.SetMaxLines(1);
                        optionText.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                        var dialogueText = new TextView(themed) { Text = parts.Length > 1 ? parts[1] : parts[0], TextSize = 13 };
                        dialogueText.SetTextColor(PgrTheme.Foreground); dialogueText.SetMaxLines(2);
                        dialogueText.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                        dialogueCard.AddView(optionText, new LinearLayout.LayoutParams(-1, -2));
                        dialogueCard.AddView(dialogueText, new LinearLayout.LayoutParams(-1, -2));
                        if (choiceActionLabel != null) dialogueCard.AddView(ActionHint(choiceActionLabel), new LinearLayout.LayoutParams(-1, -2));
                        dialogueCard.Click += (_, _) => Confirm(choice);
                        dialogueCard.LongClick += (_, e) => { if (Current()) ShowDetail(choice); e.Handled = true; };
                        cell.AddView(dialogueCard, new LinearLayout.LayoutParams(0, -1, 1));
                        cell.AddView(SmallButton("全文", () => ShowDetail(choice)), new LinearLayout.LayoutParams(ReadDp(52), -1) { LeftMargin = ReadDp(2) });
                        continue;
                    }
                    var card = SmallButton(labels[choice], () => { if (singleConfirmationLabel != null) ShowDetail(choice); else Confirm(choice); });
                    card.TextSize = 13; card.SetMaxLines(2); card.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                    card.SetPadding(ReadDp(6), 0, ReadDp(6), 0);
                    card.SetIncludeFontPadding(false);
                    card.Gravity = GravityFlags.CenterVertical | GravityFlags.Left;
                    card.ContentDescription = labels[choice] + (choiceActionLabel == null ? "" : "。" + choiceActionLabel) + "。长按或点全文核对完整台词。";
                    card.LongClick += (_, e) => { if (Current()) ShowDetail(choice); e.Handled = true; };
                    card.Background = new Android.Graphics.Drawables.RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Red, 60)),
                        PgrTheme.Plate(themed, Color.White, PgrTheme.Brass, PgrTheme.CutSmall, Corners.BottomRight),
                        PgrTheme.Plate(themed, Color.White, null, PgrTheme.CutSmall, Corners.BottomRight));
                    card.SetTextColor(PgrTheme.Foreground);
                    if (choiceActionLabel != null)
                    {
                        // 16-C: choiceActionLabel shown at the card's lower right (source text, previously only in the accessibility description).
                        var holder = new FrameLayout(themed);
                        holder.AddView(card, new FrameLayout.LayoutParams(-1, -1));
                        var hint = ActionHint(choiceActionLabel); hint.Clickable = false; hint.Focusable = false;
                        holder.AddView(hint, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.End) { RightMargin = ReadDp(8), BottomMargin = ReadDp(3) });
                        cell.AddView(holder, new LinearLayout.LayoutParams(0, -1, 1));
                    }
                    else cell.AddView(card, new LinearLayout.LayoutParams(0, -1, 1));
                    cell.AddView(SmallButton("全文", () => ShowDetail(choice)), new LinearLayout.LayoutParams(ReadDp(52), -1) { LeftMargin = ReadDp(2) });
                }
            }
            if (singleConfirmationLabel != null) FooterButton(singleConfirmationLabel, () => Confirm(0), primary: true);
            if (optionAction != null) FooterButton(optionActionLabel == "按选项核对" ? "按选项" : optionActionLabel ?? "更多", () => RunAction(optionAction));
            if (ocrAction != null) FooterButton(anchorCards ? "识别下一句" : ocrActionLabel ?? "重新定位", () => RunAction(ocrAction));
            if (labels.Count > 4)
            {
                FooterButton("上一页", () => { page--; RenderCards(); }, page > 0);
                FooterButton("下一页", () => { page++; RenderCards(); }, start + 4 < labels.Count);
            }
            RefreshExpansion();
        }
        RenderCards();
        dialog = new Dialog(themed);
        dialog.RequestWindowFeature((int)WindowFeatures.NoTitle);
        dialog.SetContentView(body);
        confirmationDialog = dialog;
        var window = dialog.Window!;
        window.SetType(WindowManagerTypes.ApplicationOverlay);
        // ScreenArea already excludes system bars and cutouts. Use screen coordinates
        // throughout so a floating Dialog parent does not apply those insets twice.
        window.AddFlags(WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutInScreen);
        var screenCoordinates = window.Attributes!;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            screenCoordinates.FitInsetsTypes = 0;
            screenCoordinates.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Always;
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(28))
            screenCoordinates.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Never;
        window.Attributes = screenCoordinates;
        window.ClearFlags(WindowManagerFlags.DimBehind);
        dialog.SetCanceledOnTouchOutside(false);
        dialog.DismissEvent += (_, _) =>
        {
            if (!ReferenceEquals(confirmationDialog, dialog)) return;
            var onCancelled = confirmationCancelled; confirmationCancelled = null;
            var theme = confirmationTheme; confirmationTheme = null;
            confirmationDialog = null; confirmationIsManualBranch = false; confirmationIsPauseNotice = false; confirmationIsAutoStart = false; dialog.Dispose(); theme?.Dispose();
            NotifyConfirmationCancelled(onCancelled);
        };
        dialog.Show();
        window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Color.Transparent));
        window.DecorView.SetPadding(0, 0, 0, 0);
        window.DecorView.Elevation = 0;
        window.SetLayout(width, compactHeight);
        window.SetGravity(GravityFlags.Top | GravityFlags.Left);
        var attributes = window.Attributes!;
        var place = NoticePlacement(width, compactHeight);
        attributes.X = place.X;
        attributes.Y = place.Y; window.Attributes = attributes;
        float downX = 0, downY = 0; int originX = 0, originY = 0; bool dragged = false;
        dragTitle.Touch += (_, e) =>
        {
            if (e.Event == null || !Current()) return;
            if (e.Event.ActionMasked == MotionEventActions.Down)
            { downX = e.Event.RawX; downY = e.Event.RawY; originX = window.Attributes!.X; originY = window.Attributes!.Y; dragged = false; }
            else if (e.Event.ActionMasked == MotionEventActions.Move)
            {
                if (Math.Abs(e.Event.RawX - downX) + Math.Abs(e.Event.RawY - downY) > ReadDp(6)) dragged = true;
                if (!dragged) { e.Handled = true; return; }
                var bounds = ScreenArea(); var moved = window.Attributes!;
                moved.X = Math.Clamp(originX + (int)(e.Event.RawX - downX), bounds.Left, Math.Max(bounds.Left, bounds.Right - window.DecorView.Width));
                moved.Y = Math.Clamp(originY + (int)(e.Event.RawY - downY), bounds.Top, Math.Max(bounds.Top, bounds.Bottom - window.DecorView.Height));
                window.Attributes = moved;
            }
            else if (e.Event.ActionMasked == MotionEventActions.Up && !dragged && collapsed)
            { collapsed = false; RefreshExpansion(); }
            else if (e.Event.ActionMasked == MotionEventActions.Up && dragged)
                SavePlacement("notice", window.Attributes!.X, window.Attributes!.Y, window.DecorView.Width, window.DecorView.Height);
            e.Handled = true;
        };
    }

    public void ClearManualBranchSelection() => OnMain(() =>
    {
        if (confirmationIsManualBranch) DismissConfirmation(notifyCancellation: false);
    });

    public void ClearAutoStartConfirmation() => OnMain(() =>
    {
        if (confirmationIsAutoStart) DismissConfirmation(notifyCancellation: false);
    });

    public void ShowAutoPlaybackStart(string text, Action confirmed, Action locate, Action cancelled, bool isCommonReturn = false) => OnMain(() =>
    {
        if (disposed || captureHidden) { NotifyConfirmationCancelled(cancelled); return; }
        Show();
        if (!showing) { NotifyConfirmationCancelled(cancelled); return; }
        DismissConfirmation();
        confirmationCancelled = cancelled;
        try
        {
            ShowGameNotification(text, new[] { text }, _ => confirmed(),
                isCommonReturn ? "已到共同线，请核对当前句" : "核对自动播放起点",
                "先核对游戏当前台词；确认后从这句配音，读完再自动点击。缺音或未知连接仍暂停。",
                null, null, "重新 OCR 定位", locate,
                singleConfirmationLabel: isCommonReturn ? "确认继续" : "从这句开始",
                previewText: text.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim());
            confirmationIsAutoStart = true;
        }
        catch (Exception ex) { DismissConfirmation(); Error?.Invoke("无法显示起点通知：" + ex.Message); }
    });

    private void DismissConfirmation(bool notifyCancellation = true)
    {
        confirmationIsManualBranch = false;
        confirmationIsPauseNotice = false;
        confirmationIsAutoStart = false;
        var dialog = confirmationDialog; confirmationDialog = null;
        var onCancelled = confirmationCancelled; confirmationCancelled = null;
        var theme = confirmationTheme; confirmationTheme = null;
        if (dialog != null)
        {
            try { dialog.Dismiss(); } catch { }
            dialog.Dispose();
        }
        theme?.Dispose();
        if (notifyCancellation) NotifyConfirmationCancelled(onCancelled);
    }

    private void NotifyConfirmationCancelled(Action? cancelled)
    {
        try { cancelled?.Invoke(); }
        catch (Exception ex) { Error?.Invoke("取消起点核对未完成：" + ex.Message); }
    }

    public void UpdateCurrentLine(string speaker, string text) => OnMain(() =>
    {
        if (disposed) return;
        string nextSpeaker = (speaker ?? "").Trim(), nextText = (text ?? "").Trim();
        if (currentSpeaker == nextSpeaker && currentText == nextText) return;
        currentSpeaker = nextSpeaker;
        currentText = nextText;
        bool wasFull = slimLineFull;
        CollapseSlimLine();
        RefreshCurrentLine();
        if (wasFull && showing) ClampPosition();
    });

    private void RefreshCurrentLine()
    {
        if (currentLineView == null) return;
        string text = string.IsNullOrWhiteSpace(currentText) ? "尚未选择当前台词"
            : (string.IsNullOrWhiteSpace(currentSpeaker) ? "" : currentSpeaker + "：") + currentText;
        // Two fixed lines keep a long sentence from growing the window or hiding its buttons.
        currentLineView.Text = text.ReplaceLineEndings(" ");
        StyleCurrentLine(currentLineView, text);
        string portraitName = string.IsNullOrWhiteSpace(currentSpeaker) ? "旁白" : currentSpeaker;
        if (overlayPortrait != null && overlayPortraitFrame != null && overlayPortraitSpeaker != portraitName)
        {
            var frame = overlayPortraitFrame; overlayPortraitSpeaker = portraitName; frame.Name = portraitName; frame.Image = null;
            PortraitStore.Request(overlayPortrait, portraitName, standee: false, maxPx: Dp(64), image => { if (!disposed && frame.Name == portraitName) frame.Image = image; });
        }
    }

    public void UpdateClickFollow(bool active) => OnMain(() =>
    {
        if (disposed) return;
        if (clickFollowing == active) return;
        clickFollowing = active;
        RefreshPlaybackMode();
        if (clickFollowButton != null)
        {
            clickFollowButton.Text = active ? "停止点按" : "点按跟随";
            clickFollowButton.ContentDescription = active ? "停止点按跟随" : "开启点按跟随";
            StyleControl(clickFollowButton, primary: active);
            clickFollowButton.SetPadding(Dp(3), Dp(7), Dp(3), Dp(7));
            clickFollowButton.SetMinHeight(Touch()); clickFollowButton.SetMinimumHeight(Touch());
            SetActionIcon(clickFollowButton, active ? "pause" : "locate");
        }
        RefreshPresentation();
    });

    /// <summary>Preference setter; persistence is owned by AppSession and only user changes emit an event.</summary>
    public void SetCompactControlsVisible(bool value) => OnMain(() =>
    {
        if (disposed) return;
        compactControlsVisible = value;
        RefreshPresentation();
        ClampPosition();
    });

    public void SetCompactButtonsVisible(bool value) => OnMain(() =>
    {
        if (disposed) return;
        compactButtonsVisible = value;
        RefreshPresentation();
        ClampPosition();
    });

    /// <summary>Temporarily use only the ball over our pages, preserving the user's game preference.</summary>
    public void UpdateAppForeground(bool foreground) => OnMain(() =>
    {
        if (disposed || appForeground == foreground) return;
        appForeground = foreground;
        if (foreground) DismissConfirmation();
        RefreshPresentation();
        ClampPosition();
    });

    private void RefreshPlaybackMode()
    {
        if (playbackModeView != null)
        {
            playbackModeView.Text = clickFollowing ? "当前模式 · 点按跟随" : autoPlaying
                ? "当前模式 · 自动播放" : "当前模式 · 手动播放";
            playbackModeView.SetTextColor(clickFollowing || autoPlaying ? PgrTheme.RedText : PgrTheme.Steel);
            playbackModeView.ContentDescription = playbackModeView.Text;
        }
    }

    public void UpdateStatus(string text, bool paused = false, bool autoPlaying = false) => OnMain(() =>
    {
        if (disposed) return;
        status = text;
        bool pauseChanged = playbackPaused != paused;
        bool autoChanged = this.autoPlaying != autoPlaying;
        playbackPaused = paused;
        this.autoPlaying = autoPlaying;
        // A normal status refresh must not erase the reason the automatic run stopped.
        if (autoPlaying && autoPlaybackPausedReason.Length > 0) ClearAutoPlaybackPaused();
        RefreshPlaybackMode();
        if (statusView != null)
        {
            StyleStatus(statusView, text, paused);
        }
        if (pauseButton != null)
        {
            pauseButton.Text = paused ? "继续" : "暂停";
            pauseButton.ContentDescription = pauseButton.Text;
            if (pauseChanged) SetActionIcon(pauseButton, paused ? "play" : "pause");
        }
        if (autoPlayButton != null)
        {
            autoPlayButton.Text = autoPlaying ? "停止自动" : "自动播放";
            autoPlayButton.ContentDescription = autoPlaying ? "停止自动播放" : "开始自动播放";
            if (autoChanged)
            {
                StyleControl(autoPlayButton, primary: autoPlaying);
                autoPlayButton.SetPadding(Dp(3), Dp(7), Dp(3), Dp(7));
                autoPlayButton.SetMinHeight(Touch());
                autoPlayButton.SetMinimumHeight(Touch());
                SetActionIcon(autoPlayButton, autoPlaying ? "pause" : "play");
            }
        }
        if (autoChanged) RefreshPresentation();
    });

    /// <summary>Called on the session's main looper; use the actual screen rectangle, not layout offsets.</summary>
    public bool ContainsPoint(float x, float y)
    {
        if (captureHidden && !captureKeepStopControl || !showing || disposed || root == null || !root.IsAttachedToWindow || root.Visibility != ViewStates.Visible)
            return false;
        var location = new int[2];
        root.GetLocationOnScreen(location);
        return x >= location[0] && y >= location[1] &&
            x < location[0] + root.Width && y < location[1] + root.Height;
    }

    /// <summary>分支选项的整个触碰范围不得挡住现有悬浮控制。</summary>
    public bool IntersectsRectangle(float left, float top, float right, float bottom)
    {
        if (!float.IsFinite(left) || !float.IsFinite(top) || !float.IsFinite(right) || !float.IsFinite(bottom) ||
            right <= left || bottom <= top) return true;
        if (captureHidden && !captureKeepStopControl || !showing || disposed || root == null ||
            !root.IsAttachedToWindow || root.Visibility != ViewStates.Visible) return false;
        var location = new int[2];
        root.GetLocationOnScreen(location);
        return left < location[0] + root.Width && right > location[0] &&
            top < location[1] + root.Height && bottom > location[1];
    }

    public void ShowNotice(string message) => OnMain(() =>
    {
        if (disposed || captureHidden || string.IsNullOrWhiteSpace(message)) return;
        status = message;
        Show();
        if (!showing) return;
        DismissConfirmation();
        UpdateStatus(message, playbackPaused, autoPlaying);
        try
        {
            ShowGameNotification(message, new[] { message }, _ => { }, "配音提示", null,
                null, null, null, null, singleConfirmationLabel: "知道了");
        }
        catch (Exception ex) { DismissConfirmation(); Error?.Invoke("无法显示提示通知：" + ex.Message); }
    });

    public void ShowAutoPlaybackPaused(string reason) => OnMain(() =>
    {
        if (disposed || captureHidden || string.IsNullOrWhiteSpace(reason)) return;
        autoPlaybackPausedReason = reason.Trim();
        long revision = ++autoPlaybackPausedRevision;
        Show();
        if (!showing) return;
        DismissConfirmation();
        RefreshAutoPlaybackPaused();
        try
        {
            confirmationCancelled = () => { if (revision == autoPlaybackPausedRevision) ClearAutoPlaybackPaused(); };
            ShowGameNotification(autoPlaybackPausedReason, new[] { autoPlaybackPausedReason }, _ =>
                { if (revision == autoPlaybackPausedRevision) ClearAutoPlaybackPaused(); },
                "自动播放已暂停", "关闭提示不会恢复自动播放。", null, null, null, null,
                singleConfirmationLabel: "知道了");
            confirmationIsPauseNotice = true;
        }
        catch (Exception ex) { DismissConfirmation(); Error?.Invoke("无法显示暂停通知：" + ex.Message); }
    });

    /// <summary>0.4.7 终点等待：非模态卡片，不遮挡游戏点击；自动播放仍在运行。</summary>
    public void ShowGameEndWaiting(string hint, string detail) => OnMain(() =>
    {
        if (disposed) return;
        gameEndWaitHint = hint.Trim(); gameEndWaitDetail = detail.Trim();
        RefreshGameEndWaiting();
    });

    public void ClearGameEndWaiting() => OnMain(() =>
    {
        if (disposed) return;
        gameEndWaitHint = gameEndWaitDetail = "";
        RefreshGameEndWaiting();
    });

    private void RefreshGameEndWaiting()
    {
        if (gameEndWaitCard is not { } card) return;
        card.RemoveAllViews();
        bool visible = gameEndWaitHint.Length > 0;
        card.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
        if (!visible) return;
        var eyebrowRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        eyebrowRow.SetGravity(GravityFlags.CenterVertical);
        var pulse = new View(context);
        pulse.Background = PgrTheme.Plate(context, PgrArt.GoldLine, null, 0, Corners.None);
        eyebrowRow.AddView(pulse, new LinearLayout.LayoutParams(Dp(6), Dp(6)) { RightMargin = Dp(6) });
        var blink = new global::Android.Views.Animations.AlphaAnimation(1f, .25f) { Duration = 900, RepeatMode = global::Android.Views.Animations.RepeatMode.Reverse, RepeatCount = global::Android.Views.Animations.Animation.Infinite };
        pulse.StartAnimation(blink);
        eyebrowRow.AddView(PgrUi.En(context, PgrVoice.Following.GameEndWaitPolicy.Eyebrow, 9.5f, PgrTheme.BrassText), new LinearLayout.LayoutParams(0, -2, 1));
        card.AddView(eyebrowRow, new LinearLayout.LayoutParams(-1, -2));
        var heading = PgrTheme.Apply(new TextView(context) { Text = gameEndWaitHint }, PgrTheme.Role.Heading);
        heading.TextSize = FontSize(15);
        card.AddView(heading, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        var detail = new TextView(context) { Text = gameEndWaitDetail, TextSize = FontSize(12) };
        detail.SetTextColor(PgrTheme.Secondary);
        detail.SetLineSpacing(Dp(2), 1);
        card.AddView(detail, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
    }

    public void ClearAutoPlaybackPaused() => OnMain(() =>
    {
        if (disposed) return;
        if (confirmationIsPauseNotice) DismissConfirmation(notifyCancellation: false);
        autoPlaybackPausedReason = "";
        autoPlaybackPausedRevision++;
        RefreshAutoPlaybackPaused();
    });

    private void RefreshAutoPlaybackPaused()
    {
        if (autoPlaybackPausedCard is not { } card) return;
        card.RemoveAllViews();
        bool visible = autoPlaybackPausedReason.Length > 0;
        card.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
        if (!visible) return;

        long revision = autoPlaybackPausedRevision;
        var header = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        var title = new TextView(context) { Text = "自动播放已暂停", TextSize = FontSize(15) };
        title.SetTextColor(PgrTheme.RedText);
        title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        header.AddView(title, new LinearLayout.LayoutParams(0, -2, 1));
        var dismiss = new Button(context)
        {
            Text = "知道了",
            TextSize = FontSize(12),
            ContentDescription = "知道了，仅关闭暂停提示，不恢复自动播放"
        };
        StyleControl(dismiss, primary: true);
        dismiss.SetPadding(Dp(5), Dp(5), Dp(5), Dp(5));
        dismiss.SetMinHeight(Touch()); dismiss.SetMinimumHeight(Touch());
        dismiss.Click += (_, _) =>
        {
            if (disposed || revision != autoPlaybackPausedRevision || !ReferenceEquals(card, autoPlaybackPausedCard)) return;
            ClearAutoPlaybackPaused();
        };
        header.AddView(dismiss, new LinearLayout.LayoutParams(ReadDp(72), Touch()) { LeftMargin = Dp(6) });
        card.AddView(header, new LinearLayout.LayoutParams(-1, -2));
        var reason = new TextView(context) { Text = autoPlaybackPausedReason, TextSize = FontSize(13) };
        reason.SetTextColor(PgrTheme.Foreground);
        reason.SetPadding(0, Dp(6), 0, Dp(6));
        reason.SetLineSpacing(Dp(2), 1);
        // 关闭入口先于完整原因，横屏也无需滚过长段正文才能关闭。
        card.AddView(reason, new LinearLayout.LayoutParams(-1, -2));
    }

    public void SetExpanded(bool value) => OnMain(() => SetExpandedCore(value));

    private void SetExpandedCore(bool value, bool refreshBrowse = true)
    {
        if (disposed) return;
        bool revealBrowse = refreshBrowse && value && !expanded && browsePage.HasValue && showing;
        if (!value) DismissConfirmation();
        CollapseSlimLine();
        expanded = value;
        // 同一主线程内先取得当前章节的模型，再让收起的阅读页重新可见。
        if (revealBrowse) BrowseRefreshRequested?.Invoke();
        RefreshPresentation();
        UpdateSize();
        UpdateLayout();
        ClampPosition();
        if (value && autoPlaying) actionScroll?.Post(() => actionScroll?.ScrollTo(0, 0));
    }

    private void RefreshPresentation()
    {
        ApplyCaptureVisibility();
        if (captureStopControl != null) captureStopControl.Visibility = CaptureStopVisible ? ViewStates.Visible : ViewStates.Gone;
        if (CaptureStopVisible)
        {
            // 定位期间保持图形与尺寸不变，状态通知不能把本应用的台词带回截图。
            if (handleView != null) handleView.Visibility = ViewStates.Gone;
            if (compactPanel != null) compactPanel.Visibility = ViewStates.Gone;
            if (panel != null) panel.Visibility = ViewStates.Gone;
            if (browseShell != null) browseShell.Visibility = ViewStates.Gone;
            return;
        }
        bool compact = !expanded && compactControlsVisible && !appForeground;
        bool reading = expanded && browsePage.HasValue;
        if (panel != null) panel.Visibility = expanded && !reading ? ViewStates.Visible : ViewStates.Gone;
        if (browseShell != null) browseShell.Visibility = reading ? ViewStates.Visible : ViewStates.Gone;
        for (int i = 0; i < browseTabs.Count; i++)
        {
            bool selected = i == (browsePage.HasValue ? (int)browsePage.Value + 1 : 0);
            StyleControl(browseTabs[i], selected: selected, quiet: !selected);
            if (wideConsole && !selected) browseTabs[i].SetTextColor(PgrTheme.A(Color.White, 215)); // on the dark title row
            browseTabs[i].TextSize = FontSize(11);
            browseTabs[i].SetPadding(Dp(1), Dp(2), Dp(1), Dp(2));
            browseTabs[i].SetMinHeight(Touch()); browseTabs[i].SetMinimumHeight(Touch());
        }
        for (int i = 0; i < browseSideTabs.Count; i++)
        {
            bool selected = i == (browsePage.HasValue ? (int)browsePage.Value + 1 : 0);
            StyleControl(browseSideTabs[i], selected: selected, quiet: !selected);
            browseSideTabs[i].TextSize = 12;
            browseSideTabs[i].SetSingleLine(true);
            browseSideTabs[i].SetPadding(ReadDp(1), ReadDp(2), ReadDp(1), ReadDp(2));
            browseSideTabs[i].SetMinHeight(Touch()); browseSideTabs[i].SetMinimumHeight(Touch());
        }
        if (consoleHeader != null) consoleHeader.Visibility = expanded && !reading ? ViewStates.Visible : ViewStates.Gone;
        if (root != null)
        {
            // 15-B2 fusion: dark frame (bar S / console M / reader L) around cold-white paper surfaces; the ball keeps its own skin.
            bool framed = wideConsole && (expanded || compact);
            root.Orientation = compact && wideConsole ? Orientation.Horizontal : Orientation.Vertical;
            // 15-B2 ①: the collapsed ball is light paper + dark diagonal top plate + brass orbit (legible on dark scenes).
            bool ball = !expanded && !compact && !CaptureStopVisible;
            // 方案 1 横屏台词条：外框上 3dp（由纸面上方透明 3dp 露出）+ 纸面 38dp + 底部细线 4dp + 1dp = 46dp。
            bool slimBar = compact && wideConsole;
            root.Background = framed
                ? new DarkPanelDrawable(density, reading ? DecoTier.L : expanded ? DecoTier.M : DecoTier.S)
                    { CutBottomRightDp = 14, Inscription = false, Watermark = reading, ShowEmblem = false, Mark = reading ? "READ" : null, TideDp = expanded ? 18 : 8, SlimTideDp = slimBar ? 4 : 0 }
                : ball ? new FloatingBallDrawable(density * uiScale)
                : PgrTheme.Plate(context, PgrTheme.Glass, PgrTheme.GlassLine, PgrTheme.CutMedium, Corners.Diagonal,
                    brackets: Color.Argb(150, PgrTheme.Cyan.R, PgrTheme.Cyan.G, PgrTheme.Cyan.B), sheen: PgrTheme.SheenLight);
            int pad = framed ? ReadDp(4) : ball ? 0 : Dp(2);
            root.SetPadding(pad, slimBar ? 0 : pad, pad, slimBar ? ReadDp(2) : framed ? ReadDp(10) : pad);
        }
        if (compactPanel != null) compactPanel.Visibility = compact ? ViewStates.Visible : ViewStates.Gone;
        if (compactActions != null) compactActions.Visibility = compactButtonsVisible ? ViewStates.Visible : ViewStates.Gone;
        if (handleView != null)
        {
            handleView.Visibility = reading || expanded && wideConsole ? ViewStates.Gone : ViewStates.Visible;
            handleView.Text = expanded ? "配音控制" : compact
                ? (clickFollowing ? "点按跟随 · 点此展开" : autoPlaying ? "自动播放 · 点此展开" : "配音 · 点此展开") : "配";
            bool ballHandle = !expanded && !compact;
            bool sideHandle = compact && wideConsole;
            if (sideHandle)
            {
                // 左块一行：「自动播放 / 点按跟随 · 点此展开」放不下时缩小字号，不换行。
                float sp = context.Resources!.DisplayMetrics!.ScaledDensity;
                handleView.SetAutoSizeTextTypeUniformWithConfiguration((int)(7 * sp), Math.Max((int)(7 * sp) + 1, (int)(FontSize(10) * sp)), 1, (int)global::Android.Util.ComplexUnitType.Px);
            }
            else
            {
                handleView.SetAutoSizeTextTypeWithDefaults(global::Android.Widget.AutoSizeTextType.None);
                handleView.TextSize = FontSize(compact ? 10 : ballHandle ? 21 : 15);
            }
            handleView.Gravity = (expanded || compact ? GravityFlags.Start : GravityFlags.CenterHorizontal) | GravityFlags.CenterVertical;
            handleView.SetCompoundDrawablesWithIntrinsicBounds(expanded || compact && !sideHandle ? Icon("wave", compact ? 12 : 18) : null, null,
                expanded || compact ? Icon(expanded ? "chevron_up" : "chevron_down", compact ? 12 : 17, PgrTheme.Secondary) : null, null);
            handleView.CompoundDrawablePadding = Dp(compact ? 4 : 9);
            handleView.SetPadding(Dp(expanded ? 13 : compact ? 6 : 0), ballHandle ? Dp(9) : 0, Dp(expanded ? 12 : compact ? 5 : 0), 0);
            handleView.Background = compact && wideConsole || ballHandle ? null : HandleBackground(expanded, compact);
            handleView.SetTextColor(compact && wideConsole ? Color.White : PgrTheme.Foreground);
            handleView.SetMaxLines(1);
            handleView.ContentDescription = expanded ? "配音控制，点击收起，按住拖动" : compact
                ? "台词悬浮条，点击展开完整控制，按住拖动" : "战双配音悬浮球，点击展开，按住拖动";
            if (handleView.LayoutParameters is LinearLayout.LayoutParams handleLayout)
            {
                bool side = compact && wideConsole;
                int height = side ? -1 : Dp(compact ? 26 : expanded ? 54 : 58), width = side ? ReadDp(104) : -1;
                if (handleLayout.Height != height || handleLayout.Width != width) { handleLayout.Height = height; handleLayout.Width = width; handleView.LayoutParameters = handleLayout; }
            }
            if (compactPanel?.LayoutParameters is LinearLayout.LayoutParams compactLayout)
            {
                bool side = compact && wideConsole;
                int width = side ? 0 : -1, height = side ? -1 : -2; float weight = side ? 1 : 0;
                if (compactLayout.Width != width || compactLayout.Height != height || compactLayout.Weight != weight)
                { compactLayout.Width = width; compactLayout.Height = height; compactLayout.Weight = weight; compactPanel.LayoutParameters = compactLayout; }
            }
        }
        if (compactControlsButton != null)
        {
            compactControlsButton.Text = compactControlsVisible ? "仅保留悬浮球" : "显示台词和小按钮";
            compactControlsButton.ContentDescription = compactControlsButton.Text;
        }
        if (compactButtonsButton != null)
        {
            compactButtonsButton.Text = compactButtonsVisible ? "小按钮：开" : "小按钮：关";
            compactButtonsButton.ContentDescription = "上一句、当前句、下一句小按钮，" + (compactButtonsVisible ? "已显示，点此隐藏" : "已隐藏，点此显示");
        }
    }

    private void BuildView()
    {
        if (root != null) return;
        slimLineFull = false;
        wideConsole = displayWidth > displayHeight && displayWidth / Math.Max(.1f, density) >= 600;
        root = new LinearLayout(context) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
        // Translucent HUD glass: the game remains readable behind the controls.
        root.Background = PgrTheme.Plate(context, PgrTheme.Glass, PgrTheme.GlassLine, PgrTheme.CutMedium, Corners.Diagonal,
            brackets: Color.Argb(150, PgrTheme.Cyan.R, PgrTheme.Cyan.G, PgrTheme.Cyan.B), sheen: PgrTheme.SheenLight);
        root.Elevation = Dp(4);
        root.LayoutChange += (_, _) =>
        {
            if (!showing || parameters == null) return;
            int oldWidth = parameters.Width, oldHeight = parameters.Height;
            UpdateSize();
            if (oldWidth != parameters.Width || oldHeight != parameters.Height) ClampPosition();
        };
        var captureStop = new CaptureStopFrame(context, this)
        {
            Clickable = true,
            Focusable = true,
            ContentDescription = "正在定位，点击取消自动播放",
            Visibility = ViewStates.Gone,
            Background = PgrTheme.Surface(context, PgrTheme.Selection, PgrTheme.Red)
        };
        captureStopControl = captureStop;
        captureStop.SetMinimumWidth(ReadDp(48));
        captureStop.SetMinimumHeight(ReadDp(48));
        captureStop.SetOnTouchListener(new CaptureCancelTouchListener(this));
        var stopSquare = new View(context);
        stopSquare.SetBackgroundColor(PgrTheme.Red);
        captureStop.AddView(stopSquare, new FrameLayout.LayoutParams(ReadDp(16), ReadDp(16), GravityFlags.Center));
        captureStop.Click += (_, _) =>
        {
            // 真实触摸直接由下面的门控接受；旧 PerformClick 或无触摸的直接 Click 只能记诊断。
            CaptureInteraction?.Invoke($"generation={CurrentCaptureControlGeneration}; action=Click; DownTime=none; rejected=direct-click-without-touch");
        };
        root.AddView(captureStop, new LinearLayout.LayoutParams(-1, ReadDp(52)));
        var handle = new TextView(context)
        {
            Text = "配",
            TextSize = FontSize(15),
            Gravity = GravityFlags.Center,
            ContentDescription = "战双配音悬浮球，点击展开，按住拖动"
        };
        handleView = handle;
        handle.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        handle.SetTextColor(PgrTheme.Foreground);
        handle.CompoundDrawablePadding = Dp(9);
        handle.Background = HandleBackground(false, false);
        handle.SetOnTouchListener(new DragListener(this));
        root.AddView(handle, new LinearLayout.LayoutParams(-1, Dp(54)));
        compactPanel = new LinearLayout(context) { Orientation = Orientation.Vertical };
        compactPanel.SetPadding(Dp(4), Dp(3), Dp(4), Dp(3));
        currentLineView = new TextView(context) { TextSize = FontSize(10), Gravity = GravityFlags.Top | GravityFlags.Start };
        currentLineView.SetTextColor(PgrTheme.Foreground);
        currentLineView.SetMaxLines(2);
        currentLineView.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        currentLineView.SetPadding(Dp(2), 0, Dp(2), 0);
        // Speaker portrait (user-local image or geometric monogram) beside the two-line current text.
        var lineRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        overlayPortraitFrame = new PortraitFrameDrawable(context.Resources!.DisplayMetrics!.Density) { ShowSerial = false };
        overlayPortrait = new View(context) { Background = overlayPortraitFrame, ImportantForAccessibility = ImportantForAccessibility.No };
        lineRow.AddView(overlayPortrait, new LinearLayout.LayoutParams(Dp(30), Dp(30)) { RightMargin = Dp(4) });
        lineRow.AddView(currentLineView, new LinearLayout.LayoutParams(0, Dp(30), 1));
        compactPanel.AddView(lineRow, new LinearLayout.LayoutParams(-1, Dp(30)));
        RefreshCurrentLine();
        compactActions = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        foreach (var item in new[]
        {
            (Label: "上一句", Description: "上一句", Action: OverlayCommand.Previous),
            (Label: "当前句", Description: "重播当前句", Action: OverlayCommand.Replay),
            (Label: "下一句", Description: "下一句", Action: OverlayCommand.Next)
        })
        {
            var button = new Button(context) { Text = item.Label, TextSize = FontSize(10), ContentDescription = item.Description };
            StyleControl(button, primary: item.Action == OverlayCommand.Replay);
            // 方案 1：外观 32dp 高，触摸范围上下各多 6dp（44dp），都在台词条窗口之内，不会多挡游戏。
            if (wideConsole) button.Background = new global::Android.Graphics.Drawables.InsetDrawable(button.Background, 0, ReadDp(6), 0, ReadDp(6));
            button.SetPadding(Dp(2), Dp(1), Dp(2), Dp(1));
            button.SetSingleLine(true);
            int minHeight = wideConsole ? 0 : Touch();
            button.SetMinHeight(minHeight); button.SetMinimumHeight(minHeight);
            var command = item.Action;
            button.Click += (_, _) => Command?.Invoke(command);
            compactActions.AddView(button, wideConsole ? new LinearLayout.LayoutParams(ReadDp(72), ReadDp(44)) { LeftMargin = ReadDp(3) }
                : new LinearLayout.LayoutParams(0, Touch(), 1) { LeftMargin = Dp(1), RightMargin = Dp(1) });
        }
        compactPanel.AddView(compactActions, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        root.AddView(compactPanel, new LinearLayout.LayoutParams(-1, -2));
        compactPanel.Background = FusionDeco.Over(PgrTheme.Plate(context, Color.Argb(248, 0xF2, 0xF3, 0xF5), null, PgrTheme.CutSmall, Corners.BottomRight), new FusionDeco(context.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutBR = PgrTheme.CutSmall });
        if (wideConsole)
        {
            // 15-B2 bar: dark handle block「配音 / 点此展开」at left, line in the middle, the three small keys at right — one 60dp row.
            compactPanel.Orientation = Orientation.Horizontal;
            compactPanel.SetGravity(GravityFlags.CenterVertical);
            // 纸面上下各留 3dp 透明，露出外框；按钮 44dp 触摸高正好填满。
            compactPanel.Background = new global::Android.Graphics.Drawables.InsetDrawable(compactPanel.Background, 0, ReadDp(3), 0, ReadDp(3));
            compactPanel.SetPadding(Dp(4), 0, Dp(4), 0);
            lineRow.LayoutParameters = new LinearLayout.LayoutParams(0, -1, 1);
            lineRow.SetGravity(GravityFlags.CenterVertical);
            overlayPortrait.LayoutParameters = new LinearLayout.LayoutParams(Dp(24), Dp(24)) { RightMargin = Dp(4) };
            currentLineView.LayoutParameters = new LinearLayout.LayoutParams(0, -2, 1);
            currentLineView.TextSize = FontSize(11);
            currentLineView.SetMaxLines(1);
            currentLineView.Gravity = GravityFlags.CenterVertical | GravityFlags.Start;
            lineRow.Clickable = true;
            lineRow.Click += (_, _) => ToggleSlimLine();
            compactActions.LayoutParameters = new LinearLayout.LayoutParams(-2, -1) { LeftMargin = ReadDp(4) };
            compactActions.SetGravity(GravityFlags.CenterVertical);
        }
        panel = new LinearLayout(context) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        panel.SetPadding(Dp(9), Dp(8), Dp(9), Dp(6));
        browseTabs.Clear();
        var tabs = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        string[] tabLabels = { "控制", "台词", "定位", "分支", "记录" };
        for (int i = 0; i < tabLabels.Length; i++)
        {
            int index = i;
            var tab = new Button(context) { Text = tabLabels[i], TextSize = FontSize(11), ContentDescription = "悬浮" + tabLabels[i] + "页" };
            tab.Click += (_, _) => { if (index == 0) ShowControlPage(); else ShowBrowsePage((OverlayBrowsePage)(index - 1)); };
            tabs.AddView(tab, new LinearLayout.LayoutParams(0, Touch(), 1) { LeftMargin = Dp(1), RightMargin = Dp(1) });
            browseTabs.Add(tab);
        }
        panel.AddView(tabs, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
        controlPanel = new LinearLayout(context) { Orientation = Orientation.Vertical };
        panel.AddView(controlPanel, new LinearLayout.LayoutParams(-1, 0, 1));
        playbackModeView = new TextView(context) { TextSize = FontSize(12) };
        playbackModeView.SetPadding(Dp(3), 0, Dp(3), Dp(5));
        playbackModeView.SetMaxLines(1);
        playbackModeView.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        controlPanel.AddView(playbackModeView, new LinearLayout.LayoutParams(-1, -2));
        // Keep position confirmation accessible above the scrollable controls.
        var confirmPosition = new Button(context) { Text = "确认当前句", TextSize = FontSize(12), ContentDescription = "确认播放器当前句与游戏一致，不会采用未选择的 OCR 结果" };
        StyleControl(confirmPosition, primary: true);
        confirmPosition.SetPadding(Dp(3), Dp(5), Dp(3), Dp(5));
        confirmPosition.SetMinHeight(Touch()); confirmPosition.SetMinimumHeight(Touch());
        confirmPosition.Click += (_, _) =>
        {
            actionScroll?.ScrollTo(0, 0);
            Command?.Invoke(OverlayCommand.ConfirmPosition);
        };
        controlPanel.AddView(confirmPosition, new LinearLayout.LayoutParams(-1, Touch()) { LeftMargin = Dp(2), RightMargin = Dp(2), BottomMargin = Dp(8) });
        RefreshPlaybackMode();
        var scroll = new ScrollView(context) { FillViewport = false, VerticalScrollBarEnabled = true };
        actionScroll = scroll;
        var actionRows = new LinearLayout(context) { Orientation = Orientation.Vertical };
        scroll.AddView(actionRows, new ScrollView.LayoutParams(-1, -2));
        controlPanel.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        autoPlaybackPausedCard = new LinearLayout(context) { Orientation = Orientation.Vertical };
        autoPlaybackPausedCard.SetPadding(Dp(10), Dp(9), Dp(10), Dp(9));
        autoPlaybackPausedCard.Background = PgrTheme.Plate(context, Color.Argb(235, PgrTheme.Selection.R, PgrTheme.Selection.G, PgrTheme.Selection.B),
            PgrTheme.SelectionLine, PgrTheme.CutMedium, Corners.Diagonal, PgrTheme.Red, AccentEdge.Left, brackets: PgrTheme.Red);
        actionRows.AddView(autoPlaybackPausedCard, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
        RefreshAutoPlaybackPaused();
        gameEndWaitCard = new LinearLayout(context) { Orientation = Orientation.Vertical };
        gameEndWaitCard.SetPadding(Dp(12), Dp(9), Dp(12), Dp(9));
        gameEndWaitCard.Background = FusionDeco.Over(PgrTheme.Plate(context, PgrTheme.A(Color.ParseColor("#FBF7EC"), 245), PgrArt.GoldLine, PgrTheme.CutMedium, Corners.Diagonal, PgrArt.GoldLine, AccentEdge.Left),
            new FusionDeco(context.Resources!.DisplayMetrics!.Density, DecoTier.M) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium });
        actionRows.AddView(gameEndWaitCard, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
        RefreshGameEndWaiting();
        var compactOptions = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        compactButtonsButton = BrowsePagingButton("小按钮：开", () =>
        {
            SetCompactButtonsVisible(!compactButtonsVisible);
            CompactButtonsChanged?.Invoke(compactButtonsVisible);
        });
        compactControlsButton = BrowsePagingButton("仅保留悬浮球", () =>
        {
            SetCompactControlsVisible(!compactControlsVisible);
            CompactControlsChanged?.Invoke(compactControlsVisible);
            SetExpanded(false);
        });
        foreach (var button in new[] { compactButtonsButton, compactControlsButton })
            compactOptions.AddView(button, new LinearLayout.LayoutParams(0, Touch(), 1) { LeftMargin = Dp(1), RightMargin = Dp(1) });
        actionRows.AddView(compactOptions, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(5) });
        statusView = new TextView(context) { Text = status, TextSize = FontSize(12) };
        StyleStatus(statusView, status, playbackPaused);
        statusView.SetLineSpacing(Dp(2), 1);
        statusView.SetMaxLines(3);
        statusView.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        actionRows.AddView(statusView, new LinearLayout.LayoutParams(-1, -2));
        if (wideConsole)
        {
            // 15-B2 console: the same 13 keys as a 3 × 5 grid on the right; confirm / status / options in a left column.
            AddRow(actionRows, true, false, ("上一句", OverlayCommand.Previous), ("重播", OverlayCommand.Replay), ("下一句", OverlayCommand.Next),
                (playbackPaused ? "继续" : "暂停", OverlayCommand.Pause), ("原声", OverlayCommand.Original));
            AddRow(actionRows, false, false, (autoPlaying ? "停止自动" : "自动播放", OverlayCommand.AutoPlay),
                (clickFollowing ? "停止点按" : "点按跟随", OverlayCommand.ClickFollow), ("下一句区域", OverlayCommand.AdvanceRegion), ("识别定位", OverlayCommand.Ocr), ("分支", OverlayCommand.Branch));
            AddRow(actionRows, false, false, ("历史", OverlayCommand.History), ("章节", OverlayCommand.Chapters), ("反馈这句", OverlayCommand.Feedback));
            controlPanel.RemoveView(playbackModeView); controlPanel.RemoveView(confirmPosition);
            actionRows.RemoveView(autoPlaybackPausedCard); actionRows.RemoveView(gameEndWaitCard); actionRows.RemoveView(compactOptions); actionRows.RemoveView(statusView);
            var leftScroll = new ScrollView(context) { VerticalScrollBarEnabled = false };
            var left = new LinearLayout(context) { Orientation = Orientation.Vertical };
            leftScroll.AddView(left, new ScrollView.LayoutParams(-1, -2));
            left.AddView(playbackModeView, new LinearLayout.LayoutParams(-1, -2));
            left.AddView(confirmPosition, new LinearLayout.LayoutParams(-1, Touch()) { BottomMargin = Dp(6) });
            left.AddView(autoPlaybackPausedCard, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
            left.AddView(gameEndWaitCard, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
            left.AddView(compactOptions, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
            left.AddView(statusView, new LinearLayout.LayoutParams(-1, -2));
            controlPanel.Orientation = Orientation.Horizontal;
            controlPanel.AddView(leftScroll, 0, new LinearLayout.LayoutParams(ReadDp(250), -1) { RightMargin = ReadDp(8) });
            scroll.LayoutParameters = new LinearLayout.LayoutParams(0, -1, 1);
        }
        else
        {
        AddRow(actionRows, false, false, (autoPlaying ? "停止自动" : "自动播放", OverlayCommand.AutoPlay),
            (clickFollowing ? "停止点按" : "点按跟随", OverlayCommand.ClickFollow), ("下一句区域", OverlayCommand.AdvanceRegion));
        AddRow(actionRows, true, false, ("上一句", OverlayCommand.Previous), ("重播", OverlayCommand.Replay), ("下一句", OverlayCommand.Next));
        AddRow(actionRows, false, false, (playbackPaused ? "继续" : "暂停", OverlayCommand.Pause), ("分支", OverlayCommand.Branch), ("原声", OverlayCommand.Original));
        AddRow(actionRows, false, false, ("识别定位", OverlayCommand.Ocr), ("历史", OverlayCommand.History), ("章节", OverlayCommand.Chapters));
        AddRow(actionRows, false, false, ("反馈这句", OverlayCommand.Feedback));
        }
        browseShell = new LinearLayout(context) { Orientation = Orientation.Horizontal, Visibility = ViewStates.Gone };
        var sidebar = new LinearLayout(context) { Orientation = Orientation.Vertical };
        sidebar.SetPadding(ReadDp(3), ReadDp(4), ReadDp(3), ReadDp(4));
        sidebar.Background = PgrTheme.Plate(context, PgrTheme.GlassRaised, null, PgrTheme.CutSmall, Corners.BottomRight);
        var sideScroll = new ScrollView(context) { VerticalScrollBarEnabled = false };
        var sideItems = new LinearLayout(context) { Orientation = Orientation.Vertical };
        sideScroll.AddView(sideItems, new ScrollView.LayoutParams(-1, -2));
        sidebar.AddView(sideScroll, new LinearLayout.LayoutParams(-1, 0, 1));
        browseSideTabs.Clear();
        for (int i = 0; i < tabLabels.Length; i++)
        {
            int index = i;
            var tab = ReadingButton(tabLabels[i], () => { if (index == 0) ShowControlPage(); else ShowBrowsePage((OverlayBrowsePage)(index - 1)); });
            tab.ContentDescription = "悬浮" + tabLabels[i] + "页";
            sideItems.AddView(tab, new LinearLayout.LayoutParams(-1, Touch()) { BottomMargin = ReadDp(2) });
            browseSideTabs.Add(tab);
        }
        var collapse = ReadingButton("收起", () => SetExpanded(false));
        sidebar.AddView(collapse, new LinearLayout.LayoutParams(-1, Touch()));
        browseShell.AddView(sidebar, new LinearLayout.LayoutParams(ReadDp(60), -1));
        browsePanel = new LinearLayout(context) { Orientation = Orientation.Vertical };
        browsePanel.SetPadding(ReadDp(8), ReadDp(3), ReadDp(5), ReadDp(3));
        // 15-B2 reader: cold-white paper reading area inside the dark L frame.
        browsePanel.Background = FusionDeco.Over(PgrTheme.Plate(context, Color.Argb(248, 0xF2, 0xF3, 0xF5), null, PgrTheme.CutSmall, Corners.BottomRight), new FusionDeco(context.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutBR = PgrTheme.CutSmall });
        var readHeader = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        readHeader.SetGravity(GravityFlags.CenterVertical);
        browseHeading = new TextView(context) { Text = "台词", TextSize = 13, Gravity = GravityFlags.CenterVertical,
            ContentDescription = "阅读面板标题，按住拖动" };
        browseHeading.SetTextColor(PgrTheme.Steel); browseHeading.SetSingleLine(true);
        browseHeading.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); browseHeading.LetterSpacing = .04f;
        browseHeading.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        browseHeading.SetOnTouchListener(new DragListener(this, false));
        readHeader.AddView(browseHeading, new LinearLayout.LayoutParams(0, -1, 1));
        var closeReading = ReadingButton("收回", ShowControlPage);
        closeReading.ContentDescription = "收回阅读面板，返回控制页";
        readHeader.AddView(closeReading, new LinearLayout.LayoutParams(ReadDp(64), -1));
        browsePanel.AddView(readHeader, new LinearLayout.LayoutParams(-1, Touch()) { BottomMargin = ReadDp(3) });
        browseScroll = new ScrollView(context) { FillViewport = false, VerticalScrollBarEnabled = true };
        browseRows = new LinearLayout(context) { Orientation = Orientation.Vertical };
        browseScroll.AddView(browseRows, new ScrollView.LayoutParams(-1, -2));
        browsePanel.AddView(browseScroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var pagination = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        browsePrevious = ReadingButton("上一页", () => { browsePageIndex--; RenderBrowse(preferSelected: false); });
        browseCurrent = ReadingButton("回当前句", () => { if (browseModel != null) { browsePageIndex = SelectedBrowsePage(browseModel); RenderBrowse(); } });
        browseNext = ReadingButton("下一页", () => { browsePageIndex++; RenderBrowse(preferSelected: false); });
        foreach (var button in new[] { browsePrevious, browseCurrent, browseNext })
            pagination.AddView(button, new LinearLayout.LayoutParams(0, Touch(), 1) { LeftMargin = ReadDp(1), RightMargin = ReadDp(1) });
        browsePanel.AddView(pagination, new LinearLayout.LayoutParams(-1, -2) { TopMargin = ReadDp(2) });
        browseShell.AddView(browsePanel, new LinearLayout.LayoutParams(0, -1, 1));
        RenderBrowse();
        var divider = new View(context); divider.SetBackgroundColor(PgrTheme.GlassLine);
        panel.AddView(divider, new LinearLayout.LayoutParams(-1, Math.Max(1, Dp(.5f))) { TopMargin = Dp(6), BottomMargin = Dp(2) });
        // Keep collapse/hide accessible even when a landscape screen makes the actions scroll.
        if (wideConsole)
        {
            // 15-B2: tabs and 主界面 / 收起 / 隐藏 merged into the dark title row; the dot-grip title is the drag handle (same DragListener).
            panel.RemoveView(tabs); panel.RemoveView(divider);
            consoleHeader = new LinearLayout(context) { Orientation = Orientation.Horizontal, Visibility = ViewStates.Gone };
            consoleHeader.SetGravity(GravityFlags.CenterVertical);
            var grip = new TextView(context) { Text = "⋮⋮ 配音控制", TextSize = 13, Gravity = GravityFlags.CenterVertical, ContentDescription = "配音控制，点击收起，按住拖动" };
            grip.SetTextColor(Color.White); grip.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); grip.SetSingleLine(true);
            grip.SetPadding(ReadDp(10), 0, ReadDp(6), 0);
            grip.SetOnTouchListener(new DragListener(this));
            consoleHeader.AddView(grip, new LinearLayout.LayoutParams(-2, -1));
            consoleHeader.AddView(tabs, new LinearLayout.LayoutParams(0, -1, 1));
            var tools = new LinearLayout(context) { Orientation = Orientation.Horizontal };
            AddRow(tools, false, true, ("主界面", OverlayCommand.OpenApp), ("收起", null), ("隐藏", OverlayCommand.Hide));
            consoleHeader.AddView(tools, new LinearLayout.LayoutParams(ReadDp(300), -1) { LeftMargin = ReadDp(4) });
            root.AddView(consoleHeader, new LinearLayout.LayoutParams(-1, Touch() + ReadDp(4)));
        }
        else AddRow(panel, false, true, ("主界面", OverlayCommand.OpenApp), ("收起", null), ("隐藏", OverlayCommand.Hide));
        panel.Background = FusionDeco.Over(PgrTheme.Plate(context, Color.Argb(248, 0xF2, 0xF3, 0xF5), null, PgrTheme.CutSmall, Corners.BottomRight), new FusionDeco(context.Resources!.DisplayMetrics!.Density, DecoTier.M) { CutBR = PgrTheme.CutSmall });
        root.AddView(panel, new LinearLayout.LayoutParams(-1, 0, 1));
        root.AddView(browseShell, new LinearLayout.LayoutParams(-1, 0, 1));
        parameters = new WindowManagerLayoutParams(Dp(58), Dp(58),
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutInScreen,
            Format.Translucent)
        { Gravity = GravityFlags.Top | GravityFlags.Left, X = Dp(12), Y = Dp(100) };
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P) parameters.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Never;
        RefreshPresentation();
        UpdateSize();
    }

    /// <summary>16-C: visible choiceActionLabel「确认并继续配音 ›」at a card's lower right.</summary>
    private TextView ActionHint(string label)
    {
        var hint = new TextView(context) { Text = label + " ›", TextSize = 11, Gravity = GravityFlags.End, ImportantForAccessibility = ImportantForAccessibility.No };
        hint.SetTextColor(PgrTheme.RedText); hint.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); hint.SetSingleLine(true);
        return hint;
    }

    private Button BrowsePagingButton(string label, Action action)
    {
        var button = new Button(context) { Text = label, TextSize = FontSize(10), ContentDescription = label };
        StyleControl(button, quiet: true);
        button.SetMinHeight(Touch()); button.SetMinimumHeight(Touch());
        button.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
        button.Click += (_, _) => action();
        return button;
    }

    private Button ReadingButton(string label, Action action)
    {
        var button = new Button(context) { Text = label, TextSize = 12, ContentDescription = label };
        StyleControl(button, quiet: true);
        button.SetSingleLine(true); button.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        button.SetMinHeight(Touch()); button.SetMinimumHeight(Touch());
        button.SetPadding(ReadDp(5), ReadDp(2), ReadDp(5), ReadDp(2));
        button.Click += (_, _) => action();
        return button;
    }

    private void AddRow(LinearLayout container, bool playbackRow, bool footer, params (string Label, OverlayCommand? Action)[] actions)
    {
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal, BaselineAligned = false };
        foreach (var item in actions)
        {
            var button = new Button(context) { Text = item.Label, TextSize = FontSize(11) };
            StyleControl(button, primary: playbackRow && item.Action == OverlayCommand.Replay || autoPlaying && item.Action == OverlayCommand.AutoPlay || clickFollowing && item.Action == OverlayCommand.ClickFollow, quiet: footer);
            button.SetPadding(Dp(3), Dp(footer ? 3 : 7), Dp(3), Dp(footer ? 3 : 7));
            button.SetMinHeight(Touch());
            button.SetMinimumHeight(Touch());
            button.ContentDescription = item.Action == OverlayCommand.OpenApp ? "打开主界面" : item.Label;
            bool onDark = footer && wideConsole && ReferenceEquals(container.Parent, consoleHeader) == false && container != panel;
            var icon = Icon(IconName(item.Action), footer ? 15 : wideConsole ? 17 : playbackRow ? 24 : 20, onDark ? PgrTheme.A(Color.White, 200) : footer ? PgrTheme.Secondary : PgrTheme.Foreground);
            if (onDark) { button.SetTextColor(PgrTheme.A(Color.White, 215)); button.SetSingleLine(true); button.SetPadding(ReadDp(2), 0, ReadDp(2), 0); }
            button.CompoundDrawablePadding = Dp(footer ? 5 : 4);
            if (footer || wideConsole) button.SetCompoundDrawablesWithIntrinsicBounds(icon, null, null, null);
            else button.SetCompoundDrawablesWithIntrinsicBounds(null, icon, null, null);
            if (item.Action == OverlayCommand.Pause) pauseButton = button;
            if (item.Action == OverlayCommand.AutoPlay)
            {
                autoPlayButton = button;
                button.ContentDescription = autoPlaying ? "停止自动播放" : "开始自动播放";
            }
            if (item.Action == OverlayCommand.ClickFollow)
            {
                clickFollowButton = button;
                button.ContentDescription = clickFollowing ? "停止点按跟随" : "开启点按跟随";
            }
            var action = item.Action;
            button.Click += (_, _) =>
            {
                if (action == null) SetExpanded(false);
                else if (action == OverlayCommand.Hide) { HideCore(); Command?.Invoke(action.Value); }
                else if (action == OverlayCommand.Branch) ShowBrowsePage(OverlayBrowsePage.Branches);
                else if (action == OverlayCommand.History) ShowBrowsePage(OverlayBrowsePage.History);
                else if (action == OverlayCommand.Ocr) { ShowBrowsePage(OverlayBrowsePage.Locate); Command?.Invoke(action.Value); }
                else Command?.Invoke(action.Value);
            };
            row.AddView(button, new LinearLayout.LayoutParams(0, wideConsole ? Touch() : Math.Max(Touch(), Dp(footer ? 44 : playbackRow ? 68 : 58)), 1)
                { LeftMargin = Dp(2), RightMargin = Dp(2), TopMargin = Dp(footer ? 2 : 5) });
        }
        // Explicit fill width: a horizontal container (console header tools) would otherwise wrap the row and starve the weights.
        container.AddView(row, new LinearLayout.LayoutParams(-1, -2));
    }

    /// <summary>State icons must keep the placement AddRow chose: left of the label on the 48dp console keys, above it otherwise.</summary>
    private void SetActionIcon(Button button, string name)
    {
        var icon = Icon(name, wideConsole ? 17 : 20);
        if (wideConsole) button.SetCompoundDrawablesWithIntrinsicBounds(icon, null, null, null);
        else button.SetCompoundDrawablesWithIntrinsicBounds(null, icon, null, null);
    }

    /// <summary>
    /// Shared button sizing from <see cref="PgrTheme.StyleButton"/>, then the translucent overlay skin so the
    /// game stays visible underneath. Geometry (min sizes, padding) is identical to the app buttons.
    /// </summary>
    private static void StyleControl(Button button, bool selected = false, bool primary = false, bool quiet = false, bool alignStart = false)
    {
        PgrTheme.StyleButton(button, selected, primary, quiet, alignStart);
        if (quiet && !selected && !primary) return;
        PgrTheme.StyleOverlayButton(button, primary ? PgrTheme.OverlayTone.Primary : selected ? PgrTheme.OverlayTone.Selected : PgrTheme.OverlayTone.Glass);
    }

    /// <summary>
    /// Floating ball: dark glass plate with a crimson base tick and HUD brackets. Expanded header and compact
    /// strip use a lighter glass with the crimson signal bar. Only the drawable changes; sizes stay as before.
    /// </summary>
    private Android.Graphics.Drawables.Drawable HandleBackground(bool expanded, bool compact) => expanded
        ? PgrTheme.Plate(context, PgrTheme.GlassRaised, null, PgrTheme.CutSmall, Corners.BottomRight, PgrTheme.Red, AccentEdge.Left)
        : compact
            ? PgrTheme.Plate(context, PgrTheme.GlassStrong, PgrTheme.GlassLine, PgrTheme.CutSmall, Corners.BottomRight, PgrTheme.Red, AccentEdge.Left)
            : PgrTheme.Plate(context, PgrTheme.GlassStrong, PgrTheme.GlassLine, PgrTheme.CutMedium, Corners.Diagonal, PgrTheme.Red, AccentEdge.Bottom,
                accentLength: 18, sheen: PgrTheme.SheenLight);

    private PgrIconDrawable Icon(string name, int size = 20, Color? color = null) => new(name, color ?? PgrTheme.Foreground, density * uiScale, size);
    private string IconName(OverlayCommand? command) => command switch
    {
        OverlayCommand.Pause => playbackPaused ? "play" : "pause",
        OverlayCommand.Replay => "replay", OverlayCommand.Previous => "previous", OverlayCommand.Next => "next",
        OverlayCommand.Branch => "branch", OverlayCommand.Original => "original", OverlayCommand.Ocr => "locate",
        OverlayCommand.OpenApp => "open", OverlayCommand.History => "history", OverlayCommand.Chapters => "archive",
        OverlayCommand.AutoPlay => autoPlaying ? "pause" : "play", OverlayCommand.AdvanceRegion => "locate",
        OverlayCommand.ClickFollow => clickFollowing ? "pause" : "locate",
        OverlayCommand.Hide => "close", OverlayCommand.Feedback => "feedback", _ => "chevron_up"
    };
    private int Dp(float value) => (int)(value * density * uiScale + 0.5f);
    /// <summary>SPEC C.2: touch targets stay ≥ 48dp even when the short-landscape uiScale shrinks everything else.</summary>
    private int Touch(float value = 48) => Math.Max(ReadDp(48), Dp(value));
    private int ReadDp(float value) => (int)(value * density + .5f);
    private float FontSize(float value) => value * Math.Max(.85f, uiScale);
    private void OnMain(Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper) action();
        else main.Post(action);
    }
    private void ClampPosition()
    {
        if (parameters == null) return;
        UpdateSize();
        ApplyRememberedPlacement();
        var safe = ScreenArea();
        parameters.X = Math.Clamp(parameters.X, safe.Left, Math.Max(safe.Left, safe.Right - parameters.Width));
        parameters.Y = Math.Clamp(parameters.Y, safe.Top, Math.Max(safe.Top, safe.Bottom - parameters.Height));
        UpdateLayout();
        RepositionExplorePanel();
    }
    private void UpdateSize()
    {
        if (parameters == null) return;
        var safe = ScreenArea();
        int screenWidth = Math.Max(1, safe.Right - safe.Left), screenHeight = Math.Max(1, safe.Bottom - safe.Top);
        if (CaptureStopVisible)
        {
            // 不应用小屏 uiScale，取消按钮始终保留至少 48dp 的触摸尺寸。
            parameters.Width = Math.Min(ReadDp(56), screenWidth);
            parameters.Height = Math.Min(ReadDp(56), screenHeight);
            return;
        }
        bool compact = compactControlsVisible && !appForeground;
        bool reading = expanded && browsePage.HasValue;
        int readWidth = ReadDp(wideConsole ? 500 : 456);
        int barHeight = compactButtonsVisible ? Dp(26) + Dp(30) + Touch() + Dp(16) : Dp(66);
        parameters.Width = Math.Min(reading ? readWidth : expanded ? (wideConsole ? ReadDp(820) : Dp(300)) : compact ? (wideConsole ? ReadDp(580) : Dp(180)) : Dp(58), screenWidth);
        int desiredHeight = reading ? ReadDp(wideConsole ? 360 : 440) : expanded ? (wideConsole ? ReadDp(250) : Dp(444)) : compact ? (wideConsole ? SlimBarHeight() : barHeight) : Dp(58);
        parameters.Height = Math.Min(desiredHeight, Math.Max(1, (int)(screenHeight * (reading ? .9f : expanded ? .82f : 1))));
    }
    /// <summary>横屏台词条（方案 1）平时 46dp；点台词看全文时按实际行数加高，换句、展开或重建后回到一行。</summary>
    private bool slimLineFull;
    private int SlimBarHeight()
    {
        int slim = ReadDp(46);
        if (!slimLineFull || currentLineView is not { Width: > 0 } line) return slim;
        line.Measure(View.MeasureSpec.MakeMeasureSpec(line.Width, MeasureSpecMode.Exactly), View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        return slim + Math.Max(0, line.MeasuredHeight + Dp(8) - ReadDp(38));
    }
    private void ToggleSlimLine()
    {
        if (disposed || !wideConsole || expanded || currentLineView == null) return;
        slimLineFull = !slimLineFull;
        currentLineView.SetMaxLines(slimLineFull ? 4 : 1);
        ClampPosition();
    }
    private void CollapseSlimLine()
    {
        if (!slimLineFull) return;
        slimLineFull = false;
        currentLineView?.SetMaxLines(1);
    }
    private (int Left, int Top, int Right, int Bottom) ScreenArea()
    {
        var metrics = context.Resources!.DisplayMetrics!;
        int left = 0, top = (int)(26 * metrics.Density), right = metrics.WidthPixels, bottom = metrics.HeightPixels - (int)(26 * metrics.Density);
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var screen = windows.CurrentWindowMetrics;
            var bounds = screen.Bounds;
            var insets = screen.WindowInsets.GetInsetsIgnoringVisibility(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            left = bounds.Left + insets.Left; top = bounds.Top + insets.Top;
            right = bounds.Right - insets.Right; bottom = bounds.Bottom - insets.Bottom;
        }
        return (left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
    }
    private void RefreshDisplay() => OnMain(() =>
    {
        if (disposed) return;
        var safe = ScreenArea();
        float nextDensity = context.Resources!.DisplayMetrics!.Density;
        float nextFontScale = context.Resources.Configuration?.FontScale ?? 1f;
        int width = safe.Right - safe.Left, height = safe.Bottom - safe.Top;
        float heightDp = height / Math.Max(.1f, nextDensity);
        float nextScale = heightDp < 450 ? Math.Clamp(heightDp / 500f, .72f, 1f) : 1f;
        bool changed = displayWidth != width || displayHeight != height || Math.Abs(density - nextDensity) > .01f || Math.Abs(uiScale - nextScale) > .01f || Math.Abs(displayFontScale - nextFontScale) > .01f;
        displayWidth = width; displayHeight = height; density = nextDensity; uiScale = nextScale; displayFontScale = nextFontScale;
        if (!changed) { if (showing) ClampPosition(); return; }
        DismissConfirmation();
        if (root == null) return;
        bool restore = showing;
        int x = parameters?.X ?? Dp(12), y = parameters?.Y ?? safe.Top;
        HideCore(immediate: true, preserveCaptureRound: true);
        root.Dispose(); root = null;
        try
        {
            BuildView();
            if (parameters != null) { parameters.X = x; parameters.Y = y; }
            if (restore) { windows.AddView(root, parameters); showing = true; PromptBarEpoch++; ClampPosition(); }
        }
        catch (Exception ex) { showing = false; Error?.Invoke("调整悬浮窗口未完成：" + ex.Message); }
    });
    private void UpdateLayout()
    {
        if (showing && root != null && parameters != null)
            try { windows.UpdateViewLayout(root, parameters); } catch (Exception ex) { Error?.Invoke(ex.Message); }
    }
    private void HideCore(bool immediate = false, bool preserveCaptureRound = false)
    {
        ClearBranchEntered(); ClearPromptBarCore(); PromptBarEpoch++;
        ClearDefaultBranchWait();
        DismissConfirmation();
        if (preserveCaptureRound) captureCancelGesture.ResetPresentation();
        else captureCancelGesture.End();
        if (!showing) return;
        showing = false;
        try { if (root != null) { if (immediate) windows.RemoveViewImmediate(root); else windows.RemoveView(root); } } catch { }
    }
    public void Dispose() => OnMain(() =>
    {
        if (disposed) return;
        disposed = true; HideCore(); ClearPromptBarCore();
        displays?.UnregisterDisplayListener(displayListener);
        context.UnregisterComponentCallbacks(configurationListener);
        displayListener.Dispose(); configurationListener.Dispose();
        root?.Dispose(); root = null;
    });

    private sealed class DisplayListener(OverlayController owner) : Java.Lang.Object, DisplayManager.IDisplayListener
    {
        public void OnDisplayAdded(int displayId) => owner.RefreshDisplay();
        public void OnDisplayChanged(int displayId) => owner.RefreshDisplay();
        public void OnDisplayRemoved(int displayId) => owner.RefreshDisplay();
    }

    private sealed class ConfigurationListener(OverlayController owner) : Java.Lang.Object, IComponentCallbacks
    {
        public void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig) => owner.RefreshDisplay();
        public void OnLowMemory() { }
    }

    private void CaptureControlDrawn(View view)
    {
        if (disposed || !showing || !CaptureStopVisible || !ReferenceEquals(captureStopControl, view)) return;
        long now = SystemClock.UptimeMillis();
        if (captureCancelGesture.MarkShown(CurrentCaptureControlGeneration, now))
            CaptureInteraction?.Invoke($"generation={CurrentCaptureControlGeneration}; phase=shown; uptime={now}");
    }

    private sealed class CaptureStopFrame : FrameLayout
    {
        private readonly OverlayController owner;
        public CaptureStopFrame(Context context, OverlayController owner) : base(context)
        {
            this.owner = owner;
            SetWillNotDraw(false);
        }
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            owner.CaptureControlDrawn(this);
        }
    }

    private sealed class CaptureCancelTouchListener(OverlayController owner) : Java.Lang.Object, View.IOnTouchListener
    {
        private long gestureGeneration;
        public bool OnTouch(View? view, MotionEvent? motion)
        {
            if (motion == null) return true;
            if (motion.ActionMasked == MotionEventActions.Down) gestureGeneration = owner.CurrentCaptureControlGeneration;
            if (owner.disposed || !owner.showing || !owner.CaptureStopVisible || view == null ||
                !ReferenceEquals(owner.captureStopControl, view))
            {
                if (motion.ActionMasked != MotionEventActions.Move)
                    owner.CaptureInteraction?.Invoke($"generation={gestureGeneration}; action={motion.ActionMasked}; DownTime={motion.DownTime}; rejected=inactive-or-old-view");
                return true;
            }
            var action = motion.ActionMasked switch
            {
                MotionEventActions.Down => CaptureCancelGestureAction.Down,
                MotionEventActions.Move => CaptureCancelGestureAction.Move,
                MotionEventActions.Up => CaptureCancelGestureAction.Up,
                MotionEventActions.PointerDown or MotionEventActions.PointerUp => CaptureCancelGestureAction.PointerChanged,
                _ => CaptureCancelGestureAction.Cancel
            };
            int count = motion.PointerCount;
            int pointer = count > 0 ? motion.GetPointerId(0) : -1;
            bool inside = count > 0 && motion.GetX(0) >= 0 && motion.GetY(0) >= 0 && motion.GetX(0) < view.Width && motion.GetY(0) < view.Height;
            // Android 可以批量合并 Move；任何历史采样出界都取消本次触摸。
            for (int i = 0; inside && i < motion.HistorySize; i++)
                inside = motion.GetHistoricalX(0, i) >= 0 && motion.GetHistoricalY(0, i) >= 0 &&
                    motion.GetHistoricalX(0, i) < view.Width && motion.GetHistoricalY(0, i) < view.Height;
            bool accepted = owner.captureCancelGesture.Handle(gestureGeneration, action, pointer, count,
                motion.DownTime, motion.EventTime, inside, out string reason);
            if (motion.ActionMasked != MotionEventActions.Move)
                owner.CaptureInteraction?.Invoke($"generation={gestureGeneration}; current={owner.CurrentCaptureControlGeneration}; action={motion.ActionMasked}; pointer={pointer}; count={count}; DownTime={motion.DownTime}; eventTime={motion.EventTime}; {(accepted ? "accepted" : "rejected")}={reason}");
            if (accepted) owner.CaptureCancelRequested?.Invoke(gestureGeneration);
            return true;
        }
    }

    private sealed class DragListener(OverlayController owner, bool toggleOnTap = true) : Java.Lang.Object, View.IOnTouchListener
    {
        private float downX, downY;
        private int startX, startY;
        private bool dragged;
        public bool OnTouch(View? view, MotionEvent? motion)
        {
            if (motion == null || owner.parameters == null) return false;
            switch (motion.ActionMasked)
            {
                case MotionEventActions.Down:
                    downX = motion.RawX; downY = motion.RawY;
                    startX = owner.parameters.X; startY = owner.parameters.Y; dragged = false;
                    return true;
                case MotionEventActions.Move:
                    float dx = motion.RawX - downX, dy = motion.RawY - downY;
                    if (Math.Abs(dx) + Math.Abs(dy) > owner.Dp(7)) dragged = true;
                    if (dragged)
                    {
                        owner.parameters.X = startX + (int)dx; owner.parameters.Y = startY + (int)dy;
                        owner.ClampPosition();
                    }
                    return true;
                case MotionEventActions.Up:
                    if (!dragged) { view?.PerformClick(); if (toggleOnTap) owner.SetExpanded(!owner.expanded); }
                    else owner.SaveCurrentPlacement();
                    return true;
                case MotionEventActions.Cancel: return true;
                default: return false;
            }
        }
    }
}
