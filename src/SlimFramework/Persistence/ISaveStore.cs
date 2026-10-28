namespace SlimFramework.Persistence;

/// <summary>槽位字节读写。实现可以是内存、user:// 或任意文件系统。</summary>
public interface ISaveStore
{
    void Write(int slot, byte[] payload);

    bool TryRead(int slot, out byte[] payload);

    /// <summary>槽位里是否已有字节。不解析载荷。</summary>
    bool Exists(int slot);

    bool Delete(int slot);
}
