using Android.Content;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>LinearLayout that fills its parent but never exceeds MaxWidthPx (measure-time cap, so it can't overflow a narrower column).</summary>
public sealed class CapLinearLayout : LinearLayout
{
    public CapLinearLayout(Context context) : base(context) { }
    public int MaxWidthPx { get; set; }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (MaxWidthPx > 0 && MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Unspecified && MeasureSpec.GetSize(widthMeasureSpec) > MaxWidthPx)
            widthMeasureSpec = MeasureSpec.MakeMeasureSpec(MaxWidthPx, MeasureSpec.GetMode(widthMeasureSpec));
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }
}
