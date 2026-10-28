using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>把 <see cref="FlowMachine"/> 的暂停写到 <see cref="SceneTree.Paused"/>。</summary>
public sealed class GodotPauseSink : IPauseSink
{
    public void SetPaused(bool paused)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Paused = paused;
    }
}
