# Luban 运行时

这四份文件来自 [Luban](https://github.com/focus-creative-games/Luban) 的 C# 运行时（`BeanBase` / `ByteBuf` / `ITypeId` / `StringUtil`），命名空间必须保持 `Luban`，生成代码才能直接编译。

- 协议：MIT。见仓库根目录 `THIRD_PARTY_NOTICES.md`。
- 不要在这里改读格式。升级时整组替换，并让 Demo 的 `tbitem.bytes` 往返测试重新跑过。
- 游戏侧生成的 `cfg.*` 表类放在游戏程序集，不放进本目录。
