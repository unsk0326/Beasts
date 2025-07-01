using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Beasts.Data;
using Beasts.ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Beasts;

public partial class Beasts
{
    public override void Render()
    {
        DrawInGameBeasts();
        DrawBestiaryPanel();
        DrawBeastsWindow();
    }

    private static RectangleF Get64DirectionsUV(double phi, double distance, int rows)
    {
        phi += Math.PI * 0.25; // fix rotation due to projection
        if (phi > 2 * Math.PI) phi -= 2 * Math.PI;

        var xSprite = (float)Math.Round(phi / Math.PI * 32);
        if (xSprite >= 64) xSprite = 0;

        float ySprite = distance > 60 ? distance > 120 ? 2 : 1 : 0;
        var x = xSprite / 64;
        float y = 0;
        if (rows > 0)
        {
            y = ySprite / rows;
            return new RectangleF(x, y, (xSprite + 1) / 64 - x, (ySprite + 1) / rows - y);
        }

        return new RectangleF(x, y, (xSprite + 1) / 64 - x, 1);
    }
    private void DrawInGameBeasts()
    {
        var scale = 1.6f;
        var height = ImGui.GetTextLineHeight() * scale;
        var margin = height / scale / 4;
        var lines = 0;
        var origin = (GameController.Window.GetWindowRectangleTimeCache with { Location = SharpDX.Vector2.Zero }).Center
            .Translate(0 - 96, 0);

        foreach (var trackedBeast in _trackedBeasts
                     .Select(beast => new { Positioned = beast.Value.GetComponent<Positioned>(), beast.Value.Metadata })
                     .Where(beast => beast.Positioned != null))
        {
            var beast = BeastsDatabase.AllBeasts.Where(beast => trackedBeast.Metadata == beast.Path).First();

            if (!Settings.Beasts.Any(b => b.Path == beast.Path)) continue;
            var pos = GameController.IngameState.Data.ToWorldWithTerrainHeight(trackedBeast.Positioned.GridPosition);
            Graphics.DrawText(beast.DisplayName, GameController.IngameState.Camera.WorldToScreen(pos), Color.DarkRed,
                FontAlign.Center);

            DrawFilledCircleInWorldPosition(pos, 300, GetSpecialBeastColor(beast.DisplayName));

//seong.lee mod
            var delta = trackedBeast.Positioned.GridPos - GameController.Player.GridPos;
            var distance = delta.GetPolarCoordinates(out var phi);
            var rectDirection = new RectangleF(origin.X - margin - height / 2,
                        origin.Y - margin / 2 - height - lines * height, height, height);
            var rectUV = Get64DirectionsUV(phi, distance, 3);
            lines++;
            Graphics.DrawText(beast.DisplayName, new Vector2(origin.X + height / 2, origin.Y - lines * height), GetSpecialBeastColor(beast.DisplayName));
            Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, GetSpecialBeastColor(beast.DisplayName));
        }
    }

    private Color GetSpecialBeastColor(string beastName)
    {
        if (beastName.Contains("Vivid"))
        {
            return new Color(255, 250, 0);
        }

        if (beastName.Contains("Wild"))
        {
            return new Color(255, 0, 235);
        }

        if (beastName.Contains("Primal"))
        {
            return new Color(0, 245, 255);
        }

        if (beastName.Contains("Black"))
        {
            return new Color(255, 255, 255);
        }

        return Color.Red;
    }

    private void DrawBestiaryPanel()
    {
        var bestiary = GameController.IngameState.IngameUi.GetBestiaryPanel();
        if (bestiary == null || bestiary.IsVisible == false) return;

        var capturedBeastsPanel = bestiary.CapturedBeastsPanel;
        if (capturedBeastsPanel == null || capturedBeastsPanel.IsVisible == false) return;

        var beasts = bestiary.CapturedBeastsPanel.CapturedBeasts;
        foreach (var beast in beasts)
        {
            var beastMetadata = Settings.Beasts.Find(b => b.DisplayName == beast.DisplayName);
            if (beastMetadata == null) continue;
            if (!Settings.BeastPrices.ContainsKey(beastMetadata.DisplayName)) continue;

            var center = new Vector2(beast.GetClientRect().Center.X, beast.GetClientRect().Center.Y);

            Graphics.DrawBox(beast.GetClientRect(), new Color(0, 0, 0, 0.5f));
            Graphics.DrawFrame(beast.GetClientRect(), Color.White, 2);
            Graphics.DrawText(beastMetadata.DisplayName, center, Color.Orange, FontAlign.Center);

            var text = Settings.BeastPrices[beastMetadata.DisplayName].ToString(CultureInfo.InvariantCulture) + "c";
            var textPos = center + new Vector2(0, 20);
            Graphics.DrawText(text, textPos, Color.Orange, FontAlign.Center);

        }
    }

    private void DrawBeastsWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(0, 0));
        ImGui.SetNextWindowBgAlpha(0.6f);
        ImGui.Begin("Beasts Window", ImGuiWindowFlags.NoDecoration);

        if (ImGui.BeginTable("Beasts Table", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV))
        {
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Beast");

            foreach (var beastMetadata in _trackedBeasts
                         .Select(trackedBeast => trackedBeast.Value)
                         .Select(beast => Settings.Beasts.Find(b => b.Path == beast.Metadata))
                         .Where(beastMetadata => beastMetadata != null))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                ImGui.Text(Settings.BeastPrices.TryGetValue(beastMetadata.DisplayName, out var price)
                    ? $"{price.ToString(CultureInfo.InvariantCulture)}c"
                    : "0c");

                ImGui.TableNextColumn();

                ImGui.Text(beastMetadata.DisplayName);
                foreach (var craft in beastMetadata.Crafts)
                {
                    ImGui.Text(craft);
                }
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }

    private void DrawFilledCircleInWorldPosition(Vector3 position, float radius, Color color)
    {
        var circlePoints = new List<Vector2>();
        const int segments = 15;
        const float segmentAngle = 2f * MathF.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var nextOffset = new Vector2(MathF.Cos(angle + segmentAngle), MathF.Sin(angle + segmentAngle)) * radius;

            var currentWorldPos = position + new Vector3(currentOffset, 0);
            var nextWorldPos = position + new Vector3(nextOffset, 0);

            circlePoints.Add(GameController.Game.IngameState.Camera.WorldToScreen(currentWorldPos));
            circlePoints.Add(GameController.Game.IngameState.Camera.WorldToScreen(nextWorldPos));
        }

        Graphics.DrawConvexPolyFilled(circlePoints.ToArray(),
            color with { A = Color.ToByte((int)((double)0.2f * byte.MaxValue)) });
        Graphics.DrawPolyLine(circlePoints.ToArray(), color, 2);
    }
}