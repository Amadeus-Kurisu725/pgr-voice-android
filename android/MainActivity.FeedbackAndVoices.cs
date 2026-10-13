using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    string FeedbackVersion => PackageManager?.GetPackageInfo(PackageName!, PackageInfoFlags.MetaData)?.VersionName ?? "未知版本";

    IEnumerable<Node> RecordedFeedbackContext(PlaybackEngine engine, int throughIndex) =>
        engine.History.Take(Math.Clamp(throughIndex + 1, 0, engine.History.Count)).TakeLast(4)
            .Select(v => engine.Pack.ById.GetValueOrDefault(v.NodeId)).OfType<Node>();

    void FeedbackCurrentGameLine()
    {
        var engine = session.Engine;
        if (engine?.Current is not { Kind: "line" } node)
        { Info("反馈这句", "请先选择要反馈的台词，或在历史记录中选择已播放的句子。"); return; }
        BeginLineFeedback(engine.Pack, node, "游戏配音", RecordedFeedbackContext(engine, engine.HistoryPosition));
    }

    void FeedbackCurrentListeningLine()
    {
        var player = session.Listening;
        if (player.Pack == null || player.Current is not { Kind: "line" } node)
        { Info("反馈这句", "请先选择要反馈的听书台词。"); return; }
        BeginLineFeedback(player.Pack, node, "听书", listening: true);
    }

    void BeginLineFeedback(Pack pack, Node node, string mode, IEnumerable<Node>? recent = null, bool listening = false)
    {
        // 先冻结选中句，再暂停；填写期间任何位置变化都不会替换反馈对象。
        var snapshot = LineFeedback.Capture(pack, node, FeedbackVersion, "Android", mode, recent);
        if (listening) session.Listening.Pause();
        else session.Command(e => e.PauseForBrowse());
        ShowLineFeedbackCategories(snapshot);
    }

    // 关掉但没复制或分享的反馈留作草稿；同一句再次打开时恢复，换一句不带过去。
    (string Key, string? Category, string Note)? feedbackDraft;
    static string FeedbackDraftKey(LineFeedbackSnapshot s) => s.PackId + "\n" + s.NodeId + "\n" + s.Mode;

    void ShowLineFeedbackCategories(LineFeedbackSnapshot snapshot)
    {
        if (feedbackDraft is { } draft && draft.Key == FeedbackDraftKey(snapshot)) FeedbackSheet(snapshot, draft.Category, draft.Note, restored: true);
        else FeedbackSheet(snapshot, null, "");
    }

    void EditLineFeedback(LineFeedbackSnapshot snapshot, string category, string comment = "") => FeedbackSheet(snapshot, category, comment);

    /// <summary>Dark panel title row on L-tier sheets: crimson triangle, title, condensed English.</summary>
    void DarkTitle(LinearLayout header, string title, string english, string? readout = null)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(7), Dp(10)) { RightMargin = Dp(8) });
        var t = new TextView(this) { Text = title, TextSize = 17 }; t.SetTypeface(PgrTheme.SansBold, global::Android.Graphics.TypefaceStyle.Bold); t.SetTextColor(PgrTheme.OnPlate); row.AddView(t);
        var en = new TextView(this) { Text = english, TextSize = 9 }; en.SetTypeface(PgrTheme.Condensed, global::Android.Graphics.TypefaceStyle.Bold); en.LetterSpacing = .1f; en.SetTextColor(PgrTheme.NavSub); en.SetPadding(Dp(8), Dp(3), 0, 0);
        row.AddView(en, new LinearLayout.LayoutParams(0, -2, 1));
        if (readout != null) row.AddView(PgrUi.Readout(this, readout, true));
        header.AddView(row, new LinearLayout.LayoutParams(-1, Dp(44)));
    }

    Button DarkKey(LinearLayout parent, string text, Xb kind, string? icon, Action click, float weight = 1)
    {
        var b = new Button(this) { Text = text }; PgrUi.Style(b, kind, icon, center: true, textSp: 14); b.SetSingleLine(true); b.SetMinWidth(0); b.SetMinimumWidth(0);
        b.Click += (_, _) => Safe(click); DropIconWhenTight(b);
        parent.AddView(b, new LinearLayout.LayoutParams(0, Dp(50), weight) { LeftMargin = Dp(3), RightMargin = Dp(3) });
        return b;
    }

    /// <summary>11-B2 merged feedback form: frozen line card (LOCKED · 已固定) + five problem chips + note on one dark panel.</summary>
    void FeedbackSheet(LineFeedbackSnapshot snapshot, string? category, string comment, bool restored = false)
    {
        string? chosen = category; string note = comment;
        string draftKey = FeedbackDraftKey(snapshot); bool discarded = false;
        PgrSheet? sheet = null;
        sheet = new PgrSheet(this, s =>
        {
            bool wide = s.Wide;
            DarkTitle(s.Header, "反馈这句 · 选择问题", "FEEDBACK");
            var columns = new LinearLayout(this) { Orientation = wide ? Orientation.Horizontal : Orientation.Vertical };
            var left = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var right = new LinearLayout(this) { Orientation = Orientation.Vertical };
            columns.AddView(left, wide ? new LinearLayout.LayoutParams(0, -2, 1) { RightMargin = Dp(10) } : new LinearLayout.LayoutParams(-1, -2));
            columns.AddView(right, wide ? new LinearLayout.LayoutParams(0, -2, 1) : new LinearLayout.LayoutParams(-1, -2));
            // Frozen line card.
            var card = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            card.Background = PgrTheme.Plate(this, PgrArt.DeckButton, PgrTheme.A(PgrTheme.OnPlate, 60), PgrTheme.CutSmall, Corners.BottomRight, PgrArt.TideRed, AccentEdge.Left);
            card.SetPadding(Dp(12), Dp(10), Dp(10), Dp(10));
            string who = string.IsNullOrWhiteSpace(snapshot.Speaker) ? "旁白" : SpeakerVolume.DisplayName(snapshot.Speaker);
            card.AddView(SpeakerAvatar(string.IsNullOrWhiteSpace(snapshot.Speaker) ? "旁白" : snapshot.Speaker, 36, true), new LinearLayout.LayoutParams(Dp(36), Dp(36)) { RightMargin = Dp(10) });
            var cardBody = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var meta = new LinearLayout(this) { Orientation = Orientation.Horizontal }; meta.SetGravity(GravityFlags.CenterVertical);
            var name = new TextView(this) { Text = who, TextSize = 12 }; name.SetTextColor(PgrArt.GoldLine); name.SetTypeface(PgrTheme.SansBold, global::Android.Graphics.TypefaceStyle.Bold); meta.AddView(name, new LinearLayout.LayoutParams(0, -2, 1));
            var locked = new TextView(this) { Text = "LOCKED · 已固定", TextSize = 9 }; locked.SetTypeface(PgrTheme.Condensed, global::Android.Graphics.TypefaceStyle.Bold); locked.LetterSpacing = .08f; locked.SetTextColor(PgrTheme.NavText);
            locked.Background = PgrTheme.Plate(this, PgrTheme.A(Color.White, 18), PgrTheme.A(PgrTheme.OnPlate, 90), 3, Corners.BottomRight); locked.SetPadding(Dp(6), Dp(2), Dp(6), Dp(2)); meta.AddView(locked);
            cardBody.AddView(meta);
            var line = new TextView(this) { Text = snapshot.Text, TextSize = 14 }; line.SetTextColor(PgrTheme.OnPlate); line.SetMaxLines(4); line.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; line.SetPadding(0, Dp(3), 0, Dp(3)); cardBody.AddView(line);
            var mode = new TextView(this) { Text = snapshot.Mode, TextSize = 11 }; mode.SetTextColor(PgrTheme.NavSub); cardBody.AddView(mode);
            card.AddView(cardBody, new LinearLayout.LayoutParams(0, -2, 1));
            left.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });
            // Five problem chips (2 per row), selected = crimson.
            var cats = LineFeedback.Categories;
            LinearLayout? chipRow = null; Button? preview = null;
            var chips = new List<Button>();
            void Paint()
            {
                for (int k = 0; k < chips.Count; k++)
                {
                    bool on = cats[k] == chosen; PgrUi.Style(chips[k], on ? Xb.Primary : Xb.Dark, null, center: true, textSp: 13);
                    chips[k].ContentDescription = cats[k] + (on ? "，已选择" : "");
                }
                if (preview != null) preview.Alpha = chosen != null ? 1f : .55f;
            }
            // 0.4.6: short wide landscape (< 390dp tall — 640×360, 800×360, 803×344, 384dp phones; Android 15 counts the bars in
            // ScreenHeightDp) puts the five chips on one full-width row under the two columns, so 跟丢了/读音问题/其他 are no longer
            // hidden under the 取消/预览反馈 keys. 0.4.7 P1: 393/412dp phone landscape (ScreenHeightDp 393/412 incl. bars) also
            // uses the single row; tablets and the fold inner screen (≥ 480dp tall) keep the 0.4.3 two-per-row grid.
            bool oneRow = wide && Resources!.Configuration!.ScreenHeightDp < 480;
            int perRow = oneRow ? cats.Count : 2;
            for (int i = 0; i < cats.Count; i++)
            {
                if (i % perRow == 0) { chipRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; if (oneRow) chipRow.SetPadding(0, Dp(2), 0, 0); else left.AddView(chipRow, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) }); }
                string c = cats[i];
                var chip = new Button(this) { Text = c }; chip.SetSingleLine(true); chip.SetMinWidth(0); chip.SetMinimumWidth(0);
                chip.Click += (_, _) => Safe(() => { chosen = c; Paint(); });
                chipRow!.AddView(chip, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(3), RightMargin = Dp(3) });
                chips.Add(chip);
            }
            if (!oneRow && cats.Count % 2 == 1) chipRow!.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
            var edit = new EditText(this) { Text = note, Hint = "补充说明（可留空）", Gravity = GravityFlags.Top };
            edit.SetMinLines(2); edit.SetMaxLines(5); PgrTheme.StyleInput(edit); edit.SetMinimumHeight(Dp(48));
            edit.TextChanged += (_, _) => note = edit.Text ?? "";
            right.AddView(edit, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(wide ? 0 : 4) });
            var hint = new TextView(this) { Text = (restored ? "已恢复上次未完成的反馈。" : "") + "预览后可复制或自行选择应用分享。不会自动发送，也不采集截图或录屏。", TextSize = 12 }; hint.SetTextColor(PgrTheme.NavText); hint.SetPadding(0, Dp(6), 0, Dp(4));
            right.AddView(hint);
            s.Body.AddView(columns, new LinearLayout.LayoutParams(-1, -2));
            if (oneRow) s.Body.AddView(chipRow, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = -Dp(3), RightMargin = -Dp(3) });
            var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            DarkKey(keys, "取消", Xb.Dark, "close", () => { discarded = true; sheet!.Dismiss(); });
            preview = DarkKey(keys, "预览反馈", Xb.Primary, "feedback", () =>
            {
                if (chosen == null) { Toast.MakeText(this, "请先选择问题类型", ToastLength.Short)?.Show(); return; }
                sheet!.Dismiss(); PreviewLineFeedback(snapshot, chosen, note);
            }, 1.4f);
            s.Footer.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(30) });
            Paint();
        }) { DarkMark = "FEEDBACK", WideWidthDp = 640 };
        sheet.Dismissed += () =>
        {
            if (!discarded && (chosen != null || note.Trim().Length > 0)) feedbackDraft = (draftKey, chosen, note);
            else if (feedbackDraft?.Key == draftKey) feedbackDraft = null;
        };
        sheet.Show();
    }

    /// <summary>11-B2 EXPORT board: the Format() report on a dark board; 返回修改 / 复制 / 系统分享 in one row.</summary>
    void PreviewLineFeedback(LineFeedbackSnapshot snapshot, string category, string comment)
    {
        string report = LineFeedback.Format(snapshot, category, comment);
        PgrSheet? sheet = null;
        sheet = new PgrSheet(this, s =>
        {
            DarkTitle(s.Header, "反馈预览 · " + category, "EXPORT");
            var body = new TextView(this) { Text = report, TextSize = 12 }; body.SetTextIsSelectable(true);
            body.SetTypeface(PgrTheme.Mono, global::Android.Graphics.TypefaceStyle.Normal); body.SetTextColor(PgrTheme.OnPlate); body.SetLineSpacing(Dp(2), 1);
            body.Background = PgrTheme.Plate(this, PgrTheme.A(Color.Black, 70), PgrTheme.A(PgrArt.MistLine, 90), PgrTheme.CutSmall, Corners.Diagonal, PgrArt.GoldLine, AccentEdge.Left);
            body.SetPadding(Dp(14), Dp(10), Dp(12), Dp(10));
            s.Body.AddView(body, new LinearLayout.LayoutParams(-1, -2));
            var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            DarkKey(keys, "返回修改", Xb.Dark, "back", () => { sheet!.Dismiss(); EditLineFeedback(snapshot, category, comment); });
            DarkKey(keys, "复制", Xb.Dark, "import", () =>
            {
                var clipboard = (global::Android.Content.ClipboardManager)GetSystemService(ClipboardService)!;
                clipboard.PrimaryClip = ClipData.NewPlainText("台词反馈", report);
                if (feedbackDraft?.Key == FeedbackDraftKey(snapshot)) feedbackDraft = null;
                Toast.MakeText(this, "反馈已复制，由你决定是否发送", ToastLength.Short)?.Show();
            });
            DarkKey(keys, "系统分享", Xb.Primary, "open", () =>
            {
                var intent = new Intent(Intent.ActionSend);
                intent.SetType("text/plain"); intent.PutExtra(Intent.ExtraText, report);
                if (feedbackDraft?.Key == FeedbackDraftKey(snapshot)) feedbackDraft = null;
                StartActivity(Intent.CreateChooser(intent, "选择分享应用"));
            });
            s.Footer.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(30) });
        }) { DarkMark = "EXPORT", WideWidthDp = 640 };
        sheet.Show();
    }

    /// <summary>11-B2 角色音量: dark list (search + rows) and a fixed adjust console (big %, tick ring, 0/50/100 slider, 取消 / 恢复 100% / 保存).</summary>
    void ShowSpeakerVolumes()
    {
        var packs = new[] { session.Engine?.Pack, session.Listening.Pack }.OfType<Pack>().Distinct();
        var names = packs.SelectMany(p => p.Nodes).Where(n => !n.Archived && n.Kind == "line")
            .Select(n => SpeakerVolume.Key(n.Speaker)).Concat(session.Settings.SpeakerVolumes.Keys)
            .Distinct(StringComparer.Ordinal).OrderBy(n => SpeakerVolume.DisplayName(n), StringComparer.CurrentCulture).ToArray();
        if (names.Length == 0) { Info("角色音量", "请先打开一章剧情或听书，再调整其中的角色。"); return; }
        string query = ""; string? selected = null; int pending = 100;
        PgrSheet? sheet = null;
        sheet = new PgrSheet(this, s =>
        {
            bool wide = s.Wide; float density = Resources!.DisplayMetrics!.Density;
            int adjusted = names.Count(n => SpeakerVolume.GetPercent(session.Settings.SpeakerVolumes, n) != 100);
            DarkTitle(s.Header, "角色音量", "VOLUME", $"共 {names.Length} 个角色 · 已调整 {adjusted} 个");
            s.CustomBody = true;
            var columns = new LinearLayout(this) { Orientation = wide ? Orientation.Horizontal : Orientation.Vertical };
            var listCol = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var help = new TextView(this) { Text = "按当前章节里的名字分别保存。100% 保持总音量，0% 静音；不会合并别名或更换声音。", TextSize = 12 }; help.SetTextColor(PgrTheme.NavText); help.SetPadding(0, 0, 0, Dp(6));
            var search = new EditText(this) { Hint = "搜索角色", Text = query }; search.SetSingleLine(true); PgrTheme.StyleInput(search); search.SetMinimumHeight(Dp(48));
            listCol.AddView(search, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(6) });
            var rows = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var scroll = new ScrollView(this) { VerticalScrollBarEnabled = false }; scroll.AddView(rows);
            listCol.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
            // Console.
            var console = new LinearLayout(this) { Orientation = Orientation.Vertical };
            console.Background = new DarkPanelDrawable(density, DecoTier.M) { CutTopRightDp = 14, ShowEmblem = false, TideOnTop = true };
            console.SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
            var who = new TextView(this) { TextSize = 13 }; who.SetTextColor(PgrArt.GoldLine); who.SetTypeface(PgrTheme.SansBold, global::Android.Graphics.TypefaceStyle.Bold);
            var dial = new FrameLayout(this);
            var ring = new VolumeRingDrawable(density);
            dial.Background = ring;
            var value = new TextView(this) { TextSize = 32, Gravity = GravityFlags.Center }; value.SetTextColor(Color.White); value.SetTypeface(PgrTheme.Condensed, global::Android.Graphics.TypefaceStyle.Bold); PgrTheme.Tabular(value);
            dial.AddView(value, new FrameLayout.LayoutParams(-1, -1));
            var top = new LinearLayout(this) { Orientation = Orientation.Horizontal }; top.SetGravity(GravityFlags.CenterVertical);
            var topText = new LinearLayout(this) { Orientation = Orientation.Vertical };
            topText.AddView(who);
            var note = new TextView(this) { Text = "保存后同时用于游戏配音、听书和试听；实际音量还会乘以总音量。", TextSize = 11 }; note.SetTextColor(PgrTheme.NavText); topText.AddView(note);
            top.AddView(topText, new LinearLayout.LayoutParams(0, -2, 1) { RightMargin = Dp(8) });
            top.AddView(dial, new LinearLayout.LayoutParams(Dp(92), Dp(92)));
            console.AddView(top);
            var slider = new SeekBar(this) { Max = 100 }; PgrTheme.StyleSeekBar(slider); slider.SetMinimumHeight(Dp(48));
            console.AddView(slider, new LinearLayout.LayoutParams(-1, Dp(48)));
            var ticks = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.No };
            foreach (var (t, g) in new[] { ("0", GravityFlags.Start), ("50", GravityFlags.Center), ("100", GravityFlags.End) })
            { var tv = new TextView(this) { Text = t, TextSize = 10, Gravity = g }; tv.SetTextColor(PgrTheme.NavSub); tv.SetTypeface(PgrTheme.Condensed, global::Android.Graphics.TypefaceStyle.Bold); ticks.AddView(tv, new LinearLayout.LayoutParams(0, -2, 1)); }
            ticks.SetPadding(Dp(16), 0, Dp(16), Dp(6)); console.AddView(ticks);
            var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            void Save(int percent) { if (selected == null) return; session.SetSpeakerVolume(selected, percent); if (session.HasSettingsSaveWarning) Info("设置尚未保存", session.Status); selected = null; sheet!.Relayout(); }
            var cancel = DarkKey(keys, "取消", Xb.Dark, "close", () => { if (selected == null) sheet!.Dismiss(); else { selected = null; sheet!.Relayout(); } });
            var reset = DarkKey(keys, "恢复 100%", Xb.Dark, "replay", () => Save(100));
            var save = DarkKey(keys, "保存", Xb.Primary, "volume", () => Save(slider.Progress));
            console.AddView(keys);
            void ShowConsole()
            {
                bool on = selected != null;
                who.Text = on ? SpeakerVolume.DisplayName(selected!) : "角色音量";
                slider.Enabled = reset.Enabled = save.Enabled = on;
                slider.Progress = pending; value.Text = on ? $"{pending}%" : "—"; ring.Value = on ? pending / 100f : 0; ring.InvalidateSelf();
                slider.ContentDescription = on ? SpeakerVolume.DisplayName(selected!) + "的音量百分比" : "角色音量";
            }
            slider.ProgressChanged += (_, e) => { if (!e.FromUser) return; pending = e.Progress; value.Text = $"{pending}%"; ring.Value = pending / 100f; ring.InvalidateSelf(); };
            void Refresh()
            {
                rows.RemoveAllViews();
                foreach (string n in names.Where(n => SpeakerVolume.DisplayName(n).Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    string speaker = n; int pct = SpeakerVolume.GetPercent(session.Settings.SpeakerVolumes, n);
                    var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true }; row.SetGravity(GravityFlags.CenterVertical);
                    row.SetMinimumHeight(Dp(52)); row.SetPadding(Dp(10), Dp(4), Dp(10), Dp(4));
                    bool on = speaker == selected;
                    row.Background = PgrTheme.Plate(this, on ? PgrTheme.A(PgrArt.TideRed, 60) : PgrArt.DeckButton, PgrTheme.A(PgrTheme.OnPlate, on ? 140 : 40), PgrTheme.CutSmall, Corners.BottomRight, on ? PgrArt.TideRed : null, AccentEdge.Left);
                    row.AddView(SpeakerAvatar(SpeakerVolume.DisplayName(n), 30, true), new LinearLayout.LayoutParams(Dp(30), Dp(30)) { RightMargin = Dp(10) });
                    var label = new TextView(this) { Text = SpeakerVolume.DisplayName(n), TextSize = 14 }; label.SetTextColor(PgrTheme.OnPlate); label.SetSingleLine(true); label.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                    row.AddView(label, new LinearLayout.LayoutParams(0, -2, 1));
                    var pctView = new TextView(this) { Text = pct + "%", TextSize = 13 }; pctView.SetTypeface(PgrTheme.Condensed, global::Android.Graphics.TypefaceStyle.Bold); pctView.SetTextColor(pct == 100 ? PgrTheme.NavSub : PgrArt.GoldLine); PgrTheme.Tabular(pctView);
                    row.AddView(pctView);
                    row.ContentDescription = SpeakerVolume.DisplayName(n) + " · " + pct + "%";
                    row.Click += (_, _) => Safe(() => { selected = speaker; pending = SpeakerVolume.GetPercent(session.Settings.SpeakerVolumes, speaker); ShowConsole(); Refresh(); });
                    rows.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(4) });
                }
                if (rows.ChildCount == 0) { var none = new TextView(this) { Text = "没有匹配的角色", TextSize = 13 }; none.SetTextColor(PgrTheme.NavSub); rows.AddView(none); }
            }
            search.TextChanged += (_, _) => { query = search.Text ?? ""; Refresh(); };
            if (wide)
            {
                columns.AddView(listCol, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = Dp(10) });
                var rightCol = new LinearLayout(this) { Orientation = Orientation.Vertical };
                rightCol.AddView(help); rightCol.AddView(new View(this), new LinearLayout.LayoutParams(1, 0, 1)); rightCol.AddView(console);
                columns.AddView(rightCol, new LinearLayout.LayoutParams(0, -1, 1));
            }
            else
            {
                columns.AddView(help);
                columns.AddView(listCol, new LinearLayout.LayoutParams(-1, 0, 1));
                columns.AddView(console, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8), BottomMargin = Dp(30) });
            }
            s.Body.AddView(columns, new LinearLayout.LayoutParams(-1, -1));
            ShowConsole(); Refresh();
        }) { DarkMark = "VOLUME", WideWidthDp = 640 };
        sheet.Show();
    }

}
