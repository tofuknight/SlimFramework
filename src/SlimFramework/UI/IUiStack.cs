namespace SlimFramework.UI;

/// <summary>逻辑层。数值越大越靠前。</summary>
public enum UiLayer
{
    Auto = 0,
    Hud = 100,
    Loading = 200,
}

/// <summary>UI 栈端口。打开的是场景路径，具体节点类型由适配层约束。</summary>
public interface IUiStack : IModule
{
    void Open(string scenePath, UiLayer layer = UiLayer.Auto, bool cache = false);

    void CloseTop();

    int OpenCount { get; }
}
