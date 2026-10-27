namespace SlimFramework;

/// <summary>模块生命周期。由 <see cref="SlimApp"/> 按注册顺序 Init，逆序 Shutdown。</summary>
public interface IModule
{
    void Init(SlimApp app);

    void Shutdown();
}
