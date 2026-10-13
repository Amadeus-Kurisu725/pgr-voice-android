using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp;

/// <summary>
/// Shared 08–10 page pieces: M-tier cover header (chapter cover under a dark veil, eyebrow, title, outline glyph),
/// 08-B2 fold notes (48dp row: title + first sentence + 「展开说明」; body = original text), 09-B / 10-B segment tabs
/// (dark S strip in portrait, 150dp vertical tabs in wide landscape), switch / radio plates and the archive footer.
/// </summary>
public sealed partial class MainActivity
{
    readonly HashSet<string> openFoldNotes = new();

    /// <summary>Cover header band; returns the text column so a page can append keys under the title.</summary>
    LinearLayout CoverHeader(string code, string title, string? outline, Drawable? glyph = null)
    {
        float density = Resources!.DisplayMetrics!.Density;
        bool narrow = NarrowPage || PageWidthDp < 340;
        var band = new FrameLayout(this);
        var face = new CoverBandDrawable(density, DecoTier.M) { VeilAlpha = 215, CutTopRightDp = 16 };
        band.Background = face;
        if (session.Engine is { } e) LoadChapterCover(band, image => face.Cover = image, e.Pack.Title, e.Pack.Id, Dp(560));
        if (glyph != null)
            band.AddView(new View(this) { Background = glyph, ImportantForAccessibility = ImportantForAccessibility.No },
                new FrameLayout.LayoutParams(Dp(narrow ? 96 : 132), Dp(narrow ? 56 : 72), GravityFlags.End | GravityFlags.CenterVertical) { RightMargin = Dp(12) });
        else if (!string.IsNullOrEmpty(outline))
            band.AddView(PgrUi.Outline(this, outline, narrow ? 34 : 52, PgrTheme.A(Color.White, narrow ? 150 : 210), 1.3f), new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.Top) { RightMargin = Dp(14), TopMargin = Dp(6) });
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        col.SetPadding(Dp(14), Dp(12), Dp(12), Dp(16));
        var eyebrow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; eyebrow.SetGravity(GravityFlags.CenterVertical);
        eyebrow.AddView(new View(this) { Background = PgrTheme.Triangle(this), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(Dp(6), Dp(8)) { RightMargin = Dp(7) });
        var codeView = new TextView(this) { Text = code, TextSize = 11 }; codeView.SetTextColor(PgrTheme.NavText); codeView.LetterSpacing = .06f; eyebrow.AddView(codeView);
        col.AddView(eyebrow);
        var titleView = new TextView(this) { Text = title, TextSize = narrow ? 19 : CompactLayout ? 20 : 24 }; titleView.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); titleView.SetTextColor(Color.White);
        col.AddView(titleView, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(2), RightMargin = Dp(glyph != null ? 100 : 0) });
        band.AddView(col, new FrameLayout.LayoutParams(-1, -2));
        content.AddView(band, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
        return col;
    }

    /// <summary>08-B2 fold: long original explanation collapses to a 48dp row (title + first sentence + 展开说明).</summary>
    void FoldNote(LinearLayout parent, string title, string? english, string[] paragraphs, View? extra = null)
    {
        string key = page + ":" + title;
        bool open = openFoldNotes.Contains(key);
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.White, 214), PgrTheme.Border, PgrTheme.CutSmall, Corners.Diagonal), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall });
        card.SetPadding(Dp(10), 0, Dp(6), Dp(2));
        var head = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true }; head.SetGravity(GravityFlags.CenterVertical);
        head.SetMinimumHeight(Dp(52));
        head.AddView(Icon("story", PgrTheme.Steel, 16), new LinearLayout.LayoutParams(Dp(16), Dp(16)) { RightMargin = Dp(8) });
        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var titleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal }; titleRow.SetGravity(GravityFlags.Bottom);
        var t = new TextView(this) { Text = title, TextSize = 13.5f }; t.SetTextColor(PgrTheme.Foreground); t.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); t.SetSingleLine(true); t.Ellipsize = TextUtils.TruncateAt.End;
        titleRow.AddView(t, new LinearLayout.LayoutParams(-2, -2) { Weight = 0 });
        if (!string.IsNullOrEmpty(english) && !NarrowPage)
        { var en = new TextView(this) { Text = english, TextSize = 8.5f }; en.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); en.SetTextColor(PgrTheme.Steel); en.LetterSpacing = .08f; en.SetSingleLine(true); en.SetPadding(Dp(6), 0, 0, Dp(2)); titleRow.AddView(en); }
        texts.AddView(titleRow);
        string first = paragraphs.Length > 0 ? paragraphs[0] : "";
        var lead = new TextView(this) { Text = first, TextSize = 11.5f }; lead.SetTextColor(PgrTheme.Secondary); lead.SetSingleLine(true); lead.Ellipsize = TextUtils.TruncateAt.End;
        texts.AddView(lead);
        head.AddView(texts, new LinearLayout.LayoutParams(0, -2, 1));
        var toggle = new TextView(this) { TextSize = 11, Gravity = GravityFlags.Center }; toggle.SetTextColor(PgrTheme.OnPlate); toggle.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold);
        toggle.Background = PgrTheme.Plate(this, PgrTheme.NamePlateLight, null, PgrTheme.CutSmall, Corners.TopLeft); toggle.SetPadding(Dp(10), 0, Dp(10), 0);
        head.AddView(toggle, new LinearLayout.LayoutParams(-2, Dp(28)) { LeftMargin = Dp(8) });
        card.AddView(head);
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        body.SetPadding(Dp(24), 0, Dp(6), Dp(10));
        if (extra != null) body.AddView(extra);
        else foreach (var p in paragraphs) { var v = Text(p, 13); v.SetTextColor(PgrTheme.Secondary); v.SetPadding(0, Dp(2), 0, Dp(4)); body.AddView(v); }
        card.AddView(body);
        void Apply()
        {
            bool o = openFoldNotes.Contains(key);
            body.Visibility = o ? ViewStates.Visible : ViewStates.Gone; lead.Visibility = o ? ViewStates.Gone : ViewStates.Visible;
            toggle.Text = o ? "收起 ⌃" : "展开说明 ⌄";
            head.ContentDescription = title + (o ? "，收起" : "，展开说明");
        }
        head.Click += (_, _) => Safe(() => { if (!openFoldNotes.Remove(key)) openFoldNotes.Add(key); Apply(); });
        Apply();
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(6) });
    }

    /// <summary>Wide landscape two-column split inside the page; the right column becomes <c>content</c>.</summary>
    LinearLayout SideColumns(int leftDp, int gapDp = 12)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var left = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var right = new LinearLayout(this) { Orientation = Orientation.Vertical };
        row.AddView(left, new LinearLayout.LayoutParams(Dp(leftDp), -2) { RightMargin = Dp(gapDp) });
        row.AddView(right, new LinearLayout.LayoutParams(0, -2, 1));
        content.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        content = right;
        return left;
    }

    /// <summary>09-B / 10-B segment tabs: dark S strip (portrait) or 150dp vertical tabs (wide landscape). Selected = paper + crimson bar.</summary>
    View SegmentTabs(string[] zh, string[] en, int[]? counts, int selected, Action<int> pick, bool vertical)
    {
        float density = Resources!.DisplayMetrics!.Density;
        var strip = new LinearLayout(this) { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal };
        strip.Background = new DarkPanelDrawable(density, DecoTier.S) { CutTopRightDp = 12, ShowTide = false };
        strip.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        for (int i = 0; i < zh.Length; i++)
        {
            int index = i; bool on = i == selected;
            var tab = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true, Selected = on };
            tab.SetGravity(vertical ? GravityFlags.CenterVertical | GravityFlags.Start : GravityFlags.Center);
            tab.SetPadding(Dp(vertical ? 12 : 4), Dp(4), Dp(4), Dp(4));
            tab.Background = on
                ? PgrTheme.Plate(this, PgrTheme.TabSelected, null, PgrTheme.CutSmall, Corners.TopRight, PgrTheme.Red, vertical ? AccentEdge.Left : AccentEdge.Bottom, vertical ? 0 : 28)
                : new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(Color.White, 40)), null, new ColorDrawable(Color.White));
            var top = new LinearLayout(this) { Orientation = Orientation.Horizontal }; top.SetGravity(GravityFlags.Bottom | (vertical ? GravityFlags.Start : GravityFlags.CenterHorizontal));
            var name = new ShortFitText(this, zh[i], zh[i].Split(" · ")[0]) { TextSize = 13.5f }; name.SetTypeface(PgrTheme.SansBold, TypefaceStyle.Bold); name.SetSingleLine(true);
            name.SetTextColor(on ? PgrTheme.Foreground : PgrTheme.OnPlate); top.AddView(name);
            if (counts != null)
            { var c = new TextView(this) { Text = counts[i].ToString(), TextSize = 11 }; c.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); c.SetTextColor(on ? PgrTheme.RedText : PgrTheme.NavSub); c.SetPadding(Dp(5), 0, 0, Dp(1)); PgrTheme.Tabular(c); top.AddView(c); }
            tab.AddView(top);
            var sub = new TextView(this) { Text = en[i], TextSize = 8.5f }; sub.SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); sub.LetterSpacing = .08f; sub.SetSingleLine(true);
            sub.SetTextColor(on ? PgrTheme.RedText : PgrTheme.NavSub); sub.Gravity = vertical ? GravityFlags.Start : GravityFlags.CenterHorizontal;
            tab.AddView(sub, new LinearLayout.LayoutParams(-1, -2));
            tab.ContentDescription = zh[i] + (counts != null ? " " + counts[i] : "") + (on ? "，已选中" : "");
            tab.Click += (_, _) => Safe(() => { if (index != selected) pick(index); });
            if (vertical) strip.AddView(tab, new LinearLayout.LayoutParams(-1, Dp(56)) { BottomMargin = Dp(2) });
            else strip.AddView(tab, new LinearLayout.LayoutParams(0, Dp(52), 1) { LeftMargin = Dp(1), RightMargin = Dp(1) });
        }
        return strip;
    }

    /// <summary>Chamfered switch row (56dp, white plate, crimson lead when on).</summary>
    void PlateSwitch(Switch s, int minDp = 56)
    {
        void Paint() => s.Background = FusionDeco.Over(PgrTheme.Plate(this, PgrTheme.A(Color.White, 230), PgrTheme.Border, PgrTheme.CutSmall, Corners.Diagonal, s.Checked ? PgrTheme.Red : PgrTheme.BorderStrong, AccentEdge.Left), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall, Paper = false });
        ChamferSwitch(s);
        Paint(); s.SetMinimumHeight(Dp(minDp)); s.SetPadding(Dp(14), 0, Dp(10), 0);
        s.CheckedChange += (_, _) => Paint();
        if (s.LayoutParameters is LinearLayout.LayoutParams lp) { lp.TopMargin = Dp(4); lp.BottomMargin = Dp(6); s.LayoutParameters = lp; }
    }

    /// <summary>Radio row plate (52dp).</summary>
    void PlateRadio(RadioButton r)
    {
        void Paint() => r.Background = FusionDeco.Over(PgrTheme.Plate(this, r.Checked ? PgrTheme.Selection : PgrTheme.A(Color.White, 214), r.Checked ? PgrTheme.SelectionLine : PgrTheme.Border, PgrTheme.CutSmall, Corners.Diagonal, r.Checked ? PgrTheme.Red : null, AccentEdge.Left), new FusionDeco(this.Resources!.DisplayMetrics!.Density, DecoTier.S) { CutTL = PgrTheme.CutSmall, CutBR = PgrTheme.CutSmall, Paper = false });
        Paint(); r.SetMinimumHeight(Dp(52)); r.SetPadding(Dp(8), 0, Dp(10), 0);
        r.CheckedChange += (_, _) => Paint();
    }

    void PageFooter() =>
        content.AddView(new View(this) { Background = new ArchiveFooterDrawable(Resources!.DisplayMetrics!.Density), ImportantForAccessibility = ImportantForAccessibility.No }, new LinearLayout.LayoutParams(-1, Dp(120)) { TopMargin = Dp(8) });

    static string FirstSentence(string text)
    {
        int i = text.IndexOf('。'); return i > 0 && i < text.Length - 1 ? text[..(i + 1)] : text;
    }
}
