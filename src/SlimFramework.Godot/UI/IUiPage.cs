namespace SlimFramework.GodotAdapter;

/// <summary>
/// 页面打开和关闭。实现类型要继承 <c>Control</c>，但接口本身不是 Godot 脚本，
/// 这样 UI 栈留在适配程序集里，具体页面类可以由游戏工程编译。
/// </summary>
public interface IUiPage
{
    void OnOpen();

    void OnClose();
}
