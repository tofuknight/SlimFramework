namespace SlimFramework;

/// <summary>主线程每帧回调。注册为模块后由 <see cref="SlimApp.TickFrame"/> 驱动。</summary>
public interface ITickable
{
    void Tick(double delta);
}
