using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Meshes;
using Newtonsoft.Json;

namespace RolloutExtractor;

public static class ClothingPuller
{
    public static int Run(string[] args)
    {
        Console.WriteLine("ARGS " + args.Length + " [" + string.Join(" | ", args) + "]");
        string? gameDir = null;
        string? outDir = null;
        string? usmap = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--game" or "-g" && i + 1 < args.Length) gameDir = args[++i];
            else if (args[i] is "--out" or "-o" && i + 1 < args.Length) outDir = args[++i];
            else if (args[i] is "--usmap" && i + 1 < args.Length) usmap = args[++i];
            else if (!args[i].StartsWith("-") && gameDir == null) gameDir = args[i];
            else if (!args[i].StartsWith("-") && outDir == null) outDir = args[i];
        }

        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            Console.Error.WriteLine("Need --game <RollerSkate folder>. Got: " + (gameDir ?? "(null)"));
            return 2;
        }
        if (!Directory.Exists(Path.Combine(gameDir, "Content", "Paks")) &&
            !Directory.GetFiles(gameDir, "*.utoc", SearchOption.AllDirectories).Any())
        {
            var up = Path.GetFullPath(Path.Combine(gameDir, "..", ".."));
            if (Directory.Exists(Path.Combine(up, "Content", "Paks")))
                gameDir = up;
        }

        outDir = string.IsNullOrWhiteSpace(outDir)
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dumps", "game-clothing"))
            : Path.GetFullPath(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "meshes"));
        Directory.CreateDirectory(Path.Combine(outDir, "textures"));

        usmap ??= FindMappings(Path.GetFullPath(Path.Combine(outDir, "..")), gameDir);
        Console.WriteLine("Game " + gameDir);
        Console.WriteLine("Out  " + outDir);
        Console.WriteLine("Map  " + (usmap ?? "(none)"));

        NativeCodecs.Initialize(outDir);
        if (!string.IsNullOrWhiteSpace(usmap) && File.Exists(usmap))
            usmap = UsmapCompat.EnsureReadable(usmap);

        var provider = new DefaultFileProvider(gameDir, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_4));
        if (!string.IsNullOrWhiteSpace(usmap) && File.Exists(usmap))
        {
            try
            {
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(usmap);
                Console.WriteLine("Usmap loaded " + usmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Usmap skipped (" + ex.Message + "). Continuing without mappings.");
                provider.MappingsContainer = null;
            }
        }
        provider.Initialize();
        provider.SubmitKey(new FGuid(), new FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
        provider.PostMount();
        Console.WriteLine("Files " + provider.Files.Count);

        var packages = provider.Files.Keys
            .Select(k => k ?? "")
            .Where(IsClothingPackage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (packages.Count == 0)
        {
            Console.WriteLine("No clothing keys matched. Sample keys:");
            foreach (var key in provider.Files.Keys.Take(25))
                Console.WriteLine("  KEY " + key);
        }
        Console.WriteLine("Clothing packages " + packages.Count);

        var meshes = new List<object>();
        var textures = new List<object>();
        var failed = 0;
        var hoodieSaved = false;

        foreach (var pkg in packages)
        {
            try
            {
                var package = provider.LoadPackage(pkg);
                foreach (var exp in package.GetExports())
                {
                    if (exp is USkeletalMesh or UStaticMesh)
                    {
                        var saved = WriteMesh(exp, Path.Combine(outDir, "meshes"));
                        if (saved != null)
                        {
                            Console.WriteLine("MESH " + saved);
                            meshes.Add(new { name = Path.GetFileNameWithoutExtension(saved), file = Rel(outDir, saved), source = pkg });
                            if (Path.GetFileNameWithoutExtension(saved).Contains("hoodie", StringComparison.OrdinalIgnoreCase))
                                hoodieSaved = true;
                        }
                    }
                    else if (exp is UTexture2D tex)
                    {
                        var saved = WriteTexture(tex, Path.Combine(outDir, "textures"));
                        if (saved != null)
                        {
                            Console.WriteLine("TEX  " + saved);
                            textures.Add(new { name = Path.GetFileNameWithoutExtension(saved), file = Rel(outDir, saved), source = pkg });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("FAIL " + pkg + " — " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        var manifest = new
        {
            pulledAt = DateTime.UtcNow,
            game = gameDir,
            meshCount = meshes.Count,
            textureCount = textures.Count,
            failed,
            hoodie = hoodieSaved,
            note = "Local extract from a Steam copy you own. Do not commit or upload these files.",
            meshes,
            textures,
        };
        File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
        Console.WriteLine("WROTE " + Path.Combine(outDir, "manifest.json"));
        Console.WriteLine("DONE meshes=" + meshes.Count + " textures=" + textures.Count + " failed=" + failed + " hoodie=" + hoodieSaved);
        return meshes.Count + textures.Count > 0 ? 0 : 1;
    }

    static bool IsClothingPackage(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        if (p.Contains("/animation") || p.Contains("/anim_") || p.Contains("animbp") ||
            p.Contains("physicsasset") || p.Contains("controlrig") || p.Contains("/maps/") ||
            p.Contains("/fx/") || p.Contains("/vfx") || p.Contains("/audio"))
            return false;
        if (!(p.Contains("mainfolder") || p.Contains("/game/")))
            return false;
        var clothing = p.Contains("/character/upper") || p.Contains("/character/lower") ||
                       p.Contains("/character/hat") || p.Contains("/character/glasses") ||
                       p.Contains("/character/hair") || p.Contains("/character/beard") ||
                       p.Contains("/character/body") || p.Contains("/character/boot") ||
                       p.Contains("/character/skate") || p.Contains("/character/frame") ||
                       p.Contains("/character/wheel") || p.Contains("/ui/customization") ||
                       p.Contains("hoodie") || p.Contains("tshirt") || p.Contains("cargo");
        if (!clothing) return false;
        return !p.Contains('.') || p.EndsWith(".uasset") || p.EndsWith(".umap");
    }

    static string? WriteMesh(object exp, string dir)
    {
        Directory.CreateDirectory(dir);
        var options = new ExporterOptions
        {
            MeshFormat = EMeshFormat.Gltf2,
            LodFormat = ELodFormat.FirstLod,
            ExportMorphTargets = false,
        };
        MeshExporter exporter = exp switch
        {
            USkeletalMesh skel => new MeshExporter(skel, options),
            UStaticMesh stat => new MeshExporter(stat, options),
            _ => throw new InvalidOperationException("not a mesh"),
        };
        if (!exporter.TryWriteToDir(new DirectoryInfo(dir), out _, out var saved) || string.IsNullOrWhiteSpace(saved))
            return null;
        return Path.IsPathRooted(saved) ? saved : Path.Combine(dir, saved);
    }

    static string? WriteTexture(UTexture2D tex, string dir)
    {
        Directory.CreateDirectory(dir);
        var decoded = CUE4Parse_Conversion.Textures.TextureDecoder.Decode(tex);
        if (decoded == null) return null;
        var path = Path.Combine(dir, tex.Name + ".png");
        using var data = decoded.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    static string Rel(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    static string? FindMappings(string dumpsDir, string gameDir)
    {
        var guesses = new[]
        {
            Path.Combine(dumpsDir, "mappings.usmap"),
            Path.Combine(dumpsDir, "Mappings.usmap"),
            Path.Combine(dumpsDir, "RollerSkate.usmap"),
            Path.Combine(gameDir, "Binaries", "Win64", "Mappings.usmap"),
            Path.Combine(gameDir, "Mappings.usmap"),
        };
        return guesses.FirstOrDefault(File.Exists);
    }
}
