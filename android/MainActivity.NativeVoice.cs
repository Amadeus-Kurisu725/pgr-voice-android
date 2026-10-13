using Android.Widget;
using PgrVoice.AndroidApp.Ui;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    /// <summary>设置 · 声音：“游戏有原配时静音配音包”开关、说明，打开后的提醒。</summary>
    void NativeVoiceSettings()
    {
        int total = NativeVoiceCatalog.Shared?.Statistics.ConfiguredEntries ?? 0;
        var toggle = new Switch(this) { Text = "游戏有原配时静音配音包", Checked = session.Settings.NativeVoiceMute, TextSize = 14 };
        PgrTheme.StyleSwitch(toggle);
        content.AddView(toggle, new LinearLayout.LayoutParams(-1, -2)); CardSwitchRow(toggle);
        Line($"已收录 {total} 句游戏原配。轮到这些句子时配音包静音，由游戏播原声；听书不受影响。", 13);
        var reminder = Text("请在游戏设置里打开角色语音；游戏语音关掉时，这些句子两边都没有声音。可在章节列表里按章选择“用原配 / 用配音包”。", 13);
        reminder.SetTextColor(PgrTheme.TealText);
        reminder.SetBackgroundColor(PgrTheme.A(PgrTheme.Cyan, 28));
        reminder.SetPadding(Dp(10), Dp(7), Dp(10), Dp(7));
        reminder.Visibility = toggle.Checked ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
        content.AddView(reminder, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });
        toggle.CheckedChange += (_, e) =>
        {
            session.SetNativeVoiceMute(e.IsChecked);
            reminder.Visibility = e.IsChecked ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
        };
    }

    /// <summary>章节行：有原配的章显示“原配 N”徽标和“用原配 / 用配音包”切换（总开关关闭时只显示徽标）。</summary>
    void NativeVoiceChapterControls(LinearLayout row, string packId)
    {
        int count = session.NativeVoiceCount(packId);
        if (count == 0) return;
        row.AddView(PgrUi.Chip(this, $"原配 {count}", PgrUi.Status.Teal), new LinearLayout.LayoutParams(-2, -2) { Gravity = Android.Views.GravityFlags.CenterVertical, LeftMargin = Dp(6) });
        if (!session.Settings.NativeVoiceMute) return;
        // 切换键：整块 44dp 触摸范围，外观是与“原配 N”同款的小签；用原配为青色，用配音包为灰色。
        var hit = new FrameLayout(this) { Clickable = true, Focusable = true };
        void Paint()
        {
            bool useNative = !session.Settings.NativeVoiceDubPacks.Contains(packId);
            hit.RemoveAllViews();
            var chip = PgrUi.Chip(this, useNative ? "用原配" : "用配音包", useNative ? PgrUi.Status.Teal : PgrUi.Status.NotStarted);
            chip.TextSize = 12;
            hit.AddView(chip, new FrameLayout.LayoutParams(-2, -2, Android.Views.GravityFlags.Center));
            hit.ContentDescription = "本章游戏原配：" + (useNative ? "用原配，点按改为用配音包" : "用配音包，点按改为用原配");
        }
        Paint();
        hit.Click += (_, _) =>
        {
            session.SetNativeVoiceForPack(packId, session.Settings.NativeVoiceDubPacks.Contains(packId));
            Paint();
        };
        row.AddView(hit, new LinearLayout.LayoutParams(-2, Dp(44)) { Gravity = Android.Views.GravityFlags.CenterVertical, LeftMargin = Dp(4) });
    }
}
