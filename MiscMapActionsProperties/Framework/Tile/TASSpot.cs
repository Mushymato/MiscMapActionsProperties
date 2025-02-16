using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData;

namespace MiscMapActionsProperties.Framework.Tile;

/// <summary>
/// Add new back layer tile property mushymato.MMAP_TAS
/// Show a TAS on the tile
/// </summary>
internal static class TASSpot
{
    internal static readonly string TileProp_TAS = $"{ModEntry.ModId}_TAS";
    internal static readonly string Asset_TAS = $"{ModEntry.ModId}/TAS";
    private static readonly ConditionalWeakTable<xTile.Map, List<TileTAS>> tileTASCache = [];

    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnToTitle;
        ModEntry.help.Events.Player.Warped += OnWarped;
    }

    private static void OnReturnToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        tileTASCache.Clear();
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        InitLocationTAS(Game1.currentLocation);
    }

    private static Dictionary<string, TemporaryAnimatedSpriteDefinition>? _tasData = null;

    /// <summary>Question dialogue data</summary>
    internal static Dictionary<string, TemporaryAnimatedSpriteDefinition> TASData
    {
        get
        {
            _tasData ??= Game1.content.Load<Dictionary<string, TemporaryAnimatedSpriteDefinition>>(Asset_TAS);
            return _tasData;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_TAS))
            e.LoadFrom(() => new Dictionary<string, TemporaryAnimatedSpriteDefinition>(), AssetLoadPriority.Low);
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_TAS)))
            _tasData = null;
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        InitLocationTAS(e.NewLocation);
    }

    private static void InitLocationTAS(GameLocation location)
    {
        List<TileTAS> tileTASList = tileTASCache.GetValue(location.map, CreateMapDefs);
        foreach (var tileTAS in tileTASList)
        {
            if (!GameStateQuery.CheckConditions(tileTAS.Def.Condition, location))
                return;
            TemporaryAnimatedSprite tas = TemporaryAnimatedSprite.CreateFromData(
                tileTAS.Def,
                tileTAS.Tile.X,
                tileTAS.Tile.Y,
                (tileTAS.Tile.Y + 0.66f) * Game1.tileSize / 10000f + tileTAS.Tile.X * 1E-05f
            // 1f
            );
            ModEntry.Log($"{tas} at {tileTAS.Tile}");
            location.TemporarySprites.Add(tas);
        }
    }

    private static List<TileTAS> CreateMapDefs(xTile.Map map)
    {
        List<TileTAS> tileTAS = [];
        var backLayer = map.RequireLayer("Back");
        for (int x = 0; x < backLayer.LayerWidth; x++)
        {
            for (int y = 0; y < backLayer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (pos.Equals(Vector2.Zero))
                    continue;
                MapTile tile = backLayer.Tiles[x, y];
                if (tile == null)
                    continue;
                if (tile.Properties.TryGetValue(TileProp_TAS, out string tasKey))
                {
                    ModEntry.Log($"tasKey {tasKey} at {pos}");
                    if (TASData.TryGetValue(tasKey, out TemporaryAnimatedSpriteDefinition? def))
                        tileTAS.Add(new(def, pos));
                }
            }
        }
        return tileTAS;
    }
}

internal sealed record TileTAS(TemporaryAnimatedSpriteDefinition Def, Vector2 Tile)
{
    // internal TemporaryAnimatedSprite GetTAS()
    // {
    //     TemporaryAnimatedSprite.GetTemporaryAnimatedSprite(
    //         Def.Texture,
    //         Def.SourceRect,
    //         Def.Interval,
    //         Def.Frames,
    //         // (duration != null) ? (int)(duration / (Def.Frames * Def.Interval)) : Def.Loops,
    //         Def.Loops,
    //         Tile * Game1.tileSize + Def.PositionOffset * 4f,
    //         Def.Flicker,
    //         Def.Flip,
    //         Def.SortOffset,
    //         Def.AlphaFade,
    //         Utility.StringToColor(Def.Color) ?? Color.White,
    //         Def.Scale * 4f,
    //         Def.ScaleChange,
    //         Def.Rotation,
    //         Def.RotationChange
    //     );
    // }
}
