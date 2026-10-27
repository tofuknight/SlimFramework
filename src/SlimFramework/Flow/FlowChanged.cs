namespace SlimFramework.Flow;

/// <summary>流程或暂停变化。FromId 与 ToId 相同表示只改了暂停。</summary>
public readonly record struct FlowChanged(string? FromId, string? ToId, bool IsPaused) : IEvent;
