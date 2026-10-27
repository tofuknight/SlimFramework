using SlimFramework;
using SlimFramework.Flow;
using SlimFramework.Persistence;

namespace SlimFramework.Demo;

/// <summary>示例插件：向已注册的存档和流程服务挂自己的类型。</summary>
public sealed class DemoPlugin : IPlugin
{
    public string Id => "demo";

    public ProfileState State { get; } = new();

    public void Register(SlimApp app)
    {
        app.Get<SaveService>().Register(new ProfileContributor(State));
        app.Get<FlowMachine>().Register(new DemoFlowState("boot"));
        app.Get<FlowMachine>().Register(new DemoFlowState("play"));
    }

    private sealed class DemoFlowState : IFlowState
    {
        public DemoFlowState(string id) => Id = id;

        public string Id { get; }

        public void Enter(SlimApp app)
        {
        }

        public void Exit()
        {
        }
    }
}
