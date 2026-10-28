using Luban;

namespace cfg;

/// <summary>手写的 Luban 风格 bean，字段顺序即 bin 布局。正式项目改由 Luban 生成。</summary>
public sealed partial class Item : BeanBase
{
    public Item(ByteBuf buf)
    {
        Id = buf.ReadInt();
        Name = buf.ReadString();
        Desc = buf.ReadString();
        Count = buf.ReadInt();
    }

    public static Item DeserializeItem(ByteBuf buf) => new(buf);

    public readonly int Id;
    public readonly string Name;
    public readonly string Desc;
    public readonly int Count;

    public const int TypeId = 1001;

    public override int GetTypeId() => TypeId;

    public void ResolveRef(Tables tables)
    {
    }
}
