using GameModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongReader
{
    public class DiscardTile {
        public TileTexture TileTexture { get; }
        public bool IsMelded { get; }
        public bool IsImmediatelyDiscarded { get; }

        public DiscardTile(TileTexture tileTexture, bool isMelded, bool isImmediatelyDiscarded) {
            TileTexture = tileTexture;
            IsMelded = isMelded;
            IsImmediatelyDiscarded = isImmediatelyDiscarded;
        }

        public override string ToString() {
            return $"{TileTexture} IsMelded: {IsMelded} IsImmediatelyDiscarded: {IsImmediatelyDiscarded}";
        }
    }

    public class TileTexture {
        private string textureId;
        private string mjaiNotation;

        public TileTexture(string aTextureId, string aMjaiNotation) {
            textureId = aTextureId;
            mjaiNotation = aMjaiNotation;
        }

        public string TextureId
        {
            get { return textureId; }
        }

        public string MjaiNotation
        {
            get { return mjaiNotation; }
        }

        public override string ToString() {
            return $"textureId: {textureId} mjaiNotation: {mjaiNotation}";
        }
    }

    public class ObservedTile {
        private MahjongNodeType playerArea;
        private TileTexture tileTexture;

        public ObservedTile(MahjongNodeType aPlayerArea, TileTexture aTileTexture) {
            playerArea = aPlayerArea;
            tileTexture = aTileTexture;
        }

        public MahjongNodeType PlayerArea
        {
            get { return playerArea; }
        }

        public TileTexture TileTexture
        {
            get { return tileTexture; }
        }

        public override string ToString() {
            return $"playerArea: {Enum.GetName(playerArea)} tileTexture: {tileTexture}";
        }        
    }

    public class TileCountTracker {
        private Dictionary<string, int> notationToRemainingCount;

        public TileCountTracker(Dictionary<string, int> aNotationToRemainingCount) {
            notationToRemainingCount = aNotationToRemainingCount;
        }

        public Dictionary<string, int> RemainingFromObserved(List<ObservedTile> observedTiles) {
            Dictionary<string, int> copiedRemaining = notationToRemainingCount.ToDictionary(entry => entry.Key, entry => entry.Value);

            foreach (ObservedTile observedTile in observedTiles) {
                copiedRemaining[observedTile.TileTexture.MjaiNotation]--;
            }

            return copiedRemaining;
        }
    }

    public class TileTextureUtilities {
        public static Dictionary<string, TileTexture> TextureIdToTileTexture;
        public static Dictionary<string, string> NotationToTextureId;
        public static TileCountTracker TileCountTracker;

        static TileTextureUtilities() {
            Dictionary<string, TileTexture> textureIdToTileTexture = new Dictionary<string, TileTexture>();
            Dictionary<string, string> notationToTextureId = new Dictionary<string, string>();
            Dictionary<string, int> countMap = new Dictionary<string, int>();

            Action<int, string> updateCountMap = (number, notation) => {
                var suit = notation[1];
                var tileCount = 0;

                // honor tiles (z) don't have aka dora and we know that there are four instances of each
                // for the characters, dots, bamboo 5 there is the aka dora usually referring to as 0
                if (suit == 'z') {
                    tileCount = 4;
                } else {
                    if (number == 5) {
                        tileCount = 3;
                    } else {
                        tileCount = 4;
                    }
                }

                countMap.Add(notation, tileCount);
            };

            // characters/man
            for (int i = 1; i < 10; i++) {
                int nextTextureId = i + 76040; //40 is the actual character/man start
                string fullTextureId = "0" + nextTextureId.ToString();
                string notation = i.ToString() + Suit.MAN;
                textureIdToTileTexture.Add(fullTextureId, new TileTexture(fullTextureId, notation));
                notationToTextureId.Add(notation, fullTextureId);
                updateCountMap(i, notation);
            }

            // dots/pin
            for (int i = 1; i < 10; i++) {
                int nextTextureId = i + 76049; //49 is the actual dots/pin start
                string fullTextureId = "0" + nextTextureId.ToString();
                string notation = i.ToString() + Suit.PIN;
                textureIdToTileTexture.Add(fullTextureId, new TileTexture(fullTextureId, notation));
                notationToTextureId.Add(notation, fullTextureId);
                updateCountMap(i, notation);
            }

            // bamboo/sou
            for (int i = 1; i < 10; i++) {
                int nextTextureId = i + 76058; //58 is the actual bamboo/sou start
                string fullTextureId = "0" + nextTextureId.ToString();
                string notation = i.ToString() + Suit.SOU;
                textureIdToTileTexture.Add(fullTextureId, new TileTexture(fullTextureId, notation));
                notationToTextureId.Add(notation, fullTextureId);
                updateCountMap(i, notation);
            }

            // honor tiles / wind first / dragons
            for (int i = 1; i < 8; i++) {
                int nextTextureId = i + 76067; //67 is the actual honor tile start
                string fullTextureId = "0" + nextTextureId.ToString();
                string notation = i.ToString() + Suit.HONOR;
                textureIdToTileTexture.Add(fullTextureId, new TileTexture(fullTextureId, notation));
                notationToTextureId.Add(notation, fullTextureId);
                updateCountMap(i, notation);
            }

            string manDoraNotation = $"0{Suit.MAN}";
            string manDoraTextureId = "076075";
            textureIdToTileTexture.Add(manDoraTextureId, new TileTexture(manDoraTextureId, manDoraNotation));
            countMap.Add(manDoraNotation, 1);
            notationToTextureId.Add(manDoraNotation, manDoraTextureId);

            string pinDoraNotation = $"0{Suit.PIN}";
            string pinDoraTextureId = "076076";
            textureIdToTileTexture.Add(pinDoraTextureId, new TileTexture(pinDoraTextureId, pinDoraNotation));
            countMap.Add(pinDoraNotation, 1);
            notationToTextureId.Add(pinDoraNotation, pinDoraTextureId);

            string souDoraNotation = $"0{Suit.SOU}";
            string souDoraTextureId = "076077";
            textureIdToTileTexture.Add(souDoraTextureId, new TileTexture(souDoraTextureId, souDoraNotation));
            countMap.Add(souDoraNotation, 1);
            notationToTextureId.Add(souDoraNotation, souDoraTextureId);

            TextureIdToTileTexture = textureIdToTileTexture;
            NotationToTextureId = notationToTextureId;
            TileCountTracker = new TileCountTracker(countMap);
        }

        // TODO: support traditonal (IRL Mahjong tile face) textures? Same pattern, different texture id offset
        public const string TEXTURE_PATH_TILE_ICON_PREFIX = "ui/icon/076000/076000_hr1.tex";
        public const string TEXTURE_PATH_TILE_ICON_PREFIX_NEW = "ui/icon/076000/076000.tex";

        public static bool IsValidTileTexturePath(string? texturePath) {
            if (texturePath == null) return false;
            return texturePath.StartsWith(TEXTURE_PATH_TILE_ICON_PREFIX) || texturePath.StartsWith(TEXTURE_PATH_TILE_ICON_PREFIX_NEW);
        }

        public static TileTexture GetTileTextureFromTexturePath(string texturePath) {
            // Extract the texture ID from the full path
            var parts = texturePath.Split('/');
            var lastPart = parts[parts.Length - 1];
            var textureTex = lastPart.Split('_')[0];

            if (!TextureIdToTileTexture.ContainsKey(textureTex)) {
                throw new ApplicationException("Unknown key: " + texturePath);
            }

            return TextureIdToTileTexture[textureTex];
        }
    }
}