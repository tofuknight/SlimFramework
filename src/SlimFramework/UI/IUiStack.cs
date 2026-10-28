namespace SlimFramework.UI;

/// <summary>绘制层。数值越大越靠前。不决定 <see cref="IUiStack.CloseTop"/> 的关闭顺序。</summary>
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

    /// <summary>关闭最后打开的页面。<see cref="UiLayer"/> 只影响绘制，不决定关闭顺序。</summary>
    void CloseTop();

    int OpenCount { get; }
}
