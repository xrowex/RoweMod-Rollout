using System.Text.Json.Nodes;

namespace RoweMod.App;

static class WorkshopChecks
{
    public static void Run(string repo, Action<string> ok)
    {
        var root = Path.Combine(repo, "dumps", "workshop-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "art", "rig"));
        File.WriteAllText(Path.Combine(root, "art", "rig", "main-rig.blend"), "test rig");
        var mesh = MeshWork.CreateMesh(root, "Test garment", "Tops");
        var art = File.ReadAllBytes(mesh.Blend!);
        MeshWork.SaveTarget(root, MeshWork.FromPiece(root, mesh));
        LocalModTrash.Delete(root, mesh);
        Require(!File.Exists(mesh.ItemJson) && File.ReadAllBytes(mesh.Blend!).SequenceEqual(art), "Delete must keep Blender artwork");
        Require(!File.Exists(MeshWork.TargetPath(root)), "Delete must clear its active cook target");
        Require(ClothingLibrary.Scan(root).All(p => p.Id != mesh.Id), "Deleted mesh still listed");
        Require(DtPatcher.Program.IsLocallyDeleted(root, mesh.Id), "Deleted row not suppressed");
        var cached = Path.Combine(root, "dumps", "workshop", mesh.Id, "item.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        File.Copy(Path.Combine(LocalModTrash.Root(root), mesh.Id, "item.json"), cached);
        Require(DtPatcher.Program.PatchAllItems(root, new[] { cached }, _ => { }) == 0,
            "A subscribed copy must not resurrect a deleted local mod");
        ExpectFailure(() => MeshWork.CreateMesh(root, "Test garment", "Tops"));
        var deleted = LocalModTrash.List(root).Single();
        File.WriteAllText(mesh.ItemJson!, "existing work");
        ExpectFailure(() => LocalModTrash.Restore(root, deleted));
        Require(File.ReadAllText(mesh.ItemJson!) == "existing work", "Restore overwrote a conflict");
        File.Delete(mesh.ItemJson!);
        LocalModTrash.Restore(root, deleted);
        Require(File.Exists(mesh.ItemJson) && !DtPatcher.Program.IsLocallyDeleted(root, mesh.Id), "Restore failed");
        Require(File.Exists(MeshWork.TargetPath(root)), "Cook target was not restored");
        ok("delete / restore keeps source art, blocks subscribed duplicates, and protects existing files");

        var otherBlend = Path.Combine(root, "art", "elsewhere.blend");
        File.WriteAllText(otherBlend, "saved elsewhere");
        MeshWork.UseMeshSource(mesh, otherBlend);
        var linked = ClothingLibrary.Scan(root).Single(p => p.Id == mesh.Id);
        Require(linked.Blend == otherBlend && MeshWork.FromPiece(root, linked).Blend == otherBlend,
            "Chosen Blender source not used for export");
        Require(linked.Model == null && linked.CanExport, "First export must work without a previous GLB");
        ok("Choose mesh persists; a new saved project can export before a GLB exists");
        var fbx = Path.Combine(root, "art", "test-garment-mod-male.fbx");
        File.WriteAllText(fbx, "external FBX source");
        MeshWork.UseMeshSource(linked, fbx);
        linked = ClothingLibrary.Scan(root).Single(p => p.Id == mesh.Id);
        Require(linked.Blend == fbx && linked.CanExport, "FBX source not persisted");
        Require(MeshWork.ExportEnv(MeshWork.FromPiece(root, linked))["ROWE_SOURCE"] == fbx,
            "FBX source not sent to exporter");
        File.Move(fbx, fbx + ".missing");
        var missing = ClothingLibrary.Scan(root).Single(p => p.Id == mesh.Id);
        Require(missing.Blend == fbx && !missing.CanExport, "Missing chosen source silently fell back to a different project");
        File.Move(fbx + ".missing", fbx);
        ExpectFailure(() => MeshWork.UseMeshSource(linked, linked.ItemJson!));
        var skate = new ClothingPiece { Id = "test-boot", Title = "Boot", Slot = "Boots", Kind = ClothingKind.Mesh, Blend = fbx, Model = fbx };
        Require(Path.GetExtension(MeshWork.FromPiece(root, skate).Glb) == ".glb", "Skate export can overwrite FBX source");
        ok("FBX source selection survives reload; missing source and unsupported formats are rejected");
        var package = RoweMod.Core.ModPackage.Build(root, linked.ItemJson!, "test", Path.Combine(root, "share-test"));
        var shared = JsonNode.Parse(File.ReadAllText(Path.Combine(package, "item.json")))!.AsObject();
        Require(!shared.ContainsKey("sourceBlend") && !shared.ContainsKey("sourceMesh"),
            "Share package leaked the local Blender path");
        ok("gallery package excludes the author's local Blender source path");
        var tools = new DetectedTools { Repo = root, Blender = "test-blender" };
        using (var workshop = new ExportWorkshopForm(tools))
        {
            workshop.StartPosition = FormStartPosition.Manual;
            workshop.Location = new Point(-2000, -2000);
            workshop.Show();
            workshop.ShowTab(WorkshopTab.Mesh);
            var buttons = Descendants(workshop).OfType<Button>().ToArray();
            Require(buttons.Any(b => b.Text == "Export + Play" && b.Enabled), "First-export button is disabled");
            Require(buttons.Any(b => b.Text == "Delete…") && buttons.Any(b => b.Text == "Choose mesh…"), "Mesh management actions missing");
            LocalModTrash.Delete(root, linked);
            buttons.Single(b => b.Text.StartsWith("Deleted mods")).PerformClick();
            var restore = Descendants(workshop).OfType<Button>().Single(b => b.Text == "Restore");
            restore.PerformClick();
            Require(File.Exists(linked.ItemJson), "Restore button did not restore the item");
        }
        ok("new mesh has Export + Play / Choose mesh / Delete; Deleted mods Restore button works");

        var png = Path.Combine(root, "art", "keep.png");
        File.WriteAllText(png, "original paint");
        var json = Path.Combine(root, "items", "upper", "paint-mod.json");
        File.WriteAllText(json, "{\"row\":\"paint-mod\",\"cloneRow\":\"tshirt-white\",\"table\":\"DT-upper\"}");
        var paint = new ClothingPiece { Id = "paint-mod", Title = "Paint", Slot = "Tops", Kind = ClothingKind.Texture, ItemJson = json, PaintFile = png };
        LocalModTrash.Delete(root, paint);
        Require(File.ReadAllText(png) == "original paint", "Delete removed a painted texture");
        LocalModTrash.Restore(root, LocalModTrash.List(root).Single());
        var outside = new ClothingPiece { Id = "outside-mod", Title = "Outside", Slot = "Tops", Kind = ClothingKind.Texture, ItemJson = png };
        ExpectFailure(() => LocalModTrash.Delete(root, outside));
        var stock = new ClothingPiece { Id = "game:stock", Title = "Stock", Slot = "Tops", Kind = ClothingKind.Mesh, ItemJson = json };
        ExpectFailure(() => LocalModTrash.Delete(root, stock));
        ExpectFailure(() => LocalModTrash.Restore(root, new DeletedMod("paint-mod", "Paint", "../escape.json")));
        ok("paint deletion keeps textures; stock items and paths outside items are protected");
    }

    static void Require(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
    }

    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected rejection did not happen");
    }
}
