using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace LowerPrice;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    
    [Menu("Price Ratio", "Multiplier for item prices (0.0–1.0)")]
    public RangeNode<float> PriceRatio { get; set; } = new(0.9f, 0.0f, 1.0f);

    [Menu("Action Delay (ms)", "Delay between actions to simulate human behavior")]
    public RangeNode<int> ActionDelay { get; set; } = new(75, 50, 1000);

    [Menu("Random Delay (ms)", "Random delay added to action delay (0-100ms)")]
    public RangeNode<int> RandomDelay { get; set; } = new(25, 0, 100);

    [Menu("Pickup Items Listed for 1 Currency Unit", "Control-left-click items priced at 1 instead of repricing")]
    public ToggleNode PickupItemsAtOne { get; set; } = new(false);

    [Menu("Reprice Chaos Orb")]
    public ToggleNode RepriceChaos { get; set; } = new(true);

    [Menu("Reprice Divine Orb")]
    public ToggleNode RepriceDivine { get; set; } = new(true);

    [Menu("Reprice Exalted Orb")]
    public ToggleNode RepriceExalted { get; set; } = new(true);

    [Menu("Cancel With Right Mouse Button", "Cancel operation on manual right-click")]
    public ToggleNode CancelWithRightClick { get; set; } = new(true);
}