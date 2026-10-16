using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.Text.Json;

namespace PromptWindowDevice.Sink;

[Activity(Name = "cn.pgrvoice.touchsink.TouchActivity", MainLauncher = true, Exported = true,
    Theme = "@android:style/Theme.Material.Light.NoActionBar", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class TouchActivity : Activity
{
    readonly List<object> events = new();
    TextView? label;
    int ups;
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
        label = new TextView(this) { TextSize = 22, Text = "独立 UID 触摸接收器\n等待产品 TapAsync 点击", Gravity = GravityFlags.Center };
        label.SetTextColor(Color.White); label.SetBackgroundColor(Color.Rgb(36, 63, 83));
        SetContentView(label);
        Save("created");
    }
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e == null) return false;
        if (e.ActionMasked == MotionEventActions.Up) ups++;
        events.Add(new { time = SystemClock.UptimeMillis(), action = e.ActionMasked.ToString(), x = e.RawX, y = e.RawY, flags = e.Flags.ToString(), ups });
        if (e.ActionMasked == MotionEventActions.Up)
        {
            if (label != null) label.Text = $"独立 UID 触摸接收器\n实际 UP：{ups}\n位置：{e.RawX:0},{e.RawY:0}";
            var echo = new Intent("cn.pgrvoice.promptprobe.SINK_EVENT");
            echo.SetComponent(new ComponentName("cn.pgrvoice.promptprobe", "cn.pgrvoice.promptprobe.ProbeReceiver"));
            echo.PutExtra("count", ups); echo.PutExtra("x", e.RawX); echo.PutExtra("y", e.RawY); echo.PutExtra("flags", e.Flags.ToString());
            SendBroadcast(echo);
        }
        Save("touch");
        return true;
    }
    void Save(string phase)
    {
        string path = System.IO.Path.Combine(FilesDir!.AbsolutePath, "touch-sink.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { phase, uid = Android.OS.Process.MyUid(), package = PackageName, ups, events }));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
