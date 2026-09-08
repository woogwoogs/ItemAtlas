using System.Reflection;
using System.Text.Json;

namespace ItemAtlas;

public sealed class BaseDatabase
{
    public int Schema { get; set; }
    public string Updated { get; set; } = "";
    public string Patch { get; set; } = "";
    public List<ItemBase> Items { get; set; } = new();
    public List<UniqueItem> Uniques { get; set; } = new();
    public static BaseDatabase Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ItemAtlas.Data.items.json")
            ?? throw new InvalidDataException("Missing bundled database. Reinstall ItemAtlas.");
        var db = JsonSerializer.Deserialize<BaseDatabase>(stream) ?? throw new InvalidDataException("Empty database.");
        if (db.Schema != 1 || db.Items.Count == 0 || db.Items.Select(x => x.Metadata).Distinct().Count() != db.Items.Count)
            throw new InvalidDataException("Invalid base database.");
        return db;
    }
    public ItemBase? Match(string? path, string? name)
    {
        var match = Items.Find(x => string.Equals(x.Metadata, path, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
        var names = Items.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return names.Length == 1 ? names[0] : null;
    }
}
public sealed class ItemBase
{
    public bool IsRuneforged => Name.StartsWith("Runeforged ", StringComparison.OrdinalIgnoreCase) || Name.StartsWith("Runemastered ", StringComparison.OrdinalIgnoreCase);
    public int DefenceType => ((Stat("Armour") ?? 0) > 0 ? 1 : 0) | ((Stat("Evasion") ?? 0) > 0 ? 2 : 0) | ((Stat("Energy shield") ?? 0) > 0 ? 4 : 0);
    public string Signature => JsonSerializer.Serialize(new { Name, Class, Availability, Stats = Stats.OrderBy(x => x.Key).ToArray(), Notes = Notes.Order().ToArray() });
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Metadata { get; set; } = "";
    public string Class { get; set; } = "";
    public string Availability { get; set; } = "";
    public Dictionary<string, double> Stats { get; set; } = new();
    public string[] Notes { get; set; } = Array.Empty<string>();
    public double? Stat(string key) => Stats.TryGetValue(key, out var value) ? value : null;
}
public sealed class BaseRow
{
    public ItemBase[] Items { get; init; } = Array.Empty<ItemBase>();
    public ItemBase Base => Items[0];
    public string Label { get; set; } = "";
    public bool Contains(ItemBase? item) => item != null && Items.Contains(item);
    public static BaseRow[] Group(IEnumerable<ItemBase> items)
    {
        var rows = items.GroupBy(x => x.Signature).Select(g => new BaseRow { Items = g.OrderBy(x => x.Metadata).ToArray(), Label = g.First().Name }).ToArray();
        foreach(var group in rows.GroupBy(x => (x.Base.Class, x.Base.Name)).Where(g => g.Count() > 1))
        {
            int i=0;
            foreach(var row in group.OrderBy(x => x.Base.Metadata)) row.Label += $" [variant {++i}]";
        }
        return rows;
    }
}
public static class Availability
{
    public static string AvailabilityHelp(string status) => status switch
    {
        "standard-only" => "Older base associated with Standard in this database snapshot.",
        "unique-only" => "Base associated with unique items rather than ordinary base drops.",
        "unavailable" => "Marked unavailable in this database snapshot.",
        "Not flagged" => "No special availability flag is recorded. This does not guarantee that the base drops.",
        _ => "Special availability recorded by the database: " + status
    };
}
public sealed class UniqueItem
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public string Base { get; set; } = "";
    public string[] OtherBases { get; set; } = Array.Empty<string>();
    public string[] Mods { get; set; } = Array.Empty<string>();
    public string Variant { get; set; } = "";
}
