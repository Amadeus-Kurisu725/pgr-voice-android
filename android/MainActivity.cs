using PgrVoice.Listening;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media.Projection;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ocr;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.AndroidApp.Ui;
using PgrVoice.AndroidApp.Contracts;

namespace PgrVoice.AndroidApp;

[Activity(Label="战双剧情配音",MainLauncher=true,Exported=true,LaunchMode=LaunchMode.SingleTop,
    ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.SmallestScreenSize|ConfigChanges.ScreenLayout|ConfigChanges.KeyboardHidden)]
public sealed partial class MainActivity : Activity
{
    const int ImportRequest=10,CaptureRequest=11,ExportRequest=12,OverlayRequest=13,RestoreRequest=14,PortraitRequest=15,StandeeRequest=16,CoverRequest=17;
    AppSession session=null!;
    LinearLayout root=null!,content=null!,currentCard=null!,positionRow=null!,headingRow=null!,tabRow=null!,titleBlock=null!;
    View tabDivider=null!;
    /// <summary>Activity content: [nav rail (wide landscape)] + root. Split columns host two-column pages in wide landscape.</summary>
    LinearLayout shell=null!,navRail=null!,splitHost=null!,splitLeftColumn=null!,splitLeftBar=null!,splitLeft=null!,splitRight=null!,mainContent=null!;
    ScrollView splitLeftScroll=null!,splitRightScroll=null!;
    readonly List<Button> railButtons=new();
    Button railOrientationButton=null!;
    ScrollView pageScroll=null!;
    TextView status=null!,current=null!,title=null!,titleCode=null!,currentMeta=null!,offlineTag=null!;
    Button confirmPositionButton=null!,orientationButton=null!;
    ImageView headingIcon=null!;
    IReadOnlyList<MatchCandidate>? renderedLocateCandidates;
    TextView? autoPlaybackState;
    Button? autoPlaybackStopButton,autoPlaybackPermissionButton;
    Button? captureStatusButton;
    Action? captureStatusTick;
    bool captureStatusUiVisible,captureStatusTickScheduled;
    readonly List<Button> tabButtons=new();
    readonly string[] tabIcons={"archive","story","locate","branch","history","settings"};
    readonly int[] tabPages={0,1,2,6,3,4};
    readonly string[] tabLabels={"章节","剧情","定位","分支","历史","设置"};
    readonly string[] tabEnglish={"CHAPTER","STORY","LOCATE","BRANCH","HISTORY","SETTINGS"};
    /// <summary>Nav status diamonds: teal-grey on 剧情 while following, brass on 分支 while a branch waits (display only).</summary>
    readonly Color?[] tabDots=new Color?[6];
    SubtitleRegionOverlay? subtitleRegionOverlay;
    int styledTab=-1;
    int page,linePage,historyPage;
    string search="",exportText="";
    CancellationTokenSource? importCancellation;
    bool alive,returnToGameAfterCapture,captureRequestPending;
    View currentPortrait=null!;
    LinearLayout currentBody=null!;
    PortraitFrameDrawable currentPortraitFrame=null!;
    string? currentPortraitSpeaker;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);alive=true;
        PgrVoice.AndroidApp.Ui.FusionDeco.FontScale=Resources!.Configuration!.FontScale;
        session=AppSession.Get(this);
        RegisterBackNavigation();
        returnToGameAfterCapture=savedInstanceState?.GetBoolean("return_to_game_after_capture",false)??false;
        captureRequestPending=savedInstanceState?.GetBoolean("capture_request_pending",false)??false;
        shell=new LinearLayout(this){Orientation=Orientation.Horizontal};
        shell.Background=new HudBackdropDrawable(Resources!.DisplayMetrics!.Density);
        root=new LinearLayout(this){Orientation=Orientation.Vertical};root.SetPadding(Dp(16),Dp(28),Dp(16),Dp(20));
        CreateNavRail();
        shell.AddView(root,new LinearLayout.LayoutParams(0,ViewGroup.LayoutParams.MatchParent,1));
        SetContentView(shell);
        shell.SetOnApplyWindowInsetsListener(new SafeInsets(this));
        shell.RequestApplyInsets();
        headingFit=new FitRow(this);headingRow=headingFit;root.AddView(headingRow);headingRow.SetGravity(GravityFlags.CenterVertical);headingRow.SetPadding(0,0,0,Dp(14));
        headingRow.Background=new FusionHeaderDeco(Resources!.DisplayMetrics!.Density){BaselineDp=4};
        headingIcon=Icon("mark",PgrTheme.Foreground,30);headingRow.AddView(headingIcon,new LinearLayout.LayoutParams(Dp(34),Dp(36)){RightMargin=Dp(10)});
        titleBlock=new LinearLayout(this){Orientation=Orientation.Vertical};titleBlock.SetGravity(GravityFlags.CenterVertical);
        title=PgrTheme.Apply(new TextView(this){Text="战双剧情配音"},PgrTheme.Role.Title);titleBlock.AddView(title);
        title.SetMaxLines(1);title.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;
        titleCode=PgrTheme.Apply(new TextView(this){Text="PGR · STORY VOICE ARCHIVE"},PgrTheme.Role.Eyebrow);titleCode.SetMaxLines(1);titleBlock.AddView(titleCode); // 0.4.3 behaviour: word-boundary cut (no "ARC…")
        headingRow.AddView(titleBlock,new LinearLayout.LayoutParams(0,ViewGroup.LayoutParams.WrapContent,1));
        offlineTag=PgrTheme.Chip(this,"离线配音 · OFFLINE");headingRow.AddView(offlineTag);
        orientationButton=new Button(this){Text="横 / 竖屏",TextSize=12};PgrTheme.StyleButton(orientationButton,quiet:true);
        orientationButton.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable("orientation",PgrTheme.Secondary,Resources!.DisplayMetrics!.Density,16),null,null,null);orientationButton.CompoundDrawablePadding=Dp(5);
        orientationButton.Click+=(_,_)=>Safe(ChooseMainOrientation);
        headingRow.AddView(orientationButton,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent,ViewGroup.LayoutParams.WrapContent){LeftMargin=Dp(6)});
        // Narrow widths / large font or display scale: the title 「战双剧情配音」 must stay whole. Degrade the secondary items
        // instead: 1 tag drops its English part, 2 横/竖屏 becomes icon-only, 3 the tag hides, 4 title steps down 12%.
        headingFit.Levels=4;headingFit.ApplyLevel=ApplyHeaderLevel;
        headingFit.NeedWidth=()=>headingFit.Natural(headingIcon)+headingFit.Natural(title)+headingFit.Natural(offlineTag)+headingFit.Natural(orientationButton); // exact: degrade only where 0.4.3 would have ellipsized the title
        CreateChapterCategoryBar();
        pageScroll=new ScrollView(this){FillViewport=true};pageScroll.SetClipToPadding(false);pageScroll.VerticalScrollBarEnabled=false;
        pageScroll.ScrollChange+=(_,_)=>UpdateStickySide();
        content=new LinearLayout(this){Orientation=Orientation.Vertical};content.SetPadding(0,Dp(5),0,Dp(20));pageScroll.AddView(content);mainContent=content;
        root.AddView(pageScroll,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,0,1));
        CreateSplitHost();
        currentCard=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true};
        currentCard.Background=new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.Ink,40)),FusionDeco.Over(PgrTheme.Plate(this,PgrTheme.Panel,PgrTheme.BorderStrong,PgrTheme.CutMedium,Corners.Diagonal,PgrTheme.Red,AccentEdge.Left,sheen:PgrTheme.SheenLight,notch:40,shadow:true),
            currentDeco=new FusionDeco(Resources!.DisplayMetrics!.Density,DecoTier.M,FxKind.Cur){CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium,InsetDp=3,Ring=(16+28,0,34)}),PgrTheme.Plate(this,Color.White,null,PgrTheme.CutMedium));
        currentCard.ContentDescription="当前台词和配音状态，点击查看完整内容";currentCard.Click+=(_,_)=>CurrentCardClick();
        root.AddView(currentCard,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.WrapContent){TopMargin=Dp(4),BottomMargin=Dp(4)});
        currentPortraitFrame=new PortraitFrameDrawable(Resources!.DisplayMetrics!.Density){ShowSerial=true};
        currentPortrait=new View(this){Background=currentPortraitFrame,ImportantForAccessibility=ImportantForAccessibility.No};
        currentCard.AddView(currentPortrait,new LinearLayout.LayoutParams(Dp(56),Dp(56)){RightMargin=Dp(12),Gravity=GravityFlags.CenterVertical});
        currentBody=new LinearLayout(this){Orientation=Orientation.Vertical};
        currentCard.AddView(currentBody,new LinearLayout.LayoutParams(0,ViewGroup.LayoutParams.WrapContent,1){Gravity=GravityFlags.CenterVertical});
        currentMeta=PgrTheme.Apply(new TextView(this){Text="当前台词"},PgrTheme.Role.Eyebrow,PgrTheme.Steel);currentMeta.SetAllCaps(false);currentMeta.LetterSpacing=.06f;currentMeta.SetMaxLines(1);currentMeta.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;currentBody.AddView(currentMeta);
        current=PgrTheme.Apply(new TextView(this),PgrTheme.Role.BodyStrong);current.SetMaxLines(2);current.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;current.SetLineSpacing(Dp(2),1);currentBody.AddView(current);
        status=PgrTheme.Apply(new TextView(this),PgrTheme.Role.Caption,PgrTheme.Muted);status.SetMaxLines(1);status.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;currentBody.AddView(status);
        positionRow=Row(root);
        confirmPositionButton=Button(positionRow,"确认当前句",ConfirmFooterPosition);PgrTheme.StyleButton(confirmPositionButton,primary:true);
        confirmPositionButton.Background=new LayerDrawable(new Drawable[]{confirmPositionButton.Background!,new TriangleRunDrawable(Resources!.DisplayMetrics!.Density)});
        tabDivider=PgrTheme.Rule(this);root.AddView(tabDivider,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,Dp(6)){TopMargin=Dp(2)});
        tabDivider.Visibility=ViewStates.Gone;
        // Bottom navigation: angled dark-slate plate with a thin diagonal texture; tabs are folder tabs on it.
        // Bottom navigation on an S-tier dark slate plate (grid, small orbit arc, Red Tide hairline, corner ticks).
        tabRow=Row(root);tabRow.Background=new DarkPanelDrawable(Resources!.DisplayMetrics!.Density,DecoTier.S){CutTopRightDp=14,TideOnTop=true};
        for(int i=0;i<tabLabels.Length;i++){int targetPage=tabPages[i];var tab=Button(tabRow,tabLabels[i],()=>ShowPage(targetPage));tab.TextSize=10;tab.ContentDescription=tabLabels[i];tabButtons.Add(tab);}
        CreateStoryTray();CreateListeningTray();
        session.Changed+=UpdateStatus;session.ContentChanged+=Render;session.CapturePermissionRequested+=RequestCapture;
        session.NavigationRequested+=OnNavigation;
        session.Listening.Changed+=OnListeningChanged;
        ApplyMainOrientation();AdjustLayout();UpdateStatus();HandleIntent(Intent);Render();
        RestoreOrStartOnboarding(savedInstanceState);
    }
    protected override void OnNewIntent(Intent? intent){base.OnNewIntent(intent);HandleIntent(intent);Render();}
    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutBoolean("return_to_game_after_capture",returnToGameAfterCapture);
        outState.PutBoolean("capture_request_pending",captureRequestPending);
        outState.PutBoolean("onboarding_active",onboardingActive);
        base.OnSaveInstanceState(outState);
    }
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {base.OnConfigurationChanged(newConfig);AdjustLayout();shell.RequestApplyInsets();Render();PgrSheet.Open?.Relayout();}
    void AdjustLayout()
    {
        bool compact=CompactLayout,wide=WideLandscape;
        // Wide landscape (≥600dp): left nav rail replaces the header row and bottom tabs; the chapter page drops the
        // current-line card and confirm button (章节页不常驻).
        headingRow.Visibility=wide?ViewStates.Gone:ViewStates.Visible;
        tabRow.Visibility=wide?ViewStates.Gone:ViewStates.Visible;
        navRail.Visibility=wide?ViewStates.Visible:ViewStates.Gone;
        bool hideCurrent=page==5||(wide&&page==0)||StoryTrayActive;
        currentCard.Visibility=hideCurrent?ViewStates.Gone:ViewStates.Visible;
        positionRow.Visibility=hideCurrent?ViewStates.Gone:ViewStates.Visible;
        title.TextSize=compact?16:PgrTheme.TextTitle;current.TextSize=compact?13:PgrTheme.TextBody;status.TextSize=compact?11:PgrTheme.TextCaption;
        titleCode.Visibility=compact?ViewStates.Gone:ViewStates.Visible;
        current.SetMaxLines(compact||page!=1?1:2);status.SetMaxLines(1);
        headingRow.SetPadding(0,0,0,Dp(compact?6:14));
        title.SetPadding(0,Dp(compact?1:4),0,Dp(compact?1:1));
        headingIcon.LayoutParameters=new LinearLayout.LayoutParams(Dp(compact?26:34),Dp(compact?26:36)){RightMargin=Dp(compact?6:10)};
        orientationButton.SetSingleLine(true);orientationButton.SetPadding(Dp(8),Dp(3),Dp(8),Dp(3));
        orientationButton.SetMinHeight(Dp(PgrTheme.Touch));orientationButton.SetMinimumHeight(Dp(PgrTheme.Touch));
        orientationButton.ContentDescription="切换主界面横竖屏，当前"+MainOrientationLabel;
        railOrientationButton.ContentDescription=orientationButton.ContentDescription;
        currentMeta.TextSize=compact?10:11;currentMeta.SetPadding(0,Dp(compact?0:2),0,Dp(compact?1:5));
        current.SetPadding(0,0,0,Dp(compact?1:4));status.SetPadding(0,0,0,Dp(compact?0:1));
        currentCard.SetPadding(Dp(16),Dp(compact?4:10),Dp(16),Dp(compact?4:10));
        if(currentCard.LayoutParameters is LinearLayout.LayoutParams cardLayout)
        {cardLayout.TopMargin=cardLayout.BottomMargin=Dp(compact?1:4);currentCard.LayoutParameters=cardLayout;}
        currentPortrait.LayoutParameters=new LinearLayout.LayoutParams(Dp(compact?36:56),Dp(compact?36:56)){RightMargin=Dp(compact?8:12),Gravity=GravityFlags.CenterVertical};
        currentMeta.Visibility=compact&&page!=2?ViewStates.Gone:ViewStates.Visible;
        headerCompact=compact;headerTitleSp=compact?16:PgrTheme.TextTitle;headingFit.Refit();
        AdjustPositionButton();tabRow.SetPadding(Dp(8),0,Dp(16),0);
        for(int i=0;i<tabButtons.Count;i++)StyleTab(i);
        for(int i=0;i<railButtons.Count;i++)StyleRail(i);
        UpdateStoryTrayVisibility();
    }
    string MainOrientationLabel=>session.Settings.MainOrientation switch {"portrait"=>"竖屏","landscape"=>"横屏",_=>"跟随手机"};
    void ChooseMainOrientation()=>Choose("主界面方向（不改变游戏）",new[]{"竖屏","横屏","跟随手机"},index=>
    {
        session.Settings.MainOrientation=index switch {0=>"portrait",1=>"landscape",_=>"auto"};
        session.SaveSettings();ApplyMainOrientation();AdjustLayout();
    });
    void ApplyMainOrientation()=>RequestedOrientation=session.Settings.MainOrientation switch
    {"portrait"=>ScreenOrientation.Portrait,"landscape"=>ScreenOrientation.Landscape,_=>ScreenOrientation.Unspecified};
    void AdjustPositionButton()
    {
        bool compact=CompactLayout;
        var button=confirmPositionButton;
        button.TextSize=compact?12:14;button.SetSingleLine(true);
        button.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;
        button.SetPadding(Dp(5),Dp(compact?4:11),Dp(5),Dp(compact?4:11));
        button.SetMinHeight(Dp(48));button.SetMinimumHeight(Dp(48));
        if(button.LayoutParameters is LinearLayout.LayoutParams layout)
        {layout.TopMargin=Dp(compact?1:3);layout.BottomMargin=Dp(compact?1:5);button.LayoutParameters=layout;}
    }
    void HandleIntent(Intent? intent)
    {
        string? target=intent?.GetStringExtra("page");
        if(target=="Branch"){page=1;Window?.DecorView?.Post(ShowBranches);}
        else if(target=="Chapters")page=0;
        else if(target=="History")page=3;
        else if(target=="Settings")page=4;
        else if(target=="BranchSettings")page=6;
        else if(target=="Locate")page=2;
        else if(target=="Listening"){session.Listening.OpenLast();page=5;}
        else if(target=="Feedback")
        {
            page=1;intent?.RemoveExtra("page");
            var snapshot=session.TakePendingLineFeedback();
            if(snapshot!=null)Window?.DecorView?.Post(()=>Safe(()=>ShowLineFeedbackCategories(snapshot)));
        }
        else if(target=="Capture")
        {
            returnToGameAfterCapture=true;
            intent?.RemoveExtra("page");
            Window?.DecorView?.Post(RequestCapture);
        }
    }
    protected override void OnResume(){base.OnResume();if(session!=null){session.SetUiVisible(true);UpdateStatus();ResumeCaptureStatusUi();ResumeListeningUi();ResumeOnboarding();}}
    protected override void OnPause(){PauseOnboarding();PauseListeningUi();PauseCaptureStatusUi();session?.SetUiVisible(false);session?.SaveProgress();base.OnPause();}
    protected override void OnDestroy()
    {
        librarySearchCancellation?.Cancel();
        PgrSheet.Open?.Dismiss();
        DestroyOnboarding();
        importCancellation?.Cancel();importDialog?.Dismiss();importDialog=null;
        alive=false;session.Changed-=UpdateStatus;session.ContentChanged-=Render;session.CapturePermissionRequested-=RequestCapture;session.NavigationRequested-=OnNavigation;
        subtitleRegionOverlay?.Dispose();subtitleRegionOverlay=null;
        session.Listening.Changed-=OnListeningChanged;
        PauseCaptureStatusUi();
        PauseListeningUi();
        base.OnDestroy();
    }
    void OnNavigation(OverlayCommand command){if(command==OverlayCommand.Branch)page=1;else if(command==OverlayCommand.History)page=3;else if(command==OverlayCommand.Chapters)page=0;}
    int Dp(int n)=>(int)(n*Resources!.DisplayMetrics!.Density+.5f);
    bool CompactLayout=>Resources!.Configuration!.Orientation==global::Android.Content.Res.Orientation.Landscape;
    /// <summary>SPEC landscape breakpoint: Landscape and screen width ≥ 600dp → nav rail, right-side panels, split pages.</summary>
    /// <summary>01 split (hero left / packs right) only when the right column gets ≥ 240dp; narrower wide-landscape phones (~640dp) use one scrolling column.</summary>
    bool ChapterSplit=>WideLandscape&&Resources!.Configuration!.ScreenWidthDp-80-386>=240;
    bool WideLandscape=>CompactLayout&&Resources!.Configuration!.ScreenWidthDp>=600;
    TextView Text(string text,int size=16)
    {
        var v=new TextView(this){Text=text,TextSize=size};v.SetTextColor(PgrTheme.Foreground);
        v.SetTypeface(size>=22?PgrTheme.SansBold:PgrTheme.Sans,size>=22?TypefaceStyle.Bold:TypefaceStyle.Normal);
        v.SetPadding(0,Dp(CompactLayout?3:6),0,Dp(CompactLayout?3:6));v.SetLineSpacing(Dp(size>=15?4:3),1);return v;
    }
    ImageView Icon(string name,Color color,int size=20)
    {var icon=new ImageView(this);icon.SetImageDrawable(new PgrIconDrawable(name,color,Resources!.DisplayMetrics!.Density,size));icon.ImportantForAccessibility=ImportantForAccessibility.No;return icon;}
    void ButtonIcon(Button button,string name,bool primary=false)
    {
        // 主按钮样式由 Button(...,primary:true) 在加入布局前设置；这里只配图标（禁用时灰色）。
        button.SetCompoundDrawablesWithIntrinsicBounds(PgrTheme.StateIcon(name,primary?PgrTheme.OnAccent:PgrTheme.IconColor(name),Resources!.DisplayMetrics!.Density,18),null,null,null);button.CompoundDrawablePadding=Dp(8); // 0.4.7 P2: gray icon when disabled
    }
    void StyleTab(int index)
    {
        var button=tabButtons[index];bool selected=tabPages[index]==page||(page==5&&index==0);
        PgrTheme.StyleNavTab(button,selected,CompactLayout,tabLabels[index],tabEnglish[index],tabDots[index]);
        button.ContentDescription=tabLabels[index]+(tabDots[index]==null?"":index==3?"，有待确认分支":"，跟随中");
        var icon=new PgrIconDrawable(tabIcons[index],selected?PgrTheme.Red:PgrTheme.NavText,Resources!.DisplayMetrics!.Density,CompactLayout?18:20);
        button.SetCompoundDrawablesWithIntrinsicBounds(CompactLayout?icon:null,CompactLayout?null:icon,null,null);
        button.CompoundDrawablePadding=Dp(CompactLayout?4:2);button.SetPadding(Dp(CompactLayout?6:0),Dp(CompactLayout?3:5),Dp(CompactLayout?6:0),Dp(CompactLayout?3:4));
        button.SetMinHeight(Dp(CompactLayout?48:60));button.SetMinimumHeight(Dp(CompactLayout?48:60));button.TextSize=CompactLayout?11:PgrTheme.TextLabel;
        if(button.LayoutParameters is LinearLayout.LayoutParams layout)
        {layout.TopMargin=Dp(CompactLayout?4:6);layout.BottomMargin=0;layout.LeftMargin=layout.RightMargin=Dp(1);button.LayoutParameters=layout;}
    }
    void UpdateTabDots()
    {
        Color? follow=session.AutoPlaybackRunning||session.ClickFollowRunning||session.BranchFollowRunning?PgrTheme.Cyan:null;
        Color? branch=session.ConfirmedBranchWaiting||session.SourceChoiceWaiting?PgrTheme.Brass:null;
        if(tabDots[1]==follow&&tabDots[3]==branch)return;
        tabDots[1]=follow;tabDots[3]=branch;
        if(tabButtons.Count==tabDots.Length){StyleTab(1);StyleTab(3);}
        if(railButtons.Count==tabDots.Length){StyleRail(1);StyleRail(3);}
    }
    // 「第43章 远信回响」→ 43；「01 涂鸦艺术」（新版主线包标题）→ 01；无编号 → 「—」（只用于文字，大号描边编号见 CoverNumber）。
    static string ChapterNumber(string title)
    {
        var match=System.Text.RegularExpressions.Regex.Match(title,@"第\s*(\d+)\s*章");
        if(!match.Success)match=System.Text.RegularExpressions.Regex.Match(title,@"^\s*(\d{1,3})\s*[·\-_.:：\s]+\S");
        return match.Success?match.Groups[1].Value.PadLeft(2,'0'):"—";
    }
    /// <summary>Big outlined cover number (same rule as the desktop ChapterCover.Split): 第N章 / leading number, else a short
    /// code such as EX02; "" when the title has none, so the cover shows no outline instead of an outlined 「—」 (0.4.7 N).</summary>
    static string CoverNumber(string title)
    {
        string number=ChapterNumber(title); if(number!="—")return number;
        var match=System.Text.RegularExpressions.Regex.Match(title,@"^\s*([A-Za-z]{1,3}-?\d+(?:-\d+)?)\s*[·\-_.:：\s]+\S");
        return match.Success?match.Groups[1].Value:"";
    }
    static string PackTitle(string title)
    {
        return ChapterCatalog.Title(title);
    }
    static string ChapterTitle(string title)
    {return System.Text.RegularExpressions.Regex.Replace(PackTitle(title),@"^第\s*\d+\s*章[\s_·]*","").Trim();}
    /// <summary>Text line routed through the type scale: ≤11 eyebrow, 12–15 note, 16–19 section, ≥24 page title.</summary>
    void Line(string text,int size=16)
    {
        if(size<=11){var eyebrow=PgrTheme.Eyebrow(this,text);eyebrow.SetAllCaps(false);eyebrow.LetterSpacing=.1f;eyebrow.SetPadding(0,Dp(CompactLayout?4:10),0,Dp(CompactLayout?1:2));content.AddView(eyebrow);return;}
        if(size>=17&&size<24){Section(text,null);return;}
        var label=Text(text,CompactLayout&&size>=24?20:size);
        if(size>=24){PgrTheme.Apply(label,PgrTheme.Role.Display);label.TextSize=CompactLayout?20:PgrTheme.TextDisplay;label.SetPadding(0,Dp(CompactLayout?2:4),0,Dp(CompactLayout?4:8));}
        else if(size<=14)label.SetTextColor(PgrTheme.Secondary);
        content.AddView(label);
    }
    /// <summary>Chinese section title with an English sublabel over a HUD rule.</summary>
    void Section(string title,string? english)=>content.AddView(PgrTheme.SectionHeader(this,title,english,CompactLayout),new LinearLayout.LayoutParams(-1,-2));
    /// <summary>Page header: Chinese/English code line, then the Chinese page title.</summary>
    void Header(string code,string title)
    {
        var header=PgrTheme.PageHeader(this,code,title,compact:CompactLayout);
        if(header.GetChildAt(0) is TextView eyebrow){eyebrow.SetAllCaps(false);eyebrow.LetterSpacing=.1f;}
        content.AddView(header,new LinearLayout.LayoutParams(-1,-2));
    }
    FitRow headingFit=null!;bool headerCompact;float headerTitleSp=PgrTheme.TextTitle;
    void ApplyHeaderLevel(int level)
    {
        offlineTag.Text=level>=1?"离线配音":"离线配音 · OFFLINE";
        offlineTag.Visibility=headerCompact||level>=3?ViewStates.Gone:ViewStates.Visible;
        bool iconOnly=level>=2;
        orientationButton.Text=iconOnly?"":"横 / 竖屏";orientationButton.CompoundDrawablePadding=iconOnly?0:Dp(5);
        orientationButton.SetMinWidth(Dp(PgrTheme.Touch));orientationButton.SetMinimumWidth(Dp(PgrTheme.Touch));
        title.TextSize=level>=4?headerTitleSp*.88f:headerTitleSp;
    }
    LinearLayout Row(LinearLayout parent)
    {var row=new LinearLayout(this){Orientation=Orientation.Horizontal};parent.AddView(row);return row;}
    Button Button(LinearLayout parent,string label,Action click,bool primary=false)
    {
        // 0.4.7 P2：主按钮在加入布局前一次定样式。0.4.6 先按普通键加入、再由 ButtonIcon 改成主按钮，真机上底板停在“禁用”层
        // （浅色底 + 白字，10 页“回到游戏配置 / 开始”），按钮本身其实可用。
        var b=new Button(this){Text=label,TextSize=14};
        if(primary)PgrTheme.StyleButton(b,primary:true);else{PgrTheme.StyleButton(b,alignStart:parent.Orientation!=Orientation.Horizontal);DecoButton(b,parent);}
        b.Click+=(_,_)=>Safe(click);
        var layout=parent.Orientation==Orientation.Horizontal?new LinearLayout.LayoutParams(0,ViewGroup.LayoutParams.WrapContent,1):new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(parent.Orientation==Orientation.Horizontal?Dp(3):0,Dp(3),parent.Orientation==Orientation.Horizontal?Dp(3):0,Dp(5));
        parent.AddView(b,layout);return b;
    }
    LinearLayout Card(LinearLayout parent,bool selected=false,FxKind kind=FxKind.Card,string? code=null)
    {
        var card=new LinearLayout(this){Orientation=Orientation.Vertical};card.SetPadding(Dp(16),Dp(10),Dp(16),Dp(12));
        // 0.4.3 F-B: M tier (brackets + faint F1b corner + brass ruler); buttons inside stay plain.
        card.Background=FusionDeco.Over(PgrTheme.Card(this,selected),new FusionDeco(Resources!.DisplayMetrics!.Density,DecoTier.M,kind){Code=code,CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium,InsetDp=selected?0:3});
        card.Tag="fx";
        parent.AddView(card,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.WrapContent){TopMargin=Dp(8),BottomMargin=Dp(7)});return card;
    }
    void StyleDialog(AlertDialog? dialog)=>PgrTheme.StyleDialog(dialog);
    void Safe(Action action){try{if(session.IsDeletingPackage){Info("请稍候","正在删除章节文件，请等待完成。");return;}action();}catch(Exception ex){Info("操作未完成",ex.Message);}}
    // Info / Confirm / Prompt / Choose → MainActivity.Dialogs.cs (18-B).
    void ShowPage(int index){if(index==5)session.Listening.OpenLast();page=index;linePage=0;Render();}
    void UpdateStatus()
    {
        if(!alive)return;
        RunOnUiThread(()=>
        {
            if(!alive)return;
            if(page==2&&!ReferenceEquals(renderedLocateCandidates,session.Candidates)){Render();return;}
            var e=session.Engine;string mode=session.AutoPlaybackRunning?"自动播放中":session.ClickFollowRunning?"点按跟随中":"手动播放";
            string meta=e?.Current is { } n?$"{mode}  /  {n.Speaker}":mode+"  /  当前台词";
            if(page==2)meta="播放器当前位置"+(e?.Current is { } located?" · "+located.Speaker:"");
            if(currentMeta.Text!=meta)currentMeta.Text=meta;
            UpdatePortrait(currentPortrait,currentPortraitFrame,e?.Current?.Speaker,ref currentPortraitSpeaker);UpdateCurrentDeco();
            string confirmLabel=page==2&&session.Candidates.Count>0?"采用识别结果":"确认当前句";
            if(confirmPositionButton.Text!=confirmLabel)confirmPositionButton.Text=confirmLabel;
            string text=e?.Current==null?"尚未选句":session.CurrentGameLineText;if(current.Text!=text)current.Text=text;
            string statusText=session.SettingsSaveWarning??session.StatusLine; // 现稿续接段加短标签
            if(status.Text!=statusText)status.Text=statusText;
            currentMeta.SetTextColor(session.AutoPlaybackRunning||session.ClickFollowRunning?PgrTheme.RedText:PgrTheme.Steel);
            UpdateTabDots();
            if(session.Listening.IsPlaying)
            {
                currentMeta.Text="正在听书 · 点击返回播放器";currentMeta.SetTextColor(PgrTheme.Steel);
                current.Text=session.Listening.Current?.Text??session.Listening.Status;
                status.Text=session.SettingsSaveWarning??session.Listening.PositionText;
            }
            if(autoPlaybackState!=null)
            {
                autoPlaybackState.Text=session.AutoPlaybackRunning?"已开启 · 按当前路线推进":"未开启 · 请从游戏内悬浮控制开始";
                autoPlaybackState.SetTextColor(session.AutoPlaybackRunning?PgrTheme.RedText:PgrTheme.Secondary);
            }
            if(autoPlaybackStopButton!=null)autoPlaybackStopButton.Enabled=session.AutoPlaybackRunning;
            if(autoPlaybackPermissionButton!=null)autoPlaybackPermissionButton.Text=GameAdvanceAccessibilityService.IsConnected?"自动点击权限 · 已连接":"开启自动点击权限";
            UpdateStoryTray();
            UpdateCaptureStatusButton();QueueCaptureStatusTick();
        });
    }
    void UpdateCaptureStatusButton()
    {
        if(captureStatusButton==null)return;
        string label=session.Screen.CaptureActive?"停止屏幕捕获":"授权屏幕捕获";
        if(captureStatusButton.Text!=label)captureStatusButton.Text=label;
    }
    void ResumeCaptureStatusUi(){captureStatusUiVisible=true;UpdateCaptureStatusButton();QueueCaptureStatusTick();}
    void PauseCaptureStatusUi(){captureStatusUiVisible=false;CancelCaptureStatusTick();}
    void CancelCaptureStatusTick()
    {
        if(captureStatusTick!=null)pageScroll?.RemoveCallbacks(captureStatusTick);
        captureStatusTickScheduled=false;
    }
    void QueueCaptureStatusTick()
    {
        if(!alive||!captureStatusUiVisible||page!=4||captureStatusButton==null||captureStatusTickScheduled)return;
        // Capture start/stop completes asynchronously; only observe state while this page is visible.
        captureStatusTick??=()=>{captureStatusTickScheduled=false;if(!alive||!captureStatusUiVisible||page!=4)return;UpdateCaptureStatusButton();QueueCaptureStatusTick();};
        captureStatusTickScheduled=true;pageScroll.PostDelayed(captureStatusTick,500);
    }
    void Render()
    {
        if(!alive)return;
        RunOnUiThread(()=>
        {
            if(!alive)return;CancelCaptureStatusTick();captureStatusButton=null;autoPlaybackState=null;autoPlaybackStopButton=null;autoPlaybackPermissionButton=null;ClearListeningViews();
            stickySide=null;content=mainContent;content.RemoveAllViews();splitLeftBar.RemoveAllViews();splitLeft.RemoveAllViews();splitRight.RemoveAllViews();
            bool split=page==0&&ChapterSplit;
            pageScroll.Visibility=split?ViewStates.Gone:ViewStates.Visible;splitHost.Visibility=split?ViewStates.Visible:ViewStates.Gone;
            chapterCategoryBar.Visibility=page==0&&!split?ViewStates.Visible:ViewStates.Gone;
            bool pageChanged=styledTab!=page;
            if(pageChanged)
            {
                styledTab=page;
                AdjustLayout();
            }
            switch(page){case 0:Chapters();break;case 1:Story();break;case 2:Locate();break;case 3:History();break;case 5:ListeningPage();break;case 6:BranchSettingsPage();break;default:SettingsPage();break;}
            UpdateStoryTrayVisibility();
            if(pageChanged)pageScroll.Post(()=>{if(alive){pageScroll.ScrollTo(0,0);splitLeftScroll.ScrollTo(0,0);splitRightScroll.ScrollTo(0,0);}});
            UpdateStatus();
        });
    }
    bool NeedPack(){if(session.Engine!=null)return false;Line("请先在“章节”中导入并打开配音包。");return true;}
    void SectionSelector()
    {
        var e=session.Engine!;var sections=e.Pack.Chapters.SelectMany(c=>c.Sections).ToList();
        var selected=sections.FirstOrDefault(s=>s.Id==session.SectionId)??SectionDisplay.FirstListed(sections)!;
        var sectionButton=Button(content,"小节："+SectionDisplay.Title(sections,selected.Id),()=>ChooseDisplayedSection(e.Pack,sections,"选择游戏当前小节",
            ()=>ReferenceEquals(e,session.Engine),section=>
        {session.Command(x=>x.PauseForBrowse());session.SectionId=section.Id;linePage=0;Render();},session.SectionId));
        Dropdown(sectionButton,"story");
    }
    /// <summary>Selector button: leading icon, trailing chevron, single line.</summary>
    void Dropdown(Button button,string icon)
    {
        var density=Resources!.DisplayMetrics!.Density;
        button.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable(icon,PgrTheme.Slate,density,18),null,new PgrIconDrawable("chevron_down",PgrTheme.Muted,density,16),null);
        button.CompoundDrawablePadding=Dp(10);button.SetSingleLine(true);button.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;
    }
    /// <summary>05-B story page: cover band + cast, 小节 selector, tools, script-rail line list, F1b footer. Playback keys live in the tray.</summary>
    void Story()
    {
        if(NeedPack())return;
        var e=session.Engine!;
        StoryCoverBand(e);
        SectionSelector();
        var reference=Button(content,"内置剧情文本 · 当前小节",ShowStoryReference);var referenceFace=PgrUi.Style(reference,Xb.Normal,"archive","REFERENCE");
        {var secs=e.Pack.Chapters.SelectMany(c=>c.Sections).ToList();var (secNo,_,_)=SectionParts(SectionDisplay.Title(secs,session.SectionId));
         referenceFace.Deco=new FusionDeco(Resources!.DisplayMetrics!.Density,DecoTier.M,FxKind.Ref){Code=secNo.Length>0?"REF · "+secNo:null,CutTR=8,CutBL=8};}
        // 0.4.6: tight width / large font: drop icon, then REFERENCE, then trim — never wrap to "当前小/节". Normal phones fit as before.
        reference.SetMinWidth(0);reference.SetMinimumWidth(0);FitSingleLine(reference,12f,8f);
        var action=Row(content);
        var save=Button(action,"保存书签",AddBookmark);PgrUi.Style(save,Xb.Normal,"bookmark",textSp:13);
        var undo=Button(action,"撤销纠偏",()=>session.Command(x=>{if(!x.UndoCorrection())Info("撤销纠偏",x.NavigationError);}));PgrUi.Style(undo,Xb.Normal,"replay",textSp:13);
        var report=Button(action,"反馈这句",FeedbackCurrentGameLine);PgrUi.Style(report,Xb.Normal,"feedback",textSp:13);
        foreach(var fit in new[]{save,undo,report}){fit.SetSingleLine(true);fit.SetMinWidth(0);fit.SetMinimumWidth(0);DropIconWhenTight(fit);}
        if(e.Notice.Length>0)Line(e.Notice,13);
        if(e.Mode==RunMode.Original)Line("原声时段：配音停止、位置冻结。点选当前游戏台词，确认续接后恢复。",14);
        var (number,_,_)=SectionParts(SectionDisplay.Title(e.Pack.Chapters.SelectMany(c=>c.Sections).ToList(),session.SectionId));
        content.AddView(PgrUi.Zone(this,"台词","LINES",number.Length>0?number:null,16),new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(10)});
        RenderLines(false);
        content.AddView(new View(this){Background=new ArchiveFooterDrawable(Resources!.DisplayMetrics!.Density),ImportantForAccessibility=ImportantForAccessibility.No},new LinearLayout.LayoutParams(-1,Dp(150)){TopMargin=Dp(8)});
    }
    string? renderedGapDirectoryKey;
    void RenderLines(bool searching)
    {
        var engine=session.Engine!;
        var lines=engine.Pack.Nodes.Where(n=>!n.Archived&&n.Kind=="line"&&n.SectionId==session.SectionId&&
            (!searching||string.IsNullOrWhiteSpace(search)||n.Text.Contains(search,StringComparison.OrdinalIgnoreCase)||n.Speaker.Contains(search,StringComparison.OrdinalIgnoreCase))).ToList();
        if(!searching && engine.Mode==RunMode.Gap)
        {
            string key=engine.Pack.Id+":"+session.SectionId+":"+engine.CurrentId;
            if(renderedGapDirectoryKey!=key)
            {
                var visibleIds=lines.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
                var previous=engine.History.Take(engine.HistoryPosition+1).LastOrDefault(v=>visibleIds.Contains(v.NodeId));
                int nearby=previous==null?-1:lines.FindIndex(n=>n.Id==previous.NodeId);
                if(nearby>=0)linePage=nearby/45;
                renderedGapDirectoryKey=key;
            }
            if(session.GameDeadEnd(engine))
            {
                GameDeadEndCard(content);
            }
            else Line("此处等待核对。下面仍是完整台词，可按游戏画面确认一句；列表顺序不代表已核实的下一句。",13);
        }
        else if(!searching)renderedGapDirectoryKey=null;
        const int size=45;int pages=Math.Max(1,(lines.Count+size-1)/size);linePage=Math.Clamp(linePage,0,pages-1);
        var counter=PgrTheme.Apply(Text($"台词 · {lines.Count} 句 · 第 {linePage+1}/{pages} 页",12),PgrTheme.Role.Code,PgrTheme.Secondary);PgrTheme.Tabular(counter);counter.SetPadding(0,Dp(12),0,Dp(6));content.AddView(counter);
        int lineIndex=linePage*size;string? previousSpeaker=null;
        var pageLines=lines.Skip(linePage*size).Take(size).ToList();float density=Resources!.DisplayMetrics!.Density;
        for(int i=0;i<pageLines.Count;i++)
        {
            var node=pageLines[i];
            lineIndex++;
            string notice=PendingVoice.Notice(engine.Pack,node);
            bool selected=node.Id==engine.CurrentId;
            string speakerName=string.IsNullOrWhiteSpace(node.Speaker)?"旁白":node.Speaker.Trim();
            // Script rail (05-B 台本轨): diamond node + original three-digit number; current = solid crimson node + white card + 当前.
            var row=new LinearLayout(this){Orientation=Orientation.Horizontal};
            var rail=new LinearLayout(this){Orientation=Orientation.Vertical,ImportantForAccessibility=ImportantForAccessibility.No};
            rail.Background=new RailNodeDrawable(density){IsCurrent=selected,First=i==0&&linePage==0,Last=i==pageLines.Count-1&&linePage+1>=pages,NodeTopDp=18};
            bool narrowScript=WideLandscape&&Resources!.Configuration!.ScreenWidthDp<820;int av=narrowScript?28:36;
            var marker=PgrTheme.Apply(Text(lineIndex.ToString("000"),10),PgrTheme.Role.Code,selected?PgrTheme.RedText:PgrTheme.Muted);marker.SetPadding(0,0,0,0);marker.Gravity=GravityFlags.CenterHorizontal;
            rail.AddView(marker,new LinearLayout.LayoutParams(Dp(narrowScript?24:28),-2){TopMargin=Dp(28)});
            row.AddView(rail,new LinearLayout.LayoutParams(Dp(narrowScript?26:32),-1){RightMargin=Dp(narrowScript?2:4)});
            var line=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true,Selected=selected};line.SetPadding(Dp(narrowScript?8:12),Dp(10),Dp(narrowScript?8:12),Dp(11));line.SetGravity(GravityFlags.Top);
            if(selected)line.Background=PgrTheme.Pressable(this,FusionDeco.Over(PgrTheme.Card(this,true),new FusionDeco(density,DecoTier.M,FxKind.Line){Code="LINE · "+lineIndex.ToString("000"),Ring=(Dp(narrowScript?8:12)/density+av/2f,11+av/2f,av/2f+6),Paper=false,CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium,InsetDp=0}),PgrTheme.CutMedium,Corners.Diagonal,PgrTheme.RedRipple);
            else line.Background=PgrTheme.PressableCard(this,selected);
            line.ContentDescription=AppSession.BrowseSpeaker(node)+"："+node.Text;line.Click+=(_,_)=>Safe(()=>ConfirmNode(node));
            bool showAvatar=speakerName!=previousSpeaker;previousSpeaker=speakerName;
            if(showAvatar)line.AddView(SpeakerAvatar(speakerName,av,true),new LinearLayout.LayoutParams(Dp(av),Dp(av)){RightMargin=Dp(narrowScript?6:10),TopMargin=Dp(1)});
            else line.AddView(new View(this),new LinearLayout.LayoutParams(Dp(av),Dp(1)){RightMargin=Dp(narrowScript?6:10)});
            var body=new LinearLayout(this){Orientation=Orientation.Vertical};
            var nameRow=new LinearLayout(this){Orientation=Orientation.Horizontal};nameRow.SetGravity(GravityFlags.CenterVertical);
            var speaker=Text(speakerName,12);speaker.SetTextColor(selected?PgrTheme.RedText:PgrTheme.Secondary);speaker.SetPadding(0,0,0,Dp(3));speaker.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);speaker.LetterSpacing=.04f;nameRow.AddView(speaker);
            if(selected)nameRow.AddView(PgrUi.Chip(this,"当前",PgrUi.Status.Current),new LinearLayout.LayoutParams(-2,-2){LeftMargin=Dp(8),BottomMargin=Dp(2)});
            body.AddView(nameRow);
            var dialogue=Text(node.Text,15);dialogue.SetPadding(0,0,0,0);dialogue.SetLineSpacing(Dp(4),1);body.AddView(dialogue);
            if(notice.Length>0){var warning=PgrTheme.Apply(Text(notice,11),PgrTheme.Role.Caption,PgrTheme.Muted);warning.TextSize=11;body.AddView(warning);}
            line.AddView(body,new LinearLayout.LayoutParams(0,-2,1));
            row.AddView(line,new LinearLayout.LayoutParams(0,-2,1){BottomMargin=Dp(6)});
            content.AddView(row,new LinearLayout.LayoutParams(-1,-2));
        }
        var pagerRow=Row(content);
        if(linePage>0)PgrUi.Style(Button(pagerRow,"上一页",()=>{linePage--;Render();}),Xb.Normal,"chevron_up","PREV");
        if(linePage+1<pages)PgrUi.Style(Button(pagerRow,"下一页",()=>{linePage++;Render();}),Xb.Normal,"chevron_down","NEXT");
    }
    void ConfirmFooterPosition()
    {
        if(page!=2||session.Candidates.Count==0){session.ConfirmCurrent();return;}
        var candidates=session.Candidates.ToArray();var engine=session.Engine;
        if(candidates.Length==1){ConfirmOcrCandidate(candidates[0],engine);return;}
        Choose("选择与游戏一致的识别候选",candidates.Select(c=>c+"\n"+c.Context).ToArray(),i=>ConfirmOcrCandidate(candidates[i],engine));
    }
    void ConfirmOcrCandidate(MatchCandidate candidate,PlaybackEngine? expectedEngine)
    {
        bool StillValid()
        {
            if(ReferenceEquals(session.Engine,expectedEngine)&&expectedEngine!=null&&
                session.Candidates.Any(c=>ReferenceEquals(c,candidate))&&
                expectedEngine.Pack.ById.TryGetValue(candidate.Node.Id,out var node)&&ReferenceEquals(node,candidate.Node))return true;
            Info("识别结果已失效","当前章节、模式或识别结果已改变。请重新识别，或搜索选择游戏中的台词。");Render();return false;
        }
        ConfirmNode(candidate.Node,StillValid);
    }
    void ConfirmNode(Node node,Func<bool>? canApply=null)
    {
        if(canApply!=null&&!canApply())return;
        if(node.Kind=="choice")
        {
            Confirm("定位游戏中的分支菜单",node.Text+"\n\n"+session.Engine!.LocationContext(node),()=>
            {if(canApply==null||canApply())session.Command(e=>{if(!e.OpenGameMenu(node.Id,node.SectionId))throw new InvalidOperationException(e.NavigationError);});});return;
        }
        bool original=session.Engine?.Mode==RunMode.Original;
        Confirm(original?"结束原声并从这里续接？":"确认游戏当前台词",AppSession.BrowseSpeaker(node)+"："+node.Text+"\n\n"+session.Engine!.LocationContext(node)+"\n\n请确保与游戏当前画面一致。",()=>
        {if(canApply==null||canApply())session.ConfirmLine(node.Id,original);});
    }
    void ShowBranches()
    {
        if(session.Engine is not { } e)return;
        if(e.Mode==RunMode.Original){Info("原声时段","先在剧情或定位页选择续接台词。当前不会改变路线。");return;}
        if(e.Mode==RunMode.Choice)
        {
            // 0.4.7 同回应隐藏：与刚选项回应相同的选项（如 19-14 答错回来）游戏里已不显示，这里同样不列出。
            var hiddenIds=DefaultBranchPolicy.HiddenOptions(e).Select(o=>o.Id).ToHashSet();
            var options=e.AvailableOptions.Where(o=>!hiddenIds.Contains(o.Id)).ToList();
            var labels=options.Select(o=>o.ToString()).Concat(new[]{"返回上级菜单","人物、话题与分支目录"}).ToArray();
            Choose("当前分支与导航",labels,i=>
            {
                if(i==options.Count)ReturnToParentMenu();
                else if(i==options.Count+1)ShowBranchDirectory();
                else Confirm("确认分支",options[i].Label+"\n"+options[i].Reason,()=>session.Command(x=>{ExplorationMemory.RecordCurrent(x,options[i]);x.SelectBranch(x.AvailableOptions.FindIndex(o=>o.Id==options[i].Id));},true));
            });return;
        }
        if(e.Mode==RunMode.Gap&&e.ResumeMenus.Count>0)
        {
            var menus=e.ResumeMenus;
            var labels=menus.Select(m=>m.Text).Concat(new[]{"返回上级菜单","人物、话题与分支目录"}).ToArray();
            Choose("续接菜单与导航",labels,i=>
            {
                if(i==menus.Count)ReturnToParentMenu();
                else if(i==menus.Count+1)ShowBranchDirectory();
                else session.Command(x=>x.SelectContinuation(i));
            });return;
        }
        // 现稿续接：出口未逐句核实而包内写有下一处（通常已自动续接；此处供自动续接未触发时手动使用）。
        if((e.Mode==RunMode.Gap||e.Mode==RunMode.Following)&&session.DeclaredContinuation is { } declared)
        {
            var target=declared.Target;
            string label=target.Kind=="line"?(string.IsNullOrWhiteSpace(target.Speaker)?"旁白":target.Speaker)+"："+target.Text:target.Text;
            Choose("续接菜单与导航",new[]{UiText.DraftPrefix+label,"返回上级菜单","人物、话题与分支目录"},i=>
            {
                if(i==1)ReturnToParentMenu();
                else if(i==2)ShowBranchDirectory();
                else Confirm("确认游戏当前位置",label+"\n\n"+e.LocationContext(target)+"\n\n"+UiText.DraftConfirmNote,session.FollowDeclaredContinuation);
            });return;
        }
        ShowBranchDirectory();
    }
    void ReturnToParentMenu() => session.Command(x=>{if(!x.ReturnToStoryMenu())Info("返回菜单",x.NavigationError);});
    void ShowBranchDirectory()
    {
        if(session.Engine is not { } e)return;
        if(e.Mode==RunMode.Original){Info("原声时段","先在剧情或定位页选择续接台词。当前不会改变路线。");return;}
        var targets=e.GetStoryMenus(session.SectionId);
        var labels=new List<string>{"返回上级菜单","重选最近一次分支"};labels.AddRange(targets.Select(t=>t.Label+" · "+t.Status));
        Choose("人物、话题与分支",labels.ToArray(),i=>
        {
            if(i==0)ReturnToParentMenu();
            else if(i==1)session.Command(x=>{if(!x.ReselectLastChoice())Info("重选分支",x.NavigationError);});
            else Confirm("定位游戏当前菜单",targets[i-2].Label+"\n"+targets[i-2].Preview,()=>session.Command(x=>{if(!x.OpenGameMenu(targets[i-2].MenuId,session.SectionId))Info("分支定位",x.NavigationError);}));
        });
    }
    void OcrScopeSelector()
    {
        var scopeButton=Button(content,"定位范围："+(session.Settings.OcrFullScreen?"全画面":"框选区域"),()=>
            Choose("选择定位范围",new[]{"全画面：无需框选","框选区域：仅识别保存的字幕区域"},i=>
            {session.SetOcrFullScreen(i==0);Render();}));
        Dropdown(scopeButton,"locate");
        Line(session.Settings.OcrFullScreen
            ?"全画面无需框选，识别结果仍需核对后采用。编辑区域只会保存框选范围，不会切换当前设置。"
            :"仅识别框选区域，请将角色名和完整对白框入。识别结果仍需核对后采用。",13);
    }
    void AddBookmark()
    {if(session.Engine?.Current==null){Info("保存书签","请先选择当前台词。");return;}Prompt("书签名称",AppSession.BrowseSpeaker(session.Engine.Current), label=>{var e=session.Engine!;session.Progress.SaveBookmark(e.Pack.Id,e.CreateBookmark(label));Render();Toast.MakeText(this,"书签已保存，可在“历史”页查看",ToastLength.Short)?.Show();});}
    void SettingsPage()
    {
        CoverHeader("控制台 · SETTINGS","播放与识别",null,new CoverGlyphDrawable(Resources!.DisplayMetrics!.Density,"dial"));
        int tab=Math.Clamp(session.Settings.SettingsTab,0,3);
        void PickTab(int i){session.Settings.SettingsTab=i;session.SaveSettings();Render();}
        var tabs=SegmentTabs(new[]{"悬浮 · 跟随","识别 · 声音","立绘 · 封面","存档 · 诊断"},new[]{"OVERLAY","OCR & AUDIO","PORTRAITS","DATA"},null,tab,PickTab,WideLandscape&&PageWidthDp>=560);
        if(WideLandscape&&PageWidthDp>=560)
        {
            var left=SideColumns(Resources!.Configuration!.ScreenHeightDp>=600?184:150,10);left.AddView(tabs); // 0.4.6: tablet/fold inner landscape: 184dp so 「重新打开首次使用引导」 stays on one line (phones keep 0.4.3 150dp)
            var guide=Button(left,"重新打开首次使用引导",StartOnboarding);ButtonIcon(guide,"replay");guide.TextSize=12;
            var hint=Text("无需绑定客户端或选择服务器。回到你正在玩的战双，使用悬浮控制即可。",11);hint.SetTextColor(PgrTheme.Secondary);left.AddView(hint);
        }
        else
        {
            ButtonIcon(Button(content,"重新打开首次使用引导",StartOnboarding),"replay");
            Line("无需绑定客户端或选择服务器。回到你正在玩的战双，使用悬浮控制即可。",13);
            content.AddView(tabs,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(4)});
        }
        if(tab==0){
        // 0.4.3 F-B (10 设置): overlay buttons + switch rows grouped in one M card 「OVL · 02」, rows split by hairlines.
        Section("悬浮控制","OVERLAY");var ctrlCard=Card(content,kind:FxKind.Ctrl,code:"OVL · 02");ctrlCard.SetPadding(Dp(12),Dp(10),Dp(12),Dp(16));var overlayControls=Row(ctrlCard);
        var overlayOn=Button(overlayControls,"开启悬浮控制",ShowOverlay);ButtonIcon(overlayOn,"play");
        var overlayOff=Button(overlayControls,"关闭悬浮控制",session.HideOverlay);ButtonIcon(overlayOff,"close");
        // 0.4.6: half-width keys at 360dp × large font drop the icon / trim instead of wrapping to "开启悬浮控/制".
        foreach(var ok in new[]{overlayOn,overlayOff}){var ic=ok.GetCompoundDrawables();ok.SetCompoundDrawablesRelativeWithIntrinsicBounds(ic[0],ic[1],ic[2],ic[3]);ok.SetMinWidth(0);ok.SetMinimumWidth(0);FitSingleLine(ok,12f,6f);} // ButtonIcon sets a left (non-relative) icon that FitSingleLine could not see → icon never dropped and 「开启悬浮控制」 was clipped to 「开启悬浮控」 (360dp × 1.3); move it to start first
        var compactButtons=new Switch(this){Text="收起时显示小按钮",Checked=session.Settings.ShowCompactButtons,TextSize=14};
        PgrTheme.StyleSwitch(compactButtons);
        compactButtons.CheckedChange+=(_,e)=>session.SetCompactButtonsVisible(e.IsChecked);
        ctrlCard.AddView(compactButtons,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.WrapContent){TopMargin=Dp(4)});CardSwitchRow(compactButtons);
        ctrlCard.AddView(new View(this){Background=new global::Android.Graphics.Drawables.ColorDrawable(PgrTheme.A(PgrArt.PaperRule,204))},new LinearLayout.LayoutParams(-1,Math.Max(1,Dp(1))));
        var compactControls=new Switch(this){Text="显示台词悬浮条（关闭后只留球）",Checked=session.Settings.ShowCompactControls,TextSize=14};
        PgrTheme.StyleSwitch(compactControls);
        compactControls.CheckedChange+=(_,e)=>session.SetCompactControlsVisible(e.IsChecked);
        ctrlCard.AddView(compactControls,new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.WrapContent));CardSwitchRow(compactControls);
        Section("点按跟随","TAP FOLLOW");
        FoldNote(content,"点按跟随说明","HOW IT WORKS",new[]{"普通台词不依赖 OCR。先把配音对齐游戏当前句，再到游戏里设置“下一句区域”，开启“点按跟随”。每次你轻点框内，游戏和配音各前进一步。","等游戏文字完整后再轻点一次，等下一句出现，不要快速连点。补齐文字或转发时连续点击可能错位，可用上一句／下一句小按钮校正。人物互动和未确认的路线仍需手动选择。"});
        var tapCard=Card(content,kind:FxKind.Tap,code:"TAP · 03");tapCard.SetPadding(Dp(16),Dp(10),Dp(16),Dp(18));
        var branchSettings=Button(tapCard,"分支线设置 · "+(session.Settings.AutoPlayConfirmBranch?"选择后确认对白":session.Settings.AutoPlayDefaultBranchEnabled?"自动播放默认分支"+(session.Settings.DefaultBranchOption==2?"二":"一"):session.Settings.BranchAutoFollow?"分支配音跟随":"分支期间暂停配音"),()=>ShowPage(6));
        ButtonIcon(branchSettings,"branch");
        var branchHint=Text("分支、人物互动和回访话题的处理方式，统一在下方“分支”页设置。",13);branchHint.SetTextColor(PgrTheme.Secondary);tapCard.AddView(branchHint);
        ButtonIcon(Button(tapCard,"点按跟随的辅助点击权限",ExplainAutoPlaybackAccessibility),"settings");
        ButtonIcon(Button(tapCard,"回到游戏设置点按跟随",()=>ReturnToGameForAutoPlayback(true)),"open");
        Section("自动播放","AUTO PLAY");
        var autoCard=Card(content);
        autoPlaybackState=Text("未开启 · 请从游戏内悬浮控制开始",14);autoPlaybackState.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);autoCard.AddView(autoPlaybackState);
        var autoDescription=Text("先核对当前句，点“从这句开始”直接重播，不重复 OCR。需要纠偏时选“重新 OCR 定位”，人工采用候选后再开启。之后按已确认的路线顺序，每句自然播完才点击一次游戏下一句。",13);
        
        var autoBoundary=Text("开启“确认选后的对白，再继续播放”时，遇到选项才暂停；先在游戏选择，再点原选项与选后对白卡片，直接继续自动播放，汇合时不再确认。关闭该模式、且未开启默认分支时，到分支会暂停并在顶部提示。先在游戏中选择，再展开通知确认相同台词或选项，即可开启点按跟随，不需回控制页重复开启。支线期间由你轻点下一句区域推进；回到已核实共同线后，按通知核对台词并确认恢复自动播放。也可在“分支”页选择支线期间暂停配音；未知连接仍暂停。",13);
        
        autoPlaybackPermissionButton=Button(autoCard,"开启自动点击权限",ExplainAutoPlaybackAccessibility);ButtonIcon(autoPlaybackPermissionButton,"settings");
        ButtonIcon(Button(autoCard,"回到游戏配置 / 开始",ReturnToGameForAutoPlayback,primary:true),"play",true);
        autoPlaybackStopButton=Button(autoCard,"停止自动播放",()=>session.StopAutoPlayback());ButtonIcon(autoPlaybackStopButton,"pause");
        var autoHelp=Text("保持屏幕捕获授权有效，在所选游戏前台设置“下一句区域”，再点“自动播放”核对起点。取消核对不会播放或点击。切出游戏、锁屏或换屏后，重新核对位置；自动播放中上下句和重播继续沿用当前模式；需要时单独 OCR 定位。普通手动播放和 OCR 定位不需要无障碍权限。",13);
        
        FoldNote(content,"自动播放说明","HOW IT WORKS",new[]{autoDescription.Text??"",autoBoundary.Text??"",autoHelp.Text??""});
        }
        if(tab==1){
        Section("识别与声音","OCR & AUDIO");
        FoldNote(content,"识别与发热","OCR & HEAT",new[]{"OCR 用于主动定位；分支辅助由你确认选项，不靠 OCR 猜测分支，普通点按不逐句识别。感觉发热时可减少定位操作、使用台词列表选句，也可保持不变。充电、游戏等都可能发热，请自行判断；软件不会按温度弹窗或自动调整。"});
        var audioMode=Button(content,"声音："+(session.Settings.VoicePriority?"配音优先":"同时播放"),()=>Choose("声音策略",new[]{"同时播放：优先保留游戏音乐与音效","配音优先：请求游戏临时降低音量"},i=>
        {session.Settings.VoicePriority=i==1;session.ApplyAudioSettings();Render();}));
        Dropdown(audioMode,"volume");
        Line("不同手机与游戏的音频焦点行为有差异，请实际试听；此设置不会修改游戏内音量。",13);
        var volumeLabel=PgrTheme.Eyebrow(this,"配音音量 · VOLUME");volumeLabel.SetAllCaps(false);volumeLabel.SetPadding(0,Dp(12),0,0);content.AddView(volumeLabel);
        var volume=new SeekBar(this){Max=100,Progress=(int)(session.Settings.Volume*100)};PgrTheme.StyleSeekBar(volume);volume.ContentDescription="配音音量";volume.ProgressChanged+=(_,e)=>{if(e.FromUser){session.Settings.Volume=e.Progress/100f;session.ApplyAudioSettings();}};content.AddView(volume);
        ButtonIcon(Button(content,"角色音量",ShowSpeakerVolumes),"volume");
        NativeVoiceSettings();
        Line(session.Engine?.Pack.FixedVoiceStatus ?? "角色固定声线：等待配音包，未开启",13);
        Section("识别","OCR");
        var engineButton=Button(content,"识别引擎："+(session.Settings.OcrEngine==OcrEngineKind.Paddle?"PP-OCRv5":"ML Kit 中文（对照）"),()=>Choose("本地识别引擎",new[]{"PP-OCRv5 mobile（默认）","ML Kit 中文（测试对照）"},i=>
        {session.ChangeOcrEngine(i==0?OcrEngineKind.Paddle:OcrEngineKind.MlKitChinese);Render();}));
        Dropdown(engineButton,"ocr");
        captureStatusButton=Button(content,session.Screen.CaptureActive?"停止屏幕捕获":"授权屏幕捕获",()=>{if(session.Screen.CaptureActive)session.Screen.Stop();else RequestCapture();UpdateCaptureStatusButton();});
        ButtonIcon(captureStatusButton,"locate");
        OcrScopeSelector();
        ButtonIcon(Button(content,"编辑框选区域",EditRegion),"ocr");
        }
        if(tab==2)PortraitSettings();
        if(tab==3){
        Section("本地存档与诊断","DATA & DIAGNOSTICS");
        var archiveRow=Row(content);
        ButtonIcon(Button(archiveRow,"导出当前章节存档",()=>
        {if(session.Engine==null)return;exportText=System.Text.Json.JsonSerializer.Serialize(session.Engine.ExportNavigation(),Json.Options);SaveFile("剧情存档.json");}),"import");
        ButtonIcon(Button(archiveRow,"恢复当前章节存档",()=>{if(session.Engine!=null)SelectFile(RestoreRequest,"application/json");}),"history");
        ButtonIcon(Button(content,"导出本次诊断",()=>{exportText=session.Diagnostics.Export();SaveFile("配音诊断.json");}),"feedback");
        ButtonIcon(Button(content,UiText.ExportPlaybackLog,()=>{exportText=PgrVoice.Listening.PlaybackLog.Export();SaveFile("播放记录-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".csv","text/csv");}),"feedback");
        Line("导出为 CSV 文件（UTF-8，Excel / WPS 可直接打开），遇到问题或点过「反馈」后，把这个文件发给作者即可。",12);
        Line("播放记录：每次自动选择、按顺序续接、无配音越过、单选项 / 探索菜单自动继续、无文本跳小节、玩家反馈和每次停下各一行（CSV，Excel 可直接打开，按“类型”列筛选），只保留最近 5000 条。",12);
        var diagnostics=PgrTheme.Apply(Text(session.Diagnostics.Summary,12),PgrTheme.Role.Code,PgrTheme.Secondary);diagnostics.SetLineSpacing(Dp(3),1);content.AddView(diagnostics);
        ButtonIcon(Button(content,"停止配音和识别",()=>{session.Stop();VoiceForegroundService.StopAll(this);}),"close");
        ButtonIcon(Button(content,"使用说明与配置范围",()=>Info("使用与测试范围","Android 10 及以上，64 位。\n优化目标：骁龙 888 + 8 GB；推荐目标：8 Gen 2 / 8 Gen 3 + 12 GB。\n\n这些是开发目标，不能代表已实测最低配置。模拟器无法验证游戏同开性能、混音和发热。X Fold5 与骁龙 888 的完整测试结果请以交付说明为准。\n\nOCR 定位：默认识别全画面，无需框选；也可在定位页或设置页切换为框选区域。核对候选后选择开始台词，不会持续识别每句。识别不准时，可以直接从台词列表选句。\n\n点按跟随：需要辅助点击权限。确认当前句并设置触碰区域后，每次轻点框内，游戏和配音各推进一句。可在独立“分支”页选择“分支配音跟随”，或选择“分支期间暂停配音”，由你自己玩到共同线再核对续播。先设置一次下一句区域；普通二选一会在顶部通知，先在游戏里选好，再展开通知并点与画面相同的台词，从选中的句子开始播放，使用已保存区域跟随。也可切到“按选项”核对同名项。长句可点“全文”或长按核对；收起保留等待，取消才退出本次跟随。通知外仍可操作游戏；已核实的3D人物与话题也使用顶部通知，多段对话由你对照游戏选择，聊完返回菜单后再次通知，已聊人物可再次选择。\n\n自动播放：需屏幕捕获授权、辅助点击权限、游戏在前台及有效下一句区域。先核对当前句，点“从这句开始”直接重播，不重复 OCR；需要时选“重新 OCR 定位”，人工采用候选后再开启。取消核对不会播放或点击。之后按已确认路线顺序，配音自然播完才点下一句。默认使用“确认选后的对白，再继续播放”：遇到已知选项暂停，你先在游戏里选择，再展开顶部通知，点对应原选项与选后对白卡片，从这句直接续播，不补读被跳过的选项或台词；嵌套汇合或回到共同剧情都不再额外确认，下一处选项才再次提示。软件不识别你是否选错，选错可从悬浮分支页重选最近一次选项或重新定位。关闭选后对白确认及默认分支自动播放、且选择分支配音跟随时，普通二选一先停自动点击并显示顶部通知；游戏里选好后展开，按实际出现的台词确认，或切到“按选项”核对同名项，确认这一次就会使用已保存区域开启点按跟随，不必再回悬浮控制重复开启。顶部会明确提示分支期间使用点按跟随；等字幕完整后，你每轻点一次下一句区域，游戏和配音各推进一句。回到已核实共同线时通知具体台词，展开核对一致后恢复自动播放。两种模式交替开启，不会同时点击。暂停分支模式会在已核实汇合时提供共同线台词供你核对；未知连接仍停下。上述常规处理用于未开启默认分支的自动播放。开启“自动播放时使用默认分支”后，软件直接按默认路线配音；请你在游戏点同一项。顶部显示“进入分支一／二：选项内容”，约3秒后收起，不等待识别或再次手动续接。自动播放期间用悬浮上一句、当前句、下一句调整配音后会继续自动播放，不重复确认起点；调整当下不点击游戏。已开启默认分支时，在已确认的支线内暂停后也可核对当前句重新开启，继续当前路线，默认项仅用于下一处选择。悬浮标识始终按当前路线显示；内层汇合仍保留父分支标识。切出游戏、换屏或未知连接仍暂停核对。\n\n内外屏与横竖屏分别保存框选区域。使用框选定位时，请将角色名和对白完整框入，尽量避开动态背景。编辑框选区域不会切换定位范围；游戏的下一句触碰区域请在游戏前台另行设置。\n\n无账号、无云端 OCR，无需 root。普通播放和 OCR 定位都不需要无障碍权限。")),"story");
        }
        PageFooter();
    }
    void ExplainAutoPlaybackAccessibility()
    {
        // 18-B CONFIRM (was a stock AlertDialog); texts and actions unchanged.
        PgrDialog("CONFIRM","开启自动点击权限",DialogMessage("可选的“点按跟随”和“自动播放”需要辅助点击权限。点按跟随只在你轻点指定区域后，同坐标点击游戏一次；自动播放则在配音结束后点击。\n\n普通小按钮播放和 OCR 定位不需要。系统设置中请开启“剧情自动下一句”，再回到游戏配置并手动开始。仅开启权限不会播放或点击，也不读取游戏台词。"),new[]
        {
            new DialogKey("暂不开启",Xb.Normal,"close",null),
            new DialogKey("前往系统设置",Xb.Primary,null,()=>StartActivity(new Intent(global::Android.Provider.Settings.ActionAccessibilitySettings)))
        });
    }
    void ReturnToGameForAutoPlayback()=>ReturnToGameForAutoPlayback(false);
    void ReturnToGameForAutoPlayback(bool clickFollow)
    {
        ShowOverlay();
        if(!global::Android.Provider.Settings.CanDrawOverlays(this))return;
        if(ReturnToGame())GameHint("回到游戏后，展开悬浮控制，设置下一句区域并点“"+(clickFollow?"点按跟随":"自动播放")+"”。");
    }
    bool ReturnToGame()
    {
        // 只退回此前的任务，不查找、启动或绑定某个游戏客户端。
        if(MoveTaskToBack(true))return true;
        GameHint("请从最近任务切回游戏，再使用悬浮控制。");
        return false;
    }
    /// <summary>SPEC C.5 / 17-A ⑥: return-to-game sentence as an overlay top hint (NotTouchable, ~3.5 s); system Toast only when overlays are unavailable.</summary>
    void GameHint(string message)
    {
        if(!session.ShowTopHint(message))Toast.MakeText(this,message,ToastLength.Long)?.Show();
    }
    void ShowOverlay()
    {
        if(!global::Android.Provider.Settings.CanDrawOverlays(this))
        {StartActivityForResult(new Intent(global::Android.Provider.Settings.ActionManageOverlayPermission,global::Android.Net.Uri.Parse("package:"+PackageName)),OverlayRequest);return;}
        RequestNotifications();session.ShowOverlay();
    }
    void RequestNotifications()
    {if(Build.VERSION.SdkInt>=BuildVersionCodes.Tiramisu&&CheckSelfPermission("android.permission.POST_NOTIFICATIONS")!=Permission.Granted)RequestPermissions(new[]{"android.permission.POST_NOTIFICATIONS"},15);}
    void RequestCapture()
    {
        if(!alive||captureRequestPending)return;
        session.PrepareCapture();RequestNotifications();
        var manager=(MediaProjectionManager)GetSystemService(MediaProjectionService)!;
        captureRequestPending=true;
        try { StartActivityForResult(manager.CreateScreenCaptureIntent(),CaptureRequest); }
        catch { captureRequestPending=false;returnToGameAfterCapture=false;throw; }
    }
    void RequestManualOcr()
    {
        if(!session.Screen.CaptureActive){RequestCapture();return;}
        if(!ReturnToGame())return;
        if(global::Android.Provider.Settings.CanDrawOverlays(this))
        {session.ShowOverlay();session.RequestOcr(true);}
        else session.RequestOcr();
    }
    void EditRegion()
    {
        subtitleRegionOverlay?.Dispose();subtitleRegionOverlay=null;
        if(!session.Screen.CaptureActive){RequestCapture();return;}
        if(!ReturnToGame())return;
        session.RequestPreview((bitmap,selection)=>
        {
            if(!alive){bitmap.Dispose();return;}
            if(global::Android.Provider.Settings.CanDrawOverlays(this))
            {
                subtitleRegionOverlay?.Dispose();
                var picker=new SubtitleRegionOverlay(this,bitmap,session.Settings.RegionFor(selection.Display.RegionKey));
                subtitleRegionOverlay=picker;
                picker.Closed+=()=>{if(ReferenceEquals(subtitleRegionOverlay,picker))subtitleRegionOverlay=null;};
                picker.Saved+=region=>
                {
                    if(!alive)return;
                    try
                    {
                        session.ShowOverlay();
                        session.SetRegion(selection,region);
                        Toast.MakeText(this,session.Status,ToastLength.Long)?.Show();
                    }
                    catch(Exception ex)
                    {
                        session.Diagnostics.Log("字幕区域保存",ex.Message);
                        Toast.MakeText(this,"字幕区域未保存："+ex.Message,ToastLength.Long)?.Show();
                    }
                };
                try { picker.Show(); }
                catch(Exception ex)
                {
                    picker.Dispose();
                    session.Diagnostics.Log("字幕区域悬浮窗口",ex.Message);
                    Toast.MakeText(this,"无法显示字幕框选窗口，请检查悬浮权限后重试。",ToastLength.Long)?.Show();
                }
                return;
            }
            StartActivity(new Intent(this,typeof(MainActivity)).AddFlags(ActivityFlags.NewTask|ActivityFlags.SingleTop));
            var editor=new RegionEditor(this,bitmap,session.Settings.RegionFor(selection.Display.RegionKey));
            editor.LayoutParameters=new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent,(int)(Resources!.DisplayMetrics!.HeightPixels*.60));
            // 18-B SELECT shell (no overlay permission fallback); the editor keeps its own drags, so the body does not scroll.
            var dialog=PgrDialog("SELECT","框选角色名和完整对白，避开 NEXT 和波形",editor,new[]
            {
                new DialogKey("取消",Xb.Normal,"close",null),
                new DialogKey("保存",Xb.Primary,null,()=>session.SetRegion(selection,editor.Region))
            },scrollBody:false,fixedBodyHeight:(int)(Resources!.DisplayMetrics!.HeightPixels*.60));
            dialog.DismissEvent+=(_,_)=>{editor.Dispose();bitmap.Dispose();};
        });
    }
    void SelectFile(int request,string mime)
    {
        if(request==ImportRequest&&(deletingArchive||importCancellation!=null||session.PackagesBusy)){Info("请稍候","章节文件正在处理中，请等待完成。");return;}
        var intent=new Intent(Intent.ActionOpenDocument);intent.AddCategory(Intent.CategoryOpenable);intent.SetType("*/*");
        if(request==ImportRequest)
        {
            intent.AddFlags(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantWriteUriPermission|ActivityFlags.GrantPersistableUriPermission);
            intent.PutExtra(Intent.ExtraAllowMultiple,true);
        }
        intent.PutExtra(Intent.ExtraMimeTypes,new[]{mime,"application/octet-stream","application/x-zip-compressed"});StartActivityForResult(intent,request);
    }
    void SaveFile(string name,string mime="application/json")
    {var intent=new Intent(Intent.ActionCreateDocument);intent.AddCategory(Intent.CategoryOpenable);intent.SetType(mime);intent.PutExtra(Intent.ExtraTitle,name);StartActivityForResult(intent,ExportRequest);}
    protected override async void OnActivityResult(int requestCode,Result resultCode,Intent? data)
    {
        base.OnActivityResult(requestCode,resultCode,data);
        global::Android.Util.Log.Info("PgrVoice",$"ActivityResult request={requestCode} result={(int)resultCode} hasData={data!=null} hasUri={data?.Data!=null} flags={(data is null?0:(int)data.Flags)} authority={data?.Data?.Authority??""}");
        try
        {
            if(await HandleOnboardingResult(requestCode,resultCode,data))return;
            if(requestCode==CaptureRequest)
            {
                captureRequestPending=false;
                bool returnToGame=returnToGameAfterCapture;returnToGameAfterCapture=false;
                if(resultCode==Result.Ok&&data!=null)
                {
                    session.Screen.StartCapture(resultCode,data);
                    if(returnToGame)
                    {
                        session.ShowOverlay();
                        if(ReturnToGame())GameHint("已授权屏幕捕获。等游戏画面稳定后，点悬浮控制的“识别定位”。");
                    }
                }
                else session.CaptureDenied();
                return;
            }
            if(requestCode==OverlayRequest){if(global::Android.Provider.Settings.CanDrawOverlays(this))session.ShowOverlay();return;}
            if(resultCode!=Result.Ok||data==null)return;
            if(requestCode==ImportRequest)
            {
                await ImportSelectedZipsAsync(data);
                return;
            }
            if(requestCode==PortraitRequest||requestCode==StandeeRequest||requestCode==CoverRequest)
            {ImportPortraitImages(data,requestCode==StandeeRequest?PortraitStore.StandeeFolder:requestCode==CoverRequest?PortraitStore.CoverFolder:PortraitStore.PortraitFolder);return;}
            if(data.Data==null)return;
            if(requestCode==ExportRequest)
            {
                if(string.IsNullOrEmpty(exportText))throw new InvalidOperationException("导出已中断，请回到设置页重新选择要导出的内容。");
                using(var output=ContentResolver!.OpenOutputStream(data.Data,"wt")??throw new IOException("无法打开所选保存位置，请选择其他位置后重试。"))
                using(var writer=new StreamWriter(output))
                {await writer.WriteAsync(exportText);await writer.FlushAsync();}
                Toast.MakeText(this,"已导出",ToastLength.Short)?.Show();
            }
            else if(requestCode==RestoreRequest)
            {
                using var input=ContentResolver!.OpenInputStream(data.Data)!;
                var snapshot=await System.Text.Json.JsonSerializer.DeserializeAsync<NavigationSnapshot>(input,Json.Options)??throw new InvalidDataException("存档为空。");
                Confirm("恢复剧情存档","只恢复配音位置，游戏画面不会改变。",()=>session.Command(e=>{if(!e.ImportNavigation(snapshot))throw new InvalidDataException(e.NavigationError);}));
            }
        }
        catch(Exception ex) when(requestCode==ExportRequest&&(ex is Java.Lang.SecurityException||ex is UnauthorizedAccessException))
        {
            Info("未能导出","所选保存位置没有授予写入权限，文件尚未保存成功。\n\n请重新导出，在文件选择器左侧选择“内部存储”（有时显示设备名称），进入 Download 或 Documents 文件夹后保存。\n\n不要把刚才可能生成的空文件当作备份。");
        }
        catch(Exception ex){Info("操作未完成",ex.Message);}
    }
    sealed class SafeInsets(MainActivity owner) : Java.Lang.Object,View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View? view,WindowInsets? insets)
        {
            if(view==null||insets==null)return insets!;
            int left,top,right,bottom;
            if(OperatingSystem.IsAndroidVersionAtLeast(30))
            {var bars=insets.GetInsets(WindowInsets.Type.SystemBars()|WindowInsets.Type.DisplayCutout());left=bars.Left;top=bars.Top;right=bars.Right;bottom=bars.Bottom;}
            else
            {
#pragma warning disable CA1422
                left=insets.SystemWindowInsetLeft;top=insets.SystemWindowInsetTop;right=insets.SystemWindowInsetRight;bottom=insets.SystemWindowInsetBottom;
#pragma warning restore CA1422
            }
            // The shell takes the system insets (the nav rail sits inside them); the page column keeps its own margins.
            int horizontal=owner.Dp(owner.CompactLayout?12:16),vertical=owner.Dp(owner.CompactLayout?4:10);
            view.SetPadding(left,top,right,bottom);
            // Tablet / unfolded portrait (≥ 720dp wide): cap the phone column at a 680dp reading width, centred over the
            // full-bleed backdrop, instead of stretching every card across the screen.
            if(!owner.CompactLayout&&owner.Resources!.Configuration!.ScreenWidthDp>=720)
            {int usable=owner.Resources!.DisplayMetrics!.WidthPixels-left-right;horizontal=Math.Max(horizontal,(usable-owner.Dp(680))/2);}
            owner.root.SetPadding(horizontal,vertical,horizontal,vertical);
            return insets;
        }
    }
}
