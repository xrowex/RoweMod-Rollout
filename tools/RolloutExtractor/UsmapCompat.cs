using System.IO.Compression;

namespace RolloutExtractor;

/// <summary>
/// CUE4Parse 1.2.2 stops at usmap v3. UE4SS DumpUSMAP writes v4
/// (ExplicitEnumValues: int64 value then name index). Rewrite as v3.
/// Header after version is int32 bHasVersioning, matching FArchive.ReadBoolean.
/// </summary>
static class UsmapCompat
{
    const ushort Magic = 0x30C4;
    const byte LargeEnums = 3;
    const byte ExplicitEnumValues = 4;

    public static string? EnsureReadable(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16) return path;
        if (BitConverter.ToUInt16(bytes, 0) != Magic) return path;
        var version = bytes[2];
        if (version <= LargeEnums) return path;
        if (version != ExplicitEnumValues)
        {
            Console.WriteLine("Usmap version " + version + " is newer than this converter.");
            return path;
        }

        var v3 = Path.Combine(Path.GetDirectoryName(path)!, "mappings.v3.usmap");
        if (File.Exists(v3) && File.GetLastWriteTimeUtc(v3) >= File.GetLastWriteTimeUtc(path) &&
            new FileInfo(v3).Length > 64)
        {
            Console.WriteLine("Usmap v4 -> using cached " + v3);
            return v3;
        }

        Console.WriteLine("Usmap v4 -> rewriting as v3 " + v3);
        try
        {
            File.WriteAllBytes(v3, RewriteV4AsV3(bytes));
            Console.WriteLine("Usmap v3 " + new FileInfo(v3).Length + " bytes");
            return v3;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Usmap rewrite failed: " + ex.Message);
            Console.WriteLine("  header " + BitConverter.ToString(bytes, 0, Math.Min(32, bytes.Length)));
            return path;
        }
    }

    static byte[] RewriteV4AsV3(byte[] file)
    {
        using var input = new MemoryStream(file, writable: false);
        using var br = new BinaryReader(input);
        if (br.ReadUInt16() != Magic) throw new InvalidDataException("usmap magic");
        if (br.ReadByte() != ExplicitEnumValues) throw new InvalidDataException("expected usmap v4");

        using var header = new MemoryStream();
        using var hw = new BinaryWriter(header, System.Text.Encoding.UTF8, leaveOpen: true);
        hw.Write(Magic);
        hw.Write(LargeEnums);

        var hasVersioning = br.ReadInt32();
        hw.Write(hasVersioning);
        if (hasVersioning != 0)
        {
            hw.Write(br.ReadInt32()); // FileVersionUE4
            hw.Write(br.ReadInt32()); // FileVersionUE5
            hw.Write(br.ReadInt32()); // licensee
            var customCount = br.ReadInt32();
            hw.Write(customCount);
            hw.Write(br.ReadBytes(customCount * 20));
        }

        var compression = br.ReadByte();
        var compressedSize = br.ReadUInt32();
        var decompressedSize = br.ReadUInt32();
        var raw = br.ReadBytes((int)compressedSize);
        var payload = Decompress(compression, raw, (int)decompressedSize);
        var converted = ConvertPayload(payload);

        hw.Write((byte)0);
        hw.Write((uint)converted.Length);
        hw.Write((uint)converted.Length);
        hw.Write(converted);
        return header.ToArray();
    }

    static byte[] ConvertPayload(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var br = new BinaryReader(input);
        using var output = new MemoryStream();
        using var bw = new BinaryWriter(output);

        var nameCount = br.ReadUInt32();
        bw.Write(nameCount);
        for (var i = 0; i < nameCount; i++)
        {
            var len = br.ReadUInt16();
            var chars = br.ReadBytes(len);
            if (chars.Length != len) throw new EndOfStreamException("name " + i);
            bw.Write(len);
            bw.Write(chars);
        }

        var enumCount = br.ReadUInt32();
        bw.Write(enumCount);
        for (var i = 0; i < enumCount; i++)
        {
            bw.Write(br.ReadInt32()); // enum name index
            var count = br.ReadUInt16();
            bw.Write(count);
            for (var j = 0; j < count; j++)
            {
                br.ReadInt64(); // v4 explicit value
                bw.Write(br.ReadInt32()); // name index
            }
        }

        Console.WriteLine("Usmap names=" + nameCount + " enums=" + enumCount +
                          " leftover=" + (br.BaseStream.Length - br.BaseStream.Position));

        var remaining = br.BaseStream.Length - br.BaseStream.Position;
        if (remaining > 0)
            bw.Write(br.ReadBytes((int)remaining));
        return output.ToArray();
    }

    static byte[] Decompress(byte method, byte[] data, int size)
    {
        if (method == 0) return data;
        if (method == 1)
        {
            using var input = new MemoryStream(data);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(size);
            z.CopyTo(output);
            return output.ToArray();
        }
        throw new InvalidDataException("usmap compression " + method);
    }
}
