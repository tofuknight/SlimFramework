using Luban;

namespace cfg;

/// <summary>Luban 生成的 Tables 入口。loader 的参数是不带扩展名的文件名。</summary>
public sealed partial class Tables
{
    public TbItem TbItem { get; }

    public Tables(Func<string, ByteBuf> loader)
    {
        TbItem = new TbItem(loader("tbitem"));
        TbItem.ResolveRef(this);
    }
}
