using Android.Graphics;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 07 定位 / 09 历史 with no chapter open: page cover header + an empty-state card in the 08-B2 language (head row with icon,
/// HUD rule, dashed placeholder, NeedPack sentence + short hint, 导入 ZIP / 前往章节页). The tray is the slim dark one, fixed collapsed.
/// </summary>
public sealed partial class MainActivity
{
    void LocateEmpty()
    {
        CoverHeader("字幕识别 · LOCATE / OCR", "定位当前台词", "OCR");
        EmptyPackCard(content, "locate", "字幕识别", "OCR", "导入章节后，可在这里识别游戏字幕并定位当前台词。", scan: true);
        PageFooter();
    }

    void HistoryEmpty()
    {
        CoverHeader("剧情记录 · LOG", "书签与履历", "LOG");
        EmptyPackCard(content, "history", "台词履历", "LINE LOG", "导入章节后，这里会显示书签、最近分支和台词履历。", scan: false);
        PageFooter();
    }

    void EmptyPackCard(LinearLayout parent, string icon, string title, string en, string hint, bool scan)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var card = new CapLinearLayout(this) { Orientation = Orientation.Vertical };
        // 0.4.3 F-B L empty card: left ruler + NO DATA, ORBIT 00 / NO SIGNAL, R·096 corner, fused TIDE · NULL badge above the keys.
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.White, 226), PgrTheme.Border, PgrTheme.CutMedium, Corners.Diagonal, brackets: PgrTheme.A(PgrTheme.Red, 200), shadow: true),
            new FusionDeco(density, DecoTier.L, FxKind.Empty) { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium, InsetDp = 3 });
        card.Tag = "fx";
        card.SetPadding(Dp(22), Dp(10), Dp(16), Dp(18));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal }; head.SetGravity(GravityFlags.CenterVertical); head.SetMinimumHeight(Dp(44));
        head.AddView(Icon(icon, PgrTheme.Brass, 18), new LinearLayout.LayoutParams(Dp(18), Dp(18)) { RightMargin = Dp(8) });
        var name = new TextView(this) { Text = title, TextSize = 15 }; name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); name.SetTextColor(PgrTheme.Foreground); name.SetSingleLine(true); head.AddView(name);
        var code = new TextView(this) { Text = en, TextSize = 8.5f }; code.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); code.LetterSpacing = .1f; code.SetTextColor(PgrTheme.Steel); code.SetPadding(Dp(6), Dp(3), 0, 0); head.AddView(code);
        card.AddView(head);
        card.AddView(new View(this) { Background = new HudRuleDrawable(density), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(-1, Dp(6)) { BottomMargin = Dp(6) });
        if (scan)
            card.AddView(new View(this) { Background = new EmptyScanDrawable(density), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(-1, Dp(92)));
        else
            for (int i = 0; i < 3; i++)
            {
                var ph = new LinearLayout(this) { Orientation = Orientation.Horizontal, ImportantForAccessibility = ImportantForAccessibility.No };
                ph.AddView(new View(this) { Background = new RouteNodeDrawable(density) { Dashed = true, First = i == 0, Last = i == 2, Status = "待核对", NodeTopDp = 14 } }, new LinearLayout.LayoutParams(Dp(28), Dp(30)));
                ph.AddView(new View(this) { Background = PgrTheme.Plate(this, PgrTheme.A(PgrTheme.Muted, 30), null, 2, Corners.None) }, new LinearLayout.LayoutParams(0, Dp(8), i == 1 ? .55f : .8f) { TopMargin = Dp(10) });
                ph.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, i == 1 ? .45f : .2f));
                card.AddView(ph);
            }
        var need = Text("请先在“章节”中导入并打开配音包。", 15); need.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); need.SetPadding(0, Dp(10), 0, Dp(2)); card.AddView(need);
        var note = Text(hint, 12); note.SetTextColor(PgrTheme.Secondary); card.AddView(note);
        var keys = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var import = new Button(this) { Text = "导入 ZIP" }; PgrUi.Style(import, Xb.Primary, "import", center: true, textSp: 14); import.SetSingleLine(true);
        import.Click += (_, _) => Safe(() => { if (importCancellation != null) importCancellation.Cancel(); else SelectFile(ImportRequest, "application/zip"); });
        var go = new Button(this) { Text = "前往章节页" }; PgrUi.Style(go, Xb.Normal, "chapters", center: true, textSp: 14).Deco = null; go.SetSingleLine(true); go.Click += (_, _) => Safe(() => ShowPage(0));
        foreach (var k in new[] { import, go }) { k.SetMinWidth(0); k.SetMinimumWidth(0); DropIconWhenTight(k); }
        keys.AddView(import, new LinearLayout.LayoutParams(0, Dp(50), 1) { RightMargin = Dp(5) }); keys.AddView(go, new LinearLayout.LayoutParams(0, Dp(50), 1) { LeftMargin = Dp(5) });
        card.AddView(keys, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(54) });
        // Wide landscape keeps the card at a readable width instead of stretching across the page column.
        // Capped at measure time against the real parent column (a fixed 560dp could overflow a narrower side column / small landscape).
        if (WideLandscape) card.MaxWidthPx = Dp(560);
        var lp = new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6), BottomMargin = Dp(10) };
        parent.AddView(card, lp);
    }
}
