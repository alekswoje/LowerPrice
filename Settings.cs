using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace LowerPrice;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    
    [Menu("Price Ratio", "Multiplier for item prices (0.0–1.0)")]
    public RangeNode<float> PriceRatio { get; set; } = new(0.9f, 0.0f, 1.0f);

    [Menu("Discount Hotkey", "Hotkey to trigger price discount")]
    public HotkeyNode DiscountHotkey { get; set; } = new(System.Windows.Forms.Keys.F6);
}