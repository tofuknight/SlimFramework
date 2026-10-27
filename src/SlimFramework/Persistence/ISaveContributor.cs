namespace SlimFramework.Persistence;

/// <summary>
/// 存档分段。SectionId 写入 <c>SaveSlot.sections</c> 的 key。
/// 载荷格式由实现自定（Demo 使用自己的 protobuf），框架不解析。
/// </summary>
public interface ISaveContributor
{
    string SectionId { get; }

    byte[] Capture();

    /// <summary>槽位没有该段时传入空数组，实现应恢复默认值。</summary>
    void Restore(byte[] payload);
}
