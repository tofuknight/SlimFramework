namespace SlimFramework.Resources;

/// <summary>
/// 按路径加载资源。路径含义由实现决定；Godot 适配接受 res:// 与 uid://。
/// 每次 Load 增加一次引用，Release 减一。
/// </summary>
public interface IAssetLoader
{
    T Load<T>(string path) where T : class;

    void Release(string path);
}
