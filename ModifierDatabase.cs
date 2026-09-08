using System.Reflection;
using System.Text.Json;

namespace ItemAtlas;
public sealed class ModifierPool
{
    public string Key { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Name { get; set; } = "";
    public int[] BaseIds { get; set; } = Array.Empty<int>();
    public List<ModifierEntry> Mods { get; set; } = new();
    public string TierLabel(ModifierEntry entry)
    {
        // Number the full natural-roll family, never the search-filtered rows.
        var natural = Mods.FirstOrDefault(m => m.Kind == "Base" && m.Id == entry.Id && m.Prefix == entry.Prefix);
        if(natural == null) return "-"; // Special-only outcomes have no ordinary tier ranking.
        var levels = Mods.Where(m => m.Kind == "Base" && m.Family == natural.Family && m.Prefix == natural.Prefix)
            .Select(m => m.Level).Distinct().OrderDescending().ToArray();
        return "T" + (Array.IndexOf(levels,natural.Level)+1);
    }
    public static List<ModifierPool> Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ItemAtlas.Data.mods.json") ?? throw new InvalidDataException("Missing modifier database.");
        return JsonSerializer.Deserialize<List<ModifierPool>>(stream) ?? throw new InvalidDataException("Empty modifier database.");
    }
    public static ModifierPool? Resolve(IEnumerable<ModifierPool> pools, ItemBase b)
    {
        var choices=pools.Where(p=>p.BaseIds.Contains(b.Id)).ToArray();
        if(choices.Length==1) return choices[0];
        string variant=b.Class=="Jewel" ? b.Name.ToLowerInvariant().Replace(' ','-') : b.DefenceType switch
        {1=>"str",2=>"dex",3=>"str_dex",4=>"int",5=>"str_int",6=>"dex_int",7=>"str_dex_int",_=>""};
        return choices.FirstOrDefault(p=>p.Variant==variant);
    }
}
public sealed class ModifierEntry
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Prefix { get; set; }
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public string Family { get; set; } = "";
    public string Text { get; set; } = "";
    public string Restriction { get; set; } = "";
    public double? Weight { get; set; }
    public string Status(int baseId,int level,bool unique)
    {
        if(unique && Kind!="Corrupted") return "Unique item: reference only";
        if(Restriction.Length>0 && !Restriction.Split(',',';','|').Any(s=>s.Trim().StartsWith(baseId+":"))) return "Different base required";
        // Forced essence modifiers are not treated as ordinary random rolls.
        if(Kind=="Essence") return "Requires this essence";
        if(Kind=="Base" && Weight==0) return "No natural spawn weight";
        if(level<=0) return "Item level unknown";
        if(level<Level) return $"Needs iLvl {Level}";
        if(Kind=="Corrupted") return "Tier eligible: corruption";
        return Kind=="Desecrated" ? "Tier eligible: desecration" : "Tier eligible";
    }
}
