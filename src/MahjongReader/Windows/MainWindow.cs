using GameModel;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;

namespace MahjongReader.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private IPluginLog PluginLog;

    private List<ObservedTile> internalObservedTiles;

    public List<ObservedTile> ObservedTiles
    {
        get
        {
            return internalObservedTiles;
        }
        set
        {
            internalObservedTiles = value;
        }
    }

    private Dictionary<string, int> internalRemainingMap;

    public Dictionary<string, int> RemainingMap
    {
        get
        {
            return internalRemainingMap;
        }
        set
        {
            internalRemainingMap = value;
        }
    }

    private Dictionary<string, int> internalSuitCounts;

    public Dictionary<string, int> SuitCounts
    {
        get
        {
            return internalSuitCounts;
        }
        set
        {
            internalSuitCounts = value;
        }
    }

    private Dictionary<string, ISharedImmediateTexture> mjaiNotationToTexture;
    private Dictionary<string, ISharedImmediateTexture> suitToTexture;

    public MainWindow(Plugin plugin, IPluginLog pluginLog) : base(
        "Mahjong Reader", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 850),  // Increased to accommodate larger tiles
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
        this.PluginLog = pluginLog;
        internalObservedTiles = new List<ObservedTile>();
        
        // Initialize remaining map with default values (4 for each tile, 3 for red five)
        internalRemainingMap = new Dictionary<string, int>();
        // Man (Characters) tiles
        for (int i = 1; i <= 9; i++) {
            internalRemainingMap[$"{i}m"] = i == 5 ? 3 : 4;
        }
        internalRemainingMap["0m"] = 1;  // Red five

        // Pin (Dots) tiles
        for (int i = 1; i <= 9; i++) {
            internalRemainingMap[$"{i}p"] = i == 5 ? 3 : 4;
        }
        internalRemainingMap["0p"] = 1;  // Red five

        // Sou (Bamboo) tiles
        for (int i = 1; i <= 9; i++) {
            internalRemainingMap[$"{i}s"] = i == 5 ? 3 : 4;
        }
        internalRemainingMap["0s"] = 1;  // Red five

        // Honor tiles (no red five)
        for (int i = 1; i <= 7; i++) {
            internalRemainingMap[$"{i}z"] = 4;
        }

        // Initialize suit counts
        internalSuitCounts = new Dictionary<string, int>
        {
            ["m"] = 36,  // 4 * 9 tiles (including one red five)
            ["p"] = 36,  // 4 * 9 tiles (including one red five)
            ["s"] = 36   // 4 * 9 tiles (including one red five)
        };

        mjaiNotationToTexture = new();
        // setup textures
        PluginLog.Debug($"Starting texture loading...");
        foreach (var notationToTextureId in TileTextureUtilities.NotationToTextureId) {
            var textureId = notationToTextureId.Value;
            var texturePath = $"ui/icon/076000/{textureId}.tex";
            PluginLog.Debug($"Loading texture: {texturePath} for notation {notationToTextureId.Key}");
            var texture = Plugin.TextureProvider.GetFromGame(texturePath);
            if (texture == null) {
                // Try the old path as fallback
                texturePath = $"ui/icon/076000/{textureId}_hr1.tex";
                PluginLog.Debug($"Retrying with old path: {texturePath}");
                texture = Plugin.TextureProvider.GetFromGame(texturePath);
                if (texture == null) {
                    PluginLog.Error($"Failed to load texture for notation {notationToTextureId.Key} with texture ID {textureId} at paths {texturePath}");
                    continue;
                }
            }
            try {
                mjaiNotationToTexture.Add(notationToTextureId.Key, texture);
                PluginLog.Debug($"Successfully loaded texture for {notationToTextureId.Key}");
            } catch (ArgumentException) {
                PluginLog.Error($"Duplicate notation key found: {notationToTextureId.Key}");
            }
        }

        suitToTexture = new();
        // maybe one day we'll support traditional properly
        var manTexturePath = $"ui/icon/076000/076041.tex";  // First man tile
        var pinTexturePath = $"ui/icon/076000/076050.tex";  // First pin tile
        var souTexturePath = $"ui/icon/076000/076059.tex";  // First sou tile

        PluginLog.Debug($"Loading suit textures from: {manTexturePath}, {pinTexturePath}, {souTexturePath}");
        var manTexture = Plugin.TextureProvider.GetFromGame(manTexturePath);
        var pinTexture = Plugin.TextureProvider.GetFromGame(pinTexturePath);
        var souTexture = Plugin.TextureProvider.GetFromGame(souTexturePath);

        // Try old paths if new paths fail
        if (manTexture == null) {
            manTexturePath = $"ui/icon/076000/076041_hr1.tex";
            manTexture = Plugin.TextureProvider.GetFromGame(manTexturePath);
        }
        if (pinTexture == null) {
            pinTexturePath = $"ui/icon/076000/076050_hr1.tex";
            pinTexture = Plugin.TextureProvider.GetFromGame(pinTexturePath);
        }
        if (souTexture == null) {
            souTexturePath = $"ui/icon/076000/076059_hr1.tex";
            souTexture = Plugin.TextureProvider.GetFromGame(souTexturePath);
        }

        if (manTexture != null) {
            suitToTexture.Add(Suit.MAN, manTexture);
            PluginLog.Debug("Loaded man suit texture");
        } else {
            PluginLog.Error($"Failed to load man suit texture from {manTexturePath}");
        }
        if (pinTexture != null) {
            suitToTexture.Add(Suit.PIN, pinTexture);
            PluginLog.Debug("Loaded pin suit texture");
        } else {
            PluginLog.Error($"Failed to load pin suit texture from {pinTexturePath}");
        }
        if (souTexture != null) {
            suitToTexture.Add(Suit.SOU, souTexture);
            PluginLog.Debug("Loaded sou suit texture");
        } else {
            PluginLog.Error($"Failed to load sou suit texture from {souTexturePath}");
        }
    }

    public void Dispose() { }

    private void DrawTileRemaining(string suit, int number, bool isDora) {
        var notation = $"{number}{suit}";
        
        // First check if the notation exists in the remaining map
        if (!internalRemainingMap.ContainsKey(notation)) {
            PluginLog.Error($"Missing count for notation: {notation}");
            ImGui.TableNextColumn();
            ImGui.Text($"[Missing Count]");
            return;
        }

        var count = isDora ? internalRemainingMap[notation] + internalRemainingMap[$"0{suit}"] : internalRemainingMap[notation];
        var isDoraRemaing = isDora ? internalRemainingMap[$"0{suit}"] > 0 : false;
        
        if (!mjaiNotationToTexture.ContainsKey(notation)) {
            PluginLog.Error($"Missing texture for notation: {notation}");
            ImGui.TableNextColumn();
            ImGui.Text($"[Missing Texture] x{count}");
            return;
        }
        
        var texture = mjaiNotationToTexture[notation].GetWrapOrEmpty();
        var desiredSize = new Vector2(55, 72); // Increased size for better visibility
        ImGui.TableNextColumn();
        ImGui.Image(texture.ImGuiHandle, desiredSize);
        ImGui.SameLine();
        if (isDoraRemaing) {
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"x{count}");
        } else {
            ImGui.Text($"x{count}");
        }
    }

    private void DrawSuitRemaining(string suit) {
        if (!internalSuitCounts.ContainsKey(suit)) {
            PluginLog.Error($"Missing count for suit: {suit}");
            ImGui.TableNextColumn();
            ImGui.Text("[Missing Count]");
            return;
        }

        if (!suitToTexture.ContainsKey(suit)) {
            PluginLog.Error($"Missing texture for suit: {suit}");
            ImGui.TableNextColumn();
            ImGui.Text($"[Missing Texture] x{internalSuitCounts[suit]}");
            return;
        }

        var count = internalSuitCounts[suit];
        var texture = suitToTexture[suit].GetWrapOrEmpty();
        var desiredSize = new Vector2(55, 72); // Increased size to match tile size
        ImGui.TableNextColumn();
        ImGui.Image(texture.ImGuiHandle, desiredSize);
        ImGui.SameLine();
        ImGui.Text($"x{count}");
    }

    public override void Draw()
    {
        ImGui.BeginTable("##Tiles", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit);
        for (var i = 1; i < 10; i++) {
            ImGui.TableNextRow();
            bool isDora = i == 5;

            DrawTileRemaining(Suit.MAN, i, isDora);
            DrawTileRemaining(Suit.PIN, i, isDora);
            DrawTileRemaining(Suit.SOU, i, isDora);

            if (i == 3) {
                DrawSuitRemaining(Suit.MAN);
            } else if (i == 5) {
                DrawSuitRemaining(Suit.PIN);
            } else if (i == 7) {
                DrawSuitRemaining(Suit.SOU);
            } else {
                ImGui.TableNextColumn();
            }
        }
        ImGui.EndTable();

        ImGui.Dummy(new Vector2(0, 40));

        ImGui.BeginTable("#TilesWind", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit);
        ImGui.TableNextRow();
        for (var i = 1; i < 5; i++) {
            DrawTileRemaining(Suit.HONOR, i, false);
        }
        ImGui.EndTable();

        ImGui.BeginTable("#TilesDragon", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit);
        ImGui.TableNextRow();
        ImGui.TableNextRow();
        for (var i = 5; i < 8; i++) {
            DrawTileRemaining(Suit.HONOR, i, false);
        }
        ImGui.EndTable();
    }
}
