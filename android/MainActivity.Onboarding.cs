using Android.App;
using Android.Content;
using AudioManager = Android.Media.AudioManager;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.AndroidApp.Ui;
using OperationCanceledException = System.OperationCanceledException;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    bool onboardingActive;
    AlertDialog? onboardingDialog;
    OnboardingPreview? onboardingPreview;
    OnboardingSelection? onboardingSelection;
    CancellationTokenSource? onboardingImportCancellation;
    TextView? onboardingTrialMessage;
    Button? onboardingHeard;
    string onboardingNotice="";
    OnboardingProgress Guide=>session.Onboarding.Progress;

    void RestoreOrStartOnboarding(Bundle? saved)
    {
        if(session.Onboarding.OfferAutomatically){StartOnboarding();return;}
        if(Guide.Status==OnboardingStatus.InProgress&&
            (saved?.GetBoolean("onboarding_active",false)==true||Guide.PendingExternal!=null))
        { onboardingActive=true;session.PauseForOnboarding();RenderOnboarding(); }
    }
    void StartOnboarding()
    {
        if(session.PackagesBusy||importCancellation!=null){Info("请稍候","请等章节文件处理完成后再打开引导。");return;}
        StopOnboardingPreview();session.PauseForOnboarding();session.Onboarding.Begin();
        session.Settings.OnboardingShown=true;session.SaveSettings();
        onboardingActive=true;onboardingNotice="";RenderOnboarding();
    }
    void ResumeOnboarding()
    { if(onboardingActive)UpdateOnboardingTrial(); }
    void PauseOnboarding()
    { onboardingPreview?.Stop();UpdateOnboardingTrial();if(onboardingActive)session.Onboarding.Save(); }
    void StopOnboardingPreview()
    {
        if(onboardingPreview!=null){onboardingPreview.Changed-=UpdateOnboardingTrial;onboardingPreview.Dispose();onboardingPreview=null;}
        onboardingTrialMessage=null;onboardingHeard=null;
    }
    void DestroyOnboarding()
    {
        StopOnboardingPreview();onboardingImportCancellation?.Cancel();
        onboardingDialog?.Dismiss();onboardingDialog=null;
    }
    void CloseOnboarding(bool skip)
    {
        onboardingActive=false;session.Onboarding.Exit(skip);onboardingImportCancellation?.Cancel();
        StopOnboardingPreview();onboardingDialog?.Dismiss();onboardingDialog=null;
        // 暂停状态保留；关闭教学不恢复任何之前的播放或自动点击。
    }
    void RenderOnboarding()
    {
        if(!alive||!onboardingActive)return;
        StopOnboardingPreview();onboardingDialog?.Dismiss();onboardingDialog=null;
        // 13-B: dark L step card (QUICK START + outline 0n/04 + vertical step list with results); content scrolls on archive paper;
        // dark S navigation strip. Wide landscape: step card = left 300dp column, nav strip under the right column only.
        float density=Resources!.DisplayMetrics!.Density;
        bool wideGuide=WideLandscape;
        var shell=new LinearLayout(this){Orientation=wideGuide?Orientation.Horizontal:Orientation.Vertical};
        shell.Background=new ArchivePaperDrawable(density){Fill=PgrTheme.Background,TideDp=40};
        shell.SetPadding(Dp(14),Dp(14),Dp(14),Dp(10));
        var stepCard=OnboardingStepCard(wideGuide);
        var outer=new LinearLayout(this){Orientation=Orientation.Vertical};
        if(wideGuide){shell.AddView(stepCard,new LinearLayout.LayoutParams(Dp(300),-1){RightMargin=Dp(14)});shell.AddView(outer,new LinearLayout.LayoutParams(0,-1,1));}
        else{shell.AddView(stepCard,new LinearLayout.LayoutParams(-1,Dp(166)){BottomMargin=Dp(8)});shell.AddView(outer,new LinearLayout.LayoutParams(-1,0,1));}
        var scroll=new ScrollView(this){FillViewport=true};
        var box=new LinearLayout(this){Orientation=Orientation.Vertical};scroll.AddView(box);
        outer.AddView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        if(onboardingNotice.Length>0)
        {
            var notice=Text(onboardingNotice,13);notice.SetTextColor(PgrTheme.Foreground);
            notice.Background=FusionDeco.Over(PgrTheme.Plate(this,PgrTheme.Panel,PgrTheme.BorderStrong,PgrTheme.CutSmall,Ui.Corners.BottomRight,PgrTheme.Steel,Ui.AccentEdge.Left), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutBR = PgrTheme.CutSmall });
            notice.SetPadding(Dp(14),Dp(10),Dp(12),Dp(10));box.AddView(notice,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(6)});
        }
        if(onboardingImportCancellation!=null)
        {
            box.AddView(OnboardingTitle("正在导入章节"));
            box.AddView(OnboardingBody("文件较大时需要等一会儿。导入完成后会回到选章与试听。"));
            Button(box,"取消本次导入",()=>{onboardingImportCancellation?.Cancel();});
        }
        else switch(Guide.Step)
        {
            case OnboardingStep.Purpose:
                box.AddView(OnboardingTitle("你准备怎样听剧情？"));
                box.AddView(OnboardingBody("先选用途，再选一章试听。全程不会自动开始连续播放。"));
                var purposes=new LinearLayout(this){Orientation=Orientation.Horizontal};
                purposes.AddView(PurposeCard("听剧情",null,"headphones","打开听书，独立保存收听位置，无需打开游戏。",()=>{Guide.SelectPurpose(OnboardingPurpose.Listening);SaveAndRenderOnboarding();}),new LinearLayout.LayoutParams(0,-2,1){RightMargin=Dp(5)});
                purposes.AddView(PurposeCard("边玩边听",null,"play","先试听，再按需开启悬浮控制，回到正在玩的游戏。",()=>{Guide.SelectPurpose(OnboardingPurpose.Game);SaveAndRenderOnboarding();}),new LinearLayout.LayoutParams(0,-2,1){LeftMargin=Dp(5)});
                box.AddView(purposes,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(8)});
                break;
            case OnboardingStep.Package:BuildOnboardingPackages(box);break;
            case OnboardingStep.Trial:BuildOnboardingTrial(box);break;
            case OnboardingStep.Finish:BuildOnboardingFinish(box);break;
        }
        var reopen=Text("关闭后可在“设置 → 重新打开首次使用引导”继续。",12);reopen.SetTextColor(PgrTheme.Muted);reopen.SetPadding(0,Dp(6),0,Dp(4));outer.AddView(reopen);
        var navigation=new LinearLayout(this){Orientation=Orientation.Horizontal};navigation.SetGravity(GravityFlags.CenterVertical);
        navigation.Background=new DarkPanelDrawable(density,DecoTier.S){CutTopRightDp=12};navigation.SetPadding(Dp(6),Dp(6),Dp(6),Dp(6));
        void NavKey(string text,Xb kind,string icon,Action click){var k=new Button(this){Text=text};PgrUi.Style(k,kind,icon,center:true,textSp:13);k.SetSingleLine(true);k.SetMinWidth(0);k.SetMinimumWidth(0);k.Click+=(_,_)=>Safe(click);DropIconWhenTight(k);navigation.AddView(k,new LinearLayout.LayoutParams(0,Dp(48),1){LeftMargin=Dp(3),RightMargin=Dp(3)});}
        if(onboardingImportCancellation==null&&Guide.Step!=OnboardingStep.Purpose)NavKey("上一步",Xb.Dark,"back",()=>{Guide.Back();SaveAndRenderOnboarding();});
        NavKey("暂时跳过",Xb.Dark,"next",()=>CloseOnboarding(true));NavKey("关闭引导",Xb.Dark,"close",()=>CloseOnboarding(false));
        outer.AddView(navigation,new LinearLayout.LayoutParams(-1,-2));
        onboardingDialog=new AlertDialog.Builder(this).SetView(shell)!.SetCancelable(true)!.Create();
        onboardingDialog!.CancelEvent+=(_,_)=>CloseOnboarding(false);
        onboardingDialog.Show();StyleDialog(onboardingDialog);
        onboardingDialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent,ViewGroup.LayoutParams.MatchParent);
        // Paper fills the whole dialog window (AlertDialog leaves an empty button panel under a wrap-height view).
        onboardingDialog.Window?.SetBackgroundDrawable(shell.Background);shell.Background=null;
    }
    /// <summary>13-B step card: QUICK START + outline 0n/04 (left), vertical steps 用途 / 选章 / 试听 / 完成 with results (right).</summary>
    View OnboardingStepCard(bool vertical)
    {
        float density=Resources!.DisplayMetrics!.Density;
        var card=new LinearLayout(this){Orientation=vertical?Orientation.Vertical:Orientation.Horizontal};
        var stepDeco=new DarkPanelDrawable(density,DecoTier.L){Mark="QUICK START",CutTopRightDp=18,Watermark=vertical};
        card.Background=stepDeco;
        card.SetPadding(Dp(14),Dp(12),Dp(12),Dp(vertical?44:30));
        int step=(int)Guide.Step;
        var left=new LinearLayout(this){Orientation=Orientation.Vertical};
        var qs=new TextView(this){Text="QUICK START",TextSize=10};qs.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);qs.LetterSpacing=.14f;qs.SetTextColor(PgrArt.GoldLine);left.AddView(qs);
        left.AddView(PgrUi.Outline(this,$"{step+1:00}",vertical?64:52,PgrTheme.A(Color.White,220),1.4f));
        var of=new TextView(this){Text="/ 04",TextSize=12};of.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);of.SetTextColor(PgrTheme.NavSub);left.AddView(of);
        card.AddView(left,vertical?new LinearLayout.LayoutParams(-1,-2){BottomMargin=Dp(12)}:new LinearLayout.LayoutParams(Dp(104),-1));
        var steps=new LinearLayout(this){Orientation=Orientation.Vertical,ImportantForAccessibility=ImportantForAccessibility.No};
        string[] labels={"用途","选章","试听","完成"};
        string purpose=step>0?(Guide.Purpose==OnboardingPurpose.Game?"边玩边听":"听剧情"):"";
        var chosenPack=Guide.PackId!=null?session.Packages.Find(Guide.PackId):null;
        string chapter=chosenPack!=null?PackTitle(chosenPack.Title):"";
        // Chapter chosen: its cover sits behind the step card (scrimmed from the left); no cover = plain deck.
        if(chosenPack!=null)LoadChapterCover(card,image=>stepDeco.Cover=image,chosenPack.Title,chosenPack.PackId,Dp(480));
        string[] results={purpose,step>1?chapter:"",step>2?"我听到了":"",""};
        for(int i=0;i<4;i++)
        {
            var row=new LinearLayout(this){Orientation=Orientation.Horizontal};row.SetGravity(GravityFlags.CenterVertical);
            var mark=new TextView(this){Text=i<step?"✓":i==step?"◆":"◇",TextSize=11,Gravity=GravityFlags.Center};
            mark.SetTextColor(i<step?PgrArt.MistLine:i==step?PgrArt.TideRed:PgrTheme.NavSub);row.AddView(mark,new LinearLayout.LayoutParams(Dp(18),-2));
            var label=new TextView(this){Text=labels[i],TextSize=13};label.SetTypeface(PgrTheme.SansBold,i==step?TypefaceStyle.Bold:TypefaceStyle.Normal);label.SetTextColor(i<=step?PgrTheme.OnPlate:PgrTheme.NavSub);row.AddView(label);
            string tail=i==step?"进行中":results[i];
            if(tail.Length>0){var r=new TextView(this){Text="  ·  "+tail,TextSize=11};r.SetTextColor(i==step?PgrArt.TideRed:PgrTheme.NavText);r.SetSingleLine(true);r.Ellipsize=global::Android.Text.TextUtils.TruncateAt.End;row.AddView(r,new LinearLayout.LayoutParams(0,-2,1));}
            steps.AddView(row,new LinearLayout.LayoutParams(-1,Dp(vertical?30:28)));
        }
        card.AddView(steps,vertical?new LinearLayout.LayoutParams(-1,-2):new LinearLayout.LayoutParams(0,-2,1));
        card.ContentDescription=$"首次使用 · 第 {step+1}/4 步";
        return card;
    }
    View PurposeCard(string title,string? english,string icon,string hint,Action pick)
    {
        var card=new LinearLayout(this){Orientation=Orientation.Vertical,Clickable=true,Focusable=true};
        card.Background=PgrTheme.Pressable(this,FusionDeco.Over(PgrTheme.Card(this,false,true),new FusionDeco(Resources!.DisplayMetrics!.Density,DecoTier.M){CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium,InsetDp=3}),PgrTheme.CutMedium,Ui.Corners.Diagonal,PgrTheme.NeutralRipple);card.SetPadding(Dp(14),Dp(14),Dp(12),Dp(14));card.SetMinimumHeight(Dp(150));
        card.AddView(Icon(icon,PgrTheme.Red,28),new LinearLayout.LayoutParams(Dp(28),Dp(28)){BottomMargin=Dp(10)});
        var t=new TextView(this){Text=title,TextSize=18};t.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);t.SetTextColor(PgrTheme.Foreground);card.AddView(t);
        if(english!=null){var en=new TextView(this){Text=english,TextSize=9};en.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);en.LetterSpacing=.12f;en.SetTextColor(PgrTheme.RedText);card.AddView(en);}
        var h=new TextView(this){Text=hint,TextSize=12.5f};h.SetTextColor(PgrTheme.Secondary);h.SetPadding(0,Dp(8),0,0);card.AddView(h);
        card.ContentDescription=title+"，"+hint;
        card.Click+=(_,_)=>Safe(pick);
        return card;
    }
    TextView OnboardingTitle(string text){var title=PgrTheme.Apply(Text(text,24),PgrTheme.Role.Display);title.TextSize=CompactLayout?21:PgrTheme.TextDisplay;title.SetPadding(0,Dp(10),0,Dp(4));return title;}
    TextView OnboardingBody(string text){var body=Text(text,15);body.SetTextColor(PgrTheme.Secondary);return body;}
    TextView OnboardingHint(string text){var hint=Text(text,13);hint.SetTextColor(PgrTheme.Muted);hint.SetPadding(Dp(2),0,0,Dp(10));return hint;}
    /// <summary>Four-segment HUD progress strip; the current step is crimson, completed steps steel.</summary>
    void OnboardingSteps(LinearLayout parent)
    {
        var strip=new LinearLayout(this){Orientation=Orientation.Horizontal,ImportantForAccessibility=ImportantForAccessibility.No};
        for(int i=0;i<4;i++)
        {
            var segment=new View(this);
            segment.SetBackgroundColor(i==(int)Guide.Step?PgrTheme.Red:i<(int)Guide.Step?PgrTheme.Steel:PgrTheme.Border);
            strip.AddView(segment,new LinearLayout.LayoutParams(0,Dp(3),1){RightMargin=i<3?Dp(4):0});
        }
        parent.AddView(strip,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(8),BottomMargin=Dp(6)});
    }
    void SaveAndRenderOnboarding()
    { session.Onboarding.Save();onboardingNotice="";RenderOnboarding(); }
    void BuildOnboardingPackages(LinearLayout box)
    {
        box.AddView(OnboardingTitle("选一章配音"));
        box.AddView(OnboardingBody("已有章节可以直接选择。也可从手机文件中选 ZIP，软件会完成导入，不用先解压。"));
        ButtonIcon(Button(box,"从手机导入章节 ZIP",BeginOnboardingImport),"import");
        var packages=session.Packages.List();
        if(packages.Count==0)box.AddView(OnboardingHint("还没有导入章节。请准备一章配音 ZIP，或暂时跳过后再导入。"));
        foreach(var package in packages)
            ButtonIcon(Button(box,PackTitle(package.Title),()=>ChooseOnboardingChapter(package.PackId)),"archive");
    }
    void ChooseOnboardingChapter(string packId)
    {
        Safe(()=>
        {
            var pack=session.Packages.Load(packId);
            if(pack.Chapters.Count==1){SelectOnboardingSample(pack,pack.Chapters[0]);return;}
            Choose("选择要试听的章节",pack.Chapters.Select(c=>PackTitle(c.Title)).ToArray(),i=>SelectOnboardingSample(pack,pack.Chapters[i]));
        });
    }
    void SelectOnboardingSample(Pack pack,Chapter chapter,string? after=null)
    {
        var sections=chapter.Sections.Select(s=>s.Id).ToHashSet();
        var lines=pack.Nodes.Where(n=>n.Kind=="line"&&!n.Archived&&sections.Contains(n.SectionId)).ToList();
        var playable=lines.Where(n=>pack.ResolveAudio(n) is { } path&&File.Exists(path)).ToList();
        var choices=playable.Count>0?playable:lines;
        if(choices.Count==0){onboardingNotice="本章没有可试听的台词，请换一章。";RenderOnboarding();return;}
        int index=after==null?0:(choices.FindIndex(n=>n.Id==after)+1)%choices.Count;
        Guide.SelectLine(pack.Id,chapter.Id,choices[index].Id);SaveAndRenderOnboarding();
    }
    bool OnboardingMediaAudible()
    {
        var manager=(AudioManager)GetSystemService(AudioService)!;
        return manager.GetStreamVolume(global::Android.Media.Stream.Music)>0;
    }
    float OnboardingVolume()=>SpeakerVolume.Apply(session.Settings.Volume,session.Settings.SpeakerVolumes,onboardingSelection?.Node.Speaker);
    void BuildOnboardingTrial(LinearLayout box)
    {
        box.AddView(OnboardingTitle("试听这一句"));
        try{onboardingSelection=session.SelectedOnboardingLine();}
        catch(Exception ex){onboardingSelection=null;box.AddView(Text("章节暂不可用："+ex.Message,14));}
        if(onboardingSelection is not { } selection)
        {
            box.AddView(Text("所选章节或台词已经变化，请重新选择。",14));
            Button(box,"重新选章",()=>{Guide.Step=OnboardingStep.Package;SaveAndRenderOnboarding();});return;
        }
        var sampleChapter=PgrTheme.Eyebrow(this,PackTitle(selection.Chapter.Title),PgrTheme.Steel);sampleChapter.SetAllCaps(false);sampleChapter.TextSize=12;sampleChapter.LetterSpacing=.04f;box.AddView(sampleChapter);
        var sample=Text(AppSession.BrowseSpeaker(selection.Node)+"："+selection.Node.Text,18);sample.SetTypeface(PgrTheme.SansMedium,global::Android.Graphics.TypefaceStyle.Normal);
        sample.Background=FusionDeco.Over(PgrTheme.Card(this,selected:true),new FusionDeco(Resources!.DisplayMetrics!.Density,DecoTier.M){CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium});sample.SetPadding(Dp(16),Dp(12),Dp(14),Dp(12));
        box.AddView(sample,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(6)});
        box.AddView(OnboardingHint("只播放画面上的一句，播完就停；不会改动游戏配音或听书位置。"));
        onboardingPreview=new(new AndroidAudioPlayer(this),OnboardingVolume,OnboardingMediaAudible);
        onboardingPreview.Changed+=UpdateOnboardingTrial;
        onboardingTrialMessage=Text(onboardingPreview.Message,14);box.AddView(onboardingTrialMessage);
        // 13-B TRIAL console (dark M): 試听 primary, 我听到了 teal-lit, 换一句 / 没有声音 dark; one row in wide landscape.
        var console=new LinearLayout(this){Orientation=Orientation.Vertical};
        console.Background=new DarkPanelDrawable(Resources!.DisplayMetrics!.Density,DecoTier.M){CutTopRightDp=14,ShowEmblem=false};console.SetPadding(Dp(12),Dp(10),Dp(12),Dp(22));
        var consoleHead=new TextView(this){Text="TRIAL · 单句试听",TextSize=10};consoleHead.SetTypeface(PgrTheme.Condensed,global::Android.Graphics.TypefaceStyle.Bold);consoleHead.LetterSpacing=.1f;consoleHead.SetTextColor(PgrArt.GoldLine);console.AddView(consoleHead,new LinearLayout.LayoutParams(-1,-2){BottomMargin=Dp(6)});
        bool oneRow=WideLandscape;
        var r1=new LinearLayout(this){Orientation=Orientation.Horizontal};var r2=oneRow?r1:new LinearLayout(this){Orientation=Orientation.Horizontal};
        Button TrialKey(LinearLayout row,string text,Xb kind,string icon,Action click){var k=new Button(this){Text=text};PgrUi.Style(k,kind,icon,center:true,textSp:13);k.SetSingleLine(true);k.SetMinWidth(0);k.SetMinimumWidth(0);k.Click+=(_,_)=>Safe(click);DropIconWhenTight(k);row.AddView(k,new LinearLayout.LayoutParams(0,Dp(50),1){LeftMargin=Dp(3),RightMargin=Dp(3)});return k;}
        TrialKey(r1,"试听这一句",Xb.Primary,"play",()=>onboardingPreview?.Start(selection.AudioPath));
        TrialKey(r1,"换一句试听",Xb.Dark,"next",()=>SelectOnboardingSample(selection.Pack,selection.Chapter,selection.Node.Id));
        onboardingHeard=TrialKey(r2,"我听到了",Xb.Dark,"headphones",()=>
        {if(Guide.ConfirmHeard(onboardingPreview?.CanAcknowledge==true))SaveAndRenderOnboarding();});
        onboardingHeard.SetTextColor(PgrArt.MistLine);
        onboardingHeard.Enabled=false;
        TrialKey(r2,"没有声音",Xb.Dark,"close",()=>
        {
            onboardingPreview?.Stop();UpdateOnboardingTrial();
            if(onboardingTrialMessage!=null)onboardingTrialMessage.Text="请检查手机媒体音量、蓝牙/耳机输出、下面两项音量和本句音频，再点“试听这一句”。本步骤会保留。";
        });
        console.AddView(r1);if(!oneRow)console.AddView(r2,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6)});
        box.AddView(console,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(6)});
        var totalLabel=PgrTheme.Eyebrow(this,"配音总音量 · VOLUME");totalLabel.SetAllCaps(false);totalLabel.SetPadding(0,Dp(12),0,0);box.AddView(totalLabel);
        var total=new SeekBar(this){Max=100,Progress=(int)(session.Settings.Volume*100)};PgrTheme.StyleSeekBar(total);total.ContentDescription="配音总音量";
        total.ProgressChanged+=(_,e)=>{if(e.FromUser){onboardingPreview?.Stop();session.Settings.Volume=e.Progress/100f;session.ApplyAudioSettings();UpdateOnboardingTrial();}};box.AddView(total);
        var roleLabel=PgrTheme.Eyebrow(this,"本句角色音量 · "+selection.Node.Speaker);roleLabel.SetAllCaps(false);roleLabel.SetPadding(0,Dp(8),0,0);box.AddView(roleLabel);
        var role=new SeekBar(this){Max=100,Progress=SpeakerVolume.GetPercent(session.Settings.SpeakerVolumes,selection.Node.Speaker)};PgrTheme.StyleSeekBar(role);role.ContentDescription="本句角色音量";
        role.ProgressChanged+=(_,e)=>{if(e.FromUser){onboardingPreview?.Stop();session.SetSpeakerVolume(selection.Node.Speaker,e.Progress);UpdateOnboardingTrial();}};box.AddView(role);
        ButtonIcon(Button(box,"打开系统声音设置",()=>{PauseOnboarding();StartActivity(new Intent(global::Android.Provider.Settings.ActionSoundSettings));}),"settings");
        bool audioReady=File.Exists(selection.AudioPath);
        var audioState=Text(audioReady?"本句音频文件已就绪。":"本句音频缺失，请换一句或返回上一步重新导入本章。",13);audioState.SetTextColor(audioReady?PgrTheme.Steel:PgrTheme.RedText);box.AddView(audioState);
    }
    void UpdateOnboardingTrial()
    {
        if(!alive||!onboardingActive)return;
        if(onboardingTrialMessage!=null&&onboardingPreview!=null)onboardingTrialMessage.Text=onboardingPreview.Message;
        if(onboardingHeard!=null)onboardingHeard.Enabled=onboardingPreview?.CanAcknowledge==true;
    }
    void BuildOnboardingFinish(LinearLayout box)
    {
        box.AddView(OnboardingTitle("已完成试听"));
        if(Guide.Purpose==OnboardingPurpose.Listening)
        {
            box.AddView(OnboardingBody("打开所选章节后，点“继续收听”开始听书。收听位置单独保存。"));
            ButtonIcon(Button(box,"完成并打开听书",()=>FinishOnboarding(false),primary:true),"headphones",true);
        }
        else
        {
            box.AddView(OnboardingBody("打开所选章节后，先选与游戏当前画面一致的台词。需要在游戏里控制时，再开启悬浮控制。"));
            box.AddView(OnboardingHint("悬浮控制可手动播放。OCR 定位和辅助点击在你选择对应功能时再授权。"));
            ButtonIcon(Button(box,global::Android.Provider.Settings.CanDrawOverlays(this)?"显示悬浮控制":"按需开启悬浮控制",BeginOnboardingOverlay),"open");
            ButtonIcon(Button(box,"完成并打开游戏配音",()=>FinishOnboarding(false),primary:true),"story",true);
            ButtonIcon(Button(box,"完成并返回游戏",()=>FinishOnboarding(true)),"play");
        }
    }
    void FinishOnboarding(bool returnToGame)
    {
        Safe(()=>
        {
            session.OpenOnboardingChapter();onboardingActive=false;StopOnboardingPreview();
            onboardingDialog?.Dismiss();onboardingDialog=null;
            page=Guide.Purpose==OnboardingPurpose.Listening?5:1;Render();
            if(returnToGame)ReturnToGame();
        });
    }
    void BeginOnboardingOverlay()
    {
        if(global::Android.Provider.Settings.CanDrawOverlays(this)){session.ShowOverlay();onboardingNotice="悬浮控制已开启；完成后可返回游戏。";RenderOnboarding();return;}
        Guide.BeginExternal("overlay");session.Onboarding.Save();
        try{StartActivityForResult(new Intent(global::Android.Provider.Settings.ActionManageOverlayPermission,global::Android.Net.Uri.Parse("package:"+PackageName)),Guide.ExternalRequestCode);}
        catch{Guide.EndExternal();session.Onboarding.Save();throw;}
    }
    void BeginOnboardingImport()
    {
        if(onboardingImportCancellation!=null||session.PackagesBusy)return;
        Guide.BeginExternal("import");session.Onboarding.Save();
        var intent=new Intent(Intent.ActionOpenDocument);intent.AddCategory(Intent.CategoryOpenable);intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes,new[]{"application/zip","application/octet-stream","application/x-zip-compressed"});
        intent.AddFlags(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantPersistableUriPermission);
        try{StartActivityForResult(intent,Guide.ExternalRequestCode);}
        catch{Guide.EndExternal();session.Onboarding.Save();throw;}
    }
    async Task<bool> HandleOnboardingResult(int request,Result result,Intent? data)
    {
        if(request<0x4000||request>=0x8000)return false;
        string kind=(request&1)==0?"import":"overlay";
        long epoch=Guide.ExternalEpoch;
        if(!alive||!onboardingActive||request!=Guide.ExternalRequestCode||!Guide.AcceptExternal(kind,epoch))return true;
        Guide.EndExternal();session.Onboarding.Save();
        if(kind=="overlay")
        {
            if(global::Android.Provider.Settings.CanDrawOverlays(this)){session.ShowOverlay();onboardingNotice="悬浮控制已开启；完成后可返回游戏。";}
            else onboardingNotice="尚未开启悬浮权限，仍可完成引导并使用主界面。";
            RenderOnboarding();return true;
        }
        if(result!=Result.Ok||data?.Data==null){onboardingNotice="已取消选择文件，可以继续选择已有章节或重新导入。";RenderOnboarding();return true;}
        using var cancellation=new CancellationTokenSource();onboardingImportCancellation=cancellation;
        onboardingNotice="正在导入所选 ZIP，请稍候…";RenderOnboarding();
        try
        {
            var uri=data.Data;
            ImportedZipSource? source=null;
            try{source=await Task.Run(()=>ImportedZipAccess.Read(ApplicationContext!,uri));}catch(Exception ex){session.Diagnostics.Log("引导ZIP",ex.Message);}
            cancellation.Token.ThrowIfCancellationRequested();
            using var input=ContentResolver!.OpenInputStream(uri)??throw new IOException("无法读取所选 ZIP。");
            var installed=await session.ImportAsync(input,cancellation.Token,source,openAfterImport:false);
            if(ReferenceEquals(onboardingImportCancellation,cancellation))onboardingImportCancellation=null;
            if(!alive||!onboardingActive||Guide.Epoch!=epoch)return true;
            if(installed!=null)
            {
                if(source!=null)ImportedZipAccess.PersistGrant(ApplicationContext!,uri,data.Flags);
                onboardingNotice="导入完成，请选择要试听的章节。";ChooseOnboardingChapter(installed.PackId);
            }
            else{onboardingNotice="导入已取消，可重新选择 ZIP。";RenderOnboarding();}
        }
        catch(Exception ex)
        {
            if(ReferenceEquals(onboardingImportCancellation,cancellation))onboardingImportCancellation=null;
            if(alive&&onboardingActive&&Guide.Epoch==epoch){onboardingNotice=ex is OperationCanceledException?"导入已取消，可重新选择 ZIP。":"导入未完成："+ex.Message;RenderOnboarding();}
        }
        finally{if(ReferenceEquals(onboardingImportCancellation,cancellation))onboardingImportCancellation=null;}
        return true;
    }
}
