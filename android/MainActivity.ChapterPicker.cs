using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Packages;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    /// <summary>Fixed category bar above the chapter page (portrait); in wide landscape the plate sits atop the left column.</summary>
    LinearLayout chapterCategoryBar = null!;

    /// <summary>英文分类名（表在 <see cref="CategoryNames"/>，供分类面板测试共用）。</summary>
    static string CategoryEn(string category) => CategoryNames.En(category);

    void CreateChapterCategoryBar()
    {
        chapterCategoryBar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        chapterCategoryBar.SetGravity(GravityFlags.CenterVertical);
        root.AddView(chapterCategoryBar, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
    }

    static string[] ChapterCategories(IReadOnlyList<InstalledPackage> packages) => ChapterCatalog.Categories
        .Concat(packages.Any(p => ChapterCatalog.Category(p.PackId, p.Title) == ChapterCatalog.Other)
            ? new[] { ChapterCatalog.Other } : Array.Empty<string>()).ToArray();

    /// <summary>Resolves the shown category (same rules as before) and rebuilds the category name plate into <paramref name="host"/>.</summary>
    string UpdateChapterCategory(IReadOnlyList<InstalledPackage> packages, LinearLayout host)
    {
        var categories = ChapterCategories(packages);
        string selected = session.Settings.ChapterCategory ?? "";
        if (!categories.Contains(selected))
        {
            var current = session.Engine?.Pack;
            string currentCategory = current == null ? "" : ChapterCatalog.Category(current.Id, current.Title);
            selected = categories.Contains(currentCategory) ? currentCategory : categories.FirstOrDefault() ?? "";
            if (session.Settings.ChapterCategory != selected)
            {
                session.Settings.ChapterCategory = selected;
                session.SaveSettings();
                pageScroll.Post(() => { if (alive && page == 0) ScrollChapterPageTop(); });
            }
        }
        host.RemoveAllViews();
        int count = packages.Count(p => ChapterCatalog.Category(p.PackId, p.Title) == selected);
        host.AddView(CategoryPlate(selected, Array.IndexOf(categories, selected) + 1, count), new LinearLayout.LayoutParams(0, Dp(52), 1));
        // 52dp chamfered search button (原「搜索已导入章节」).
        var search = new ImageButton(this) { ContentDescription = "搜索已导入章节" };
        search.SetImageDrawable(new PgrIconDrawable("search", PgrTheme.Foreground, Resources!.DisplayMetrics!.Density, 20));
        search.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)),
            FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.Raised, PgrTheme.BorderStrong, PgrTheme.CutSmall, Corners.Diagonal),
                new FusionDeco(Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall }), PgrTheme.Plate(this, Color.White, null, PgrTheme.CutSmall, Corners.Diagonal));
        search.Click += (_, _) => Safe(ShowLibrarySearch);
        host.AddView(search, new LinearLayout.LayoutParams(Dp(52), Dp(52)) { LeftMargin = Dp(8) });
        return selected;
    }

    void ScrollChapterPageTop() { pageScroll.ScrollTo(0, 0); splitLeftScroll.ScrollTo(0, 0); splitRightScroll.ScrollTo(0, 0); }

    /// <summary>Angled category name plate: dark「分类 CATEGORY」cap with crimson underline + light body (outlined number, name, English, NN PACKS, chevron).</summary>
    View CategoryPlate(string category, int number, int packs)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var plate = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        FusionDeco catDeco = null!;
        plate.SetGravity(GravityFlags.CenterVertical);
        plate.ContentDescription = "选择章节分类，当前" + category;
        plate.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)),
            FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.Raised, PgrTheme.BorderStrong, PgrTheme.CutMedium, Corners.BottomRight),
                catDeco = new FusionDeco(density, DecoTier.S) { CutBR = PgrTheme.CutMedium }), PgrTheme.Plate(this, Color.White, null, PgrTheme.CutMedium, Corners.BottomRight));
        plate.Click += (_, _) => Safe(ChooseChapterCategory);
        var cap = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        cap.SetGravity(GravityFlags.CenterVertical); cap.SetPadding(Dp(12), 0, Dp(16), 0);
        cap.Background = new LayerDrawable(new Drawable[] {
            new DarkPanelDrawable(density, DecoTier.S) { CutBottomRightDp = 14, ShowTide = false, Ticks = false },
            new ColorDrawable(PgrTheme.Red) });
        ((LayerDrawable)cap.Background).SetLayerInset(1, Dp(10), Dp(49), Dp(22), 0);
        var zh = new TextView(this) { Text = "分类", TextSize = 13 }; zh.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zh.SetTextColor(PgrTheme.OnPlate); zh.SetIncludeFontPadding(false); cap.AddView(zh);
        cap.AddView(PgrUi.En(this, "CATEGORY", 7.5f, PgrTheme.NavSub), new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(3) });
        plate.AddView(cap, new LinearLayout.LayoutParams(-2, -1));
        // S corner starts at the light body, right of the dark cap.
        cap.LayoutChange += (_, _) => { float left = cap.Width / density; if (Math.Abs(catDeco.LeftDp - left) > .5f) { catDeco.LeftDp = left; catDeco.Invalidate(); } };
        plate.AddView(PgrUi.Outline(this, number.ToString("00"), 22, PgrTheme.Red), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(10), RightMargin = Dp(8) });
        var names = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        var name = new TextView(this) { Text = category, TextSize = 16 }; name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); name.SetTextColor(PgrTheme.Foreground);
        name.SetSingleLine(true); name.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; name.SetIncludeFontPadding(false); names.AddView(name);
        names.AddView(PgrUi.En(this, CategoryEn(category), 8.5f, dropIfClipped: true), new LinearLayout.LayoutParams(-2, -2) { TopMargin = Dp(3) }); // 0.4.6: dropped, not clipped ("MAIN STOR") on 344dp
        plate.AddView(names, new LinearLayout.LayoutParams(0, -2, 1));
        plate.AddView(PgrUi.Readout(this, $"{packs:00} PACKS"), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(6) });
        plate.AddView(Icon("chevron_down", PgrTheme.Secondary, 18), new LinearLayout.LayoutParams(Dp(20), Dp(20)) { RightMargin = Dp(10) });
        return plate;
    }

    /// <summary>
    /// 分类面板（「选择分类 CATEGORY」）0.4.8 B 版（定稿 cfx-a5b6.js cellB）：竖屏两列，横屏右侧 560dp 面板三列（沿用 01 横屏），
    /// 整格封面卡；中文名、英文名完整显示（可换行、格子变高，不截断）。标题、副标题、取消栏不变。点格子应用，取消关闭。
    /// </summary>
    void ChooseChapterCategory()
    {
        var packages = session.Packages.List();
        var categories = ChapterCategories(packages);
        var sheet = new PgrSheet(this, s =>
        {
            s.Title("选择分类", "CATEGORY");
            s.Subtitle($"共 {categories.Length} 类 · 右侧数字为已导入章节数");
            int columns = s.Wide ? 3 : 2;
            s.WideWidthDp = 560;
            int gap = Dp(s.Wide ? 7 : 8);
            LinearLayout? row = null;
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                if (i % columns == 0) { row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; s.Body.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(7) }); }
                bool current = session.Settings.ChapterCategory == category;
                int count = packages.Count(p => ChapterCatalog.Category(p.PackId, p.Title) == category);
                var cell = CategoryCell(category, i + 1, count, current, packages);
                cell.Click += (_, _) => Safe(() =>
                {
                    s.Dismiss();
                    // 分类只过滤目录；实际打开章节仍由章节卡的点击操作完成。
                    if (session.Settings.ChapterCategory == category) return;
                    session.Settings.ChapterCategory = category;
                    session.SaveSettings();
                    Render();
                    pageScroll.Post(() => { if (alive && page == 0) ScrollChapterPageTop(); });
                });
                // 同一行的格子等高（wrap 后按最高者拉齐）。
                row!.AddView(cell, new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = i % columns == 0 ? 0 : gap });
            }
            // 空位与卡片统一 MATCH_PARENT；固定1px会让末行测量高度被空位压成1px。
            for (int k = categories.Length % columns; k != 0 && k < columns; k++) row!.AddView(new View(this), new LinearLayout.LayoutParams(0, -1, 1) { LeftMargin = gap });
            s.Cancel();
        });
        sheet.Show();
    }

    /// <summary>B 版格子：整格封面（无封面 = 深色石板 +「NO COVER」），左上编号块，右上已导入数，底部中文名 + 英文名。最低 78dp，文字多时变高。</summary>
    View CategoryCell(string category, int number, int count, bool current, IReadOnlyList<InstalledPackage> packages)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var art = new CategoryCardDrawable(density) { Selected = current };
        var cell = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, Background = art };
        int frame = PgrTheme.Dp(this, art.FrameDp);
        cell.SetPadding(frame, frame, frame, frame);
        cell.SetMinimumHeight(Dp(78));
        cell.ContentDescription = category + " " + CategoryEn(category) + "，已导入 " + count + " 章" + (current ? "，当前分类" : "");
        // 顶部：编号块（左上，右下切角 5dp；选中实心红）+ 已导入数（右上）+ 无封面时「NO COVER」。
        var top = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        var index = new TextView(this) { Text = number.ToString("00"), TextSize = 14, Gravity = GravityFlags.Center };
        index.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); index.SetTextColor(Color.White); index.SetIncludeFontPadding(false); PgrTheme.Tabular(index);
        index.Background = PgrTheme.Plate(this, current ? PgrTheme.Red : Color.Argb(184, 29, 35, 42), null, 5, Corners.BottomRight);
        index.SetPadding(Dp(6), 0, Dp(6), 0); index.SetMinWidth(Dp(30)); index.SetMinimumWidth(Dp(30));
        top.AddView(index, new FrameLayout.LayoutParams(-2, Dp(22), GravityFlags.Top | GravityFlags.Start));
        var tally = new TextView(this) { Text = count.ToString("00"), TextSize = 10 };
        tally.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); tally.LetterSpacing = .06f; tally.SetIncludeFontPadding(false); PgrTheme.Tabular(tally);
        tally.SetTextColor(count > 0 ? Color.White : Color.ParseColor("#AAB4BF"));
        tally.SetBackgroundColor(Color.Argb(184, 29, 35, 42)); tally.SetPadding(Dp(5), Dp(3), Dp(5), Dp(3));
        top.AddView(tally, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.End) { TopMargin = Dp(5), RightMargin = Dp(8) });
        cell.AddView(top, new LinearLayout.LayoutParams(-1, -2));
        // 封面：该分类第一个已导入章节，否则随包代表封面；都没有 = 无封面石板。
        var first = packages.Where(p => ChapterCatalog.Category(p.PackId, p.Title) == category)
            .OrderBy(p => ChapterCatalog.SortKey(p.PackId, p.Title, p.PackId), StringComparer.Ordinal).FirstOrDefault();
        string[] categoryKey = ["category:" + category];
        if (first != null) LoadChapterCover(cell, image => art.Cover = image, first.Title, first.PackId, Dp(320));
        else if (BundledCovers.Find(this, categoryKey) != null) PortraitStore.RequestCover(cell, [], Dp(320), image => art.Cover = image, categoryKey);
        else
        {
            var none = PgrUi.En(this, "NO COVER", 7.5f, Color.Argb(140, 195, 203, 212));
            top.AddView(none, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.End) { TopMargin = Dp(28), RightMargin = Dp(10) });
        }
        cell.AddView(new View(this), new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = Dp(4) });
        // 底部名称：中文 15.5sp 粗体白（可换行，不截断）；英文 8.5sp 按宽度收字距 / 降字号，仍放不下换行。
        var names = new LinearLayout(this) { Orientation = Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        names.SetPadding(Dp(10), 0, Dp(10), Dp(6));
        var name = new TextView(this) { Text = category, TextSize = 15.5f };
        name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); name.SetTextColor(Color.White); name.SetIncludeFontPadding(false);
        name.SetLineSpacing(Dp(1), 1); name.Ellipsize = null;
        names.AddView(name, new LinearLayout.LayoutParams(-1, -2));
        names.AddView(new CategoryEnText(this, CategoryEn(category), current ? Color.ParseColor("#F3C4CB") : Color.ParseColor("#C3CBD4")),
            new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(3) });
        cell.AddView(names, new LinearLayout.LayoutParams(-1, -2));
        return cell;
    }
}
