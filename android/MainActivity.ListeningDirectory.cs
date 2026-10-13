using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    Pack? listeningBrowsePack;
    string? listeningBrowseChapter, listeningBrowseSection, listeningSelectedKey;
    bool listeningBrowsePinned, listeningRestoreScroll;
    int listeningBrowseTop, listeningBrowseOffset;
    IReadOnlyList<ListeningItem>? listeningDirectoryPlan;
    ListeningDirectory? listeningDirectory;
    LinearLayout? listeningDirectoryCard;
    ListView? listeningDirectoryList;
    ListeningDirectoryAdapter? listeningDirectoryAdapter;
    TextView? listeningDirectoryCount, listeningBrowseHint;
    Button? listeningSectionButton, listeningLocateButton, listeningPendingButton;
    string? listeningHighlightedItem;
    TextView? listeningSectionName, listeningSectionNumber, listeningSectionSource;
    FrameLayout? listeningCastHost;
    string? listeningCastSection;

    void ClearListeningDirectoryViews()
    {
        if (listeningDirectoryList != null)
        {
            listeningBrowseTop = listeningDirectoryList.FirstVisiblePosition;
            listeningBrowseOffset = listeningDirectoryList.GetChildAt(0)?.Top ?? 0;
            listeningRestoreScroll = true;
        }
        listeningDirectoryCard = null; listeningDirectoryList = null; listeningDirectoryAdapter = null;
        listeningDirectoryCount = listeningBrowseHint = null;
        listeningSectionButton = listeningLocateButton = listeningPendingButton = null;
        listeningSectionName = listeningSectionNumber = listeningSectionSource = null; listeningCastHost = null; listeningCastSection = null;
        listeningDirectoryPlan = null;
        if (session.Listening.Pack == null)
        { listeningBrowsePack = null; listeningDirectory = null; listeningBrowseChapter = listeningBrowseSection = listeningSelectedKey = null; }
    }

    void BuildListeningDirectory()
    {
        var card = listeningDirectoryCard = Card(content);
        card.SetPadding(Dp(10), Dp(10), Dp(10), Dp(12));
        float density = Resources!.DisplayMetrics!.Density;
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.Panel, PgrTheme.Border, PgrTheme.CutMedium, Corners.Diagonal, brackets: PgrTheme.A(PgrTheme.Slate, 170),
            sheen: PgrTheme.SheenLight, shadow: true), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.M) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium, InsetDp = 3 });
        // 172dp cover band (02-B): chapter cover under a slate veil (S deco); 小节目录 SCRIPT + emblem + 灰鸦 · GRAY RAVEN;
        // outlined section number; 「小节：」+ name + source tag; slanted 换小节 button; cast strip; crimson short line.
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.S) { VeilAlpha = 205, CutTopRightDp = 16 };
        band.Background = face;
        LoadChapterCover(band, image => face.Cover = image, session.Listening.Pack?.Title, session.Listening.Pack?.Id, Dp(480));
        var top = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        top.SetGravity(GravityFlags.CenterVertical);
        top.AddView(new View(this) { Background = PgrTheme.Triangle(this) }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var zone = new TextView(this) { Text = "小节目录", TextSize = 14 }; zone.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); zone.SetTextColor(PgrTheme.OnPlate); top.AddView(zone);
        top.AddView(PgrUi.En(this, "SCRIPT", 9, PgrTheme.NavSub), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(7), RightMargin = Dp(12) });
        if (!NarrowPage)
        {
            top.AddView(new View(this) { Background = new EmblemDrawable(density) }, new LinearLayout.LayoutParams(Dp(14), Dp(16)) { RightMargin = Dp(4) });
            top.AddView(PgrUi.En(this, "灰鸦 · GRAY RAVEN", 9, PgrTheme.NavText));
        }
        band.AddView(top, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.Start) { LeftMargin = Dp(12), TopMargin = Dp(12) });
        listeningSectionNumber = PgrUi.Outline(this, "", NarrowPage ? 34 : 44, Color.White, 1.3f);
        band.AddView(listeningSectionNumber, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.End) { RightMargin = Dp(14), TopMargin = Dp(6) });
        var middle = new LinearLayout(this) { Orientation = Orientation.Horizontal }; middle.SetGravity(GravityFlags.CenterVertical);
        var names = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var prefix = new TextView(this) { Text = "小节：", TextSize = 11 }; prefix.SetTextColor(PgrTheme.NavText); names.AddView(prefix);
        var nameRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; nameRow.SetGravity(GravityFlags.CenterVertical);
        listeningSectionName = new TextView(this) { TextSize = 18 }; listeningSectionName.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); listeningSectionName.SetTextColor(Color.White);
        listeningSectionName.SetSingleLine(true); listeningSectionName.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        nameRow.AddView(listeningSectionName, new LinearLayout.LayoutParams(0, -2, 1) { Weight = 1 });
        listeningSectionSource = PgrUi.Chip(this, "", PgrUi.Status.NotStarted);
        listeningSectionSource.SetTextColor(PgrTheme.Foreground); listeningSectionSource.Background = PgrTheme.Plate(this, PgrTheme.A(Color.White, 230), null, 4, Corners.BottomRight);
        nameRow.AddView(listeningSectionSource, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        names.AddView(nameRow);
        middle.AddView(names, new LinearLayout.LayoutParams(0, -2, 1));
        listeningSectionButton = new Button(this) { Text = "换小节", TextSize = 13 };
        listeningSectionButton.Click += (_, _) => Safe(ShowListeningSections);
        PgrTheme.StyleChipButton(listeningSectionButton, "story");
        listeningSectionButton.SetCompoundDrawablesRelativeWithIntrinsicBounds(new PgrIconDrawable("story", PgrTheme.Slate, density, 15), null, new PgrIconDrawable("chevron_down", PgrTheme.Muted, density, 14), null);
        middle.AddView(listeningSectionButton, new LinearLayout.LayoutParams(-2, Dp(PgrTheme.Touch)) { LeftMargin = Dp(8) });
        band.AddView(middle, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Start | GravityFlags.CenterVertical) { LeftMargin = Dp(12), RightMargin = Dp(10), TopMargin = Dp(10) });
        listeningCastHost = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        band.AddView(listeningCastHost, new FrameLayout.LayoutParams(-1, -2, GravityFlags.Bottom | GravityFlags.Start) { LeftMargin = Dp(12), RightMargin = Dp(10), BottomMargin = Dp(12) });
        var redLine = new View(this) { ImportantForAccessibility = ImportantForAccessibility.No }; redLine.SetBackgroundColor(PgrTheme.Red);
        band.AddView(redLine, new FrameLayout.LayoutParams(Dp(44), Dp(3), GravityFlags.Bottom | GravityFlags.Start));
        card.AddView(band, new LinearLayout.LayoutParams(-1, Dp(172)) { BottomMargin = Dp(6) });
        listeningDirectoryCount = PgrTheme.Apply(Text("", 11), PgrTheme.Role.Code, PgrTheme.Secondary); PgrTheme.Tabular(listeningDirectoryCount); listeningDirectoryCount.SetPadding(Dp(4), Dp(4), 0, 0); card.AddView(listeningDirectoryCount);
        listeningBrowseHint = PgrTheme.Apply(Text("", 12), PgrTheme.Role.Caption, PgrTheme.Steel); listeningBrowseHint.SetPadding(Dp(4), Dp(2), 0, Dp(6)); card.AddView(listeningBrowseHint);
        // One framed list: rows are contiguous (index column + accent edge), no gaps between plates.
        var list = listeningDirectoryList = new ListView(this) { Divider = null, DividerHeight = 0 };
        list.Background = PgrTheme.Plate(this, PgrTheme.A(PgrTheme.Ice, 90), PgrTheme.Border, PgrTheme.CutSmall, Corners.BottomRight);
        list.SetPadding(Dp(1), Dp(1), Dp(1), Dp(1)); list.SetClipToPadding(true);
        list.Selector = new ColorDrawable(Color.Transparent);
        list.ContentDescription = "小节完整台词列表，点选后用下方按钮定位";
        listeningDirectoryAdapter = new(this); list.Adapter = listeningDirectoryAdapter;
        int screenHeight = Resources!.Configuration!.ScreenHeightDp;
        int height = CompactLayout ? Math.Clamp(screenHeight / 2, 140, 210) : Math.Clamp(screenHeight / 3, 230, 330);
        card.AddView(list, new LinearLayout.LayoutParams(-1, Dp(height)));
        list.SetOnTouchListener(new ListeningListTouch(this));
        list.ItemClick += (_, args) => Safe(() =>
        {
            if (!ReferenceEquals(list, listeningDirectoryList) || listeningDirectory == null || args.Position >= listeningDirectory.Entries.Count) return;
            listeningBrowsePinned = true; listeningSelectedKey = listeningDirectory.Entries[args.Position].Key;
            UpdateListeningDirectory();
        });
        // Actions as chamfered chips with line icons (each a full 48dp hit target).
        var actions = Row(card); actions.SetPadding(0, Dp(4), 0, 0);
        listeningLocateButton = Button(actions, "定位到选中台词", () => LocateListeningDirectory(listeningSelectedKey));
        PgrUi.Style(listeningLocateButton, Xb.Normal, "locate", textSp: 13);
        var back = Button(actions, "回到正在听", () =>
        { listeningBrowsePinned = false; UpdateListeningDirectory(true); ScrollToListeningDirectory(); });
        PgrUi.Style(back, Xb.Normal, "headphones", textSp: 13);
        var sectionActions = Row(card);
        var start = Button(sectionActions, "定位到小节开头", () =>
        {
            var player = session.Listening;
            if (!ListeningDirectoryOwnerMatches() || listeningBrowseSection == null) return;
            string target = listeningBrowseSection;
            player.JumpToSection(target);
            if (player.CurrentItem?.SectionId == target)
            { listeningBrowsePinned = false; UpdateListeningDirectory(true); ScrollToListeningDirectory(); OpenDirectoryChoices(); }
        });
        PgrUi.Style(start, Xb.Normal, "previous", textSp: 13);
        listeningPendingButton = Button(sectionActions, "打开本节待选互动", () => LocateListeningDirectory(listeningDirectory?.PendingKey));
        PgrUi.Style(listeningPendingButton, Xb.Brass, "branch", textSp: 13);
    }

    bool ListeningDirectoryOwnerMatches() => ReferenceEquals(listeningBrowsePack, session.Listening.Pack)
        && listeningBrowseChapter == session.Listening.Chapter?.Id;

    void UpdateListeningDirectory(bool scrollToSelection = false)
    {
        var player = session.Listening;
        if (listeningDirectoryList == null || player.Pack == null || player.Chapter == null) return;
        bool newOwner = !ListeningDirectoryOwnerMatches();
        if (newOwner)
        {
            listeningBrowsePack = player.Pack; listeningBrowseChapter = player.Chapter.Id;
            listeningBrowsePinned = false; listeningBrowseSection = listeningSelectedKey = null;
            listeningRestoreScroll = false;
        }
        string? currentSection = player.CurrentItem?.SectionId;
        if (!listeningBrowsePinned || !player.Chapter.Sections.Any(s => s.Id == listeningBrowseSection))
            listeningBrowseSection = currentSection ?? SectionDisplay.FirstListed(player.Chapter.Sections)?.Id;
        if (listeningBrowseSection == null) return;
        bool changed = newOwner || !ReferenceEquals(listeningDirectoryPlan, player.Items)
            || listeningDirectory?.SectionId != listeningBrowseSection;
        if (changed)
        {
            listeningDirectoryPlan = player.Items;
            listeningDirectory = player.ReadDirectory(listeningBrowseSection);
        }
        if (listeningDirectory == null) return;
        var rows = listeningDirectory.Entries;
        bool currentChanged = listeningHighlightedItem != player.CurrentItem?.Id;
        listeningHighlightedItem = player.CurrentItem?.Id;
        if (!listeningBrowsePinned)
        {
            listeningSelectedKey = rows.FirstOrDefault(e => e.ItemId != null && e.ItemId == listeningHighlightedItem)?.Key;
            if (player.HasBlockingNotice && currentSection == listeningBrowseSection)
            {
                var previous = player.Items.TakeWhile(x => x.Id != player.CurrentItem?.Id)
                    .LastOrDefault(x => x.Kind == PgrVoice.Listening.ListeningItemKind.Line && x.SectionId == currentSection);
                listeningSelectedKey = rows.FirstOrDefault(e => previous != null && e.NodeId == previous.NodeId)?.Key
                    ?? rows.FirstOrDefault(e => e.IsPreview)?.Key;
            }
        }
        if (!rows.Any(e => e.Key == listeningSelectedKey)) listeningSelectedKey = rows.FirstOrDefault()?.Key;
        listeningDirectoryAdapter!.NotifyDataSetChanged();
        var section = player.Chapter.Sections.First(s => s.Id == listeningBrowseSection);
        string sectionTitle = SectionDisplay.Title(player.Chapter.Sections, section.Id);
        var (sectionNumber, sectionName, sectionSource) = SectionParts(sectionTitle);
        listeningSectionName!.Text = sectionNumber.Length > 0 ? sectionNumber + " " + sectionName : sectionName;
        listeningSectionNumber!.Text = sectionNumber;
        listeningSectionSource!.Text = sectionSource ?? "";
        listeningSectionSource.Visibility = sectionSource == null ? ViewStates.Gone : ViewStates.Visible;
        listeningSectionButton!.ContentDescription = "换小节，当前小节：" + sectionTitle;
        if (listeningCastHost != null && listeningCastSection != listeningBrowseSection)
        {
            listeningCastSection = listeningBrowseSection;
            listeningCastHost.RemoveAllViews();
            var group = SectionDisplay.Groups(player.Chapter.Sections).FirstOrDefault(g => g.Sections.Any(x => x.Id == listeningBrowseSection));
            listeningCastHost.AddView(CastStrip(player.Pack, (group?.Sections.Select(x => x.Id)) ?? new[] { listeningBrowseSection }), new FrameLayout.LayoutParams(-1, -2));
        }
        listeningDirectoryCount!.Text = $"完整正文 {listeningDirectory.LineCount} 条（含动作提示）· 浏览不播放";
        listeningBrowseHint!.Text = currentSection != null && currentSection != listeningBrowseSection
            ? "正在浏览：" + sectionTitle + "；收听仍在：" + SectionDisplay.Title(player.Chapter.Sections, player.CurrentItem!.SectionId)
            : player.HasBlockingNotice ? "当前路线等待确认。正文仍可阅读，未接入句可单句试听；目录顺序不代表游戏下一句。"
            : listeningBrowsePinned ? "浏览位置已保留；点“回到正在听”跟回当前句。" : "红色标记当前收听句，点台词可选中。";
        var selected = rows.FirstOrDefault(e => e.Key == listeningSelectedKey);
        listeningLocateButton!.Text = selected?.IsChoice == true ? "打开选中互动" : selected?.IsPreview == true
            ? "只试听选中台词" : "定位到选中台词";
        listeningLocateButton.Enabled = selected != null;
        listeningPendingButton!.Visibility = listeningDirectory.PendingItemId == null ? ViewStates.Gone : ViewStates.Visible;
        if (scrollToSelection || (!listeningBrowsePinned && (newOwner || changed || currentChanged)))
        {
            int index = rows.ToList().FindIndex(e => e.Key == listeningSelectedKey);
            ScrollListeningList(Math.Max(0, index), 0);
        }
        else if (listeningRestoreScroll) ScrollListeningList(listeningBrowseTop, listeningBrowseOffset);
        listeningRestoreScroll = false;
    }

    void ScrollListeningList(int position, int offset)
    {
        var list = listeningDirectoryList; string? section = listeningBrowseSection, selected = listeningSelectedKey;
        list?.Post(() =>
        {
            if (alive && ReferenceEquals(list, listeningDirectoryList) && section == listeningBrowseSection && selected == listeningSelectedKey)
                list!.SetSelectionFromTop(position, offset);
        });
    }

    void ScrollToListeningDirectory()
    {
        var card = listeningDirectoryCard;
        pageScroll.Post(() => { if (alive && ReferenceEquals(card, listeningDirectoryCard) && card != null) pageScroll.SmoothScrollTo(0, card.Top); });
    }

    void LocateListeningDirectory(string? key)
    {
        if (!ListeningDirectoryOwnerMatches() || key == null || listeningBrowseSection == null) return;
        var player = session.Listening;
        var entry = player.ReadDirectory(listeningBrowseSection)?.Entries.FirstOrDefault(x => x.Key == key);
        if (entry?.IsPreview == true)
        {
            var owner = player.Pack; string sectionId = listeningBrowseSection;
            player.Pause();
            Confirm("只试听这一句", AppSession.SpeakerLabel(entry.Speaker) + "：" + entry.Text + "\n\n这句尚未接入当前路线。只播放选中的一句，结束后停住；原收听位置与选择保持。", () =>
            {
                if (!ReferenceEquals(owner, player.Pack) || !ListeningDirectoryOwnerMatches()) return;
                RequestNotifications(); player.PreviewDirectoryEntry(sectionId, key);
            });
            return;
        }
        if (!player.LocateDirectoryEntry(listeningBrowseSection, key))
        { Info("这句仅供阅读", player.Status); return; }
        listeningBrowsePinned = false; UpdateListeningDirectory(true); ScrollToListeningDirectory(); OpenDirectoryChoices();
    }

    void OpenDirectoryChoices()
    {
        var player = session.Listening;
        if (player.Choices.Count == 0) return;
        var owner = player.Pack; var plan = player.Items; string? currentId = player.CurrentItem?.Id;
        var choices = player.Choices.ToArray();
        Choose("待选互动 · 选择后继续收听", choices.Select(c => c.Label).ToArray(), i =>
        {
            if (!ReferenceEquals(owner, player.Pack) || !ReferenceEquals(plan, player.Items) || currentId != player.CurrentItem?.Id)
            { Info("收听位置已变化", "请重新打开当前互动选项。"); return; }
            RequestNotifications(); player.Choose(choices[i].Id);
        });
    }

    sealed class ListeningDirectoryAdapter(MainActivity owner) : BaseAdapter<ListeningDirectoryEntry>
    {
        public override int Count => owner.listeningDirectory?.Entries.Count ?? 0;
        public override ListeningDirectoryEntry this[int position] => owner.listeningDirectory!.Entries[position];
        public override long GetItemId(int position) => position;
        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var entry = this[position];
            var row = convertView as LinearLayout;
            float density = owner.Resources!.DisplayMetrics!.Density;
            if (row == null)
            {
                // [index column][accent edge][avatar][who / text / note][route chip]
                row = new LinearLayout(owner) { Orientation = Orientation.Horizontal, Background = new DirectoryRowDrawable(density) };
                row.SetGravity(GravityFlags.CenterVertical); row.SetMinimumHeight(owner.Dp(52));
                // Narrow landscape page (~731dp screens): slimmer index column and avatar so the line keeps its width.
                bool narrow = owner.NarrowPage;
                row.SetPadding(0, owner.Dp(8), owner.Dp(narrow ? 6 : 10), owner.Dp(8));
                row.Foreground = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink, 30)), null, new ColorDrawable(Color.White));
                var indexView = new TextView(owner) { TextSize = 12, Gravity = GravityFlags.Center };
                indexView.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); indexView.LetterSpacing = .06f; PgrTheme.Tabular(indexView);
                row.AddView(indexView, new LinearLayout.LayoutParams(owner.Dp(narrow ? 32 : 40), -2));
                var avatar = new View(owner) { Background = new PortraitFrameDrawable(density) { ShowSerial = false }, ImportantForAccessibility = ImportantForAccessibility.No };
                row.AddView(avatar, new LinearLayout.LayoutParams(owner.Dp(narrow ? 26 : 30), owner.Dp(narrow ? 26 : 30)) { LeftMargin = owner.Dp(narrow ? 6 : 12), RightMargin = owner.Dp(narrow ? 6 : 10) });
                var bodyView = new LinearLayout(owner) { Orientation = Orientation.Vertical };
                // Who line: speaker + construct-serial micro tag.
                var who = new LinearLayout(owner) { Orientation = Orientation.Horizontal }; who.SetGravity(GravityFlags.CenterVertical);
                var name = owner.Text("", 11); name.SetPadding(0, owner.Dp(1), 0, owner.Dp(1)); name.SetTypeface(PgrTheme.SansMedium, TypefaceStyle.Normal);
                name.SetSingleLine(true); name.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                who.AddView(name, new LinearLayout.LayoutParams(-2, -2, 0));
                var serial = new TextView(owner) { TextSize = 8.5f }; serial.SetTypeface(PgrTheme.Mono, TypefaceStyle.Normal);
                serial.SetTextColor(PgrTheme.Steel); serial.ImportantForAccessibility = ImportantForAccessibility.No;
                serial.Background = PgrTheme.Plate(owner, PgrTheme.A(PgrTheme.Slate, 18), PgrTheme.A(PgrTheme.Slate, 110), PgrTheme.CutSmall * .5f, Corners.BottomRight);
                serial.SetPadding(owner.Dp(4), 0, owner.Dp(5), 0);
                who.AddView(serial, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = owner.Dp(6) });
                bodyView.AddView(who, new LinearLayout.LayoutParams(-1, -2));
                foreach (int size in new[] { 15, 11 })
                {
                    var text = owner.Text("", size); text.SetPadding(0, owner.Dp(1), 0, owner.Dp(1));
                    bodyView.AddView(text, new LinearLayout.LayoutParams(-1, -2));
                }
                ((TextView)bodyView.GetChildAt(1)!).TextSize = 15;
                row.AddView(bodyView, new LinearLayout.LayoutParams(0, -2, 1));
                var chip = PgrTheme.Chip(owner, "分支 · ROUTE"); chip.SetTextColor(PgrTheme.BrassText);
                chip.Background = PgrTheme.Plate(owner, PgrTheme.A(PgrTheme.Brass, 30), PgrTheme.Brass, PgrTheme.CutSmall * .7f, Corners.BottomRight);
                row.AddView(chip, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = owner.Dp(6) });
            }
            bool selected = entry.Key == owner.listeningSelectedKey;
            bool current = entry.ItemId != null && entry.ItemId == owner.session.Listening.CurrentItem?.Id;
            string speaker = string.IsNullOrWhiteSpace(entry.Speaker) ? "旁白" : entry.Speaker.Trim();
            // Current line: lifted white row with a crimson index cell. Browsing selection: lifted with a slate outline.
            // Branch rows: brass wash. Others alternate a faint mist-blue tint.
            var face = (DirectoryRowDrawable)row.Background!;
            face.Lifted = current || selected;
            face.Fill = current || selected ? PgrTheme.Ice : entry.IsChoice ? PgrTheme.BrassTint : position % 2 == 1 ? PgrTheme.MistTint : Color.Transparent;
            face.IndexFill = current ? PgrTheme.Red : null; face.Slash = current;
            face.Outline = selected && !current ? PgrTheme.Slate : null;
            face.Accent = entry.IsChoice ? PgrTheme.Brass : PgrTheme.SpeakerAccent(speaker);
            face.InvalidateSelf();
            var index = (TextView)row.GetChildAt(0)!;
            index.Text = entry.IsChoice ? "◆" : entry.Number.ToString("000");
            index.SetTextColor(current ? PgrTheme.OnAccent : entry.IsChoice ? PgrTheme.BrassText : selected ? PgrTheme.Steel : PgrTheme.Secondary);
            var avatarView = row.GetChildAt(1)!; var frame = (PortraitFrameDrawable)avatarView.Background!;
            avatarView.Visibility = entry.IsChoice ? ViewStates.Gone : ViewStates.Visible;
            if (!entry.IsChoice && frame.Name != speaker)
            {
                frame.Name = speaker; frame.Image = null;
                PortraitStore.Request(avatarView, speaker, standee: false, maxPx: owner.Dp(48), image => { if (frame.Name == speaker) frame.Image = image; });
            }
            if (frame.Active != current) frame.Active = current;
            var body = (LinearLayout)row.GetChildAt(2)!;
            var whoRow = (LinearLayout)body.GetChildAt(0)!;
            var heading = (TextView)whoRow.GetChildAt(0)!;
            var serialTag = (TextView)whoRow.GetChildAt(1)!;
            bool tagged = !entry.IsChoice && speaker != "旁白";
            serialTag.Visibility = tagged ? ViewStates.Visible : ViewStates.Gone;
            if (tagged) serialTag.Text = PortraitStore.Serial(speaker);
            heading.Text = entry.Speaker + (current ? " · 当前收听" : "");
            heading.SetTextColor(current ? PgrTheme.RedText : entry.IsChoice ? PgrTheme.BrassText : PgrTheme.Secondary);
            ((TextView)body.GetChildAt(1)!).Text = entry.Text;
            var note = (TextView)body.GetChildAt(2)!;
            note.Text = entry.IsPreview ? "未接入当前路线 · 可选中后只试听这句" : "";
            note.SetTextColor(PgrTheme.Muted); note.Visibility = entry.IsPreview ? ViewStates.Visible : ViewStates.Gone;
            row.GetChildAt(3)!.Visibility = entry.IsChoice ? ViewStates.Visible : ViewStates.Gone;
            return row;
        }
    }

    sealed class ListeningListTouch(MainActivity owner) : Java.Lang.Object, View.IOnTouchListener
    {
        float startY;
        public bool OnTouch(View? view, MotionEvent? e)
        {
            if (view == null || e == null) return false;
            if (e.ActionMasked == MotionEventActions.Down)
            { startY = e.GetY(); owner.listeningBrowsePinned = true; view.Parent?.RequestDisallowInterceptTouchEvent(true); }
            else if (e.ActionMasked == MotionEventActions.Move)
                view.Parent?.RequestDisallowInterceptTouchEvent(view.CanScrollVertically(e.GetY() < startY ? 1 : -1));
            else if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
                view.Parent?.RequestDisallowInterceptTouchEvent(false);
            return false;
        }
    }
}
