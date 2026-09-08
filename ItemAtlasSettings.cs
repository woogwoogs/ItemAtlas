using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace ItemAtlas;
public sealed class ItemAtlasSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    public HotkeyNodeV2 ModifiersKey { get; set; } = new(Keys.F9);
    public HotkeyNodeV2 OpenKey { get; set; } = new(Keys.F8);
    public RangeNode<int> Scale { get; set; } = new(100, 75, 150);
    public string LastBase { get; set; } = "";
}
