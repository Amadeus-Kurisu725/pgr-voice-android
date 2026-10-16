using Android.OS;
using Android.Widget;
using Android.Window;

namespace PgrVoice.AndroidApp;

public sealed partial class MainActivity
{
    const long ExitConfirmWindowMs = 2000;
    long lastExitBackAt = -ExitConfirmWindowMs;
    BackCallback? backCallback;

    void RegisterBackNavigation()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        backCallback = new BackCallback(this);
        OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(IOnBackInvokedDispatcher.PriorityDefault, backCallback);
    }

    // Android 13 起由回调接管；Android 16 以 targetSdk 36 运行时不再调用 OnBackPressed。
#pragma warning disable CA1422, CS0672
    public override void OnBackPressed() => HandleBack();
#pragma warning restore CA1422, CS0672

    void HandleBack()
    {
        if (page != 0) { ShowPage(0); return; }
        long now = SystemClock.ElapsedRealtime();
        if (now - lastExitBackAt < ExitConfirmWindowMs) { Finish(); return; }
        lastExitBackAt = now;
        Toast.MakeText(this, "再按一次返回键退出", ToastLength.Short)?.Show();
    }

    sealed class BackCallback(MainActivity owner) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => owner.Safe(owner.HandleBack);
    }
}
