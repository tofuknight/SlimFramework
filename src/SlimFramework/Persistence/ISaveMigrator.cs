using SlimFramework.Contracts.Save.V1;

namespace SlimFramework.Persistence;

/// <summary>
/// 把一档 <see cref="SaveSlot.FormatVersion"/> 升到下一档。
/// 只处理信封（版本、显示名、sections）。段里的玩法字节由游戏自己的迁移器解释。
/// </summary>
public interface ISaveMigrator
{
    /// <summary>该步读取的 format_version，必须 &gt;= 1。</summary>
    int FromVersion { get; }

    /// <summary>该步写回的 format_version，必须大于 <see cref="FromVersion"/>。</summary>
    int ToVersion { get; }

    SaveSlot Migrate(SaveSlot slot);
}
