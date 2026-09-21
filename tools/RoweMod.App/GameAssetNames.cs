namespace RoweMod.App;

static class GameAssetNames
{
    public readonly record struct Info(
        string Garment,
        string? Variant,
        string Slot,
        string Family,
        string? MeshHint);

    public static Info Parse(string? path)
    {
        var p = (path ?? "").Replace('\\', '/');
        var parts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.FindIndex(parts, x => x.Equals("Character", StringComparison.OrdinalIgnoreCase));
        if (i < 0)
            return new Info(Human(FileStem(p)), null, "Game", FileStem(p), null);

        var category = At(parts, i + 1);
        var family = At(parts, i + 2);
        var extra = At(parts, i + 3);

        if (category.Equals("skates", StringComparison.OrdinalIgnoreCase))
        {
            var kind = family;
            family = extra;
            extra = At(parts, i + 4);
            return Finish(family, extra, SlotFor(kind), MeshFor(family, kind));
        }

        if (IsFile(family) || string.IsNullOrWhiteSpace(family))
            return Finish(category, null, SlotFor(category), MeshFor(category, category));

        if (IsJunk(extra) || IsFile(extra))
            extra = "";

        return Finish(family, extra, SlotFor(category), MeshFor(family, category));
    }

    public static string Title(Info info, bool mesh)
    {
        var name = info.Garment;
        if (mesh)
        {
            var sex = SexLabel(info.Variant);
            return sex == null ? name : name + " (" + sex + ")";
        }
        if (!string.IsNullOrWhiteSpace(info.Variant) && !IsSex(info.Variant))
            return name + " · " + Human(info.Variant);
        return name;
    }

    public static string GroupTitle(Info info, bool mesh)
    {
        if (mesh) return info.Slot;
        var meshName = string.IsNullOrWhiteSpace(info.MeshHint) ? "" : " (" + info.MeshHint + ")";
        return info.Garment + meshName;
    }

    static Info Finish(string family, string? extra, string slot, string? mesh)
    {
        family = family.Replace("overized", "oversized", StringComparison.OrdinalIgnoreCase);
        var variant = string.IsNullOrWhiteSpace(extra) ? null : extra;
        return new Info(Human(family), variant, slot, family.ToLowerInvariant(), mesh);
    }

    static string Human(string slug)
    {
        slug = slug.Replace("overized", "oversized", StringComparison.OrdinalIgnoreCase);
        if (Known.TryGetValue(slug.ToLowerInvariant(), out var nice))
            return nice;
        var words = slug.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w =>
            w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    static string SlotFor(string category) => category.ToLowerInvariant() switch
    {
        "upper" => "Tops",
        "lower" => "Bottoms",
        "hat" or "hats" => "Hats",
        "glass" or "glasses" => "Glasses",
        "hair" => "Hair",
        "beard" => "Beard",
        "body" => "Body",
        "boot" or "boots" => "Boots",
        "frame" or "frames" => "Frames",
        "wheel" or "wheels" => "Wheels",
        "skin" => "Skin",
        "eyes" => "Eyes",
        _ => "Game",
    };

    static string? MeshFor(string family, string category)
    {
        var f = family.ToLowerInvariant();
        var c = category.ToLowerInvariant();
        if (c is "hats" or "hat" or "glasses" or "glass" or "boots" or "boot"
            or "frames" or "frame" or "wheels" or "wheel")
            return f;
        if (c is "hair" or "beard" or "body")
            return f;
        return f + "-male";
    }

    static string? SexLabel(string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant)) return null;
        if (variant.Equals("male", StringComparison.OrdinalIgnoreCase)) return "male";
        if (variant.Equals("female", StringComparison.OrdinalIgnoreCase)) return "female";
        if (variant.EndsWith("-male", StringComparison.OrdinalIgnoreCase)) return "male";
        if (variant.EndsWith("-female", StringComparison.OrdinalIgnoreCase)) return "female";
        return null;
    }

    static bool IsSex(string? v) => SexLabel(v) != null;

    static bool IsFile(string? s) =>
        !string.IsNullOrWhiteSpace(s) && (s.Contains('.') || s.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase));

    static bool IsJunk(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        var v = s.ToLowerInvariant();
        return v is "male" or "female" or "cotton" or "textures" or "masks"
            or "icons" or "customization-icons" or "head";
    }

    static string At(string[] parts, int i) =>
        i >= 0 && i < parts.Length ? parts[i] : "";

    static string FileStem(string path)
    {
        var name = path.Replace('\\', '/').Split('/').LastOrDefault() ?? path;
        foreach (var ext in new[] { ".uasset", ".glb", ".gltf", ".png", ".uexp" })
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return name[..^ext.Length];
        }
        return name;
    }

    static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hoodie"] = "Hoodie",
        ["tshirt"] = "T-shirt",
        ["tshirt-baggy"] = "Baggy T-shirt",
        ["vest-tshirt"] = "Vest t-shirt",
        ["short-shirt"] = "Short shirt",
        ["layered-sleeve"] = "Layered sleeve",
        ["cargos"] = "Cargos",
        ["oversized-jeans"] = "Oversized jeans",
        ["overized-jeans"] = "Oversized jeans",
        ["short-pants-street"] = "Street shorts",
        ["skinny-jeans"] = "Skinny jeans",
        ["styled-pants"] = "Styled pants",
        ["standard-boots"] = "Standard boots",
        ["second-boots"] = "Second boots",
        ["basic-cap"] = "Basic cap",
        ["beanie-large"] = "Beanie",
        ["bucket-hat"] = "Bucket hat",
        ["helmet-01"] = "Helmet",
        ["helmet"] = "Helmet",
        ["panto-glasses"] = "Panto glasses",
        ["panto-sunglasses"] = "Panto sunglasses",
        ["male-01"] = "Male body 01",
        ["male-02"] = "Male body 02",
        ["male-03"] = "Male body 03",
        ["female-01"] = "Female body 01",
    };
}
