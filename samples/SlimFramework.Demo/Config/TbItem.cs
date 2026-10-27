using Luban;

namespace cfg;

/// <summary>对应 Luban 表文件名 <c>tbitem</c>。</summary>
public sealed partial class TbItem
{
    private readonly Dictionary<int, Item> _dataMap;
    private readonly List<Item> _dataList;

    public TbItem(ByteBuf buf)
    {
        _dataMap = new Dictionary<int, Item>();
        _dataList = new List<Item>();
        for (var n = buf.ReadSize(); n > 0; --n)
        {
            var row = Item.DeserializeItem(buf);
            _dataList.Add(row);
            _dataMap.Add(row.Id, row);
        }
    }

    public IReadOnlyList<Item> DataList => _dataList;

    public Item Get(int key) => _dataMap[key];

    public Item? GetOrDefault(int key) => _dataMap.TryGetValue(key, out var row) ? row : null;

    public void ResolveRef(Tables tables)
    {
        foreach (var row in _dataList)
            row.ResolveRef(tables);
    }
}
