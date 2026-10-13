using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Orientation = Android.Widget.Orientation;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// Single source of truth for the "Gray Raven archive terminal" look: tokens (colour, type, spacing,
/// chamfer geometry) and the style functions every code-built view goes through. XML resources in
/// Resources/values mirror these tokens for the platform theme only.
/// </summary>
public static class PgrTheme
{
    // ── Palette C1 「冷灰白」: cool grey-white base, translucent white cards, ink text ────────
    // Every colour in the app comes from this block (mirrored in Resources/values/colors.xml).
    public static readonly Color Void = Color.ParseColor("#E8EBEE");          // page base
    public static readonly Color HeaderStart = Color.ParseColor("#DDE2E7");   // header diagonal gradient
    public static readonly Color HeaderEnd = Color.ParseColor("#F2F3F5");
    public static readonly Color Background = Color.ParseColor("#F2F3F5");    // dialogs / editors
    public static readonly Color Panel = Color.Argb(184, 0xFF, 0xFF, 0xFF);   // cards: white at ~72%
    public static readonly Color Raised = Color.Argb(224, 0xFF, 0xFF, 0xFF);  // buttons, raised cards
    public static readonly Color Pressed = Color.ParseColor("#DDE2E7");       // pressed / disabled wash
    public static readonly Color Border = Color.ParseColor("#CBD1D7");        // hairlines
    public static readonly Color BorderStrong = Color.ParseColor("#AEB6BF");  // emphasised hairlines
    // ── Text (AA on base #E8EBEE and on cards) ─────────────────────────────────────────────
    public static readonly Color Foreground = Color.ParseColor("#1D232A");    // 13:1
    public static readonly Color Secondary = Color.ParseColor("#4B5560");     // 6.3:1
    public static readonly Color Muted = Color.ParseColor("#5C6670");         // 4.9:1
    // ── Muted crimson: primary button, name-plate underline, triangle markers, selection bar, now-playing ─
    public static readonly Color Red = Color.ParseColor("#A3283A");
    public static readonly Color RedText = Color.ParseColor("#9C2536");       // 6.4:1
    public static readonly Color RedFill = Color.ParseColor("#A3283A");       // white text 7.2:1
    public static readonly Color RedDeep = Color.ParseColor("#6E1B28");
    public static readonly Color Selection = Color.ParseColor("#F6E9EB");     // selected fill (crimson wash)
    public static readonly Color SelectionLine = Color.ParseColor("#D9AEB5");
    // ── Sparse decorative accents (small elements only) ───────────────────────────────────
    public static readonly Color Steel = Color.ParseColor("#44607D");         // mist/slate blue text: readouts, codes (5.5:1)
    public static readonly Color Slate = Color.ParseColor("#4F6B8A");         // slate strokes / marks
    public static readonly Color Cyan = Color.ParseColor("#3E7C7A");          // teal-grey lines, corner ticks
    public static readonly Color TealText = Color.ParseColor("#356C6A");      // teal-grey text (5.0:1)
    public static readonly Color Brass = Color.ParseColor("#B08A3E");         // branch / route marks (not for text)
    public static readonly Color BrassText = Color.ParseColor("#7E6128");     // branch / route text (4.8:1)
    public static readonly Color Ice = Color.ParseColor("#FFFFFF");           // highlights
    // ── Name plate (dark tab with light text, crimson underline) ──────────────────────────
    public static readonly Color NamePlate = Color.ParseColor("#2A3139");
    public static readonly Color OnPlate = Color.ParseColor("#F2F3F5");       // 11.8:1 on the plate
    // ── Depth ───────────────────────────────────────────────────────────────────────────────
    public static readonly Color RedGlow = Color.Argb(70, 0xA3, 0x28, 0x3A);  // the one subtle glow (primary / now-playing)
    public static readonly Color CyanGlow = Color.Argb(60, 0x3E, 0x7C, 0x7A);
    public static readonly Color CardShadow = Color.Argb(38, 0x1D, 0x23, 0x2A);
    public static readonly Color SheenLight = Color.Argb(110, 0xFF, 0xFF, 0xFF); // glass top highlight
    public static readonly Color ScanLine = Color.Argb(0, 0, 0, 0);
    // ── Contrast inks ─────────────────────────────────────────────────────────────────────
    /// <summary>Decorative "ink" drawn at low alpha over the base: grid, rings, rulers, outlined numerals, ripples, grain.</summary>
    public static readonly Color Ink = Color.ParseColor("#1D232A");
    /// <summary>Text and icons placed on a crimson fill (primary buttons).</summary>
    public static readonly Color OnAccent = Color.ParseColor("#FFFFFF");
    /// <summary>Darkening scrim for editors/overlays drawn over screenshots or the game.</summary>
    public static readonly Color Shade = Color.ParseColor("#000000");
    public static readonly Color TopLight = HeaderStart;
    public static readonly Color TabSelected = Color.Argb(235, 0xFF, 0xFF, 0xFF);
    // ── Bottom navigation (dark slate plate) & script list ─────────────────────────────────
    public static readonly Color NamePlateLight = Color.ParseColor("#38414C");  // nav plate gradient end
    public static readonly Color NavText = Color.ParseColor("#C3CBD4");         // ≥6.3:1 across the nav plate
    public static readonly Color NavSub = Color.ParseColor("#AAB4BF");          // ≥4.9:1 across the nav plate
    public static readonly Color MistTint = Color.Argb(14, 0x4F, 0x6B, 0x8A);   // alternating list rows
    public static readonly Color BrassTint = Color.Argb(34, 0xB0, 0x8A, 0x3E);  // branch / route rows
    static readonly Color[] SpeakerAccents = { Slate, Cyan, Steel, BorderStrong };
    /// <summary>Stable per-character accent edge (slate / teal-grey / steel / grey); narration stays a hairline.</summary>
    public static Color SpeakerAccent(string? speaker) => string.IsNullOrWhiteSpace(speaker) || speaker.Trim() == "旁白"
        ? Border : SpeakerAccents[PortraitStore.Hash(speaker.Trim()) % (uint)SpeakerAccents.Length];
    /// <summary>Default icon tint: brass marks branch / route icons, everything else uses secondary ink.</summary>
    public static Color IconColor(string icon) => icon == "branch" ? Brass : Secondary;
    /// <summary>Same colour with a new alpha (0–255). Use with tokens, never with literals.</summary>
    public static Color A(Color c, int alpha) => Color.Argb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);
    // ── Overlay glass over the game ────────────────────────────────────────────────────────
    public static readonly Color Glass = Color.Argb(238, 0xF2, 0xF3, 0xF5);
    public static readonly Color GlassStrong = Color.Argb(248, 0xF2, 0xF3, 0xF5);
    public static readonly Color GlassRaised = Color.Argb(240, 0xFF, 0xFF, 0xFF);
    public static readonly Color GlassLine = Color.Argb(220, 0xAE, 0xB6, 0xBF);

    // ── Type scale (sp) ───────────────────────────────────────────────────────────────────
    public const int TextDisplay = 26, TextTitle = 21, TextHeading = 17, TextBody = 15, TextSmall = 13,
        TextCaption = 12, TextLabel = 11, TextMicro = 10;
    // ── Spacing (dp) ──────────────────────────────────────────────────────────────────────
    public const int SpaceXS = 4, SpaceS = 8, SpaceM = 12, SpaceL = 16, SpaceXL = 24;
    // ── Geometry (dp) ─────────────────────────────────────────────────────────────────────
    public const float CutLarge = 14, CutMedium = 9, CutSmall = 6, Hairline = .75f, AccentBar = 3;
    public const int Touch = 48, GridStep = 28, GridAlpha = 6, HatchAlpha = 11;
    public const float GlowRadius = 5, ScanGap = 3;

    // ── Typefaces (system only: Roboto / Noto Sans CJK / Droid Sans Mono) ──────────────────
    public static readonly Typeface Sans = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    public static readonly Typeface SansMedium = Typeface.Create("sans-serif-medium", TypefaceStyle.Normal)!;
    public static readonly Typeface SansBold = Typeface.Create("sans-serif", TypefaceStyle.Bold)!;
    public static readonly Typeface Condensed = Typeface.Create("sans-serif-condensed", TypefaceStyle.Bold)!;
    public static readonly Typeface CondensedRegular = Typeface.Create("sans-serif-condensed", TypefaceStyle.Normal)!;
    public static readonly Typeface Mono = Typeface.Monospace!;

    public static int Dp(Context context, float value) => (int)(value * context.Resources!.DisplayMetrics!.Density + .5f);
    static float Px(Context context, float value) => value * context.Resources!.DisplayMetrics!.Density;

    // ── Surfaces ──────────────────────────────────────────────────────────────────────────
    /// <summary>Generic chamfered panel factory; every surface in the app is built here.</summary>
    public static ChamferDrawable Plate(Context context, Color fill, Color? stroke = null, float cut = CutMedium,
        Corners cuts = Corners.Diagonal, Color? accent = null, AccentEdge edge = AccentEdge.Left, float accentLength = 0,
        Color? brackets = null, Color? hatch = null, float hatchWidth = 0, Color? glow = null, Color? sheen = null,
        Color? scan = null, float notch = 0, float inset = 0, float glowRadius = GlowRadius, bool shadow = false) => new(new ChamferState
    {
        Glow = glow ?? Color.Transparent, GlowRadius = Px(context, glowRadius), Sheen = sheen ?? Color.Transparent,
        Scan = scan ?? Color.Transparent, ScanGap = Px(context, ScanGap), Inset = Px(context, shadow && inset == 0 ? 3 : inset),
        Shadow = shadow ? CardShadow : Color.Transparent, ShadowRadius = shadow ? Px(context, 5) : 0, ShadowDy = shadow ? Px(context, 1.5f) : 0,
        NotchOffset = Px(context, 18), NotchWidth = Px(context, notch), NotchDepth = notch > 0 ? Px(context, 3) : 0,
        Fill = fill, Stroke = stroke ?? Color.Transparent, StrokeWidth = Math.Max(1, Px(context, Hairline)),
        Cut = Px(context, cut), Cuts = cuts,
        Accent = accent ?? Color.Transparent, AccentEdge = accent == null ? AccentEdge.None : edge,
        AccentThickness = Px(context, edge == AccentEdge.Left ? AccentBar : 2), AccentLength = Px(context, accentLength),
        Brackets = brackets ?? Color.Transparent, BracketLength = Px(context, 9),
        Hatch = hatch ?? Color.Transparent, HatchWidth = Px(context, hatchWidth), HatchGap = Px(context, 5)
    });

    /// <summary>Card / panel surface with an optional crimson signal bar on the leading edge.</summary>
    public static Drawable Surface(Context context, Color fill, Color? stroke = null, Color? accent = null) =>
        Plate(context, fill, stroke ?? Border, CutMedium, Corners.Diagonal, accent, AccentEdge.Left);

    /// <summary>Standard content card. Selected cards gain a crimson bar, warm stroke and HUD brackets.</summary>
    public static Drawable Card(Context context, bool selected = false, bool raised = false) =>
        Plate(context, selected ? Selection : raised ? Raised : Panel, selected ? SelectionLine : Border, CutMedium,
            Corners.Diagonal, selected ? Red : null, AccentEdge.Left, brackets: selected ? Red : null,
            glow: selected ? RedGlow : null, sheen: SheenLight, shadow: !selected);

    /// <summary>Pressable card (list rows, chapter cards) with crimson-tinted ripple.</summary>
    public static Drawable PressableCard(Context context, bool selected = false, bool raised = false) =>
        Pressable(context, Card(context, selected, raised), CutMedium, Corners.Diagonal, selected ? RedRipple : NeutralRipple);

    public static readonly Color NeutralRipple = A(Ink, 36);
    public static readonly Color RedRipple = A(Red, 70);

    public static Drawable Pressable(Context context, Drawable content, float cut, Corners cuts, Color ripple) =>
        new RippleDrawable(ColorStateList.ValueOf(ripple), content, Plate(context, Color.White, null, cut, cuts));

    // ── Text roles ────────────────────────────────────────────────────────────────────────
    public enum Role { Display, Title, Heading, Body, BodyStrong, Small, Caption, Eyebrow, Code, Number }

    public static T Apply<T>(T view, Role role, Color? color = null) where T : TextView
    {
        switch (role)
        {
            case Role.Display: view.TextSize = TextDisplay; view.SetTypeface(SansBold, TypefaceStyle.Bold); view.LetterSpacing = .02f; break;
            case Role.Title: view.TextSize = TextTitle; view.SetTypeface(SansBold, TypefaceStyle.Bold); view.LetterSpacing = .02f; break;
            case Role.Heading: view.TextSize = TextHeading; view.SetTypeface(SansBold, TypefaceStyle.Bold); view.LetterSpacing = .01f; break;
            case Role.Body: view.TextSize = TextBody; view.SetTypeface(Sans, TypefaceStyle.Normal); break;
            case Role.BodyStrong: view.TextSize = TextBody; view.SetTypeface(SansMedium, TypefaceStyle.Normal); break;
            case Role.Small: view.TextSize = TextSmall; view.SetTypeface(Sans, TypefaceStyle.Normal); break;
            case Role.Caption: view.TextSize = TextCaption; view.SetTypeface(Sans, TypefaceStyle.Normal); break;
            case Role.Eyebrow: view.TextSize = TextMicro; view.SetTypeface(Condensed, TypefaceStyle.Bold); view.LetterSpacing = .16f; view.SetAllCaps(true); break;
            case Role.Code: view.TextSize = TextLabel; view.SetTypeface(Mono, TypefaceStyle.Normal); view.LetterSpacing = .04f; break;
            case Role.Number: view.SetTypeface(Condensed, TypefaceStyle.Bold); view.FontFeatureSettings = "tnum"; view.LetterSpacing = -.01f; break;
        }
        view.SetTextColor(color ?? role switch
        {
            Role.Display or Role.Title or Role.Heading or Role.Body or Role.BodyStrong or Role.Number => Foreground,
            Role.Eyebrow or Role.Code => Muted,
            _ => Secondary
        });
        view.SetIncludeFontPadding(role is not (Role.Eyebrow or Role.Code or Role.Number));
        return view;
    }

        /// <summary>Tabular, monospace-like figures for chapter numbers, line IDs and timers.</summary>
    public static void Tabular(TextView view) => view.FontFeatureSettings = "tnum";

    // ── Composite headers ─────────────────────────────────────────────────────────────────
    /// <summary>Crimson right-pointing triangle marker (eyebrows, rules).</summary>
    public static Drawable Triangle(Context context, int widthDp = 6, int heightDp = 8)
    {
        var path = new global::Android.Graphics.Path(); path.MoveTo(0, 0); path.LineTo(widthDp, heightDp / 2f); path.LineTo(0, heightDp); path.Close();
        var shape = new global::Android.Graphics.Drawables.ShapeDrawable(new global::Android.Graphics.Drawables.Shapes.PathShape(path, widthDp, heightDp));
        shape.Paint!.Color = Red; shape.SetIntrinsicWidth(Dp(context, widthDp)); shape.SetIntrinsicHeight(Dp(context, heightDp));
        return shape;
    }

    /// <summary>Small uppercase English code led by a crimson triangle marker, e.g. "▶ STORY · CH.03".</summary>
    public static TextView Eyebrow(Context context, string text, Color? color = null)
    {
        var view = Apply(new TextView(context) { Text = text }, Role.Eyebrow, color);
        view.SetCompoundDrawablesRelativeWithIntrinsicBounds(Triangle(context), null, null, null);
        view.CompoundDrawablePadding = Dp(context, 7);
        view.Gravity = GravityFlags.CenterVertical | GravityFlags.Start;
        return view;
    }

    /// <summary>Page header: English eyebrow code, Chinese title, optional Chinese caption.</summary>
    public static LinearLayout PageHeader(Context context, string code, string title, string? caption = null, bool compact = false)
    {
        var box = new LinearLayout(context) { Orientation = Orientation.Vertical };
        box.SetPadding(0, Dp(context, compact ? 4 : 10), 0, Dp(context, compact ? 4 : 8));
        box.AddView(Eyebrow(context, code), new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(context, compact ? 2 : 6) });
        var heading = Apply(new TextView(context) { Text = title }, compact ? Role.Heading : Role.Display);
        heading.SetMaxLines(3); heading.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        box.AddView(heading, new LinearLayout.LayoutParams(-1, -2));
        if (!string.IsNullOrEmpty(caption))
        {
            var note = Apply(new TextView(context) { Text = caption }, Role.Small);
            note.SetLineSpacing(Dp(context, 3), 1); note.SetPadding(0, Dp(context, 6), 0, 0);
            box.AddView(note, new LinearLayout.LayoutParams(-1, -2));
        }
        return box;
    }

    /// <summary>Section header: Chinese title + small English sublabel on a HUD rule.</summary>
    public static LinearLayout SectionHeader(Context context, string title, string? english = null, bool compact = false, string? readout = null)
    {
        var box = new LinearLayout(context) { Orientation = Orientation.Vertical };
        box.SetPadding(0, Dp(context, compact ? 8 : 18), 0, Dp(context, compact ? 4 : 8));
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.Bottom);
        var zh = Apply(new TextView(context) { Text = title }, Role.Heading);
        row.AddView(zh, new LinearLayout.LayoutParams(-2, -2));
        if (!string.IsNullOrEmpty(english))
        {
            var en = Apply(new WordFitText(context) { Text = english }, Role.Eyebrow); // drops instead of mid-word breaks (0.4.6)
            en.SetPadding(Dp(context, 8), 0, 0, Dp(context, 4));
            row.AddView(en, new LinearLayout.LayoutParams(0, -2, 1));
        }
        if (readout != null)
        {
            var code = new TextView(context) { Text = readout, TextSize = 10.5f }; code.SetTypeface(Condensed, TypefaceStyle.Bold);
            code.SetTextColor(Steel); code.LetterSpacing = .1f; Tabular(code); code.SetPadding(Dp(context, 6), 0, 0, Dp(context, 4));
            row.AddView(code, new LinearLayout.LayoutParams(-2, -2));
        }
        box.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        box.AddView(Rule(context), new LinearLayout.LayoutParams(-1, Dp(context, 6)) { TopMargin = Dp(context, 6) });
        return box;
    }

    /// <summary>HUD divider view.</summary>
    public static View Rule(Context context)
    {
        var rule = new View(context) { Background = new HudRuleDrawable(context.Resources!.DisplayMetrics!.Density) };
        rule.ImportantForAccessibility = ImportantForAccessibility.No;
        return rule;
    }

    /// <summary>Small status chip (cut bottom-right corner, hairline).</summary>
    public static TextView Chip(Context context, string text, bool active = false)
    {
        var chip = Apply(new TextView(context) { Text = text }, Role.Eyebrow, active ? Foreground : Steel);
        chip.SetAllCaps(false); chip.LetterSpacing = .08f;
        chip.Background = Plate(context, active ? Selection : A(Slate, 22), active ? SelectionLine : A(Slate, 140),
            CutSmall * .7f, Corners.BottomRight);
        chip.SetPadding(Dp(context, 7), Dp(context, 3), Dp(context, 8), Dp(context, 3));
        chip.Gravity = GravityFlags.Center;
        return chip;
    }

    // ── Buttons ───────────────────────────────────────────────────────────────────────────
    static ColorStateList TextStates(Color normal, Color? disabled = null) => new(
        new[] { new[] { -global::Android.Resource.Attribute.StateEnabled }, Array.Empty<int>() },
        new[] { (disabled ?? DisabledText).ToArgb(), normal.ToArgb() });

    /// <summary>0.4.7 P2: disabled label / icon colour shared by every button kind (same gray as a disabled normal key).</summary>
    public static readonly Color DisabledText = Color.Argb(150, 0x5C, 0x66, 0x70);

    /// <summary>0.4.7 P2: line icon that turns <see cref="DisabledText"/> gray when the button is disabled.</summary>
    public static Drawable StateIcon(string name, Color normal, float density, int sizeDp)
    {
        var states = new StateListDrawable();
        states.AddState(new[] { -global::Android.Resource.Attribute.StateEnabled }, new PgrIconDrawable(name, DisabledText, density, sizeDp));
        states.AddState(Array.Empty<int>(), new PgrIconDrawable(name, normal, density, sizeDp));
        return states;
    }

    static Drawable WithDisabled(Context context, Drawable normal, float cut, Corners cuts)
    {
        var states = new StateListDrawable();
        states.AddState(new[] { -global::Android.Resource.Attribute.StateEnabled }, Plate(context, Color.Argb(150, Panel.R, Panel.G, Panel.B), Border, cut, cuts));
        states.AddState(Array.Empty<int>(), normal);
        return states;
    }

    public static void StyleButton(Button button, bool selected = false, bool primary = false, bool quiet = false, bool alignStart = false)
    {
        var context = button.Context!;
        Drawable content; Color ripple = NeutralRipple; Corners cuts; float cut;
        if (primary)
        {
            cuts = Corners.Diagonal; cut = CutSmall;
            // Neon primary: the plate sits 2dp inside the view so its crimson bloom stays within the view clip.
            content = Plate(context, RedFill, null, cut, cuts, hatch: A(OnAccent, 46), hatchWidth: 22,
                glow: RedGlow, sheen: A(OnAccent, 40), inset: 2, glowRadius: 4);
            ripple = A(OnAccent, 60);
        }
        else if (selected)
        {
            cuts = Corners.BottomRight; cut = CutSmall;
            content = Plate(context, Selection, SelectionLine, cut, cuts, Red, AccentEdge.Left);
            ripple = RedRipple;
        }
        else if (quiet)
        {
            cuts = Corners.BottomRight; cut = CutSmall;
            content = Plate(context, Color.Transparent, null, cut, cuts);
        }
        else
        {
            cuts = Corners.BottomRight; cut = CutSmall;
            content = Plate(context, Raised, Border, cut, cuts, sheen: SheenLight);
        }
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(ripple), quiet ? content : WithDisabled(context, content, cut, cuts),
            Plate(context, Color.White, null, cut, cuts));
        button.SetTextColor(TextStates(primary ? OnAccent : selected ? Foreground : quiet ? Secondary : Foreground));
        button.RefreshDrawableState(); button.Background.JumpToCurrentState(); // 0.4.7 P2：已在布局中的按钮重设底板后同步启用状态
        button.SetAllCaps(false);
        button.SetSingleLine(false);
        button.Ellipsize = null;
        button.SetMinHeight(Dp(context, Touch));
        button.SetMinimumHeight(Dp(context, Touch));
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetPadding(Dp(context, alignStart ? 14 : 7), Dp(context, 11), Dp(context, alignStart ? 14 : 7), Dp(context, 11));
        button.Gravity = (alignStart ? GravityFlags.Start : GravityFlags.CenterHorizontal) | GravityFlags.CenterVertical;
        button.SetTypeface(selected || primary ? SansBold : SansMedium, selected || primary ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        button.LetterSpacing = .02f;
        button.StateListAnimator = null;
        button.Elevation = 0;
    }

    public enum OverlayTone { Quiet, Glass, Primary, Selected }

    /// <summary>
    /// Overlay (in-game) button skin: translucent glass so the game stays visible. Only background,
    /// text colour and face are set; callers keep their compact sizing.
    /// </summary>
    public static void StyleOverlayButton(Button button, OverlayTone tone)
    {
        var context = button.Context!;
        Drawable content = tone switch
        {
            OverlayTone.Primary => Plate(context, RedFill, null, CutSmall, Corners.Diagonal, hatch: A(OnAccent, 46), hatchWidth: 18),
            OverlayTone.Selected => Plate(context, Color.Argb(225, Selection.R, Selection.G, Selection.B), SelectionLine, CutSmall, Corners.BottomRight, Red, AccentEdge.Left),
            OverlayTone.Glass => Plate(context, GlassRaised, GlassLine, CutSmall, Corners.BottomRight),
            _ => Plate(context, Color.Transparent, null, CutSmall, Corners.BottomRight)
        };
        Corners cuts = tone == OverlayTone.Primary ? Corners.Diagonal : Corners.BottomRight;
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(tone == OverlayTone.Primary ? A(OnAccent, 60) : RedRipple),
            tone == OverlayTone.Quiet ? content : WithDisabled(context, content, CutSmall, cuts), Plate(context, Color.White, null, CutSmall, cuts));
        button.SetTextColor(TextStates(tone == OverlayTone.Primary ? OnAccent : tone == OverlayTone.Quiet ? Secondary : Foreground));
        button.SetTypeface(tone is OverlayTone.Primary or OverlayTone.Selected ? SansBold : SansMedium,
            tone is OverlayTone.Primary or OverlayTone.Selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0; button.LetterSpacing = .02f;
    }

    /// <summary>Bold first line + secondary preview line in one TextView (notification headers).</summary>
    public static Java.Lang.ICharSequence TitleWithPreview(string title, string preview, Color? titleColor = null, Color? previewColor = null)
    {
        var text = new global::Android.Text.SpannableStringBuilder(title);
        text.SetSpan(new global::Android.Text.Style.StyleSpan(TypefaceStyle.Bold), 0, title.Length, global::Android.Text.SpanTypes.ExclusiveExclusive);
        text.SetSpan(new global::Android.Text.Style.ForegroundColorSpan(titleColor ?? Foreground), 0, title.Length, global::Android.Text.SpanTypes.ExclusiveExclusive);
        if (preview.Length > 0)
        {
            int start = text.Length();
            text.Append("\n" + preview);
            text.SetSpan(new global::Android.Text.Style.ForegroundColorSpan(previewColor ?? Secondary), start, text.Length(), global::Android.Text.SpanTypes.ExclusiveExclusive);
        }
        return text;
    }

    /// <summary>Bottom/side navigation tab: transparent, crimson indicator bar when selected.</summary>
    public static void StyleTab(Button button, bool selected, bool horizontal)
    {
        var context = button.Context!;
        var content = selected
            ? Plate(context, TabSelected, null, 0, Corners.None, Red, horizontal ? AccentEdge.Left : AccentEdge.Top, horizontal ? 18 : 22,
                sheen: Color.Argb(26, Red.R, Red.G, Red.B))
            : Plate(context, Color.Transparent, null, 0, Corners.None);
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(RedRipple), content, Plate(context, Color.White, null, 0, Corners.None));
        button.SetTextColor(selected ? Foreground : Muted);
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0;
        button.SetTypeface(selected ? SansBold : SansMedium, selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        button.LetterSpacing = .04f;
        button.Selected = selected;
    }

    /// <summary>
    /// Bottom-nav tab on the dark slate plate: Chinese label with a condensed English sublabel, light chamfered folder tab
    /// with crimson tick when selected, optional status diamond. The sublabel is dropped in the compact (icon-left) layout.
    /// </summary>
    public static void StyleNavTab(Button button, bool selected, bool horizontal, string label, string english, Color? dot)
    {
        var context = button.Context!;
        var face = new NavTabDrawable(context.Resources!.DisplayMetrics!.Density) { Selected = selected, Dot = dot, Horizontal = horizontal };
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(selected ? RedRipple : A(OnPlate, 50)), face, new ColorDrawable(Color.White));
        if (horizontal) button.Text = label;
        else
        {
            var text = new global::Android.Text.SpannableString(label + "\n" + english);
            int start = label.Length + 1, end = text.Length();
            var flags = global::Android.Text.SpanTypes.ExclusiveExclusive;
            text.SetSpan(new global::Android.Text.Style.RelativeSizeSpan(.72f), start, end, flags);
            text.SetSpan(new global::Android.Text.Style.TypefaceSpan(Condensed), start, end, flags);
            text.SetSpan(new global::Android.Text.Style.ForegroundColorSpan(selected ? RedText : NavSub), start, end, flags);
            button.TextFormatted = text;
        }
        button.SetTextColor(selected ? Foreground : NavText);
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0;
        button.SetTypeface(selected ? SansBold : SansMedium, selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        button.LetterSpacing = .04f; button.SetLineSpacing(0, .92f);
        button.Selected = selected;
    }

    /// <summary>Zone header: crimson triangle, Chinese label, condensed English label, thin fading rule and a readout.</summary>
    public static LinearLayout ZoneHeader(Context context, string title, string english, out TextView readout, string? tag = null)
    {
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(new View(context) { Background = Triangle(context), ImportantForAccessibility = ImportantForAccessibility.No },
            new LinearLayout.LayoutParams(Dp(context, 6), Dp(context, 8)) { RightMargin = Dp(context, 7) });
        var zh = Apply(new TextView(context) { Text = title }, Role.BodyStrong); zh.TextSize = 13; zh.SetPadding(0, 0, 0, 0);
        row.AddView(zh);
        var en = new TextView(context) { Text = english, TextSize = 9.5f }; en.SetTypeface(Condensed, TypefaceStyle.Bold);
        en.SetTextColor(Muted); en.LetterSpacing = .14f; en.SetAllCaps(true); en.ImportantForAccessibility = ImportantForAccessibility.No;
        row.AddView(en, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(context, 7) });
        var rule = new View(context) { ImportantForAccessibility = ImportantForAccessibility.No };
        rule.Background = new GradientDrawable(GradientDrawable.Orientation.LeftRight!, new int[] { BorderStrong.ToArgb(), A(BorderStrong, 0).ToArgb() });
        row.AddView(rule, new LinearLayout.LayoutParams(0, Math.Max(1, Dp(context, .75f)), 1) { LeftMargin = Dp(context, 8), RightMargin = Dp(context, 8) });
        if (tag != null)
        {
            // Original squad emblem + callsign tag (decorative).
            row.AddView(new View(context) { Background = new EmblemDrawable(context.Resources!.DisplayMetrics!.Density), ImportantForAccessibility = ImportantForAccessibility.No },
                new LinearLayout.LayoutParams(Dp(context, 16), Dp(context, 18)) { RightMargin = Dp(context, 3) });
            var callsign = new TextView(context) { Text = tag, TextSize = 9 }; callsign.SetTypeface(Condensed, TypefaceStyle.Bold);
            callsign.SetTextColor(OnPlate); callsign.LetterSpacing = .1f; callsign.ImportantForAccessibility = ImportantForAccessibility.No;
            callsign.Background = new AngledPlateDrawable(context.Resources!.DisplayMetrics!.Density) { Fill = NamePlate, Stroke = null, SlantDp = 4 };
            callsign.SetPadding(Dp(context, 8), Dp(context, 2), Dp(context, 8), Dp(context, 2));
            row.AddView(callsign, new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(context, 6) });
        }
        readout = new TextView(context) { TextSize = 10.5f }; readout.SetTypeface(Condensed, TypefaceStyle.Bold);
        readout.SetTextColor(Steel); readout.LetterSpacing = .1f; Tabular(readout);
        // Own light plate so the readout stays AA over a header cover strip.
        readout.Background = Plate(context, Raised, null, CutSmall * .6f, Corners.BottomRight);
        readout.SetPadding(Dp(context, 5), Dp(context, 2), Dp(context, 5), Dp(context, 2));
        row.AddView(readout);
        row.SetPadding(Dp(context, 4), Dp(context, 6), Dp(context, 2), Dp(context, 6));
        return row;
    }

    /// <summary>Action chip with angled plate ends, a thin slate cap and a line icon: compact visual plate inside a 48dp hit target.</summary>
    public static void StyleChipButton(Button button, string icon, Color? iconColor = null)
    {
        var context = button.Context!;
        float density = context.Resources!.DisplayMetrics!.Density;
        int v = Dp(context, 6);
        AngledPlateDrawable Face(Color fill, Color? cap) => new(density) { Fill = fill, Stroke = Border, SlantDp = 6, Cap = cap, CapDp = 4 };
        var states = new StateListDrawable();
        states.AddState(new[] { -global::Android.Resource.Attribute.StateEnabled }, new InsetDrawable(Face(A(Panel, 150), A(Slate, 70)), 0, v, 0, v));
        states.AddState(Array.Empty<int>(), new InsetDrawable(Face(Raised, iconColor == Brass ? Brass : Slate), 0, v, 0, v));
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(NeutralRipple), states,
            new InsetDrawable(new AngledPlateDrawable(density) { Fill = Color.White, Stroke = null, SlantDp = 6 }, 0, v, 0, v));
        button.SetTextColor(TextStates(Foreground)); button.TextSize = 12;
        button.SetAllCaps(false); button.StateListAnimator = null; button.Elevation = 0; button.LetterSpacing = .02f;
        button.SetTypeface(SansMedium, TypefaceStyle.Normal);
        button.SetMinHeight(Dp(context, Touch)); button.SetMinimumHeight(Dp(context, Touch)); button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.SetPadding(Dp(context, 14), 0, Dp(context, 12), 0);
        button.Gravity = GravityFlags.Center;
        button.SetCompoundDrawablesRelativeWithIntrinsicBounds(new PgrIconDrawable(icon, iconColor ?? Slate, density, 15), null, null, null);
        button.CompoundDrawablePadding = Dp(context, 6);
    }

    /// <summary>Dropdown restyled as an angled name plate: dark leading cap (icon) with crimson underline, light body.</summary>
    public static void StyleNamePlateDropdown(Button button, string icon)
    {
        var context = button.Context!;
        float density = context.Resources!.DisplayMetrics!.Density;
        AngledPlateDrawable Face(Color fill) => new(density) { Fill = fill, Stroke = BorderStrong, SlantDp = 8, Cap = NamePlate, CapDp = 40, CapUnderline = true };
        var states = new StateListDrawable();
        states.AddState(new[] { -global::Android.Resource.Attribute.StateEnabled }, Face(A(Panel, 150)));
        states.AddState(Array.Empty<int>(), Face(Raised));
        button.BackgroundTintList = null;
        button.Background = new RippleDrawable(ColorStateList.ValueOf(NeutralRipple), states, new AngledPlateDrawable(density) { Fill = Color.White, Stroke = null, SlantDp = 8 });
        button.SetCompoundDrawablesWithIntrinsicBounds(new PgrIconDrawable(icon, OnPlate, density, 18), null, new PgrIconDrawable("chevron_down", Muted, density, 16), null);
        button.CompoundDrawablePadding = Dp(context, 22);
        button.SetPadding(Dp(context, 14), Dp(context, 11), Dp(context, 18), Dp(context, 11));
    }

    // ── Inputs & controls ─────────────────────────────────────────────────────────────────
    public static void StyleInput(EditText input)
    {
        var context = input.Context!;
        input.SetTextColor(Foreground);
        input.SetHintTextColor(Muted);
        input.TextSize = TextBody;
        input.BackgroundTintList = null;
        var states = new StateListDrawable();
        states.AddState(new[] { global::Android.Resource.Attribute.StateFocused },
            Plate(context, Panel, BorderStrong, CutSmall, Corners.BottomRight, Red, AccentEdge.Bottom));
        states.AddState(Array.Empty<int>(), Plate(context, Panel, Border, CutSmall, Corners.BottomRight));
        input.Background = states;
        input.SetPadding(Dp(context, 14), Dp(context, 12), Dp(context, 14), Dp(context, 12));
        input.SetMinimumHeight(Dp(context, 52));
        input.SetHighlightColor(Color.Argb(110, Red.R, Red.G, Red.B));
    }

    static Drawable CheckablePlate(Context context)
    {
        var states = new StateListDrawable();
        states.AddState(new[] { global::Android.Resource.Attribute.StateChecked },
            new RippleDrawable(ColorStateList.ValueOf(RedRipple), Plate(context, Selection, SelectionLine, CutSmall, Corners.BottomRight, Red, AccentEdge.Left), Plate(context, Color.White, null, CutSmall, Corners.BottomRight)));
        states.AddState(Array.Empty<int>(),
            new RippleDrawable(ColorStateList.ValueOf(NeutralRipple), Plate(context, Panel, Border, CutSmall, Corners.BottomRight), Plate(context, Color.White, null, CutSmall, Corners.BottomRight)));
        return states;
    }

    static ColorStateList Checked(Color on, Color off) => new(
        new[] { new[] { global::Android.Resource.Attribute.StateChecked }, Array.Empty<int>() },
        new[] { on.ToArgb(), off.ToArgb() });

    public static void StyleSwitch(Switch toggle)
    {
        var context = toggle.Context!;
        toggle.SetTextColor(Foreground); toggle.TextSize = TextBody;
        toggle.SetTypeface(SansMedium, TypefaceStyle.Normal);
        // SPEC 切角开关: chamfered track + diamond thumb (own state colours, so no tint).
        float den = context.Resources!.DisplayMetrics!.Density;
        toggle.ThumbTintList = null; toggle.TrackTintList = null;
        toggle.ThumbDrawable = new DiamondSwitchThumb(den); toggle.TrackDrawable = new ChamferSwitchTrack(den);
        toggle.SwitchMinWidth = Dp(context, 42); toggle.ThumbTextPadding = 0; toggle.RefreshDrawableState();
        toggle.Background = CheckablePlate(context);
        toggle.SetMinimumHeight(Dp(context, 52));
        toggle.SetPadding(Dp(context, 14), Dp(context, 8), Dp(context, 12), Dp(context, 8));
        toggle.SwitchPadding = Dp(context, 12);
    }

    public static void StyleRadio(RadioButton option)
    {
        var context = option.Context!;
        option.SetTextColor(Foreground); option.TextSize = TextBody;
        option.SetTypeface(SansMedium, TypefaceStyle.Normal);
        option.ButtonTintList = Checked(Red, Muted);
        option.Background = CheckablePlate(context);
        option.SetMinimumHeight(Dp(context, 52));
        option.SetPadding(Dp(context, 10), Dp(context, 6), Dp(context, 10), Dp(context, 6));
    }

    public static void StyleSeekBar(SeekBar bar)
    {
        var context = bar.Context!;
        bar.ProgressDrawable = Segmented(context);
        bar.ProgressTintList = null; bar.ProgressBackgroundTintList = null;
        var thumb = new GradientDrawable(); thumb.SetShape(ShapeType.Rectangle);
        thumb.SetColor(TextStates(Ice, Muted)); thumb.SetSize(Dp(context, 4), Dp(context, 22));
        thumb.SetStroke(Dp(context, 1), Color.Argb(160, Red.R, Red.G, Red.B));
        bar.SetThumb(thumb);
        bar.ThumbTintList = null;
        bar.SplitTrack = false;
        bar.SetMinimumHeight(Dp(context, Touch));
    }

    public static void StyleProgress(ProgressBar bar)
    {
        if (!bar.Indeterminate) { bar.ProgressDrawable = Segmented(bar.Context!); bar.ProgressTintList = null; bar.ProgressBackgroundTintList = null; }
        bar.IndeterminateTintList = ColorStateList.ValueOf(Red);
    }

    /// <summary>Segmented holographic track: dim cyan cells behind, glowing crimson cells clipped to progress.</summary>
    public static Drawable Segmented(Context context)
    {
        float density = context.Resources!.DisplayMetrics!.Density;
        var track = new HudSegmentDrawable(density, Border, Color.Transparent);
        var fill = new ClipDrawable(new HudSegmentDrawable(density, Red, Color.Transparent), GravityFlags.Left, ClipDrawableOrientation.Horizontal);
        var layers = new LayerDrawable(new Drawable[] { track, fill });
        layers.SetId(0, global::Android.Resource.Id.Background);
        layers.SetId(1, global::Android.Resource.Id.Progress);
        return layers;
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────────────────
    /// <summary>Chamfered dialog shell, crimson-ticked title and filled positive action.</summary>
    public static void StyleDialog(AlertDialog? dialog)
    {
        if (dialog?.Context is not { } context) return;
        int inset = Dp(context, 14);
        dialog.Window?.SetBackgroundDrawable(new InsetDrawable(
            Plate(context, Background, BorderStrong, CutLarge, Corners.Diagonal, Red, AccentEdge.Top, 36, brackets: Cyan,
                sheen: SheenLight, notch: 44), inset, inset, inset, inset));
        int titleId = context.Resources!.GetIdentifier("alertTitle", "id", "android");
        if (titleId != 0 && dialog.FindViewById<TextView>(titleId) is { } title)
        {
            Apply(title, Role.Heading); title.TextSize = 18;
            title.SetMaxLines(3); title.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        }
        if (dialog.FindViewById<TextView>(global::Android.Resource.Id.Message) is { } message)
        { message.SetTextColor(Foreground); message.TextSize = TextBody; message.SetLineSpacing(Dp(context, 4), 1); }
        if (dialog.ListView is { } list)
        {
            list.Divider = new ColorDrawable(Border); list.DividerHeight = Math.Max(1, Dp(context, Hairline));
            list.Selector = new ColorDrawable(Color.Transparent);
        }
        foreach (var which in new[] { (int)DialogButtonType.Positive, (int)DialogButtonType.Negative, (int)DialogButtonType.Neutral })
        {
            var button = dialog.GetButton(which); if (button == null) continue;
            bool positive = which == (int)DialogButtonType.Positive;
            StyleButton(button, primary: positive, quiet: !positive);
            button.SetSingleLine(true);
            button.SetPadding(Dp(context, 16), 0, Dp(context, 16), 0);
            if (button.LayoutParameters is ViewGroup.MarginLayoutParams margins)
            { margins.LeftMargin = Dp(context, 4); margins.RightMargin = Dp(context, 4); button.LayoutParameters = margins; }
        }
    }

    /// <summary>List adapter for chooser dialogs: index code, primary line, optional detail line.</summary>
    public sealed class ChoiceAdapter(Context context, IReadOnlyList<string> items, int selected = -1) : BaseAdapter<string>
    {
        public override int Count => items.Count;
        public override string this[int position] => items[position];
        public override long GetItemId(int position) => position;
        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var row = convertView as LinearLayout;
            if (row == null)
            {
                row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
                row.SetMinimumHeight(Dp(context, 52));
                row.SetPadding(Dp(context, 20), Dp(context, 10), Dp(context, 18), Dp(context, 10));
                var index = Apply(new TextView(context), Role.Code);
                index.SetPadding(0, Dp(context, 3), 0, 0);
                row.AddView(index, new LinearLayout.LayoutParams(Dp(context, 30), -2));
                var body = new LinearLayout(context) { Orientation = Orientation.Vertical };
                body.AddView(Apply(new TextView(context), Role.Body), new LinearLayout.LayoutParams(-1, -2));
                var detail = Apply(new TextView(context), Role.Caption); detail.SetPadding(0, Dp(context, 3), 0, 0);
                body.AddView(detail, new LinearLayout.LayoutParams(-1, -2));
                row.AddView(body, new LinearLayout.LayoutParams(0, -2, 1));
            }
            bool isSelected = position == selected;
            string text = items[position]; int split = text.IndexOf('\n');
            var code = (TextView)row.GetChildAt(0)!;
            code.Text = (position + 1).ToString("00"); code.SetTextColor(isSelected ? RedText : Muted);
            var lines = (LinearLayout)row.GetChildAt(1)!;
            var main = (TextView)lines.GetChildAt(0)!; var sub = (TextView)lines.GetChildAt(1)!;
            main.Text = split < 0 ? text : text[..split];
            main.SetTypeface(isSelected ? SansBold : Sans, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            sub.Text = split < 0 ? "" : text[(split + 1)..];
            sub.Visibility = split < 0 ? ViewStates.Gone : ViewStates.Visible;
            row.Background = new RippleDrawable(ColorStateList.ValueOf(RedRipple),
                isSelected ? Plate(context, Selection, null, 0, Corners.None, Red, AccentEdge.Left) : null,
                Plate(context, Color.White, null, 0, Corners.None));
            row.ContentDescription = (isSelected ? "当前选择，" : "") + text;
            return row;
        }
    }
}
