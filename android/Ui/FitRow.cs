using Android.Content;
using Android.Views;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// Horizontal row that degrades its secondary content step by step until everything fits the available width, so the
/// priority view (e.g. the app title) is never ellipsized and nothing overlaps. <see cref="ApplyLevel"/> switches the row
/// to a degradation level (0 = full design); <see cref="NeedWidth"/> reports the row's natural width at the current level.
/// The level is re-evaluated only when the width changes or <see cref="Refit"/> is called, so it never oscillates.
/// </summary>
public class FitRow : LinearLayout
{
    public FitRow(Context context) : base(context) { Orientation = Orientation.Horizontal; }

    public int Levels { get; set; }
    public Action<int>? ApplyLevel { get; set; }
    /// <summary>Natural width in px of the content at the current level (defaults to the sum of the visible children).</summary>
    public Func<int>? NeedWidth { get; set; }
    public int Level { get; private set; } = -1;
    int fittedWidth = -1;

    public void Refit() { fittedWidth = -1; RequestLayout(); }

    public int Natural(View v)
    {
        if (v.Visibility == ViewStates.Gone) return 0;
        v.Measure(MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        int w = v.MeasuredWidth;
        // 0.4.6: a fixed-width child (header mark: 34dp box around a 30dp icon) occupies its layout width, not its intrinsic
        // size — counting the latter under-reported the header by 4dp and let 「战双剧情配…」 ellipsize at 393dp × 1.3 font.
        if (v.LayoutParameters is { Width: > 0 } fixedLp) w = Math.Max(w, fixedLp.Width);
        if (v.LayoutParameters is MarginLayoutParams m) w += m.LeftMargin + m.RightMargin;
        return w;
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int avail = MeasureSpec.GetSize(widthMeasureSpec);
        if (ApplyLevel != null && MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Unspecified && avail > 0 && avail != fittedWidth)
        {
            fittedWidth = avail;
            int chosen = Levels;
            for (int l = 0; l <= Levels; l++)
            {
                ApplyLevel(l);
                int need = PaddingLeft + PaddingRight + (NeedWidth?.Invoke() ?? SumChildren());
                if (need <= avail) { chosen = l; break; }
            }
            if (chosen != Levels) ApplyLevel(chosen);
            Level = chosen;
        }
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    int SumChildren() { int s = 0; for (int i = 0; i < ChildCount; i++) s += Natural(GetChildAt(i)!); return s; }
}

/// <summary>
/// 0.4.6: English eyebrow beside a section heading. It may wrap between words (0.4.3 look, e.g. AUTO-PLAY / CHOICES), but when
/// a single word no longer fits the column (narrow column + large font) it is left out instead of breaking mid-word.
/// </summary>
public sealed class WordFitText : TextView
{
    public WordFitText(Context context) : base(context) { }
    bool dropped;
    /// <summary>Single-line labels: drop when the whole text does not fit (instead of clipping, e.g. "MAIN STOR").</summary>
    public bool WholeLine { get; set; }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        dropped = false;
        if (MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Unspecified)
        {
            int avail = MeasureSpec.GetSize(widthMeasureSpec) - PaddingLeft - PaddingRight;
            float longest = 0;
            if (WholeLine) longest = Paint!.MeasureText(Text ?? "");
            else foreach (var word in (Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)) longest = Math.Max(longest, Paint!.MeasureText(word));
            if (longest > avail) { dropped = true; SetMeasuredDimension(MeasureSpec.GetSize(widthMeasureSpec), 0); return; }
        }
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    protected override void OnDraw(global::Android.Graphics.Canvas? canvas) { if (!dropped) base.OnDraw(canvas); }
}

/// <summary>
/// 0.4.6: single-line label with a shorter fallback (e.g. segment tab 「悬浮 · 跟随」 → 「悬浮」) used only when the full text
/// does not fit the width it is offered (large font / narrow phones) instead of being clipped to 「悬浮 · 跟」.
/// </summary>
public sealed class ShortFitText : TextView
{
    public ShortFitText(Context context, string full, string shortText) : base(context) { Full = full; Short = shortText; Text = full; SetSingleLine(true); }
    public string Full { get; }
    public string Short { get; }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        string want = Full;
        if (MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Unspecified
            && Paint!.MeasureText(Full) > MeasureSpec.GetSize(widthMeasureSpec) - PaddingLeft - PaddingRight) want = Short;
        if (Text != want) Text = want;
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }
}

/// <summary>
/// 0.4.6: single-line text that steps its size down (to <see cref="MinSp"/>) only when the full text would be ellipsized in the
/// width it is offered (e.g. 「内置 18 个小节、4087 条节点资料。」 in the 640dp landscape sheet band); otherwise it keeps its base size.
/// </summary>
public sealed class ShrinkFitText : TextView
{
    public ShrinkFitText(Context context, float baseSp, float minSp) : base(context)
    { BaseSp = baseSp; MinSp = minSp; TextSize = baseSp; SetMaxLines(1); Ellipsize = global::Android.Text.TextUtils.TruncateAt.End; }
    public float BaseSp { get; }
    public float MinSp { get; }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (MeasureSpec.GetMode(widthMeasureSpec) != MeasureSpecMode.Unspecified)
        {
            int avail = MeasureSpec.GetSize(widthMeasureSpec) - PaddingLeft - PaddingRight;
            var content = TextFormatted ?? new Java.Lang.String(Text ?? "");
            float sp = BaseSp;
            var paint = new global::Android.Text.TextPaint(Paint);
            var metrics = Resources!.DisplayMetrics!;
            float Px(float v) => global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp, v, metrics);
            while (sp > MinSp)
            {
                paint.TextSize = Px(sp);
                if (global::Android.Text.Layout.GetDesiredWidth(content, paint) <= avail) break;
                sp = Math.Max(MinSp, sp - .5f);
            }
            if (Math.Abs(TextSize - Px(sp)) > .5f) SetTextSize(global::Android.Util.ComplexUnitType.Sp, sp);
        }
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }
}
