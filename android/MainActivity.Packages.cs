using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Packages;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    bool deletingArchive;
    static string FileSize(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.##} GB" :
        bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):0.##} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} B";

    /// <summary>12-A 管理章节文件: archive-paper sheet (wide landscape 640dp, two columns); DANGER ZONE with two danger levels.</summary>
    void ManagePackage(string packId, bool justImported = false)
    {
        if (session.PackagesBusy) { Info("章节正在处理", "请等当前导入或删除完成后再管理文件。"); return; }
        var pack = session.Packages.Find(packId) ?? throw new IOException("章节已被删除。");
        PgrSheet? sheet = null;
        sheet = new PgrSheet(this, s =>
        {
            bool wide = s.Wide;
            s.Title(justImported ? "章节导入完成" : "管理章节文件", justImported ? "IMPORT · COMPLETE" : "PACK · FILES");
            var columns = new LinearLayout(this) { Orientation = wide ? Orientation.Horizontal : Orientation.Vertical };
            var left = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var right = new LinearLayout(this) { Orientation = Orientation.Vertical };
            columns.AddView(left, wide ? new LinearLayout.LayoutParams(0, -2, 1.1f) { RightMargin = Dp(12) } : new LinearLayout.LayoutParams(-1, -2));
            columns.AddView(right, wide ? new LinearLayout.LayoutParams(0, -2, 1) : new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
            var packTitle = Text(justImported ? "导入成功，已解压的章节可以独立播放。" : PackTitle(pack.Title), 15); packTitle.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); left.AddView(packTitle);
            var location = Text("解压文件夹 · 点击进入\n" + System.IO.Path.GetDirectoryName(pack.PackFile), 12);
            location.SetTextColor(PgrTheme.Steel); location.SetTypeface(PgrTheme.Mono, TypefaceStyle.Normal); location.ContentDescription = "打开章节解压文件夹";
            location.Background = PgrTheme.PressableCard(this); location.SetPadding(Dp(14), Dp(10), Dp(12), Dp(10)); location.SetMinHeight(Dp(52));
            location.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable("folder", PgrTheme.Steel, Resources!.DisplayMetrics!.Density, 18), null, null, null); location.CompoundDrawablePadding = Dp(10);
            location.Clickable = true; location.Focusable = true; left.AddView(location, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6), BottomMargin = Dp(4) });
            location.Click += (_, _) => Safe(() => { sheet!.Dismiss(); BrowsePackage(packId, "", pack.Revision); });
            var storageNote = Text("应用内部存储，点上方路径可在这里浏览文件。", 12); storageNote.SetTextColor(PgrTheme.Muted); left.AddView(storageNote);
            bool hasZip = session.Settings.ImportedArchives.TryGetValue(packId, out var source);
            if (hasZip) { var zip = Text("原 ZIP：" + source!.Name + (source.Size is >= 0 ? " · " + FileSize(source.Size.Value) : ""), 13); zip.SetPadding(0, Dp(8), 0, 0); left.AddView(zip); }
            else { var none = Text("未记录原 ZIP，或已从应用中删除。旧版导入的压缩包请在系统文件管理中处理。", 12); none.SetPadding(0, Dp(8), 0, 0); left.AddView(none); }
            // DANGER ZONE: dashed divider, level-1 outline (删除原 ZIP), level-2 solid hatch (删除章节配音包).
            var zone = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.No }; zone.SetGravity(GravityFlags.CenterVertical);
            var zoneLabel = new TextView(this) { Text = "DANGER ZONE", TextSize = 9.5f }; zoneLabel.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); zoneLabel.LetterSpacing = .12f; zoneLabel.SetTextColor(PgrTheme.RedText); zone.AddView(zoneLabel);
            var dash = new View(this) { Background = new DashLineDrawable(Resources!.DisplayMetrics!.Density, PgrTheme.A(PgrTheme.Red, 150)) };
            zone.AddView(dash, new LinearLayout.LayoutParams(0, Dp(2), 1) { LeftMargin = Dp(8) });
            right.AddView(zone, new LinearLayout.LayoutParams(-1, Dp(24)) { BottomMargin = Dp(4) });
            if (hasZip)
            {
                var delZip = new Button(this) { Text = "删除原 ZIP（保留已导入章节）" }; PgrUi.Style(delZip, Xb.DangerOutline, "trash", textSp: 14);
                delZip.Click += (_, _) => Safe(() => { sheet!.Dismiss(); ConfirmDeleteArchive(packId, source!); });
                right.AddView(delZip, new LinearLayout.LayoutParams(-1, Dp(52)) { BottomMargin = Dp(8) });
            }
            var deletePack = new Button(this) { Text = "删除章节配音包" }; PgrUi.Style(deletePack, Xb.DangerSolid, "trash", "DELETE PACK", textSp: 14);
            deletePack.Click += (_, _) => Safe(() =>
            {
                sheet!.Dismiss();
                DangerConfirm("删除章节配音包？", PackTitle(pack.Title), new[] { "将删除本章解压文件及旧版本音频，停止本章播放。" }, new[] { "保留游戏配音进度、听书记录和书签；原 ZIP 不会删除。" }, "再次播放需要重新导入。", () => DeletePackage(pack));
            });
            right.AddView(deletePack, new LinearLayout.LayoutParams(-1, Dp(56)));
            s.Body.AddView(columns, new LinearLayout.LayoutParams(-1, -2));
            var close = new Button(this) { Text = "关闭" }; PgrUi.Style(close, Xb.Normal, "close", "CLOSE", center: true);
            close.Click += (_, _) => sheet!.Dismiss();
            s.Footer.AddView(close, new LinearLayout.LayoutParams(-1, Dp(PgrTheme.Touch)) { BottomMargin = Dp(24) });
        }) { WideWidthDp = 640 };
        sheet.Show();
    }

    /// <summary>12-A / 18-B level-2 danger confirmation: crimson hatch band, ⚠ title, 「× 将删除 / ✓ 保留」 groups, red reminder,
    /// solid crimson 确认. Wording is the original sentence split into lines; callbacks unchanged.</summary>
    void DangerConfirm(string title, string subject, string[] deletes, string[] keeps, string? warning, Action yes)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeDeviceDefaultLightNoActionBar);
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = new ArchivePaperDrawable(density) { Fill = PgrTheme.Background, TopRedBar = true, CutTopRightDp = 18, TideDp = 24 };
        var band = new View(this) { Background = PgrTheme.Plate(this, PgrTheme.Red, null, 0, Corners.None, hatch: PgrTheme.A(Color.White, 60), hatchWidth: 6), ImportantForAccessibility = ImportantForAccessibility.No };
        card.AddView(band, new LinearLayout.LayoutParams(-1, Dp(10)));
        var inner = new LinearLayout(this) { Orientation = Orientation.Vertical }; inner.SetPadding(Dp(18), Dp(12), Dp(18), Dp(28));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical);
        head.AddView(Icon("warning", PgrTheme.Red, 20), new LinearLayout.LayoutParams(Dp(20), Dp(20)) { RightMargin = Dp(8) });
        var t = new TextView(this) { Text = title, TextSize = 18 }; t.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); t.SetTextColor(PgrTheme.Foreground); head.AddView(t, new LinearLayout.LayoutParams(0, -2, 1));
        var eyebrow = new TextView(this) { Text = "DELETE", TextSize = 9 }; eyebrow.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); eyebrow.LetterSpacing = .12f; eyebrow.SetTextColor(PgrTheme.RedText); head.AddView(eyebrow);
        inner.AddView(head);
        var subj = Text(subject, 14); subj.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); subj.SetPadding(0, Dp(8), 0, Dp(6)); inner.AddView(subj);
        void Group(string mark, Color color, string[] lines)
        {
            foreach (var line in lines)
            {
                var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                var m = new TextView(this) { Text = mark, TextSize = 14 }; m.SetTextColor(color); m.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); row.AddView(m, new LinearLayout.LayoutParams(Dp(20), -2));
                var v = Text(line, 13); v.SetTextColor(PgrTheme.Foreground); row.AddView(v, new LinearLayout.LayoutParams(0, -2, 1));
                inner.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
            }
        }
        Group("×", PgrTheme.RedText, deletes); Group("✓", PgrTheme.TealText, keeps);
        if (!string.IsNullOrEmpty(warning)) { var w = Text(warning, 13); w.SetTextColor(PgrTheme.RedText); w.SetPadding(0, Dp(4), 0, 0); inner.AddView(w); }
        var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var cancel = new Button(this) { Text = "取消" }; PgrUi.Style(cancel, Xb.Normal, "close", center: true, textSp: 14); cancel.SetMinWidth(0); cancel.SetMinimumWidth(0);
        var ok = new Button(this) { Text = "确认" }; PgrUi.Style(ok, Xb.DangerSolid, "trash", center: true, textSp: 14); ok.SetMinWidth(0); ok.SetMinimumWidth(0);
        cancel.Click += (_, _) => dialog.Dismiss();
        ok.Click += (_, _) => { dialog.Dismiss(); Safe(yes); };
        keys.AddView(cancel, new LinearLayout.LayoutParams(0, Dp(50), 1) { RightMargin = Dp(5) }); keys.AddView(ok, new LinearLayout.LayoutParams(0, Dp(50), 1) { LeftMargin = Dp(5) });
        inner.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(14) });
        var scroll = new ScrollView(this); scroll.AddView(inner);
        card.AddView(scroll);
        var frame = new FrameLayout(this) { Clickable = true }; frame.SetBackgroundColor(Color.Argb(115, 0x1D, 0x23, 0x2A));
        frame.Click += (_, _) => dialog.Dismiss();
        var c = Resources!.Configuration!;
        int width = Math.Min(Dp(470), Dp(c.ScreenWidthDp - 28));
        card.Clickable = true;
        frame.AddView(card, new FrameLayout.LayoutParams(width, -2, GravityFlags.Center));
        dialog.SetContentView(frame);
        dialog.Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        dialog.Show();
    }

    void ConfirmDeleteArchive(string packId, ImportedZipSource source)
    {
        DangerConfirm("删除原 ZIP？", source.Name, new[] { "只删除导入时选中的压缩包，" }, new[] { "已解压的章节、进度和书签会保留。" }, "这是原文件，删除后如需备份须重新下载。", async () =>
        {
            if (deletingArchive || session.PackagesBusy) { Info("请稍候", "文件正在处理中，请等待完成。"); return; }
            deletingArchive = true;
            try
            {
                if (!session.Settings.ImportedArchives.TryGetValue(packId, out var now) || now != source || session.Packages.Find(packId) == null)
                { Info("文件记录已变化", "请重新打开章节管理后再操作。"); return; }
                await Task.Run(() => ImportedZipAccess.Delete(ApplicationContext!, source));
                session.ForgetImportedArchive(source.Uri);
                if (alive) { Render(); Info("原 ZIP 已删除", "已解压的章节、进度和书签都已保留，可以继续播放。"); }
            }
            catch (Exception ex)
            {
                if (alive) Info("未能删除原 ZIP", ex is Java.Lang.SecurityException ?
                    "原文件的删除授权不可用，请在系统文件管理中删除。已导入的章节不受影响。" : ex.Message);
            }
            finally { deletingArchive = false; }
        });
    }

    async void DeletePackage(InstalledPackage pack)
    {
        if (deletingArchive) { Info("请稍候", "正在删除原 ZIP，请等待完成。"); return; }
        try { await session.RemovePackageAsync(pack.PackId, pack.Revision); if (alive) { page = 0; Render(); Info("章节已删除", session.Status); } }
        catch (Exception ex) { if (alive) Info("未能删除章节", ex.Message); }
    }

    /// <summary>12-A 章节解压文件: archive-paper sheet (460dp right panel in wide landscape), list scrolls inside.</summary>
    async void BrowsePackage(string packId, string relative, string? revision, int pageIndex = 0)
    {
        try
        {
            var folder = await Task.Run(() => session.Packages.Browse(packId, relative, revision));
            if (!alive) return;
            const int pageSize = 40;
            int pages = Math.Max(1, (folder.Entries.Count + pageSize - 1) / pageSize);
            pageIndex = Math.Clamp(pageIndex, 0, pages - 1);
            PgrSheet? sheet = null;
            sheet = new PgrSheet(this, s =>
            {
                s.Title("章节解压文件", "PACK · FOLDER", $"{folder.Entries.Count}");
                var path = Text(folder.FullPath, 12); path.SetTextIsSelectable(true); path.SetTypeface(PgrTheme.Mono, TypefaceStyle.Normal); path.SetTextColor(PgrTheme.Steel); s.Header.AddView(path);
                var tools = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                var copy = new Button(this) { Text = "复制路径" }; PgrUi.Style(copy, Xb.Normal, "import", center: true, textSp: 13); copy.SetMinWidth(0); copy.SetMinimumWidth(0);
                copy.Click += (_, _) => Safe(() =>
                {
                    var clipboard = (ClipboardManager)GetSystemService(ClipboardService)!;
                    clipboard.PrimaryClip = ClipData.NewPlainText("章节文件位置", folder.FullPath);
                    Toast.MakeText(this, "路径已复制", ToastLength.Short)?.Show();
                });
                tools.AddView(copy, new LinearLayout.LayoutParams(0, Dp(48), 1) { RightMargin = Dp(4) });
                if (relative.Length > 0)
                {
                    var up = new Button(this) { Text = "上一级", ContentDescription = "↑ 上一级" }; PgrUi.Style(up, Xb.Normal, "chevron_up", center: true, textSp: 13); up.SetMinWidth(0); up.SetMinimumWidth(0);
                    up.Click += (_, _) => Safe(() => { sheet!.Dismiss(); int slash = relative.LastIndexOf('/'); BrowsePackage(packId, slash < 0 ? "" : relative[..slash], folder.Revision); });
                    tools.AddView(up, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(4) });
                }
                s.Header.AddView(tools, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
                var count = Text($"{folder.Entries.Count} 项 · 第 {pageIndex + 1}/{pages} 页", 12); count.SetTypeface(PgrTheme.Mono, TypefaceStyle.Normal); count.SetTextColor(PgrTheme.Muted); count.SetPadding(0, Dp(6), 0, Dp(4)); s.Header.AddView(count);
                foreach (var entry in folder.Entries.Skip(pageIndex * pageSize).Take(pageSize))
                {
                    if (entry.IsDirectory)
                    {
                        var dir = Button(s.Body, "文件夹  " + entry.Name + "  ›", () => { sheet!.Dismiss(); BrowsePackage(packId, entry.RelativePath, folder.Revision); });
                        ButtonIcon(dir, "folder");
                    }
                    else
                    {
                        var file = Text(entry.Name + "\n" + FileSize(entry.Bytes), 13);
                        file.Background = PgrTheme.Plate(this, PgrTheme.Panel, PgrTheme.Border, PgrTheme.CutSmall, Corners.BottomRight);
                        file.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
                        s.Body.AddView(file, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), BottomMargin = Dp(2) });
                    }
                }
                var pager = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                if (pageIndex > 0) Button(pager, "上一页", () => { sheet!.Dismiss(); BrowsePackage(packId, relative, folder.Revision, pageIndex - 1); });
                if (pageIndex + 1 < pages) Button(pager, "下一页", () => { sheet!.Dismiss(); BrowsePackage(packId, relative, folder.Revision, pageIndex + 1); });
                if (pager.ChildCount > 0) s.Body.AddView(pager);
                var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                var manage = new Button(this) { Text = "章节管理" }; PgrUi.Style(manage, Xb.Normal, "archive", center: true, textSp: 14); manage.SetMinWidth(0); manage.SetMinimumWidth(0);
                manage.Click += (_, _) => Safe(() => { sheet!.Dismiss(); ManagePackage(packId); });
                var close = new Button(this) { Text = "关闭" }; PgrUi.Style(close, Xb.Normal, "close", center: true, textSp: 14); close.SetMinWidth(0); close.SetMinimumWidth(0);
                close.Click += (_, _) => sheet!.Dismiss();
                keys.AddView(manage, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1) { RightMargin = Dp(4) }); keys.AddView(close, new LinearLayout.LayoutParams(0, Dp(PgrTheme.Touch), 1) { LeftMargin = Dp(4) });
                s.Footer.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(24) });
            }) { WideWidthDp = 460 };
            sheet.Show();
        }
        catch (Exception ex) { if (alive) Info("无法打开文件夹", ex.Message); }
    }
}
