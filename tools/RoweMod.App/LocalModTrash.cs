using System.Text.Json;

namespace RoweMod.App;

sealed record DeletedMod(string Row, string Title, string ItemPath);

static class LocalModTrash
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string Root(string repo) => Path.Combine(repo, "dumps", "deleted-mods");

    static string Inside(string root, string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This file is outside the local mod folder.");
        return full;
    }

    static string Folder(string repo, string row)
    {
        if (string.IsNullOrWhiteSpace(row) || row != Path.GetFileName(row) || row.Contains('/') || row.Contains('\\'))
            throw new InvalidOperationException("Invalid mod name.");
        return Inside(Root(repo), Path.Combine(Root(repo), row));
    }

    public static IReadOnlyList<DeletedMod> List(string repo)
    {
        if (!Directory.Exists(Root(repo))) return Array.Empty<DeletedMod>();
        var result = new List<DeletedMod>();
        foreach (var folder in Directory.GetDirectories(Root(repo)))
        {
            if (!File.Exists(Path.Combine(folder, "item.json"))) continue;
            var info = Path.Combine(folder, "deleted.json");
            if (!File.Exists(info)) continue;
            var entry = JsonSerializer.Deserialize<DeletedMod>(File.ReadAllText(info));
            if (entry != null) result.Add(entry);
        }
        return result.OrderBy(x => x.Title).ToArray();
    }

    public static void Delete(string repo, ClothingPiece piece)
    {
        if (piece.FromGame || piece.ItemJson == null || piece.Sample)
            throw new InvalidOperationException("Only your own local mods can be deleted here.");
        var source = Inside(Path.Combine(repo, "items"), piece.ItemJson);
        var folder = Folder(repo, piece.Id);
        var archived = Path.Combine(folder, "item.json");
        if (File.Exists(archived)) throw new InvalidOperationException("This mod is already in Deleted mods.");
        var item = RoweMod.Core.ModPackage.ReadItem(source);
        if (item.Row != piece.Id) throw new InvalidOperationException("Mod changed. Refresh and try again.");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "deleted.json"),
            JsonSerializer.Serialize(new DeletedMod(piece.Id, piece.Title, Path.GetRelativePath(repo, source)), Json));
        // The archived definition also suppresses any subscribed copy of this row.
        // Source art and cooked assets stay untouched so restoration is lossless.
        File.Move(source, archived);
        var targetPath = MeshWork.TargetPath(repo);
        if (File.Exists(targetPath))
        {
            var target = MeshWork.LoadOrDefault(repo);
            if (target.ItemJson != null && Path.GetFullPath(target.ItemJson).Equals(source, StringComparison.OrdinalIgnoreCase))
                File.Move(targetPath, Path.Combine(folder, "cook-target.json"), overwrite: true);
        }
    }

    public static void Restore(string repo, DeletedMod entry)
    {
        var source = Path.Combine(Folder(repo, entry.Row), "item.json");
        var dest = Inside(Path.Combine(repo, "items"), Path.Combine(repo, entry.ItemPath));
        if (File.Exists(dest)) throw new InvalidOperationException("A mod already uses this name. No files were replaced.");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(source, dest);
        var target = Path.Combine(Folder(repo, entry.Row), "cook-target.json");
        if (File.Exists(target) && !File.Exists(MeshWork.TargetPath(repo)))
            File.Copy(target, MeshWork.TargetPath(repo));
    }
}
