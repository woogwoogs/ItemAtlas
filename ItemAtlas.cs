using System.Globalization;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ImGuiNET;

namespace ItemAtlas;

public sealed partial class ItemAtlas : BaseSettingsPlugin<ItemAtlasSettings>
{
    private BaseDatabase _db = new();
    private ItemBase? _selected;
    private long _hoveredItemAddress;
    private bool _open, _special, _scroll;
    private string _search = "", _class = "", _notice = "", _error = "", _uniqueSearch = "";
    private string[] _classes = Array.Empty<string>();
    private int _maxLevel = 100, _sort, _uniqueScope, _defence, _view;
    private BaseRow[] _rows = Array.Empty<BaseRow>();
    private string Label(ItemBase b) => _rows.FirstOrDefault(r => r.Contains(b))?.Label ?? b.Name;
    private static readonly Vector4 Teal = new(.30f,.83f,.73f,1), Gold = new(1,.76f,.35f,1), Muted = new(.58f,.63f,.68f,1);
    private static void Text(string text) => ImGui.TextUnformatted(text.Replace('\u2013','-').Replace('\u2014','-'));
    private static void Color(Vector4 c, string text) { ImGui.PushStyleColor(ImGuiCol.Text,c); Text(text); ImGui.PopStyleColor(); }
    private static void Wrap(string text) { ImGui.PushTextWrapPos(0); Text(text); ImGui.PopTextWrapPos(); }
    private static string F(double? n) => n?.ToString("0.##",CultureInfo.InvariantCulture) ?? "-";
    private static void Cell(string text) => Text(text.Replace("\n", " / "));
    public override bool Initialise()
    {
        CanUseMultiThreading = false;
        try
        {
            _db = BaseDatabase.Load();
            _pools=ModifierPool.Load();
            _rows = BaseRow.Group(_db.Items);
            _classes = _db.Items.Select(x => x.Class).Distinct().Order().ToArray();
            Select(_db.Items.Find(x => x.Metadata == Settings.LastBase) ?? _db.Items.First(x => x.Class == "One Hand Mace"));
        }
        catch(Exception e) { _error = e.Message; }
        return true;
    }
    private void Select(ItemBase item)
    {
        if(_selected != item) ResetModifierView();
        _existing.Clear(); _affixRead="Prefixes: - | Suffixes: -"; _itemLevel=0; _uniqueItem=false; _pool=ModifierPool.Resolve(_pools,item);
        _selected = item; _class = item.Class; Settings.LastBase = item.Metadata;
        _view = 0; _defence = item.DefenceType; _uniqueScope = 0; _uniqueSearch = "";
        _search = ""; _maxLevel = 100; _special = false; _scroll = true;
    }
    public override void Tick()
    {
        if (!Settings.Enable.Value || !GameController.InGame || !GameController.Window.IsForeground()) return;
        bool modsKey=Settings.ModifiersKey.PressedOnce();
        bool baseKey=Settings.OpenKey.PressedOnce();
        if(!modsKey && !baseKey) return;
        _notice = "";
        try
        {
            var hover = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
            var item = hover?.Item;
            if (hover is { IsValid: true } && item is { IsValid: true })
            {
                var match = _db.Match(item.Path, item.GetComponent<Base>()?.Name);
                if (match != null) {
                    if(_hoveredItemAddress != item.Address) ResetModifierView();
                    _hoveredItemAddress=item.Address;
                    Select(match);
                    var itemMods=item.GetComponent<Mods>();
                    _itemLevel=itemMods?.ItemLevel ?? 0;
                    ReadAffixes(itemMods);
                    _uniqueItem=itemMods?.ItemRarity.ToString()=="Unique";
                    if(modsKey) _view=3;
                    _notice = "Hovered base selected."; }
                else _notice = "That item is not in the base database. Use the base table to select one.";
                _open = true; return;
            }
        }
        catch { _notice = "Could not read the hovered item. Browse the database below."; _open = true; return; }
        if(modsKey) { _view=3; _open=true; } else _open = !_open;
    }
    public override void DrawSettings()
    {
        base.DrawSettings();
        if(ImGui.Button("Open ItemAtlas")) _open = true;
        Wrap("Hover an inventory or stash item: F8 opens bases; F9 opens modifiers. Both keys are configurable. Press it away from items to toggle; Escape closes the focused window.");
        if(_error.Length > 0) Wrap(_error);
    }
    public override void Render()
    {
        if(!Settings.Enable.Value || !_open || !GameController.InGame) return;
        float s = Settings.Scale.Value / 100f;
        ImGui.SetNextWindowSize(new Vector2(1120,760)*s,ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(850,560)*s,new Vector2(float.MaxValue));
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.055f,.065f,.08f,.98f));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.10f,.29f,.28f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,new Vector4(.14f,.36f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.12f,.23f,.25f,1));
        bool visible = ImGui.Begin("ItemAtlas###ItemAtlasWindow",ref _open,ImGuiWindowFlags.NoCollapse);
        try
        {
            ImGui.SetWindowFontScale(s);
            if(!visible) return;
            if(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.Escape)) { _open=false; return; }
            if(_error.Length > 0) { Wrap(_error); return; }
            Text(_selected == null ? "BASE ITEMS" : $"{_class}  |  Selected: {Label(_selected)}");
            if(_notice.Length > 0) Color(Muted,_notice);
            if(ImGui.RadioButton("Bases",_view==0)) _view=0;
            ImGui.SameLine(); if(ImGui.RadioButton("Uniques",_view==2)) _view=2;
            ImGui.SameLine(); if(ImGui.RadioButton("Modifiers",_view==3)) _view=3;
            ImGui.Separator();
            ImGui.BeginChild("content",new Vector2(0,-26*s),ImGuiChildFlags.None);
            try { if(_view==0) Catalog(); else if(_view==3) ModifierView(); else Uniques(); }
            finally { ImGui.EndChild(); }
            Color(Muted,$"Patch {_db.Patch} | Database updated: {_db.Updated}");
        }
        finally { ImGui.End(); ImGui.PopStyleColor(4); }
    }
    private void Catalog()
    {
        ImGui.SetNextItemWidth(200);
        if(ImGui.BeginCombo("##class",_class))
        {
            foreach(var c in _classes) if(ImGui.Selectable(c,c == _class)) { Select(_db.Items.First(x => x.Class == c)); _defence=0; _notice=""; }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(240); ImGui.InputTextWithHint("##search","Search bases...",ref _search,120);
        if(_rows.Any(r=>r.Base.Class==_class && r.Base.DefenceType!=0))
        {
            Text("Type:");
            int typeIndex=0;
            foreach(var pair in new[]{(0,"All"),(1,"Armour"),(2,"Evasion"),(4,"Energy shield"),(3,"Armour / Evasion"),(5,"Armour / ES"),(6,"Evasion / ES"),(7,"All three")})
            {
                if(pair.Item1!=0 && !_rows.Any(r=>r.Base.Class==_class && r.Base.DefenceType==pair.Item1)) continue;
                if(typeIndex++ % 4 != 0) ImGui.SameLine();
                if(ImGui.RadioButton(pair.Item2,_defence==pair.Item1)) { _defence=pair.Item1; _scroll=false; }
            }
        }
        if(ImGui.TreeNode("More filters"))
        {
            ImGui.SetNextItemWidth(150); ImGui.SliderInt("Max drop level",ref _maxLevel,1,100);
            ImGui.Checkbox("Include restricted / legacy bases",ref _special);
            ImGui.SetNextItemWidth(160); ImGui.Combo("Sort",ref _sort,"Drop level\0Name\0Raw base DPS\0Armour\0Evasion\0Energy shield\0");
            ImGui.TreePop();
        }
        var rows=_rows.Where(r=>r.Base.Class==_class && (_defence==0 || r.Base.DefenceType==_defence) &&
            r.Base.Name.Contains(_search,StringComparison.OrdinalIgnoreCase) && (r.Base.Stat("Drop level")??0)<=_maxLevel &&
            (_special || r.Base.Availability=="Not flagged" || r.Contains(_selected)));
        rows=_sort switch { 1=>rows.OrderBy(r=>r.Label), 2=>rows.OrderByDescending(r=>r.Base.Stat("Raw base DPS")),
            3=>rows.OrderByDescending(r=>r.Base.Stat("Armour")),4=>rows.OrderByDescending(r=>r.Base.Stat("Evasion")),
            5=>rows.OrderByDescending(r=>r.Base.Stat("Energy shield")), _=>rows.OrderBy(r=>r.Base.Stat("Drop level")).ThenBy(r=>r.Label) };
        var list=rows.ToArray();
        Color(Muted,$"{list.Length} bases  |  Select a base to view its uniques");
        ImGui.BeginChild("base-sections",Vector2.Zero,ImGuiChildFlags.None);
        try
        {
            BaseTable("Regular bases",list.Where(r=>!r.Base.IsRuneforged).ToArray());
            BaseTable("Runeforged / Runemastered bases",list.Where(r=>r.Base.IsRuneforged).ToArray());
            if(list.Length==0) Wrap("No matching bases. Adjust the type or search filters.");
            _scroll=false;
        }
        finally { ImGui.EndChild(); }
    }
    private void BaseTable(string title, BaseRow[] rows)
    {
        if(rows.Length==0) return;
        ImGui.Spacing(); Text(title);
        var stats=new[]{"Armour","Evasion","Energy shield","Runic ward","Block %","Raw base DPS","Attacks/sec","Crit %","Life per use","Mana per use","Movement %"}
            .Where(k=>rows.Any(r=>(r.Base.Stat(k)??0)!=0)).ToArray();
        float implicitWidth=320*Settings.Scale.Value/100f;
        float tableHeight = ImGui.GetTextLineHeight()+ImGui.GetStyle().CellPadding.Y*2+ImGui.GetStyle().ScrollbarSize+8;
        foreach(var row in rows) tableHeight += Math.Max(ImGui.GetTextLineHeight(),ImGui.CalcTextSize(string.Join("\n",row.Base.Notes),false,implicitWidth-12).Y)+ImGui.GetStyle().CellPadding.Y*2;
        if(!ImGui.BeginTable(title+"##visible-implicit-v5",stats.Length+4,ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollX, new Vector2(0,tableHeight), (720+stats.Length*85)*Settings.Scale.Value/100f)) return;
        try
        {
            ImGui.TableSetupColumn("Name",ImGuiTableColumnFlags.WidthFixed,230*Settings.Scale.Value/100f);
            ImGui.TableSetupColumn("Implicit",ImGuiTableColumnFlags.WidthFixed,implicitWidth);
            ImGui.TableSetupColumn("Lvl",ImGuiTableColumnFlags.WidthFixed,35);
            ImGui.TableSetupColumn("Req",ImGuiTableColumnFlags.WidthFixed,100);
            foreach(var k in stats) ImGui.TableSetupColumn(k,ImGuiTableColumnFlags.WidthFixed, k=="Energy shield" ? 90 : 70);

            ImGui.TableHeadersRow();
            foreach(var row in rows)
            {
                var b=row.Base;
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                if(ImGui.Selectable((row.Contains(_selected)?"* ":"")+row.Label+"##"+b.Id,row.Contains(_selected))) { _selected=row.Contains(_selected)?_selected:b; Settings.LastBase=_selected!.Metadata; _existing.Clear(); _affixRead="Prefixes: - | Suffixes: -"; _itemLevel=0; _uniqueItem=false; _pool=ModifierPool.Resolve(_pools,_selected); }
                if(_scroll && row.Contains(_selected)) ImGui.SetScrollHereY(.35f);
                ImGui.TableNextColumn(); Wrap(b.Notes.Length==0?"-":string.Join("\n",b.Notes));
                ImGui.TableNextColumn(); Text(F(b.Stat("Drop level")));
                ImGui.TableNextColumn(); Cell(string.Join(" / ",new[]{("Strength","Str"),("Dexterity","Dex"),("Intelligence","Int")}.Where(p=>(b.Stat(p.Item1)??0)>0).Select(p=>F(b.Stat(p.Item1))+" "+p.Item2)) is { Length: >0 } req ? req : "-");
                foreach(var k in stats) { ImGui.TableNextColumn(); Text(F(b.Stat(k))); }

            }
        }
        finally { ImGui.EndTable(); }
    }
    private void Uniques()
    {
        if(_selected==null) return;
        Color(Gold,"UNIQUE ITEMS");
        if(ImGui.RadioButton("Selected base",_uniqueScope==0)) _uniqueScope=0;
        ImGui.SameLine(); if(ImGui.RadioButton("Whole class",_uniqueScope==2)) _uniqueScope=2;
        var target = _selected;
        Wrap(_uniqueScope==2 ? $"Class: {_class}" : $"Base: {Label(target)}");
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##unique-search","Search uniques...",ref _uniqueSearch,120);
        var matches=_db.Uniques.Where(u => u.Class==_class && (_uniqueScope==2 || u.Base==target.Name || u.OtherBases.Contains(target.Name)) && u.Name.Contains(_uniqueSearch,StringComparison.OrdinalIgnoreCase)).ToArray();
        Color(Muted,$"{matches.Length} uniques. Expand a name for reference modifiers.");
        foreach(var u in matches)
        {
            if(ImGui.TreeNode(u.Name+" - "+u.Base+"##unique"+u.Name))
            {
                Wrap("Reference base: "+u.Base);
                if(u.OtherBases.Length>0) Wrap("Other source associations (may be historical): "+string.Join(", ",u.OtherBases));
                Color(Muted,u.Variant);
                foreach(var mod in u.Mods) Wrap(mod);
                if(u.Mods.Length==0) Wrap("Modifier details are not available in this snapshot.");
                var b=_db.Items.FirstOrDefault(x=>x.Class==_class && x.Name==u.Base);
                if(b!=null && ImGui.SmallButton("View base##"+u.Name)) Select(b);
                ImGui.TreePop();
            }
        }
        if(matches.Length==0) Wrap("No matching uniques in this snapshot. Try the whole-class option.");
    }
}