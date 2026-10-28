using SlimFramework.GodotAdapter;

namespace SlimFramework.DemoGame;

/// <summary>适配冒烟用的最小页面。打开时计数，证明场景真的进了 UI 栈。</summary>
public partial class SmokePage : UiPage
{
    public static int TimesOpened { get; private set; }

    public override void OnOpen() => TimesOpened++;
}
