using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 07-B 定位页: dark OCR control band (授权屏幕捕获 / OCR 定位), scope + edit-region cells, OCR RAW dark card, MATCH evidence chain
/// (candidate cards → original ConfirmOcrCandidate), section selector, search, lines. The player position and 「采用识别结果 /
/// 确认当前句」 (ConfirmFooterPosition) live in the fixed locate tray (Settings.LocateTrayCollapsed); wide landscape: 07L-1 column.
/// </summary>
public sealed partial class MainActivity
{
    LinearLayout locateTrayExpanded = null!, locateColumnPanel = null!, locateColumnMatches = null!;
    TextView locateColumnSub = null!, locateColumnRaw = null!;
    readonly List<(Button Button, bool Vertical)> confirmHexes = new();
    string locateColumnHead = "";

    void Locate()
    {
        renderedLocateCandidates = session.Candidates;
        if (session.Engine == null) { LocateEmpty(); return; }
        float density = Resources!.DisplayMetrics!.Density;
        bool narrow = NarrowPage || PageWidthDp < 340;
        // Control band (M deco over the chapter cover): eyebrow, title, outlined OCR, capture / OCR keys.
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.M) { VeilAlpha = 215, CutTopRightDp = 16 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, session.Engine!.Pack.Title, session.Engine.Pack.Id, Dp(560));
        band.AddView(PgrUi.Outline(this, "OCR", narrow ? 34 : 52, PgrTheme.A(Color.White, narrow ? 150 : 210), 1.3f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.Top) { RightMargin = Dp(14), TopMargin = Dp(6) });
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        col.SetPadding(Dp(14), Dp(12), Dp(12), Dp(14));
        var eyebrow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; eyebrow.SetGravity(GravityFlags.CenterVertical);
        eyebrow.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var code = new TextView(this) { Text = "字幕识别 · LOCATE / OCR", TextSize = 11 }; code.SetTextColor(PgrTheme.NavText); code.LetterSpacing = .06f; eyebrow.AddView(code);
        col.AddView(eyebrow);
        var title = new TextView(this) { Text = "定位当前台词", TextSize = narrow ? 19 : CompactLayout ? 20 : 24 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White);
        col.AddView(title, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), BottomMargin = Dp(12) });
        var keys = new LinearLayout(this) { Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal };
        var capture = new Button(this) { Text = "授权屏幕捕获" }; PgrUi.Style(capture, Xb.Dark, "locate", center: true, textSp: 14);
        capture.Click += (_, _) => Safe(RequestCapture);
        var ocr = new Button(this) { Text = "OCR 定位" }; PgrUi.Style(ocr, Xb.Primary, "ocr", center: true, textSp: 15);
        ocr.Click += (_, _) => Safe(RequestManualOcr);
        foreach (var fit in new[] { capture, ocr }) { fit.SetSingleLine(true); fit.SetMinWidth(0); fit.SetMinimumWidth(0); DropIconWhenTight(fit); }
        if (narrow)
        {
            keys.AddView(capture, new LinearLayout.LayoutParams(-1, Dp(48)) { BottomMargin = Dp(6) });
            keys.AddView(ocr, new LinearLayout.LayoutParams(-1, Dp(50)));
        }
        else
        {
            keys.AddView(capture, new LinearLayout.LayoutParams(0, Dp(52), 1) { RightMargin = Dp(6) });
            keys.AddView(ocr, new LinearLayout.LayoutParams(0, Dp(52), 1) { LeftMargin = Dp(6) });
        }
        col.AddView(keys);
        band.AddView(col, new FrameLayout.LayoutParams(-1, -2));
        content.AddView(band, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
        Line("识别后点“采用这句”或“采用识别结果”，核对台词和路线后确认。", 13);

        // Scope + edit-region cells.
        string scopeValue = session.Settings.OcrFullScreen ? "全画面" : "框选区域";
        var scope = new Button(this) { Text = "定位范围：" + scopeValue, ContentDescription = "定位范围：" + scopeValue };
        LinearLayout cells;
        if (narrow) cells = new LinearLayout(this) { Orientation = Orientation.Vertical };
        else
        {
            // 0.4.6: on tight rows (small width / large font) the label shortens instead of ellipsizing "定位范围：全…".
            var fit = new FitRow(this); cells = fit; string[] labels = { "定位范围：" + scopeValue, "范围：" + scopeValue, scopeValue };
            fit.Levels = labels.Length - 1; fit.ApplyLevel = l => scope.Text = labels[l];
            fit.NeedWidth = () => (int)(fit.Natural(scope) * 2.15f / 1.15f) + Dp(8);
        }
        PgrUi.Style(scope, Xb.Normal, "locate", textSp: 13); Dropdown(scope, "locate"); scope.SetPadding(Dp(12), 0, Dp(8), 0);
        scope.Click += (_, _) => Safe(() => Choose("选择定位范围", new[] { "全画面：无需框选", "框选区域：仅识别保存的字幕区域" }, i => { session.SetOcrFullScreen(i == 0); Render(); }));
        var edit = new Button(this) { Text = "编辑框选区域" }; PgrUi.Style(edit, Xb.Normal, "ocr", sub: "先切回游戏", textSp: 13);
        edit.ContentDescription = "编辑框选区域（先切回游戏）"; edit.SetMinWidth(0); edit.SetMinimumWidth(0); edit.SetMaxLines(2);
        edit.Click += (_, _) => Safe(EditRegion);
        // 0.4.6: tight half-row (360dp at large font): drop the leading icon before 编辑框选区域 would wrap to "编辑框选区/域"
        // and push the 先切回游戏 line out of the two-line key. Normal phones (label fits beside the icon) are unchanged.
        var editIcons = edit.GetCompoundDrawablesRelative(); var editIcon = editIcons.Length > 0 ? editIcons[0] : null;
        if (editIcon != null) edit.LayoutChange += (_, le) =>
        {
            int ew = le.Right - le.Left; if (ew <= 0 || ew == le.OldRight - le.OldLeft) return;
            using var ep = new Paint(edit.Paint);
            bool show = ep.MeasureText("编辑框选区域") + edit.PaddingStart + edit.PaddingEnd + editIcon.IntrinsicWidth + edit.CompoundDrawablePadding + Dp(4) <= ew;
            if ((edit.GetCompoundDrawablesRelative()[0] != null) != show)
                edit.Post(() => edit.SetCompoundDrawablesRelativeWithIntrinsicBounds(show ? editIcon : null, editIcons[1], editIcons[2], editIcons[3]));
        };
        if (narrow)
        {
            cells.AddView(scope, new LinearLayout.LayoutParams(-1, Dp(52)) { BottomMargin = Dp(6) });
            cells.AddView(edit, new LinearLayout.LayoutParams(-1, -2));
        }
        else
        {
            cells.AddView(scope, new LinearLayout.LayoutParams(0, -1, 1.15f) { RightMargin = Dp(4) });
            cells.AddView(edit, new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(4) });
        }
        content.AddView(cells, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6), BottomMargin = Dp(6) });
        var scopeNote = Text(session.Settings.OcrFullScreen
            ? "全画面无需框选，识别结果仍需核对后采用。编辑区域只会保存框选范围，不会切换当前设置。"
            : "仅识别框选区域，请将角色名和完整对白框入。识别结果仍需核对后采用。", 12);
        scopeNote.SetTextColor(PgrTheme.Secondary);
        scopeNote.Background = PgrTheme.Plate(this, PgrTheme.A(PgrTheme.Cyan, 18), null, PgrTheme.CutSmall, Corners.BottomRight, PgrTheme.Cyan, AccentEdge.Left);
        scopeNote.SetPadding(Dp(12), Dp(6), Dp(10), Dp(6));
        content.AddView(scopeNote, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });

        // OCR RAW dark card (M): tap = original PrepareRecognizedTextCandidates flow.
        var recognized = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
        recognized.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 40)),
            new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 16, TideDp = 24, Inscription = false, Watermark = false, ShowEmblem = false }, null);
        recognized.SetPadding(Dp(14), Dp(10), Dp(14), Dp(14));
        recognized.ContentDescription = "点击采用这段识别原文";
        recognized.Click += (_, _) => Safe(() =>
        {
            if (!session.PrepareRecognizedTextCandidates())
            { Info("无法选择对应台词", session.Status); return; }
            // 这里只允许采用原文检索所得的候选，不能退回确认旧的播放位置。
            if (page != 2 || session.Candidates.Count == 0)
            { Info("识别候选已变化", "请再次点击识别原文，选择与游戏一致的台词。"); return; }
            ConfirmFooterPosition();
        });
        var rawHead = new LinearLayout(this) { Orientation = Orientation.Horizontal }; rawHead.SetGravity(GravityFlags.CenterVertical);
        rawHead.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var rawTitle = new TextView(this) { Text = "上次识别原文 · OCR RAW", TextSize = 11 }; rawTitle.SetTextColor(PgrTheme.NavText); rawTitle.LetterSpacing = .04f;
        rawHead.AddView(rawTitle, new LinearLayout.LayoutParams(0, -2, 1));
        var rawScope = new TextView(this) { Text = "定位范围：" + (session.Settings.OcrFullScreen ? "全画面" : "框选区域"), TextSize = 10.5f }; rawScope.SetTextColor(PgrTheme.NavSub);
        rawHead.AddView(rawScope);
        recognized.AddView(rawHead);
        var ocrText = new TextView(this) { Text = session.OcrText, TextSize = 16 }; ocrText.SetTextColor(Color.White); ocrText.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal);
        ocrText.SetLineSpacing(Dp(3), 1);
        recognized.AddView(ocrText, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6), BottomMargin = Dp(6) });
        var chooseRecognized = new TextView(this) { Text = "点这里选择对应台词", TextSize = 13 }; chooseRecognized.SetTextColor(PgrArt.TideRed);
        chooseRecognized.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        chooseRecognized.SetCompoundDrawablesWithIntrinsicBounds(null, null, new PgrIconDrawable("chevron", PgrArt.TideRed, density, 14), null); chooseRecognized.CompoundDrawablePadding = Dp(4);
        recognized.AddView(chooseRecognized, new LinearLayout.LayoutParams(-2, -2));
        string resultState = session.Candidates.Count == 0 ? "点原文可重新选择对应台词，核对内容和路线后再确认；实际播放位置见下方。" : "识别原文不代表播放位置。请核对候选后采用；下方显示播放器当前位置。";
        var resultNotice = new TextView(this) { Text = resultState, TextSize = 12 }; resultNotice.SetTextColor(PgrTheme.NavText);
        recognized.AddView(resultNotice, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        content.AddView(recognized, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });

        // Evidence chain.
        var candidateEngine = session.Engine;
        if (session.Candidates.Count > 0)
        {
            content.AddView(PgrUi.En(this, "MATCH · " + session.Candidates.Count + " CANDIDATES", 9.5f, PgrTheme.Steel), new LinearLayout.LayoutParams(-2, -2) { BottomMargin = Dp(4) });
            int i = 0;
            foreach (var candidate in session.Candidates)
            {
                var c = candidate; i++;
                content.AddView(CandidateCard(c, i, i == session.Candidates.Count, () => ConfirmOcrCandidate(c, candidateEngine)), new LinearLayout.LayoutParams(-1, -2));
            }
        }
        SectionSelector();
        var input = new EditText(this) { Hint = "搜索台词或角色", Text = search, ImeOptions = global::Android.Views.InputMethods.ImeAction.Search }; input.SetSingleLine(true);
        input.EditorAction += (_, e) =>
        {
            if (e.ActionId != global::Android.Views.InputMethods.ImeAction.Search) { e.Handled = false; return; }
            e.Handled = true;
            (GetSystemService(InputMethodService) as global::Android.Views.InputMethods.InputMethodManager)?.HideSoftInputFromWindow(input.WindowToken, 0);
            Safe(() => { search = input.Text ?? ""; linePage = 0; Render(); });
        };
        content.AddView(SearchInputRow(input), new LinearLayout.LayoutParams(-1, Dp(50)) { TopMargin = Dp(8), BottomMargin = Dp(6) });
        var searchRow = Row(content);
        var local = Button(searchRow, "搜索当前小节", () =>
        {
            (GetSystemService(InputMethodService) as global::Android.Views.InputMethods.InputMethodManager)?.HideSoftInputFromWindow(input.WindowToken, 0);
            search = input.Text ?? ""; linePage = 0; Render();
        }); PgrUi.Style(local, Xb.Normal, "search", textSp: 14);
        var cross = Button(searchRow, "跨章节搜索", ShowLibrarySearch); PgrUi.Style(cross, Xb.Normal, "archive", textSp: 14);
        foreach (var fit in new[] { local, cross }) { fit.SetSingleLine(true); fit.SetMinWidth(0); fit.SetMinimumWidth(0); DropIconWhenTight(fit); }
        RenderLines(true);
        content.AddView(new View(this) { Background = new ArchiveFooterDrawable(density), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(-1, Dp(150)) { TopMargin = Dp(8) });
        FillLocateColumn(candidateEngine);
    }

    /// <summary>Evidence-chain candidate card: rail node + index, avatar, speaker, 匹配 meter, 需确认, line, context (route / 前 / 后), 采用这句.</summary>
    View CandidateCard(MatchCandidate candidate, int index, bool last, Action adopt)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var rail = new View(this) { Background = new CandidateRailDrawable(density) { Index = index, Last = last }, ImportantForAccessibility = ImportantForAccessibility.No };
        row.AddView(rail, new LinearLayout.LayoutParams(Dp(26), -1));
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.Panel, PgrTheme.Border, PgrTheme.CutMedium, Corners.Diagonal, brackets: PgrTheme.A(PgrTheme.Slate, 150), shadow: true), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium, InsetDp = 3 });
        card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(SpeakerAvatar(candidate.Node.Speaker, 30, index == 1), new LinearLayout.LayoutParams(Dp(30), Dp(30)) { RightMargin = Dp(8) });
        var speaker = new TextView(this) { Text = string.IsNullOrWhiteSpace(candidate.Node.Speaker) ? "旁白" : candidate.Node.Speaker, TextSize = 13 };
        speaker.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); speaker.SetTextColor(PgrTheme.Foreground); speaker.SetSingleLine(true); speaker.Ellipsize = TextUtils.TruncateAt.End;
        head.AddView(speaker, new LinearLayout.LayoutParams(0, -2, 1));
        var score = new TextView(this) { Text = $"匹配 {candidate.Score:P0}", TextSize = 11 }; score.SetTextColor(PgrTheme.Foreground); score.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); PgrTheme.Tabular(score);
        head.AddView(score, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(5) });
        head.AddView(new View(this) { Background = new MeterDrawable(density) { Value = (float)Math.Clamp(candidate.Score, 0, 1) }, ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(40), Dp(6)) { RightMargin = Dp(8) });
        head.AddView(PgrUi.Chip(this, "需确认", PgrUi.Status.Brass));
        card.AddView(head);
        var line = new TextView(this) { Text = candidate.Node.Text, TextSize = 15 }; line.SetTextColor(PgrTheme.Foreground); line.SetLineSpacing(Dp(3), 1);
        card.AddView(line, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6), BottomMargin = Dp(4) });
        if (candidate.Context.Length > 0)
        {
            var rule = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No }; rule.SetBackgroundColor(PgrTheme.A(PgrTheme.Border, 200));
            card.AddView(rule, new LinearLayout.LayoutParams(-1, Dp(1)) { BottomMargin = Dp(4) });
            var parts = candidate.Context.Split('\n');
            var route = new TextView(this) { Text = parts[0], TextSize = 12 }; route.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
            route.SetTextColor(parts[0].Contains("路线：") ? PgrTheme.BrassText : PgrTheme.Steel);
            card.AddView(route);
            if (parts.Length > 1)
            {
                var rest = new TextView(this) { Text = string.Join("\n", parts.Skip(1)), TextSize = 12 }; rest.SetTextColor(PgrTheme.Secondary); rest.SetLineSpacing(Dp(2), 1);
                card.AddView(rest);
            }
        }
        var adoptRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; adoptRow.SetGravity(GravityFlags.End);
        var button = new Button(this) { Text = "采用这句" }; PgrUi.Style(button, index == 1 ? Xb.Primary : Xb.DangerOutline, "chevron", center: true, textSp: 14);
        button.ContentDescription = "采用这句 · " + candidate.ToString().Replace("\n", " ");
        button.SetSingleLine(true); button.SetPadding(Dp(16), 0, Dp(16), 0);
        button.Click += (_, _) => Safe(adopt);
        adoptRow.AddView(button, new LinearLayout.LayoutParams(-2, Dp(48)));
        card.AddView(adoptRow, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        row.AddView(card, new LinearLayout.LayoutParams(0, -2, 1) { BottomMargin = Dp(10) });
        return row;
    }

    // ── Locate tray (shares the story tray host / column; content switches by page) ──────────────────────────────────

    /// <summary>
    /// Short portrait screens (&lt; 680dp tall, e.g. 360×640) and narrow wide-landscape (&lt; 720dp, e.g. 640×360): the expanded
    /// tray / tray column would leave the page only a sliver, so trays
    /// open collapsed (the collapsed bar keeps 「确认当前句」) until the user expands one in this session. Saved choice untouched.
    /// </summary>
    bool ShortScreen => !CompactLayout ? Resources!.Configuration!.ScreenHeightDp < 680 : WideLandscape && Resources!.Configuration!.ScreenWidthDp < 720;
    bool trayUserExpanded, listenUserExpanded;
    bool TrayCollapsed => (ShortScreen && !trayUserExpanded) || TraySetting;
    bool TraySetting => page switch
    {
        2 => session.Engine == null || session.Settings.LocateTrayCollapsed,
        3 => session.Engine == null || session.Settings.HistoryTrayCollapsed,
        4 => session.Settings.SettingsTrayCollapsed,
        6 => session.Engine == null || session.Settings.BranchTrayCollapsed,
        _ => session.Settings.StoryTrayCollapsed,
    };

    LinearLayout BuildLocateExpanded(bool column)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var tray = new LinearLayout(this) { Orientation = Orientation.Vertical };
        if (!column)
        {
            tray.Background = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 16, TideDp = 22, Inscription = false, Watermark = false, ShowEmblem = false };
            tray.SetPadding(Dp(14), Dp(8), Dp(14), Dp(10));
        }
        var t = NewTrayLine(44);
        tray.AddView(CurrentLineBlock(t, 44, true), new LinearLayout.LayoutParams(-1, -2));
        tray.AddView(ConfirmHex(), new LinearLayout.LayoutParams(-1, Dp(54)) { TopMargin = Dp(8) });
        return tray;
    }

    LinearLayout BuildLocateColumnPanel()
    {
        float density = Resources!.DisplayMetrics!.Density;
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        panel.Background = new DarkPanelDrawable(density, DecoTier.L) { Mark = "LOCATE", TideDp = 40, EmblemWidthDp = 170, EmblemRightDp = -30, EmblemBottomDp = 150 };
        panel.SetPadding(Dp(22), Dp(14), Dp(14), Dp(20));
        panel.AddView(PgrUi.En(this, "LOCATE · 定位", 9.5f, PgrArt.GoldLine));
        var title = new TextView(this) { Text = "播放器当前位置", TextSize = 19 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White);
        panel.AddView(title, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        locateColumnSub = new TextView(this) { TextSize = 12 }; locateColumnSub.SetTextColor(PgrTheme.NavText); locateColumnSub.SetSingleLine(true); locateColumnSub.Ellipsize = TextUtils.TruncateAt.End;
        panel.AddView(locateColumnSub);
        var raw = new LinearLayout(this) { Orientation = Orientation.Horizontal }; raw.SetGravity(GravityFlags.CenterVertical);
        raw.Background = PgrTheme.Plate(this, PgrTheme.A(Color.Black, 60), PgrTheme.A(Color.White, 40), PgrTheme.CutSmall, Corners.BottomRight, PgrArt.TideRed, AccentEdge.Left);
        raw.SetPadding(Dp(10), Dp(6), Dp(8), Dp(6));
        raw.AddView(PgrUi.En(this, "OCR RAW", 8.5f, PgrTheme.NavSub), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(8) });
        locateColumnRaw = new TextView(this) { TextSize = 13 }; locateColumnRaw.SetTextColor(Color.White); locateColumnRaw.SetSingleLine(true); locateColumnRaw.Ellipsize = TextUtils.TruncateAt.End;
        raw.AddView(locateColumnRaw, new LinearLayout.LayoutParams(0, -2, 1));
        panel.AddView(raw, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10), BottomMargin = Dp(8) });
        locateColumnMatches = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.AddView(locateColumnMatches, new LinearLayout.LayoutParams(-1, -2));
        panel.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
        panel.AddView(BuildLocateExpanded(true), new LinearLayout.LayoutParams(-1, -2));
        return panel;
    }

    /// <summary>07L-1 column: section · speaker, OCR RAW single line, MATCH · N CANDIDATES + first two candidates (48dp rows).</summary>
    void FillLocateColumn(PlaybackEngine? engine)
    {
        if (locateColumnPanel == null || engine == null) return;
        var sections = engine.Pack.Chapters.SelectMany(c => c.Sections).ToList();
        var (number, name, _) = SectionParts(SectionDisplay.Title(sections, session.SectionId));
        locateColumnHead = (number.Length > 0 ? number + " " : "") + name;
        locateColumnSub.Text = locateColumnHead + (engine.Current is { } n && !string.IsNullOrWhiteSpace(n.Speaker) ? " · " + n.Speaker : "");
        locateColumnRaw.Text = session.OcrText;
        locateColumnMatches.RemoveAllViews();
        // Short landscape heights: the tray keeps priority; candidates stay on the left page.
        int max = Resources!.Configuration!.ScreenHeightDp < 440 ? 1 : 2;
        if (session.Candidates.Count == 0) return;
        locateColumnMatches.AddView(PgrUi.En(this, "MATCH · " + session.Candidates.Count + " CANDIDATES", 8.5f, PgrTheme.NavSub), new LinearLayout.LayoutParams(-2, -2) { BottomMargin = Dp(4) });
        int i = 0;
        foreach (var candidate in session.Candidates.Take(max))
        {
            var c = candidate; i++;
            var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true }; row.SetGravity(GravityFlags.CenterVertical);
            row.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 40)),
                PgrTheme.Plate(this, i == 1 ? PgrTheme.A(PgrArt.TideRed, 36) : PgrTheme.A(Color.White, 14), i == 1 ? PgrArt.TideRed : PgrTheme.A(Color.White, 50), PgrTheme.CutSmall, Corners.BottomRight), null);
            row.SetPadding(Dp(8), Dp(2), Dp(8), Dp(2)); row.SetMinimumHeight(Dp(48));
            row.ContentDescription = "采用这句 · " + c.ToString().Replace("\n", " ");
            var idx = PgrUi.En(this, i.ToString("00"), 11, i == 1 ? PgrArt.TideRed : PgrTheme.NavText); row.AddView(idx, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(6) });
            row.AddView(SpeakerAvatar(c.Node.Speaker, 26, i == 1), new LinearLayout.LayoutParams(Dp(26), Dp(26)) { RightMargin = Dp(6) });
            var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var text = new TextView(this) { Text = c.Node.Text, TextSize = 12.5f }; text.SetTextColor(Color.White); text.SetSingleLine(true); text.Ellipsize = TextUtils.TruncateAt.End; texts.AddView(text);
            var where = new TextView(this) { Text = c.Context.Split('\n')[0], TextSize = 10.5f }; where.SetTextColor(PgrTheme.NavText); where.SetSingleLine(true); where.Ellipsize = TextUtils.TruncateAt.End; texts.AddView(where);
            row.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
            var score = PgrUi.En(this, $"{c.Score:P0}", 11, Color.White); row.AddView(score, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6), RightMargin = Dp(6) });
            row.AddView(PgrUi.Chip(this, "需确认", PgrUi.Status.Brass));
            row.Click += (_, _) => Safe(() => ConfirmOcrCandidate(c, engine));
            locateColumnMatches.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
        }
    }
}
