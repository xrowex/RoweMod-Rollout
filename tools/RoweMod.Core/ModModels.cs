using System.Text.Json.Serialization;

namespace RoweMod.Core;

public sealed class ModInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Row { get; set; } = "";
    public string Version { get; set; } = "1";
    public string Table { get; set; } = "";
}

public sealed class CatalogFile
{
    public string Repo { get; set; } = GalleryClient.DefaultRepo;
    public List<CatalogEntry> Mods { get; set; } = new();
}

public sealed class CatalogEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Row { get; set; } = "";
    public string Version { get; set; } = "1";
    public string Path { get; set; } = "";
    public string? Preview { get; set; }
}

public sealed class Subscriptions
{
    public List<string> Ids { get; set; } = new();
}

public sealed class GitHubTokenFile
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
