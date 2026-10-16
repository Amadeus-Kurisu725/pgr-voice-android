using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

// 角色头像：用户导入的图片优先，其次内置的电脑版同款头像（assets/avatars），都没有时显示几何字标。背景立绘只显示用户导入的图片。章节封面另有内置图（assets/covers），用户导入的同名封面优先。
public sealed partial class MainActivity
{
    PortraitFrameDrawable? listeningPortraitFrame;
    View? listeningPortrait;
    TextView? listeningCallsign;
    StandeeDrawable? listeningStandee;
    string? listeningPortraitSpeaker, listeningStandeeSpeaker;

    void UpdatePortrait(View view, PortraitFrameDrawable frame, string? speaker, ref string? shown)
    {
        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim();
        if (shown == name) return;
        shown = name; frame.Name = name; frame.Image = null;
        PortraitStore.Request(view, name, standee: false, maxPx: Dp(96), image => { if (frame.Name == name) frame.Image = image; });
    }

    void UpdateStandee(View view, StandeeDrawable standee, string? speaker, ref string? shown)
    {
        string name = session.Settings.ShowStandee && !string.IsNullOrWhiteSpace(speaker) ? speaker.Trim() : "";
        if (shown == name) return;
        shown = name; standee.Image = null;
        if (name.Length == 0) return;
        string requested = name;
        string? Current() => listeningStandeeSpeaker;
        PortraitStore.Request(view, name, standee: true, maxPx: Dp(420), image => { if (Current() == requested) standee.Image = image; });
    }

    /// <summary>
    /// Dialogue-style speaker block on the listening card: large framed portrait, angled name plate with a construct
    /// frame tag over a thin rule, and the section's squad strip (speakers appearing in this section).
    /// </summary>
    void BuildListeningSpeakerRow(LinearLayout playing)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var row = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
        row.SetGravity(GravityFlags.Bottom);
        listeningPortraitFrame = new PortraitFrameDrawable(density);
        listeningPortrait = new View(this) { Background = listeningPortraitFrame, ImportantForAccessibility = ImportantForAccessibility.No };
        int size = Dp(CompactLayout ? 64 : 92);
        row.AddView(listeningPortrait, new LinearLayout.LayoutParams(size, size) { RightMargin = Dp(10) });
        var plateRow = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal, Background = new DialogueRuleDrawable(density) };
        plateRow.SetGravity(GravityFlags.CenterVertical); plateRow.SetPadding(0, 0, 0, Dp(6));
        listeningSpeaker!.Background = new NamePlateDrawable(density);
        listeningSpeaker.SetTextColor(PgrTheme.OnPlate); listeningSpeaker.SetSingleLine(true);
        listeningSpeaker.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        listeningSpeaker.SetPadding(Dp(12), Dp(5), Dp(22), Dp(7));
        plateRow.AddView(listeningSpeaker, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, 0) { RightMargin = Dp(6) });
        listeningCallsign = PgrTheme.Chip(this, ""); listeningCallsign.ImportantForAccessibility = ImportantForAccessibility.No;
        plateRow.AddView(listeningCallsign, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        row.AddView(plateRow, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1) { BottomMargin = Dp(4) });
        playing.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = Dp(10) });
        listeningStandee = new StandeeDrawable(density);
        playing.Background = new LayerDrawable([playing.Background!, listeningStandee]);
        listeningPortraitSpeaker = listeningStandeeSpeaker = null;
    }

    /// <summary>Squad strip: every speaker of the current section (horizontally scrollable via CastScroll), the active one framed in red.</summary>
    void BuildListeningSquad(LinearLayout parent)
    {
        listeningSquad = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
        listeningSquad.SetPadding(0, Dp(4), 0, Dp(2));
        parent.AddView(CastScroll.Wrap(listeningSquad), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        renderedSquadKey = null;
    }

    LinearLayout? listeningSquad;
    string? renderedSquadKey;
    /// <summary>06-B tray: SQUAD as small 22dp avatars without names (names stay in the content description).</summary>
    bool listeningSquadCompact;
    readonly List<(string Name, PortraitFrameDrawable Frame, TextView? Label)> squadMembers = new();

    void UpdateListeningSquad(string? activeSpeaker)
    {
        if (listeningSquad == null) return;
        var player = session.Listening;
        string? sectionId = player.CurrentItem?.SectionId;
        var names = sectionId == null ? new List<string>() : player.Items
            .Where(i => i.SectionId == sectionId && i.Node is { } n && !string.IsNullOrWhiteSpace(n.Speaker) && n.Speaker.Trim() != "旁白")
            .Select(i => i.Node!.Speaker.Trim()).Distinct(StringComparer.Ordinal).ToList();
        string key = sectionId + "|" + string.Join("|", names);
        if (key != renderedSquadKey)
        {
            renderedSquadKey = key; listeningSquad.RemoveAllViews(); squadMembers.Clear(); (listeningSquad.Parent as HorizontalScrollView)?.ScrollTo(0, 0);
            listeningSquad.Visibility = names.Count > 1 ? ViewStates.Visible : ViewStates.Gone;
            if (names.Count > 1)
            {
                if (listeningSquadCompact)
                    listeningSquad.AddView(PgrUi.En(this, "SQUAD", 7.5f, PgrTheme.NavSub), new LinearLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.CenterVertical, RightMargin = Dp(4) });
                else
                {
                var label = PgrTheme.Apply(Text("本节\nSQUAD", 9), PgrTheme.Role.Eyebrow, PgrTheme.Muted); label.SetAllCaps(false);
                listeningSquad.AddView(label, new LinearLayout.LayoutParams(Dp(44), ViewGroup.LayoutParams.WrapContent) { Gravity = GravityFlags.CenterVertical });
                }
                listeningSquad.ContentDescription = "本节出场角色：" + string.Join("、", names);
                float density = Resources!.DisplayMetrics!.Density;
                foreach (string name in names)
                {
                    var cell = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
                    cell.SetGravity(GravityFlags.CenterHorizontal);
                    var frame = new PortraitFrameDrawable(density) { ShowSerial = false, Name = name };
                    var view = new View(this) { Background = frame };
                    if (listeningSquadCompact)
                    {
                        listeningSquad.AddView(view, new LinearLayout.LayoutParams(Dp(22), Dp(22)) { RightMargin = Dp(3), Gravity = GravityFlags.CenterVertical });
                        squadMembers.Add((name, frame, null));
                        PortraitStore.Request(view, name, standee: false, maxPx: Dp(44), image => { if (frame.Name == name) frame.Image = image; });
                        continue;
                    }
                    cell.AddView(view, new LinearLayout.LayoutParams(Dp(40), Dp(40)));
                    var text = PgrTheme.Apply(Text(name, 10), PgrTheme.Role.Caption, PgrTheme.Secondary); text.TextSize = 10; text.SetSingleLine(true);
                    text.Gravity = GravityFlags.Center; cell.SetMinimumWidth(Dp(52)); // names are never cut: the cell grows, the strip scrolls
                    cell.AddView(text, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
                    listeningSquad.AddView(cell, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { RightMargin = Dp(4) });
                    squadMembers.Add((name, frame, text));
                    PortraitStore.Request(view, name, standee: false, maxPx: Dp(64), image => { if (frame.Name == name) frame.Image = image; });
                }
            }
        }
        foreach (var m in squadMembers)
        {
            bool on = m.Name == activeSpeaker?.Trim();
            if (m.Frame.Active != on) m.Frame.Active = on;
            m.Label?.SetTextColor(on ? PgrTheme.Foreground : PgrTheme.Muted);
        }
    }

    void UpdateListeningSpeakerDecor(string? speaker)
    {
        if (listeningPortrait == null || listeningPortraitFrame == null) return;
        UpdatePortrait(listeningPortrait, listeningPortraitFrame, speaker, ref listeningPortraitSpeaker);
        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim();
        string tag = PortraitStore.Serial(name);
        if (listeningCallsign != null && listeningCallsign.Text != tag) listeningCallsign.Text = tag;
        if (listeningStandee != null) UpdateStandee(listeningPortrait, listeningStandee, speaker, ref listeningStandeeSpeaker);
        UpdateListeningSquad(speaker);
    }

    /// <summary>Chapter cover for a hero header: imported 章节封面 (full pack title, title without "第N章", "第N章", category forms) first, then the bundled cover.</summary>
    void LoadChapterCover(View host, Action<Bitmap?> deliver, string? packTitle, string? packId, int maxPx)
    {
        if (string.IsNullOrWhiteSpace(packTitle)) return;
        string full = PackTitle(packTitle), bare = ChapterTitle(packTitle), number = ChapterNumber(packTitle);
        var names = new List<string> { full, bare, packTitle };
        // Category-prefixed names as commonly saved: "主线第03章", "间章_标题".
        string category = ChapterCatalog.Category(packId ?? "", packTitle);
        if (number != "—") { names.Add(category + "第" + number + "章"); names.Add("第" + number + "章"); names.Add("第" + int.Parse(number) + "章"); }
        if (bare.Length > 0) { names.Add(category.Replace("剧情", "") + "_" + bare); names.Add(category + "_" + bare); }
        PortraitStore.RequestCover(host, names.Distinct().Select(PortraitStore.Sanitize), maxPx, deliver, BundledCovers.Keys(packId, packTitle));
    }

    void ClearPortraitViews()
    {
        listeningPortrait = null; listeningPortraitFrame = null; listeningCallsign = null; listeningStandee = null;
        listeningSquad = null; renderedSquadKey = null; squadMembers.Clear(); listeningSquadCompact = false;
        listeningPortraitSpeaker = listeningStandeeSpeaker = null;
    }

    void PortraitSettings()
    {
        Section("角色立绘", "PORTRAITS");
        var toggle = Button(content, "背景立绘：" + (session.Settings.ShowStandee ? "显示" : "隐藏"), () =>
        {
            session.Settings.ShowStandee = !session.Settings.ShowStandee; session.SaveSettings(); Render();
        });
        ButtonIcon(toggle, "settings");
        toggle.ContentDescription = "听书卡片背景立绘，当前" + (session.Settings.ShowStandee ? "显示" : "隐藏") + "，点击切换";
        var row = Row(content);
        ButtonIcon(Button(row, "导入角色头像", () => PickPortraitImages(PortraitRequest)), "import");
        ButtonIcon(Button(row, "导入背景立绘", () => PickPortraitImages(StandeeRequest)), "import");
        ButtonIcon(Button(content, "导入章节封面", () => PickPortraitImages(CoverRequest)), "import");
        FoldNote(content, "图片命名与格式", "NAMING", new[] { "应用已内置主要章节封面和电脑版同款角色头像；导入同名封面或头像后优先显示导入的图片。立绘不内置，请使用自己有权使用的图片：头像、立绘以角色名命名（例如“莉瑟.png”），章节封面以章节名命名（例如“第03章 灰烬回声.jpg”或“灰烬回声.jpg”），支持 PNG / JPG / WebP。没有图片时显示几何字标或默认页眉。" });
        var folders = PgrTheme.Apply(Text("头像文件夹：\n" + PortraitStore.Folder(this, false) + "\n立绘文件夹：\n" + PortraitStore.Folder(this, true) + "\n章节封面文件夹：\n" + PortraitStore.Folder(this, PortraitStore.CoverFolder), 11), PgrTheme.Role.Code, PgrTheme.Secondary);
        folders.SetLineSpacing(Dp(3), 1); folders.SetTextIsSelectable(true);
        FoldNote(content, "图片文件夹 · 3 个", "FOLDERS", new[] { "头像文件夹：" + PortraitStore.Folder(this, false) }, folders);
    }

    void PickPortraitImages(int request)
    {
        var intent = new Intent(Intent.ActionOpenDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("image/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, true);
        StartActivityForResult(intent, request);
    }

    void ImportPortraitImages(Intent data, string sub)
    {
        var uris = new List<global::Android.Net.Uri>();
        if (data.ClipData is { } clip) for (int i = 0; i < clip.ItemCount; i++) { if (clip.GetItemAt(i)?.Uri is { } u) uris.Add(u); }
        else if (data.Data != null) uris.Add(data.Data);
        var names = new List<string>(); var errors = new List<string>();
        foreach (var uri in uris)
            try { names.Add(PortraitStore.Import(this, uri, sub)); }
            catch (Exception ex) { errors.Add(ex.Message); }
        currentPortraitSpeaker = null; listeningPortraitSpeaker = listeningStandeeSpeaker = null;
        Info(sub == PortraitStore.StandeeFolder ? "导入背景立绘" : sub == PortraitStore.CoverFolder ? "导入章节封面" : "导入角色头像",
            (names.Count > 0 ? "已导入：" + string.Join("、", names) : "没有导入图片。") + (errors.Count > 0 ? "\n\n" + string.Join("\n", errors) : ""));
        Render();
    }
}
