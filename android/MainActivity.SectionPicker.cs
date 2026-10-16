using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 02-B two-step section panel. Step 1: 普通 / 隐藏 three-column numbered grid (single-segment sections apply at once);
/// step 2: segment cards for a multi-segment section with a back arrow. Wide landscape shows both steps side by side.
/// Callbacks and the stillValid check are unchanged from the original two-level Choose dialogs.
/// </summary>
public sealed partial class MainActivity
{
    static readonly System.Text.RegularExpressions.Regex SectionNumberPattern =
        new(@"^(?<num>[A-Za-z]*\d+(?:-\d+)+[A-Za-z]?)\s*(?<name>.*)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    static readonly System.Text.RegularExpressions.Regex SourceTagPattern =
        new(@"\s*[\[【](?<tag>[^\]】]+)[\]】]\s*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    static bool HiddenSection(string title) => title.Contains("[隐藏剧情]") || title.StartsWith("隐藏 ·", StringComparison.Ordinal);

    /// <summary>Splits a display title into (section number, name, source tag); display only.</summary>
    static (string Number, string Name, string? Source) SectionParts(string title)
    {
        string text = title.Trim(); string? source = null;
        var tag = SourceTagPattern.Match(text);
        if (tag.Success) { source = tag.Groups["tag"].Value.Trim(); text = text[..tag.Index].Trim(); }
        foreach (var prefix in new[] { "普通 ·", "隐藏 ·" })
            if (text.StartsWith(prefix, StringComparison.Ordinal)) text = text[prefix.Length..].Trim();
        var match = SectionNumberPattern.Match(text);
        return match.Success ? (match.Groups["num"].Value, match.Groups["name"].Value.Trim(), source) : ("", text, source);
    }

    void ChooseDisplayedSection(Pack pack, IEnumerable<Section> sections, string title,
        Func<bool> stillValid, Action<Section> selected, string? currentSectionId = null)
    {
        var groups = SectionDisplay.Groups(sections);
        if (groups.Count == 0) { Info(title, "当前没有可选内容。"); return; }
        void Apply(Section section) { if (stillValid()) selected(section); }
        var currentGroup = groups.FirstOrDefault(g => g.Sections.Any(s => s.Id == currentSectionId));
        SectionDisplayGroup? open = null;          // portrait: the group whose segments are shown (step 2)
        SectionDisplayGroup? focus = currentGroup ?? groups.FirstOrDefault(g => g.Sections.Count > 1) ?? groups[0];   // wide: right column
        PgrSheet sheet = null!;
        sheet = new PgrSheet(this, s =>
        {
            s.CustomBody = s.Wide; s.WideWidthDp = 0;
            if (s.Wide)
            {
                var columns = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                s.Body.AddView(columns, new LinearLayout.LayoutParams(-1, -1));
                var left = new LinearLayout(this) { Orientation = Orientation.Vertical };
                columns.AddView(left, new LinearLayout.LayoutParams(Dp(430), -1) { RightMargin = Dp(12) });
                var right = new LinearLayout(this) { Orientation = Orientation.Vertical };
                columns.AddView(right, new LinearLayout.LayoutParams(0, -1, 1));
                SectionGridHeader(left, pack, title, groups.Count);
                var leftScroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
                var grid = new LinearLayout(this) { Orientation = Orientation.Vertical }; leftScroll.AddView(grid);
                SectionGrid(grid, groups, currentSectionId, focus, group =>
                {
                    if (group.Sections.Count == 1) { s.Dismiss(); Apply(group.Sections[0]); return; }
                    focus = group; s.Relayout();
                });
                left.AddView(leftScroll, new LinearLayout.LayoutParams(-1, 0, 1));
                s.Cancel(left);
                if (focus != null)
                {
                    SegmentHeader(right, pack, focus, title, null);
                    var rightScroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
                    var cards = new LinearLayout(this) { Orientation = Orientation.Vertical }; rightScroll.AddView(cards);
                    SegmentCards(cards, pack, focus, currentSectionId, section => { s.Dismiss(); Apply(section); });
                    right.AddView(rightScroll, new LinearLayout.LayoutParams(-1, 0, 1));
                }
                else right.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1));
                s.Cancel(right);
                return;
            }
            if (open == null)
            {
                SectionGridHeader(s.Header, pack, title, groups.Count);
                SectionGrid(s.Body, groups, currentSectionId, null, group =>
                {
                    if (!stillValid()) { s.Dismiss(); return; }
                    if (group.Sections.Count == 1) { s.Dismiss(); Apply(group.Sections[0]); return; }
                    open = group; focus = group; s.Relayout();
                });
            }
            else
            {
                SegmentHeader(s.Header, pack, open, title, () => { open = null; s.Relayout(); });
                SegmentCards(s.Body, pack, open, currentSectionId, section => { s.Dismiss(); Apply(section); });
            }
            s.Cancel();
        });
        sheet.Show();
    }

    void SectionGridHeader(LinearLayout parent, Pack pack, string title, int count)
    {
        string number = ChapterNumber(pack.Title);
        var head = PgrUi.Zone(this, title, "SECTION", number == "—" ? null : "CH." + number, 17);
        parent.AddView(head, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        string chapter = number == "—" ? PackTitle(pack.Title) : $"第{int.Parse(number)}章 {ChapterTitle(pack.Title)}";
        var sub = new TextView(this) { Text = $"{chapter} · {count:00} 个小节 · 标「N 段」的小节点开后再选段", TextSize = 12 };
        sub.SetTextColor(PgrTheme.Secondary); sub.SetPadding(Dp(14), Dp(2), 0, Dp(8)); parent.AddView(sub);
    }

    /// <summary>「普通 MAIN STORY · 主线剧情」/「隐藏 HIDDEN · 隐藏剧情」groups of three-column numbered section cells.</summary>
    void SectionGrid(LinearLayout parent, IReadOnlyList<SectionDisplayGroup> groups, string? currentSectionId, SectionDisplayGroup? focus, Action<SectionDisplayGroup> pick)
    {
        float density = Resources!.DisplayMetrics!.Density;
        foreach (bool hidden in new[] { false, true })
        {
            var members = groups.Where(g => HiddenSection(g.Title) == hidden).ToList();
            if (members.Count == 0) continue;
            var header = new LinearLayout(this) { Orientation = Orientation.Horizontal }; header.SetGravity(GravityFlags.CenterVertical);
            header.SetPadding(0, Dp(8), 0, Dp(6));
            header.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
            var zh = new TextView(this) { Text = hidden ? "隐藏" : "普通", TextSize = 14 }; zh.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zh.SetTextColor(PgrTheme.Foreground); header.AddView(zh);
            header.AddView(PgrUi.En(this, hidden ? "HIDDEN" : "MAIN STORY", 9), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(7) });
            var src = new TextView(this) { Text = hidden ? " · 隐藏剧情" : " · 主线剧情", TextSize = 10 }; src.SetTextColor(PgrTheme.Muted); header.AddView(src);
            var rule = new View(this); rule.SetBackgroundColor(PgrTheme.Border);
            header.AddView(rule, new LinearLayout.LayoutParams(0, Math.Max(1, Dp(1) * 3 / 4), 1) { LeftMargin = Dp(8), RightMargin = Dp(8) });
            header.AddView(PgrUi.Readout(this, members.Count.ToString("00")));
            parent.AddView(header);
            LinearLayout? row = null;
            for (int i = 0; i < members.Count; i++)
            {
                var group = members[i];
                if (i % 3 == 0) { row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; parent.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) }); }
                bool current = group.Sections.Any(s => s.Id == currentSectionId);
                bool focused = focus != null && ReferenceEquals(group, focus);
                var (number, name, _) = SectionParts(group.Title);
                var cell = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
                cell.SetPadding(Dp(10), Dp(6), Dp(8), Dp(8));
                cell.Background = new GridCellDrawable(density) { IsCurrent = current, Accent = focused && !current ? PgrTheme.Slate : null, Line = focused && !current ? PgrTheme.Slate : PgrTheme.Border };
                cell.ContentDescription = group.Title + (group.Sections.Count > 1 ? $"，{group.Sections.Count} 段" : "") + (current ? "，当前小节" : "");
                cell.Click += (_, _) => Safe(() => pick(group));
                var top = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
                var num = PgrUi.Outline(this, number.Length > 0 ? number : (i + 1).ToString("00"), 17, current ? PgrTheme.Red : PgrTheme.Secondary, 1f);
                num.SetSingleLine(true); top.AddView(num, new LinearLayout.LayoutParams(0, -2, 1));
                if (group.Sections.Count > 1)
                {
                    var parts = new TextView(this) { Text = $"{group.Sections.Count} 段", TextSize = 10 }; parts.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold);
                    parts.SetTextColor(current ? PgrTheme.RedText : PgrTheme.Steel); top.AddView(parts);
                }
                cell.AddView(top);
                var label = new TextView(this) { Text = name.Length > 0 ? name : group.Title, TextSize = 13, ImportantForAccessibility = ImportantForAccessibility.No };
                label.SetTypeface(current ? PgrTheme.SansBold : PgrTheme.SansMedium, current ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                label.SetTextColor(current ? PgrTheme.RedText : PgrTheme.Foreground); label.SetMaxLines(2); label.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                label.SetPadding(0, Dp(2), 0, 0); cell.AddView(label);
                cell.SetMinimumHeight(Dp(56));
                row!.AddView(cell, new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = Dp(i % 3 == 0 ? 0 : 3), RightMargin = Dp(i % 3 == 2 ? 0 : 3) });
            }
            if (row != null) for (int k = members.Count % 3; k > 0 && k < 3; k++) row.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1) { LeftMargin = Dp(3), RightMargin = Dp(3) });
        }
    }

    /// <summary>Step-2 header: back arrow (portrait), group title + SEGMENT + 「N 段」, subtitle, cover band with cast strip.</summary>
    void SegmentHeader(LinearLayout parent, Pack pack, SectionDisplayGroup group, string dialogTitle, Action? back)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical);
        if (back != null)
        {
            var arrow = new ImageButton(this) { ContentDescription = "返回上一步" };
            arrow.SetImageDrawable(new PgrIconDrawable("back", PgrTheme.Foreground, density, 20));
            arrow.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)), null, new ColorDrawable(Color.White));
            arrow.Click += (_, _) => Safe(back);
            row.AddView(arrow, new LinearLayout.LayoutParams(Dp(48), Dp(48)) { LeftMargin = -Dp(12) });
        }
        row.AddView(PgrUi.Zone(this, group.Title, "SEGMENT", $"{group.Sections.Count} 段", 16), new LinearLayout.LayoutParams(0, -2, 1));
        parent.AddView(row, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(back == null ? 4 : 0) });
        var sub = new TextView(this) { Text = $"{dialogTitle} · 本小节分为 {group.Sections.Count} 段", TextSize = 12 };
        sub.SetTextColor(PgrTheme.Secondary); sub.SetPadding(Dp(14), Dp(2), 0, Dp(8)); parent.AddView(sub);
        // Cover band: chapter cover + slate veil (S deco), cast strip, outlined section number.
        var band = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.No };
        var face = new CoverBandDrawable(density, DecoTier.S) { VeilAlpha = 200, CutTopRightDp = 14 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, pack.Title, pack.Id, Dp(480));
        var (number, _, _) = SectionParts(group.Title);
        if (number.Length > 0)
            band.AddView(PgrUi.Outline(this, number, 38, Color.White, 1.2f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(12) });
        band.AddView(CastStrip(pack, group.Sections.Select(s => s.Id)), new FrameLayout.LayoutParams(-1, -2, GravityFlags.Start | GravityFlags.Bottom) { LeftMargin = Dp(10), RightMargin = Dp(10), BottomMargin = Dp(8) });
        parent.AddView(band, new LinearLayout.LayoutParams(-1, Dp(84)) { BottomMargin = Dp(8) });
    }

    /// <summary>
    /// 「出场角色 CAST」strip: the fixed label, then every speaker of the given sections (avatar + name, narrator = emblem) in a
    /// horizontally scrollable row (CastScroll: no scrollbar, right-edge fade while more chips follow). Give it a bounded width.
    /// </summary>
    LinearLayout CastStrip(Pack pack, IEnumerable<string> sectionIds, int max = int.MaxValue)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var ids = sectionIds.ToHashSet(StringComparer.Ordinal);
        var speakers = pack.Nodes.Where(n => !n.Archived && n.Kind == "line" && ids.Contains(n.SectionId))
            .Select(n => string.IsNullOrWhiteSpace(n.Speaker) ? "旁白" : n.Speaker.Trim()).Distinct().Take(max).ToList();
        var strip = new LinearLayout(this) { Orientation = Orientation.Horizontal }; strip.SetGravity(GravityFlags.CenterVertical);
        var label = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var zh = new TextView(this) { Text = "出场角色", TextSize = 10 }; zh.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zh.SetTextColor(PgrTheme.OnPlate); label.AddView(zh);
        label.AddView(PgrUi.En(this, "CAST", 7.5f, PgrTheme.NavSub));
        strip.AddView(label, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(10) });
        var chips = new LinearLayout(this) { Orientation = Orientation.Horizontal }; chips.SetGravity(GravityFlags.CenterVertical);
        foreach (var speaker in speakers)
        {
            var frame = new PortraitFrameDrawable(density) { ShowSerial = false, Name = speaker };
            var avatar = new View(this) { Background = frame };
            PortraitStore.Request(avatar, speaker, standee: false, maxPx: Dp(48), image => { if (frame.Name == speaker) frame.Image = image; });
            chips.AddView(avatar, new LinearLayout.LayoutParams(Dp(24), Dp(24)) { RightMargin = Dp(4) });
            var name = new TextView(this) { Text = speaker, TextSize = 10 }; name.SetTextColor(PgrTheme.OnPlate); name.SetSingleLine(true);
            name.SetShadowLayer(Dp(3), 0, 0, PgrTheme.Ink);
            chips.AddView(name, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(8) });
        }
        strip.AddView(CastScroll.Wrap(chips), new LinearLayout.LayoutParams(0, -2, 1));
        return strip;
    }

    /// <summary>Segment cards: index cell, 40dp avatar (or brass diamond for menus), SegmentLabel text, type chips; then the footnote.</summary>
    void SegmentCards(LinearLayout parent, Pack pack, SectionDisplayGroup group, string? currentSectionId, Action<Section> pick)
    {
        float density = Resources!.DisplayMetrics!.Density;
        for (int i = 0; i < group.Sections.Count; i++)
        {
            var section = group.Sections[i];
            string label = SectionDisplay.SegmentLabel(pack, group, section);
            string body = label.Length > 5 && label[2..5] == " · " ? label[5..] : label;
            var nodes = pack.Nodes.Where(n => !n.Archived && n.SectionId == section.Id).ToList();
            var start = nodes.FirstOrDefault(n => n.Id == section.StartId);
            var line = start?.Kind == "line" ? start : nodes.FirstOrDefault(n => n.Kind == "line");
            bool menu = start?.Kind == "choice" || (line == null && nodes.Any(n => n.Kind == "choice"));
            bool current = section.Id == currentSectionId;
            var card = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
            card.SetGravity(GravityFlags.CenterVertical); card.SetPadding(0, Dp(8), Dp(10), Dp(8)); card.SetMinimumHeight(Dp(64));
            card.Background = new GridCellDrawable(density) { IsCurrent = current, IndexWidthDp = 46, Fill = menu ? Color.Argb(245, 0xFB, 0xF8, 0xF1) : Color.Argb(240, 255, 255, 255), Line = menu ? PgrTheme.A(PgrTheme.Brass, 170) : PgrTheme.Border };
            card.ContentDescription = label;
            card.Click += (_, _) => Safe(() => pick(section));
            var index = new TextView(this) { Text = (i + 1).ToString("00"), TextSize = 17, Gravity = GravityFlags.Center, ImportantForAccessibility = ImportantForAccessibility.No };
            index.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); index.SetTextColor(current ? PgrTheme.RedText : PgrTheme.Secondary);
            card.AddView(index, new LinearLayout.LayoutParams(Dp(46), -2));
            if (menu)
            {
                var diamond = new View(this) { Background = new DiamondMark(density, PgrTheme.Brass), ImportantForAccessibility = ImportantForAccessibility.No };
                card.AddView(diamond, new LinearLayout.LayoutParams(Dp(40), Dp(16)) { LeftMargin = Dp(10), RightMargin = Dp(10) });
            }
            else
            {
                string speaker = string.IsNullOrWhiteSpace(line?.Speaker) ? "旁白" : line!.Speaker.Trim();
                var frame = new PortraitFrameDrawable(density) { ShowSerial = false, Name = speaker };
                var avatar = new View(this) { Background = frame, ImportantForAccessibility = ImportantForAccessibility.No };
                PortraitStore.Request(avatar, speaker, standee: false, maxPx: Dp(80), image => { if (frame.Name == speaker) frame.Image = image; });
                card.AddView(avatar, new LinearLayout.LayoutParams(Dp(40), Dp(40)) { LeftMargin = Dp(10), RightMargin = Dp(10) });
            }
            var texts = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
            var text = new TextView(this) { Text = body, TextSize = 14 }; text.SetTextColor(PgrTheme.Foreground); text.SetMaxLines(2);
            text.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; text.SetLineSpacing(Dp(2), 1); texts.AddView(text);
            var chips = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            if (section.Id.Contains("-position-", StringComparison.Ordinal)) chips.AddView(PgrUi.Chip(this, "当前画面定位", PgrUi.Status.Teal), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(6) });
            else if (System.Text.RegularExpressions.Regex.IsMatch(section.Id, "-m[0-9]{3}$")) chips.AddView(PgrUi.Chip(this, "条件路线", PgrUi.Status.Brass), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(6) });
            if (menu) chips.AddView(PgrUi.Chip(this, "互动选择", PgrUi.Status.Brass));
            if (chips.ChildCount > 0) texts.AddView(chips, new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(5) });
            card.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
            card.AddView(Icon("chevron", current ? PgrTheme.Red : PgrTheme.Muted, 16), new LinearLayout.LayoutParams(Dp(18), Dp(18)) { LeftMargin = Dp(6) });
            parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
        }
        var foot = new TextView(this) { Text = $"本小节共 {group.Sections.Count} 段 · 每段显示开头一句或选项", TextSize = 11 };
        foot.SetTextColor(PgrTheme.Secondary); foot.SetPadding(Dp(4), Dp(4), 0, Dp(6));
        foot.SetCompoundDrawablesRelativeWithIntrinsicBounds(new BarMark(density), null, null, null); foot.CompoundDrawablePadding = Dp(7);
        parent.AddView(foot);
    }

    /// <summary>Brass interaction diamond (segment cards for menus).</summary>
    sealed class DiamondMark(float density, Color color) : PgrShapeDrawable(density)
    {
        public override void Draw(Canvas canvas)
        {
            var b = Bounds; float cx = b.ExactCenterX(), cy = b.ExactCenterY(), s = Math.Min(b.Height(), b.Width()) / 2f;
            path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(color); canvas.DrawPath(path, paint);
        }
    }
}
