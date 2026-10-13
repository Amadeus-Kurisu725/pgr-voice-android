using Android.Content;
using Android.Graphics;
using Android.Widget;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 0.4.8 分类面板 B 版（定稿 cfx-a5b6.js cellB）：整格封面，底部暗色渐变压中文名 + 英文名；
/// 无封面时深色石板底 + 16dp 细网格。选中：2dp 深红描边、底部深红渐变、左下 36×2dp 白色短线。外框右上切角 12dp。
/// </summary>
public sealed class CategoryCardDrawable(float density) : PgrShapeDrawable(density)
{
    Bitmap? cover;
    public Bitmap? Cover { get => cover; set { cover = value; InvalidateSelf(); } }
    public bool Selected { get; set; }
    /// <summary>边框宽（dp）：选中 2，否则 1；子视图按此留内边距。</summary>
    public float FrameDp => Selected ? 2 : 1;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        using var outer = new RectF(b);
        paint.Reset(); paint.AntiAlias = true; paint.SetStyle(Paint.Style.Fill);
        PgrArt.Chamfer(path, outer, 0, D(12), 0, 0);
        paint.Color = Selected ? C(PgrTheme.Red) : C(Color.Argb(242, 174, 182, 191));
        canvas.DrawPath(path, paint);
        float f = D(FrameDp);
        using var r = new RectF(outer.Left + f, outer.Top + f, outer.Right - f, outer.Bottom - f);
        PgrArt.Chamfer(path, r, 0, D(11), 0, 0);
        int save = canvas.Save(); canvas.ClipPath(path);
        if (cover is { IsRecycled: false } img)
        {
            // 定稿 background-position 60% 28% / cover。
            float scale = Math.Max(r.Width() / img.Width, r.Height() / img.Height);
            float w = img.Width * scale, h = img.Height * scale;
            float left = r.Left + (r.Width() - w) * .6f, top = r.Top + (r.Height() - h) * .28f;
            using var dst = new RectF(left, top, left + w, top + h);
            paint.FilterBitmap = true; paint.Alpha = alpha;
            canvas.DrawBitmap(img, null, dst, paint); paint.Alpha = 255;
        }
        else
        {
            using var slate = new LinearGradient(r.Left, r.Top, r.Right, r.Bottom, C(Color.ParseColor("#3A4450")), C(Color.ParseColor("#2A3139")), Shader.TileMode.Clamp!);
            paint.SetShader(slate); canvas.DrawRect(r, paint); paint.SetShader(null);
            paint.Color = C(Color.Argb(20, 143, 180, 210));
            for (float x = r.Left; x < r.Right; x += D(16)) canvas.DrawRect(x, r.Top, x + D(1), r.Bottom, paint);
            for (float y = r.Top; y < r.Bottom; y += D(16)) canvas.DrawRect(r.Left, y, r.Right, y + D(1), paint);
        }
        // 底部渐变：0 → 40% → 72%（自下而上）。
        Color baseC = Selected ? Color.Rgb(110, 27, 40) : Color.Rgb(20, 24, 29);
        using var shade = new LinearGradient(0, r.Bottom, 0, r.Top,
            new int[] { C(baseC, Selected ? 240 : 230).ToArgb(), C(baseC, Selected ? 153 : 140).ToArgb(), C(baseC, 0).ToArgb(), C(baseC, 0).ToArgb() },
            new float[] { 0f, .4f, .72f, 1f }, Shader.TileMode.Clamp!);
        paint.SetShader(shade); canvas.DrawRect(r, paint); paint.SetShader(null);
        if (Selected) { paint.Color = C(Color.White); canvas.DrawRect(r.Left + D(10), r.Bottom - D(2), r.Left + D(46), r.Bottom, paint); }
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// 英文副名：按 <see cref="CategoryNames.FitEnglish"/> 先收字距、再降字号（≥7sp）；仍放不下则换行。不截断、不加省略号。
/// </summary>
public sealed class CategoryEnText : TextView
{
    public CategoryEnText(Context context, string text, Color color) : base(context)
    {
        Text = text; ImportantForAccessibility = Android.Views.ImportantForAccessibility.No;
        SetTypeface(PgrTheme.Condensed, TypefaceStyle.Bold); SetTextColor(color); SetAllCaps(true); SetIncludeFontPadding(false);
        TextSize = CategoryNames.EnSizeSp; LetterSpacing = CategoryNames.EnSpacingEm; SetSingleLine(true); Ellipsize = null;
    }

    bool singleLine = true;

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (MeasureSpec.GetMode(widthMeasureSpec) != Android.Views.MeasureSpecMode.Unspecified)
        {
            int avail = MeasureSpec.GetSize(widthMeasureSpec) - PaddingLeft - PaddingRight;
            float scaled = Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, 1f, Resources!.DisplayMetrics);
            string text = Text ?? "";
            using var probe = new Android.Text.TextPaint(Paint);
            var fit = CategoryNames.FitEnglish((ls, sp) => { probe.TextSize = sp * scaled; probe.LetterSpacing = ls; return probe.MeasureText(text); }, avail);
            if (Math.Abs(TextSize / scaled - fit.SizeSp) > .01f) SetTextSize(Android.Util.ComplexUnitType.Sp, fit.SizeSp);
            if (Math.Abs(LetterSpacing - fit.SpacingEm) > .001f) LetterSpacing = fit.SpacingEm;
            if (fit.Wrap == singleLine) { singleLine = !fit.Wrap; SetSingleLine(singleLine); Ellipsize = null; }
        }
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }
}
