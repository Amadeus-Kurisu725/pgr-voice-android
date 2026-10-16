using Android.Content.Res;
using Android.Graphics.Drawables;

namespace PgrVoice.AndroidApp.Ui;

/// <summary>
/// 自绘装饰没有可复制的配置：系统 mutate() 复制 Ripple / Layer 背景时继续共用同一实例，
/// 与不提供 ConstantState 时 LayerDrawable 的实际做法相同，只是不再每次打印带调用栈的警告。
/// </summary>
public sealed class SharedDrawableState(Drawable drawable) : Drawable.ConstantState
{
    public override global::Android.Content.PM.ConfigChanges ChangingConfigurations => 0;
    public override Drawable NewDrawable() => drawable;
    public override Drawable NewDrawable(Resources? resources) => drawable;
}
