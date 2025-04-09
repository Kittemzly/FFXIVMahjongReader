using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using GameModel;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using MahjongReader.Windows;
using Dalamud.Interface.Utility;

namespace MahjongReader
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Mahjong Reader";
        private const string CommandName = "/mahjong";

        private readonly IDalamudPluginInterface pluginInterface;
        private readonly ICommandManager commandManager;
        private readonly IGameGui gameGui;
        private readonly IPluginLog pluginLog;
        private readonly IAddonLifecycle addonLifecycle;
        public readonly ITextureProvider TextureProvider;

        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("MahjongReader");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        private ImportantPointers ImportantPointers { get; init; }
        private NodeCrawlerUtils NodeCrawlerUtils { get; init; }
        private Task WindowUpdateTask { get; set; } = null!;
        private YakuDetector YakuDetector { get; init; }

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IGameGui gameGui,
            IPluginLog pluginLog,
            IAddonLifecycle addonLifecycle,
            ITextureProvider textureProvider)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.gameGui = gameGui;
            this.pluginLog = pluginLog;
            this.addonLifecycle = addonLifecycle;
            this.TextureProvider = textureProvider;

            this.Configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.pluginInterface);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, pluginLog);
            
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            this.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Tracks observed Mahjong tiles and available Yaku"
            });

            this.pluginInterface.UiBuilder.Draw += DrawUI;
            this.pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            this.pluginInterface.UiBuilder.OpenMainUi += () => MainWindow.IsOpen = true;

            addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Emj", OnAddonPostSetup);
            ImportantPointers = new ImportantPointers(pluginLog);
            NodeCrawlerUtils = new NodeCrawlerUtils(pluginLog);
            YakuDetector = new YakuDetector();
        }

        public void Dispose()
        {
            addonLifecycle.UnregisterListener(OnAddonPostSetup);
            addonLifecycle.UnregisterListener(OnAddonPostRefresh);
            WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            
            commandManager.RemoveHandler(CommandName);
        }

        private unsafe void OnAddonPostSetup(AddonEvent type, AddonArgs args) {
            var addonPtr = args.Addon;
            if (addonPtr == IntPtr.Zero) {
                pluginLog.Info("Could not find Emj");
                return;
            }
            addonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Emj", OnAddonPostRefresh);
            addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Emj", OnAddonPreFinalize);

            MainWindow.IsOpen = true;
            var addon = (AtkUnitBase*)addonPtr;
            var rootNode = addon->RootNode;
            ImportantPointers.WipePointers();
            NodeCrawlerUtils.TraverseAllAtkResNodes(rootNode, (intPtr) => ImportantPointers.MaybeTrackPointer(intPtr));
        }

        private void OnAddonPreFinalize(AddonEvent type, AddonArgs args) {
            MainWindow.IsOpen = false;
        }

        private void OnAddonPostRefresh(AddonEvent type, AddonArgs args) {
            var addonPtr = args.Addon;
            if (addonPtr == IntPtr.Zero) {
                pluginLog.Info("Could not find Emj");
                return;
            }

            if (WindowUpdateTask == null || WindowUpdateTask.IsCompleted || WindowUpdateTask.IsFaulted || WindowUpdateTask.IsCanceled) {
                pluginLog.Info("Running window updater");
                WindowUpdateTask = Task.Run(WindowUpdater);
            }
        }

        private unsafe void WindowUpdater() {
#if DEBUG
    Stopwatch stopwatch = new Stopwatch();
    stopwatch.Start();
#endif

            var observedTiles = GetObservedTiles();
            pluginLog.Info($"tiles count: {observedTiles.Count}");
            var remainingMap = TileTextureUtilities.TileCountTracker.RemainingFromObserved(observedTiles);
            var suitCounts = new Dictionary<string, int>();
            foreach (var kvp in remainingMap) { // imagine testing
                var suit = kvp.Key.Substring(1, 1);
                if (suit == Suit.HONOR) {
                    continue;
                }
                
                if (suitCounts.ContainsKey(suit)) {
                    suitCounts[suit] += kvp.Value;
                } else {
                    suitCounts.Add(suit, kvp.Value);
                }
            }

            MainWindow.ObservedTiles = observedTiles;
            MainWindow.RemainingMap = remainingMap;
            MainWindow.SuitCounts = suitCounts;
#if DEBUG
    stopwatch.Stop();
    TimeSpan elapsedTime = stopwatch.Elapsed;
    pluginLog.Info($"QQQQQQQ - Elapsed time: {elapsedTime.TotalMilliseconds} ms");
#endif
        }

        private unsafe void OnCommand(string command, string args)
        {
            var addonPtr = gameGui.GetAddonByName("Emj", 1);

            if (addonPtr == IntPtr.Zero) {
                pluginLog.Info("Could not find Emj");
                return;
            }
        }

        private unsafe List<ObservedTile> GetObservedDiscardTiles(List<IntPtr> ptrs, MahjongNodeType playerArea) {
            var observedTileTextures = new List<ObservedTile>(); 
            ptrs.ForEach(ptr => {
                var castedPtr = (AtkResNode*)ptr;
                var tileTexture = NodeCrawlerUtils.GetTileTextureFromDiscardTile(ptr);
                if (tileTexture != null) {
                    if (!tileTexture.IsMelded) {
                        observedTileTextures.Add(new ObservedTile(playerArea, tileTexture.TileTexture));
                    }
                }
            });
            return observedTileTextures;
        }

        private unsafe List<ObservedTile> GetObservedMeldTiles(List<IntPtr> ptrs, MahjongNodeType playerArea) {
            var observedTileTextures = new List<ObservedTile>();
            ptrs.ForEach(ptr => {
                var castedPtr = (AtkResNode*)ptr;
                var tileTextures = NodeCrawlerUtils.GetTileTexturesFromMeldGroup(ptr);
                tileTextures?.ForEach(texture => observedTileTextures.Add(new ObservedTile(playerArea, texture)));
            });
            return observedTileTextures;
        }

        public unsafe List<ObservedTile> GetObservedTiles() {
            var observedTileTextures = new List<ObservedTile>();
            ImportantPointers.PlayerHand.ForEach(ptr => {
                var castedPtr = (AtkResNode*)ptr;
                var tileTexture = NodeCrawlerUtils.GetTileTextureFromPlayerHandTile(ptr);
                if (tileTexture != null) {
                    observedTileTextures.Add(new ObservedTile(MahjongNodeType.PLAYER_HAND_TILE, tileTexture));
                }
            });

            // Discarded tiles have their own node tree shape
            observedTileTextures.AddRange(GetObservedDiscardTiles(ImportantPointers.PlayerDiscardPile, MahjongNodeType.PLAYER_DISCARD_TILE));
            observedTileTextures.AddRange(GetObservedDiscardTiles(ImportantPointers.RightDiscardPile, MahjongNodeType.RIGHT_DISCARD_TILE));
            observedTileTextures.AddRange(GetObservedDiscardTiles(ImportantPointers.FarDiscardPile, MahjongNodeType.FAR_DISCARD_TILE));
            observedTileTextures.AddRange(GetObservedDiscardTiles(ImportantPointers.LeftDiscardPile, MahjongNodeType.LEFT_DISCARD_TILE));

            // Player melds have their own shape
            ImportantPointers.PlayerMeldGroups.ForEach(ptr => {
                var castedPtr = (AtkResNode*)ptr;
                var tileTextures = NodeCrawlerUtils.GetTileTexturesFromPlayerMeldGroup(ptr);
                tileTextures?.ForEach(texture => observedTileTextures.Add(new ObservedTile(MahjongNodeType.PLAYER_MELD_GROUP, texture)));
            });

            // Melds that are not your own have a different node tree shape
            observedTileTextures.AddRange(GetObservedMeldTiles(ImportantPointers.RightMeldGroups, MahjongNodeType.RIGHT_MELD_GROUP));
            observedTileTextures.AddRange(GetObservedMeldTiles(ImportantPointers.FarMeldGroups, MahjongNodeType.FAR_MELD_GROUP));
            observedTileTextures.AddRange(GetObservedMeldTiles(ImportantPointers.LeftMeldGroups, MahjongNodeType.LEFT_MELD_GROUP));
            
            return observedTileTextures;
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }
    }
}
