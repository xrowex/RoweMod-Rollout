using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RoweMod.App;

sealed class CookTarget
{
    public string MeshName { get; set; } = "tshirt-baggy-male";
    public string MeshDir { get; set; } = "/Game/MainFolder/Character/upper/tshirt-baggy";
    public string Blend { get; set; } = "";
    public string Glb { get; set; } = "";
    public string Gamebind { get; set; } = "";
    public string BindGlb { get; set; } = "";
    public string? ItemJson { get; set; }
}

static class MeshWork
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string TargetPath(string repo) => Path.Combine(repo, "dumps", "cook-target.json");

    public static string? FindBindGlb(string repo)
    {
        foreach (var path in new[]
        {
            Path.Combine(repo, "art", "_ref", "cue-hoodie", "RollerSkate", "Content", "MainFolder", "Character", "upper", "hoodie", "hoodie-male.glb"),
            Path.Combine(repo, "dumps", "game-clothing", "meshes", "RollerSkate", "Content", "MainFolder", "Character", "upper", "hoodie", "hoodie-male.glb"),
        })
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public static CookTarget FromPiece(string repo, ClothingPiece piece)
    {
        var meshName = MeshFileName(piece);
        var meshDir = MeshGameDir(piece);
        var bind = FindBindGlb(repo) ?? "";
        return new CookTarget
        {
            MeshName = meshName,
            MeshDir = meshDir,
            Blend = piece.Blend ?? "",
            Glb = Path.Combine(repo, "art", meshName + ".glb"),
            Gamebind = Path.Combine(repo, "art", meshName + ".gamebind.glb"),
            BindGlb = bind,
            ItemJson = piece.ItemJson,
        };
    }

    public static CookTarget DefaultShirt(string repo)
    {
        return new CookTarget
        {
            MeshName = "tshirt-baggy-male",
            MeshDir = "/Game/MainFolder/Character/upper/tshirt-baggy",
            Blend = Path.Combine(repo, "art", "rig", "main-rig_shirt.blend"),
            Glb = Path.Combine(repo, "art", "tshirt-baggy-male.glb"),
            Gamebind = Path.Combine(repo, "art", "tshirt-baggy-male.gamebind.glb"),
            BindGlb = FindBindGlb(repo) ?? "",
            ItemJson = Path.Combine(repo, "items", "upper", "tshirt-baggy-mod.json"),
        };
    }

    public static void SaveTarget(string repo, CookTarget target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TargetPath(repo))!);
        File.WriteAllText(TargetPath(repo), JsonSerializer.Serialize(target, Json));
    }

    public static CookTarget LoadOrDefault(string repo)
    {
        var path = TargetPath(repo);
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<CookTarget>(File.ReadAllText(path), Json);
                if (loaded != null && !string.IsNullOrWhiteSpace(loaded.MeshName))
                {
                    loaded.BindGlb = string.IsNullOrWhiteSpace(loaded.BindGlb) ? (FindBindGlb(repo) ?? "") : loaded.BindGlb;
                    return loaded;
                }
            }
            catch
            {
                // fall back to the baggy tee
            }
        }
        return DefaultShirt(repo);
    }

    public static Dictionary<string, string> ExportEnv(CookTarget target)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROWE_MESH"] = target.MeshName,
            ["ROWE_OUT_GLB"] = target.Glb,
            ["ROWE_OUT_FBX"] = Path.ChangeExtension(target.Glb, ".fbx"),
            ["ROWE_GAMEBIND"] = target.Gamebind,
        };
        if (!string.IsNullOrWhiteSpace(target.Blend)) env["ROWE_BLEND"] = target.Blend;
        if (!string.IsNullOrWhiteSpace(target.BindGlb)) env["ROWE_BIND_GLB"] = target.BindGlb;
        return env;
    }

    public static ClothingPiece CreateMesh(string repo, string title, string slot)
    {
        var row = Slug(title);
        if (!row.EndsWith("-mod", StringComparison.OrdinalIgnoreCase))
            row += "-mod";

        var tops = slot.Equals("Tops", StringComparison.OrdinalIgnoreCase);
        var table = tops ? "DT-upper" : "DT-lower";
        var folder = tops ? "upper" : "lower";
        var clone = tops ? "tshirt-white" : "oversized-jeans-dark";
        var meshName = row + "-male";
        var meshDir = "/Game/MainFolder/Character/" + folder + "/" + row;
        var meshAsset = meshDir + "/" + meshName;

        var jsonPath = Path.Combine(repo, "items", folder, row + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        if (File.Exists(jsonPath))
            throw new InvalidOperationException("Item already exists: " + row);

        var kit = Path.Combine(repo, "art", "rig", "main-rig.blend");
        if (!File.Exists(kit))
            throw new InvalidOperationException("Missing art/rig/main-rig.blend.");
        var blend = Path.Combine(repo, "art", "rig", row + ".blend");
        if (!File.Exists(blend))
            File.Copy(kit, blend);

        var spec = new Dictionary<string, object?>
        {
            ["table"] = table,
            ["cloneRow"] = clone,
            ["row"] = row,
            ["localizedName"] = title.Trim() + " (Mod)",
            ["price"] = 0,
            ["colour"] = new[] { 1.0, 1.0, 1.0, 1.0 },
            [tops ? "upperMale" : "lowerMale"] = meshAsset,
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(spec, Json));

        return new ClothingPiece
        {
            Id = row,
            Title = title.Trim() + " (Mod)",
            Slot = slot,
            Kind = ClothingKind.Mesh,
            BasedOn = clone,
            ItemJson = jsonPath,
            Blend = blend,
            Folder = Path.GetDirectoryName(jsonPath),
            Table = table,
            MeshAsset = meshAsset,
            CanExport = true,
        };
    }

    public static void SetSample(string jsonPath, bool sample)
    {
        var node = JsonNode.Parse(File.ReadAllText(jsonPath)) as JsonObject
            ?? throw new InvalidOperationException("Could not read " + jsonPath);
        node["sample"] = sample;
        File.WriteAllText(jsonPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string Slug(string title)
    {
        var s = title.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(s)) s = "new-garment";
        return s;
    }

    static string MeshFileName(ClothingPiece piece)
    {
        if (!string.IsNullOrWhiteSpace(piece.MeshAsset))
            return piece.MeshAsset.Split('/').Last();
        var row = piece.Id.EndsWith("-mod", StringComparison.OrdinalIgnoreCase)
            ? piece.Id[..^4]
            : piece.Id;
        return row + "-male";
    }

    static string MeshGameDir(ClothingPiece piece)
    {
        if (!string.IsNullOrWhiteSpace(piece.MeshAsset))
        {
            var i = piece.MeshAsset.LastIndexOf('/');
            if (i > 0) return piece.MeshAsset[..i];
        }
        var folder = piece.Slot.Equals("Bottoms", StringComparison.OrdinalIgnoreCase) ? "lower" : "upper";
        return "/Game/MainFolder/Character/" + folder + "/" + piece.Id;
    }
}
