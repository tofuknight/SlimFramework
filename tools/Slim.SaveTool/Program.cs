using Google.Protobuf;
using SlimFramework.Contracts.Save.V1;

if (args.Length < 2)
{
    Console.Error.WriteLine("用法:");
    Console.Error.WriteLine("  Slim.SaveTool pack <file> <displayName>");
    Console.Error.WriteLine("  Slim.SaveTool unpack <file>");
    return 1;
}

var file = args[1];
switch (args[0])
{
    case "pack":
        var displayName = args.Length > 2 ? args[2] : "";
        var slot = new SaveSlot
        {
            FormatVersion = 1,
            DisplayName = displayName,
        };
        File.WriteAllBytes(file, slot.ToByteArray());
        Console.WriteLine($"wrote {file}");
        return 0;
    case "unpack":
        var parsed = SaveSlot.Parser.ParseFrom(File.ReadAllBytes(file));
        Console.WriteLine($"format_version={parsed.FormatVersion}");
        Console.WriteLine($"display_name={parsed.DisplayName}");
        Console.WriteLine($"sections={parsed.Sections.Count}");
        return 0;
    default:
        Console.Error.WriteLine($"未知命令: {args[0]}");
        return 1;
}
