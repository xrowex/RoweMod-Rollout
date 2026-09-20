using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

internal static class Program
{
    private static int Main(string[] args)
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate";
        var mappings = Path.Combine(repo, "dumps", "mappings.usmap");
        var outDir = Path.Combine(repo, "dumps", "bind-pose");
        Directory.CreateDirectory(outDir);

        var game = LoadProvider(gameDir, mappings);
        var hoodie = DumpMesh(game, "RollerSkate/Content/MainFolder/Character/upper/hoodie/hoodie-male.uasset", "hoodie-male");
        var body = DumpMesh(game, "RollerSkate/Content/MainFolder/Character/body/male/male-01/male-body-01.uasset", "male-body-01");
        var skel = DumpSkeleton(game, "RollerSkate/Content/MainFolder/Character/body/main-rig/main-rig.uasset", "main-rig");

        var shirtCandidates = new[]
        {
            Path.Combine(repo, "ue", "RollerSkate", "Saved", "Cooked", "Windows", "RollerSkate"),
            Path.Combine(repo, "dumps", "mod-staging", "RollerSkate"),
        };
        BoneDump? shirt = null;
        foreach (var root in shirtCandidates)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var local = LoadProvider(root, mappings);
                shirt = DumpMesh(local, "RollerSkate/Content/MainFolder/Character/upper/tshirt-baggy/tshirt-baggy-male.uasset", "tshirt-baggy-male");
                if (shirt.Bones.Count > 0) break;
            }
            catch (Exception ex)
            {
                Console.WriteLine("shirt load fail " + root + " " + ex.Message);
            }
        }

        File.WriteAllText(Path.Combine(outDir, "hoodie-male.bones.json"), JsonConvert.SerializeObject(hoodie, Formatting.Indented));
        File.WriteAllText(Path.Combine(outDir, "male-body-01.bones.json"), JsonConvert.SerializeObject(body, Formatting.Indented));
        File.WriteAllText(Path.Combine(outDir, "main-rig.bones.json"), JsonConvert.SerializeObject(skel, Formatting.Indented));
        if (shirt != null)
            File.WriteAllText(Path.Combine(outDir, "tshirt-baggy-male.bones.json"), JsonConvert.SerializeObject(shirt, Formatting.Indented));

        Compare("hoodie vs shirt", hoodie, shirt);
        Compare("hoodie vs body", hoodie, body);
        Compare("hoodie vs main-rig", hoodie, skel);
        return 0;
    }

    private static DefaultFileProvider LoadProvider(string root, string mappings)
    {
        var provider = new DefaultFileProvider(root, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_4));
        if (File.Exists(mappings))
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings);
        provider.Initialize();
        provider.SubmitKey(new FGuid(), new FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
        provider.PostMount();
        return provider;
    }

    private static BoneDump DumpMesh(DefaultFileProvider provider, string pkg, string label)
    {
        Console.WriteLine("-- mesh " + pkg);
        var dump = new BoneDump { Label = label, Source = pkg };
        try
        {
            foreach (var exp in provider.LoadAllObjects(pkg))
            {
                if (exp is USkeletalMesh mesh)
                {
                    Fill(dump, mesh.ReferenceSkeleton, "USkeletalMesh.ReferenceSkeleton");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            dump.Error = ex.Message;
            Console.WriteLine("   FAIL " + ex.Message);
        }
        Console.WriteLine("   bones " + dump.Bones.Count);
        return dump;
    }

    private static BoneDump DumpSkeleton(DefaultFileProvider provider, string pkg, string label)
    {
        Console.WriteLine("-- skel " + pkg);
        var dump = new BoneDump { Label = label, Source = pkg };
        try
        {
            foreach (var exp in provider.LoadAllObjects(pkg))
            {
                if (exp is USkeleton skeleton)
                {
                    Fill(dump, skeleton.ReferenceSkeleton, "USkeleton.ReferenceSkeleton");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            dump.Error = ex.Message;
            Console.WriteLine("   FAIL " + ex.Message);
        }
        Console.WriteLine("   bones " + dump.Bones.Count);
        return dump;
    }

    private static void Fill(BoneDump dump, FReferenceSkeleton? reference, string from)
    {
        if (reference == null)
        {
            dump.Error = "null " + from;
            return;
        }
        dump.From = from;
        var infos = reference.FinalRefBoneInfo;
        var poses = reference.FinalRefBonePose;
        if (infos == null || poses == null) return;
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            var pose = i < poses.Length ? poses[i] : default;
            dump.Bones.Add(new BoneRow
            {
                Index = i,
                Name = info.Name.ToString(),
                Parent = info.ParentIndex,
                Tx = pose.Translation.X,
                Ty = pose.Translation.Y,
                Tz = pose.Translation.Z,
                Qx = pose.Rotation.X,
                Qy = pose.Rotation.Y,
                Qz = pose.Rotation.Z,
                Qw = pose.Rotation.W,
                Sx = pose.Scale3D.X,
                Sy = pose.Scale3D.Y,
                Sz = pose.Scale3D.Z,
            });
        }
    }

    private static void Compare(string title, BoneDump? a, BoneDump? b)
    {
        Console.WriteLine("==== " + title + " ====");
        if (a == null || b == null || a.Bones.Count == 0 || b.Bones.Count == 0)
        {
            Console.WriteLine(" missing dump");
            return;
        }
        var mapB = b.Bones.ToDictionary(x => x.Name, x => x);
        var mismatches = 0;
        foreach (var bone in a.Bones)
        {
            if (!mapB.TryGetValue(bone.Name, out var other))
            {
                Console.WriteLine(" MISSING " + bone.Name);
                mismatches++;
                continue;
            }
            var dt = Dist(bone.Tx, bone.Ty, bone.Tz, other.Tx, other.Ty, other.Tz);
            var dq = QuatDot(bone, other);
            var extra = "";
            if (dt > 0.5f) extra += $" dT={dt:0.000}";
            if (dq < 0.98f) extra += $" dQ={dq:0.000}";
            if (extra.Length > 0)
            {
                Console.WriteLine(" DIFF " + bone.Name + extra
                    + $" A=({bone.Tx:0.00},{bone.Ty:0.00},{bone.Tz:0.00}) q({bone.Qx:0.00},{bone.Qy:0.00},{bone.Qz:0.00},{bone.Qw:0.00})"
                    + $" B=({other.Tx:0.00},{other.Ty:0.00},{other.Tz:0.00}) q({other.Qx:0.00},{other.Qy:0.00},{other.Qz:0.00},{other.Qw:0.00})");
                mismatches++;
            }
        }
        Console.WriteLine(" mismatches " + mismatches + " / " + a.Bones.Count);
    }

    private static float Dist(float ax, float ay, float az, float bx, float by, float bz)
    {
        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float QuatDot(BoneRow a, BoneRow b)
    {
        var d = MathF.Abs(a.Qx * b.Qx + a.Qy * b.Qy + a.Qz * b.Qz + a.Qw * b.Qw);
        return MathF.Min(1f, d);
    }
}

internal sealed class BoneDump
{
    public string Label { get; set; } = "";
    public string Source { get; set; } = "";
    public string? From { get; set; }
    public string? Error { get; set; }
    public List<BoneRow> Bones { get; set; } = [];
}

internal sealed class BoneRow
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Parent { get; set; }
    public float Tx { get; set; }
    public float Ty { get; set; }
    public float Tz { get; set; }
    public float Qx { get; set; }
    public float Qy { get; set; }
    public float Qz { get; set; }
    public float Qw { get; set; }
    public float Sx { get; set; }
    public float Sy { get; set; }
    public float Sz { get; set; }
}
