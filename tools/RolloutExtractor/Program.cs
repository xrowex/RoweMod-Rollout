using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Meshes;
using Newtonsoft.Json;
using CUE4Parse.MappingsProvider;

internal static class Program
{
    private static int Main(string[] args)
    {
        var gameDir = args.Length > 0
            ? args[0]
            : @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate";
        var outDir = args.Length > 1
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dumps");
        outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "json"));
        Directory.CreateDirectory(Path.Combine(outDir, "meshes"));

        var mappings = args.Length > 2 ? args[2] : FindMappings(outDir);
        Console.WriteLine($"Game: {gameDir}");
        Console.WriteLine($"Out:  {outDir}");
        Console.WriteLine($"Map:  {mappings ?? "(none)"}");

        var provider = new DefaultFileProvider(gameDir, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_4));
        if (!string.IsNullOrWhiteSpace(mappings) && File.Exists(mappings))
        {
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings);
        }
        provider.Initialize();
        provider.SubmitKey(new FGuid(), new FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
        provider.PostMount();
        Console.WriteLine($"Files: {provider.Files.Count}");

        string[] packages =
        [
            "RollerSkate/Content/MainFolder/UI/customization/data/DT-upper.uasset",
            "RollerSkate/Content/MainFolder/UI/customization/data/DT-lower.uasset",
            "RollerSkate/Content/MainFolder/UI/customization/data/S-upper.uasset",
            "RollerSkate/Content/MainFolder/UI/customization/data/S-lower.uasset",
            "RollerSkate/Content/MainFolder/Character/upper/hoodie/hoodie-male.uasset",
            "RollerSkate/Content/MainFolder/Character/upper/hoodie/hoodie-female.uasset",
            "RollerSkate/Content/MainFolder/Character/body/male/male-01/male-body-01.uasset",
            "RollerSkate/Content/MainFolder/Character/body/female/female-01/female-body-01.uasset",
            "RollerSkate/Content/MainFolder/Character/body/main-rig/main-rig.uasset",
            "RollerSkate/Content/MainFolder/Blueprints/NewMainCharacter.uasset",
        ];

        foreach (var pkg in packages)
        {
            Console.WriteLine($"-- {pkg}");
            try
            {
                var exports = provider.LoadAllObjects(pkg).ToList();
                var jsonPath = Path.Combine(outDir, "json", Path.GetFileNameWithoutExtension(pkg) + ".json");
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(exports, Formatting.Indented));
                Console.WriteLine($"   json {exports.Count} exports -> {jsonPath}");
                foreach (var exp in exports)
                {
                    DumpSkeleton(exp, outDir);
                    if (exp is USkeletalMesh mesh)
                    {
                        var options = new ExporterOptions
                        {
                            MeshFormat = EMeshFormat.Gltf2,
                            LodFormat = ELodFormat.FirstLod,
                            ExportMorphTargets = false,
                        };
                        var exporter = new MeshExporter(mesh, options);
                        if (exporter.TryWriteToDir(new DirectoryInfo(Path.Combine(outDir, "meshes")), out var label, out var saved))
                        {
                            Console.WriteLine($"   mesh {label} {saved}");
                        }

                        options.MeshFormat = EMeshFormat.UEFormat;
                        exporter = new MeshExporter(mesh, options);
                        exporter.TryWriteToDir(new DirectoryInfo(Path.Combine(outDir, "meshes")), out label, out saved);
                        Console.WriteLine($"   ueformat {label} {saved}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   FAIL {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"   inner {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }
        return 0;
    }

    private static void DumpSkeleton(UObject exp, string outDir)
    {
        if (exp is not USkeleton skeleton) return;
        var bones = skeleton.ReferenceSkeleton?.FinalRefBoneInfo?.Select(b => b.Name.ToString()).ToArray() ?? [];
        var path = Path.Combine(outDir, "json", "main-rig.bones.json");
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            name = skeleton.Name,
            boneCount = bones.Length,
            bones
        }, Formatting.Indented));
        Console.WriteLine($"   skeleton {bones.Length} bones");
    }

    private static string? FindMappings(string outDir)
    {
        var candidates = new[]
        {
            Path.Combine(outDir, "mappings.usmap"),
            Path.Combine(outDir, "Mappings.usmap"),
            Path.Combine(outDir, "RollerSkate.usmap"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
