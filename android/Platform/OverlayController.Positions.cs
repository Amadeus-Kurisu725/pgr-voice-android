using Android.Content;
using Android.Views;

namespace PgrVoice.AndroidApp.Platform;

/// <summary>
/// SPEC 总表 C.4：按屏幕旋转（0–3，同 RegionKey 口径）分别记住悬浮球 / 台词悬浮条 / 控制台 / 阅读栏 / 选后核对卡的拖动位置。
/// 位置按安全区比例保存，恢复后照常 ClampPosition；首次默认 = 15-B 顶部居中（阅读栏靠右）。拖动阈值、轻点切换、收起 / 隐藏不变。
/// </summary>
public sealed partial class OverlayController
{
    const string PlacementStore = "overlay_positions";
    string? lastPlacementKey;

    int ScreenRotation()
    {
        try { return (int)(windows.DefaultDisplay?.Rotation ?? SurfaceOrientation.Rotation0); } catch { return 0; }
    }

    string PlacementMode()
    {
        if (CaptureStopVisible) return "stop";
        if (expanded && browsePage.HasValue) return "reader";
        if (expanded) return "console";
        return compactControlsVisible && !appForeground ? "bar" : "ball";
    }

    ISharedPreferences? Placements() => context.GetSharedPreferences(PlacementStore, FileCreationMode.Private);

    /// <summary>Called after UpdateSize inside ClampPosition: when the window kind or rotation changed, move to the remembered / default place.</summary>
    void ApplyRememberedPlacement()
    {
        if (parameters == null) return;
        string mode = PlacementMode();
        if (mode == "stop") return; // 定位停止键保持在原位置，不记忆
        string key = mode + "_" + ScreenRotation();
        if (key == lastPlacementKey) return;
        lastPlacementKey = key;
        var safe = ScreenArea();
        int spanX = Math.Max(0, safe.Right - safe.Left - parameters.Width), spanY = Math.Max(0, safe.Bottom - safe.Top - parameters.Height);
        var store = Placements();
        if (store != null && store.Contains(key + "_x"))
        {
            parameters.X = safe.Left + (int)(store.GetFloat(key + "_x", 0) * spanX);
            parameters.Y = safe.Top + (int)(store.GetFloat(key + "_y", 0) * spanY);
            return;
        }
        if (mode == "bar" || mode == "console") { parameters.X = safe.Left + spanX / 2; parameters.Y = safe.Top + Math.Min(spanY, ReadDp(8)); }
        else if (mode == "reader") { parameters.X = safe.Left + Math.Max(0, spanX - ReadDp(12)); parameters.Y = safe.Top + Math.Min(spanY, ReadDp(8)); }
        // 悬浮球没有记忆时保持当前位置（源码默认 X 12dp / Y 100dp）。
    }

    void SaveCurrentPlacement()
    {
        if (parameters == null) return;
        string mode = PlacementMode();
        if (mode == "stop") return;
        SavePlacement(mode, parameters.X, parameters.Y, parameters.Width, parameters.Height);
    }

    void SavePlacement(string mode, int x, int y, int width, int height)
    {
        var safe = ScreenArea();
        int spanX = Math.Max(1, safe.Right - safe.Left - width), spanY = Math.Max(1, safe.Bottom - safe.Top - height);
        string key = mode + "_" + ScreenRotation();
        try
        {
            using var edit = Placements()?.Edit();
            edit?.PutFloat(key + "_x", Math.Clamp((x - safe.Left) / (float)spanX, 0, 1));
            edit?.PutFloat(key + "_y", Math.Clamp((y - safe.Top) / (float)spanY, 0, 1));
            edit?.Apply();
        }
        catch { }
        if (mode != "notice") lastPlacementKey = key;
    }

    /// <summary>选后核对卡 / 可点提示（16-C / 17-A 同槽）：返回记住的位置，否则顶部居中。</summary>
    (int X, int Y) NoticePlacement(int width, int height)
    {
        var safe = ScreenArea();
        int spanX = Math.Max(0, safe.Right - safe.Left - width), spanY = Math.Max(0, safe.Bottom - safe.Top - height);
        string key = "notice_" + ScreenRotation();
        var store = Placements();
        if (store != null && store.Contains(key + "_x"))
            return (safe.Left + (int)(store.GetFloat(key + "_x", .5f) * spanX), safe.Top + (int)(store.GetFloat(key + "_y", 0) * spanY));
        return (safe.Left + spanX / 2, safe.Top);
    }
}
