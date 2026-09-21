using System.Text;
using System.Text.Json;
using DtPatcher;

namespace RoweMod.Core;

public static class ModPackage
{
    static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static readonly string[] ExtraExtensions = { ".uasset", ".uexp", ".ubulk", ".uptnl" };

    public static ItemSpec ReadItem(string itemJson)
    {
        var spec = JsonSerializer.Deserialize<ItemSpec>(File.ReadAllText(itemJson), Json)
            ?? throw new InvalidOperationException("Could not read " + itemJson);
        spec.Normalize();
        return spec;
    }

    public static void ValidateForShare(ItemSpec spec)
    {
        if (spec.Sample)
            throw new InvalidOperationException("Templates cannot be submitted.");
        if (string.IsNullOrWhiteSpace(spec.Row) || !spec.Row.EndsWith("-mod", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("row must end with -mod.");
        if (string.IsNullOrWhiteSpace(spec.CloneRow))
            throw new InvalidOperationException("item needs cloneRow.");
        foreach (var path in spec.AssetPaths())
        {
            var reason = ForbiddenReason(path, spec.Row);
            if (reason != null)
                throw new InvalidOperationException(reason + " (" + path + ")");
        }
    }

    public static string? ForbiddenReason(string gamePath, string row)
    {
        var p = gamePath.Replace('\\', '/');
        if (p.Contains("dumps/", StringComparison.OrdinalIgnoreCase))
            return "Do not share pulled dumps";
        if (p.Contains("main-rig", StringComparison.OrdinalIgnoreCase))
            return "Do not pack main-rig";
        if (p.Contains("MI-Upper", StringComparison.OrdinalIgnoreCase))
            return "Do not pack MI-Upper";
        if (!p.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
            return null;
        var stem = row.EndsWith("-mod", StringComparison.OrdinalIgnoreCase) ? row[..^4] : row;
        var lower = p.ToLowerInvariant();
        if (lower.Contains("-mod") || lower.Contains("/" + stem.ToLowerInvariant() + "/"))
            return null;
        return "Stock game path — share only your -mod assets";
    }

    public static string Build(string repo, string itemJson, string author, string destDir)
    {
        var spec = ReadItem(itemJson);
        ValidateForShare(spec);
        var id = spec.Row;
        Directory.CreateDirectory(destDir);
        foreach (var old in Directory.Exists(destDir) ? Directory.GetFiles(destDir, "*", SearchOption.AllDirectories) : Array.Empty<string>())
            File.Delete(old);

        var slot = SlotName(spec.Table);
        var info = new ModInfo
        {
            Id = id,
            Title = string.IsNullOrWhiteSpace(spec.LocalizedName) ? id : spec.LocalizedName,
            Author = string.IsNullOrWhiteSpace(author) ? "anon" : author.Trim(),
            Slot = slot,
            Row = spec.Row,
            Version = DateTime.UtcNow.ToString("yyyyMMddHHmm"),
            Table = spec.Table,
        };
        File.WriteAllText(Path.Combine(destDir, "mod.json"), JsonSerializer.Serialize(info, Json), Utf8);
        File.Copy(itemJson, Path.Combine(destDir, "item.json"), overwrite: true);

        var preview = FindPreview(repo, spec);
        if (preview != null)
            File.Copy(preview, Path.Combine(destDir, "preview.png"), overwrite: true);

        var files = new List<string> { "mod.json", "item.json" };
        if (preview != null) files.Add("preview.png");

        foreach (var gamePath in spec.AssetPaths())
        {
            foreach (var copied in CopyCooked(repo, destDir, gamePath))
                files.Add(copied.Replace('\\', '/'));
        }

        File.WriteAllText(Path.Combine(destDir, "files.json"), JsonSerializer.Serialize(new { files }, Json), Utf8);
        return destDir;
    }

    public static bool CanPack(string repo, ItemSpec spec, out string why)
    {
        if (spec.Sample)
        {
            why = "sample";
            return false;
        }
        spec.Normalize();
        foreach (var path in MeshPaths(spec))
        {
            if (ForbiddenReason(path, spec.Row) != null) continue;
            if (!HasCooked(repo, path))
            {
                why = "no cooked mesh yet";
                return false;
            }
        }
        foreach (var path in TexturePaths(spec))
        {
            if (ForbiddenReason(path, spec.Row) != null) continue;
            if (!HasCooked(repo, path))
            {
                why = "no cooked albedo yet";
                return false;
            }
        }
        why = "";
        return true;
    }

    static IEnumerable<string> MeshPaths(ItemSpec spec)
    {
        foreach (var path in new[] { spec.UpperMale, spec.LowerMale })
        {
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }
        if (spec.Refs == null) yield break;
        foreach (var kv in spec.Refs)
        {
            if (kv.Key.Contains("Mesh", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kv.Value))
                yield return kv.Value;
        }
    }

    static IEnumerable<string> TexturePaths(ItemSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Albedo)) yield return spec.Albedo;
        if (spec.Refs == null) yield break;
        foreach (var kv in spec.Refs)
        {
            if (kv.Key.Contains("Mesh", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(kv.Value)) yield return kv.Value;
        }
    }

    public static bool HasCooked(string repo, string gamePath) =>
        FindCookedFiles(repo, gamePath).Any();

    public static bool IsCookedPackageFile(string file)
    {
        var ext = Path.GetExtension(file);
        if (ext.Equals(".uexp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ubulk", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".uptnl", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!ext.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            using var fs = File.OpenRead(file);
            if (fs.Length < 16 || fs.Length > 512 * 1024) return false;
            Span<byte> buf = stackalloc byte[16];
            if (fs.Read(buf) < 16) return false;
            for (var i = 8; i < 16; i++)
                if (buf[i] != 0) return false;
            return File.Exists(Path.ChangeExtension(file, ".uexp"));
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<string> FindCookedFiles(string repo, string gamePath, string? packageDir = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !gamePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
            yield break;
        var rel = gamePath["/Game/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var name = Path.GetFileName(rel);
        var parent = Path.GetDirectoryName(rel) ?? "";
        var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> dirs()
        {
            if (!string.IsNullOrWhiteSpace(packageDir))
                yield return Path.Combine(packageDir, "cooked", parent);
            foreach (var dir in CookedSearchDirs(repo, parent))
                yield return dir;
        }

        foreach (var srcDir in dirs())
        {
            if (!Directory.Exists(srcDir)) continue;
            foreach (var file in Directory.GetFiles(srcDir))
            {
                var baseName = Path.GetFileNameWithoutExtension(file);
                if (!NameMatches(baseName, name)) continue;
                if (baseName.EndsWith("_Skeleton", StringComparison.OrdinalIgnoreCase)) continue;
                if (baseName.Contains("PhysicsAsset", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsCookedPackageFile(file)) continue;
                var destName = Path.GetFileName(file);
                if (!chosen.ContainsKey(destName))
                    chosen[destName] = file;
            }
        }

        foreach (var file in chosen.Values)
            yield return file;
    }

    public static IEnumerable<string> CopyCooked(string repo, string destDir, string gamePath)
    {
        if (!gamePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
            yield break;
        var rel = gamePath["/Game/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(rel) ?? "";
        foreach (var file in FindCookedFiles(repo, gamePath))
        {
            var destRel = Path.Combine("cooked", parent, Path.GetFileName(file));
            var dest = Path.Combine(destDir, destRel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
            yield return destRel;
        }
    }

    public static IEnumerable<string> CookedSearchDirs(string repo, string parent)
    {
        yield return Path.Combine(repo, "ue", "RollerSkate", "Saved", "Cooked", "Windows", "RollerSkate", "Content", parent);
        yield return Path.Combine(repo, "ue", "RollerSkate", "Content", parent);
        var workshop = WorkshopCookedDirs(repo, parent);
        foreach (var dir in workshop)
            yield return dir;
    }

    static IEnumerable<string> WorkshopCookedDirs(string repo, string parent)
    {
        var root = AppPaths.WorkshopRoot(repo);
        if (!Directory.Exists(root)) yield break;
        foreach (var cooked in Directory.GetDirectories(root, "cooked", SearchOption.AllDirectories))
            yield return Path.Combine(cooked, parent);
    }

    static bool NameMatches(string baseName, string assetName)
    {
        if (baseName.Equals(assetName, StringComparison.OrdinalIgnoreCase)) return true;
        if (assetName.Contains("tshirt-baggy", StringComparison.OrdinalIgnoreCase) &&
            (baseName.Equals("M_tshirt-baggy", StringComparison.OrdinalIgnoreCase) ||
             baseName.StartsWith("T_tshirt-baggy-", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    static string? FindPreview(string repo, ItemSpec spec)
    {
        var hits = new[]
        {
            Path.Combine(repo, "art", "previews", "T_" + spec.Row + ".png"),
            Path.Combine(repo, "art", "previews", spec.Row + ".png"),
            Path.Combine(repo, "art", "textures", spec.Row, "T_" + spec.Row + "-albedo.png"),
        };
        foreach (var p in hits)
            if (File.Exists(p)) return p;
        if (!string.IsNullOrWhiteSpace(spec.Albedo))
        {
            var tex = Path.Combine(repo, "art", "textures");
            if (Directory.Exists(tex))
            {
                var name = spec.Albedo.Split('/').Last();
                foreach (var file in Directory.GetFiles(tex, "*.png", SearchOption.AllDirectories))
                {
                    if (Path.GetFileNameWithoutExtension(file).Equals(name, StringComparison.OrdinalIgnoreCase))
                        return file;
                    if (Path.GetFileName(Path.GetDirectoryName(file))!.Equals(spec.Row, StringComparison.OrdinalIgnoreCase)
                        && file.Contains("albedo", StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
        }
        return null;
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

    public static CatalogFile BuildCatalog(string galleryRoot)
    {
        var catalog = new CatalogFile();
        var mods = Path.Combine(galleryRoot, "mods");
        if (!Directory.Exists(mods)) return catalog;
        foreach (var dir in Directory.GetDirectories(mods))
        {
            var infoPath = Path.Combine(dir, "mod.json");
            if (!File.Exists(infoPath)) continue;
            var info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(infoPath), Json);
            if (info == null || string.IsNullOrWhiteSpace(info.Id)) continue;
            var rel = "mods/" + info.Id;
            catalog.Mods.Add(new CatalogEntry
            {
                Id = info.Id,
                Title = info.Title,
                Author = info.Author,
                Slot = info.Slot,
                Row = info.Row,
                Version = info.Version,
                Path = rel,
                Preview = File.Exists(Path.Combine(dir, "preview.png")) ? rel + "/preview.png" : null,
            });
        }
        catalog.Mods.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return catalog;
    }
}
