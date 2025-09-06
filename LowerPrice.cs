using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

namespace LowerPrice
{
    public class LowerPrice : BaseSettingsPlugin<Settings>
    {
        private readonly ConcurrentDictionary<RectangleF, bool?> _mouseStateForRect = new();
        private readonly Random random = new Random();

        private bool MoveCancellationRequested => Settings.CancelWithRightClick && (Control.MouseButtons & MouseButtons.Right) != 0;

        public override bool Initialise()
        {
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\pick.png").Replace('\\', '/'), false);
            return true;
        }

        public override void Render()
        {
            if (!Settings.Enable) return;

            var merchantPanel = GameController.IngameState.IngameUi.OfflineMerchantPanel;
            if (merchantPanel != null && merchantPanel.IsVisible)
            {
                const float buttonSize = 37;
                var offset = new Vector2(10, 10);
                var buttonPos = GameController.Window.GetWindowRectangleTimeCache.TopLeft + offset;
                var buttonRect = new RectangleF(buttonPos.X, buttonPos.Y, buttonSize, buttonSize);
                Graphics.DrawImage("pick.png", buttonRect);

                if (IsButtonPressed(buttonRect))
                {
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
            var inventory = GameController.IngameState.IngameUi.OfflineMerchantPanel.AllInventories[0];
            if (inventory == null || !inventory.VisibleInventoryItems.Any()) return;

            foreach (var item in inventory.VisibleInventoryItems)
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

                                                if (!reprice) continue;

                                                float newPrice = (float)Math.Floor(oldPrice * Settings.PriceRatio.Value);
                                                if (oldPrice == 1 || newPrice <= 1)
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
    }
}