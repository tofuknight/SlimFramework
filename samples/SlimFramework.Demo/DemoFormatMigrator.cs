using Google.Protobuf;
using SlimFramework.Contracts.Save.V1;
using SlimFramework.Persistence;

namespace SlimFramework.Demo;

/// <summary>
/// 演示把 format_version 1 升到 2。标记段用来确认迁移器跑过。
/// 玩法段原样留下，由 <see cref="ProfileContributor"/> 自己读。
/// </summary>
public sealed class DemoFormatMigrator : ISaveMigrator
{
    public const string MarkerSection = "demo.migrated";

    public int FromVersion => 1;

    public int ToVersion => 2;

    public SaveSlot Migrate(SaveSlot slot)
    {
        var next = slot.Clone();
        next.FormatVersion = ToVersion;
        next.Sections[MarkerSection] = ByteString.CopyFromUtf8("1to2");
        return next;
    }
}
