using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 03-B2 cross-chapter search: one sheet (bottom panel / wide right panel) with keyword-centred snippet rows, the selected
/// row expanded to 3 lines, and a fixed dark 台词预览 plate (expand ⌃ = full table; 返回结果 = collapse). Search, paging and
/// 「在剧情页打开」 keep the original logic; opening still needs an explicit tap and still asks the original confirmation.
/// </summary>
public sealed partial class MainActivity
{
    readonly LibrarySearchIndex librarySearchIndex = new();
    CancellationTokenSource? librarySearchCancellation;

    void ShowLibrarySearch()
    {
        librarySearchCancellation?.Cancel();
        float density = Resources!.DisplayMetrics!.Density;
        string query = ""; LibrarySearchReport? report = null; int resultPage = 0, selected = -1;
        bool open = true, searching = false, previewExpanded = false, previewHidden = false;
        string? statusText = null, problemText = null; int progressDone = 0, progressTotal = 0;
        PgrSheet sheet = null!;
        // Views of the current layout (rebuilt on rotation).
        LinearLayout resultsView = null!, pager = null!, previewHost = null!, statusBar = null!, resultsColumn = null!;
        TextView statusLine = null!, statusProblems = null!, statusReadout = null!; View statusProgress = null!;
        ScrollView resultsScroll = null!; EditText input = null!; bool wide = false;

        void UpdateStatus()
        {
            if (statusBar == null) return;
            statusBar.Visibility = statusText == null ? ViewStates.Gone : ViewStates.Visible;
            statusLine.Text = statusText ?? "";
            statusProblems.Text = problemText ?? ""; statusProblems.Visibility = problemText == null ? ViewStates.Gone : ViewStates.Visible;
            bool showProgress = searching && progressTotal > 0;
            statusProgress.Visibility = showProgress ? ViewStates.Visible : ViewStates.Gone;
            if (statusProgress.Background is SegmentsDrawable seg) { seg.Count = Math.Max(1, progressTotal); seg.Done = progressDone; seg.Mark = -1; seg.InvalidateSelf(); }
            if (report != null && report.Results.Count > 0)
            {
                int pages = (report.Results.Count + 24) / 25;
                int first = resultPage * 25 + 1, last = Math.Min(report.Results.Count, first + 24);
                statusReadout.Text = $"{first:000}–{last:000}" + (wide ? $" · P{resultPage + 1}/{pages}" : "");
                statusReadout.Visibility = ViewStates.Visible;
            }
            else statusReadout.Visibility = ViewStates.Gone;
        }

        void RenderResults()
        {
            if (resultsView == null) return;
            resultsView.RemoveAllViews(); pager.RemoveAllViews();
            UpdateStatus();
            if (report != null)
            {
                int index = resultPage * 25;
                foreach (var result in report.Results.Skip(resultPage * 25).Take(25))
                {
                    int at = index++;
                    resultsView.AddView(SearchResultRow(result, at, at == selected, query, () =>
                    {
                        selected = at; previewHidden = false; RenderResults(); RenderPreview();
                    }), new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
                }
            }
            BuildPager();
        }

        void BuildPager()
        {
            pager.RemoveAllViews();
            bool hasPrev = report != null && resultPage > 0, hasNext = report != null && (resultPage + 1) * 25 < report.Results.Count;
            if (wide)
            {
                // 412dp-tall screens: 搜索 / 重新读取 / 下一页 share one row at the bottom of the result column.
                var go = new Button(this) { Text = "搜索" }; PgrUi.Style(go, Xb.Primary, "search", "SEARCH", textSp: 14);
                go.Click += (_, _) => Safe(() => Search(false));
                pager.AddView(go, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1.2f) { RightMargin = Dp(6) });
                var reload = new Button(this) { Text = "重新读取" }; PgrUi.Style(reload, Xb.Normal, "replay", textSp: 14);
                reload.Click += (_, _) => Safe(() => Search(true));
                pager.AddView(reload, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1) { RightMargin = Dp(6) });
            }
            else if (report != null && report.Results.Count > 0)
            {
                int pages = (report.Results.Count + 24) / 25;
                pager.AddView(PgrUi.En(this, $"PAGE {resultPage + 1:00} / {pages:00}", 10, PgrTheme.Secondary), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(4) });
            }
            if (searching)
            {
                var close = new Button(this) { Text = "关闭" }; PgrUi.Style(close, Xb.Normal, "close", center: true, textSp: 14);
                close.Click += (_, _) => sheet.Dismiss();
                pager.AddView(close, new LinearLayout.LayoutParams(wide ? 0 : -2, Dp(PgrTheme.Touch), wide ? 1 : 0) { LeftMargin = Dp(6) });
            }
            if (hasPrev)
            {
                var prev = new Button(this) { Text = "上一页" }; PgrUi.Style(prev, Xb.Normal, "chevron_up", wide ? null : "PREV", textSp: 14);
                prev.Click += (_, _) => { resultPage--; RenderResults(); resultsScroll.ScrollTo(0, 0); };
                pager.AddView(prev, new LinearLayout.LayoutParams(wide ? 0 : -2, Dp(PgrTheme.Touch), wide ? 1 : 0) { LeftMargin = Dp(6) });
            }
            if (hasNext)
            {
                var next = new Button(this) { Text = "下一页" }; PgrUi.Style(next, Xb.Normal, "chevron_down", wide ? null : "NEXT", textSp: 14);
                next.Click += (_, _) => { resultPage++; RenderResults(); resultsScroll.ScrollTo(0, 0); };
                pager.AddView(next, new LinearLayout.LayoutParams(wide ? 0 : -2, Dp(PgrTheme.Touch), wide ? 1 : 0) { LeftMargin = Dp(6) });
            }
            pager.Visibility = pager.ChildCount > 0 ? ViewStates.Visible : ViewStates.Gone;
        }

        void RenderPreview()
        {
            if (previewHost == null) return;
            previewHost.RemoveAllViews();
            var hit = report != null && selected >= 0 && selected < report.Results.Count ? report.Results[selected] : null;
            bool show = hit != null && !(wide && previewHidden);
            previewHost.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
            // Portrait: an expanded plate takes the result list's place (返回结果 = collapse).
            if (!wide) resultsColumn.Visibility = show && previewExpanded ? ViewStates.Gone : ViewStates.Visible;
            if (!show) return;
            bool expanded = wide || previewExpanded;
            BuildSearchPreview(previewHost, hit!, selected, query, expanded, wide,
                toggle: () => { if (wide) previewHidden = true; else previewExpanded = !previewExpanded; RenderPreview(); },
                open: () => OpenSearchHit(hit!, () => alive && open && sheet.IsShowing && ReferenceEquals(PgrSheet.Open, sheet), sheet));
        }

        async void Search(bool refresh)
        {
            librarySearchCancellation?.Cancel();
            var cancellation = librarySearchCancellation = new CancellationTokenSource();
            string q = query.Trim();
            report = null; resultPage = 0; selected = -1; previewExpanded = false; problemText = null;
            if (q.Length == 0) { statusText = "请输入角色名或一段台词。"; RenderResults(); RenderPreview(); return; }
            statusText = "正在搜索章节文本…"; searching = true; progressDone = progressTotal = 0; RenderResults(); RenderPreview();
            try
            {
                var files = session.Packages.List().Select(p => p.PackFile).ToArray();
                var progress = new Progress<LibrarySearchProgress>(p =>
                {
                    if (open && alive && ReferenceEquals(cancellation, librarySearchCancellation))
                    { statusText = $"正在搜索 · {p.Completed}/{p.Total} 章"; progressDone = p.Completed; progressTotal = p.Total; UpdateStatus(); }
                });
                var found = await librarySearchIndex.SearchAsync(files, q, cancellation.Token, progress, refresh);
                if (!open || !alive || cancellation.IsCancellationRequested || !ReferenceEquals(cancellation, librarySearchCancellation)) return;
                searching = false;
                report = found; resultPage = 0; selected = found.Results.Count > 0 ? 0 : -1;
                statusText = $"搜索 {found.SearchedPacks} 章 · 找到 {found.TotalMatches} 句" +
                    (found.TotalMatches > found.Results.Count ? $" · 显示前 {found.Results.Count} 句，请增加关键词" : "");
                problemText = found.Problems.Count > 0 ? $"{found.Problems.Count} 个章节暂不可读，可更新后重新读取。" : null;
                RenderResults(); RenderPreview(); resultsScroll?.ScrollTo(0, 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (open && alive && !cancellation.IsCancellationRequested && ReferenceEquals(cancellation, librarySearchCancellation))
                { searching = false; statusText = "搜索未完成：" + ex.Message; RenderResults(); }
            }
        }

        sheet = new PgrSheet(this, s =>
        {
            wide = s.Wide; s.CustomBody = true; s.WideWidthDp = -1;
            // Left / only column: title + close, input, actions, status, results, pager.
            var column = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var titleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; titleRow.SetGravity(GravityFlags.CenterVertical);
            var zone = PgrUi.Zone(this, "跨章节搜索", "SEARCH", null, 17);
            if (wide) zone.ContentDescription = "跨章节搜索。搜索已导入的章节。查看结果不改变播放位置。";
            titleRow.AddView(zone, new LinearLayout.LayoutParams(0, -2, 1));
            titleRow.AddView(CloseButton(() => s.Dismiss()), new LinearLayout.LayoutParams(Dp(48), Dp(48)) { RightMargin = -Dp(10) });
            column.AddView(titleRow, new LinearLayout.LayoutParams(-1, -2));
            if (!wide)
            {
                var note = new TextView(this) { Text = "搜索已导入的章节。查看结果不改变播放位置。", TextSize = 12 }; note.SetTextColor(PgrTheme.Secondary);
                note.SetPadding(Dp(14), 0, 0, Dp(8)); column.AddView(note);
            }
            input = new EditText(this) { Hint = "角色名或台词；空格分隔多个关键词", Text = query, ImeOptions = ImeAction.Search };
            input.SetSingleLine(true);
            input.TextChanged += (_, _) => query = input.Text ?? "";
            input.EditorAction += (_, e) => { if (e.ActionId == ImeAction.Search) { Safe(() => Search(false)); e.Handled = true; } else e.Handled = false; };
            column.AddView(SearchInputRow(input), new LinearLayout.LayoutParams(-1, Dp(52)) { BottomMargin = Dp(8) });
            if (!wide)
            {
                var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                var go = new Button(this) { Text = "搜索" }; PgrUi.Style(go, Xb.Primary, "search", "SEARCH", wing: true);
                go.Click += (_, _) => Safe(() => Search(false));
                actions.AddView(go, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1) { RightMargin = Dp(8) });
                var reload = new Button(this) { Text = "重新读取" }; PgrUi.Style(reload, Xb.Normal, "replay", "RELOAD");
                reload.Click += (_, _) => Safe(() => Search(true));
                actions.AddView(reload, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1));
                DropIconWhenTight(go); DropIconWhenTight(reload); // 0.4.6: large font — icon, then SEARCH/RELOAD tag give way instead of 「搜/索」「重新/读取」
                column.AddView(actions, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
            }
            // Status bar (dark S): status text, readout range, brass problem line, progress segments while searching.
            statusBar = new LinearLayout(this) { Orientation = Orientation.Vertical };
            statusBar.Background = new DarkPanelDrawable(density, DecoTier.S) { CutTopRightDp = 10, Inscription = false, Watermark = false };
            statusBar.SetPadding(Dp(12), Dp(8), Dp(12), Dp(9));
            var statusRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; statusRow.SetGravity(GravityFlags.CenterVertical);
            var diamond = new View(this) { Background = new DiamondMark(density, PgrTheme.Cyan), ImportantForAccessibility = ImportantForAccessibility.No };
            statusRow.AddView(diamond, new LinearLayout.LayoutParams(Dp(9), Dp(9)) { RightMargin = Dp(8) });
            statusLine = new TextView(this) { TextSize = 13 }; statusLine.SetTextColor(Color.ParseColor("#F2F3F5")); statusLine.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal);
            PgrTheme.Tabular(statusLine);
            statusRow.AddView(statusLine, new LinearLayout.LayoutParams(0, -2, 1));
            statusReadout = PgrUi.En(this, "", 10, PgrTheme.NavText); statusReadout.SetAllCaps(false); PgrTheme.Tabular(statusReadout);
            statusRow.AddView(statusReadout);
            statusBar.AddView(statusRow);
            statusProblems = new TextView(this) { TextSize = 11 }; statusProblems.SetTextColor(PgrArt.GoldLine); statusProblems.SetPadding(Dp(17), Dp(2), 0, 0);
            statusBar.AddView(statusProblems);
            statusProgress = new View(this) { Background = new SegmentsDrawable(density) { SegmentDp = 14 }, ImportantForAccessibility = ImportantForAccessibility.No };
            statusBar.AddView(statusProgress, new LinearLayout.LayoutParams(-1, Dp(8)) { TopMargin = Dp(6), LeftMargin = Dp(17) });
            statusLine.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
            column.AddView(statusBar, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
            resultsColumn = new LinearLayout(this) { Orientation = Orientation.Vertical };
            resultsScroll = new ScrollView(this) { VerticalScrollBarEnabled = false, FillViewport = false };
            resultsView = new LinearLayout(this) { Orientation = Orientation.Vertical };
            resultsScroll.AddView(resultsView, new FrameLayout.LayoutParams(-1, -2));
            resultsScroll.SetFadingEdgeLength(Dp(20)); resultsScroll.VerticalFadingEdgeEnabled = true;
            resultsColumn.AddView(resultsScroll, new LinearLayout.LayoutParams(-1, 0, 1));
            pager = new LinearLayout(this) { Orientation = Orientation.Horizontal }; pager.SetGravity(GravityFlags.CenterVertical | GravityFlags.End);
            resultsColumn.AddView(pager, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
            column.AddView(resultsColumn, new LinearLayout.LayoutParams(-1, 0, 1));
            previewHost = new LinearLayout(this) { Orientation = Orientation.Vertical };
            if (wide)
            {
                var split = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                split.AddView(column, new LinearLayout.LayoutParams(0, -1, 1));
                split.AddView(previewHost, new LinearLayout.LayoutParams(Dp(Math.Clamp(Resources!.Configuration!.ScreenWidthDp * 46 / 100, 300, 420)), -1) { LeftMargin = Dp(14), TopMargin = -Dp(14), BottomMargin = -Dp(12), RightMargin = -Dp(16) });
                s.Body.AddView(split, new LinearLayout.LayoutParams(-1, -1));
            }
            else
            {
                s.Body.AddView(column, new LinearLayout.LayoutParams(-1, 0, 1));
                s.Body.AddView(previewHost, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = -Dp(16), RightMargin = -Dp(16), BottomMargin = -Dp(12), TopMargin = Dp(6) });
            }
            UpdateStatus(); RenderResults(); RenderPreview();
        });
        sheet.Dismissed += () => { open = false; librarySearchCancellation?.Cancel(); };
        sheet.Show();
    }

    ImageButton CloseButton(Action close)
    {
        var x = new ImageButton(this) { ContentDescription = "关闭" };
        x.SetImageDrawable(new PgrIconDrawable("close", PgrTheme.Foreground, Resources!.DisplayMetrics!.Density, 20));
        x.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)), null, new ColorDrawable(Color.White));
        x.Click += (_, _) => close();
        return x;
    }

    /// <summary>Dark search cell (icon) + white input plate.</summary>
    LinearLayout SearchInputRow(EditText input, bool onDark = false)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var cell = new FrameLayout(this) { Background = new DarkPanelDrawable(density, DecoTier.S) { Inscription = false, Watermark = false, ShowTide = false, Ticks = false, CutBottomLeftDp = 8 }, ImportantForAccessibility = ImportantForAccessibility.No };
        var icon = Icon("search", Color.White, 20);
        cell.AddView(icon, new FrameLayout.LayoutParams(Dp(22), Dp(22), GravityFlags.Center));
        row.AddView(cell, new LinearLayout.LayoutParams(Dp(52), -1));
        input.TextSize = 16; input.SetTextColor(PgrTheme.Foreground); input.SetHintTextColor(PgrTheme.Muted);
        input.Background = PgrTheme.Plate(this, Color.White, onDark ? null : PgrTheme.BorderStrong, 10, Corners.TopRight);
        input.SetPadding(Dp(12), 0, Dp(12), 0); input.Gravity = GravityFlags.CenterVertical;
        input.SetMinHeight(Dp(48)); input.SetMinimumHeight(Dp(48));
        row.AddView(input, new LinearLayout.LayoutParams(0, -1, 1));
        return row;
    }

    View SpeakerAvatar(string? speaker, int sizeDp, bool active = true)
    {
        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim();
        var frame = new PortraitFrameDrawable(Resources!.DisplayMetrics!.Density) { ShowSerial = false, Name = name, Active = active };
        var avatar = new View(this) { Background = frame, ImportantForAccessibility = ImportantForAccessibility.No };
        PortraitStore.Request(avatar, name, standee: false, maxPx: Dp(sizeDp * 2), image => { if (frame.Name == name) frame.Image = image; });
        return avatar;
    }

    /// <summary>01-B style result row: index cell (solid crimson when selected), avatar, 「章号 章名 · 小节」, snippet line.</summary>
    View SearchResultRow(LibrarySearchResult result, int index, bool isSelected, string query, Action select)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        row.Background = new GridCellDrawable(density) { IsCurrent = isSelected, SolidIndex = isSelected, IndexWidthDp = 44, Fill = isSelected ? Color.White : (index % 2 == 0 ? Color.Argb(235, 255, 255, 255) : Color.Argb(235, 244, 247, 250)) };
        row.ContentDescription = result.ToString() + (isSelected ? "，已选中" : "");
        row.Click += (_, _) => select();
        row.SetMinimumHeight(Dp(52)); row.SetGravity(GravityFlags.CenterVertical);
        var number = new TextView(this) { Text = (index + 1).ToString("000"), TextSize = 12, Gravity = GravityFlags.Center, ImportantForAccessibility = ImportantForAccessibility.No };
        number.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); number.SetTextColor(isSelected ? Color.White : PgrTheme.Foreground); PgrTheme.Tabular(number);
        row.AddView(number, new LinearLayout.LayoutParams(Dp(44), -1));
        row.AddView(SpeakerAvatar(result.Speaker, 32), new LinearLayout.LayoutParams(Dp(32), Dp(32)) { LeftMargin = Dp(8), RightMargin = Dp(8) });
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        texts.SetPadding(0, Dp(7), Dp(10), Dp(7));
        string chapter = ChapterNumber(result.PackTitle);
        var meta = new SpannableStringBuilder();
        if (chapter != "—") { meta.Append(chapter + " "); meta.SetSpan(new ForegroundColorSpan(PgrTheme.Steel), 0, chapter.Length, SpanTypes.ExclusiveExclusive); meta.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, chapter.Length, SpanTypes.ExclusiveExclusive); }
        meta.Append(PackTitle(result.PackTitle) + " · " + result.SectionTitle);
        var metaView = new TextView(this) { TextFormatted = meta, TextSize = 11 }; metaView.SetTextColor(PgrTheme.Secondary); metaView.SetSingleLine(true); metaView.Ellipsize = TextUtils.TruncateAt.End;
        texts.AddView(metaView);
        var snippet = new SnippetTextView(this, result.Speaker, result.Text, query, isSelected);
        texts.AddView(snippet, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2) });
        row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        return row;
    }

    /// <summary>Dark 台词预览 plate. Collapsed (M): speaker + full line + route chip + 在剧情页打开. Expanded (L): adds the chapter / section / id / route table and the lock note.</summary>
    void BuildSearchPreview(LinearLayout host, LibrarySearchResult hit, int index, string query, bool expanded, bool wide, Action toggle, Action open)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var plate = new LinearLayout(this) { Orientation = Orientation.Vertical };
        plate.Background = new DarkPanelDrawable(density, expanded ? DecoTier.L : DecoTier.M) { CutTopRightDp = wide ? 0 : 18, Mark = expanded ? "PREVIEW" : null, TideDp = expanded ? 48 : 24 };
        plate.SetPadding(Dp(16), Dp(12), Dp(16), Dp(14));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(8) });
        var title = new TextView(this) { Text = "台词预览", TextSize = 15 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White); head.AddView(title);
        head.AddView(PgrUi.En(this, $"PREVIEW · {index + 1:000}", 9, PgrTheme.NavSub), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(8) });
        var chevron = new ImageButton(this) { ContentDescription = wide ? "收起预览栏" : expanded ? "返回结果" : "展开台词预览" };
        chevron.SetImageDrawable(new PgrIconDrawable(wide ? "chevron_down" : expanded ? "chevron_down" : "chevron_up", PgrTheme.OnPlate, density, 18));
        chevron.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 40)), null, new ColorDrawable(Color.White));
        chevron.Click += (_, _) => toggle();
        head.AddView(chevron, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { RightMargin = -Dp(10), TopMargin = -Dp(8), BottomMargin = -Dp(8) });
        plate.AddView(head);
        var scrollBody = new LinearLayout(this) { Orientation = Orientation.Vertical };
        if (expanded)
        {
            var meta = new TextView(this) { Text = $"{ChapterNumber(hit.PackTitle)} {PackTitle(hit.PackTitle)} · {hit.SectionTitle}", TextSize = 11 };
            meta.SetTextColor(PgrTheme.NavText); meta.SetPadding(0, Dp(2), 0, Dp(8)); scrollBody.AddView(meta);
        }
        var who = new LinearLayout(this) { Orientation = Orientation.Horizontal }; who.SetGravity(GravityFlags.CenterVertical);
        who.AddView(SpeakerAvatar(hit.Speaker, 44), new LinearLayout.LayoutParams(Dp(44), Dp(44)) { RightMargin = Dp(10) });
        var name = new TextView(this) { Text = hit.Speaker.Length == 0 ? "旁白" : hit.Speaker, TextSize = 14 };
        name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); name.SetTextColor(PgrTheme.OnPlate);
        name.Background = new NamePlateDrawable(density); name.SetPadding(Dp(12), Dp(5), Dp(24), Dp(7));
        who.AddView(name, new LinearLayout.LayoutParams(-2, -2));
        scrollBody.AddView(who, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(8) });
        var line = new TextView(this) { TextSize = 16 };
        line.SetTextColor(Color.ParseColor("#F2F3F5")); line.SetLineSpacing(Dp(4), 1);
        line.TextFormatted = SnippetTextView.Highlighted(hit.Speaker, hit.Text, query, onDark: true);
        scrollBody.AddView(line);
        bool hasRoute = !string.IsNullOrWhiteSpace(hit.Route);
        var route = PgrUi.Chip(this, hasRoute ? "路线：" + hit.Route : "未附分支编号；打开后按原路线确认", hasRoute ? PgrUi.Status.Brass : PgrUi.Status.NotStarted);
        if (!hasRoute) { route.SetTextColor(PgrTheme.NavText); route.Background = PgrTheme.Plate(this, PgrTheme.A(Color.White, 18), PgrTheme.A(PgrTheme.NavText, 120), 4, Corners.BottomRight); }
        else { route.SetTextColor(PgrArt.GoldLine); }
        route.SetSingleLine(false); route.SetMaxLines(2);
        scrollBody.AddView(route, new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(8) });
        if (expanded)
        {
            var table = new LinearLayout(this) { Orientation = Orientation.Vertical }; table.SetPadding(0, Dp(12), 0, Dp(4));
            void Row(string label, string value)
            {
                var r = new LinearLayout(this) { Orientation = Orientation.Horizontal }; r.SetPadding(0, Dp(6), 0, Dp(6));
                var l = new TextView(this) { Text = label, TextSize = 12 }; l.SetTextColor(PgrTheme.NavText); r.AddView(l, new LinearLayout.LayoutParams(Dp(76), -2));
                var v = new TextView(this) { Text = value, TextSize = 13 }; v.SetTextColor(Color.ParseColor("#F2F3F5")); PgrTheme.Tabular(v); r.AddView(v, new LinearLayout.LayoutParams(0, -2, 1));
                table.AddView(r);
                var rule = new View(this); rule.SetBackgroundColor(PgrTheme.A(PgrArt.MistLine, 50)); table.AddView(rule, new LinearLayout.LayoutParams(-1, 1));
            }
            Row("章节", PackTitle(hit.PackTitle)); Row("小节", hit.SectionTitle); Row("台词编号", hit.NodeId); Row("路线", hasRoute ? hit.Route : "—");
            scrollBody.AddView(table);
            var note = new TextView(this) { Text = "打开后只显示台词所在位置；确认游戏当前句仍使用原有入口。", TextSize = 12 };
            note.SetTextColor(PgrTheme.NavText); note.SetLineSpacing(Dp(2), 1);
            note.SetCompoundDrawablesRelativeWithIntrinsicBounds(new LockGlyph(density), null, null, null); note.CompoundDrawablePadding = Dp(8);
            note.Background = PgrTheme.Plate(this, PgrTheme.A(Color.White, 12), PgrTheme.A(PgrArt.MistLine, 70), 6, Corners.BottomRight);
            note.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
            scrollBody.AddView(note, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
            var scroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
            scroll.AddView(scrollBody);
            plate.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        }
        else plate.AddView(scrollBody);
        var openButton = new Button(this) { Text = "在剧情页打开" }; PgrUi.Style(openButton, Xb.Primary, "open", "OPEN", wing: true, textSp: 15);
        openButton.Click += (_, _) => open();
        plate.AddView(openButton, new LinearLayout.LayoutParams(-1, Dp(52)) { TopMargin = Dp(12), BottomMargin = Dp(wide ? 10 : 4) });
        host.AddView(plate, new LinearLayout.LayoutParams(-1, expanded ? -1 : -2));
        if (!wide && expanded && host.LayoutParameters is LinearLayout.LayoutParams lp) { lp.Height = 0; lp.Weight = 1; host.LayoutParameters = lp; }
        else if (!wide && host.LayoutParameters is LinearLayout.LayoutParams lp2) { lp2.Height = -2; lp2.Weight = 0; host.LayoutParameters = lp2; }
    }

    /// <summary>「在剧情页打开」: unchanged checks; the sheet replaces the old search dialog in the validity test.</summary>
    async void OpenSearchHit(LibrarySearchResult hit, Func<bool> sheetOpen, PgrSheet sheet)
    {
        var searchRequest = librarySearchCancellation;
        bool RequestStillActive() => alive && sheetOpen() && (searchRequest == null || (!searchRequest.IsCancellationRequested && ReferenceEquals(searchRequest, librarySearchCancellation)));
        try
        {
            if (!RequestStillActive()) return;
            if (session.Engine?.Mode == RunMode.Original) throw new InvalidOperationException("当前处于游戏原声时段，请先按原流程确认续接位置。");
            if (session.PackagesBusy) throw new InvalidOperationException("章节文件正在处理中，请稍后重新搜索。");
            var engineBefore = session.Engine;
            var nodeBefore = engineBefore?.CurrentId;
            var modeBefore = engineBefore?.Mode;
            var sectionBefore = session.SectionId;
            var listeningPackBefore = session.Listening.Pack;
            var listeningNodeBefore = session.Listening.Current?.Id;
            var target = await Task.Run(() => LibrarySearchIndex.Resolve(hit));
            if (!RequestStillActive()) return;
            if (!ReferenceEquals(engineBefore, session.Engine) || session.Engine?.CurrentId != nodeBefore || session.Engine?.Mode != modeBefore ||
                session.SectionId != sectionBefore || session.Listening.Pack != listeningPackBefore || session.Listening.Current?.Id != listeningNodeBefore ||
                session.Engine?.Mode == RunMode.Original || session.PackagesBusy)
                throw new InvalidOperationException("播放章节或文件状态已变化，请重新打开搜索结果。");
            var installed = session.Packages.Find(hit.PackId);
            if (installed == null || System.IO.Path.GetFullPath(installed.PackFile) != System.IO.Path.GetFullPath(hit.PackFile))
                throw new InvalidOperationException("章节已更新或移除，请重新读取后搜索。");
            // 同图也可能已更新正文/音频；明确打开时重读当前清单，进度仍由原加载流程恢复。
            session.LoadPack(hit.PackId);
            var actual = session.Engine!.Pack;
            if (PlaybackEngine.NavigationFingerprint(actual) != PlaybackEngine.NavigationFingerprint(target.Pack) ||
                !actual.ById.TryGetValue(hit.NodeId, out var node) || node.Archived || node.Kind != "line" || node.SectionId != hit.SectionId || node.PathId != hit.PathId || node.Text != hit.Text || node.Speaker != hit.Speaker)
                throw new InvalidOperationException("台词版本已变化，请重新搜索。");
            session.SectionId = hit.SectionId;
            var lines = actual.Nodes.Where(n => !n.Archived && n.Kind == "line" && n.SectionId == hit.SectionId).ToList();
            linePage = Math.Max(0, lines.FindIndex(n => n.Id == hit.NodeId)) / 45;
            page = 1; sheet.Dismiss(); Render();
            ConfirmNode(node, () => ReferenceEquals(actual, session.Engine?.Pack));
        }
        catch (Exception ex) { if (RequestStillActive()) Info("搜索结果未打开", ex.Message); }
    }
}

/// <summary>
/// Result line: 「说话人：台词」. Unselected = single-line keyword snippet fitted to the measured width (SearchSnippet), tail fade +
/// muted ellipses; selected = full line, max 3 lines. Keywords are crimson on a faint tint.
/// </summary>
public sealed class SnippetTextView : TextView
{
    readonly string speaker, text, query; readonly bool expanded; int builtWidth = -1;
    public SnippetTextView(Context context, string speaker, string text, string query, bool expanded) : base(context)
    {
        this.speaker = speaker; this.text = text; this.query = query; this.expanded = expanded;
        TextSize = 14; SetTextColor(PgrTheme.Foreground); SetLineSpacing(PgrTheme.Dp(context, 2), 1);
        ImportantForAccessibility = ImportantForAccessibility.No;
        if (expanded) { SetMaxLines(3); Ellipsize = TextUtils.TruncateAt.End; TextFormatted = Highlighted(speaker, text, query, false); }
        else { SetSingleLine(true); Ellipsize = TextUtils.TruncateAt.End; TextFormatted = Highlighted(speaker, text, query, false); }
    }
    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);
        int avail = w - PaddingLeft - PaddingRight;
        if (expanded || avail <= 0 || avail == builtWidth) return;
        builtWidth = avail;
        var measure = Paint!;
        // Bold speaker is slightly wider than the measuring paint; keep a small safety margin.
        var snippet = SearchSnippet.Build(speaker, text, query, s => measure.MeasureText(s), avail * .96f);
        var span = Spans(snippet.Display, (speaker.Length == 0 ? "旁白" : speaker.Trim()).Length + 1, snippet.Highlights, false);
        foreach (var (s, e) in snippet.Ellipses) span.SetSpan(new ForegroundColorSpan(PgrTheme.Muted), s, e, SpanTypes.ExclusiveExclusive);
        if (snippet.Fade is { } f)
        {
            int n = f.End - f.Start;
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? .3f : 1f - .7f * (i + 1) / n;
                span.SetSpan(new ForegroundColorSpan(PgrTheme.A(PgrTheme.Foreground, (int)(255 * Math.Max(.3f, a)))), f.Start + i, f.Start + i + 1, SpanTypes.ExclusiveExclusive);
            }
        }
        Post(() => TextFormatted = span);
    }

    static SpannableString Spans(string display, int speakerLength, IEnumerable<(int Start, int End)> highlights, bool onDark)
    {
        var span = new SpannableString(display);
        span.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, Math.Min(speakerLength, display.Length), SpanTypes.ExclusiveExclusive);
        if (!onDark) span.SetSpan(new ForegroundColorSpan(PgrTheme.Foreground), 0, Math.Min(speakerLength, display.Length), SpanTypes.ExclusiveExclusive);
        foreach (var (s, e) in highlights)
        {
            span.SetSpan(new ForegroundColorSpan(onDark ? PgrArt.TideRed : PgrTheme.RedText), s, e, SpanTypes.ExclusiveExclusive);
            span.SetSpan(new BackgroundColorSpan(onDark ? PgrTheme.A(PgrArt.TideRed, 40) : PgrTheme.A(PgrTheme.Red, 26)), s, e, SpanTypes.ExclusiveExclusive);
            span.SetSpan(new StyleSpan(TypefaceStyle.Bold), s, e, SpanTypes.ExclusiveExclusive);
        }
        return span;
    }

    /// <summary>Full 「说话人：台词」 with every keyword hit highlighted (selected row, preview plate).</summary>
    public static SpannableString Highlighted(string speaker, string text, string query, bool onDark)
    {
        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim();
        string display = name + "：" + text; var terms = SearchSnippet.Terms(query);
        var hits = SearchSnippet.Hits(name, terms).Select(h => (h.Start, h.End))
            .Concat(SearchSnippet.Hits(text, terms).Select(h => (h.Start + name.Length + 1, h.End + name.Length + 1)));
        return Spans(display, name.Length + 1, hits, onDark);
    }
}
