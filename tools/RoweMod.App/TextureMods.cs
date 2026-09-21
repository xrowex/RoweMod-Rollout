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
        if (piece.Slot is "Body" or "Skin" or "Eyes") return false;
        if (piece.ItemJson != null) return true;
        if (piece.Id.StartsWith("tex:", StringComparison.OrdinalIgnoreCase))
        {
            if (piece.Slot is "Wheels")
            {
                var n = (piece.AssetName ?? piece.PaintFile ?? piece.Title).ToLowerInvariant();
                return !n.Contains("normal") && !n.Contains("rough") && !n.Contains("mask");
            }
            return LooksLikeAlbedo(piece.AssetName ?? piece.PaintFile ?? piece.Title);
        }
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
        if (table is "DT-bodytypes" or "DT-skin" or "DT-eyes")
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

        var folder = GameFolder(table);
        var albedo = "/Game/MainFolder/Character/" + folder + "/" + row + "/T_" + row + "-albedo";
        var normal = "/Game/MainFolder/Character/" + folder + "/" + row + "/T_" + row + "-normal";
        var destNrm = Path.Combine(texDir, "T_" + row + "-normal.png");
        if (table != "DT-wheels" && !File.Exists(destNrm))
            WriteFlatNormal(destNrm, destPng);

        var jsonPath = existing?.JsonPath ?? Path.Combine(repo, "items", slotDir, row + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        object spec;
        if (table == "DT-wheels")
        {
            spec = new
            {
                table,
                cloneRow = string.IsNullOrWhiteSpace(cloneRow) ? "wheels-white" : cloneRow,
                row,
                localizedName = existing?.LocalizedName ?? Human(cloneRow) + " (Paint)",
                price = 0,
                refs = new Dictionary<string, string> { ["WheelAlbedo"] = albedo },
            };
        }
        else
        {
            spec = new
            {
                table,
                cloneRow,
                row,
                localizedName = existing?.LocalizedName ?? Human(cloneRow) + " (Paint)",
                price = 0,
                colour = new[] { 1.0, 1.0, 1.0, 1.0 },
                albedo,
                normal,
            };
        }
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(spec, JsonWrite));
        var title = existing?.LocalizedName ?? Human(cloneRow) + " (Paint)";
        return new PaintModResult(row, title, jsonPath, destPng, texDir);
    }

    public static bool NeedsCook(string repo)
    {
        foreach (var json in ItemFiles(repo))
        {
            ItemFile? spec;
            try { spec = JsonSerializer.Deserialize<ItemFile>(File.ReadAllText(json), JsonRead); }
            catch { continue; }
            if (spec == null || spec.Sample) continue;
            foreach (var gamePath in TexturePaths(spec))
            {
                if (!gamePath.StartsWith("/Game/", StringComparison.Ordinal)) continue;
                var png = FindAlbedoPng(repo, spec.Row, gamePath);
                if (png == null) continue;
                var cooked = CookedFile(repo, gamePath);
                if (cooked == null || File.GetLastWriteTimeUtc(png) > File.GetLastWriteTimeUtc(cooked))
                    return true;
            }
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
            if (LooksLikeAlbedo(name) &&
                !string.IsNullOrWhiteSpace(row) &&
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
                var category = parts[idx + 1];
                var family = parts[idx + 2].Replace("overized", "oversized", StringComparison.OrdinalIgnoreCase);
                var variant = idx + 3 < parts.Length ? parts[idx + 3] : "";
                if (category.Equals("skates", StringComparison.OrdinalIgnoreCase))
                {
                    family = variant.Replace("overized", "oversized", StringComparison.OrdinalIgnoreCase);
                    variant = idx + 4 < parts.Length ? parts[idx + 4] : "";
                }
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
            var n = Path.GetFileNameWithoutExtension(piece.AssetName ?? piece.Title)
                .Replace(" (from your game)", "", StringComparison.OrdinalIgnoreCase);
            if (!LooksLikeAlbedo(n) && !n.Contains("FABRIC", StringComparison.OrdinalIgnoreCase) &&
                !n.Contains("7148", StringComparison.OrdinalIgnoreCase))
                cloneRow = n;
        }
        if (table == "DT-wheels")
            cloneRow = "wheels-white";
        if (table == "DT-frames" && string.IsNullOrWhiteSpace(cloneRow))
            cloneRow = "standard-flat-frame";
        if (table == "DT-boot" && string.IsNullOrWhiteSpace(cloneRow))
            cloneRow = "standard-boot";

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
                    HasTexture(spec))
                    return new ExistingMod(spec.Row ?? "", spec.LocalizedName, json);
            }
            catch { /* skip bad item */ }
        }
        return null;
    }

    static bool HasTexture(ItemFile spec) =>
        !string.IsNullOrWhiteSpace(spec.Albedo) || TexturePaths(spec).Any();

    public static void WriteFlatNormal(string dest, string? albedoPng = null)
    {
        var w = 1024;
        var h = 1024;
        if (albedoPng != null && File.Exists(albedoPng))
        {
            using var src = Image.FromFile(albedoPng);
            w = Math.Max(64, src.Width);
            h = Math.Max(64, src.Height);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.FromArgb(128, 128, 255));
        bmp.Save(dest, System.Drawing.Imaging.ImageFormat.Png);
    }

    static IEnumerable<string> TexturePaths(ItemFile spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Albedo))
            yield return spec.Albedo;
        if (!string.IsNullOrWhiteSpace(spec.Normal))
            yield return spec.Normal;
        if (!string.IsNullOrWhiteSpace(spec.Roughness))
            yield return spec.Roughness;
        if (spec.Refs == null) yield break;
        foreach (var kv in spec.Refs)
        {
            if (kv.Key.Contains("Mesh", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(kv.Value))
                yield return kv.Value;
        }
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
        public string? Normal { get; set; }
        public string? Roughness { get; set; }
        public Dictionary<string, string>? Refs { get; set; }
        public bool Sample { get; set; }
    }
}

readonly record struct PaintModResult(string Row, string Title, string JsonPath, string Png, string TexDir);
