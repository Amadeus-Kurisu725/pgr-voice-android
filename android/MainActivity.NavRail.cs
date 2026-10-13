using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

/// <summary>Wide-landscape scaffold (SPEC 横屏布局): 80dp dark nav rail and the two-column split host.</summary>
public sealed partial class MainActivity
{
    void CreateNavRail()
    {
        float density = Resources!.DisplayMetrics!.Density;
        navRail = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        // S-tier deco: fine grid, small orbit arc, Red Tide hairline, corner ticks; bottom-right cut corner.
        navRail.Background = new DarkPanelDrawable(density, DecoTier.S) { CutBottomRightDp = 16 };
        navRail.SetPadding(0, Dp(4), 0, Dp(10));
        var mark = new FrameLayout(this) { ImportantForAccessibility = ImportantForAccessibility.No };
        var icon = Icon("mark", PgrTheme.OnPlate, 26);
        mark.AddView(icon, new FrameLayout.LayoutParams(Dp(30), Dp(30), GravityFlags.Center));
        navRail.AddView(mark, new LinearLayout.LayoutParams(-1, Dp(40)));
        var scroll = new ScrollView(this) { VerticalScrollBarEnabled = false, VerticalFadingEdgeEnabled = true };
        scroll.SetFadingEdgeLength(Dp(16));
        var items = new LinearLayout(this) { Orientation = Orientation.Vertical };
        scroll.AddView(items);
        for (int i = 0; i < tabLabels.Length; i++)
        {
            int targetPage = tabPages[i];
            var item = new Button(this) { Text = tabLabels[i], TextSize = 11 };
            item.Click += (_, _) => Safe(() => ShowPage(targetPage));
            items.AddView(item, new LinearLayout.LayoutParams(-1, Dp(50)));
            railButtons.Add(item);
        }
        navRail.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        railOrientationButton = new Button(this) { Text = "横 / 竖屏", TextSize = 10 };
        railOrientationButton.BackgroundTintList = null;
        railOrientationButton.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.OnPlate, 50)),
            PgrTheme.Plate(this, PgrTheme.A(Color.White, 16), PgrTheme.A(PgrTheme.OnPlate, 80), PgrTheme.CutSmall, Corners.BottomRight), null);
        railOrientationButton.SetTextColor(PgrTheme.OnPlate); railOrientationButton.SetAllCaps(false); railOrientationButton.StateListAnimator = null;
        railOrientationButton.SetCompoundDrawablesWithIntrinsicBounds(null, new PgrIconDrawable("orientation", PgrTheme.OnPlate, density, 16), null, null);
        railOrientationButton.SetPadding(0, Dp(4), 0, Dp(4)); railOrientationButton.CompoundDrawablePadding = Dp(1);
        railOrientationButton.SetMinHeight(Dp(PgrTheme.Touch)); railOrientationButton.SetMinimumHeight(Dp(PgrTheme.Touch));
        railOrientationButton.Click += (_, _) => Safe(ChooseMainOrientation);
        navRail.AddView(railOrientationButton, new LinearLayout.LayoutParams(-1, Dp(PgrTheme.Touch)) { LeftMargin = Dp(8), RightMargin = Dp(8), TopMargin = Dp(4) });
        shell.AddView(navRail, new LinearLayout.LayoutParams(Dp(80), ViewGroup.LayoutParams.MatchParent));
        // Short landscape screens (≈411dp and below): smaller mark, tighter padding, 48dp tabs, so all six tabs and 横 / 竖屏
        // fit without clipping; anything still left over scrolls inside the tab list (fading edge shows it).
        navRail.LayoutChange += (_, e) =>
        {
            int h = e.Bottom - e.Top; if (h <= 0 || h == e.OldBottom - e.OldTop) return;
            int full = Dp(4 + 10) + Dp(40) + Dp(50) * tabLabels.Length + Dp(PgrTheme.Touch + 4);
            bool compact = h < full;
            int markH = compact ? Dp(26) : Dp(40);
            // 0.4.6: very short rails (640×360 / 803×344 landscape) would still push 设置 out of view and cut 历史 under 横 / 竖屏 by
            // more than a hairline — there the toggle joins the end of the scrolling tab list so all six tabs show whole.
            // (≥ 369dp rails, e.g. 393×852 landscape, keep the 0.4.3 pinned toggle.)
            int compactNeed = Dp(2 + 6) + markH + Dp(48) * tabLabels.Length + Dp(PgrTheme.Touch + 2);
            bool tight = compact && compactNeed - h > Dp(12);
            int itemH = compact ? Math.Clamp((h - Dp(2 + 6) - markH - (tight ? 0 : Dp(PgrTheme.Touch + 2))) / tabLabels.Length, Dp(48), Dp(50)) : Dp(50);
            navRail.Post(() =>
            {
                var want = tight ? (ViewGroup)items : navRail;
                if (!ReferenceEquals(railOrientationButton.Parent, want))
                {
                    (railOrientationButton.Parent as ViewGroup)?.RemoveView(railOrientationButton);
                    want.AddView(railOrientationButton, new LinearLayout.LayoutParams(-1, Dp(PgrTheme.Touch)) { LeftMargin = Dp(8), RightMargin = Dp(8), TopMargin = Dp(4), BottomMargin = tight ? Dp(4) : 0 });
                }
                navRail.SetPadding(0, compact ? Dp(2) : Dp(4), 0, compact ? Dp(6) : Dp(10));
                if (mark.LayoutParameters is LinearLayout.LayoutParams mp && mp.Height != markH) { mp.Height = markH; mark.LayoutParameters = mp; }
                foreach (var b in railButtons)
                    if (b.LayoutParameters is LinearLayout.LayoutParams lp && lp.Height != itemH) { lp.Height = itemH; b.SetMinHeight(itemH); b.SetMinimumHeight(itemH); b.LayoutParameters = lp; }
                if (railOrientationButton.LayoutParameters is LinearLayout.LayoutParams op) { op.TopMargin = compact ? Dp(2) : Dp(4); railOrientationButton.LayoutParameters = op; }
            });
        };
    }

    void StyleRail(int index)
    {
        var button = railButtons[index]; bool selected = tabPages[index] == page || (page == 5 && index == 0);
        float density = Resources!.DisplayMetrics!.Density;
        var face = new RailTabDrawable(density) { Selected = selected, Dot = tabDots[index] };
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(PgrTheme.A(PgrTheme.OnPlate, 50)), face, new ColorDrawable(Color.White));
        string label = tabLabels[index], english = tabEnglish[index];
        var text = new SpannableString(label + "\n" + english);
        int start = label.Length + 1, end = text.Length();
        text.SetSpan(new RelativeSizeSpan(.7f), start, end, SpanTypes.ExclusiveExclusive);
        text.SetSpan(new TypefaceSpan(PgrTheme.Condensed), start, end, SpanTypes.ExclusiveExclusive);
        text.SetSpan(new ForegroundColorSpan(selected ? PgrTheme.RedText : PgrTheme.NavSub), start, end, SpanTypes.ExclusiveExclusive);
        button.TextFormatted = text;
        button.SetTextColor(selected ? PgrTheme.Foreground : PgrTheme.NavText);
        button.SetTypeface(selected ? PgrTheme.SansBold : PgrTheme.SansMedium, selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0; button.LetterSpacing = .04f; button.SetLineSpacing(0, .9f);
        button.SetCompoundDrawablesWithIntrinsicBounds(null, new PgrIconDrawable(tabIcons[index], selected ? PgrTheme.Red : PgrTheme.NavText, density, 18), null, null);
        button.CompoundDrawablePadding = 0; button.SetPadding(Dp(4), Dp(3), 0, Dp(2));
        int railH = button.LayoutParameters?.Height > 0 ? button.LayoutParameters.Height : Dp(50); button.SetMinHeight(railH); button.SetMinimumHeight(railH); button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.ContentDescription = tabLabels[index] + (tabDots[index] == null ? "" : index == 3 ? "，有待确认分支" : "，跟随中");
        button.Selected = selected;
    }

    /// <summary>Two independently scrolling columns used by split pages (01 in wide landscape): left 372dp, right fills.</summary>
    void CreateSplitHost()
    {
        splitHost = new LinearLayout(this) { Orientation = Orientation.Horizontal, Visibility = ViewStates.Gone };
        splitLeftColumn = new LinearLayout(this) { Orientation = Orientation.Vertical };
        splitLeftBar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        splitLeftBar.SetGravity(GravityFlags.CenterVertical);
        splitLeftColumn.AddView(splitLeftBar, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4), BottomMargin = Dp(2) });
        splitLeftScroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
        splitLeft = new LinearLayout(this) { Orientation = Orientation.Vertical }; splitLeft.SetPadding(0, 0, 0, Dp(16));
        splitLeftScroll.AddView(splitLeft);
        splitLeftColumn.AddView(splitLeftScroll, new LinearLayout.LayoutParams(-1, 0, 1));
        splitHost.AddView(splitLeftColumn, new LinearLayout.LayoutParams(Dp(372), -1) { RightMargin = Dp(14) });
        splitRightScroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
        splitRight = new LinearLayout(this) { Orientation = Orientation.Vertical }; splitRight.SetPadding(0, Dp(4), 0, Dp(16));
        splitRightScroll.AddView(splitRight);
        splitHost.AddView(splitRightScroll, new LinearLayout.LayoutParams(0, -1, 1));
        root.AddView(splitHost, new LinearLayout.LayoutParams(-1, 0, 1));
    }
}
