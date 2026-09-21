using System.Text.Json;
using System.Text.Json.Nodes;

namespace RoweMod.App;

static class TextureMods
{
    static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static readonly JsonSerializerOptions JsonRead = new() { PropertyNameCaseInsensitive = true };

    public static bool IsPaintTarget(ClothingPiece piece)
    {
        if (piece.Kind != ClothingKind.Texture) return false;
        if (piece.Sample) return false;
        if (piece.ItemJson != null) return true;
        if (piece.Id.StartsWith("tex:", StringComparison.OrdinalIgnoreCase))
            return LooksLikeAlbedo(piece.Title);
        return false;
    }

    public static bool LooksLikeAlbedo(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("normal") || n.Contains("rough") || n.Contains("mask") ||
            n.Contains("metallic") || n.Contains("ao"))
            return false;
        return n.Contains("base_color") || n.Contains("base-color") || n.Contains("albedo") ||
               n.Contains("basecolor") || n.Contains("diffuse");
    }

    public static PaintModResult? Promote(string repo, ClothingPiece piece)
    {
        var png = piece.PaintFile ?? piece.Preview;
        if (png == null || !File.Exists(png)) return null;
        if (!Guess(piece, out var table, out var cloneRow))
            return null;

        var existing = FindExisting(repo, cloneRow);
        var row = existing?.Row ?? (cloneRow.EndsWith("-mod", StringComparison.OrdinalIgnoreCase)
            ? cloneRow
            : cloneRow + "-mod");
        var slotDir = SlotFolder(table);
        var texDir = Path.Combine(repo, "art", "textures", row);
        Directory.CreateDirectory(texDir);
        var destPng = Path.Combine(texDir, "T_" + row + "-albedo.png");
        File.Copy(png, destPng, overwrite: true);

        var albedo = "/Game/MainFolder/Character/" + GameFolder(table) + "/" + row + "/T_" + row + "-albedo";
        var jsonPath = existing?.JsonPath ?? Path.Combine(repo, "items", slotDir, row + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        var spec = new
        {
            table,
            cloneRow,
            row,
            localizedName = existing?.LocalizedName ?? Human(cloneRow) + " (Paint)",
            price = 0,
            colour = new[] { 1.0, 1.0, 1.0, 1.0 },
            albedo,
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(spec, JsonWrite));
        return new PaintModResult(row, spec.localizedName, jsonPath, destPng, texDir);
    }

    public static bool NeedsCook(string repo)
    {
        foreach (var json in ItemFiles(repo))
        {
            ItemFile? spec;
            try { spec = JsonSerializer.Deserialize<ItemFile>(File.ReadAllText(json), JsonRead); }
            catch { continue; }
            if (spec == null || spec.Sample) continue;
            if (spec.Albedo == null || !spec.Albedo.StartsWith("/Game/", StringComparison.Ordinal))
                continue;
            var png = FindAlbedoPng(repo, spec.Row, spec.Albedo);
            if (png == null) continue;
            var cooked = CookedFile(repo, spec.Albedo);
            if (cooked == null || File.GetLastWriteTimeUtc(png) > File.GetLastWriteTimeUtc(cooked))
                return true;
        }
        return false;
    }

    public static string? FindAlbedoPng(string repo, string? row, string albedoPath)
    {
        var name = albedoPath.Split('/').Last();
        var hits = new List<string>();
        var texRoot = Path.Combine(repo, "art", "textures");
        if (!Directory.Exists(texRoot)) return null;
        foreach (var file in Directory.GetFiles(texRoot, "*.png", SearchOption.AllDirectories))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Equals(name, StringComparison.OrdinalIgnoreCase))
                return file;
            if (!string.IsNullOrWhiteSpace(row) &&
                Path.GetFileName(Path.GetDirectoryName(file))!.Equals(row, StringComparison.OrdinalIgnoreCase) &&
                stem.Contains("albedo", StringComparison.OrdinalIgnoreCase))
                hits.Add(file);
        }
        return hits.FirstOrDefault();
    }

    static string? CookedFile(string repo, string gamePath)
    {
        var rel = gamePath["/Game/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var dir = Path.Combine(
            repo, "ue", "RollerSkate", "Saved", "Cooked", "Windows", "RollerSkate", "Content",
            Path.GetDirectoryName(rel) ?? "");
        var name = Path.GetFileName(rel);
        var uasset = Path.Combine(dir, name + ".uasset");
        return File.Exists(uasset) ? uasset : null;
    }

    static bool Guess(ClothingPiece piece, out string table, out string cloneRow)
    {
        table = piece.Table ?? TableFromSlot(piece.Slot);
        cloneRow = "";
        var source = (piece.Source ?? "").Replace('\\', '/');
        if (source.Length > 0)
        {
            var parts = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.FindIndex(parts, p => p.Equals("Character", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx + 2 < parts.Length)
            {
                var family = parts[idx + 2].Replace("overized", "oversized", StringComparison.OrdinalIgnoreCase);
                var variant = idx + 3 < parts.Length ? parts[idx + 3] : "";
                if (variant.Contains('.', StringComparison.Ordinal)) variant = "";
                if (family.Equals("cargos", StringComparison.OrdinalIgnoreCase) &&
                    (IsJunkVariant(variant) || variant.Equals("cotton", StringComparison.OrdinalIgnoreCase)))
                    cloneRow = "cargos-black";
                else if (IsJunkVariant(variant))
                    cloneRow = family;
                else if (!string.IsNullOrWhiteSpace(variant))
                    cloneRow = family + "-" + variant;
                else
                    cloneRow = family;
            }
        }

        if (string.IsNullOrWhiteSpace(cloneRow) && !string.IsNullOrWhiteSpace(piece.BasedOn))
            cloneRow = piece.BasedOn;
        if (string.IsNullOrWhiteSpace(cloneRow))
        {
            var n = Path.GetFileNameWithoutExtension(piece.Title)
                .Replace(" (from your game)", "", StringComparison.OrdinalIgnoreCase);
            if (!LooksLikeAlbedo(n) && !n.Contains("FABRIC", StringComparison.OrdinalIgnoreCase) &&
                !n.Contains("7148", StringComparison.OrdinalIgnoreCase))
                cloneRow = n;
        }

        return !string.IsNullOrWhiteSpace(table) && !string.IsNullOrWhiteSpace(cloneRow);
    }

    static bool IsJunkVariant(string variant)
    {
        var v = variant.ToLowerInvariant();
        return v is "male" or "female" or "cotton" or "customization-icons" or "icons";
    }

    static ExistingMod? FindExisting(string repo, string cloneRow)
    {
        foreach (var json in ItemFiles(repo))
        {
            try
            {
                var spec = JsonSerializer.Deserialize<ItemFile>(File.ReadAllText(json), JsonRead);
                if (spec?.CloneRow != null &&
                    !spec.Sample &&
                    spec.CloneRow.Equals(cloneRow, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(spec.Albedo))
                    return new ExistingMod(spec.Row ?? "", spec.LocalizedName, json);
            }
            catch { /* skip bad item */ }
        }
        return null;
    }

    static IEnumerable<string> ItemFiles(string repo)
    {
        var dir = Path.Combine(repo, "items");
        if (!Directory.Exists(dir)) yield break;
        foreach (var json in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            yield return json;
    }

    static string TableFromSlot(string slot) => slot switch
    {
        "Tops" => "DT-upper",
        "Bottoms" => "DT-lower",
        "Hats" => "DT-hats",
        "Glasses" => "DT-glasses",
        "Hair" => "DT-hair",
        "Beard" => "DT-beard",
        "Body" => "DT-bodytypes",
        "Skin" => "DT-skin",
        "Eyes" => "DT-eyes",
        "Boots" => "DT-boot",
        "Frames" => "DT-frames",
        "Wheels" => "DT-wheels",
        _ => "DT-upper",
    };

    static string SlotFolder(string table) => table switch
    {
        "DT-upper" => "upper",
        "DT-lower" => "lower",
        "DT-hats" => "hats",
        "DT-glasses" => "glasses",
        "DT-hair" => "hair",
        "DT-beard" => "beard",
        "DT-bodytypes" => "body",
        "DT-skin" => "skin",
        "DT-eyes" => "eyes",
        "DT-boot" => "boots",
        "DT-frames" => "frames",
        "DT-wheels" => "wheels",
        _ => "upper",
    };

    static string GameFolder(string table) => table switch
    {
        "DT-lower" => "lower",
        "DT-hats" => "hats",
        "DT-glasses" => "glasses",
        "DT-hair" => "hair",
        "DT-beard" => "beard",
        "DT-bodytypes" => "body",
        "DT-skin" => "body/customization/body",
        "DT-eyes" => "body/customization/eyes",
        "DT-boot" => "skates/boots",
        "DT-frames" => "skates/frames",
        "DT-wheels" => "skates/wheels",
        _ => "upper",
    };

    static string Human(string row) =>
        string.Join(' ', row.Replace("-mod", "").Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    sealed record ExistingMod(string Row, string? LocalizedName, string JsonPath);

    sealed class ItemFile
    {
        public string? Table { get; set; }
        public string? CloneRow { get; set; }
        public string? Row { get; set; }
        public string? LocalizedName { get; set; }
        public string? Albedo { get; set; }
        public bool Sample { get; set; }
    }
}

readonly record struct PaintModResult(string Row, string Title, string JsonPath, string Png, string TexDir);
