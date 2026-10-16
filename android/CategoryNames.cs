namespace PgrVoice.AndroidApp;

/// <summary>
/// 分类面板（「选择分类 CATEGORY」）的名称取值与英文适配规则。纯逻辑、不依赖 Android，便于测试。
/// 中文名取自 <see cref="ChapterCatalog.Categories"/>（第一类「主线」），英文取自本表。
/// </summary>
public static class CategoryNames
{
    public static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["主线"] = "MAIN STORY", ["浮点纪实"] = "FLOAT RECORD", ["外篇剧情"] = "SIDE STORY", ["间章剧情"] = "INTERLUDE",
        ["本我回廊"] = "EGO CORRIDOR", ["黄金之涡"] = "GOLDEN VORTEX", ["空中花园"] = "BABYLONIA", ["多维演绎"] = "MULTIVERSE",
        ["时宇漫纪"] = "CHRONICLE", ["联动"] = "COLLAB", ["好感剧情"] = "AFFECTION", ["调色板战争"] = "PALETTE WAR", ["边界公约"] = "BORDER PACT",
        [ChapterCatalog.Other] = "OTHER",
    };

    public static string En(string category) => English.TryGetValue(category, out var en) ? en : "OTHER";

    public const float EnSizeSp = 8.5f, EnMinSizeSp = 7f, EnSpacingEm = .14f, EnMinSpacingEm = .04f;

    /// <summary>英文副名适配结果：字距（em）、字号（sp）、是否需要换行（放不下时换行，绝不截断或省略号）。</summary>
    public readonly record struct EnFit(float SpacingEm, float SizeSp, bool Wrap);

    /// <summary>
    /// 与定稿 cfx-a5b6.js fitAll 相同：先把字距从 .14em 逐步收到 .04em，再把字号从 8.5 逐步降到 7（每步 .25）；
    /// 仍放不下则保持最小值并换行。measure(字距em, 字号sp) 返回整行宽度（与 available 同单位）。
    /// </summary>
    public static EnFit FitEnglish(Func<float, float, float> measure, float available)
    {
        float ls = EnSpacingEm, fs = EnSizeSp;
        while (measure(ls, fs) > available + .5f && ls > EnMinSpacingEm + .001f) ls = MathF.Round(ls - .02f, 2);
        while (measure(ls, fs) > available + .5f && fs > EnMinSizeSp + .001f) fs -= .25f;
        return new EnFit(ls, fs, measure(ls, fs) > available + .5f);
    }
}
