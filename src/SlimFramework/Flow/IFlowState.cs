namespace SlimFramework.Flow;

/// <summary>一个流程状态。场景切换由实现自己完成，框架不绑定 PackedScene。</summary>
public interface IFlowState
{
    string Id { get; }

    void Enter(SlimApp app);

    void Exit();
}
