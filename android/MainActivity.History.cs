using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 09-B 历史页: cover header 「剧情记录 · LOG」, segment tabs 书签 / 最近分支 / 台词履历 (remembered, default 台词履历;
/// wide landscape = 150dp vertical tabs), bookmark cards, brass route timeline, 05-B script rail for the line log.
/// All actions and dialogs are the original ones.
/// </summary>
public sealed partial class MainActivity
{
    void History()
    {
        if(session.Engine==null){HistoryEmpty();return;}
        var e=session.Engine!;float density=Resources!.DisplayMetrics!.Density;
        CoverHeader("剧情记录 · LOG","书签与履历","LOG");
        var bookmarks=session.Progress.Bookmarks(e.Pack.Id).ToList();
        var recent=e.RecentChoices.Take(15).ToList();
        int tab=Math.Clamp(session.Settings.HistoryTab,0,2);
        void PickTab(int i){session.Settings.HistoryTab=i;session.SaveSettings();Render();}
        bool wide=WideLandscape&&PageWidthDp>=560;
        var tabs=SegmentTabs(new[]{"书签","最近分支","台词履历"},new[]{"BOOKMARKS","BRANCHES","LINE LOG"},new[]{bookmarks.Count,recent.Count,e.History.Count},tab,PickTab,wide);
        string countLine=$"已记录 {e.History.Count} 条履历，随时回到熟悉的故事。";
        if(wide){var left=SideColumns(150,10);left.AddView(tabs);var note=Text(countLine,11);note.SetTextColor(PgrTheme.Secondary);note.SetPadding(Dp(2),Dp(8),0,0);left.AddView(note);}
        else{Line(countLine,13);content.AddView(tabs,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(6),BottomMargin=Dp(6)});}
        if(tab==0)
        {
            ButtonIcon(Button(content,"为当前位置保存书签",AddBookmark,primary:true),"bookmark",true);
            foreach(var mark in bookmarks)
            {
                var card=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true};card.SetGravity(GravityFlags.CenterVertical);
                card.SetPadding(Dp(12),Dp(10),Dp(10),Dp(10));card.SetMinimumHeight(Dp(64));card.Background=PgrTheme.PressableCard(this);
                string who=string.IsNullOrWhiteSpace(mark.Speaker)?"旁白":mark.Speaker;
                card.AddView(SpeakerAvatar(who,40,true),new LinearLayout.LayoutParams(Dp(40),Dp(40)){RightMargin=Dp(10)});
                var body=new LinearLayout(this){Orientation=Orientation.Vertical};
                var label=new TextView(this){Text=mark.Label,TextSize=14};label.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);label.SetTextColor(PgrTheme.Foreground);label.SetSingleLine(true);label.Ellipsize=TextUtils.TruncateAt.End;body.AddView(label);
                var meta=new TextView(this){Text=mark.SectionTitle+"  ·  "+mark.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm"),TextSize=11};meta.SetTextColor(PgrTheme.Steel);meta.SetSingleLine(true);meta.Ellipsize=TextUtils.TruncateAt.End;PgrTheme.Tabular(meta);body.AddView(meta);
                if(mark.Text.Length>0){var line=new TextView(this){Text=(string.IsNullOrWhiteSpace(mark.Speaker)?"":mark.Speaker+"：")+mark.Text,TextSize=12.5f};line.SetTextColor(PgrTheme.Secondary);line.SetMaxLines(2);line.Ellipsize=TextUtils.TruncateAt.End;line.SetPadding(0,Dp(3),0,0);body.AddView(line);}
                card.AddView(body,new LinearLayout.LayoutParams(0,-2,1));
                card.AddView(Icon("chevron_right",PgrTheme.Secondary,14),new LinearLayout.LayoutParams(Dp(14),Dp(14)){LeftMargin=Dp(6)});
                card.ContentDescription=mark.Label+"，"+mark.SectionTitle;
                card.Click+=(_,_)=>Safe(()=>Choose("书签："+mark.Label,new[]{"恢复到这里（静音）","删除书签"},i=>
                {if(i==0)session.Command(x=>{if(!x.RestoreBookmark(mark))Info("恢复失败",x.NavigationError);});else Confirm("删除书签",mark.Label,()=>{session.Progress.RemoveBookmark(e.Pack.Id,mark.Id);Render();});}));
                content.AddView(card,new LinearLayout.LayoutParams(-1,-2){TopMargin=Dp(4),BottomMargin=Dp(4)});
            }
        }
        else if(tab==1)
        {
            Section("最近分支","RECENT BRANCHES");
            var hint=Text("最近 15 次以内的选择，点按后核对再重选。",12);hint.SetTextColor(PgrTheme.Secondary);content.AddView(hint);
            for(int i=0;i<recent.Count;i++)
            {
                var choice=recent[i];
                var row=new LinearLayout(this){Orientation=Orientation.Horizontal};
                row.AddView(new View(this){Background=new RouteNodeDrawable(density){Status=i==0?"返回已访问选择":"brass_open",First=i==0,Last=i==recent.Count-1,NodeTopDp=24},ImportantForAccessibility=ImportantForAccessibility.No},new LinearLayout.LayoutParams(Dp(28),-1));
                var body=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true};body.SetGravity(GravityFlags.CenterVertical);
                body.SetPadding(Dp(12),Dp(8),Dp(10),Dp(8));body.SetMinimumHeight(Dp(52));
                body.Background=PgrTheme.Pressable(this,FusionDeco.Over(PgrTheme.Plate(this,i==0?PgrTheme.A(PgrTheme.Brass,26):PgrTheme.A(Color.White,214),i==0?PgrTheme.A(PgrTheme.Brass,150):PgrTheme.Border,PgrTheme.CutSmall,Corners.Diagonal,i==0?PgrTheme.Brass:null,AccentEdge.Left), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall, Paper = false }),PgrTheme.CutSmall,Corners.Diagonal,PgrTheme.NeutralRipple);
                var label=new TextView(this){Text=choice.Label,TextSize=14};label.SetTextColor(PgrTheme.Foreground);label.SetTypeface(PgrTheme.SansBold,i==0?TypefaceStyle.Bold:TypefaceStyle.Normal);label.SetMaxLines(2);label.Ellipsize=TextUtils.TruncateAt.End;
                body.AddView(label,new LinearLayout.LayoutParams(0,-2,1));
                if(i==0){var latest=new TextView(this){Text="LATEST",TextSize=9};latest.SetTypeface(PgrTheme.Condensed,TypefaceStyle.Bold);latest.LetterSpacing=.1f;latest.SetTextColor(PgrTheme.BrassText);latest.SetPadding(Dp(6),0,Dp(4),0);body.AddView(latest);}
                body.AddView(Icon("branch",PgrTheme.Brass,14),new LinearLayout.LayoutParams(Dp(14),Dp(14)){LeftMargin=Dp(4)});
                body.ContentDescription=choice.Label;
                body.Click+=(_,_)=>Safe(()=>Confirm("重新选择分支",choice.Label,()=>session.Command(x=>{if(!x.ReselectChoice(choice.Sequence))Info("重选失败",x.NavigationError);})));
                row.AddView(body,new LinearLayout.LayoutParams(0,-2,1){BottomMargin=Dp(5)});
                content.AddView(row,new LinearLayout.LayoutParams(-1,-2){TopMargin=i==0?Dp(8):0});
            }
        }
        else
        {
            Section("台词履历","LINE LOG");
            int count=e.History.Count;historyPage=Math.Clamp(historyPage,0,Math.Max(0,(count-1)/40));
            var indexes=Enumerable.Range(0,count).Reverse().Skip(historyPage*40).Take(40).ToList();
            bool narrowScript=WideLandscape&&Resources!.Configuration!.ScreenWidthDp<820;int av=narrowScript?28:36;
            for(int k=0;k<indexes.Count;k++)
            {
                int index=indexes[k];
                var visit=e.History[index];var node=e.Pack.ById[visit.NodeId];
                bool current=index==e.HistoryPosition;
                string speakerName=string.IsNullOrWhiteSpace(node.Speaker)?"旁白":node.Speaker.Trim();
                var row=new LinearLayout(this){Orientation=Orientation.Horizontal};
                var rail=new LinearLayout(this){Orientation=Orientation.Vertical,ImportantForAccessibility=ImportantForAccessibility.No};
                rail.Background=new RailNodeDrawable(density){IsCurrent=current,First=k==0&&historyPage==0,Last=k==indexes.Count-1&&(historyPage+1)*40>=count,NodeTopDp=18};
                var marker=PgrTheme.Apply(Text((index+1).ToString(),10),PgrTheme.Role.Code,current?PgrTheme.RedText:PgrTheme.Muted);marker.Gravity=GravityFlags.CenterHorizontal;
                rail.AddView(marker,new LinearLayout.LayoutParams(Dp(narrowScript?24:30),-2){TopMargin=Dp(28)});
                row.AddView(rail,new LinearLayout.LayoutParams(Dp(narrowScript?26:32),-1){RightMargin=Dp(narrowScript?2:4)});
                var line=new LinearLayout(this){Orientation=Orientation.Horizontal,Clickable=true,Focusable=true};line.SetPadding(Dp(narrowScript?8:12),Dp(10),Dp(narrowScript?8:12),Dp(11));
                line.Background=PgrTheme.PressableCard(this,current);
                line.AddView(SpeakerAvatar(speakerName,av,true),new LinearLayout.LayoutParams(Dp(av),Dp(av)){RightMargin=Dp(narrowScript?6:10),TopMargin=Dp(1)});
                var body=new LinearLayout(this){Orientation=Orientation.Vertical};
                var nameRow=new LinearLayout(this){Orientation=Orientation.Horizontal};nameRow.SetGravity(GravityFlags.CenterVertical);
                var speaker=Text(speakerName,12);speaker.SetTextColor(current?PgrTheme.RedText:PgrTheme.Secondary);speaker.SetTypeface(PgrTheme.SansBold,TypefaceStyle.Bold);speaker.SetPadding(0,0,0,Dp(3));nameRow.AddView(speaker);
                if(current)nameRow.AddView(PgrUi.Chip(this,"当前",PgrUi.Status.Current),new LinearLayout.LayoutParams(-2,-2){LeftMargin=Dp(8),BottomMargin=Dp(2)});
                body.AddView(nameRow);
                var dialogue=Text(node.Text,15);dialogue.SetLineSpacing(Dp(4),1);body.AddView(dialogue);
                line.AddView(body,new LinearLayout.LayoutParams(0,-2,1));
                line.ContentDescription=$"{index+1}. {AppSession.BrowseSpeaker(node)}：{node.Text}";
                line.Click+=(_,_)=>Safe(()=>Choose("台词履历",new[]{"试听（不改变进度）","恢复到这里（静音）","反馈这句"},i=>
                {if(i==0)session.PreviewNode(node);else if(i==1)session.Command(x=>{if(!x.RestoreVisit(index,false))Info("恢复失败",x.NavigationError);});else BeginLineFeedback(e.Pack,node,"游戏历史",RecordedFeedbackContext(e,index));}));
                row.AddView(line,new LinearLayout.LayoutParams(0,-2,1){BottomMargin=Dp(6)});
                content.AddView(row,new LinearLayout.LayoutParams(-1,-2));
            }
            var pager=Row(content);
            if(historyPage>0)ButtonIcon(Button(pager,"较新记录",()=>{historyPage--;Render();}),"chevron_up");
            if((historyPage+1)*40<count)ButtonIcon(Button(pager,"更早记录",()=>{historyPage++;Render();}),"chevron_down");
        }
        PageFooter();
    }
}
