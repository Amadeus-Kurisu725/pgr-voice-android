using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    void BranchSettingsPage()
    {
        CoverHeader("游戏配音 · BRANCH ROUTING","分支线设置",null,new CoverGlyphDrawable(Resources!.DisplayMetrics!.Density,"route"));
        Line("选择遇到分支时怎样继续。设置会保存，下次仍使用你的选择。",13);
        // 08 landscape: route map card left (≈390dp, expanded), settings in the middle column.
        bool wideRoute=WideLandscape&&PageWidthDp>=640;
        if(wideRoute){var left=SideColumns(Math.Min(390,PageWidthDp/2));RouteMapCard(left,true);}
        else RouteMapCard(content,false);
        DefaultBranchPlaybackSettings();
        if (!session.Settings.AutoPlayConfirmBranch)
        {
        Section("常规分支处理","STANDARD HANDLING");
        Line("用于点按跟随，以及未开启默认分支的自动播放。",13);
        var modes=new RadioGroup(this){Orientation=Orientation.Vertical};
        var follow=new RadioButton(this){Id=View.GenerateViewId(),Text="分支配音跟随",TextSize=16,Checked=session.Settings.BranchAutoFollow};
        var pause=new RadioButton(this){Id=View.GenerateViewId(),Text="分支期间暂停配音",TextSize=16,Checked=!session.Settings.BranchAutoFollow};
        foreach(var option in new[]{follow,pause})
        {
            PgrTheme.StyleRadio(option);PlateRadio(option);
            modes.AddView(option,new RadioGroup.LayoutParams(-1,-2){TopMargin=Dp(3),BottomMargin=Dp(3)});
        }
        modes.Check(session.Settings.BranchAutoFollow?follow.Id:pause.Id);
        modes.CheckedChange+=(_,e)=>
        {
            if(e.CheckedId!=follow.Id&&e.CheckedId!=pause.Id)return;
            bool enabled=e.CheckedId==follow.Id;
            if(session.Settings.BranchAutoFollow==enabled)return;
            session.SetBranchAutoFollow(enabled);Render();
        };
        content.AddView(modes,new LinearLayout.LayoutParams(-1,-2));
        if(session.Settings.BranchAutoFollow)
            FoldNote(content,"分支配音跟随 · 4 步","4 STEPS",new[]{
                "未开启默认分支自动播放时，自动播放到分支会先暂停，并在顶部提示：已到分支，请先在游戏中选择，再确认并开启点按跟随。",
                "1. 先在游戏里选择分支，等当前台词出现。",
                "2. 展开顶部通知，选与游戏相同的台词并确认；也可按选项核对同名分支。确认会播放该句，并使用已保存区域开启点按跟随，不用再去控制页开启一次。长句可点“全文”核对。",
                "3. 支线期间不会自动点下一句。等字幕完整后，在保存的下一句区域轻点一次，游戏和配音一起推进。",
                "4. 原来用自动播放的，回到已核实共同线时会在顶部提示具体台词；游戏出现这句后确认恢复自动播放。原来用点按跟随的不会改成自动播放，遇到续接提示时按游戏画面核对。",
                "人物或话题也先在游戏中选好，再确认同项；聊完返回菜单后会再次通知。未知连接、屏幕变化或点击权限不可用时，会说明原因，需核对后继续。"});
        else{var explanation=Card(content);explanation.AddView(Text("遇到普通分支后暂停配音和自动点击，由你自己玩分支。有明确汇合点时，顶部通知会预览共同线台词；游戏出现这句后，展开并确认继续。无需在软件中选择走了哪条分支。",14));}
        }
        FoldNote(content,"需要确认时的顶部通知","TOP NOTICE",new[]{"顶部通知先显示简短内容，点开后查看和操作；收起只缩小提示，保留等待，取消才退出本次跟随。请先对照游戏，确认一致后才继续；未核实汇合点或3D人物互动不会猜测共同线位置，可从台词列表或定位页重新对齐。"});
        Section("下一句点击区域","ADVANCE AREA");
        Line("开始前在游戏里设置一次，自动播放与点按跟随共用。换屏幕或横竖屏后，使用对应屏幕保存的区域。",13);
        ButtonIcon(Button(content,"回到游戏设置下一句区域",()=>ReturnToGameForAutoPlayback(true)),"open");
        ButtonIcon(Button(content,"辅助点击权限",ExplainAutoPlaybackAccessibility),"settings");
        Line("除“默认分支”外，这里的设置仅用于游戏配音；听书是否采用默认分支在听书页选择。",12);
        PageFooter();
    }

    /// <summary>默认分支序号（第一 / 第二个选项）：听书“默认分支”与自动播放默认分支共用；只有一个选项时按第一个。沿用 09-B / 10-B 段页签样式。</summary>
    void DefaultBranchOptionChooser()
    {
        Section(UiText.DefaultBranchSection,UiText.DefaultBranchSectionEnglish);
        int selected=session.Settings.DefaultBranchOption==2?1:0;
        var tabs=SegmentTabs(UiText.DefaultBranchSegments,UiText.DefaultBranchSegmentsEnglish,null,selected,i=>{session.SetDefaultBranchOption(i+1);Render();},false);
        content.AddView(tabs,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(4),BottomMargin=Dp(4)});
        Line(UiText.DefaultBranchCaption,12);
        // 0.4.7（G/I/L）：分支自动选择（默认开启）。
        var autoSelect=new Switch(this){Text=UiText.BranchAutoSelectLabel,TextSize=16,Checked=session.Settings.BranchAutoSelect};
        PgrTheme.StyleSwitch(autoSelect);
        autoSelect.CheckedChange+=(_,e)=>{if(session.Settings.BranchAutoSelect==e.IsChecked)return;session.SetBranchAutoSelect(e.IsChecked);Render();};
        content.AddView(autoSelect,new LinearLayout.LayoutParams(-1,-2));PlateSwitch(autoSelect);
        Line(UiText.BranchAutoSelectCaption,12);
    }

    void DefaultBranchPlaybackSettings()
    {
        DefaultBranchOptionChooser();
        Section("自动播放遇到选择时","AUTO-PLAY CHOICES");
        var confirmBranch=new Switch(this)
        {
            Text="确认选后的对白，再继续播放",TextSize=16,
            Checked=session.Settings.AutoPlayConfirmBranch
        };
        PgrTheme.StyleSwitch(confirmBranch);
        confirmBranch.CheckedChange+=(_,e)=>{session.SetAutoPlayConfirmBranch(e.IsChecked);Render();};
        content.AddView(confirmBranch,new LinearLayout.LayoutParams(-1,-2));PlateSwitch(confirmBranch);
        if(session.Settings.AutoPlayConfirmBranch)
            Line("自动播放要直接使用上方默认分支，请先关闭此开关，下方会显示对应开关。",13);
        if(session.Settings.AutoPlayConfirmBranch)
        {
            FoldNote(content,"遇到选项时","HOW IT WORKS",new[]{
                "平时沿当前路线播放，遇到选项才停。先在游戏里选择，再展开顶部通知，对照原选项和选后对白，点对应卡片继续。汇合时不需要再次确认。",
                "从你确认的实际对白开始，不补读被跳过的选项或台词。选项首句相同时，原选项文字仍保留；长句可点全文查看。软件不替你点击游戏选项，也不猜测你选了哪一项。",
                "选错时可在悬浮“分支与菜单”里重选最近一次选项，或从台词列表重新定位。尚未核实的连接、缺音和人物互动仍停下说明原因。"});
            return;
        }
        Section("自动播放默认分支","DEFAULT BRANCH");
        var defaultBranch=new Switch(this)
        {
            Text="自动播放时使用默认分支",TextSize=16,
            Checked=session.Settings.AutoPlayDefaultBranchEnabled
        };
        PgrTheme.StyleSwitch(defaultBranch);
        defaultBranch.CheckedChange+=(_,e)=>
        {
            if(session.Settings.AutoPlayDefaultBranchEnabled==e.IsChecked)return;
            session.SetAutoPlayDefaultBranchEnabled(e.IsChecked);Render();
        };
        content.AddView(defaultBranch,new LinearLayout.LayoutParams(-1,-2));PlateSwitch(defaultBranch);
        Line("开启后，自动播放按设定的路线续播。请在游戏选择对应选项，软件不会替你点击游戏选项。关闭后恢复下方常规处理方式，所选偏好仍会保留。",13);
        FoldNote(content,"默认分支说明","DEFAULT BRANCH",new[]{
            "到分支时，软件直接按默认项配音，不再等待识别或要求手动续接。请你在游戏中点同一项；顶部显示“进入分支一／二：选项内容”，约3秒后收起。选错时可暂停并重新定位。",
            "悬浮台词始终标明当前分支，关闭默认分支也保留标识。内层选项汇合后仍显示父分支。开启默认分支时，已确认的支线可在暂停后核对当前句重新开启；当前路线保持，默认项只用于下一处选择。"});
        Line("此开关独立于下方设置，只联动游戏配音的自动播放，按上方默认分支（当前："+UiText.OptionOrdinal(session.Settings.DefaultBranchOption)+"）续播；只有一个选项时按第一个。人物互动、条件未明或尚未核实的路线仍会提示确认，不会换选另一条路线。",12);
    }

    /// <summary>08-B2 路线图卡: compact window (current ± 1, else first 3) + 「另有 N 个选择点 · 展开全部」; expand state remembered.
    /// Rows open the original 「定位游戏当前菜单」 confirmation; empty / linear states per SPEC.</summary>
    void RouteMapCard(LinearLayout parent,bool wide)
    {
        float density=Resources!.DisplayMetrics!.Density;
        var card=new LinearLayout(this){Orientation=Orientation.Vertical};
        card.Background=FusionDeco.Over(PgrTheme.Plate(this,PgrTheme.A(Color.White,226),PgrTheme.Border,PgrTheme.CutMedium,Corners.Diagonal,brackets:PgrTheme.A(PgrTheme.Red,200),shadow:true),
            new FusionDeco(density,DecoTier.M){CutTL=PgrTheme.CutMedium,CutBR=PgrTheme.CutMedium,InsetDp=3});card.Tag="fx";
        card.SetPadding(Dp(12),Dp(8),Dp(12),Dp(12));
        var e=session.Engine;
        var menus=e==null?new List<StoryMenuTarget>():e.GetStoryMenus(session.SectionId);
        bool expanded=wide||session.Settings.BranchRouteExpanded;
        // Head: icon + 人物、话题与分支 + ROUTE MAP + count + 展开 / 收起 tab.
        var head=new LinearLayout(this){Orientation=Orientation.Horizontal};head.SetGravity(GravityFlags.CenterVertical);head.SetMinimumHeight(Dp(44));
        head.AddView(Icon("branch",PgrTheme.Brass,18),new LinearLayout.LayoutParams(Dp(18),Dp(18)){RightMargin=Dp(8)});
        var title=new TextView(this){Text="人物、话题与分支",TextSize=15};title.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);title.SetTextColor(PgrTheme.Foreground);title.SetSingleLine(true);head.AddView(title);
        bool tight=PageWidthDp<420;
        if(!tight){var en=new TextView(this){Text="ROUTE MAP",TextSize=8.5f};en.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);en.LetterSpacing=.1f;en.SetTextColor(PgrTheme.Steel);en.SetPadding(Dp(6),Dp(3),0,0);head.AddView(en);}
        head.AddView(new View(this),new LinearLayout.LayoutParams(0,1,1));
        var count=new TextView(this){Text=e==null?"":menus.Count+" 个",TextSize=11};count.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);count.SetTextColor(PgrTheme.Muted);PgrTheme.Tabular(count);head.AddView(count);
        if(!wide&&menus.Count>3)
        {
            var tab=new TextView(this){Text=expanded?"⌃ 收起":"⌄ 展开",TextSize=12,Gravity=GravityFlags.Center,Clickable=true,Focusable=true};
            tab.SetTextColor(PgrTheme.OnPlate);tab.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);
            tab.Background=PgrTheme.Plate(this,PgrTheme.NamePlateLight,null,PgrTheme.CutSmall,Corners.TopLeft);
            tab.ContentDescription=expanded?"收起路线图":"展开全部选择点";
            tab.Click+=(_,_)=>Safe(()=>{session.Settings.BranchRouteExpanded=!session.Settings.BranchRouteExpanded;session.SaveSettings();Render();});
            var hit=new FrameLayout(this);hit.AddView(tab,new FrameLayout.LayoutParams(Dp(64),Dp(30),GravityFlags.Center));
            head.AddView(hit,new LinearLayout.LayoutParams(Dp(72),Dp(48)){LeftMargin=Dp(6)});
        }
        card.AddView(head);
        var rule=new View(this){Background=new HudRuleDrawable(density),ImportantForAccessibility=ImportantForAccessibility.No};
        card.AddView(rule,new LinearLayout.LayoutParams(-1,Dp(6)){BottomMargin=Dp(6)});
        if(e==null)
        {
            // 未导入章节: dashed rail placeholders + NeedPack sentence + NEW hint + 导入 ZIP / 前往章节页.
            for(int i=0;i<3;i++)
            {
                var ph=new LinearLayout(this){Orientation=Orientation.Horizontal,ImportantForAccessibility=ImportantForAccessibility.No};
                ph.AddView(new View(this){Background=new RouteNodeDrawable(density){Dashed=true,First=i==0,Last=i==2,Status="待核对",NodeTopDp=14}},new LinearLayout.LayoutParams(Dp(28),Dp(30)));
                ph.AddView(new View(this){Background=PgrTheme.Plate(this,PgrTheme.A(PgrTheme.Muted,30),null,2,Corners.None)},new LinearLayout.LayoutParams(0,Dp(8),i==1?.55f:.8f){TopMargin=Dp(10)});
                ph.AddView(new View(this),new LinearLayout.LayoutParams(0,1,i==1?.45f:.2f));
                card.AddView(ph);
            }
            var need=Text("请先在“章节”中导入并打开配音包。",15);need.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);need.SetPadding(0,Dp(10),0,Dp(2));card.AddView(need);
            var hint=Text("导入章节后，这里会显示本小节的路线图。",12);hint.SetTextColor(PgrTheme.Secondary);card.AddView(hint);
            var keys=new LinearLayout(this){Orientation=Orientation.Horizontal};
            var import=new Button(this){Text="导入 ZIP"};PgrUi.Style(import,Xb.Primary,"import",center:true,textSp:14);import.SetSingleLine(true);
            import.Click+=(_,_)=>Safe(()=>{if(importCancellation!=null)importCancellation.Cancel();else SelectFile(ImportRequest,"application/zip");});
            var go=new Button(this){Text="前往章节页"};PgrUi.Style(go,Xb.Normal,"chapters",center:true,textSp:14);go.SetSingleLine(true);go.Click+=(_,_)=>Safe(()=>ShowPage(0));
            foreach(var k in new[]{import,go}){k.SetMinWidth(0);k.SetMinimumWidth(0);DropIconWhenTight(k);}
            keys.AddView(import,new LinearLayout.LayoutParams(0,Dp(50),1){RightMargin=Dp(5)});keys.AddView(go,new LinearLayout.LayoutParams(0,Dp(50),1){LeftMargin=Dp(5)});
            card.AddView(keys,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(12)});
            parent.AddView(card,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(10)});
            return;
        }
        if(menus.Count==0)
        {
            // 已导入但本小节无选择点: straight line (crimson start → brass arrow) + LINEAR · NO CHOICE.
            var line=new LinearLayout(this){Orientation=Orientation.Horizontal,ImportantForAccessibility=ImportantForAccessibility.No};line.SetGravity(GravityFlags.CenterVertical);
            var start=new View(this){Background=new RouteNodeDrawable(density){IsCurrent=true,First=true,Last=true,NodeTopDp=12}};line.AddView(start,new LinearLayout.LayoutParams(Dp(28),Dp(24)));
            line.AddView(new View(this){Background=new ColorDrawable(PgrTheme.A(PgrTheme.Slate,150))},new LinearLayout.LayoutParams(0,Dp(1),1));
            var arrow=Text("▶",10);arrow.SetTextColor(PgrTheme.Brass);line.AddView(arrow);
            card.AddView(line);
            var tag=new TextView(this){Text="LINEAR · NO CHOICE",TextSize=9,Gravity=GravityFlags.End};tag.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);tag.LetterSpacing=.1f;tag.SetTextColor(PgrTheme.Steel);card.AddView(tag,new LinearLayout.LayoutParams(-1,-2));
            var none=Text("当前没有可选内容。",15);none.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);none.SetPadding(0,Dp(6),0,Dp(2));card.AddView(none);
            var linear=Text("本小节没有选择点，按台词顺序播放。",12);linear.SetTextColor(PgrTheme.Secondary);card.AddView(linear);
        }
        else
        {
            int currentIndex=menus.FindIndex(m=>m.IsCurrent);
            int start=0,take=menus.Count;
            if(!expanded&&menus.Count>3){take=3;start=currentIndex<0?0:Math.Clamp(currentIndex-1,0,menus.Count-3);}
            var shown=menus.Skip(start).Take(take).ToList();
            for(int i=0;i<shown.Count;i++)
            {
                var m=shown[i];var target=m;
                var row=new LinearLayout(this){Orientation=Orientation.Horizontal};
                bool lastRow=i==shown.Count-1&&(expanded||menus.Count<=3);
                row.AddView(new View(this){Background=new RouteNodeDrawable(density){Status=m.Status,IsCurrent=m.IsCurrent,First=i==0&&start==0,Last=lastRow,NodeTopDp=22},ImportantForAccessibility=ImportantForAccessibility.No},new LinearLayout.LayoutParams(Dp(28),-1));
                var body=new LinearLayout(this){Orientation=Orientation.Vertical,Clickable=true,Focusable=true};
                body.SetPadding(Dp(8),Dp(7),Dp(8),Dp(7));body.SetMinimumHeight(Dp(52));
                body.Background=m.IsCurrent?PgrTheme.Pressable(this,PgrTheme.Plate(this,PgrTheme.Selection,PgrTheme.SelectionLine,PgrTheme.CutSmall,Corners.Diagonal,PgrTheme.Red,AccentEdge.Left),PgrTheme.CutSmall,Corners.Diagonal,PgrTheme.RedRipple)
                    :PgrTheme.Pressable(this,new ColorDrawable(Color.Transparent),PgrTheme.CutSmall,Corners.Diagonal,PgrTheme.NeutralRipple);
                var top=new LinearLayout(this){Orientation=Orientation.Horizontal};top.SetGravity(GravityFlags.CenterVertical);
                var label=new TextView(this){Text=m.Label,TextSize=14};label.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);label.SetTextColor(m.IsCurrent?PgrTheme.RedText:PgrTheme.Foreground);label.SetMaxLines(tight?2:1);label.Ellipsize=TextUtils.TruncateAt.End;
                top.AddView(label,new LinearLayout.LayoutParams(0,-2,1));
                if(m.IsCurrent)top.AddView(PgrUi.Chip(this,"当前",PgrUi.Status.Current),new LinearLayout.LayoutParams(-2,-2){LeftMargin=Dp(6)});
                top.AddView(PgrUi.Chip(this,m.Status,m.Status switch{"返回已访问选择"=>PgrUi.Status.Brass,"已核实互动"=>PgrUi.Status.Teal,"仅定位选择点"=>PgrUi.Status.Steel,"先到所属选择点"=>PgrUi.Status.Alert,_=>PgrUi.Status.NotStarted}),new LinearLayout.LayoutParams(-2,-2){LeftMargin=Dp(6)});
                body.AddView(top);
                string optionsLine=m.Preview.Split('\n')[0];
                string takenLabel=e.Choices.TryGetValue(m.MenuId,out var takenPath)&&e.Pack.ById.TryGetValue(m.MenuId,out var menuNode)?menuNode.Options.FirstOrDefault(o=>o.PathId==takenPath)?.Label??"":"";
                var sub=new LinearLayout(this){Orientation=Orientation.Horizontal};sub.SetGravity(GravityFlags.CenterVertical);
                if(takenLabel.Length>0)
                {
                    var taken=new TextView(this){Text="◆ "+takenLabel,TextSize=12};taken.SetTextColor(PgrTheme.BrassText);taken.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);taken.SetSingleLine(true);sub.AddView(taken);
                    var others=string.Join(" ／ ",optionsLine.Split(" ／ ").Where(o=>o!=takenLabel));
                    if(others.Length>0){var rest=new TextView(this){Text=" ／ "+others,TextSize=12};rest.SetTextColor(PgrTheme.Secondary);rest.SetSingleLine(true);rest.Ellipsize=TextUtils.TruncateAt.End;sub.AddView(rest,new LinearLayout.LayoutParams(0,-2,1));}
                }
                else{var opts=new TextView(this){Text=optionsLine,TextSize=12};opts.SetTextColor(PgrTheme.Secondary);opts.SetSingleLine(true);opts.Ellipsize=TextUtils.TruncateAt.End;sub.AddView(opts,new LinearLayout.LayoutParams(0,-2,1));}
                body.AddView(sub,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(2)});
                body.ContentDescription=m.Label+"，"+m.Status+(m.IsCurrent?"，当前":"");
                body.Click+=(_,_)=>Safe(()=>Confirm("定位游戏当前菜单",target.Label+"\n"+target.Preview,()=>session.Command(x=>{if(!x.OpenGameMenu(target.MenuId,session.SectionId))Info("分支定位",x.NavigationError);})));
                row.AddView(body,new LinearLayout.LayoutParams(0,-2,1){BottomMargin=Dp(3)});
                card.AddView(row,new LinearLayout.LayoutParams(-1,-2));
            }
            int hidden=menus.Count-shown.Count;
            if(hidden>0)
            {
                var more=new LinearLayout(this){Orientation=Orientation.Horizontal};
                more.AddView(new View(this){Background=new RouteNodeDrawable(density){Dashed=true,Last=true,Status="待核对",NodeTopDp=24},ImportantForAccessibility=ImportantForAccessibility.No},new LinearLayout.LayoutParams(Dp(28),-1));
                var bar=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true};bar.SetGravity(GravityFlags.CenterVertical);bar.SetMinimumHeight(Dp(48));bar.SetPadding(Dp(10),0,Dp(8),0);
                bar.Background=PgrTheme.Pressable(this,PgrTheme.Plate(this,PgrTheme.A(PgrTheme.Slate,22),PgrTheme.A(PgrTheme.Slate,90),PgrTheme.CutSmall,Corners.Diagonal),PgrTheme.CutSmall,Corners.Diagonal,PgrTheme.NeutralRipple);
                var label=new TextView(this){Text=$"另有 {hidden} 个选择点 · 展开全部",TextSize=13};label.SetTextColor(PgrTheme.Steel);label.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);label.SetSingleLine(true);label.Ellipsize=TextUtils.TruncateAt.End;
                bar.AddView(label,new LinearLayout.LayoutParams(0,-2,1));
                var rest=menus.Where((_,k)=>k<start||k>=start+take).GroupBy(x=>x.Status);
                foreach(var g in rest)
                {
                    string mark=g.Key switch{"返回已访问选择"=>"◆","已核实互动"=>"◎","仅定位选择点"=>"◇","先到所属选择点"=>"!",_=>"◌"};
                    var c=new TextView(this){Text=mark+g.Count(),TextSize=11};c.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);c.SetPadding(Dp(5),0,0,0);
                    c.SetTextColor(g.Key switch{"返回已访问选择"=>PgrTheme.BrassText,"已核实互动"=>PgrTheme.TealText,"先到所属选择点"=>PgrTheme.RedText,"仅定位选择点"=>PgrTheme.Steel,_=>PgrTheme.Muted});bar.AddView(c);
                }
                bar.ContentDescription=$"另有 {hidden} 个选择点，展开全部";
                bar.Click+=(_,_)=>Safe(()=>{session.Settings.BranchRouteExpanded=true;session.SaveSettings();Render();});
                more.AddView(bar,new LinearLayout.LayoutParams(0,-2,1){TopMargin=Dp(2)});
                card.AddView(more,new LinearLayout.LayoutParams(-1,-2));
            }
        }
        var nav=new LinearLayout(this){Orientation=wide&&PageWidthDp<760?Orientation.Vertical:Orientation.Horizontal};
        var back=new Button(this){Text="返回上级菜单"};PgrUi.Style(back,Xb.Normal,"back",center:true,textSp:13);back.Click+=(_,_)=>Safe(ReturnToParentMenu);
        var redo=new Button(this){Text="重选最近一次分支"};PgrUi.Style(redo,Xb.Normal,"replay",center:true,textSp:13);
        redo.Click+=(_,_)=>Safe(()=>session.Command(x=>{if(!x.ReselectLastChoice())Info("重选分支",x.NavigationError);}));
        foreach(var k in new[]{back,redo}){k.SetSingleLine(true);k.SetMinWidth(0);k.SetMinimumWidth(0);DropIconWhenTight(k);}
        if(nav.Orientation==Orientation.Horizontal){nav.AddView(back,new LinearLayout.LayoutParams(0,Dp(50),1){RightMargin=Dp(5)});nav.AddView(redo,new LinearLayout.LayoutParams(0,Dp(50),1){LeftMargin=Dp(5)});}
        else{nav.AddView(back,new LinearLayout.LayoutParams(-1,Dp(50)){BottomMargin=Dp(6)});nav.AddView(redo,new LinearLayout.LayoutParams(-1,Dp(50)));}
        card.AddView(nav,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(10)});
        parent.AddView(card,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(10)});
    }
}
