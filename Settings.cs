using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace LowerPrice;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    
    [Menu("Price Ratio", "Multiplier for item prices (0.0–1.0)")]
    public RangeNode<float> PriceRatio { get; set; } = new(0.9f, 0.0f, 1.0f);

    [Menu("Use Flat Reduction", "Use flat number reduction instead of percentage")]
    public ToggleNode UseFlatReduction { get; set; } = new(false);

    [Menu("Flat Reduction Amount", "Amount to subtract from item prices")]
    public RangeNode<int> FlatReductionAmount { get; set; } = new(1, 1, 100);

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

    [Menu("Enable Timer")]
    public ToggleNode EnableTimer { get; set; } = new(false);

    [Menu("Timer Duration (minutes)", "How long to wait before playing sound notification")]
    public RangeNode<int> TimerDurationMinutes { get; set; } = new(60, 1, 300);

    [Menu("Show Timer Countdown", "Display countdown timer on screen")]
    public ToggleNode ShowTimerCountdown { get; set; } = new(true);

    [Menu("Enable Sound Notification", "Play sound when timer expires")]
    public ToggleNode EnableSoundNotification { get; set; } = new(true);
}