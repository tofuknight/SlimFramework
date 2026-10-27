namespace SlimFramework;

/// <summary>可插拔扩展。Register 只允许触达框架服务，不得反向把游戏类型塞进框架程序集。</summary>
public interface IPlugin
{
    string Id { get; }

    void Register(SlimApp app);
}
