using Godot;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>按槽位和档位缓存占位贴图。形状只服务这一条样例鱼，不进插件。</summary>
internal sealed class OpenOceanParts
{
    private readonly Dictionary<(string Slot, int Tier), Texture2D> _cache = new();

    public Texture2D Get(string slot, int tier)
    {
        if (tier < 0 || tier >= PlaceholderCutouts.TierCount)
            throw new ArgumentOutOfRangeException(nameof(tier));
        var key = (slot, tier);
        if (_cache.TryGetValue(key, out var texture))
            return texture;
        texture = ImageTexture.CreateFromImage(PlaceholderCutouts.Create(slot, tier));
        _cache[key] = texture;
        return texture;
    }
}

/// <summary>纯色剪影。没有现成像素图，用简单多边形把三档拉开。</summary>
internal static class PlaceholderCutouts
{
    public const int TierCount = 3;

    public static Image Create(string slotId, int tier)
    {
        if (slotId == OpenOceanFish.SlotBody)
            return Body(tier);

        var (image, poly, fill) = slotId switch
        {
            OpenOceanFish.SlotHead => Head(tier),
            OpenOceanFish.SlotJaw => Jaw(tier),
            OpenOceanFish.SlotRidge => Ridge(tier),
            OpenOceanFish.SlotDorsal => Dorsal(tier),
            OpenOceanFish.SlotPectoral => Pectoral(tier),
            OpenOceanFish.SlotTail => Tail(tier),
            _ => throw new ArgumentException($"没有占位贴图：{slotId}。", nameof(slotId))
        };
        Paint(image, poly, fill, stroke: true);
        if (slotId == OpenOceanFish.SlotHead)
            DrawEye(image);
        return image;
    }

    private static Image Body(int tier)
    {
        var image = Blank(OpenOceanBody.TextureWidth, OpenOceanBody.TextureHeight);
        var fill = Rgb(tier switch
        {
            0 => (132, 176, 204),
            1 => (64, 130, 178),
            _ => (28, 72, 120)
        });
        var belly = Rgb(tier switch
        {
            0 => (186, 210, 216),
            1 => (126, 176, 198),
            _ => (86, 128, 160)
        });
        var back = Rgb(tier switch
        {
            0 => (78, 118, 150),
            1 => (32, 86, 128),
            _ => (16, 48, 84)
        });
        Paint(image, OpenOceanBody.Uv, fill, stroke: true);
        var height = image.GetHeight();
        var width = image.GetWidth();
        var backLine = height * 0.34f;
        var bellyLine = height * 0.58f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A <= 0f)
                    continue;
                if (y < backLine)
                    image.SetPixel(x, y, back);
                else if (y > bellyLine)
                    image.SetPixel(x, y, belly);
            }
        }

        return image;
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Head(int tier)
    {
        var image = Blank(72, 44);
        Vector2[] poly = tier switch
        {
            0 => [new(4, 22), new(22, 12), new(50, 14), new(66, 22), new(50, 30), new(22, 32)],
            1 => [new(2, 22), new(18, 8), new(48, 8), new(68, 22), new(48, 34), new(18, 36)],
            _ => [new(2, 22), new(16, 4), new(44, 6), new(70, 22), new(44, 38), new(16, 40)]
        };
        return (image, poly, Rgb(tier switch
        {
            0 => (176, 208, 216),
            1 => (110, 176, 196),
            _ => (206, 224, 230)
        }));
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Jaw(int tier)
    {
        var image = Blank(52, 22);
        Vector2[] poly = tier switch
        {
            0 => [new(2, 8), new(24, 4), new(40, 10), new(24, 15), new(2, 12)],
            1 => [new(2, 8), new(22, 2), new(48, 9), new(28, 17), new(2, 14)],
            _ => [new(2, 9), new(18, 2), new(50, 6), new(50, 14), new(22, 19), new(2, 16)]
        };
        return (image, poly, Rgb(tier switch
        {
            0 => (210, 176, 140),
            1 => (186, 124, 96),
            _ => (110, 64, 62)
        }));
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Ridge(int tier)
    {
        var image = Blank(72, 14);
        Vector2[] poly = tier switch
        {
            0 => [new(2, 7), new(16, 5), new(56, 5), new(70, 7), new(56, 9), new(16, 9)],
            1 => [new(2, 7), new(14, 4), new(58, 3), new(70, 7), new(58, 11), new(14, 10)],
            _ => [new(2, 7), new(12, 2), new(60, 1), new(70, 7), new(60, 13), new(12, 12)]
        };
        return (image, poly, Rgb(tier switch
        {
            0 => (196, 104, 108),
            1 => (214, 132, 86),
            _ => (232, 184, 84)
        }));
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Dorsal(int tier)
    {
        var image = Blank(28, 36);
        Vector2[] poly = tier switch
        {
            0 => [new(6, 32), new(22, 32), new(14, 16)],
            1 => [new(5, 34), new(23, 34), new(14, 8)],
            _ => [new(4, 34), new(24, 34), new(12, 2)]
        };
        return (image, poly, Fin(tier));
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Pectoral(int tier)
    {
        var image = Blank(36, 22);
        Vector2[] poly = tier switch
        {
            0 => [new(22, 4), new(32, 8), new(10, 16)],
            1 => [new(24, 3), new(34, 8), new(6, 18)],
            _ => [new(26, 2), new(34, 9), new(2, 20)]
        };
        return (image, poly, Fin(tier));
    }

    private static (Image Image, Vector2[] Poly, Color Fill) Tail(int tier)
    {
        var image = Blank(48, 44);
        Vector2[] poly = tier switch
        {
            0 => [new(40, 22), new(8, 10), new(18, 22), new(8, 34)],
            1 => [new(42, 22), new(4, 6), new(16, 22), new(4, 38)],
            _ => [new(44, 22), new(2, 2), new(14, 22), new(2, 42)]
        };
        return (image, poly, Fin(tier));
    }

    private static Color Fin(int tier) => Rgb(tier switch
    {
        0 => (112, 198, 190),
        1 => (48, 158, 168),
        _ => (22, 112, 128)
    });

    private static void DrawEye(Image image)
    {
        FillCircle(image, 50, 18, 4, Rgb((18, 32, 42)));
        FillCircle(image, 51, 17, 1, Rgb((240, 248, 252)));
    }

    private static Image Blank(int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));
        return image;
    }

    private static void Paint(Image image, Vector2[] poly, Color fill, bool stroke)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (Inside(poly, x + 0.5f, y + 0.5f))
                    image.SetPixel(x, y, fill);
            }
        }

        if (stroke)
            Stroke(image, poly, Darken(fill));
    }

    private static void Stroke(Image image, Vector2[] poly, Color color)
    {
        for (var i = 0; i < poly.Length; i++)
            StrokeLine(image, poly[i], poly[(i + 1) % poly.Length], color);
    }

    private static void StrokeLine(Image image, Vector2 a, Vector2 b, Color color)
    {
        var steps = Mathf.CeilToInt(a.DistanceTo(b));
        for (var i = 0; i <= steps; i++)
        {
            var point = a.Lerp(b, steps == 0 ? 0f : i / (float)steps);
            FillCircle(image, Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y), 1, color);
        }
    }

    private static void FillCircle(Image image, int cx, int cy, int radius, Color color)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        for (var y = cy - radius; y <= cy + radius; y++)
        {
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;
                if (((x - cx) * (x - cx)) + ((y - cy) * (y - cy)) > radius * radius)
                    continue;
                image.SetPixel(x, y, color);
            }
        }
    }

    private static bool Inside(Vector2[] poly, float x, float y)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            if ((pi.Y > y) == (pj.Y > y))
                continue;
            var xCross = ((pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y)) + pi.X;
            if (x < xCross)
                inside = !inside;
        }

        return inside;
    }

    private static Color Rgb((int R, int G, int B) value) =>
        new(value.R / 255f, value.G / 255f, value.B / 255f);

    private static Color Darken(Color color) => new(color.R * 0.4f, color.G * 0.4f, color.B * 0.4f);
}
