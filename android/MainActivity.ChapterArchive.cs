using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Packages;

namespace PgrVoice.AndroidApp;

/// <summary>01-B chapter page: category name plate, current-chapter cover hero, listening entrance, two / three column cover tiles.</summary>
public sealed partial class MainActivity
{
    void Chapters()
    {
        bool wide = ChapterSplit;
        var packages = session.Packages.List();
        string categoryName = UpdateChapterCategory(packages, wide ? splitLeftBar : chapterCategoryBar);
        var visiblePackages = packages.Where(p => ChapterCatalog.Category(p.PackId, p.Title) == categoryName).ToArray();
        // Wide landscape: left 372dp column (hero / resume / continue / listening), right column = tiles + import; each scrolls.
        if (wide) content = splitLeft;
        if (visiblePackages.Length > 0) ArchiveHero(packages.Count, wide, visiblePackages[0]);
        ListeningEntrance();
        if (wide) content = splitRight;
        // Right column width (wide): screen − 80dp rail − 372dp left column − 14dp gap. On ~731dp phones it is only ~200dp,
        // so the heading stacks (import key under the zone title) and the tiles drop to fewer columns instead of collapsing.
        int rightDp = Resources!.Configuration!.ScreenWidthDp - 80 - 386;
        bool narrowRight = wide && rightDp < 330;
        var heading = Row(content); heading.SetGravity(GravityFlags.CenterVertical);
        if (narrowRight) heading.Orientation = Orientation.Vertical;
        heading.AddView(PgrUi.Zone(this, "章节配音包", "VOICE PACKS", $"{visiblePackages.Length:00} PACKS"), narrowRight ? new LinearLayout.LayoutParams(-1, -2) : new LinearLayout.LayoutParams(0, -2, 1));
        var import = new Button(this) { Text = importCancellation != null ? "取消导入" : "导入 ZIP" };
        PgrUi.Style(import, Xb.Dark, "import", textSp: 13);
        import.Click += (_, _) => Safe(() => { if (importCancellation != null) importCancellation.Cancel(); else SelectFile(ImportRequest, "application/zip"); });
        heading.AddView(import, narrowRight ? new LinearLayout.LayoutParams(-1, Dp(PgrTheme.Touch)) { TopMargin = Dp(6) } : new LinearLayout.LayoutParams(-2, Dp(PgrTheme.Touch)) { LeftMargin = Dp(8) });
        var hint = new TextView(this) { Text = "可长按多选 ZIP，一次导入多个章节。", TextSize = 12 }; hint.SetTextColor(PgrTheme.Secondary); hint.SetPadding(Dp(14), Dp(2), 0, Dp(8));
        content.AddView(hint);
        // Archive-paper list area: grid + scan, orbit arcs, F1b emblem, inscription and Red Tide band along the bottom.
        var paper = new LinearLayout(this) { Orientation = Orientation.Vertical };
        paper.Background = new ArchivePaperDrawable(Resources!.DisplayMetrics!.Density) { EmblemWidthDp = 150, EmblemRightDp = 10, EmblemBottomDp = 64, TideDp = 56 };
        paper.SetPadding(Dp(8), Dp(10), Dp(8), Dp(72));
        content.AddView(paper, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
        if (visiblePackages.Length == 0)
        {
            var empty = Card(paper); EmptyDeco(empty); empty.AddView(PgrTheme.Eyebrow(this, "EMPTY · 0 PACKS")); var caption = PgrTheme.Apply(Text("暂无章节", 18), PgrTheme.Role.Heading); empty.AddView(caption);
            var help = Text("本分类还没有导入章节，可点“导入 ZIP”添加。", 14); help.SetTextColor(PgrTheme.Secondary); empty.AddView(help);
        }
        int columns = !wide ? 2 : rightDp >= 520 ? 3 : rightDp >= 360 ? 2 : 1; // < 360dp: 2 tiles would cut titles to 「源…」
        LinearLayout? row = null;
        for (int i = 0; i < visiblePackages.Length; i++)
        {
            if (i % columns == 0) { row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; paper.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) }); }
            row!.AddView(ChapterTile(visiblePackages[i]), new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = Dp(i % columns == 0 ? 0 : 4), RightMargin = Dp(i % columns == columns - 1 ? 0 : 4) });
        }
        if (row != null) for (int k = visiblePackages.Length % columns; k > 0 && k < columns; k++)
            // Filler must also match the row height: a fixed 1px filler made the row (and its match-parent tiles) 1px tall.
            row.AddView(new View(this), new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = Dp(4), RightMargin = Dp(4) });
        var footer = new TextView(this) { Text = "章节按顺序逐个导入，更新时保留已有进度。", TextSize = 12 }; footer.SetTextColor(PgrTheme.Secondary);
        footer.SetPadding(Dp(6), Dp(10), 0, Dp(6));
        footer.SetCompoundDrawablesRelativeWithIntrinsicBounds(new BarMark(Resources!.DisplayMetrics!.Density), null, null, null); footer.CompoundDrawablePadding = Dp(7);
        paper.AddView(footer);
        var inscription = PgrUi.En(this, $"GRAY RAVEN ARCHIVE · {CategoryEn(categoryName)} · {visiblePackages.Length:00} PACKS", 8.5f, PgrTheme.Steel);
        inscription.LetterSpacing = .22f; inscription.SetPadding(Dp(6), Dp(2), 0, 0); paper.AddView(inscription);
        content = mainContent;
    }

    /// <summary>Current-chapter hero (no chapter open: first chapter of the shown category): full-width chapter cover (no text on the art) + NOW tag + outlined chapter number, then a white card.</summary>
    void ArchiveHero(int chapterCount, bool wide, InstalledPackage firstShown)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var engine = session.Engine;
        // 0.4.6: wide landscape with real height (tablet / foldable inner, ≥600dp tall) gets a taller cover band so the art keeps
        // its focal area instead of a 58dp sliver; phone landscape (≤ ~430dp tall) keeps the 0.4.3 band.
        bool tallWide = wide && Resources!.Configuration!.ScreenHeightDp >= 600;
        var block = new LinearLayout(this) { Orientation = Orientation.Vertical, ContentDescription = "剧情档案" };
        var cover = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        var band = new CoverBandDrawable(density, DecoTier.M) { BottomShade = true, CutTopRightDp = 14 };
        band.Deco.ShowEmblem = true; band.Deco.EmblemWidthDp = wide ? 60 : 120; band.Deco.EmblemRightDp = wide ? 70 : 90; band.Deco.EmblemBottomDp = wide ? -10 : -30;
        cover.Background = band;
        // No chapter open: the hero still carries the art of the first chapter in the shown category.
        if (engine != null) LoadChapterCover(cover, image => band.Cover = image, engine.Pack.Title, engine.Pack.Id, Dp(560));
        else LoadChapterCover(cover, image => band.Cover = image, firstShown.Title, firstShown.PackId, Dp(560));
        if (engine != null)
        {
            var now = new TextView(this) { Text = "当前章节 · NOW", TextSize = 10 }; now.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); now.LetterSpacing = .1f;
            now.SetTextColor(PgrTheme.OnPlate); now.Background = PgrTheme.Plate(this, PgrTheme.A(PgrTheme.NamePlate, 235), null, PgrTheme.CutSmall, Corners.BottomRight, PgrTheme.Red, AccentEdge.Left);
            now.SetPadding(Dp(10), Dp(4), Dp(10), Dp(4));
            cover.AddView(now, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.Start) { TopMargin = Dp(wide && !tallWide ? 6 : 12) });
        }
        // 0.4.7 N: titles without 「第N章」 (e.g. 「01 涂鸦艺术」) used to give 「—」, drawn here as a 72sp outlined dash = a stray hollow bar.
        string bigNumber = engine != null ? CoverNumber(engine.Pack.Title) : chapterCount.ToString("00");
        if (bigNumber.Length > 0)
        {
            var number = PgrUi.Outline(this, bigNumber, tallWide ? 60 : wide ? 40 : 72, Color.White, 1.4f); number.SetShadowLayer(Dp(6), 0, 0, PgrTheme.A(PgrTheme.Ink, 120));
            cover.AddView(number, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.End) { RightMargin = Dp(12), BottomMargin = Dp(wide ? 0 : 4) });
        }
        block.AddView(cover, new LinearLayout.LayoutParams(-1, Dp(tallWide ? 150 : wide ? 58 : 178)));
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        // 0.4.3 F-B L archive card (decor only under the content; the cover above stays clean).
        var (_, sectionLines) = SectionProgress();
        var catPacks = session.Packages.List().Where(p => ChapterCatalog.Category(p.PackId, p.Title) == ChapterCatalog.Category(firstShown.PackId, firstShown.Title)).ToList();
        int seqIndex = Math.Max(0, catPacks.FindIndex(p => p.PackId == (engine?.Pack.Id ?? firstShown.PackId)));
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.GlassRaised, PgrTheme.Border, PgrTheme.CutMedium, Corners.BottomLeft, brackets: PgrTheme.A(PgrTheme.Slate, 150)),
            new FusionDeco(density, DecoTier.L, FxKind.Hero) { CutBL = PgrTheme.CutMedium, ArcNo = engine != null ? ChapterNumber(engine.Pack.Title) : ChapterNumber(firstShown.Title),
                ArcLines = sectionLines.ToString("0000"), Seq = (seqIndex + 1).ToString("00") + "/" + Math.Max(1, catPacks.Count).ToString("00") });
        card.Tag = "fx";
        card.SetPadding(Dp(16), Dp(12), Dp(14), Dp(14));
        var eyebrow = new TextView(this) { Text = "剧情档案 · STORY ARCHIVE", TextSize = 11 }; eyebrow.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal); eyebrow.SetTextColor(PgrTheme.Secondary); eyebrow.LetterSpacing = .06f;
        eyebrow.SetCompoundDrawablesRelativeWithIntrinsicBounds(new BarMark(density), null, null, null); eyebrow.CompoundDrawablePadding = Dp(7);
        card.AddView(eyebrow);
        var headline = new TextView(this) { Text = engine != null ? PackTitle(engine.Pack.Title) : "故事仍在回响", TextSize = wide ? 21 : 24 };
        headline.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); headline.SetTextColor(PgrTheme.Foreground); headline.SetMaxLines(2); headline.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        headline.SetPadding(0, Dp(6), 0, Dp(3)); card.AddView(headline);
        var meta = PgrTheme.Apply(Text($"{chapterCount:00} 个章节  /  本地配音库", 11), PgrTheme.Role.Code, PgrTheme.Secondary); PgrTheme.Tabular(meta); meta.SetPadding(0, 0, 0, Dp(8)); card.AddView(meta);
        if (engine != null)
        {
            // 上次位置 RESUME: avatar of the current speaker + the stored progress summary.
            var resume = new LinearLayout(this) { Orientation = Orientation.Horizontal }; resume.SetGravity(GravityFlags.CenterVertical);
            resume.SetPadding(0, Dp(8), 0, Dp(10));
            var avatarFrame = new PortraitFrameDrawable(density) { ShowSerial = true };
            var avatar = new View(this) { Background = avatarFrame, ImportantForAccessibility = ImportantForAccessibility.No };
            string? shown = null; UpdatePortrait(avatar, avatarFrame, engine.Current?.Speaker, ref shown);
            resume.AddView(avatar, new LinearLayout.LayoutParams(Dp(44), Dp(44)) { RightMargin = Dp(12) });
            var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
            texts.Background = new BarEdge(density, PgrTheme.Cyan); texts.SetPadding(Dp(10), 0, 0, 0);
            var label = new LinearLayout(this) { Orientation = Orientation.Horizontal }; label.SetGravity(GravityFlags.CenterVertical);
            var zh = new TextView(this) { Text = "上次位置", TextSize = 11 }; zh.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zh.SetTextColor(PgrTheme.TealText); label.AddView(zh);
            label.AddView(PgrUi.En(this, "RESUME", 8, PgrTheme.TealText), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(6) });
            texts.AddView(label);
            var summary = new TextView(this) { Text = session.Progress.GetSummary(engine.Pack.Id) is { Length: > 0 } s ? s : "尚未开始 · 点击查看剧情", TextSize = 13 };
            summary.SetTextColor(PgrTheme.Foreground); summary.SetMaxLines(wide ? 2 : 3); summary.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; summary.SetLineSpacing(Dp(2), 1);
            texts.AddView(summary);
            resume.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
            card.AddView(resume);
            var go = new Button(this) { Text = "继续当前剧情" };
            PgrUi.Style(go, Xb.Primary, "play", "CONTINUE", wing: true, textSp: 15);
            go.Click += (_, _) => Safe(() => ShowPage(1));
            card.AddView(go, new LinearLayout.LayoutParams(-1, Dp(52)));
        }
        block.AddView(card, new LinearLayout.LayoutParams(-1, -2));
        content.AddView(block, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(wide ? 6 : 4), BottomMargin = Dp(10) });
    }

    /// <summary>Two-column cover tile: cover 100dp (outlined CH. number, 40dp dark folder corner = 管理文件), title + status chip, two-line summary.</summary>
    View ChapterTile(InstalledPackage pack)
    {
        float density = Resources!.DisplayMetrics!.Density;
        bool selected = session.Engine?.Pack.Id == pack.PackId;
        string? summary = session.Progress.GetSummary(pack.PackId) is { Length: > 0 } s ? s : null;
        var tile = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
        tile.Background = PgrTheme.Plate(this, PgrTheme.GlassRaised, null, 7, Corners.BottomRight, shadow: true);
        tile.Foreground = new GridCellDrawable(density) { IsCurrent = selected, Fill = Color.Transparent, Line = PgrTheme.Border };
        tile.ContentDescription = (selected ? "查看当前章：" : "打开章节：") + PackTitle(pack.Title);
        tile.Click += (_, _) => Safe(() => { session.LoadPack(pack.PackId); ShowPage(1); });
        var cover = new FrameLayout(this);
        var band = new CoverBandDrawable(density, DecoTier.S) { BottomShade = true };
        band.Deco.ShowEmblem = true; band.Deco.Tier = DecoTier.M; band.Deco.EmblemWidthDp = 70; band.Deco.EmblemRightDp = -6; band.Deco.EmblemBottomDp = -16; band.Deco.ShowTide = false;
        cover.Background = band;
        LoadChapterCover(cover, image => band.Cover = image, pack.Title, pack.PackId, Dp(320));
        var code = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        code.SetGravity(GravityFlags.Bottom);
        string tileNumber = CoverNumber(pack.Title);
        if (tileNumber.Length > 0)
        {
            var ch = PgrUi.En(this, "CH.", 8, Color.White); ch.SetPadding(0, 0, Dp(2), Dp(5)); code.AddView(ch);
            var number = PgrUi.Outline(this, tileNumber, 30, Color.White, 1.2f); number.SetShadowLayer(Dp(4), 0, 0, PgrTheme.A(PgrTheme.Ink, 140)); code.AddView(number);
        }
        cover.AddView(code, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Bottom | GravityFlags.Start) { LeftMargin = Dp(8), BottomMargin = Dp(2) });
        // 文件角钮: 40dp dark visual inside a 48dp hit target; original 「管理文件」 action and TalkBack text.
        var manage = new ImageButton(this) { ContentDescription = "管理章节文件：" + PackTitle(pack.Title) };
        manage.SetImageDrawable(new PgrIconDrawable("folder", PgrTheme.OnPlate, density, 18));
        manage.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 60)),
            new InsetDrawable(PgrTheme.Plate(this, PgrTheme.A(PgrTheme.NamePlate, 225), PgrTheme.A(PgrTheme.OnPlate, 60), 6, Corners.BottomLeft), Dp(4)), null);
        manage.Click += (_, _) => Safe(() => ManagePackage(pack.PackId));
        cover.AddView(manage, new FrameLayout.LayoutParams(Dp(48), Dp(48), GravityFlags.Top | GravityFlags.End));
        tile.AddView(cover, new LinearLayout.LayoutParams(-1, Dp(100)));
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        body.SetPadding(Dp(10), Dp(8), Dp(8), Dp(10));
        var titleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; titleRow.SetGravity(GravityFlags.CenterVertical);
        var name = new TextView(this) { Text = ChapterTitle(pack.Title), TextSize = 14 }; name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        name.SetTextColor(selected ? PgrTheme.RedText : PgrTheme.Foreground); name.SetSingleLine(true); name.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        titleRow.AddView(name, new LinearLayout.LayoutParams(0, -2, 1));
        var chip = selected ? PgrUi.Chip(this, "当前", PgrUi.Status.Current) : summary != null ? PgrUi.Chip(this, "续听", PgrUi.Status.Resume) : PgrUi.Chip(this, "未开始", PgrUi.Status.NotStarted);
        titleRow.AddView(chip, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(4) });
        body.AddView(titleRow);
        var progress = new TextView(this) { Text = summary ?? "尚未开始 · 点击查看剧情", TextSize = 11.5f }; progress.SetTextColor(PgrTheme.Secondary);
        progress.SetMaxLines(2); progress.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; progress.SetPadding(0, Dp(4), 0, 0); progress.SetLineSpacing(Dp(1), 1);
        body.AddView(progress);
        tile.AddView(body, new LinearLayout.LayoutParams(-1, -2));
        var nativeRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        nativeRow.SetGravity(GravityFlags.CenterVertical); nativeRow.SetPadding(Dp(4), 0, Dp(8), Dp(8));
        NativeVoiceChapterControls(nativeRow, pack.PackId);
        if (nativeRow.ChildCount > 0) tile.AddView(nativeRow, new LinearLayout.LayoutParams(-1, -2));
        return tile;
    }

    /// <summary>Short crimson vertical bar before eyebrows.</summary>
    sealed class BarMark(float density) : PgrShapeDrawable(density)
    {
        public override int IntrinsicWidth => (int)D(3);
        public override int IntrinsicHeight => (int)D(11);
        public override void Draw(Canvas canvas) { paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.Red); canvas.DrawRect(Bounds, paint); }
    }

    /// <summary>Thin coloured left edge (resume block).</summary>
    sealed class BarEdge(float density, Color color) : PgrShapeDrawable(density)
    {
        public override void Draw(Canvas canvas) { var b = Bounds; paint.SetStyle(Paint.Style.Fill); paint.Color = C(color); canvas.DrawRect(b.Left, b.Top, b.Left + D(2), b.Bottom, paint); }
    }
}
