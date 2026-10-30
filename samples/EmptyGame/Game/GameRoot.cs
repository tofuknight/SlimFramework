using SlimFramework;
using SlimFramework.GodotAdapter;

namespace Game;

/// <summary>游戏入口。在 <see cref="Configure"/> 里、base 之后 Use 自己的插件。</summary>
public partial class GameRoot : SlimBootstrap
{
    protected override void Configure(SlimApp app)
    {
        base.Configure(app);
    }
}
