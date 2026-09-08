namespace ItemAtlas;

public sealed class ExistingAffixes
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly HashSet<(bool Prefix,string Family)> _families = new();
    private readonly HashSet<string> _corruptedFamilies = new();
    public void Clear() { _ids.Clear(); _families.Clear(); _corruptedFamilies.Clear(); }
    public void AddCorrupted(string id, IEnumerable<ModifierEntry> known)
    {
        foreach(var m in known.Where(m=>m.Kind=="Corrupted" && m.Id==id))
        { _ids.Add(id); _corruptedFamilies.Add(m.Family); }
    }
    public void Add(string id, bool prefix, string family, IEnumerable<ModifierEntry> known)
    {
        if(!string.IsNullOrEmpty(id)) _ids.Add(id);
        if(!string.IsNullOrEmpty(family)) _families.Add((prefix,family));
        foreach(var m in known.Where(m=>m.Id==id && m.Prefix==prefix)) _families.Add((prefix,m.Family));
    }
    public bool Exact(ModifierEntry m) => _ids.Contains(m.Id);
    public bool Conflicts(ModifierEntry m) => Exact(m) || (m.Kind=="Corrupted" ? _corruptedFamilies.Contains(m.Family) : _families.Contains((m.Prefix,m.Family)));
}
