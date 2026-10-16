using System.Text;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 03-B2 keyword-centred snippet for one search result row (display only). Pure logic: the caller supplies a width
/// measure (Paint.MeasureText) so the window is fitted by real text width, not by character count.
/// </summary>
public sealed record SearchSnippetResult(string Display, IReadOnlyList<(int Start, int End)> Highlights,
    IReadOnlyList<(int Start, int End)> Ellipses, (int Start, int End)? Fade, bool Truncated);

public static class SearchSnippet
{
    const string Dots = "…";
    static readonly string Punctuation = "，。！？；：、…—,.!?;:";

    /// <summary>Same normalisation as LibrarySearchIndex (NFKC, drop whitespace / punctuation, upper-case) with a map back to source indices.</summary>
    public static (string Text, int[] Map) Normalize(string text)
    {
        var sb = new StringBuilder(text.Length); var map = new List<int>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            string piece = text[i].ToString().Normalize(NormalizationForm.FormKC);
            foreach (char c in piece)
            {
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c)) continue;
                sb.Append(char.ToUpperInvariant(c)); map.Add(i);
            }
        }
        return (sb.ToString(), map.ToArray());
    }

    public static string[] Terms(string query) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Select(t => Normalize(t).Text).Where(t => t.Length > 0).Distinct().ToArray();

    /// <summary>All keyword hits in <paramref name="text"/> as source ranges [s, e).</summary>
    public static List<(int Start, int End)> Hits(string text, IEnumerable<string> terms, bool firstOnly = false)
    {
        var (norm, map) = Normalize(text); var hits = new List<(int, int)>();
        foreach (var term in terms)
        {
            int from = 0;
            while (from <= norm.Length - term.Length)
            {
                int k = norm.IndexOf(term, from, StringComparison.Ordinal);
                if (k < 0) break;
                hits.Add((map[k], map[k + term.Length - 1] + 1)); from = k + term.Length;
                if (firstOnly) break;
            }
        }
        return Merge(hits);
    }

    static List<(int Start, int End)> Merge(IEnumerable<(int Start, int End)> ranges)
    {
        var list = ranges.OrderBy(r => r.Start).ToList(); var merged = new List<(int Start, int End)>();
        foreach (var r in list)
        {
            if (merged.Count > 0 && r.Start <= merged[^1].End) merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, r.End));
            else merged.Add(r);
        }
        return merged;
    }

    static int WindowStart(string text, int anchor)
    {
        int start = Math.Max(0, anchor - 4);
        for (int i = anchor - 1; i >= start; i--) if (Punctuation.IndexOf(text[i]) >= 0) { start = i + 1; break; }
        return start <= 1 ? 0 : start;
    }

    public static SearchSnippetResult Build(string speaker, string text, string query, Func<string, float> measure, float width)
    {
        string name = string.IsNullOrWhiteSpace(speaker) ? "旁白" : speaker.Trim();
        string prefix = name + "：";
        var terms = Terms(query);
        var textHits = Hits(text, terms);
        var nameHits = Hits(name, terms);
        if (width <= 0 || measure(prefix + text) <= width)
            return Compose(prefix, nameHits, text, new List<(int, int)> { (0, text.Length) }, textHits);

        // Anchors: first hit of each keyword in the line, skipping keywords already visible in the speaker.
        string normName = Normalize(name).Text;
        var anchors = new List<(int Start, int End)>();
        foreach (var term in terms)
        {
            if (normName.Contains(term, StringComparison.Ordinal)) continue;
            var first = Hits(text, new[] { term }, true);
            if (first.Count > 0) anchors.Add(first[0]);
        }
        if (anchors.Count == 0)
            foreach (var term in terms) { var first = Hits(text, new[] { term }, true); if (first.Count > 0) { anchors.Add(first[0]); break; } }
        anchors = Merge(anchors);

        string Render(List<(int Start, int End)> w) => Compose(prefix, nameHits, text, w, textHits).Display;
        bool Fits(List<(int Start, int End)> w) => measure(Render(w)) <= width;

        if (anchors.Count == 0)
        {
            var w0 = new List<(int Start, int End)> { (0, 0) };
            Grow(w0, Fits, text.Length);
            return Compose(prefix, nameHits, text, w0, textHits);
        }
        // 1. Single window covering every anchor.
        var single = new List<(int Start, int End)> { (WindowStart(text, anchors[0].Start), anchors[^1].End) };
        if (Fits(single))
        {
            Grow(single, Fits, text.Length);
            if (single[0].End >= text.Length)
                while (single[0].Start > 0)
                {
                    var trial = new List<(int Start, int End)> { (single[0].Start - 1, single[0].End) };
                    if (!Fits(trial)) break; single = trial;
                }
            return Compose(prefix, nameHits, text, single, textHits);
        }
        // 2. One window per anchor, joined by an ellipsis.
        var multi = Merge(anchors.Select(a => (WindowStart(text, a.Start), a.End)));
        if (!Fits(multi)) multi = Merge(anchors);
        if (!Fits(multi)) multi = new List<(int Start, int End)> { (WindowStart(text, anchors[0].Start), anchors[0].End) };
        Grow(multi, Fits, text.Length);
        return Compose(prefix, nameHits, text, multi, textHits);
    }

    /// <summary>Round-robin extend each window end by one character while the line still fits.</summary>
    static void Grow(List<(int Start, int End)> windows, Func<List<(int Start, int End)>, bool> fits, int length)
    {
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < windows.Count; i++)
            {
                int limit = i + 1 < windows.Count ? windows[i + 1].Start : length;
                if (windows[i].End >= limit) continue;
                var trial = new List<(int Start, int End)>(windows); trial[i] = (trial[i].Start, trial[i].End + 1);
                if (i + 1 < trial.Count && trial[i].End >= trial[i + 1].Start) { trial[i] = (trial[i].Start, trial[i + 1].End); trial.RemoveAt(i + 1); }
                if (!fits(trial)) continue;
                windows.Clear(); windows.AddRange(trial); grew = true;
            }
        }
    }

    static SearchSnippetResult Compose(string prefix, List<(int Start, int End)> nameHits, string text,
        List<(int Start, int End)> windows, List<(int Start, int End)> textHits)
    {
        var sb = new StringBuilder(prefix);
        var highlights = new List<(int, int)>(nameHits.Where(h => h.End <= prefix.Length - 1));
        var ellipses = new List<(int, int)>();
        (int, int)? fade = null; bool truncated = false;
        foreach (var w in windows)
        {
            if (w.Start > 0) { ellipses.Add((sb.Length, sb.Length + 1)); sb.Append(Dots); truncated = true; }
            int offset = sb.Length - w.Start;
            sb.Append(text, w.Start, w.End - w.Start);
            foreach (var h in textHits)
            {
                int s = Math.Max(h.Start, w.Start), e = Math.Min(h.End, w.End);
                if (s < e) highlights.Add((s + offset, e + offset));
            }
        }
        if (windows.Count > 0 && windows[^1].End < text.Length)
        {
            int visible = windows[^1].End - windows[^1].Start;
            if (visible > 0) fade = (sb.Length - Math.Min(2, visible), sb.Length);
            ellipses.Add((sb.Length, sb.Length + 1)); sb.Append(Dots); truncated = true;
        }
        return new SearchSnippetResult(sb.ToString(), highlights, ellipses, fade, truncated);
    }
}
