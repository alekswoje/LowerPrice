using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LowerPrice;

public class Settings : ISettings
{
    // ===== GENERAL SETTINGS =====
    public ToggleNode Enable { get; set; } = new(true);
    
    [Menu("Action Delay (ms)", "Delay between actions to simulate human behavior")]
    public RangeNode<int> ActionDelay { get; set; } = new(75, 50, 1000);

    [Menu("Random Delay (ms)", "Random delay added to action delay (0-100ms)")]
    public RangeNode<int> RandomDelay { get; set; } = new(25, 0, 100);

    [Menu("Cancel With Right Mouse Button", "Cancel operation on manual right-click")]
    public ToggleNode CancelWithRightClick { get; set; } = new(true);

    // ===== CURRENCY SELECTION =====
    [Menu("Reprice Chaos Orb", "Enable repricing for Chaos Orbs")]
    public ToggleNode RepriceChaos { get; set; } = new(true);

    [Menu("Reprice Divine Orb", "Enable repricing for Divine Orbs")]
    public ToggleNode RepriceDivine { get; set; } = new(true);

    [Menu("Reprice Exalted Orb", "Enable repricing for Exalted Orbs")]
    public ToggleNode RepriceExalted { get; set; } = new(true);

    [Menu("Reprice Annul Orb", "Enable repricing for Annul Orbs")]
    public ToggleNode RepriceAnnul { get; set; } = new(true);

    // ===== PRICING STRATEGY =====
    [Menu("Use Flat Reduction", "Use flat number reduction instead of percentage")]
    public ToggleNode UseFlatReduction { get; set; } = new(false);

    [Menu("Price Ratio", "Multiplier for item prices (0.0–1.0)")]
    public RangeNode<float> PriceRatio { get; set; } = new(0.9f, 0.0f, 1.0f);

    [Menu("Flat Reduction Amount", "Amount to subtract from item prices")]
    public RangeNode<int> FlatReductionAmount { get; set; } = new(1, 1, 100);

    // Currency-specific overrides
    [Menu("Divine Override", "Force flat reduction for Divine Orbs (overrides global setting)")]
    public ToggleNode DivineUseFlat { get; set; } = new(true);

    [Menu("Chaos Override", "Force flat reduction for Chaos Orbs (overrides global setting)")]
    public ToggleNode ChaosUseRatio { get; set; } = new(true);

    [Menu("Exalted Override", "Force flat reduction for Exalted Orbs (overrides global setting)")]
    public ToggleNode ExaltedUseRatio { get; set; } = new(true);

    [Menu("Annul Override", "Force flat reduction for Annul Orbs (overrides global setting)")]
    public ToggleNode AnnulUseFlat { get; set; } = new(true);

    // ===== SPECIAL ACTIONS =====
    [Menu("Pickup Items at 1 Currency", "Control-left-click items priced at 1 instead of repricing")]
    public ToggleNode PickupItemsAtOne { get; set; } = new(false);

    [Menu("Reprice Hotkey", "Hotkey to trigger repricing manually")]
    public HotkeyNode ManualRepriceHotkey { get; set; } = new(Keys.None);

    // ===== TIMER & NOTIFICATIONS =====
    [Menu("Enable Timer", "Enable timer functionality")]
    public ToggleNode EnableTimer { get; set; } = new(false);

    [Menu("Timer Duration (minutes)", "How long to wait before playing sound notification")]
    public RangeNode<int> TimerDurationMinutes { get; set; } = new(60, 1, 300);

    [Menu("Show Timer Countdown", "Display countdown timer on screen")]
    public ToggleNode ShowTimerCountdown { get; set; } = new(true);

    [Menu("Enable Sound Notification", "Play sound when timer expires")]
    public ToggleNode EnableSoundNotification { get; set; } = new(true);

    // ===== VALUE DISPLAY =====
    [Menu("Show Value Display", "Display total value of items in merchant panel")]
    public ToggleNode ShowValueDisplay { get; set; } = new(true);

    [Menu("Value Display Position X", "X position of value display")]
    public RangeNode<int> ValueDisplayX { get; set; } = new(10, 0, 2000);

    [Menu("Value Display Position Y", "Y position of value display")]
    public RangeNode<int> ValueDisplayY { get; set; } = new(100, 0, 2000);

    [Menu("Auto-Update Currency Rates", "Automatically fetch currency rates from poe.ninja")]
    public ToggleNode AutoUpdateRates { get; set; } = new(true);

    [Menu("Currency Update Interval (minutes)", "How often to update currency rates")]
    public RangeNode<int> CurrencyUpdateInterval { get; set; } = new(30, 5, 120);

    // Helper methods for backward compatibility
    // UseFlatReduction is now a direct property
}