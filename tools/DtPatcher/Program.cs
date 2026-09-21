using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace DtPatcher;

public static class Program
{
    public static int Main(string[] args) => Run(args);

    public static string DiscoverRepo(string? start = null)
    {
        var seeds = new List<string>();
        if (!string.IsNullOrWhiteSpace(start)) seeds.Add(start);
        seeds.Add(AppContext.BaseDirectory);
        seeds.Add(Environment.CurrentDirectory);
        foreach (var seed in seeds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DirectoryInfo? dir = new(Path.GetFullPath(seed));
            for (var i = 0; i < 12 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "items")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "tools")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new DirectoryNotFoundException("RoweMod repo root not found from " + (start ?? AppContext.BaseDirectory));
    }

    public static int PatchAllItems(string? repo = null, IEnumerable<string>? extraItems = null, Action<string>? log = null)
    {
        var args = new List<string> { "--all-items" };
        if (extraItems != null)
        {
            foreach (var item in extraItems)
            {
                args.Add("--item");
                args.Add(item);
            }
        }
        else if (!string.IsNullOrWhiteSpace(repo))
        {
            foreach (var item in DiscoverWorkshopItems(repo))
            {
                args.Add("--item");
                args.Add(item);
            }
        }
        return Run(args.ToArray(), repo, log);
    }

    static Action<string> Out = Console.WriteLine;

    public static IEnumerable<string> DiscoverWorkshopItems(string repo)
    {
        var root = Path.Combine(repo, "dumps", "workshop");
        if (!Directory.Exists(root)) yield break;
        foreach (var item in Directory.GetFiles(root, "item.json", SearchOption.AllDirectories))
            yield return item;
    }

    public static int Run(string[] args, string? repoOverride = null, Action<string>? log = null)
    {
        var prev = Out;
        Out = log ?? Console.WriteLine;
        try
        {
            return RunCore(args, repoOverride);
        }
        finally
        {
            Out = prev;
        }
    }

    static int RunCore(string[] args, string? repoOverride)
    {
        var repo = string.IsNullOrWhiteSpace(repoOverride)
            ? DiscoverRepo()
            : Path.GetFullPath(repoOverride);
        var itemPaths = new List<string>();
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--item" && i + 1 < args.Length)
            {
                itemPaths.Add(args[++i]);
                continue;
            }
            if (args[i] == "--all-items")
            {
                var itemsDir = Path.Combine(repo, "items");
                if (Directory.Exists(itemsDir))
                    itemPaths.AddRange(Directory.GetFiles(itemsDir, "*.json", SearchOption.AllDirectories));
                continue;
            }
            if (args[i].StartsWith("--"))
                continue;
            positional.Add(args[i]);
        }

        if (itemPaths.Count == 0 && positional.Count == 0)
            itemPaths.AddRange(Directory.GetFiles(Path.Combine(repo, "items"), "*.json", SearchOption.AllDirectories));

        var includeSamples = args.Any(a => a == "--include-samples")
            || string.Equals(Environment.GetEnvironmentVariable("ROWE_PACK_SAMPLES"), "1", StringComparison.OrdinalIgnoreCase);

        Usmap? mappings = null;
        var usmap = Path.Combine(repo, "dumps", "mappings.usmap");
        if (File.Exists(usmap))
            mappings = new Usmap(usmap);

        if (args.Any(a => a == "--list"))
        {
            var src = positional.Count > 0
                ? positional[0]
                : FindTable(repo, "DT-upper");
            if (src == null || !File.Exists(src))
            {
                Out("missing table to list");
                return 1;
            }
            var listAsset = Load(src, mappings);
            return ListRows(listAsset, dumpProps: args.Any(a => a == "--props"));
        }

        var specs = new List<(string Path, ItemSpec Spec)>();
        var seenRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemPath in itemPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(itemPath);
            if (!File.Exists(full))
            {
                Out("missing item " + full);
                return 1;
            }
            var spec = Newtonsoft.Json.JsonConvert.DeserializeObject<ItemSpec>(File.ReadAllText(full)) ?? new ItemSpec();
            spec.Normalize();
            if (string.IsNullOrWhiteSpace(spec.Row) || string.IsNullOrWhiteSpace(spec.CloneRow))
            {
                Out("item needs row + cloneRow: " + full);
                return 1;
            }
            if (spec.Sample && !includeSamples)
            {
                Out("SKIP sample " + spec.Row);
                continue;
            }
            if (!seenRows.Add(spec.Row))
            {
                Out("SKIP duplicate " + spec.Row + " " + full);
                continue;
            }
            if (!ReadyToPatch(repo, spec))
            {
                Out("SKIP " + spec.Row + " (no cooked mesh yet)");
                continue;
            }
            specs.Add((full, spec));
        }

        var patchedDir = Path.Combine(repo, "dumps", "patched");
        Directory.CreateDirectory(patchedDir);
        foreach (var leftover in Directory.GetFiles(patchedDir, "*.*"))
        {
            File.Delete(leftover);
            Out("CLEARED " + leftover);
        }

        if (specs.Count == 0)
        {
            Out("no live items (kit samples are templates and are not packed)");
            return 0;
        }

        foreach (var group in specs.GroupBy(s => s.Spec.Table, StringComparer.OrdinalIgnoreCase))
        {
            var tableName = group.Key;
            var src = FindTable(repo, tableName);
            if (src == null)
            {
                Out("missing source table " + tableName + " — run tools/extract_tables.ps1");
                return 2;
            }
            Out("TABLE " + tableName + " SRC " + src);
            var asset = Load(src, mappings);
            var table = asset.Exports.OfType<DataTableExport>().FirstOrDefault();
            if (table == null)
            {
                Out("No DataTableExport in " + src);
                return 2;
            }
            foreach (var (path, spec) in group)
            {
                Out("ITEM " + path);
                var applied = ApplyItem(asset, table, spec);
                if (applied != 0)
                    return applied;
            }
            var dst = Path.Combine(patchedDir, tableName + ".uasset");
            asset.Write(dst);
            Out("WROTE " + dst + " rows=" + table.Table.Data.Count);
        }
        return 0;
    }

    private static UAsset Load(string src, Usmap? mappings) =>
        mappings == null
            ? new UAsset(src, EngineVersion.VER_UE5_4)
            : new UAsset(src, EngineVersion.VER_UE5_4, mappings);

    static bool ReadyToPatch(string repo, ItemSpec spec)
    {
        foreach (var path in MeshGamePaths(spec))
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
                continue;
            var rel = path["/Game/".Length..].Replace('/', Path.DirectorySeparatorChar) + ".uasset";
            var cooked = Path.Combine(repo, "ue", "RollerSkate", "Saved", "Cooked", "Windows", "RollerSkate", "Content", rel);
            if (File.Exists(cooked) && File.Exists(Path.ChangeExtension(cooked, ".uexp")))
                continue;
            var found = false;
            var workshop = Path.Combine(repo, "dumps", "workshop");
            if (Directory.Exists(workshop))
            {
                var name = Path.GetFileName(rel);
                foreach (var hit in Directory.GetFiles(workshop, name, SearchOption.AllDirectories))
                {
                    if (new FileInfo(hit).Length > 512 * 1024) continue;
                    if (File.Exists(Path.ChangeExtension(hit, ".uexp")))
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found) return false;
        }
        return true;
    }

    static IEnumerable<string> MeshGamePaths(ItemSpec spec)
    {
        foreach (var path in new[] { spec.UpperMale, spec.LowerMale })
        {
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }
        spec.Normalize();
        foreach (var kv in spec.Refs)
        {
            if (kv.Key.Contains("Mesh", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kv.Value))
                yield return kv.Value;
        }
    }

    private static string? FindTable(string repo, string tableName)
    {
        var candidates = new[]
        {
            Path.Combine(repo, "dumps", "legacy", "RollerSkate", "Content", "MainFolder", "UI", "customization", "data", tableName + ".uasset"),
            Path.Combine(repo, "dumps", "dt-upper", "RollerSkate", "Content", "MainFolder", "UI", "customization", "data", tableName + ".uasset"),
            Path.Combine(repo, "dumps", tableName.ToLowerInvariant(), "RollerSkate", "Content", "MainFolder", "UI", "customization", "data", tableName + ".uasset"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static int ApplyItem(UAsset asset, DataTableExport table, ItemSpec spec)
    {
        var cloneFrom = table.Table.Data.FirstOrDefault(r =>
            r.Name.ToString().Equals(spec.CloneRow, StringComparison.OrdinalIgnoreCase)
            || r.Name.ToString().Contains(spec.CloneRow, StringComparison.OrdinalIgnoreCase));
        if (cloneFrom == null)
        {
            Out("SKIP " + spec.Row + " (no clone row " + spec.CloneRow + ")");
            return 0;
        }

        var clone = (StructPropertyData)cloneFrom.Clone();
        clone.Name = new FName(asset, spec.Row);

        var objectImports = new Dictionary<string, FPackageIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, path) in spec.Refs)
        {
            var objectName = path.Split('/')[^1];
            var className = GuessClass(key);
            objectImports[key] = EnsureImport(asset, path, objectName, className, "/Script/Engine");
        }

        foreach (var prop in clone.Value)
        {
            var name = prop.Name.ToString();
            var prefix = name.Split('_')[0];

            if (prop is ObjectPropertyData obj)
            {
                var match = objectImports.Keys.FirstOrDefault(k =>
                    name.StartsWith(k, StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals(k, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    obj.Value = objectImports[match];
            }
            if (prop is TextPropertyData text && prefix.Equals("LocalizedName", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(spec.LocalizedName))
                text.CultureInvariantString = new FString(spec.LocalizedName);
            if (prop is IntPropertyData price && prefix.Equals("Price", StringComparison.OrdinalIgnoreCase) && spec.Price.HasValue)
                price.Value = spec.Price.Value;
            if (prop is BoolPropertyData flag && spec.Bools != null)
            {
                var bkey = spec.Bools.Keys.FirstOrDefault(k => prefix.StartsWith(k, StringComparison.OrdinalIgnoreCase) || prefix.Equals(k, StringComparison.OrdinalIgnoreCase));
                if (bkey != null)
                    flag.Value = spec.Bools[bkey];
            }
            if (prop is StructPropertyData colour && spec.Colour is { Length: >= 3 } && prefix.Equals("Colour", StringComparison.OrdinalIgnoreCase))
                SetColour(colour, spec.Colour[0], spec.Colour[1], spec.Colour[2], spec.Colour.Length > 3 ? spec.Colour[3] : 1f);
            if (prop is StructPropertyData extraColour && spec.Colours != null)
            {
                var ckey = spec.Colours.Keys.FirstOrDefault(k => prefix.Equals(k, StringComparison.OrdinalIgnoreCase));
                if (ckey != null && spec.Colours[ckey] is { Length: >= 3 } rgba)
                    SetColour(extraColour, rgba[0], rgba[1], rgba[2], rgba.Length > 3 ? rgba[3] : 1f);
            }
            if (prop is StructPropertyData vec && spec.Vector2 != null)
            {
                var vkey = spec.Vector2.Keys.FirstOrDefault(k => prefix.Equals(k, StringComparison.OrdinalIgnoreCase) || name.StartsWith(k, StringComparison.OrdinalIgnoreCase));
                if (vkey != null && spec.Vector2[vkey] is { Length: >= 2 } xy)
                    SetVector2(vec, xy[0], xy[1]);
            }
        }

        var existing = table.Table.Data.FirstOrDefault(r => r.Name.ToString() == spec.Row);
        if (existing != null)
            table.Table.Data.Remove(existing);
        var insertAt = 0;
        while (insertAt < table.Table.Data.Count &&
               table.Table.Data[insertAt].Name.ToString().EndsWith("-mod", StringComparison.OrdinalIgnoreCase))
            insertAt++;
        table.Table.Data.Insert(insertAt, clone);
        Out("ROW " + spec.Row + " table=" + spec.Table + " index=" + insertAt);
        return 0;
    }

    private static string GuessClass(string key)
    {
        if (key.Equals("SkatesMesh", StringComparison.OrdinalIgnoreCase))
            return "SkeletalMesh";
        if (key.Contains("Mesh", StringComparison.OrdinalIgnoreCase) &&
            (key.Contains("Hat", StringComparison.OrdinalIgnoreCase) ||
             key.Contains("Glasses", StringComparison.OrdinalIgnoreCase) ||
             key.Contains("Beard", StringComparison.OrdinalIgnoreCase) ||
             key.Contains("Blade", StringComparison.OrdinalIgnoreCase)))
            return "StaticMesh";
        if (key.Contains("Mesh", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Male", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Female", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("UpperMale", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("LowerMale", StringComparison.OrdinalIgnoreCase))
            return "SkeletalMesh";
        return "Texture2D";
    }

    private static FPackageIndex EnsureImport(UAsset asset, string packagePath, string objectName, string className, string classPackage)
    {
        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
                return FPackageIndex.FromRawIndex(-(i + 1));
        }

        asset.Imports.Add(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        var pkgIndex = FPackageIndex.FromRawIndex(-asset.Imports.Count);
        asset.Imports.Add(new Import(classPackage, className, pkgIndex, objectName, false, asset));
        return FPackageIndex.FromRawIndex(-asset.Imports.Count);
    }

    private static void SetColour(StructPropertyData colour, float r, float g, float b, float a)
    {
        foreach (var inner in colour.Value)
            SetLinearColour(inner, r, g, b, a);
    }

    private static void SetVector2(StructPropertyData vec, float x, float y)
    {
        foreach (var inner in vec.Value)
        {
            var vp = inner.GetType().GetProperty("Value");
            if (vp == null) continue;
            var val = vp.GetValue(inner);
            if (val == null) continue;
            var vt = val.GetType();
            foreach (var (n, f) in new (string, float)[] { ("X", x), ("Y", y) })
            {
                var p = vt.GetProperty(n);
                if (p != null && p.CanWrite)
                    p.SetValue(val, Convert.ChangeType(f, p.PropertyType));
                vt.GetField(n)?.SetValue(val, Convert.ChangeType(f, vt.GetField(n)!.FieldType));
            }
            if (vt.IsValueType)
                vp.SetValue(inner, val);
        }
    }

    private static void SetLinearColour(object inner, float r, float g, float b, float a)
    {
        var vp = inner.GetType().GetProperty("Value");
        if (vp == null) return;
        var val = vp.GetValue(inner);
        if (val == null) return;
        var vt = val.GetType();
        foreach (var (n, f) in new (string, float)[] { ("R", r), ("G", g), ("B", b), ("A", a) })
        {
            var p = vt.GetProperty(n);
            if (p != null && p.CanWrite)
                p.SetValue(val, Convert.ChangeType(f, p.PropertyType));
            var field = vt.GetField(n);
            field?.SetValue(val, Convert.ChangeType(f, field.FieldType));
        }
        if (vt.IsValueType)
            vp.SetValue(inner, val);
    }

    private static int ListRows(UAsset asset, bool dumpProps = false)
    {
        var table = asset.Exports.OfType<DataTableExport>().FirstOrDefault();
        if (table == null)
        {
            Console.WriteLine("No DataTableExport");
            return 2;
        }

        Console.WriteLine("Rows " + table.Table.Data.Count);
        var dumped = false;
        foreach (var row in table.Table.Data)
        {
            string price = "-", name = "-", extra = "";
            foreach (var prop in row.Value)
            {
                var pname = prop.Name.ToString();
                if (prop is IntPropertyData ip && pname.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
                    price = ip.Value.ToString();
                if (prop is TextPropertyData tp && pname.StartsWith("LocalizedName", StringComparison.OrdinalIgnoreCase))
                    name = tp.CultureInvariantString?.ToString() ?? "";
            }
            Console.WriteLine($"{row.Name}\tprice={price}\tname={name}{extra}");
            if (dumpProps && !dumped && !row.Name.ToString().EndsWith("-mod", StringComparison.OrdinalIgnoreCase))
            {
                dumped = true;
                foreach (var prop in row.Value)
                {
                    var hint = prop is ObjectPropertyData obj ? " -> " + obj.Value : "";
                    Console.WriteLine("  PROP " + prop.GetType().Name + " " + prop.Name + hint);
                }
            }
        }
        return 0;
    }
}

public sealed class ItemSpec
{
    public bool Sample { get; set; }
    public string Table { get; set; } = "DT-upper";
    public string CloneRow { get; set; } = "";
    public string Row { get; set; } = "";
    public string LocalizedName { get; set; } = "";
    public int? Price { get; set; }
    public float[]? Colour { get; set; }
    public Dictionary<string, float[]>? Colours { get; set; }
    public Dictionary<string, float[]>? Vector2 { get; set; }
    public Dictionary<string, bool>? Bools { get; set; }
    public Dictionary<string, string>? Refs { get; set; }
    public string? UpperMale { get; set; }
    public string? LowerMale { get; set; }
    public string? Albedo { get; set; }
    public string? Normal { get; set; }
    public string? Roughness { get; set; }
    public string? PreviewImage { get; set; }
    public string? EyeAlbedo { get; set; }

    public void Normalize()
    {
        Refs ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Refs.ContainsKey(key))
                Refs[key] = path;
        }
        Add("UpperMale", UpperMale);
        Add("LowerMale", LowerMale);
        Add("Albedo", Albedo);
        Add("Normal", Normal);
        Add("Roughness", Roughness);
        Add("PreviewImage", PreviewImage);
        Add("EyeAlbedo", EyeAlbedo);
        if (string.IsNullOrWhiteSpace(Table))
            Table = "DT-upper";
    }

    public IEnumerable<string> AssetPaths()
    {
        Normalize();
        foreach (var value in Refs.Values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }
}
