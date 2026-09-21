using System.Diagnostics;
using System.Text.Json;

namespace RoweMod.App;

enum ClothingKind
{
    Texture,
    Mesh,
    Kit,
}

sealed class ClothingPiece
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Slot { get; init; }
    public required ClothingKind Kind { get; init; }
    public string? BasedOn { get; init; }
    public string? ItemJson { get; init; }
    public string? Blend { get; init; }
    public string? Model { get; init; }
    public string? Folder { get; init; }
    public string? TexturesDir { get; init; }
    public string? Preview { get; init; }
    public string? Table { get; init; }
    public string? Source { get; init; }
    public string? PaintFile { get; init; }
    public string? MeshAsset { get; init; }
    public bool Sample { get; init; }
    public bool CanExport { get; init; }

    public string OpenFile => Blend ?? Model ?? ItemJson ?? Folder ?? "";
    public bool FromGame => Id.StartsWith("game:", StringComparison.OrdinalIgnoreCase)
        || Id.StartsWith("tex:", StringComparison.OrdinalIgnoreCase);
    public bool HasModeledMesh => Kind == ClothingKind.Mesh && Model != null;
    public bool IsEmptyRig => Kind == ClothingKind.Mesh && !FromGame && Blend != null && Model == null;
}

static class ClothingLibrary
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ClothingPiece> Scan(string repo)
    {
        var pieces = new List<ClothingPiece>();
        AddKit(repo, pieces);
        AddItems(repo, pieces);
        AddPulled(repo, pieces);
        return pieces;
    }

    public static string SlotName(string table) => table switch
    {
        "DT-upper" => "Tops",
        "DT-lower" => "Bottoms",
        "DT-hats" => "Hats",
        "DT-glasses" => "Glasses",
        "DT-hair" => "Hair",
        "DT-beard" => "Beard",
        "DT-bodytypes" => "Body",
        "DT-skin" => "Skin",
        "DT-eyes" => "Eyes",
        "DT-boot" => "Boots",
        "DT-frames" => "Frames",
        "DT-wheels" => "Wheels",
        _ => table,
    };

    public static void OpenFolder(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            return;
        }
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
    }

    public static void OpenFile(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenInBlender(string blender, string file)
    {
        if (!File.Exists(blender) || !File.Exists(file)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = blender,
            Arguments = "\"" + file + "\"",
            UseShellExecute = false,
        });
    }

    static void AddKit(string repo, List<ClothingPiece> pieces)
    {
        var blend = Path.Combine(repo, "art", "rig", "main-rig.blend");
        var fbx = Path.Combine(repo, "art", "rig", "main-rig.fbx");
        pieces.Add(new ClothingPiece
        {
            Id = "kit",
            Title = "Skeleton only — not clothing",
            Slot = "Kit",
            Kind = ClothingKind.Kit,
            Blend = File.Exists(blend) ? blend : null,
            Model = File.Exists(fbx) ? fbx : null,
            Folder = Path.Combine(repo, "art", "rig"),
        });
    }

    static void AddItems(string repo, List<ClothingPiece> pieces)
    {
        var itemsDir = Path.Combine(repo, "items");
        if (!Directory.Exists(itemsDir)) return;
        foreach (var json in Directory.GetFiles(itemsDir, "*.json", SearchOption.AllDirectories))
        {
            ItemDto? spec;
            try
            {
                spec = JsonSerializer.Deserialize<ItemDto>(File.ReadAllText(json), JsonOpts);
            }
            catch
            {
                continue;
            }
            if (spec == null || string.IsNullOrWhiteSpace(spec.Row)) continue;

            var row = spec.Row;
            var stem = row.EndsWith("-mod", StringComparison.OrdinalIgnoreCase) ? row[..^4] : row;
            var meshAsset = spec.UpperMale ?? spec.LowerMale;
            var meshFile = string.IsNullOrWhiteSpace(meshAsset) ? null : meshAsset.Split('/').Last();
            var newMesh = !string.IsNullOrWhiteSpace(meshAsset);
            string? blend = FirstExisting(
                Path.Combine(repo, "art", "rig", row + ".blend"),
                Path.Combine(repo, "art", "rig", stem + ".blend"),
                row.Contains("tshirt", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(repo, "art", "rig", "main-rig_shirt.blend")
                    : null,
                Path.Combine(repo, "art", row + ".blend"),
                Path.Combine(repo, "art", stem + ".blend"));
            string? model = FirstExisting(
                meshFile == null ? null : Path.Combine(repo, "art", meshFile + ".gamebind.glb"),
                meshFile == null ? null : Path.Combine(repo, "art", meshFile + ".glb"),
                meshFile == null ? null : Path.Combine(repo, "art", meshFile + ".fbx"),
                Path.Combine(repo, "art", row + ".glb"),
                Path.Combine(repo, "art", stem + ".fbx"));
            string? textures = FirstExistingDir(
                Path.Combine(repo, "art", "textures", row),
                Path.Combine(repo, "art", "textures", stem));
            if (textures == null && row.Contains("tshirt", StringComparison.OrdinalIgnoreCase))
                textures = FirstExistingDir(Path.Combine(repo, "art", "textures", "tshirt-baggy"));
            if (textures == null && row.Contains("jeans", StringComparison.OrdinalIgnoreCase))
                textures = FirstExistingDir(Path.Combine(repo, "art", "textures", "oversized-jeans"));
            if (row.Contains("hoodie", StringComparison.OrdinalIgnoreCase) && !newMesh)
            {
                // Recolor of the live game hoodie. Do not open the local
                // blockout blend — it is not the in-game mesh.
                blend = null;
            }

            var preview = FirstExisting(
                Path.Combine(repo, "art", "previews", "T_" + row + ".png"),
                Path.Combine(repo, "art", "previews", row + ".png"));

            pieces.Add(new ClothingPiece
            {
                Id = row,
                Title = string.IsNullOrWhiteSpace(spec.LocalizedName) ? row : spec.LocalizedName,
                Slot = SlotName(spec.Table ?? "DT-upper"),
                Kind = newMesh ? ClothingKind.Mesh : ClothingKind.Texture,
                BasedOn = spec.CloneRow,
                ItemJson = json,
                Blend = blend,
                Model = model,
                Folder = Path.GetDirectoryName(json),
                TexturesDir = textures,
                Preview = preview,
                Table = spec.Table,
                PaintFile = TextureMods.FindAlbedoPng(repo, row, spec.Albedo ?? ""),
                MeshAsset = meshAsset,
                Sample = spec.Sample,
                CanExport = newMesh && !spec.Sample && blend != null,
            });
        }
    }

    static void AddPulled(string repo, List<ClothingPiece> pieces)
    {
        var root = Path.Combine(repo, "dumps", "game-clothing");
        if (!Directory.Exists(root)) return;
        var meshDir = Path.Combine(root, "meshes");
        if (Directory.Exists(meshDir))
        {
            foreach (var file in Directory.GetFiles(meshDir, "*.glb", SearchOption.AllDirectories)
                         .Concat(Directory.GetFiles(meshDir, "*.gltf", SearchOption.AllDirectories)))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                pieces.Add(new ClothingPiece
                {
                    Id = "game:" + name,
                    Title = name + " (from your game)",
                    Slot = GuessSlot(file),
                    Kind = ClothingKind.Mesh,
                    Model = file,
                    Folder = Path.GetDirectoryName(file),
                });
            }
        }
        var texDir = Path.Combine(root, "textures");
        if (Directory.Exists(texDir))
        {
            var sources = LoadPullSources(root);
            foreach (var file in Directory.GetFiles(texDir, "*.png", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                sources.TryGetValue(name, out var source);
                pieces.Add(new ClothingPiece
                {
                    Id = "tex:" + name,
                    Title = name + " (from your game)",
                    Slot = GuessSlot(source ?? file),
                    Kind = ClothingKind.Texture,
                    Folder = Path.GetDirectoryName(file),
                    TexturesDir = Path.GetDirectoryName(file),
                    Preview = file,
                    Source = source,
                    PaintFile = file,
                });
            }
        }
    }

    static Dictionary<string, string> LoadPullSources(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifest = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifest)) return map;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            if (!doc.RootElement.TryGetProperty("textures", out var arr)) return map;
            foreach (var t in arr.EnumerateArray())
            {
                if (!t.TryGetProperty("name", out var n) || n.GetString() is not { } name) continue;
                if (t.TryGetProperty("source", out var s) && s.GetString() is { } source)
                    map[name] = source;
            }
        }
        catch
        {
            // pull manifest is optional
        }
        return map;
    }

    static string GuessSlot(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        if (p.Contains("/upper") || p.Contains("hoodie") || p.Contains("tshirt") || p.Contains("shirt")) return "Tops";
        if (p.Contains("/lower") || p.Contains("cargo") || p.Contains("jean") || p.Contains("pant")) return "Bottoms";
        if (p.Contains("hat") || p.Contains("helmet")) return "Hats";
        if (p.Contains("glass")) return "Glasses";
        if (p.Contains("hair")) return "Hair";
        if (p.Contains("beard")) return "Beard";
        if (p.Contains("boot")) return "Boots";
        if (p.Contains("frame")) return "Frames";
        if (p.Contains("wheel")) return "Wheels";
        if (p.Contains("body") || p.Contains("skin")) return "Body";
        return "Game";
    }

    static string? FirstExisting(params string?[] paths)
    {
        foreach (var p in paths)
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
        }
        return null;
    }

    static string? FirstExistingDir(params string?[] paths)
    {
        foreach (var p in paths)
        {
            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) return p;
        }
        return null;
    }

    sealed class ItemDto
    {
        public string? Table { get; set; }
        public string? CloneRow { get; set; }
        public string? Row { get; set; }
        public string? LocalizedName { get; set; }
        public string? UpperMale { get; set; }
        public string? LowerMale { get; set; }
        public string? Albedo { get; set; }
        public bool Sample { get; set; }
    }
}
