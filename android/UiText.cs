using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

/// <summary>
/// 0.4.7：现稿续接与默认分支的安卓界面文字集中在此（核心层文字见 PgrVoice.Listening.ListeningText），改字只需改这两处。
/// </summary>
public static class UiText
{
    public const string DraftTag = ListeningText.DraftTag;
    public const string DraftPrefix = ListeningText.DraftPrefix;
    /// <summary>状态行短标签：“现稿续接 · ”。</summary>
    public const string DraftStatusTag = DraftTag + " · ";

    // 续接入口（剧情页菜单 / 悬浮页）
    public const string DraftConfirmNote = "此段出口尚未逐句核实，将按配音包现有连接续接（现稿续接）。";
    public const string DraftOverlayTitle = "按现稿续接";
    public const string DraftOverlayHint = "此段出口尚未逐句核实。下方是配音包现稿写的下一处，确认后按现稿续接。";
    public const string DraftAutoplayNotice = DraftPrefix + "按配音包现有连接继续自动播放。";

    // 08 分支页：默认分支
    public const string DefaultBranchSection = "默认分支";
    public const string DefaultBranchSectionEnglish = "DEFAULT OPTION";
    public static readonly string[] DefaultBranchSegments = { "第一个选项", "第二个选项" };
    public static readonly string[] DefaultBranchSegmentsEnglish = { "OPTION 1", "OPTION 2" };
    public const string DefaultBranchCaption = "听书的“默认分支”与自动播放默认分支共用此设置；只有一个选项的地方按第一个。";

    /// <summary>待配音句状态行前缀（托盘 / 悬浮窗 / 通知）。</summary>
    public const string PendingVoiceTag = ListeningText.PendingVoiceTag;
    public const string PendingVoiceManual = "字幕已显示，点下一句继续";
    public const string PendingVoiceAuto = "字幕停留后自动继续";
    public const string BranchAutoSelectLabel = "分支自动选择";
    public const string BranchAutoSelectCaption = "听书遇到待选支线、续接未核实或后续没有配音文本时，15 秒倒计时后自动处理：支线按上方默认项选择并显示“已自动选择：…”，未核续接按本节顺序继续，无文本处跳到下一小节（最后一个小节则结束本章）。关闭后停下等待。只有一个选项的菜单、探索菜单不受此开关影响，始终自动继续。";
    public const string ExportPlaybackLog = "导出播放记录";
    /// <summary>0.4.8 日志导出入口：反馈后告诉玩家去哪里导出（与按钮名、设置页签名一致）。</summary>
    public const string ExportPlaybackLogWhere = "设置 →「存档 · 诊断」→「导出播放记录」";
    public const string ExportPlaybackLogHint = "可在" + ExportPlaybackLogWhere + "导出后发给作者。";

    public static string OptionOrdinal(int number) => ListeningText.OptionOrdinal(number);
    /// <summary>听书策略菜单里的“默认分支”项。</summary>
    public static string ListeningDefaultBranchLabel(int number) => "默认分支：按" + (number == 2 ? "第二个" : "第一个") + "选项继续（在分支页更改）";
}
