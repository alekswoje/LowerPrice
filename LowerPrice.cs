using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

namespace LowerPrice;

public class LowerPrice : BaseSettingsPlugin<Settings>
{
    private readonly List<(NormalInventoryItem item, float oldPrice, float newPrice, Vector2 position)> _itemsToUpdate = new();

    public override bool Initialise()
    {
        return true;
    }

    public override void Render()
    {
        if (!Settings.Enable) return;

        var shopTab = GameController.IngameState.IngameUi.Children.ElementAtOrDefault(35);
        if (shopTab != null && shopTab.IsVisible)
        {
            DebugWindow.LogMsg("Shop tab is open.");
            if (Input.IsKeyDown(Settings.DiscountHotkey.Value))
            {
                ProcessItems(shopTab);
            }
        }
        else
        {
            DebugWindow.LogMsg("Shop tab is not open.");
        }
    }

    private void ProcessItems(Element shopTab)
    {
        _itemsToUpdate.Clear();
        var stash = GameController.IngameState.IngameUi.StashElement.VisibleStash;
        if (stash == null) return;

        foreach (var item in stash.VisibleInventoryItems)
        {
            var mods = item.Item?.GetComponent<Mods>();
            var price = mods?.ItemMods.FirstOrDefault(m => m.Name.Contains("Price"))?.Values.FirstOrDefault() ?? 0f;
            if (price > 0)
            {
                var newPrice = (float)Math.Floor(price * Settings.PriceRatio.Value);
                var position = item.GetClientRectCache.Center;
                _itemsToUpdate.Add((item, price, newPrice, position));
            }
        }

        if (_itemsToUpdate.Any())
        {
            UpdateItemPrices();
        }
    }

    private async void UpdateItemPrices()
    {
        foreach (var (item, oldPrice, newPrice, position) in _itemsToUpdate)
        {
            if (!GameController.IngameState.IngameUi.Children.ElementAtOrDefault(35).IsVisible)
            {
                DebugWindow.LogMsg("Shop tab closed, aborting price update.");
                break;
            }

            Mouse.moveMouse(position + GameController.Window.GetWindowRectangleTimeCache.TopLeft);
            await TaskUtils.NextFrame();
            Mouse.LeftDown(); 
            await TaskUtils.NextFrame();
            Mouse.LeftUp();   
            await TaskUtils.NextFrame();
            Keyboard.Type($"{newPrice}");
            await TaskUtils.NextFrame();
            Keyboard.KeyPress(Keys.Enter);
            await TaskUtils.NextFrame();
        }
    }
}