using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Provider;
using Android.Views;
using Path = Android.Graphics.Path;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// Speaker portraits and standees imported by the user live in the app's own external files folder (portraits/ and
/// standees/), named after the character ("莉瑟.png"). Avatars and chapter covers fall back to bundled assets
/// (BundledAvatars, BundledCovers) when nothing was imported; standees are never bundled.
/// Decoding is down-sampled and happens off the UI thread; results are cached in memory.
/// </summary>
public static class PortraitStore
{
    static readonly string[] Extensions = [".png", ".webp", ".jpg", ".jpeg"];
    static readonly object Gate = new();
    static readonly Dictionary<string, (long Stamp, Bitmap? Image)> Cache = new(StringComparer.Ordinal);
    static readonly LinkedList<string> Order = new();
    const int CacheLimit = 24;

    public const string PortraitFolder = "portraits", StandeeFolder = "standees", CoverFolder = "章节封面";

    public static string Folder(Context context, bool standee) => Folder(context, standee ? StandeeFolder : PortraitFolder);

    public static string Folder(Context context, string sub)
    {
        string root = context.GetExternalFilesDir(null)?.AbsolutePath ?? context.FilesDir!.AbsolutePath;
        string dir = System.IO.Path.Combine(root, sub);
        try { Directory.CreateDirectory(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return dir;
    }

    /// <summary>Candidate keys for a speaker: the name itself, then the name without a bracketed suffix ("莉瑟（通讯）" → "莉瑟").</summary>
    static IEnumerable<string> Keys(string name)
    {
        string n = Sanitize(name);
        if (n.Length > 0) yield return n;
        int cut = n.IndexOfAny(['（', '(', '·', ' ']);
        if (cut > 0) yield return n[..cut].Trim();
    }

    public static string Sanitize(string name)
    {
        var chars = (name ?? "").Trim().Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c) && c != '/' && c != '\\').ToArray();
        return new string(chars).Trim();
    }

    static string? Find(string folder, string name)
    {
        foreach (string key in Keys(name))
            foreach (string ext in Extensions)
            {
                string p = System.IO.Path.Combine(folder, key + ext);
                if (File.Exists(p)) return p;
            }
        return null;
    }

    /// <summary>Loads the image for <paramref name="name"/> (or null) and delivers it on the view's UI thread.</summary>
    public static void Request(View view, string? name, bool standee, int maxPx, Action<Bitmap?> deliver)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "旁白") { deliver(null); return; }
        if (standee) { RequestFrom(view, StandeeFolder, [name], maxPx, deliver); return; }
        // Avatars: the user's imported image wins; otherwise the bundled desktop-player set (BundledAvatars); otherwise null (monogram tile).
        var app = view.Context!.ApplicationContext ?? view.Context!;
        var names = BundledAvatars.CandidateNames(app, name).ToArray();
        RequestFrom(view, PortraitFolder, names.Length > 0 ? names : [name], maxPx, deliver, names, ctx => BundledAvatars.Find(ctx, names));
    }

    /// <summary>
    /// Chapter cover lookup: the user's imported 章节封面 (same chain as avatars, each candidate title in order) wins;
    /// otherwise the bundled cover for <paramref name="bundledKeys"/> (assets/covers, see BundledCovers); otherwise null
    /// (callers draw the decorated dark band).
    /// </summary>
    public static void RequestCover(View view, IEnumerable<string> titles, int maxPx, Action<Bitmap?> deliver, IEnumerable<string>? bundledKeys = null)
    {
        var names = titles.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToArray();
        var keys = bundledKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToArray() ?? [];
        if (names.Length == 0 && keys.Length == 0) { deliver(null); return; }
        RequestFrom(view, CoverFolder, names, maxPx, deliver, keys, keys.Length > 0 ? ctx => BundledCovers.Find(ctx, keys) : null);
    }

    static void RequestFrom(View view, string sub, string[] names, int maxPx, Action<Bitmap?> deliver, string[]? bundledKeys = null, Func<Context, string?>? bundled = null)
    {
        string folder = Folder(view.Context!, sub);
        var app = view.Context!.ApplicationContext ?? view.Context!;
        string cacheKey = sub + "|" + string.Join("/", names) + "|" + string.Join("/", bundledKeys ?? []) + "|" + maxPx;
        Task.Run(() =>
        {
            Bitmap? result = null;
            try
            {
                string? path = names.Select(n => Find(folder, n)).FirstOrDefault(p => p != null);
                string? asset = path == null && bundled != null ? bundled(app) : null;
                long stamp = path != null ? File.GetLastWriteTimeUtc(path).Ticks : asset != null ? -1 : 0;
                lock (Gate)
                    if (Cache.TryGetValue(cacheKey, out var hit) && hit.Stamp == stamp) { result = hit.Image; goto done; }
                result = path != null ? Decode(path, maxPx) : asset != null ? BundledCovers.Decode(app, asset, maxPx) : null;
                lock (Gate)
                {
                    Cache[cacheKey] = (stamp, result); Order.Remove(cacheKey); Order.AddFirst(cacheKey);
                    while (Order.Count > CacheLimit) { Cache.Remove(Order.Last!.Value); Order.RemoveLast(); }
                }
            }
            catch (Exception) { result = null; }
            done:
            var image = result;
            view.Post(() => deliver(image));
        });
    }

    static Bitmap? Decode(string path, int maxPx)
    {
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;
        int sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= maxPx) sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
        return BitmapFactory.DecodeFile(path, options);
    }

    /// <summary>Copies a user-picked image into the portrait/standee folder under its own file name. Returns the character name.</summary>
    public static string Import(Context context, global::Android.Net.Uri uri, string sub)
    {
        string fileName = "";
        using (var cursor = context.ContentResolver!.Query(uri, [IOpenableColumns.DisplayName], null, null, null))
            if (cursor != null && cursor.MoveToFirst()) fileName = cursor.GetString(0) ?? "";
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new InvalidDataException($"“{fileName}”不是 PNG / JPG / WebP 图片。");
        string name = Sanitize(System.IO.Path.GetFileNameWithoutExtension(fileName));
        if (name.Length == 0) throw new InvalidDataException("图片文件名需为角色名或章节名，例如“莉瑟.png”。");
        string folder = Folder(context, sub);
        foreach (string old in Extensions) { string p = System.IO.Path.Combine(folder, name + old); if (File.Exists(p)) File.Delete(p); }
        using var input = context.ContentResolver!.OpenInputStream(uri) ?? throw new IOException("无法读取所选图片。");
        using var output = File.Create(System.IO.Path.Combine(folder, name + ext));
        input.CopyTo(output);
        lock (Gate) { Cache.Clear(); Order.Clear(); }
        return name;
    }

    /// <summary>Deterministic, original decoration identifier (not game data): construct frame serial.</summary>
    public static string Serial(string? name) => "CN-" + (Hash(name) & 0xFFFF).ToString("X4");
    public static uint Hash(string? s) { uint h = 2166136261; foreach (char c in s ?? "") { h ^= c; h *= 16777619; } return h; }
}

/// <summary>
/// Chapter covers bundled in assets/covers (WebP + manifest.json). Keys are derived from the pack id and title the
/// same way the generator (android-impl/042/gen/build_covers.py) derives them from ChapterCatalog: "id:…", "main:N",
/// "er:N", "ex:N", "title:分类|标题", "title:*|标题". Used only after the user's imported 章节封面.
/// </summary>
public static class BundledCovers
{
    const string Root = "covers/";
    static readonly object Gate = new();
    static Dictionary<string, string>? keys;
    static readonly System.Text.RegularExpressions.Regex Prefix = new(@"^(第\s*\d+\s*章|EX\s*\d+|ER\s*\d+|\d+)[\s_·]*",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Lookup keys for a pack, most specific first.</summary>
    public static IEnumerable<string> Keys(string? packId, string? packTitle)
    {
        string id = (packId ?? "").Trim(), title = PgrVoice.ChapterCatalog.Title(packTitle ?? "");
        string category = PgrVoice.ChapterCatalog.Category(id, packTitle ?? "");
        if (id.Length > 0)
        {
            string lower = id.ToLowerInvariant(); yield return "id:" + lower;
            if (lower.StartsWith("voice-v2-", StringComparison.Ordinal)) yield return "id:" + lower[9..];
        }
        System.Text.RegularExpressions.Match m;
        if (category == "主线")
        {
            m = System.Text.RegularExpressions.Regex.Match(title, @"第\s*(\d+)\s*章");
            if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(title, @"^(\d+)\s");
            if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(id, @"^(?:voice-v2-)?(?:pgr-ch|MAIN)(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) yield return "main:" + int.Parse(m.Groups[1].Value);
        }
        else if (category == "浮点纪实")
        {
            m = System.Text.RegularExpressions.Regex.Match(id, @"^(?:voice-v2-)?ER(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(title, @"^ER\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) yield return "er:" + int.Parse(m.Groups[1].Value);
        }
        else if (category == "外篇剧情")
        {
            m = System.Text.RegularExpressions.Regex.Match(title, @"^EX\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) yield return "ex:" + int.Parse(m.Groups[1].Value);
        }
        string bare = Prefix.Replace(title, "").Trim();
        if (bare.Length > 0) { yield return "title:" + category + "|" + bare; yield return "title:*|" + bare; }
    }

    static Dictionary<string, string> Load(Context context)
    {
        lock (Gate)
        {
            if (keys != null) return keys;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                using var stream = context.Assets!.Open(Root + "manifest.json");
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var cover in doc.RootElement.GetProperty("covers").EnumerateObject())
                    if (cover.Value.TryGetProperty("file", out var file) && file.GetString() is { Length: > 0 } f) files[cover.Name] = f;
                foreach (var key in doc.RootElement.GetProperty("keys").EnumerateObject())
                    if (key.Value.GetString() is { } slug && files.TryGetValue(slug, out var f)) map[key.Name] = Root + f;
            }
            catch (Exception) { }
            return keys = map;
        }
    }

    /// <summary>Asset path of the first bundled cover matching <paramref name="lookupKeys"/>, or null.</summary>
    public static string? Find(Context context, IEnumerable<string> lookupKeys)
    {
        var map = Load(context);
        foreach (string key in lookupKeys) if (map.TryGetValue(key, out var asset)) return asset;
        return null;
    }

    public static Bitmap? Decode(Context context, string asset, int maxPx)
    {
        var assets = context.Assets!;
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        using (var probe = assets.Open(asset)) BitmapFactory.DecodeStream(probe, null, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;
        int sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= maxPx) sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
        using var input = assets.Open(asset);
        return BitmapFactory.DecodeStream(input, null, options);
    }
}

/// <summary>
/// Speaker avatars bundled in assets/avatars: exactly the desktop player's 角色头像 set (WebP, 192px squares) with its
/// aliases.json, generated by android-impl/043/gen/build_avatars.py. Name matching follows the desktop CharacterArt rules
/// (alias → full name → name without bracketed note → base name before 「·」), plus the Android cut at a space.
/// Used only after the user's own imported avatar.
/// </summary>
public static class BundledAvatars
{
    const string Root = "avatars/";
    static readonly object Gate = new();
    static Dictionary<string, string>? files, aliases;
    static readonly char[] Separators = ['·', '・', '•', '｜', '|'];

    static void Load(Context context)
    {
        lock (Gate)
        {
            if (files != null) return;
            var f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var stream = context.Assets!.Open(Root + "manifest.json");
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                foreach (var e in doc.RootElement.GetProperty("files").EnumerateObject())
                    if (e.Value.GetString() is { Length: > 0 } v) f[e.Name.Normalize(System.Text.NormalizationForm.FormC)] = Root + v;
                foreach (var e in doc.RootElement.GetProperty("aliases").EnumerateObject())
                    if (e.Value.GetString() is { Length: > 0 } v) a[e.Name.Trim()] = v.Trim();
            }
            catch (Exception) { }
            aliases = a; files = f;
        }
    }

    /// <summary>Desktop CharacterArt.SanitizeName: NFC, drop control and \/:*?"&lt;&gt;| characters, trim spaces, dots and ideographic space.</summary>
    public static string Sanitize(string? speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker)) return "";
        var sb = new System.Text.StringBuilder(speaker.Length);
        foreach (char c in speaker.Normalize(System.Text.NormalizationForm.FormC))
            if (!char.IsControl(c) && "\\/:*?\"<>|".IndexOf(c) < 0) sb.Append(c);
        return sb.ToString().Trim().Trim('.', ' ', '\u3000');
    }

    static string StripNote(string value)
    {
        string text = value;
        foreach (var (open, close) in new[] { ('（', '）'), ('(', ')'), ('【', '】'), ('[', ']') })
        {
            int start = text.IndexOf(open);
            if (start >= 0) { int end = text.IndexOf(close, start); text = (end > start ? text.Remove(start, end - start + 1) : text[..start]).Trim(); }
        }
        return text;
    }

    /// <summary>Candidate file names in priority order (desktop CharacterArt.CandidateNames with the bundled aliases).</summary>
    public static IEnumerable<string> CandidateNames(Context context, string? speaker)
    {
        Load(context);
        var names = new List<string>();
        void Add(string? value) { string n = Sanitize(value); if (n.Length > 0 && !names.Contains(n, StringComparer.OrdinalIgnoreCase)) names.Add(n); }
        string raw = (speaker ?? "").Trim();
        if (raw.Length == 0) return names;
        foreach (string key in new[] { raw, Sanitize(raw) }) if (aliases!.TryGetValue(key, out var m)) Add(m);
        Add(raw);
        string withoutNote = StripNote(raw);
        if (aliases!.TryGetValue(withoutNote, out var mn)) Add(mn);
        Add(withoutNote);
        int dot = withoutNote.IndexOfAny(Separators);
        if (dot > 0) { string b = withoutNote[..dot].Trim(); if (aliases.TryGetValue(b, out var mb)) Add(mb); Add(b); }
        int sp = withoutNote.IndexOf(' ');
        if (sp > 0) Add(withoutNote[..sp]);
        return names;
    }

    /// <summary>Asset path of the first bundled avatar matching one of <paramref name="names"/>, or null.</summary>
    public static string? Find(Context context, IEnumerable<string> names)
    {
        Load(context);
        foreach (string n in names) if (files!.TryGetValue(n, out var asset)) return asset;
        return null;
    }
}

/// <summary>
/// Speaker portrait in a cut-corner frame: crimson bloom stroke, cyan corner ticks, a serial strip, and the user's
/// image clipped to the frame. Without an image it shows an original geometric monogram (first glyph of the name).
/// </summary>
public sealed class PortraitFrameDrawable : Drawable
{
    readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
    readonly Path shape = new();
    readonly float d;
    int alpha = 255;
    string name = "";
    Bitmap? image;
    public PortraitFrameDrawable(float density) => d = density;
    public bool ShowSerial { get; set; } = true;
    /// <summary>Inactive frames (other squad members) use a steel stroke and a dimmed image.</summary>
    public bool Active { get => active; set { active = value; InvalidateSelf(); } }
    bool active = true;
    public string Name { get => name; set { name = value ?? ""; InvalidateSelf(); } }
    public Bitmap? Image { get => image; set { image = value; InvalidateSelf(); } }

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float inset = 3 * d, l = b.Left + inset, t = b.Top + inset, r = b.Right - inset, bo = b.Bottom - inset;
        float cut = Math.Min(10 * d, (r - l) * .22f);
        shape.Reset();
        shape.MoveTo(l + cut, t); shape.LineTo(r, t); shape.LineTo(r, bo - cut); shape.LineTo(r - cut, bo); shape.LineTo(l, bo); shape.LineTo(l, t + cut); shape.Close();
        uint h = PortraitStore.Hash(name);
        paint.SetStyle(Paint.Style.Fill); paint.Color = WithAlpha(PgrTheme.Raised, 255);
        canvas.DrawPath(shape, paint);
        canvas.Save(); canvas.ClipPath(shape);
        if (image is { IsRecycled: false } img)
        {
            // Centre-crop, biased to the top so faces stay in frame.
            float scale = Math.Max((r - l) / img.Width, (bo - t) / img.Height);
            float w = img.Width * scale, hgt = img.Height * scale;
            var dst = new RectF((l + r - w) / 2, t - (hgt - (bo - t)) * .15f, (l + r + w) / 2, t - (hgt - (bo - t)) * .15f + hgt);
            paint.Alpha = active ? alpha : alpha * 150 / 255; canvas.DrawBitmap(img, null, dst, paint); dst.Dispose();
        }
        else Monogram(canvas, l, t, r, bo, h);
        // Construct serial on a dark plate strip along the bottom edge.
        if (ShowSerial && r - l >= 44 * d)
        {
            paint.SetStyle(Paint.Style.Fill); paint.Color = WithAlpha(PgrTheme.NamePlate, 225);
            canvas.DrawRect(l, bo - 12 * d, r, bo, paint);
            paint.SetTypeface(PgrTheme.Mono); paint.TextSize = 7.5f * d; paint.LetterSpacing = .08f;
            paint.Color = WithAlpha(PgrTheme.OnPlate, 255);
            canvas.DrawText(PortraitStore.Serial(name), l + 5 * d, bo - 3.5f * d, paint); paint.LetterSpacing = 0;
        }
        canvas.Restore();
        // Frame: crimson bloom stroke + cyan corner ticks on the uncut corners.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d);
        paint.Color = active ? WithAlpha(PgrTheme.Red, 255) : WithAlpha(PgrTheme.BorderStrong, 255);
        canvas.DrawPath(shape, paint);
        if (!active) return;
        paint.Color = WithAlpha(PgrTheme.Cyan, 200); paint.StrokeWidth = Math.Max(1, 1.2f * d);
        float k = 6 * d, o = 1.5f * d;
        canvas.DrawLine(r - k, t - o, r + o, t - o, paint); canvas.DrawLine(r + o, t - o, r + o, t + k, paint);
        canvas.DrawLine(l - o, bo - k, l - o, bo + o, paint); canvas.DrawLine(l - o, bo + o, l + k, bo + o, paint);
    }

    void Monogram(Canvas canvas, float l, float t, float r, float b, uint h)
    {
        float w = r - l, cx = (l + r) / 2, cy = (t + b) / 2;
        // Diagonal hatch, seeded angle offset.
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, d * .6f); paint.Color = WithAlpha(PgrTheme.Border, 160);
        float gap = 5 * d, off = (h % 5) * d;
        for (float x = l - w + off; x < r; x += gap) canvas.DrawLine(x, b, x + w, t, paint);
        string glyph = Glyph(name);
        if (glyph.Length == 0) { PgrDeco.Emblem(canvas, cx, cy, w * .34f, d, paint, alpha); return; }
        // Hexagon cage in cyan, crimson slash, white glyph.
        paint.Color = WithAlpha(PgrTheme.Cyan, 120); paint.StrokeWidth = Math.Max(1, d * .8f);
        using (var hex = new Path())
        {
            float rad = w * .36f;
            for (int i = 0; i < 6; i++) { double a = Math.PI / 3 * i + Math.PI / 6; float x = cx + rad * (float)Math.Cos(a), y = cy + rad * (float)Math.Sin(a); if (i == 0) hex.MoveTo(x, y); else hex.LineTo(x, y); }
            hex.Close(); canvas.DrawPath(hex, paint);
        }
        paint.Color = WithAlpha(PgrTheme.Slate, 150); paint.StrokeWidth = 2 * d;
        canvas.DrawLine(cx + w * .18f, t + w * .08f, cx - w * .02f, b - w * .08f, paint);
        paint.SetStyle(Paint.Style.Fill); paint.SetTypeface(PgrTheme.SansBold); paint.TextSize = w * .42f; paint.TextAlign = Paint.Align.Center;
        paint.Color = WithAlpha(PgrTheme.Foreground, 255);
        canvas.DrawText(glyph, cx, cy + paint.TextSize * .36f, paint); paint.TextAlign = Paint.Align.Left;
    }

    static string Glyph(string name)
    {
        string n = (name ?? "").Trim();
        if (n.Length == 0 || n == "旁白") return "";
        return char.IsSurrogate(n[0]) && n.Length > 1 ? n[..2] : n[..1].ToUpperInvariant();
    }

    Color WithAlpha(Color c, int a) => Color.Argb(a * alpha / 255, c.R, c.G, c.B);
    public override void SetAlpha(int value) { alpha = Math.Clamp(value, 0, 255); InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); shape.Dispose(); } base.Dispose(disposing); }
}

/// <summary>
/// Optional semi-transparent character standee behind a card: the user's image anchored right, faded towards the
/// text side and the bottom so body text keeps AA contrast. Draws nothing without an image.
/// </summary>
public sealed class StandeeDrawable : Drawable
{
    readonly Paint paint = new(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);
    readonly Paint fade = new(PaintFlags.AntiAlias);
    readonly float d;
    Bitmap? image;
    public StandeeDrawable(float density) { d = density; fade.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstIn!)); }
    public Bitmap? Image { get => image; set { image = value; InvalidateSelf(); } }
    /// <summary>Peak opacity of the image (0–255).</summary>
    public int Strength { get; set; } = 84;

    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (image is not { IsRecycled: false } img || b.Width() <= 0 || b.Height() <= 0) return;
        float l = b.Left + 3 * d, t = b.Top + 3 * d, r = b.Right - 3 * d, bo = b.Bottom - 3 * d;
        float hgt = (bo - t) * 1.4f, w = img.Width * hgt / img.Height;
        int layer = canvas.SaveLayer(l, t, r, bo, null);
        var dst = new RectF(r - w + w * .08f, t, r + w * .08f, t + hgt);
        paint.Alpha = Strength; canvas.DrawBitmap(img, null, dst, paint); dst.Dispose();
        using (var hShader = new LinearGradient(r - Math.Min(w, (r - l) * .7f), 0, r - (r - l) * .12f, 0, Color.Transparent, Color.Black, Shader.TileMode.Clamp!))
        { fade.SetShader(hShader); canvas.DrawRect(l, t, r, bo, fade); }
        using (var vShader = new LinearGradient(0, bo - (bo - t) * .35f, 0, bo, Color.Black, Color.Transparent, Shader.TileMode.Clamp!))
        { fade.SetShader(vShader); canvas.DrawRect(l, t, r, bo, fade); }
        fade.SetShader(null);
        canvas.RestoreToCount(layer);
    }
    public override void SetAlpha(int value) { paint.Alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); fade.Dispose(); } base.Dispose(disposing); }
}

/// <summary>
/// Dialogue name plate, in the manner of an in-game dialogue box: a dark crimson tab with a vertical red marker on
/// the leading edge and a slanted trailing edge. Text padding leaves room for the slant.
/// </summary>
public sealed class NamePlateDrawable : Drawable
{
    readonly Paint paint = new(PaintFlags.AntiAlias);
    readonly Path path = new();
    readonly float d;
    public NamePlateDrawable(float density) => d = density;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0 || b.Height() <= 0) return;
        float slant = b.Height() * .45f;
        path.Reset(); path.MoveTo(b.Left, b.Top); path.LineTo(b.Right, b.Top); path.LineTo(b.Right - slant, b.Bottom); path.LineTo(b.Left, b.Bottom); path.Close();
        paint.SetStyle(Paint.Style.Fill); paint.Color = PgrTheme.NamePlate; canvas.DrawPath(path, paint);
        // Crimson underline along the plate's base.
        paint.Color = PgrTheme.Red; canvas.DrawRect(b.Left, b.Bottom - 2 * d, b.Right - slant, b.Bottom, paint);
    }
    public override void SetAlpha(int value) { paint.Alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) { paint.Dispose(); path.Dispose(); } base.Dispose(disposing); }
}

/// <summary>Thin rule under the name plate row: hairline across, small red square marker at the trailing end.</summary>
public sealed class DialogueRuleDrawable : Drawable
{
    readonly Paint paint = new(PaintFlags.AntiAlias);
    readonly float d;
    public DialogueRuleDrawable(float density) => d = density;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds; if (b.Width() <= 0) return;
        paint.SetStyle(Paint.Style.Fill); paint.Color = PgrTheme.BorderStrong;
        canvas.DrawRect(b.Left, b.Bottom - Math.Max(1, d * .75f), b.Right - 8 * d, b.Bottom, paint);
        paint.Color = PgrTheme.Red;
        using var tri = new Path(); tri.MoveTo(b.Right - 7 * d, b.Bottom); tri.LineTo(b.Right, b.Bottom); tri.LineTo(b.Right, b.Bottom - 7 * d); tri.Close(); canvas.DrawPath(tri, paint);
    }
    public override void SetAlpha(int value) { paint.Alpha = value; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { paint.SetColorFilter(colorFilter); InvalidateSelf(); }
#pragma warning disable CS0672, CA1422
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672, CA1422
    protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
}

/// <summary>Original geometry-only decoration shared by the HUD drawables.</summary>
public static class PgrDeco
{
    /// <summary>
    /// Original orbital-station emblem (not any official logo): split outer ring, tilted orbit, hexagonal core and a
    /// vertical spire with three chevrons below. Stroked with the current paint colour scheme (cyan ring, crimson core).
    /// </summary>
    public static void Emblem(Canvas canvas, float cx, float cy, float r, float d, Paint paint, int alpha = 255)
    {
        Color C(Color c, int a) => Color.Argb(a * alpha / 255, c.R, c.G, c.B);
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, r * .06f);
        paint.Color = C(PgrTheme.Cyan, 200);
        using (var oval = new RectF(cx - r, cy - r, cx + r, cy + r))
            for (int i = 0; i < 4; i++) canvas.DrawArc(oval, i * 90 + 12, 66, false, paint);
        canvas.Save(); canvas.Rotate(-18, cx, cy);
        paint.StrokeWidth = Math.Max(1, r * .035f); paint.Color = C(PgrTheme.Steel, 160);
        using (var orbit = new RectF(cx - r * 1.25f, cy - r * .32f, cx + r * 1.25f, cy + r * .32f)) canvas.DrawOval(orbit, paint);
        canvas.Restore();
        using (var spire = new Path())
        {
            spire.MoveTo(cx, cy - r * 1.18f); spire.LineTo(cx + r * .16f, cy); spire.LineTo(cx, cy + r * .62f); spire.LineTo(cx - r * .16f, cy); spire.Close();
            paint.SetStyle(Paint.Style.Fill); paint.Color = C(PgrTheme.NamePlate, 230); canvas.DrawPath(spire, paint);
        }
        paint.SetStyle(Paint.Style.Stroke); paint.StrokeWidth = Math.Max(1, r * .07f); paint.Color = C(PgrTheme.Foreground, 210);
        using (var hex = new Path())
        {
            float hr = r * .34f;
            for (int i = 0; i < 6; i++) { double a = Math.PI / 3 * i; float x = cx + hr * (float)Math.Cos(a), y = cy + hr * (float)Math.Sin(a); if (i == 0) hex.MoveTo(x, y); else hex.LineTo(x, y); }
            hex.Close(); canvas.DrawPath(hex, paint);
        }
        paint.Color = C(PgrTheme.Cyan, 170); paint.StrokeWidth = Math.Max(1, r * .05f);
        for (int i = 0; i < 3; i++)
        { float y = cy + r * (.78f + i * .16f), w = r * (.34f - i * .08f); canvas.DrawLine(cx - w, y, cx, y + r * .1f, paint); canvas.DrawLine(cx, y + r * .1f, cx + w, y, paint); }
    }

    /// <summary>
    /// Corruption edge texture: blocky crimson "infection" pixels eroding inward from one edge, dense at the edge and
    /// thinning out, with a few cyan glitch slivers. Deterministic per seed; static.
    /// </summary>
    public static void Corruption(Canvas canvas, RectF area, bool fromBottom, uint seed, float d, Paint paint, int alpha = 255)
    {
        paint.SetStyle(Paint.Style.Fill);
        float cell = 3 * d; uint s = seed | 1;
        uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        int cols = (int)(area.Width() / cell), rows = (int)(area.Height() / cell);
        for (int c = 0; c < cols; c++)
            for (int rI = 0; rI < rows; rI++)
            {
                float depth = (float)rI / Math.Max(1, rows);                  // 0 at the eroded edge
                float chance = (1 - depth) * (1 - depth) * .55f;
                if ((Next() % 1000) / 1000f > chance) continue;
                float x = area.Left + c * cell, y = fromBottom ? area.Bottom - (rI + 1) * cell : area.Top + rI * cell;
                bool cyan = Next() % 29 == 0;
                int a = (int)((cyan ? 120 : 70 + 120 * (1 - depth)) * alpha / 255);
                var col = cyan ? PgrTheme.Cyan : (Next() % 3 == 0 ? PgrTheme.RedDeep : PgrTheme.NamePlate);
                paint.Color = Color.Argb(a, col.R, col.G, col.B);
                float wcell = cyan ? cell * (3 + Next() % 5) : cell;
                canvas.DrawRect(x, y, x + wcell - d * .5f, y + cell - d * .5f, paint);
            }
    }
}
