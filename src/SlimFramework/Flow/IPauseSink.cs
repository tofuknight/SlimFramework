namespace SlimFramework.Flow;

/// <summary>把暂停意图交给引擎。核心只改标志，不碰 SceneTree。</summary>
public interface IPauseSink
{
    void SetPaused(bool paused);
}
