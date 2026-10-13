using Android.Graphics;
using Android.Graphics.Drawables;
using Path = Android.Graphics.Path;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>Segmented progress / match strip (03 search progress, 04 match ticks): done = crimson, current = bright, rest = mist.</summary>
public sealed class SegmentsDrawable(float density, bool onDark = true) : PgrShapeDrawable(density)
{
    public int Count { get; set; } = 5;
    public int Done { get; set; }
    public int Mark { get; set; } = -1;
    public float SegmentDp { get; set; } = 12;
    public override int IntrinsicHeight => (int)D(4);
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || Count <= 0) return;
        int n = Math.Min(Count, Math.Max(1, (int)(b.Width() / (D(SegmentDp) + D(3)))));
        float gap = D(3), w = Math.Min(D(SegmentDp), (b.Width() - gap * (n - 1)) / n), h = Math.Min(b.Height(), D(3)), y = b.ExactCenterY() - h / 2;
        // Scale indexes when there are more items than segments.
        int Map(int i) => Count <= n ? i : (int)((long)i * n / Count);
        int done = Done <= 0 ? 0 : Math.Max(1, Map(Done - 1) + 1), cur = Mark < 0 ? -1 : Map(Mark);
        paint.SetStyle(Paint.Style.Fill);
        for (int i = 0; i < n; i++)
        {
            Color c = i == cur ? (onDark ? PgrArt.TideRed : PgrTheme.Red) : i < done ? (onDark ? PgrTheme.A(PgrArt.TideRed, 170) : PgrTheme.A(PgrTheme.Red, 150))
                : (onDark ? PgrTheme.A(PgrArt.MistLine, 80) : PgrTheme.A(PgrTheme.Slate, 70));
            paint.Color = C(c); float x = b.Left + i * (w + gap);
            canvas.DrawRect(x, y, x + w, y + h, paint);
        }
    }
}

/// <summary>Crimson hexagon key (確認当前句): pointed ends, gradient, inner gold hairline, pressed / disabled states. Vertical for the 78dp strip.</summary>
public sealed class HexButtonDrawable(float density, bool vertical = false) : PgrShapeDrawable(density)
{
    bool pressed, enabled = true;
    public bool Brass { get; set; }
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    {
        bool p = state?.Contains(global::Android.Resource.Attribute.StatePressed) == true;
        bool e = state?.Contains(global::Android.Resource.Attribute.StateEnabled) == true;
        if (p == pressed && e == enabled) return false;
        pressed = p; enabled = e; InvalidateSelf(); return true;
    }
    void Hex(RectF r, float tip)
    {
        path.Reset();
        if (!vertical)
        {
            float cy = r.CenterY();
            path.MoveTo(r.Left, cy); path.LineTo(r.Left + tip, r.Top); path.LineTo(r.Right - tip, r.Top);
            path.LineTo(r.Right, cy); path.LineTo(r.Right - tip, r.Bottom); path.LineTo(r.Left + tip, r.Bottom); path.Close();
        }
        else
        {
            float cx = r.CenterX();
            path.MoveTo(cx, r.Top); path.LineTo(r.Right, r.Top + tip); path.LineTo(r.Right, r.Bottom - tip);
            path.LineTo(cx, r.Bottom); path.LineTo(r.Left, r.Bottom - tip); path.LineTo(r.Left, r.Top + tip); path.Close();
        }
    }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        var r = new RectF(b.Left + D(2), b.Top + D(2), b.Right - D(2), b.Bottom - D(2));
        float tip = vertical ? Math.Min(D(16), r.Width() / 2) : Math.Min(D(16), r.Height() / 2);
        Hex(r, tip);
        Color top = Brass ? Color.ParseColor("#B08A3E") : Color.ParseColor("#B3384A"), bottom = Brass ? Color.ParseColor("#7E6128") : Color.ParseColor("#8E1E30");
        if (pressed) { top = PgrTheme.A(bottom, 255); }
        // Disabled (e.g. listening play while a branch waits): steel grey plate, never a dimmed crimson.
        if (!enabled) { top = Color.ParseColor("#5E6873"); bottom = Color.ParseColor("#434B55"); }
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = Color.White; paint.SetShader(new LinearGradient(0, r.Top, 0, r.Bottom, C(top), C(bottom), Shader.TileMode.Clamp!));
        canvas.DrawPath(path, paint); paint.SetShader(null);
        // Outer glow line + inner gold hairline.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1));
        paint.Color = C(PgrTheme.A(PgrArt.TideRed, enabled ? 200 : 90)); canvas.DrawPath(path, paint);
        var inner = new RectF(r.Left + D(4), r.Top + D(4), r.Right - D(4), r.Bottom - D(4));
        Hex(inner, Math.Max(0, tip - D(3)));
        paint.StrokeWidth = Math.Max(1, D(.7f)); paint.Color = C(PgrTheme.A(PgrArt.GoldLine, enabled ? 120 : 50)); canvas.DrawPath(path, paint);
        // Scan lines.
        paint.Color = C(PgrTheme.A(Color.White, 14)); paint.StrokeWidth = 1;
        for (float y = r.Top + D(3); y < r.Bottom; y += D(3)) canvas.DrawLine(r.Left + tip, y, r.Right - tip, y, paint);
    }
}

/// <summary>Diamond key frame for 上一句 / 下一句 on dark trays.</summary>
public sealed class DiamondKeyDrawable(float density) : PgrShapeDrawable(density)
{
    bool pressed;
    public override bool IsStateful => true;
    protected override bool OnStateChange(int[]? state)
    { bool p = state?.Contains(global::Android.Resource.Attribute.StatePressed) == true; if (p == pressed) return false; pressed = p; InvalidateSelf(); return true; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cx = b.ExactCenterX(), cy = b.ExactCenterY(), s = Math.Min(b.Width(), b.Height()) / 2f - D(2);
        path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(pressed ? PgrTheme.A(PgrArt.MistLine, 70) : PgrTheme.A(Color.ParseColor("#1D232A"), 150)); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1)); paint.Color = C(PgrTheme.A(PgrArt.MistLine, 190)); canvas.DrawPath(path, paint);
    }
}

/// <summary>Script rail node for one story line row: vertical rail + diamond (hollow; current = solid crimson with halo).</summary>
public sealed class RailNodeDrawable(float density) : PgrShapeDrawable(density)
{
    public bool IsCurrent { get; set; }
    public bool First { get; set; }
    public bool Last { get; set; }
    public float NodeTopDp { get; set; } = 16;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cx = b.Left + D(14), cy = b.Top + D(NodeTopDp);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.A(PgrTheme.Slate, 110));
        canvas.DrawRect(cx - D(.6f), First ? cy : b.Top, cx + D(.6f), Last ? cy : b.Bottom, paint);
        float s = IsCurrent ? D(7) : D(4.5f);
        path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
        if (IsCurrent)
        {
            paint.Color = C(PgrTheme.A(PgrTheme.Red, 40)); canvas.DrawCircle(cx, cy, D(11), paint);
            paint.Color = C(PgrTheme.Red); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1); paint.Color = C(Color.White); canvas.DrawPath(path, paint);
        }
        else
        {
            paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1)); paint.Color = C(PgrTheme.Slate); canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>05L-1 SECTION PROGRESS rail: one tick per line (long every 10), played crimson, current solid diamond, thin tide wave below. Display only.</summary>
public sealed class SectionRailDrawable(float density) : PgrShapeDrawable(density)
{
    public int Total { get; set; }
    public int Index { get; set; } = -1;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float l = b.Left + D(6), r = b.Right - D(6), y = b.Top + D(12);
        paint.SetStyle(Paint.Style.Fill);
        paint.Color = C(PgrTheme.A(PgrArt.MistLine, 110)); canvas.DrawRect(l, y - D(.5f), r, y + D(.5f), paint);
        int n = Math.Max(1, Total);
        float step = (r - l) / Math.Max(1, n - 1);
        int every = step < D(2) ? Math.Max(1, (int)Math.Ceiling(D(2) / step)) : 1;
        for (int i = 0; i < n; i += every)
        {
            float x = n == 1 ? l : l + i * step; bool major = i % 10 == 0; bool played = Index >= 0 && i <= Index;
            paint.Color = C(played ? PgrArt.TideRed : PgrTheme.A(PgrArt.MistLine, major ? 200 : 120));
            float h = major ? D(7) : D(3.5f);
            canvas.DrawRect(x - D(.5f), y - h, x + D(.5f), y + (major ? D(2) : 0), paint);
        }
        if (Index >= 0 && Total > 0)
        {
            float x = n == 1 ? l : l + Math.Min(Index, n - 1) * step;
            paint.Color = C(PgrArt.TideRed); canvas.DrawRect(l, y - D(1), x, y + D(1), paint);
            float s = D(6); path.Reset(); path.MoveTo(x, y - s); path.LineTo(x + s, y); path.LineTo(x, y + s); path.LineTo(x - s, y); path.Close();
            paint.Color = C(Color.ParseColor("#F2F3F5")); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f); paint.Color = C(PgrArt.TideRed); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Fill);
        }
        // Thin red-tide waveform under the rail (played part brighter).
        float wy = b.Top + D(26), split = Index >= 0 && Total > 0 ? l + Math.Min(Index, n - 1) * step : l;
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.9f));
        for (int pass = 0; pass < 2; pass++)
        {
            path.Reset(); bool started = false;
            for (float x = l; x <= r; x += D(3))
            {
                if (pass == 0 ? x > split : x < split) continue;
                float amp = (float)(Math.Abs(Math.Sin(x / D(7))) * 0.6 + Math.Abs(Math.Sin(x / D(19))) * 0.4) * D(5);
                if (!started) { path.MoveTo(x, wy); started = true; }
                path.LineTo(x + D(1), wy - amp); path.LineTo(x + D(2), wy + amp * .4f);
            }
            paint.Color = C(pass == 0 ? PgrTheme.A(PgrArt.TideRed, 220) : PgrTheme.A(PgrArt.TideRed, 90));
            canvas.DrawPath(path, paint);
        }
    }
}

/// <summary>Portrait tray handle tab (112×22 visual on a 48dp hit area): chamfered slate tab, top hairline, 16×2 centre tick.</summary>
public sealed class TrayHandleDrawable(float density) : PgrShapeDrawable(density)
{
    public bool Brass { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float w = Math.Min(D(112), b.Width()), h = D(22), l = b.ExactCenterX() - w / 2, t = b.Bottom - h, r = l + w;
        path.Reset(); path.MoveTo(l, b.Bottom); path.LineTo(l + D(10), t); path.LineTo(r - D(10), t); path.LineTo(r, b.Bottom); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrArt.DeckTop); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.8f)); paint.Color = C(PgrTheme.A(Color.White, 60));
        canvas.DrawLine(l + D(10), t + D(.5f), r - D(10), t + D(.5f), paint);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(Brass ? PgrArt.GoldLine : PgrArt.TideRed);
        canvas.DrawRect(b.ExactCenterX() - D(8), t, b.ExactCenterX() + D(8), t + D(2), paint);
    }
}

/// <summary>Landscape tray column left-edge handle (20×96 visual, 48dp hit area): slate tab with crimson edge.</summary>
public sealed class EdgeHandleDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Height() <= 0) return;
        float w = D(20), h = Math.Min(D(96), b.Height()), l = b.Left, r = l + w, t = b.ExactCenterY() - h / 2, bt = t + h;
        path.Reset(); path.MoveTo(r, t); path.LineTo(l + D(6), t + D(6)); path.LineTo(l, t + D(14)); path.LineTo(l, bt - D(14)); path.LineTo(l + D(6), bt - D(6)); path.LineTo(r, bt); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrArt.DeckTop); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(.8f)); paint.Color = C(PgrTheme.A(PgrArt.MistLine, 120)); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrArt.TideRed); canvas.DrawRect(l, b.ExactCenterY() - D(8), l + D(2), b.ExactCenterY() + D(8), paint);
    }
}

/// <summary>Page footer: F1b line-art emblem centred + Red Tide band (05-B 页尾).</summary>
public sealed class ArchiveFooterDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float ew = Math.Min(D(120), b.Height() * .9f * 600 / 760f);
        PgrArt.Emblem(canvas, b.ExactCenterX() + ew / 2, b.Bottom - D(14), ew, .55f, false, paint, alpha);
        PgrArt.Tide(canvas, new RectF(b.Left, b.Bottom - D(44), b.Right, b.Bottom), .9f, false, d, paint, path, alpha);
    }
}

/// <summary>Small teal-grey padlock glyph (read-only notes).</summary>
public sealed class LockGlyph(float density, Color? color = null) : PgrShapeDrawable(density)
{
    public override int IntrinsicWidth => (int)D(14);
    public override int IntrinsicHeight => (int)D(14);
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; float l = b.Left + D(2), r = b.Right - D(2), t = b.Top + D(6), bt = b.Bottom - D(1);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1.3f)); paint.Color = C(color ?? PgrTheme.Cyan);
        canvas.DrawRect(l, t, r, bt, paint);
        using var arc = new RectF(b.ExactCenterX() - D(3.5f), b.Top + D(1), b.ExactCenterX() + D(3.5f), t + D(4));
        canvas.DrawArc(arc, 180, 180, false, paint);
        paint.SetStyle(Paint.Style.Fill); canvas.DrawCircle(b.ExactCenterX(), (t + bt) / 2, D(1.2f), paint);
    }
}

/// <summary>06L-2 mini route map (display only): section axis 001 → last line, heard part crimson, choice points as small
/// brass diamonds, the pending point solid brass with its options forking above / below and rejoining.</summary>
public sealed class RouteMapDrawable(float density) : PgrShapeDrawable(density)
{
    public int Total { get; set; } = 1;
    public int CurrentLine { get; set; }
    public int[] Points { get; set; } = Array.Empty<int>();
    public int Branches { get; set; } = 2;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float l = b.Left + D(8), r = b.Right - D(8), y = b.Top + D(20);
        float X(int line) => l + (r - l) * Math.Clamp(line / (float)Math.Max(1, Total), 0, 1);
        float cx = X(CurrentLine);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1.4f));
        paint.Color = C(PgrTheme.A(PgrTheme.Slate, 120)); canvas.DrawLine(cx, y, r, y, paint);
        paint.Color = C(PgrTheme.Red); paint.StrokeWidth = Math.Max(1, D(2)); canvas.DrawLine(l, y, cx, y, paint);
        // Fork after the pending point: up to three option paths, rejoining ~18% further along.
        float jx = Math.Min(r - D(14), cx + Math.Max(D(48), (r - l) * .18f));
        int n = Math.Clamp(Branches, 1, 3);
        for (int i = 0; i < n; i++)
        {
            float off = (i - (n - 1) / 2f) * D(11);
            if (Math.Abs(off) < .5f) continue;
            path.Reset(); path.MoveTo(cx, y); path.CubicTo(cx + D(14), y + off, jx - D(14), y + off, jx, y);
            paint.StrokeWidth = Math.Max(1, D(1.2f)); paint.Color = C(PgrTheme.A(PgrTheme.Brass, i == 0 ? 230 : 150));
            if (i != 0) paint.SetPathEffect(new DashPathEffect(new[] { D(3), D(2.5f) }, 0));
            canvas.DrawPath(path, paint); paint.SetPathEffect(null);
        }
        void Diamond(float x, float s, bool fill, Color c)
        {
            path.Reset(); path.MoveTo(x, y - s); path.LineTo(x + s, y); path.LineTo(x, y + s); path.LineTo(x - s, y); path.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(fill ? c : Color.White); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1.1f)); paint.Color = C(c); canvas.DrawPath(path, paint);
        }
        Diamond(l, D(3.5f), false, PgrTheme.Slate); Diamond(r, D(3.5f), false, PgrTheme.Slate);
        foreach (int p in Points) if (Math.Abs(X(p) - cx) > D(2)) Diamond(X(p), D(4), false, PgrTheme.Brass);
        Diamond(jx, D(3.5f), false, PgrTheme.Slate);
        Diamond(cx, D(6), true, PgrTheme.Brass);
        paint.SetStyle(Paint.Style.Fill); paint.TextSize = D(9); paint.SetTypeface(PgrTheme.Condensed); paint.Color = C(PgrTheme.Secondary);
        paint.TextAlign = Paint.Align.Left; canvas.DrawText("001", l - D(4), y + D(18), paint);
        paint.TextAlign = Paint.Align.Right; canvas.DrawText(Total.ToString("000"), r + D(4), y + D(18), paint);
        paint.TextAlign = Paint.Align.Center; paint.Color = C(PgrTheme.BrassText);
        if (cx - l > D(26) && r - cx > D(26)) canvas.DrawText(CurrentLine.ToString("000"), cx, y + D(18), paint);
        paint.TextAlign = Paint.Align.Left;
    }
}

/// <summary>07 match meter: 4dp track, crimson fill at ≥90%, slate below.</summary>
public sealed class MeterDrawable(float density) : PgrShapeDrawable(density)
{
    public float Value { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float t = b.ExactCenterY() - D(2), bt = t + D(4);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.A(PgrTheme.Slate, 46)); canvas.DrawRect(b.Left, t, b.Right, bt, paint);
        paint.Color = C(Value >= .9f ? PgrTheme.Red : PgrTheme.Slate); canvas.DrawRect(b.Left, t, b.Left + b.Width() * Math.Clamp(Value, 0, 1), bt, paint);
    }
}

/// <summary>07 evidence chain rail: vertical hairline with a diamond node and the candidate number (01 crimson, others slate).</summary>
public sealed class CandidateRailDrawable(float density) : PgrShapeDrawable(density)
{
    public int Index { get; set; } = 1;
    public bool Last { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Height() <= 0) return;
        float x = b.Left + D(10), y = b.Top + D(26);
        Color c = Index == 1 ? PgrTheme.Red : PgrTheme.Slate;
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1)); paint.Color = C(PgrTheme.A(PgrTheme.Slate, 110));
        canvas.DrawLine(x, b.Top, x, Last ? y : b.Bottom, paint);
        path.Reset(); path.MoveTo(x, y - D(6)); path.LineTo(x + D(6), y); path.LineTo(x, y + D(6)); path.LineTo(x - D(6), y); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(Index == 1 ? c : Color.White); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, D(1.4f)); paint.Color = C(c); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Fill); paint.TextSize = D(10); paint.SetTypeface(PgrTheme.Condensed); paint.Color = C(c); paint.TextAlign = Paint.Align.Center;
        canvas.DrawText(Index.ToString("00"), x, y + D(20), paint); paint.TextAlign = Paint.Align.Left;
    }
}

/// <summary>08-B2 route-map node: vertical track + a status mark (brass solid = visited, teal ring = verified interaction,
/// steel hollow = locate only, crimson ! = owner first, grey dashed = pending); current = solid crimson with halo.</summary>
public sealed class RouteNodeDrawable(float density) : PgrShapeDrawable(density)
{
    public string Status { get; set; } = "";
    public bool IsCurrent { get; set; }
    public bool First { get; set; }
    public bool Last { get; set; }
    public bool Dashed { get; set; }
    public float NodeTopDp { get; set; } = 18;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cx = b.Left + D(14), cy = b.Top + D(NodeTopDp);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.A(PgrTheme.Brass, 120));
        if (Dashed) { for (float y = First ? cy : b.Top; y < (Last ? cy : b.Bottom); y += D(6)) canvas.DrawRect(cx - D(.6f), y, cx + D(.6f), y + D(3), paint); }
        else canvas.DrawRect(cx - D(.6f), First ? cy : b.Top, cx + D(.6f), Last ? cy : b.Bottom, paint);
        float s = IsCurrent ? D(7.5f) : D(5.5f);
        path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close();
        if (IsCurrent)
        {
            paint.Color = C(PgrTheme.A(PgrTheme.Red, 40)); canvas.DrawCircle(cx, cy, D(12), paint);
            paint.Color = C(PgrTheme.Red); canvas.DrawPath(path, paint);
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1); paint.Color = C(Color.White); canvas.DrawPath(path, paint);
            return;
        }
        switch (Status)
        {
            case "返回已访问选择":
                paint.Color = C(PgrTheme.Brass); canvas.DrawPath(path, paint); break;
            case "brass_open":
                paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
                paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.3f); paint.Color = C(PgrTheme.Brass); canvas.DrawPath(path, paint); break;
            case "已核实互动":
                paint.Color = C(PgrTheme.Background); canvas.DrawCircle(cx, cy, D(6.5f), paint);
                paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.4f); paint.Color = C(PgrTheme.Cyan);
                canvas.DrawCircle(cx, cy, D(6.5f), paint); canvas.DrawPath(path, paint); break;
            case "先到所属选择点":
                paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
                paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.3f); paint.Color = C(PgrTheme.Red); canvas.DrawPath(path, paint);
                paint.SetStyle(Paint.Style.Fill); canvas.DrawRect(cx - D(.7f), cy - D(3), cx + D(.7f), cy + D(1), paint); canvas.DrawRect(cx - D(.7f), cy + D(2), cx + D(.7f), cy + D(3.2f), paint); break;
            case "仅定位选择点":
                paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
                paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.3f); paint.Color = C(PgrTheme.Slate); canvas.DrawPath(path, paint); break;
            default:
                paint.Color = C(PgrTheme.Background); canvas.DrawPath(path, paint);
                paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f); paint.Color = C(PgrTheme.Muted);
                paint.SetPathEffect(new DashPathEffect(new[] { D(2), D(2) }, 0)); canvas.DrawPath(path, paint); paint.SetPathEffect(null); break;
        }
    }
}

/// <summary>Cover-header line art on the dark band: 08 = branching route (crimson node, brass fork), 10 = tick dial.</summary>
public sealed class CoverGlyphDrawable(float density, string kind) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float l = b.Left, t = b.Top, w = b.Width(), h = b.Height();
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f);
        if (kind == "dial")
        {
            float cx = l + w - h / 2 - D(4), cy = t + h / 2, r = h / 2 - D(4);
            paint.Color = C(PgrTheme.A(Color.White, 200)); canvas.DrawCircle(cx, cy, r, paint);
            paint.Color = C(PgrTheme.A(Color.White, 110)); canvas.DrawCircle(cx, cy, r * .62f, paint);
            for (int i = 0; i < 40; i++)
            {
                double a = Math.PI * 2 * i / 40; float o = r - D(i % 5 == 0 ? 7 : 4);
                canvas.DrawLine(cx + (float)Math.Cos(a) * o, cy + (float)Math.Sin(a) * o, cx + (float)Math.Cos(a) * (r - D(1)), cy + (float)Math.Sin(a) * (r - D(1)), paint);
            }
            paint.Color = C(PgrArt.TideRed); paint.StrokeWidth = D(1.8f);
            canvas.DrawLine(cx, cy, cx + r * .55f, cy - r * .55f, paint);
            paint.SetStyle(Paint.Style.Fill); Diamond(canvas, cx, cy, D(5));
            paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1); paint.Color = C(PgrTheme.A(PgrArt.GoldLine, 180));
            paint.SetPathEffect(new DashPathEffect(new[] { D(4), D(3) }, 0)); canvas.DrawLine(l, cy, cx - r - D(6), cy, paint); paint.SetPathEffect(null);
            return;
        }
        float y0 = t + h * .72f, y1 = t + h * .22f;
        paint.Color = C(PgrTheme.A(Color.White, 190));
        canvas.DrawLine(l, y0, l + w, y0, paint);
        path.Reset(); path.MoveTo(l + w * .38f, y0); path.CubicTo(l + w * .55f, y0, l + w * .52f, y1, l + w * .7f, y1); path.LineTo(l + w, y1);
        paint.Color = C(PgrTheme.A(PgrArt.GoldLine, 220)); canvas.DrawPath(path, paint);
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrArt.TideRed); Diamond(canvas, l + w * .3f, y0, D(6));
        paint.SetStyle(Paint.Style.Stroke); paint.Color = C(Color.White); Diamond(canvas, l + w * .64f, y0, D(5)); 
        paint.Color = C(PgrArt.GoldLine); Diamond(canvas, l + w * .82f, y1, D(5));
    }
    void Diamond(Canvas canvas, float cx, float cy, float s)
    {
        path.Reset(); path.MoveTo(cx, cy - s); path.LineTo(cx + s, cy); path.LineTo(cx, cy + s); path.LineTo(cx - s, cy); path.Close(); canvas.DrawPath(path, paint);
    }
}

/// <summary>11-B2 volume console dial: outer ring of 40 ticks, brass arc for the current value, faint Babylonia orbit.</summary>
public sealed class VolumeRingDrawable(float density) : PgrShapeDrawable(density)
{
    public float Value { get; set; }
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        float cx = b.ExactCenterX(), cy = b.ExactCenterY(), r = Math.Min(b.Width(), b.Height()) / 2f - D(3);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1);
        paint.Color = C(PgrTheme.A(PgrArt.MistLine, 70)); canvas.DrawCircle(cx, cy, r * .78f, paint);
        for (int i = 0; i < 40; i++)
        {
            double a = -Math.PI / 2 + Math.PI * 2 * i / 40; float inner = r - D(i % 10 == 0 ? 7 : 4);
            paint.Color = C(i < Math.Round(Value * 40) ? PgrArt.GoldLine : PgrTheme.A(PgrTheme.OnPlate, 90));
            canvas.DrawLine(cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner, cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r, paint);
        }
        paint.StrokeWidth = D(2); paint.Color = C(PgrArt.GoldLine);
        var oval = new RectF(cx - r * .9f, cy - r * .9f, cx + r * .9f, cy + r * .9f);
        canvas.DrawArc(oval, -90, 360 * Math.Clamp(Value, 0, 1), false, paint);
    }
}

/// <summary>Horizontal dashed hairline (12-A DANGER ZONE divider).</summary>
public sealed class DashLineDrawable(float density, Color color) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(color);
        for (float x = b.Left; x < b.Right; x += D(7)) canvas.DrawRect(x, b.CenterY() - D(.6f), Math.Min(b.Right, x + D(4)), b.CenterY() + D(.6f), paint);
    }
}

/// <summary>
/// 07 empty state (no chapter): placeholder viewfinder in the 08-B2 empty-state language — dashed grey frame, crimson corner
/// brackets, faint scan lines, two dashed text bars and a small crosshair. Decoration only.
/// </summary>
public sealed class EmptyScanDrawable(float density) : PgrShapeDrawable(density)
{
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float l = b.Left + D(4), t = b.Top + D(4), r = b.Right - D(4), btm = b.Bottom - D(4);
        paint.Reset(); paint.AntiAlias = true;
        // Scan lines.
        paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.A(PgrTheme.Muted, 22));
        for (float y = t + D(4); y < btm; y += D(4)) canvas.DrawRect(l + D(2), y, r - D(2), y + D(.6f), paint);
        // Dashed frame.
        paint.Color = C(PgrTheme.A(PgrTheme.Muted, 110));
        for (float x = l; x < r; x += D(7)) { canvas.DrawRect(x, t, Math.Min(r, x + D(4)), t + D(1), paint); canvas.DrawRect(x, btm - D(1), Math.Min(r, x + D(4)), btm, paint); }
        for (float y = t; y < btm; y += D(7)) { canvas.DrawRect(l, y, l + D(1), Math.Min(btm, y + D(4)), paint); canvas.DrawRect(r - D(1), y, r, Math.Min(btm, y + D(4)), paint); }
        // Corner brackets.
        float arm = Math.Min(D(16), Math.Min(r - l, btm - t) / 3), w = D(2);
        paint.Color = C(PgrTheme.A(PgrTheme.Red, 210));
        canvas.DrawRect(l, t, l + arm, t + w, paint); canvas.DrawRect(l, t, l + w, t + arm, paint);
        canvas.DrawRect(r - arm, t, r, t + w, paint); canvas.DrawRect(r - w, t, r, t + arm, paint);
        canvas.DrawRect(l, btm - w, l + arm, btm, paint); canvas.DrawRect(l, btm - arm, l + w, btm, paint);
        canvas.DrawRect(r - arm, btm - w, r, btm, paint); canvas.DrawRect(r - w, btm - arm, r, btm, paint);
        // Placeholder text bars (dashed, like 08's empty rows).
        float cy = (t + btm) / 2, bl = l + D(22);
        paint.Color = C(PgrTheme.A(PgrTheme.Muted, 34));
        canvas.DrawRect(bl, cy - D(10), bl + (r - l) * .52f, cy - D(3), paint);
        canvas.DrawRect(bl, cy + D(5), bl + (r - l) * .34f, cy + D(12), paint);
        // Crosshair on the right.
        float cx = r - D(34), rad = D(9);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = D(1.2f); paint.Color = C(PgrTheme.A(PgrTheme.Red, 150));
        canvas.DrawCircle(cx, cy, rad, paint);
        canvas.DrawLine(cx - rad - D(5), cy, cx - D(3), cy, paint); canvas.DrawLine(cx + D(3), cy, cx + rad + D(5), cy, paint);
        canvas.DrawLine(cx, cy - rad - D(5), cx, cy - D(3), paint); canvas.DrawLine(cx, cy + D(3), cx, cy + rad + D(5), paint);
        paint.SetStyle(Paint.Style.Fill);
    }
}
