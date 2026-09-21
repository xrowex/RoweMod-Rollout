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
    public string? AssetName { get; init; }
    public string? Garment { get; init; }
    public string? Variant { get; init; }
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

    public static bool IsClothingSlot(string? slot) =>
        slot is "Tops" or "Bottoms" or "Hats" or "Glasses" or "Hair" or "Beard";

    public static bool IsSkateSlot(string? slot) =>
        slot is "Boots" or "Frames" or "Wheels";

    public static bool IsSkateTable(string? table) =>
        table is "DT-boot" or "DT-frames" or "DT-wheels";

    public static bool IsSkate(ClothingPiece piece) =>
        IsSkateSlot(piece.Slot) || IsSkateTable(piece.Table);

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
        var ext = Path.GetExtension(file);
        string args;
        if (ext.Equals(".blend", StringComparison.OrdinalIgnoreCase))
        {
            args = "\"" + file + "\"";
        }
        else
        {
            var script = Path.Combine(ToolPaths.RepoRoot, "art", "open_model.py");
            if (!File.Exists(script))
                throw new InvalidOperationException("Missing art/open_model.py.");
            args = "--python \"" + script + "\" -- \"" + file + "\"";
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = blender,
            Arguments = args,
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
            var meshAsset = spec.UpperMale ?? spec.LowerMale ?? MeshRef(spec.Refs);
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
                Path.Combine(repo, "art", "skates", row + ".glb"),
                Path.Combine(repo, "art", "skates", stem + ".glb"),
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
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddManifestMeshes(root, pieces, listed);
        AddManifestTextures(root, pieces, listed);
        AddLooseMeshes(root, pieces, listed);
        AddLooseTextures(root, pieces, listed);
    }

    static void AddManifestMeshes(string root, List<ClothingPiece> pieces, HashSet<string> listed)
    {
        foreach (var (name, file, source) in ManifestEntries(root, "meshes"))
        {
            var full = ResolvePulled(root, file, name, ".glb", ".gltf");
            if (full == null || !listed.Add(full)) continue;
            AddGameMesh(pieces, full, source);
        }
    }

    static void AddManifestTextures(string root, List<ClothingPiece> pieces, HashSet<string> listed)
    {
        foreach (var (name, file, source) in ManifestEntries(root, "textures"))
        {
            var full = ResolvePulled(root, file, name, ".png");
            if (full == null || !listed.Add(full)) continue;
            AddGameTexture(pieces, full, name, source);
        }
    }

    static void AddLooseMeshes(string root, List<ClothingPiece> pieces, HashSet<string> listed)
    {
        var meshDir = Path.Combine(root, "meshes");
        if (!Directory.Exists(meshDir)) return;
        foreach (var file in Directory.GetFiles(meshDir, "*.glb", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(meshDir, "*.gltf", SearchOption.AllDirectories)))
        {
            if (!listed.Add(file)) continue;
            AddGameMesh(pieces, file, file);
        }
    }

    static void AddLooseTextures(string root, List<ClothingPiece> pieces, HashSet<string> listed)
    {
        var texDir = Path.Combine(root, "textures");
        if (!Directory.Exists(texDir)) return;
        foreach (var file in Directory.GetFiles(texDir, "*.png", SearchOption.AllDirectories))
        {
            if (!listed.Add(file)) continue;
            AddGameTexture(pieces, file, Path.GetFileNameWithoutExtension(file), file);
        }
    }

    static void AddGameMesh(List<ClothingPiece> pieces, string file, string source)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var info = GameAssetNames.Parse(source.Length > 0 ? source : file);
        var sex = name.EndsWith("-female", StringComparison.OrdinalIgnoreCase) ? "female"
            : name.EndsWith("-male", StringComparison.OrdinalIgnoreCase) ? "male"
            : info.Variant;
        var labeled = info with { Variant = sex };
        pieces.Add(new ClothingPiece
        {
            Id = "game:" + name,
            Title = GameAssetNames.Title(labeled, mesh: true),
            Slot = info.Slot == "Game" ? GuessSlot(file) : info.Slot,
            Kind = ClothingKind.Mesh,
            Model = file,
            Folder = Path.GetDirectoryName(file),
            Source = source,
            AssetName = name,
            Garment = info.Garment,
            Variant = sex,
            MeshAsset = name,
            BasedOn = info.Family,
        });
    }

    static void AddGameTexture(List<ClothingPiece> pieces, string file, string name, string source)
    {
        var info = GameAssetNames.Parse(source.Length > 0 ? source : file);
        var rel = name + "@" + (info.Variant ?? info.Family);
        pieces.Add(new ClothingPiece
        {
            Id = "tex:" + rel,
            Title = GameAssetNames.Title(info, mesh: false),
            Slot = info.Slot == "Game" ? GuessSlot(source.Length > 0 ? source : file) : info.Slot,
            Kind = ClothingKind.Texture,
            Folder = Path.GetDirectoryName(file),
            TexturesDir = Path.GetDirectoryName(file),
            Preview = file,
            Source = source,
            PaintFile = file,
            AssetName = name,
            Garment = info.Garment,
            Variant = info.Variant,
            MeshAsset = info.MeshHint,
            BasedOn = string.IsNullOrWhiteSpace(info.Variant) || IsSexFolder(info.Variant)
                ? info.Family
                : info.Family + "-" + info.Variant,
        });
    }

    static bool IsSexFolder(string? variant) =>
        variant != null && (variant.Equals("male", StringComparison.OrdinalIgnoreCase)
            || variant.Equals("female", StringComparison.OrdinalIgnoreCase));

    static IEnumerable<(string Name, string File, string Source)> ManifestEntries(string root, string key)
    {
        var manifest = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifest)) yield break;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllText(manifest)); }
        catch { yield break; }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr)) yield break;
            foreach (var t in arr.EnumerateArray())
            {
                var name = t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var file = t.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "";
                var source = t.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                if (name.Length == 0 && file.Length == 0) continue;
                yield return (name, file, source);
            }
        }
    }

    static string? ResolvePulled(string root, string rel, string name, params string[] exts)
    {
        if (!string.IsNullOrWhiteSpace(rel))
        {
            var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return full;
        }
        foreach (var folder in new[] { "textures", "meshes" })
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var ext in exts)
            {
                var flat = Path.Combine(dir, name + ext);
                if (File.Exists(flat)) return flat;
            }
        }
        return null;
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
        public Dictionary<string, string>? Refs { get; set; }
        public string? Albedo { get; set; }
        public bool Sample { get; set; }
    }

    static string? MeshRef(Dictionary<string, string>? refs)
    {
        if (refs == null) return null;
        foreach (var key in new[] { "BladeMesh", "SkatesMesh", "HatMesh", "GlassesMesh" })
        {
            if (refs.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path))
                return path;
        }
        foreach (var kv in refs)
        {
            if (kv.Key.Contains("Mesh", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value;
        }
        return null;
    }
}
