using ImGuiNET;
using System.Numerics;

namespace ItemAtlas;
public sealed partial class ItemAtlas
{
    private readonly ExistingAffixes _existing = new();
    private string _affixRead = "Prefixes: - | Suffixes: -";
    private void ReadAffixes(ExileCore2.PoEMemory.Components.Mods? mods)
    {
        _existing.Clear();
        if(mods?.ItemMods == null) { _affixRead="Prefixes: - | Suffixes: -"; return; }
        int prefixes=0, suffixes=0;
        try
        {
            var known=_pools.SelectMany(p=>p.Mods).ToArray();
            if(mods.ImplicitMods!=null) foreach(var mod in mods.ImplicitMods) if(mod!=null) _existing.AddCorrupted(mod.RawName,known);
            foreach(var mod in mods.ItemMods)
            {
                if(mod!=null) _existing.AddCorrupted(mod.RawName,known);
                var record=mod?.ModRecord;
                if(record==null) continue;
                if(mods.ImplicitMods?.Any(i=>i.RawName==mod!.RawName)==true) continue;
                var affix=record.AffixType.ToString();
                if(affix!="Prefix" && affix!="Suffix") continue;
                _existing.Add(mod!.RawName,affix=="Prefix",record.Group,known);
                _existing.Add(mod.RawName,affix=="Prefix",record.TypeName,known);
                if(affix=="Prefix") prefixes++; else suffixes++;
            }
            _affixRead=$"Prefixes: {prefixes} | Suffixes: {suffixes}";
        }
        catch { _existing.Clear(); _affixRead="Prefixes: - | Suffixes: -"; }
    }
    private readonly HashSet<string> _expandedModifiers = new();
    private bool _resetModifierScroll;
    private void ResetModifierView()
    {
        _modKind=0;
        _modSearch="";
        _expandedModifiers.Clear();
        _resetModifierScroll=true;
    }
    private List<ModifierPool> _pools=new();
    private ModifierPool? _pool;
    private int _itemLevel, _modKind;
    private bool _uniqueItem;
    private string _modSearch="";
    private void ModifierView()
    {
        if(_selected==null) return;
        Text($"{Label(_selected)} | Item level: {(_itemLevel>0?_itemLevel.ToString():"unknown")}");
        if(_pool==null) { Wrap("Modifier data unavailable for this base."); return; }
        foreach(var pair in new[]{(0,"Base"),(1,"Desecrated"),(2,"Essence"),(3,"Corrupted")})
        { if(pair.Item1>0) ImGui.SameLine(); if(ImGui.RadioButton(pair.Item2,_modKind==pair.Item1)) _modKind=pair.Item1; }
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##mod-search","Search modifiers or essence names...",ref _modSearch,160);
        Wrap(_affixRead);
        string kind=new[]{"Base","Desecrated","Essence","Corrupted"}[_modKind];
        var mods=_pool.Mods.Where(m=>m.Kind==kind && (m.Text.Contains(_modSearch,StringComparison.OrdinalIgnoreCase) || m.Name.Contains(_modSearch,StringComparison.OrdinalIgnoreCase))).ToArray();
        if(kind=="Corrupted")
        {
            ImGui.BeginChild("corrupted-results",Vector2.Zero,ImGuiChildFlags.None);
            try { ModifierColumn(mods,kind,false); } finally { ImGui.EndChild(); _resetModifierScroll=false; }
            return;
        }
        float width=(ImGui.GetContentRegionAvail().X-ImGui.GetStyle().ItemSpacing.X)/2;
        ImGui.BeginChild("prefix-results",new Vector2(width,0),ImGuiChildFlags.None);
        try { ModifierColumn(mods.Where(m=>m.Prefix).ToArray(),kind,true); } finally { ImGui.EndChild(); }
        ImGui.SameLine();
        ImGui.BeginChild("suffix-results",Vector2.Zero,ImGuiChildFlags.None);
        try { ModifierColumn(mods.Where(m=>!m.Prefix).ToArray(),kind,false); } finally { ImGui.EndChild(); _resetModifierScroll=false; }
    }
    private void ModifierColumn(ModifierEntry[] entries,string kind,bool prefix)
    {
        if(_resetModifierScroll) ImGui.SetScrollY(0);
        Text(kind=="Corrupted"?"CORRUPTED IMPLICITS":prefix?"PREFIXES":"SUFFIXES");
        if(entries.Length==0) { Color(Muted,"No modifiers in this section."); return; }
        foreach(var group in entries.GroupBy(m=>m.Family).OrderBy(g=>g.First().Text,StringComparer.OrdinalIgnoreCase))
        {
            var tiers=group.OrderByDescending(m=>m.Level).ThenBy(m=>m.Name).ToArray();
            var fullFamily=_pool!.Mods.Where(m=>m.Kind==kind && m.Prefix==prefix && m.Family==group.Key).ToArray();
            bool owned=fullFamily.Any(m=>_existing.Conflicts(m));
            var current=fullFamily.FirstOrDefault(m=>_existing.Exact(m));
            string summary=(current ?? fullFamily.OrderByDescending(m=>m.Level).First()).Text.Replace("\n"," / ");
            string label=summary+$"  [{tiers.Length} tiers]"+(current!=null?"  [ON ITEM]":owned?"  [FAMILY ON ITEM]":"");
            ImGui.PushStyleColor(ImGuiCol.Text,owned?Gold:new Vector4(.88f,.89f,.91f,1));
            // Wrapped selectable header avoids clipped family names in the two-column view.
            string id=kind+prefix+group.Key;
            bool expanded=_expandedModifiers.Contains(id);
            float h=Math.Max(ImGui.GetTextLineHeight(),ImGui.CalcTextSize(label,false,Math.Max(80,ImGui.GetContentRegionAvail().X-24)).Y);
            var pos=ImGui.GetCursorPos();
            if(ImGui.Selectable("##"+id,expanded,ImGuiSelectableFlags.None,new Vector2(0,h+6)))
            { expanded=!expanded; if(expanded) _expandedModifiers.Add(id); else _expandedModifiers.Remove(id); }
            var next=ImGui.GetCursorPos(); ImGui.SetCursorPos(pos+new Vector2(4,3));
            Text(expanded?"v":">"); ImGui.SameLine(); ImGui.PushTextWrapPos(ImGui.GetWindowContentRegionMax().X-8); Text(label); ImGui.PopTextWrapPos();
            ImGui.SetCursorPos(next); ImGui.PopStyleColor();
            if(!expanded) continue;
            if(!ImGui.BeginTable("tiers-"+id,3,ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) continue;
            try
            {
                ImGui.TableSetupColumn("Tier / iLvl",ImGuiTableColumnFlags.WidthFixed,80);
                ImGui.TableSetupColumn("Roll",ImGuiTableColumnFlags.WidthStretch,3);
                ImGui.TableSetupColumn("Status",ImGuiTableColumnFlags.WidthStretch,2);
                ImGui.TableHeadersRow();
                foreach(var m in tiers)
                {
                    bool conflict=_existing.Conflicts(m);
                    string status=m.Status(_selected!.Id,_itemLevel,_uniqueItem);
                    if(conflict) status=_existing.Exact(m)?"On item":"Blocked";
                    bool available=status.StartsWith("Tier eligible") || status=="Requires this essence";
                    ImGui.PushStyleColor(ImGuiCol.Text,_existing.Exact(m)?Gold:conflict?Muted:available?new Vector4(.88f,.89f,.91f,1):Muted);
                    ImGui.TableNextRow(); ImGui.TableNextColumn(); Text(_pool!.TierLabel(m)+" / "+m.Level);
                    ImGui.TableNextColumn(); Wrap(m.Text); if(m.Name.Length>0) Wrap(m.Name);
                    ImGui.TableNextColumn(); Wrap(status); if(m.Restriction.Length>0) Wrap("Base: "+m.Restriction);
                    ImGui.PopStyleColor();
                }
            }
            finally { ImGui.EndTable(); }
        }
    }
}