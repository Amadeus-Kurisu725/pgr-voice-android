using Android.Content;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// Horizontally scrollable cast / squad strip: no scrollbar, no overscroll glow, a subtle fade on the right edge only while
/// more chips lie beyond it. Once a drag is clearly horizontal it claims the gesture so the parent vertical scroll / tab
/// swipes don't steal it; vertical drags are left to the parent.
/// </summary>
public sealed class CastScroll : HorizontalScrollView
{
    readonly int slop;
    float downX, downY; bool decided;

    public CastScroll(Context context) : base(context)
    {
        HorizontalScrollBarEnabled = false; OverScrollMode = OverScrollMode.Never; FillViewport = false;
        HorizontalFadingEdgeEnabled = true; SetFadingEdgeLength((int)(22 * context.Resources!.DisplayMetrics!.Density));
        slop = ViewConfiguration.Get(context)!.ScaledTouchSlop;
    }

    protected override float LeftFadingEdgeStrength => 0f;

    /// <summary>Optional width cap in px (0 = none) for wrap_content strips sharing a row, e.g. the tray SQUAD.</summary>
    public int MaxWidthPx { get; set; }
    /// <summary>Optional cap as a fraction of the width offered by the parent (0 = none), so a squad never starves a name plate.</summary>
    public float MaxWidthFraction { get; set; }
    /// <summary>Optional px always left to the siblings (e.g. the speaker name plate); only bites when the row is actually tight.</summary>
    public int ReservePx { get; set; }
    /// <summary>Optional sibling whose natural (unclipped) width is also reserved, e.g. the speaker plate; only bites when the row is tight.</summary>
    public TextView? ReserveFor { get; set; }
    /// <summary>Floor (px) the strip keeps even when <see cref="ReserveFor"/> asks for more, so a couple of avatars stay visible.</summary>
    public int MinWidthPx { get; set; }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if ((MaxWidthPx > 0 || MaxWidthFraction > 0 || ReservePx > 0) && MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Exactly)
        {
            int size = MeasureSpec.GetSize(widthMeasureSpec); bool unspecified = MeasureSpec.GetMode(widthMeasureSpec) == MeasureSpecMode.Unspecified;
            int cap = unspecified ? (MaxWidthPx > 0 ? MaxWidthPx : int.MaxValue / 2) : Math.Min(size, MaxWidthPx > 0 ? MaxWidthPx : size);
            if (!unspecified && MaxWidthFraction > 0) cap = Math.Min(cap, (int)(size * MaxWidthFraction));
            if (!unspecified && ReservePx > 0) cap = Math.Max(0, Math.Min(cap, size - ReservePx));
            if (!unspecified && ReserveFor != null && ReserveFor.Visibility != ViewStates.Gone)
            {
                // Natural plate width (text + padding + margins); the strip yields down to MinWidthPx and scrolls instead of clipping the name.
                int need = (int)Math.Ceiling(ReserveFor.Paint!.MeasureText(ReserveFor.Text ?? "")) + ReserveFor.PaddingLeft + ReserveFor.PaddingRight;
                if (ReserveFor.LayoutParameters is ViewGroup.MarginLayoutParams m) need += m.LeftMargin + m.RightMargin;
                if (size - need < cap) cap = Math.Max(Math.Min(cap, MinWidthPx), size - need);
            }
            widthMeasureSpec = MeasureSpec.MakeMeasureSpec(cap, MeasureSpecMode.AtMost);
        }
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e != null)
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    downX = e.GetX(); downY = e.GetY(); decided = false;
                    if (CanScrollHorizontally(1) || CanScrollHorizontally(-1)) Parent?.RequestDisallowInterceptTouchEvent(true);
                    break;
                case MotionEventActions.Move when !decided:
                    float dx = Math.Abs(e.GetX() - downX), dy = Math.Abs(e.GetY() - downY);
                    if (dx > slop || dy > slop)
                    {
                        decided = true;
                        // Horizontal: keep the gesture (only if there is something to scroll); vertical: hand it back to the parent.
                        Parent?.RequestDisallowInterceptTouchEvent(dx > dy && (CanScrollHorizontally(1) || CanScrollHorizontally(-1)));
                    }
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    decided = false; break;
            }
        return base.DispatchTouchEvent(e);
    }

    /// <summary>Wraps <paramref name="row"/> (a horizontal LinearLayout of chips) in a CastScroll.</summary>
    public static CastScroll Wrap(View row)
    {
        var s = new CastScroll(row.Context!);
        s.AddView(row, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        return s;
    }
}
