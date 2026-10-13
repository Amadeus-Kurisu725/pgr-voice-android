using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 04-B 剧情资料: reading-first sheet. 84dp dark cover band header, long archive-paper body (read only, selectable), dark M
/// find plate at the bottom (result bar + match ticks + input + 查找 / 下一处); × closes. Wide landscape: paper on the left,
/// 380dp column on the right (band + paper + find plate). Original texts, find logic and read-only behaviour unchanged.
/// </summary>
public sealed partial class MainActivity
{
    void ShowStoryReference()
    {
        if (session.Engine is not { } engine)
        { Info("内置剧情文本", "请先打开章节，再查看当前小节的剧情文本。"); return; }
        // 固定打开时的章节与小节；查找、翻阅、关闭都不提交播放或定位命令。
        var pack = engine.Pack; string sectionId = session.SectionId;
        string sectionText = BundledStoryReference.GetSectionText(pack, sectionId);
        string sectionTitle = SectionDisplay.Title(pack.Chapters.SelectMany(c => c.Sections).ToList(), sectionId);
        string summary = BundledStoryReference.GetSummary(pack);
        float density = Resources!.DisplayMetrics!.Density;
        string query = "", searched = ""; var matches = new List<int>(); int currentMatch = -1; const string ReadOnlyNote = "只读资料。查看和查找不会改变配音位置。"; string resultText = ReadOnlyNote; bool noteBreak = false;
        bool open = true;
        TextView body = null!, result = null!; ScrollView scroll = null!; EditText input = null!; View ticks = null!; TextView matchReadout = null!;

        void ApplyHighlights()
        {
            var span = new SpannableString(sectionText);
            for (int i = 0; i < matches.Count; i++)
            {
                int s = matches[i], e = Math.Min(sectionText.Length, s + searched.Length);
                if (i == currentMatch)
                {
                    span.SetSpan(new BackgroundColorSpan(PgrTheme.Red), s, e, SpanTypes.ExclusiveExclusive);
                    span.SetSpan(new ForegroundColorSpan(Color.White), s, e, SpanTypes.ExclusiveExclusive);
                }
                else
                {
                    span.SetSpan(new UnderlineSpan(), s, e, SpanTypes.ExclusiveExclusive);
                    span.SetSpan(new ForegroundColorSpan(PgrTheme.RedText), s, e, SpanTypes.ExclusiveExclusive);
                }
            }
            body.SetText(span, TextView.BufferType.Spannable);
        }
        void UpdateResult()
        {
            // 0.4.6: when the read-only note does not fit one line (landscape sheet column, large font) it breaks after 「只读资料。」
            // instead of leaving 「置。」 alone on the second line.
            var span = new SpannableStringBuilder(resultText == ReadOnlyNote && noteBreak ? "只读资料。\n查看和查找不会改变配音位置。" : resultText);
            int a = resultText.IndexOf('【'), b = resultText.IndexOf('】');
            if (a >= 0 && b > a)
            {
                span.SetSpan(new ForegroundColorSpan(PgrArt.TideRed), a, b + 1, SpanTypes.ExclusiveExclusive);
                span.SetSpan(new StyleSpan(TypefaceStyle.Bold), a, b + 1, SpanTypes.ExclusiveExclusive);
            }
            int nl = resultText.IndexOf('\n');
            if (nl > 0 && matches.Count > 0) span.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, nl, SpanTypes.ExclusiveExclusive);
            result.TextFormatted = span;
            bool hasMatch = matches.Count > 0 && currentMatch >= 0;
            ticks.Visibility = hasMatch ? ViewStates.Visible : ViewStates.Gone;
            matchReadout.Visibility = hasMatch ? ViewStates.Visible : ViewStates.Gone;
            if (hasMatch && ticks.Background is SegmentsDrawable seg) { seg.Count = matches.Count; seg.Mark = currentMatch; seg.Done = 0; seg.InvalidateSelf(); }
            matchReadout.Text = hasMatch ? $"MATCH {currentMatch + 1:00} / {matches.Count:00}" : "";
            bool warn = resultText.StartsWith("本小节未找到", StringComparison.Ordinal) || resultText.StartsWith("请输入", StringComparison.Ordinal);
            result.SetCompoundDrawablesRelativeWithIntrinsicBounds(hasMatch ? null : warn ? new PgrIconDrawable("warning", PgrArt.GoldLine, density, 14) : new LockGlyph(density, PgrArt.MistLine), null, null, null);
            result.CompoundDrawablePadding = Dp(8);
            result.SetTextColor(warn ? PgrArt.GoldLine : Color.ParseColor("#E4E8EC"));
        }
        void ScrollToMatch()
        {
            if (currentMatch < 0 || currentMatch >= matches.Count) return;
            int position = matches[currentMatch];
            body.Post(() =>
            {
                if (!open || !alive || body.Layout is not { } layout) return;
                scroll.SmoothScrollTo(0, Math.Max(0, layout.GetLineTop(layout.GetLineForOffset(position)) - Dp(8)));
            });
        }
        void Find(bool next)
        {
            if (!open || !alive) return;
            string q = query.Trim();
            if (q.Length == 0) { resultText = "请输入本小节中的台词或角色名。"; UpdateResult(); return; }
            if (!next || q != searched)
            {
                searched = q; matches.Clear(); currentMatch = -1;
                int offset = 0;
                while (offset <= sectionText.Length - q.Length)
                {
                    int found = sectionText.IndexOf(q, offset, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) break;
                    matches.Add(found); offset = found + q.Length;
                }
            }
            if (matches.Count == 0) { resultText = "本小节未找到“" + q + "”。"; ApplyHighlights(); UpdateResult(); return; }
            currentMatch = (currentMatch + 1) % matches.Count;
            int position = matches[currentMatch];
            int before = Math.Max(0, position - 45), after = Math.Min(sectionText.Length, position + q.Length + 65);
            string excerpt = (before > 0 ? "…" : "") + sectionText[before..position].Replace('\n', ' ') +
                "【" + sectionText.Substring(position, q.Length) + "】" + sectionText[(position + q.Length)..after].Replace('\n', ' ') +
                (after < sectionText.Length ? "…" : "");
            resultText = $"第 {currentMatch + 1}/{matches.Count} 处\n" + excerpt;
            ApplyHighlights(); UpdateResult(); ScrollToMatch();
        }

        var sheet = new PgrSheet(this, s =>
        {
            bool wide = s.Wide; s.CustomBody = true; s.WideWidthDp = -1;
            var titleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; titleRow.SetGravity(GravityFlags.CenterVertical);
            titleRow.AddView(PgrUi.Zone(this, "内置剧情文本 · 当前小节", "STORY REFERENCE", null, 16, "内置剧情文本"), new LinearLayout.LayoutParams(0, -2, 1));
            titleRow.AddView(CloseButton(() => s.Dismiss()), new LinearLayout.LayoutParams(Dp(48), Dp(48)) { RightMargin = -Dp(10) });
            // Paper body.
            var paper = new FrameLayout(this);
            paper.Background = new ArchivePaperDrawable(density) { Fill = Color.Argb(235, 250, 251, 252), EmblemWidthDp = 210, EmblemRightDp = 20, EmblemBottomDp = 70, EmblemOpacity = .7f, TideDp = 46, CutBottomRightDp = 12 };
            var (secNum, _, _) = SectionParts(sectionTitle);
            string secShort = secNum.Contains('-') ? secNum[(secNum.LastIndexOf('-') + 1)..] : secNum;
            if (secShort.Length > 0)
            {
                var water = PgrUi.Outline(this, secShort.PadLeft(2, '0'), 120, PgrTheme.A(PgrTheme.Slate, 46), 1.4f);
                paper.AddView(water, new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(18) });
            }
            var vertical = new VerticalTextView(this, "GRAY RAVEN ARCHIVE · BABYLONIA RECORD", PgrTheme.A(PgrTheme.Slate, 120));
            paper.AddView(vertical, new FrameLayout.LayoutParams(Dp(16), -1, GravityFlags.End) { TopMargin = Dp(44), BottomMargin = Dp(50), RightMargin = Dp(4) });
            var paperColumn = new LinearLayout(this) { Orientation = Orientation.Vertical };
            paperColumn.AddView(ArchiveStrip(pack, secNum), new LinearLayout.LayoutParams(-1, Dp(30)));
            scroll = new ScrollView(this) { VerticalScrollBarEnabled = true };
            body = new TextView(this) { TextSize = 14 }; body.SetTextColor(PgrTheme.Foreground); body.SetTextIsSelectable(true);
            body.SetPadding(Dp(14), Dp(10), Dp(28), Dp(52)); body.SetLineSpacing(Dp(6), 1);
            body.SetText(sectionText, TextView.BufferType.Spannable);
            scroll.AddView(body, new FrameLayout.LayoutParams(-1, -2));
            paperColumn.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
            paper.AddView(paperColumn, new FrameLayout.LayoutParams(-1, -1));
            // Find plate (dark M).
            var plate = new LinearLayout(this) { Orientation = Orientation.Vertical };
            plate.Background = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 16, TideDp = 22, Inscription = false, Watermark = false };
            plate.SetPadding(Dp(12), Dp(10), Dp(12), Dp(12));
            var resultHead = new LinearLayout(this) { Orientation = Orientation.Horizontal }; resultHead.SetGravity(GravityFlags.CenterVertical);
            ticks = new View(this) { Background = new SegmentsDrawable(density) { SegmentDp = 10 }, ImportantForAccessibility = ImportantForAccessibility.No };
            resultHead.AddView(ticks, new LinearLayout.LayoutParams(0, Dp(8), 1) { RightMargin = Dp(8) });
            matchReadout = PgrUi.En(this, "", 9, PgrTheme.NavText); PgrTheme.Tabular(matchReadout);
            resultHead.AddView(matchReadout);
            plate.AddView(resultHead, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(2) });
            result = new TextView(this) { TextSize = 12 }; result.SetMaxLines(3); result.Ellipsize = TextUtils.TruncateAt.End; result.SetLineSpacing(Dp(2), 1);
            result.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
            result.LayoutChange += (_, _) =>
            {
                if (noteBreak || resultText != ReadOnlyNote || result.LineCount <= 1) return;
                noteBreak = true; result.Post(() => { if (open && alive) UpdateResult(); });
            };
            result.Background = PgrTheme.Plate(this, PgrTheme.A(Color.White, 14), PgrTheme.A(PgrArt.MistLine, 60), 6, Corners.BottomRight);
            result.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
            plate.AddView(result, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
            input = new EditText(this) { Hint = "在本小节查找台词或角色", Text = query, ImeOptions = global::Android.Views.InputMethods.ImeAction.Search };
            input.SetSingleLine(true); input.TextChanged += (_, _) => query = input.Text ?? "";
            input.EditorAction += (_, e) => { if (e.ActionId == global::Android.Views.InputMethods.ImeAction.Search) { Safe(() => Find(false)); e.Handled = true; } else e.Handled = false; };
            plate.AddView(SearchInputRow(input, true), new LinearLayout.LayoutParams(-1, Dp(50)) { BottomMargin = Dp(8) });
            var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var find = new Button(this) { Text = "查找" }; PgrUi.Style(find, Xb.Primary, "search", "FIND", wing: true); find.Click += (_, _) => Safe(() => Find(false));
            actions.AddView(find, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1) { RightMargin = Dp(8) });
            var next = new Button(this) { Text = "下一处" }; PgrUi.Style(next, Xb.Dark, "chevron_down", "NEXT"); next.Click += (_, _) => Safe(() => Find(true));
            actions.AddView(next, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1));
            DropIconWhenTight(find); DropIconWhenTight(next); // 0.4.6: tight right column (640×360 landscape) drops icon → FIND/NEXT tag instead of wrapping 「查/找」「下一/处」
            plate.AddView(actions);
            var band = StoryReferenceBand(pack, sectionTitle, summary, wide);
            if (!wide)
            {
                s.Body.AddView(titleRow, new LinearLayout.LayoutParams(-1, -2));
                s.Body.AddView(band, new LinearLayout.LayoutParams(-1, Dp(84)) { BottomMargin = Dp(8) });
                s.Body.AddView(paper, new LinearLayout.LayoutParams(-1, 0, 1));
                s.Body.AddView(plate, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = -Dp(16), RightMargin = -Dp(16), BottomMargin = -Dp(12), TopMargin = Dp(6) });
            }
            else
            {
                var split = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                var left = new LinearLayout(this) { Orientation = Orientation.Vertical };
                left.AddView(titleRow, new LinearLayout.LayoutParams(-1, -2));
                left.AddView(paper, new LinearLayout.LayoutParams(-1, 0, 1));
                split.AddView(left, new LinearLayout.LayoutParams(0, -1, 1));
                var right = new LinearLayout(this) { Orientation = Orientation.Vertical };
                right.AddView(band, new LinearLayout.LayoutParams(-1, Dp(84)) { BottomMargin = Dp(8) });
                var blank = new View(this) { Background = new ArchivePaperDrawable(density) { Fill = Color.Argb(200, 250, 251, 252), EmblemWidthDp = 110, EmblemRightDp = 120, EmblemBottomDp = 6, EmblemOpacity = .5f, TideDp = 30 }, ImportantForAccessibility = ImportantForAccessibility.No };
                right.AddView(blank, new LinearLayout.LayoutParams(-1, 0, 1) { BottomMargin = Dp(8) });
                right.AddView(plate, new LinearLayout.LayoutParams(-1, -2));
                split.AddView(right, new LinearLayout.LayoutParams(Dp(Math.Clamp(Resources!.Configuration!.ScreenWidthDp * 45 / 100, 280, 380)), -1) { LeftMargin = Dp(14) });
                s.Body.AddView(split, new LinearLayout.LayoutParams(-1, -1));
            }
            ApplyHighlights(); UpdateResult(); ScrollToMatch();
        });
        sheet.Dismissed += () => open = false;
        sheet.Show();
    }

    /// <summary>84dp dark cover band: chapter title (= 资料标题), section, 「内置 N 个小节、M 条节点资料。」, outlined chapter number.</summary>
    View StoryReferenceBand(Pack pack, string sectionTitle, string summary, bool wide)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.S) { VeilAlpha = 215, CutTopRightDp = 14 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, pack.Title, pack.Id, Dp(480));
        string number = ChapterNumber(pack.Title);
        if (number != "—") band.AddView(PgrUi.Outline(this, number, 46, PgrTheme.A(Color.White, 220), 1.3f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(12) });
        var lines = summary.Split('\n');
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var title = new TextView(this) { Text = PackTitle(pack.Title), TextSize = 18 }; title.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); title.SetTextColor(Color.White); title.SetSingleLine(true); title.Ellipsize = TextUtils.TruncateAt.End;
        texts.AddView(title);
        var sec = new TextView(this) { Text = sectionTitle, TextSize = 12 }; sec.SetTextColor(PgrTheme.NavText); sec.SetSingleLine(true); sec.Ellipsize = TextUtils.TruncateAt.End; texts.AddView(sec);
        // 摘要：首行资料标题与章名相同，并入章名；第二行统计（数字强调）。
        string stat = lines.Length > 1 ? lines[1] : lines[0];
        var statSpan = new SpannableString(stat);
        int k = 0;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(stat, @"\d+"))
        {
            statSpan.SetSpan(new ForegroundColorSpan(k++ == 0 ? Color.White : PgrArt.TideRed), m.Index, m.Index + m.Length, SpanTypes.ExclusiveExclusive);
            statSpan.SetSpan(new StyleSpan(TypefaceStyle.Bold), m.Index, m.Index + m.Length, SpanTypes.ExclusiveExclusive);
            statSpan.SetSpan(new RelativeSizeSpan(1.25f), m.Index, m.Index + m.Length, SpanTypes.ExclusiveExclusive);
        }
        var statView = new ShrinkFitText(this, 12, 10.5f) { TextFormatted = statSpan }; statView.SetTextColor(PgrTheme.OnPlate); statView.SetMaxLines(1); statView.Ellipsize = TextUtils.TruncateAt.End; texts.AddView(statView);
        band.AddView(texts, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Start | GravityFlags.CenterVertical) { LeftMargin = Dp(14), RightMargin = Dp(wide ? 84 : 96) });
        var red = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No }; red.SetBackgroundColor(PgrTheme.Red);
        band.AddView(red, new FrameLayout.LayoutParams(Dp(44), Dp(3), GravityFlags.Bottom | GravityFlags.Start));
        band.ContentDescription = PackTitle(pack.Title) + " · " + sectionTitle + "。" + stat;
        return band;
    }

    /// <summary>Dark archive strip on the paper: ARCHIVE | PGR · 05-06 | 本小节全文 | READ ONLY.</summary>
    View ArchiveStrip(Pack pack, string sectionNumber)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var strip = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        strip.SetGravity(GravityFlags.CenterVertical);
        strip.Background = new DarkPanelDrawable(density, DecoTier.S) { Inscription = false, Watermark = false, ShowTide = false, Ticks = false };
        var tag = PgrUi.En(this, "ARCHIVE", 9.5f, Color.White); tag.SetBackgroundColor(PgrTheme.RedFill); tag.Gravity = GravityFlags.Center; tag.SetPadding(Dp(10), 0, Dp(10), 0);
        strip.AddView(tag, new LinearLayout.LayoutParams(-2, -1));
        string chapter = ChapterNumber(pack.Title);
        var parts = sectionNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        string code = parts.Length >= 2 && int.TryParse(parts[0], out int a) && int.TryParse(parts[^1], out int b) ? $"{a:00}-{b:00}" : chapter != "—" ? chapter + (sectionNumber.Length > 0 ? "-" + sectionNumber : "") : sectionNumber;
        strip.AddView(PgrUi.En(this, "PGR · " + code, 9.5f, PgrTheme.OnPlate), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(10), RightMargin = Dp(10) });
        var full = new TextView(this) { Text = "本小节全文", TextSize = 11 }; full.SetTextColor(PgrTheme.OnPlate); full.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); full.SetSingleLine(true); full.Ellipsize = TextUtils.TruncateAt.End;
        strip.AddView(full, new LinearLayout.LayoutParams(0, -2, 1));
        var ro = PgrUi.En(this, "READ ONLY", 9, PgrTheme.NavText);
        ro.SetCompoundDrawablesRelativeWithIntrinsicBounds(new LockGlyph(density, PgrTheme.NavText), null, null, null); ro.CompoundDrawablePadding = Dp(4);
        strip.AddView(ro, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(10) });
        return strip;
    }
}

/// <summary>Decorative vertical caption (rotated 90°), hidden from TalkBack.</summary>
public sealed class VerticalTextView : View
{
    readonly string text; readonly Paint paint = new(PaintFlags.AntiAlias);
    public VerticalTextView(global::Android.Content.Context context, string text, Color color) : base(context)
    {
        this.text = text; ImportantForAccessibility = ImportantForAccessibility.No;
        paint.Color = color; paint.TextSize = 9 * context.Resources!.DisplayMetrics!.ScaledDensityCompat(); paint.SetTypeface(PgrTheme.Condensed); paint.LetterSpacing = .3f;
    }
    protected override void OnDraw(Canvas canvas)
    {
        // 0.4.6: on short columns draw only the whole « · »-separated parts that fit (「GRAY RAVEN ARCHIVE」) instead of a cut 「· BA」.
        string shown = text;
        if (paint.MeasureText(shown) > Height)
        {
            var parts = text.Split(" · "); shown = "";
            for (int n = parts.Length - 1; n >= 1 && shown.Length == 0; n--)
            { string t = string.Join(" · ", parts.Take(n)); if (paint.MeasureText(t) <= Height) shown = t; }
            if (shown.Length == 0) return;
        }
        canvas.Save(); canvas.Rotate(90); canvas.Translate(0, -Width / 2f + paint.TextSize / 3);
        canvas.DrawText(shown, 0, 0, paint); canvas.Restore();
    }
}

static class DisplayMetricsExtensions
{
    public static float ScaledDensityCompat(this global::Android.Util.DisplayMetrics m) =>
        global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp, 1, m);
}
