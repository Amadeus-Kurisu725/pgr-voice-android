using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

/// <summary>0.4.3 F-B light-surface deco wiring: dynamic serials (CUR / REF / ARC) and the 「inside a deco card」 rule
/// (buttons inside an M / L card stay plain; standalone light buttons get the S corner + ruler).</summary>
public sealed partial class MainActivity
{
    FusionDeco? currentDeco; string currentDecoKey = "";

    /// <summary>Current line index (0-based, −1 = none) and line count of the current section.</summary>
    (int index, int total) SectionProgress()
    {
        var e = session.Engine; if (e == null) return (-1, 0);
        var lines = e.Pack.Nodes.Where(n => !n.Archived && n.Kind == "line" && n.SectionId == session.SectionId).ToList();
        return (lines.FindIndex(n => n.Id == e.CurrentId), lines.Count);
    }

    void UpdateCurrentDeco()
    {
        var e = session.Engine; if (currentDeco == null) return;
        string key = e == null ? "" : e.Pack.Id + ":" + session.SectionId + ":" + e.CurrentId;
        if (key == currentDecoKey) return; currentDecoKey = key;
        var (i, t) = SectionProgress();
        currentDeco.Code = t > 0 ? "CUR · " + (i >= 0 ? (i + 1).ToString("000") : "000") + "/" + t.ToString("000") : null;
        currentDeco.Invalidate();
    }

    static bool InDecoCard(View? v)
    {
        for (int k = 0; v != null && k < 5; k++, v = v.Parent as View) if (v.Tag?.ToString() == "fx") return true;
        return false;
    }

    /// <summary>S deco (crimson corner + short slate ruler + brass node) on a standalone light StyleButton.</summary>
    void DecoButton(Button b, LinearLayout parent)
    {
        if (InDecoCard(parent)) return;
        Fx.Under(b, new FusionDeco(Resources!.DisplayMetrics!.Density, DecoTier.S) { CutBR = PgrTheme.CutSmall, Paper = false }, PgrTheme.NeutralRipple);
    }

    /// <summary>Switch row inside a deco card: plain 52dp row (no own plate), as in the F-B 10 mockup.</summary>
    /// <summary>SPEC 切角开关: chamfered 42×22dp track + diamond thumb (replaces the stock Material switch look).</summary>
    void ChamferSwitch(Switch s)
    {
        float den = Resources!.DisplayMetrics!.Density;
        s.ThumbTintList = null; s.TrackTintList = null; s.ThumbDrawable = new DiamondSwitchThumb(den); s.TrackDrawable = new ChamferSwitchTrack(den);
        s.SwitchMinWidth = Dp(42); s.ThumbTextPadding = 0; s.SwitchPadding = Dp(12); s.RefreshDrawableState();
    }
    void CardSwitchRow(Switch s) { ChamferSwitch(s); s.SetMinimumHeight(Dp(52)); s.SetPadding(Dp(2), 0, Dp(2), 0);
        s.Background = new global::Android.Graphics.Drawables.RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.NeutralRipple), null, new global::Android.Graphics.Drawables.ColorDrawable(global::Android.Graphics.Color.White)); }

    /// <summary>L empty-state deco (NO DATA, ORBIT 00 / NO SIGNAL, TIDE · NULL badge) over a Card() plate.</summary>
    void EmptyDeco(LinearLayout card)
    {
        card.Background = FusionDeco.Over(PgrTheme.Card(this), new FusionDeco(Resources!.DisplayMetrics!.Density, DecoTier.L, FxKind.Empty)
        { CutTL = PgrTheme.CutMedium, CutBR = PgrTheme.CutMedium, InsetDp = 3 });
        card.Tag = "fx";
        card.SetMinimumHeight(Dp(230)); card.SetPadding(Dp(22), Dp(12), Dp(16), Dp(24));
    }
}
