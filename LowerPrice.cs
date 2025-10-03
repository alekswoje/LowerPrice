using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using LowerPrice.Utils;
using ImGuiNET;
using NAudio.Wave;
using System.Net.Http;
using System.Text.Json;
using RectangleF = ExileCore2.Shared.RectangleF;

namespace LowerPrice
{
    public class LowerPrice : BaseSettingsPlugin<Settings>
    {
        private readonly ConcurrentDictionary<RectangleF, bool?> _mouseStateForRect = new();
        private readonly Random random = new Random();
        private DateTime _lastRepriceTime = DateTime.MinValue;
        private bool _timerExpired = false;
        private WaveOutEvent _waveOut;
        private bool _manualRepriceTriggered = false;
        
        // Value display fields
        private readonly HttpClient _httpClient = new HttpClient();
        private DateTime _lastCurrencyUpdate = DateTime.MinValue;
        private Dictionary<string, decimal> _currencyRates = new Dictionary<string, decimal>();
        private readonly object _currencyRatesLock = new object();

        private bool MoveCancellationRequested => Settings.CancelWithRightClick && (Control.MouseButtons & MouseButtons.Right) != 0;

        private void CheckHotkeys()
        {
            // Check manual reprice hotkey
            if (Settings.ManualRepriceHotkey.PressedOnce())
            {
                _manualRepriceTriggered = true;
            }
        }

        public override bool Initialise()
        {
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\pick.png").Replace('\\', '/'), false);
            
            // Initialize currency rates with default values
            InitializeDefaultCurrencyRates();
            
            // Load currency rates from API/local file
            _ = Task.Run(async () => await UpdateCurrencyRates());
            
            return true;
        }

        public override void Render()
        {
            // Check hotkeys first
            CheckHotkeys();

            if (!Settings.Enable) return;

            // Render timer display
            if (Settings.EnableTimer && Settings.ShowTimerCountdown)
            {
                RenderTimerDisplay();
            }

            // Render value display
            if (Settings.ShowValueDisplay)
            {
                RenderValueDisplay();
            }

            var merchantPanel = GameController.IngameState.IngameUi.OfflineMerchantPanel;
            if (merchantPanel != null && merchantPanel.IsVisible)
            {
                const float buttonSize = 37;
                var offset = new Vector2(10, 10);
                var buttonPos = GameController.Window.GetWindowRectangleTimeCache.TopLeft + offset;
                var buttonRect = new RectangleF(buttonPos.X, buttonPos.Y, buttonSize, buttonSize);
                Graphics.DrawImage("pick.png", buttonRect);

                // Check for button press or manual trigger
                if (IsButtonPressed(buttonRect) || _manualRepriceTriggered)
                {
                    _manualRepriceTriggered = false; // Reset manual trigger
                    _ = Task.Run(async () =>
                    {
                        while (Control.MouseButtons == MouseButtons.Left)
                        {
                            await Task.Delay(10);
                        }
                        UpdateAllItemPrices(merchantPanel);
                    });
                }
            }
        }

        private async void UpdateAllItemPrices(Element merchantPanel)
        {
            try
            {
                var inventory = GameController.IngameState.IngameUi.OfflineMerchantPanel.AllInventories[0];
                if (inventory == null || !inventory.VisibleInventoryItems.Any()) return;

                foreach (var item in inventory.VisibleInventoryItems)
                {
                    try
                    {
                        if (!GameController.IngameState.IngameUi.OfflineMerchantPanel.IsVisible || MoveCancellationRequested)
                        {
                            break;
                        }

                        if (item.Children.Count == 2)
                        {
                            await TaskUtils.NextFrame();
                            await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                            continue;
                        }

                        var itemOffset = new Vector2(5, 5);
                        var position = item.GetClientRectCache.TopLeft + itemOffset + GameController.Window.GetWindowRectangleTimeCache.TopLeft;

                        Mouse.moveMouse(position);
                        await TaskUtils.NextFrame();
                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));

                        // Check if item is locked before processing
                        if (IsItemLocked(item))
                        {
                            await TaskUtils.NextFrame();
                            await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                            continue;
                        }

                        var tooltip = item.Tooltip;
                        if (tooltip != null && tooltip.Children.Count > 0)
                        {
                            var tooltipChild0 = tooltip.Children[0];
                            if (tooltipChild0 != null && tooltipChild0.Children.Count > 1)
                            {
                                var tooltipChild1 = tooltipChild0.Children[1];
                                if (tooltipChild1 != null && tooltipChild1.Children.Any())
                                {
                                    var lastChild = tooltipChild1.Children.Last();
                                    if (lastChild != null && lastChild.Children.Count > 1)
                                    {
                                        var priceChild1 = lastChild.Children[1];
                                        if (priceChild1 != null && priceChild1.Children.Count > 0)
                                        {
                                            var priceChild0 = priceChild1.Children[0];
                                            if (priceChild0 != null)
                                            {
                                                string priceText = priceChild0.Text;
                                                if (priceText != null && priceText.EndsWith("x"))
                                                {
                                                    string priceStr = priceText.Replace("x", "").Trim();
                                                    if (int.TryParse(priceStr, out int oldPrice))
                                                    {
                                                        string orbType = priceChild1.Children.Count > 2 ? priceChild1.Children[2].Text : null;
                                                        bool reprice = false;
                                                        if (orbType == "Chaos Orb" && Settings.RepriceChaos) reprice = true;
                                                        else if (orbType == "Divine Orb" && Settings.RepriceDivine) reprice = true;
                                                        else if (orbType == "Exalted Orb" && Settings.RepriceExalted) reprice = true;
                                                        else if (orbType == "Orb of Annulment" && Settings.RepriceAnnul) reprice = true;

                                                        if (!reprice) continue;

                                                        float newPrice = CalculateNewPrice(oldPrice, orbType);
                                                        
                                                        if (oldPrice == 1)
                                                        {
                                                            if (Settings.PickupItemsAtOne)
                                                            {
                                                                Keyboard.KeyDown(Keys.LControlKey);
                                                                await TaskUtils.NextFrame();
                                                                await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                                Mouse.LeftDown();
                                                                await TaskUtils.NextFrame();
                                                                await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                                Mouse.LeftUp();
                                                                await TaskUtils.NextFrame();
                                                                await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                                Keyboard.KeyUp(Keys.LControlKey);
                                                                await TaskUtils.NextFrame();
                                                                await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                            }
                                                            continue;
                                                        }

                                                        if (newPrice < 1) newPrice = 1;
                                                        Mouse.RightDown();
                                                        await TaskUtils.NextFrame();
                                                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                        Mouse.RightUp();
                                                        await TaskUtils.NextFrame();
                                                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                        Keyboard.Type($"{newPrice}");
                                                        await TaskUtils.NextFrame();
                                                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                        Keyboard.KeyPress(Keys.Enter);
                                                        await TaskUtils.NextFrame();
                                                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                                                        
                                                        // Update last reprice time and reset timer
                                                        if (Settings.EnableTimer)
                                                        {
                                                            _lastRepriceTime = DateTime.Now;
                                                            _timerExpired = false;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        await TaskUtils.NextFrame();
                        await Task.Delay(Settings.ActionDelay + random.Next(Settings.RandomDelay));
                    }
                    catch (Exception ex)
                    {
                        // Log error for individual item processing but continue with next item
                        LogError($"Error processing item: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error for the entire reprice operation
                LogError($"Error in UpdateAllItemPrices: {ex.Message}");
            }
        }

        private float CalculateNewPrice(int oldPrice, string orbType)
        {
            bool useFlatReduction = false;

            // Check for currency-specific overrides first
            switch (orbType)
            {
                case "Divine Orb":
                    // Divine Override: if checked, force flat reduction; if unchecked, use global setting
                    useFlatReduction = Settings.DivineUseFlat ? true : Settings.UseFlatReduction;
                    break;
                case "Chaos Orb":
                    // Chaos Override: if checked, force flat reduction; if unchecked, use global setting
                    useFlatReduction = Settings.ChaosUseRatio ? true : Settings.UseFlatReduction;
                    break;
                case "Exalted Orb":
                    // Exalted Override: if checked, force flat reduction; if unchecked, use global setting
                    useFlatReduction = Settings.ExaltedUseRatio ? true : Settings.UseFlatReduction;
                    break;
                case "Orb of Annulment":
                    // Annul Override: if checked, force flat reduction; if unchecked, use global setting
                    useFlatReduction = Settings.AnnulUseFlat ? true : Settings.UseFlatReduction;
                    break;
                default:
                    // Use global setting for unknown currencies
                    useFlatReduction = Settings.UseFlatReduction;
                    break;
            }

            if (useFlatReduction)
            {
                return oldPrice - Settings.FlatReductionAmount.Value;
            }
            else
            {
                return (float)Math.Floor(oldPrice * Settings.PriceRatio.Value);
            }
        }

        private bool IsButtonPressed(RectangleF buttonRect)
        {
            var prevState = _mouseStateForRect.GetValueOrDefault(buttonRect);
            var isHovered = buttonRect.Contains(Mouse.GetCursorPosition() - GameController.Window.GetWindowRectangleTimeCache.TopLeft);
            if (!isHovered)
            {
                _mouseStateForRect[buttonRect] = null;
                return false;
            }

            var isPressed = Control.MouseButtons == MouseButtons.Left;
            _mouseStateForRect[buttonRect] = isPressed;
            return isPressed && prevState == false;
        }

        private bool IsItemLocked(Element item)
        {
            try
            {
                // Check all children of the item for locked texture
                if (item?.Children != null)
                {
                    foreach (var child in item.Children)
                    {
                        if (IsElementOrChildrenLocked(child))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                // If any error occurs, assume not locked to avoid blocking legitimate items
                return false;
            }
        }

        private bool IsElementOrChildrenLocked(Element element)
        {
            try
            {
                // Check if this element has the locked texture
                if (!string.IsNullOrEmpty(element.TextureName) && 
                    element.TextureName.Contains("LockedItems.dds"))
                {
                    return true;
                }

                // Recursively check children
                if (element?.Children != null)
                {
                    foreach (var child in element.Children)
                    {
                        if (IsElementOrChildrenLocked(child))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                // If any error occurs, assume not locked
                return false;
            }
        }

        private void RenderTimerDisplay()
        {
            if (_lastRepriceTime == DateTime.MinValue)
            {
                // No reprice yet, show ready status with ImGui formatting
                var pos = new Vector2(10, 60); // Below the main button
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(pos.X, pos.Y));
                ImGui.Begin("TimerStatus", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs);
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f)); // Green color
                ImGui.Text("Timer: READY");
                ImGui.PopStyleColor();
                ImGui.End();
                return;
            }

            var timeSinceLastReprice = DateTime.Now - _lastRepriceTime;
            var timerDuration = TimeSpan.FromMinutes(Settings.TimerDurationMinutes.Value);
            var timeRemaining = timerDuration - timeSinceLastReprice;

            if (timeRemaining <= TimeSpan.Zero)
            {
                // Timer expired
                if (!_timerExpired)
                {
                    _timerExpired = true;
                    if (Settings.EnableSoundNotification)
                    {
                        PlaySoundNotification();
                    }
                }
                
                var pos = new Vector2(10, 60);
                Graphics.DrawText("Timer: EXPIRED - Ready to reprice!", pos);
            }
            else
            {
                // Timer still running
                var pos = new Vector2(10, 60);
                var timeText = $"Timer: {timeRemaining:mm\\:ss} remaining";
                Graphics.DrawText(timeText, pos);
            }
        }

        private void PlaySoundNotification()
        {
            try
            {
                var soundPath = Path.Combine(DirectoryFullName, "sounds", "ping.mp3");
                if (File.Exists(soundPath))
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            using (var audioFile = new AudioFileReader(soundPath))
                            using (var waveOut = new WaveOutEvent())
                            {
                                waveOut.Init(audioFile);
                                waveOut.Play();
                                while (waveOut.PlaybackState == PlaybackState.Playing)
                                {
                                    System.Threading.Thread.Sleep(100);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"Failed to play sound: {ex.Message}");
                        }
                    });
                }
                else
                {
                    LogError($"Sound file not found: {soundPath}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error playing sound notification: {ex.Message}");
            }
        }

        private void InitializeDefaultCurrencyRates()
        {
            lock (_currencyRatesLock)
            {
                // Initialize with -1 to indicate rates need to be loaded from API
                // These will be updated when UpdateCurrencyRates() is called
                _currencyRates["chaos_to_divine"] = -1m;
                _currencyRates["chaos_to_exalted"] = -1m;
                _currencyRates["divine_to_chaos"] = -1m;
                _currencyRates["divine_to_exalted"] = -1m;
                _currencyRates["exalted_to_chaos"] = -1m;
                _currencyRates["exalted_to_divine"] = -1m;
                _currencyRates["annul_to_chaos"] = -1m;
                _currencyRates["annul_to_divine"] = -1m;
                _currencyRates["annul_to_exalted"] = -1m;
            }
        }

        private async Task UpdateCurrencyRates()
        {
            if (!Settings.AutoUpdateRates) return;
            
            var timeSinceUpdate = DateTime.Now - _lastCurrencyUpdate;
            if (timeSinceUpdate.TotalMinutes < Settings.CurrencyUpdateInterval.Value) return;

            try
            {
                // Try API first, fallback to local file
                JsonDocument jsonDoc;
                try
                {
                    var response = await _httpClient.GetStringAsync("https://poe.ninja/poe2/api/economy/temp/overview?leagueName=Rise+of+the+Abyssal&overviewName=Currency");
                    jsonDoc = JsonDocument.Parse(response);
                }
                catch
                {
                    // Fallback to local poeninja.json file
                    var localJson = await File.ReadAllTextAsync("poeninja.json");
                    jsonDoc = JsonDocument.Parse(localJson);
                }
                
                lock (_currencyRatesLock)
                {
                    foreach (var item in jsonDoc.RootElement.GetProperty("items").EnumerateArray())
                    {
                        var itemData = item.GetProperty("item");
                        var rates = item.GetProperty("rate");
                        
                        string itemId = itemData.GetProperty("id").GetString();
                        
                        // Note: poeninja rates show "how many of this currency = 1 divine/chaos/exalted"
                        // So we need to invert them to get "how many divine/chaos/exalted = 1 of this currency"
                        
                        if (rates.TryGetProperty("chaos", out var chaosRate) && chaosRate.GetDecimal() > 0)
                        {
                            _currencyRates[$"{itemId}_to_chaos"] = 1m / chaosRate.GetDecimal();
                        }
                        if (rates.TryGetProperty("divine", out var divineRate) && divineRate.GetDecimal() > 0)
                        {
                            _currencyRates[$"{itemId}_to_divine"] = 1m / divineRate.GetDecimal();
                        }
                        if (rates.TryGetProperty("exalted", out var exaltedRate) && exaltedRate.GetDecimal() > 0)
                        {
                            _currencyRates[$"{itemId}_to_exalted"] = 1m / exaltedRate.GetDecimal();
                        }
                    }
                }
                
                _lastCurrencyUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                LogError($"Failed to update currency rates: {ex.Message}");
            }
        }

        private void RenderValueDisplay()
        {
            var merchantPanel = GameController.IngameState.IngameUi.OfflineMerchantPanel;
            if (merchantPanel?.IsVisible != true) 
            {
                return;
            }

            // Update currency rates in background
            _ = Task.Run(UpdateCurrencyRates);

            var inventory = merchantPanel.AllInventories?.FirstOrDefault();
            if (inventory?.VisibleInventoryItems == null) 
            {
                return;
            }

            var itemValues = CalculateItemValues(inventory.VisibleInventoryItems);
            
            var pos = new Vector2(Settings.ValueDisplayX.Value, Settings.ValueDisplayY.Value);
            
            // Create value display text
            var totalItemsInTab = inventory?.VisibleInventoryItems?.Count() ?? 0;
            var displayText = $"Items in tab: {itemValues.ItemsWithPricing}/{totalItemsInTab}\n";
            displayText += $"Items for sale: {itemValues.TotalItems}\n";
            
            if (itemValues.ChaosTotal > 0)
                displayText += $"Chaos: {itemValues.ChaosTotal:F0}\n";
            if (itemValues.DivineTotal > 0)
                displayText += $"Divines: {itemValues.DivineTotal:F1}\n";
            if (itemValues.ExaltedTotal > 0)
                displayText += $"Exalts: {itemValues.ExaltedTotal:F1}\n";
            if (itemValues.AnnulTotal > 0)
                displayText += $"Annuls: {itemValues.AnnulTotal:F0}\n";
            
            displayText += $"\nTotal in Divine: {itemValues.TotalInDivine:F1}\n";
            displayText += $"Total in Exalts: {itemValues.TotalInExalted:F1}";
            
            // Warning if tooltip count doesn't match total items
            if (inventory?.VisibleInventoryItems != null)
            {
                var itemsWithTooltips = inventory.VisibleInventoryItems.Where(i => i.Tooltip != null).Count();
                var totalItems = inventory.VisibleInventoryItems.Count();
                
                if (itemsWithTooltips < totalItems)
                {
                    displayText += $"\n⚠️ Hover over items to load pricing data!";
                }
            }

            // Draw black background
            var textSize = Graphics.MeasureText(displayText);
            var backgroundRect = new RectangleF(pos.X - 5, pos.Y - 5, textSize.X + 10, textSize.Y + 10);
            Graphics.DrawBox(backgroundRect, Color.FromArgb(180, 0, 0, 0));

            Graphics.DrawText(displayText, pos);
        }

        private ItemValueSummary CalculateItemValues(IEnumerable<Element> items)
        {
            var summary = new ItemValueSummary();
            var totalItemsProcessed = 0;
            var itemsWithTooltips = 0;
            var itemsWithPricing = 0;
            
            try
            {
                foreach (var item in items)
                {
                    totalItemsProcessed++;
                    
                    try
                    {
                        // Check if item is locked before processing
                        if (IsItemLocked(item))
                        {
                            continue;
                        }

                        // Check if item has tooltip (more important than children)
                        var tooltip = item.Tooltip;
                        if (tooltip == null) 
                        {
                            continue;
                        }
                        
                        // Count any item with a tooltip, regardless of structure
                        itemsWithTooltips++;
                        
                        if (tooltip.Children == null || tooltip.Children.Count == 0) 
                        {
                            continue;
                        }
                        
                        // Try to find price information in the tooltip structure
                        string priceText = null;
                        string orbType = null;
                        
                        // First try the specific structure you described: Tooltip.Children[0].Children[1].Children[last].Children[1]
                        if (tooltip.Children?.Count > 0)
                        {
                            var child0 = tooltip.Children[0];
                            if (child0?.Children?.Count > 1)
                            {
                                var child1 = child0.Children[1];
                                if (child1?.Children?.Count > 0)
                                {
                                    var lastChild = child1.Children.Last();
                                    if (lastChild?.Children?.Count > 1)
                                    {
                                        var priceChild = lastChild.Children[1];
                                        if (priceChild?.Children?.Count > 2)
                                        {
                                            priceText = priceChild.Children[0]?.Text; // "279x"
                                            orbType = priceChild.Children[2]?.Text;   // "Exalted Orb"
                                            
                                            if (!string.IsNullOrEmpty(priceText) && !string.IsNullOrEmpty(orbType))
                                            {
                                                // Found price using specific structure
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        // If specific structure didn't work, try the general search
                        if (string.IsNullOrEmpty(priceText) || string.IsNullOrEmpty(orbType))
                        {
                            foreach (var tooltipChild in tooltip.Children)
                            {
                                if (tooltipChild != null && FindPriceInChildren(tooltipChild, out priceText, out orbType))
                                {
                                    break;
                                }
                            }
                        }
                        
                        // If we didn't find pricing in the main tooltip structure, try a more aggressive search
                        if (string.IsNullOrEmpty(priceText) || string.IsNullOrEmpty(orbType))
                        {
                            // Try searching through all text in the tooltip recursively
                            if (FindPriceInAllText(tooltip, out priceText, out orbType))
                            {
                                // Found pricing in text search
                            }
                            else
                            {
                                continue;
                            }
                        }
                        
                        if (!priceText.EndsWith("x"))
                        {
                            continue;
                        }
                        
                        string priceStr = priceText.Replace("x", "").Trim();
                        if (!int.TryParse(priceStr, out int price)) 
                        {
                            continue;
                        }
                        
                        itemsWithPricing++;
                        summary.TotalItems++;
                
                        lock (_currencyRatesLock)
                        {
                            switch (orbType)
                            {
                                case "Chaos Orb":
                                    summary.ChaosTotal += price;
                                    summary.TotalInDivine += price * GetRate("chaos_to_divine");
                                    summary.TotalInExalted += price * GetRate("chaos_to_exalted");
                                    break;
                                case "Divine Orb":
                                    summary.DivineTotal += price;
                                    summary.TotalInDivine += price;
                                    summary.TotalInExalted += price * GetRate("divine_to_exalted");
                                    break;
                                case "Exalted Orb":
                                    summary.ExaltedTotal += price;
                                    summary.TotalInDivine += price * GetRate("exalted_to_divine");
                                    summary.TotalInExalted += price;
                                    break;
                                case "Orb of Annulment":
                                    summary.AnnulTotal += price;
                                    summary.TotalInDivine += price * GetRate("annul_to_divine");
                                    summary.TotalInExalted += price * GetRate("annul_to_exalted");
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        // Skip this item if any error occurs
                        continue;
                    }
                }
            }
            catch
            {
                // If any major error occurs, return empty summary
                return summary;
            }
            
            // Set the processing stats
            summary.TotalItemsProcessed = totalItemsProcessed;
            summary.ItemsWithTooltips = itemsWithTooltips;
            summary.ItemsWithPricing = itemsWithPricing;
            
            return summary;
        }

        private bool FindPriceInChildren(Element element, out string priceText, out string orbType)
        {
            return FindPriceInChildren(element, out priceText, out orbType, 0);
        }

        private bool FindPriceInAllText(Element element, out string priceText, out string orbType)
        {
            return FindPriceInAllText(element, out priceText, out orbType, 0);
        }

        private bool FindPriceInAllText(Element element, out string priceText, out string orbType, int depth)
        {
            priceText = null;
            orbType = null;
            
            // Prevent stack overflow by limiting recursion depth
            if (depth > 5 || element == null) return false;
            
            if (element.Text != null)
            {
                var text = element.Text.Trim();
                
                // Look for patterns like "5x" or "10x" followed by currency type
                var priceMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)x\s*([A-Za-z\s]+)");
                if (priceMatch.Success)
                {
                    priceText = priceMatch.Groups[1].Value + "x";
                    orbType = priceMatch.Groups[2].Value.Trim();
                    return true;
                }
                
                // Look for patterns like "5 x" (with space)
                priceMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*x\s*([A-Za-z\s]+)");
                if (priceMatch.Success)
                {
                    priceText = priceMatch.Groups[1].Value + "x";
                    orbType = priceMatch.Groups[2].Value.Trim();
                    return true;
                }
            }
            
            if (element.Children != null)
            {
                foreach (var child in element.Children)
                {
                    if (FindPriceInAllText(child, out priceText, out orbType, depth + 1))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private string GetAllTextFromElement(Element element)
        {
            return GetAllTextFromElement(element, 0);
        }

        private string GetAllTextFromElement(Element element, int depth)
        {
            // Prevent stack overflow by limiting recursion depth
            if (depth > 5 || element == null) return string.Empty;
            
            if (element.Text != null)
            {
                return element.Text;
            }
            
            if (element.Children != null)
            {
                var allText = new System.Text.StringBuilder();
                foreach (var child in element.Children)
                {
                    var childText = GetAllTextFromElement(child, depth + 1);
                    if (!string.IsNullOrEmpty(childText))
                    {
                        if (allText.Length > 0) allText.Append(" ");
                        allText.Append(childText);
                    }
                }
                return allText.ToString();
            }
            
            return string.Empty;
        }

        private bool FindPriceInChildren(Element element, out string priceText, out string orbType, int depth)
        {
            priceText = null;
            orbType = null;
            
            // Prevent stack overflow by limiting recursion depth more aggressively
            if (depth > 5 || element?.Children == null) return false;
            
            try
            {
                foreach (var child in element.Children)
                {
                    if (child?.Text != null)
                    {
                        // Look for text that ends with 'x' (price indicator)
                        if (child.Text.EndsWith("x"))
                        {
                            priceText = child.Text;
                            
                            // Look for currency type in sibling elements
                            if (element.Children.Count > 1)
                            {
                                for (int i = 0; i < element.Children.Count; i++)
                                {
                                    var sibling = element.Children[i];
                                    if (sibling?.Text != null && !sibling.Text.EndsWith("x") && 
                                        (sibling.Text.Contains("Orb") || sibling.Text.Contains("Divine") || 
                                         sibling.Text.Contains("Chaos") || sibling.Text.Contains("Exalted")))
                                    {
                                        orbType = sibling.Text;
                                        return true;
                                    }
                                }
                            }
                            
                            // If no sibling found, try parent's siblings (but limit depth more strictly)
                            if (depth < 3 && element.Parent?.Children != null)
                            {
                                foreach (var parentSibling in element.Parent.Children)
                                {
                                    if (parentSibling?.Text != null && !parentSibling.Text.EndsWith("x") && 
                                        (parentSibling.Text.Contains("Orb") || parentSibling.Text.Contains("Divine") || 
                                         parentSibling.Text.Contains("Chaos") || parentSibling.Text.Contains("Exalted")))
                                    {
                                        orbType = parentSibling.Text;
                                        return true;
                                    }
                                }
                            }
                            
                            return true; // Found price text, even without currency type
                        }
                    }
                    
                    // Recursively search in children with increased depth
                    if (FindPriceInChildren(child, out priceText, out orbType, depth + 1))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If any exception occurs, just return false to prevent crashes
                return false;
            }
            
            return false;
        }

        private decimal GetRate(string rateKey)
        {
            lock (_currencyRatesLock)
            {
                if (_currencyRates.TryGetValue(rateKey, out var rate))
                {
                    if (rate == -1m)
                    {
                        return 0m;
                    }
                    return rate;
                }
                return 0m;
            }
        }

        public override void Dispose()
        {
            _waveOut?.Dispose();
            _httpClient?.Dispose();
            base.Dispose();
        }
    }

    public class ItemValueSummary
    {
        public int TotalItems { get; set; }
        public int TotalItemsProcessed { get; set; }
        public int ItemsWithTooltips { get; set; }
        public int ItemsWithPricing { get; set; }
        public decimal ChaosTotal { get; set; }
        public decimal DivineTotal { get; set; }
        public decimal ExaltedTotal { get; set; }
        public decimal AnnulTotal { get; set; }
        public decimal TotalInDivine { get; set; }
        public decimal TotalInExalted { get; set; }
    }
}