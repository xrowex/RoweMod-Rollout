using System.Text.Json;

namespace RoweMod.Core;

public static class SubscriptionStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static Subscriptions Load()
    {
        AppPaths.EnsureAppData();
        var path = AppPaths.SubscriptionsFile;
        if (!File.Exists(path)) return new Subscriptions();
        try
        {
            return JsonSerializer.Deserialize<Subscriptions>(File.ReadAllText(path), Json) ?? new Subscriptions();
        }
        catch
        {
            return new Subscriptions();
        }
    }

    public static void Save(Subscriptions subs)
    {
        AppPaths.EnsureAppData();
        subs.Ids = subs.Ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        File.WriteAllText(AppPaths.SubscriptionsFile, JsonSerializer.Serialize(subs, Json));
    }

    public static bool IsSubscribed(string id) =>
        Load().Ids.Contains(id, StringComparer.OrdinalIgnoreCase);

    public static void Set(string id, bool on)
    {
        var subs = Load();
        subs.Ids.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (on) subs.Ids.Add(id);
        Save(subs);
    }
}
