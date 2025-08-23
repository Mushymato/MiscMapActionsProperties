using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace MiscMapActionsProperties.Framework.Entities;

[Flags]
public enum FurnitureDrawMode
{
    None = 0,
    Base = 1 << 0,
    Layer = 1 << 1,
    World = 1 << 2,
    Menu = 1 << 3,
    NonTile = 1 << 4,
}

/// <summary>
/// Allow furniture to get tile data, using the same format as building tile data
/// </summary>
internal static class FurnitureProperties
{
    internal static void Register()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        ModEntry.help.Events.GameLoop.Saving += OnSaving;

        // property patches
        Patch_Properties();

        // these should be removed in 1.6.16
        Patch_Obsolete1616();

        // fish tank stuff
        Patch_FishTank();

        // drawing patches
        Patch_Drawing();
    }

    #region properties
    internal const string Asset_FurnitureProperties = $"{ModEntry.ModId}/FurnitureProperties";
    private static Dictionary<string, BuildingData>? _fpData = null;
    private static readonly PerScreen<ConditionalWeakTable<Furniture, FurnitureDLState?>> dlExtInfoCacheImpl = new();
    private static ConditionalWeakTable<Furniture, FurnitureDLState?> DlExtInfoCache => dlExtInfoCacheImpl.Value ??= [];

    /// <summary>Furniture property data (secretly building data)</summary>
    internal static Dictionary<string, BuildingData> FPData
    {
        get
        {
            _fpData ??= Game1.content.Load<Dictionary<string, BuildingData>>(Asset_FurnitureProperties);
            return _fpData;
        }
    }

    private static void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(Asset_FurnitureProperties)))
        {
            _fpData = null;
            DlExtInfoCache.Clear();
            SeatPositionCache.Clear();
            TVScreens.Clear();
            FishTankInfos.Clear();
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_FurnitureProperties))
            e.LoadFrom(() => new Dictionary<string, BuildingData>(), AssetLoadPriority.Exclusive);
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        DlExtInfoCache.Clear();
        SeatPositionCache.Clear();
    }

    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        Utility.ForEachLocation(location =>
        {
            foreach (Furniture furniture in location.furniture)
            {
                int specialVariable = furniture.SpecialVariable;
                if (specialVariable >= MMAP_SpecialVariableOffset && specialVariable < MMAP_SpecialVariableLimit)
                {
                    furniture.SpecialVariable = 0;
                }
            }
            return true;
        });
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        UpdateFishTankNeighbourBounds(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        foreach (DLExtInfo dLExtInfo in CurrentLocationDLStatesIter)
        {
            dLExtInfo.RecheckRands();
        }
        UpdateFishTankNeighbourBounds(e.NewLocation);
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (DLExtInfo dLExtInfo in CurrentLocationDLStatesIter)
        {
            dLExtInfo.TimeChanged();
        }
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (DLExtInfo dLExtInfo in CurrentLocationDLStatesIter)
        {
            dLExtInfo.UpdateTicked();
        }
    }

    private static void Patch_Properties()
    {
        try
        {
            foreach (Type furnitureType in new Type[] { typeof(Furniture), typeof(BedFurniture) })
            {
                if (
                    AccessTools.DeclaredMethod(furnitureType, nameof(Furniture.DoesTileHaveProperty))
                    is MethodInfo origMethod1
                )
                {
                    ModEntry.harm.Patch(
                        original: origMethod1,
                        postfix: new HarmonyMethod(
                            typeof(FurnitureProperties),
                            nameof(Furniture_DoesTileHaveProperty_Postfix)
                        )
                    );
                }
                if (
                    AccessTools.DeclaredMethod(furnitureType, nameof(Furniture.GetAdditionalTilePropertyRadius))
                    is MethodInfo origMethod2
                )
                {
                    ModEntry.harm.Patch(
                        original: origMethod2,
                        postfix: new HarmonyMethod(
                            typeof(FurnitureProperties),
                            nameof(Furniture_GetAdditionalTilePropertyRadius_Postfix)
                        )
                    );
                }
            }
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_IntersectsForCollision_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.AllowPlacementOnThisTile)),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_AllowPlacementOnThisTile_Postfix)
                )
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties Props:\n{err}", LogLevel.Error);
        }
        try
        {
            // seats
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.GetSeatCapacity)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_GetSeatCapacity_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.GetSeatPositions)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_GetSeatPositions_Prefix))
                {
                    before = ["Espy.PreciseFurniture"],
                }
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.GetSittingDirection)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_GetSittingDirection_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.ShowSitting)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Farmer_ShowSitting_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties Seats:\n{err}", LogLevel.Error);
        }
    }

    #region seats
    internal sealed record FurnitureSeat(Point Pos, float XOffset, float YOffset, int Direction);

    private static readonly ConditionalWeakTable<Furniture, List<FurnitureSeat>?> SeatPositionCache = [];

    private static List<FurnitureSeat>? CreateSeatPositions(Furniture furniture)
    {
        if (!FPData.TryGetValue(furniture.ItemId, out BuildingData? fpData) || !(fpData.ActionTiles?.Any() ?? false))
            return null;
        List<FurnitureSeat> seatPositions = [];
        string error;
        foreach (BuildingActionTile actionTile in fpData.ActionTiles)
        {
            string[] args = ArgUtility.SplitBySpace(actionTile.Action);
            if (!ArgUtility.TryGet(args, 0, out string value, out error, allowBlank: false, name: "string Action"))
            {
                ModEntry.Log(error, LogLevel.Error);
                continue;
            }
            if (value == "Seat")
            {
                if (
                    !ArgUtility.TryGetOptionalFloat(
                        args,
                        1,
                        out float xOffset,
                        out error,
                        defaultValue: 0,
                        name: "float xOffset"
                    )
                    || !ArgUtility.TryGetOptionalFloat(
                        args,
                        2,
                        out float yOffset,
                        out error,
                        defaultValue: 0,
                        name: "float yOffset"
                    )
                    || !ArgUtility.TryGetOptionalInt(
                        args,
                        3,
                        out int direction,
                        out error,
                        defaultValue: 0,
                        name: "int direction"
                    )
                )
                {
                    ModEntry.Log(error, LogLevel.Error);
                    continue;
                }
                seatPositions.Add(new(actionTile.Tile, xOffset, yOffset, direction));
            }
        }
        return seatPositions;
    }

    private static void Furniture_GetSeatCapacity_Postfix(Furniture __instance, ref int __result)
    {
        if (SeatPositionCache.GetValue(__instance, CreateSeatPositions) is not List<FurnitureSeat> seatPositions)
            return;
        __result = seatPositions.Count;
    }

    private static bool Furniture_GetSeatPositions_Prefix(Furniture __instance, ref List<Vector2> __result)
    {
        if (SeatPositionCache.GetValue(__instance, CreateSeatPositions) is not List<FurnitureSeat> seatPositions)
            return true;
        Rectangle boundingBox = __instance.boundingBox.Value;
        Vector2 boundingBoxPos = new(boundingBox.X / 64f, boundingBox.Y / 64f);
        __result = seatPositions.Select(seat => boundingBoxPos + seat.Pos.ToVector2()).ToList();
        return false;
    }

    private static void Furniture_GetSittingDirection_Postfix(Furniture __instance, ref int __result)
    {
        if (
            SeatPositionCache.GetValue(__instance, CreateSeatPositions) is not List<FurnitureSeat> seatPositions
            || !__instance.sittingFarmers.TryGetValue(Game1.player.UniqueMultiplayerID, out int seatIdx)
            || seatIdx >= seatPositions.Count
        )
            return;

        FurnitureSeat seatInfo = seatPositions[seatIdx];
        int direction = seatInfo.Direction;
        if (direction > 0 && direction < 4)
        {
            __result = direction;
        }
        else if (direction == -1)
        {
            __result = Game1.player.FacingDirection;
        }
    }

    private static void Farmer_ShowSitting_Postfix(Farmer __instance)
    {
        if (
            !__instance.IsSitting()
            || __instance.sittingFurniture is not Furniture seatFurniture
            || SeatPositionCache.GetValue(seatFurniture, CreateSeatPositions) is not List<FurnitureSeat> seatPositions
            || !seatFurniture.sittingFarmers.TryGetValue(__instance.UniqueMultiplayerID, out int seatIdx)
            || seatIdx >= seatPositions.Count
        )
        {
            return;
        }
        // Note: does not sync in multiplayer rn because __instance.sittingFurniture does not sync
        FurnitureSeat seatInfo = seatPositions[seatIdx];
        if (__instance.yJumpOffset != 0)
        {
            __instance.xOffset = -seatInfo.XOffset;
            __instance.yOffset = -seatInfo.YOffset;
        }
        else
        {
            __instance.xOffset -= seatInfo.XOffset;
            __instance.yOffset -= seatInfo.YOffset;
        }
    }

    #endregion

    private static void Furniture_AllowPlacementOnThisTile_Postfix(
        Furniture __instance,
        int tile_x,
        int tile_y,
        ref bool __result
    )
    {
        if (__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        if (fpData.CollisionMap == null)
            return;
        __result = fpData.IsTilePassable(
            (int)(tile_x - __instance.TileLocation.X),
            (int)(tile_y - __instance.TileLocation.Y)
        );
    }

    private static void Furniture_DoesTileHaveProperty_Postfix(
        Furniture __instance,
        int tile_x,
        int tile_y,
        string property_name,
        string layer_name,
        ref string property_value,
        ref bool __result
    )
    {
        if (__result || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        __result = fpData.HasPropertyAtTile(
            (int)(tile_x - __instance.TileLocation.X),
            (int)(tile_y - __instance.TileLocation.Y),
            property_name,
            layer_name,
            ref property_value
        );
    }

    public const int MMAP_SpecialVariableOffset = 28423000;
    public const int MMAP_SpecialVariableLimit = 28424000;

    /// <summary>Furniture.GetAdditionalTilePropertyRadius doing a dict lookup is big perf hit</summary>
    private static void Furniture_GetAdditionalTilePropertyRadius_Postfix(Furniture __instance, ref int __result)
    {
        int specialVariable = __instance.SpecialVariable;
        if (specialVariable >= MMAP_SpecialVariableOffset && specialVariable < MMAP_SpecialVariableLimit)
        {
            __result = specialVariable - MMAP_SpecialVariableOffset;
            return;
        }
        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;
        __result = Math.Max(0, fpData.AdditionalTilePropertyRadius);
        __instance.SpecialVariable = MMAP_SpecialVariableOffset + __result;
    }

    private static void Furniture_IntersectsForCollision_Postfix(
        Furniture __instance,
        Rectangle rect,
        ref bool __result
    )
    {
        bool playerIsMoving = Game1.player.isMoving();
        if (!__result && !playerIsMoving)
            return;

        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        Rectangle boundingBox = __instance.boundingBox.Value;
        // check contact effects with furniture
        if (playerIsMoving)
        {
            int radius = __instance.GetAdditionalTilePropertyRadius() * Game1.tileSize;

            Rectangle furniBounds =
                new(
                    boundingBox.X - radius,
                    boundingBox.Y - radius,
                    boundingBox.Width + 2 * radius,
                    boundingBox.Height + 2 * radius
                );
            if (!furniBounds.Intersects(rect))
                return;

            Rectangle playerBounds = Game1.player.GetBoundingBox();
            if (
                furniBounds.Intersects(playerBounds)
                && DlExtInfoCache.GetValue(__instance, FurnitureDLState.GetFurnitureDLState) is FurnitureDLState state
            )
            {
                float speed = Game1.player.getMovementSpeed();
                bool left = playerBounds.Center.X > furniBounds.Center.X;

                foreach ((_, DLExtInfo? dlExt) in state.LayerInfo)
                {
                    dlExt?.StartContact(furniBounds, speed, left);
                }
            }
        }

        // check actual collision
        if (!__result || fpData.CollisionMap == null)
            return;

        Point boundingBoxPos = new(boundingBox.X / Game1.tileSize, boundingBox.Y / Game1.tileSize);
        for (int i = rect.Top / 64; i <= rect.Bottom / 64; i++)
        {
            for (int j = rect.Left / 64; j <= rect.Right / 64; j++)
            {
                if (!fpData.IsTilePassable(j - boundingBoxPos.X, i - boundingBoxPos.Y))
                {
                    return;
                }
            }
        }
        __result = false;
    }
    #endregion

    #region obsolete_1.6.16
    private static void Patch_Obsolete1616()
    {
        try
        {
            // This patch targets a function earlier than spacecore (which patches at Furniture.getDescription), so spacecore description will override it.
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), "loadDescription"),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_loadDescription_Prefix))
            );
            // custom TV is a feature to be eaten by 1.6.16 but i'll add it here for now
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.GetFurnitureInstance)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_GetFurnitureInstance_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(TV), nameof(TV.getScreenPosition)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(TV_getScreenPosition_Postfix))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(TV), nameof(TV.getScreenSizeModifier)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(TV_getScreenSizeModifier_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties Draw:\n{err}", LogLevel.Error);
        }
    }

    private static bool Furniture_loadDescription_Prefix(Furniture __instance, ref string __result)
    {
        if (
            FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData)
            && !string.IsNullOrEmpty(fpData.Description)
            && TokenParser.ParseText(fpData.Description) is string furniDesc
        )
        {
            __result = Game1.parseText(furniDesc, Game1.smallFont, 320);
            return false;
        }
        return true;
    }

    internal const string CustomFields_TV = "TV";

    internal const string CustomFields_FishTank = "FishTank";

    private record TVScreenShape(float PosX, float PosY, float Scale);

    private static readonly ConditionalWeakTable<TV, TVScreenShape?> TVScreens = [];

    private static void Furniture_GetFurnitureInstance_Postfix(string itemId, ref Furniture __result)
    {
        // require exact Furniture type
        if (__result.GetType() != typeof(Furniture))
            return;
        // ban rugs (since they are excluded from Furniture.checkForAction)
        int furniType = __result.furniture_type.Value;
        if (furniType == Furniture.rug)
            return;

        if (
            !FPData.TryGetValue(__result.ItemId, out BuildingData? fpData)
            || fpData.CustomFields is not Dictionary<string, string> customFields
        )
            return;

        if (customFields.ContainsKey(CustomFields_TV))
        {
            __result = new TV(itemId, __result.TileLocation);
            return;
        }
        if (customFields.ContainsKey(CustomFields_FishTank))
        {
            __result = new FishTankFurniture(itemId, __result.TileLocation);
            return;
        }
    }

    private static TVScreenShape? GetTVScreenShape(TV tv)
    {
        if (
            FPData.TryGetValue(tv.ItemId, out BuildingData? fpData)
            && (fpData.CustomFields?.TryGetValue(CustomFields_TV, out string? tvRect) ?? false)
        )
        {
            string[] args = ArgUtility.SplitBySpace(tvRect);
            if (
                !ArgUtility.TryGetVector2(args, 0, out Vector2 pos, out string error, name: "Vector2 pos")
                || !ArgUtility.TryGetFloat(args, 2, out float scale, out error, name: "float scale")
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                return null;
            }
            return new(pos.X, pos.Y, scale);
        }
        return null;
    }

    public static void TV_getScreenPosition_Postfix(TV __instance, ref Vector2 __result)
    {
        if (TVScreens.GetValue(__instance, GetTVScreenShape) is TVScreenShape shape)
        {
            __result = new(__instance.boundingBox.X + shape.PosX, __instance.boundingBox.Y + shape.PosY);
        }
    }

    public static void TV_getScreenSizeModifier_Postfix(TV __instance, ref float __result)
    {
        if (TVScreens.GetValue(__instance, GetTVScreenShape) is TVScreenShape shape)
        {
            __result = shape.Scale;
        }
    }
    #endregion

    #region fishtank
    private record FishTankInfo(int Capacity, int PosX, int PosY, int Width, int Height)
    {
        /// <summary>Mark tank bounds as need combining</summary>
        internal bool IsDirty = true;

        /// <summary>Cached tank bounds</summary>
        internal Rectangle CurrentTankBounds = Rectangle.Empty;

        /// <summary>Base tank bounds given a tile location</summary>
        internal Rectangle GetBaseTankBounds(Vector2 pos)
        {
            return new Rectangle((int)(pos.X + PosX), (int)(pos.Y + PosY), Width, Height);
        }
    }

    private static readonly ConditionalWeakTable<FishTankFurniture, FishTankInfo?> FishTankInfos = [];

    private static void Patch_FishTank()
    {
        CommonPatch.Furniture_OnMoved += Furniture_OnMoved;
        try
        {
            // Things can become a fishtank via earier Furniture.GetFurnitureInstance patch, move it down here if removing that in 1.6.16
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(FishTankFurniture),
                    nameof(FishTankFurniture.GetCapacityForCategory)
                ),
                postfix: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(FishTankFurniture_GetCapacityForCategory_Postfix)
                )
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(FishTankFurniture),
                    nameof(FishTankFurniture.GetTankBounds)
                ),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(FishTankFurniture_GetTankBounds_Postfix))
                {
                    after = ["Espy.PreciseFurniture"],
                }
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(TankFish), nameof(TankFish.Update)),
                postfix: new HarmonyMethod(typeof(FurnitureProperties), nameof(TankFish_Update_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties FishTank:\n{err}", LogLevel.Error);
        }
    }

    private static void Furniture_OnMoved(object? sender, CommonPatch.OnFurnitureMovedArgs e)
    {
        if (e.Placement.Location == Game1.currentLocation && e.Furniture is FishTankFurniture)
            UpdateFishTankNeighbourBounds(Game1.currentLocation);
    }

    /// <summary>Convert context tag to FishTankInfo</summary>
    private static FishTankInfo? GetFishTankInfo(FishTankFurniture fishtank)
    {
        if (
            !FPData.TryGetValue(fishtank.ItemId, out BuildingData? fpData)
            || !(fpData.CustomFields?.TryGetValue(CustomFields_FishTank, out string? fishTankDef) ?? false)
        )
        {
            return null;
        }
        string[] args = ArgUtility.SplitBySpace(fishTankDef);
        if (
            !ArgUtility.TryGetOptionalInt(args, 0, out int capacity, out string error, defaultValue: -2, "int capacity")
            || !ArgUtility.TryGetOptionalInt(args, 1, out int posX, out error, defaultValue: 0, "int posX")
            || !ArgUtility.TryGetOptionalInt(args, 2, out int posY, out error, defaultValue: 0, "int posY")
            || !ArgUtility.TryGetOptionalInt(args, 3, out int width, out error, defaultValue: 0, "int width")
            || !ArgUtility.TryGetOptionalInt(args, 4, out int height, out error, defaultValue: 0, "int width")
        )
        {
            ModEntry.Log(error, LogLevel.Warn);
            return null;
        }
        return new FishTankInfo(capacity, posX, posY, width, height);
    }

    private static readonly FieldInfo drawPositionField = AccessTools.DeclaredField(typeof(Furniture), "drawPosition");

    private static Vector2 GetDrawPosition(Furniture furniture)
    {
        if (drawPositionField.GetValue(furniture) is Netcode.NetVector2 vec)
        {
            return vec.Value;
        }
        return Vector2.Zero;
    }

    /// <summary>Update fish tank bounds of current location</summary>
    private static void UpdateFishTankNeighbourBounds(GameLocation where)
    {
        if (where == null)
            return;

        Dictionary<FishTankFurniture, FishTankInfo> maybeConnectedTanks = [];
        foreach (Furniture furniture in where.furniture)
        {
            if (
                furniture is FishTankFurniture tank
                && FishTankInfos.GetValue(tank, GetFishTankInfo) is FishTankInfo tankInfo
            )
            {
                tankInfo.CurrentTankBounds = tankInfo.GetBaseTankBounds(GetDrawPosition(furniture));
                if (tank.modData.ContainsKey(ConnectedTextures.ConnectedTextureApplied))
                {
                    tankInfo.IsDirty = true;
                    maybeConnectedTanks[tank] = tankInfo;
                }
            }
        }

        foreach (
            (FishTankFurniture tank, FishTankInfo tankInfo) in maybeConnectedTanks.OrderBy(kv => kv.Key.TileLocation.X)
        )
        {
            if (!tankInfo.IsDirty)
                continue;
            List<(FishTankFurniture, FishTankInfo)> needUpdate = [];
            RightOnlyDFS_FishTank(maybeConnectedTanks, tank, tankInfo, ref needUpdate);
            Rectangle combinedBounds = tankInfo.CurrentTankBounds;
            foreach ((_, FishTankInfo subInfo) in needUpdate)
            {
                combinedBounds = Rectangle.Union(combinedBounds, subInfo.CurrentTankBounds);
            }
            foreach ((FishTankFurniture subTank, FishTankInfo subInfo) in needUpdate)
            {
                subInfo.CurrentTankBounds = combinedBounds;
                foreach (TankFish fish in subTank.tankFish)
                {
                    fish.position.X = Random.Shared.Next(combinedBounds.Width);
                }
                subInfo.IsDirty = false;
            }
        }
    }

    /// <summary>Traverse down right side of tank for connections, works because maybeConnectedTanks is searched in TileLocation.X order</summary>
    private static void RightOnlyDFS_FishTank(
        Dictionary<FishTankFurniture, FishTankInfo> maybeConnectedTanks,
        FishTankFurniture tank,
        FishTankInfo tankInfo,
        ref List<(FishTankFurniture, FishTankInfo)> needUpdate
    )
    {
        needUpdate.Add(new(tank, tankInfo));

        if (
            !ConnectedTextures.Data.TryGetValue(tank.QualifiedItemId, out ConnectedTextures.ConnectedTextureData? data)
            || data.ConnectWith is not IList<string> connections
        )
            return;

        if (
            !ConnectedTextures.ConnectsToSide(
                tank.Location,
                new(1, 0),
                ConnectedTextures.FurnitureTileBounds(tank),
                connections,
                out StardewValley.Object? found,
                maybeConnectedTanks.Keys
            )
        )
            return;

        if (found is not FishTankFurniture tank2)
            return;

        RightOnlyDFS_FishTank(maybeConnectedTanks, tank2, maybeConnectedTanks[tank2], ref needUpdate);
    }

    /// <summary>Patch number of fish allowed</summary>
    public static void FishTankFurniture_GetCapacityForCategory_Postfix(
        FishTankFurniture __instance,
        FishTankFurniture.FishTankCategories category,
        ref int __result
    )
    {
        if (FishTankInfos.GetValue(__instance, GetFishTankInfo) is FishTankInfo tankInfo && tankInfo.Capacity != -2)
        {
            if (category == FishTankFurniture.FishTankCategories.Decoration)
                __result = -1;
            __result = tankInfo.Capacity;
        }
    }

    /// <summary>Patch tank bounds</summary>
    public static void FishTankFurniture_GetTankBounds_Postfix(FishTankFurniture __instance, ref Rectangle __result)
    {
        if (FishTankInfos.GetValue(__instance, GetFishTankInfo) is FishTankInfo tankInfo)
        {
            if (tankInfo.CurrentTankBounds.Width != 0 && tankInfo.CurrentTankBounds.Height != 0)
            {
                __result = tankInfo.CurrentTankBounds;
            }
        }
    }

    /// <summary>Make fishes in 1 tile (64px) wide tanks not move around in the X direction</summary>
    public static void TankFish_Update_Postfix(TankFish __instance, FishTankFurniture ____tank)
    {
        if (____tank.GetTankBounds().Width <= Game1.tileSize)
        {
            if (__instance.fishType == TankFish.FishType.Float)
                __instance.velocity.X = 0;
            __instance.facingLeft = false;
        }
    }
    #endregion

    #region drawing
    internal const float LAYER_OFFSET = 1E-06f;
    private static FurnitureDrawMode FurnitureDraw = FurnitureDrawMode.None;
    private static float FurnitureLayerDepthOffset = 0f;
    private static float DrawFurnitureLayerDepthMax = 0f;
    private static readonly Regex IdIsRotation = new(@"^.+_Rotation.(\d+)$", RegexOptions.IgnoreCase);
    private static readonly MethodInfo? Furniture_getScaleSize = AccessTools.DeclaredMethod(
        typeof(Furniture),
        "getScaleSize"
    );

    private static float TryGetScaleSize(Furniture furniture)
    {
        if (Furniture_getScaleSize?.Invoke(furniture, null) is float scaleSize)
            return scaleSize;
        return 1f;
    }

    private static void Patch_Drawing()
    {
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Prefix)),
                transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Transpiler))
                {
                    priority = Priority.Last,
                },
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_draw_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(BedFurniture), nameof(BedFurniture.draw)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Prefix)),
                transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Transpiler))
                {
                    priority = Priority.Last,
                },
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(BedFurniture_draw_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawInMenu)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Prefix)),
                transpiler: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Transpiler))
                {
                    priority = Priority.Last,
                },
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawInMenu_Finalizer))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.drawAtNonTileSpot)),
                prefix: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawAtNonTileSpot_Prefix)),
                transpiler: new HarmonyMethod(
                    typeof(FurnitureProperties),
                    nameof(Furniture_drawAtNonTileSpot_Transpiler)
                )
                {
                    priority = Priority.Last,
                },
                finalizer: new HarmonyMethod(typeof(FurnitureProperties), nameof(Furniture_drawAtNonTileSpot_Finalizer))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch FurnitureProperties Draw:\n{err}", LogLevel.Error);
        }
    }

    internal sealed record FurnitureDLState(
        BuildingData FpData,
        List<(BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt)> LayerInfo
    )
    {
        internal enum DrawSource
        {
            Normal,
            Menu,
            NonTile,
        }

        private Point connectedTexturesOffset = Point.Zero;

        internal static FurnitureDLState? GetFurnitureDLState(Furniture furniture)
        {
            if (!FPData.TryGetValue(furniture.ItemId, out BuildingData? fpData))
            {
                return null;
            }
            if (fpData.DrawLayers == null)
            {
                return null;
            }
            return new(
                fpData,
                fpData
                    .DrawLayers.Select(
                        (drawLayer) =>
                        {
                            DrawLayerExt.TryGetDLExtInfo(fpData, drawLayer, out DLExtInfo? dlExtInfo);
                            return new ValueTuple<BuildingDrawLayer, DLExtInfo?>(drawLayer, dlExtInfo);
                        }
                    )
                    .ToList()
            );
        }

        internal static void SetConnectedTexturesOffset(Furniture furniture, Point connectedTexturesOffset)
        {
            if (DlExtInfoCache.GetValue(furniture, GetFurnitureDLState) is FurnitureDLState state)
            {
                state.connectedTexturesOffset = connectedTexturesOffset;
            }
        }

        private readonly Dictionary<string, int?> parsedRotations = [];

        internal bool CheckRotation(string drawLayerId, int currentRotation)
        {
            if (!parsedRotations.TryGetValue(drawLayerId, out int? rotation))
            {
                if (
                    !string.IsNullOrEmpty(drawLayerId)
                    && IdIsRotation.Match(drawLayerId) is Match match
                    && match.Success
                )
                {
                    rotation = int.Parse(match.Groups[1].Value);
                }
                else
                {
                    rotation = null;
                }
                parsedRotations[drawLayerId] = rotation;
            }
            return rotation == null || rotation == currentRotation;
        }

        internal void Draw(
            Furniture furniture,
            Vector2 drawPosition,
            SpriteBatch spriteBatch,
            float alpha,
            float furnitureLayerDepth,
            float scaleSize,
            DrawSource drawSource = DrawSource.Normal
        )
        {
            foreach ((BuildingDrawLayer drawLayer, DLExtInfo? drawLayerExt) in LayerInfo)
            {
                if (!CheckRotation(drawLayer.Id, furniture.currentRotation.Value))
                {
                    continue;
                }

                ParsedItemData? dataOrErrorItem = null;

                float layerDepth;
                Vector2 drawPos;
                if (drawSource == DrawSource.Menu)
                {
                    layerDepth = 0f;
                    dataOrErrorItem ??= ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                    Rectangle baseSrcRect = dataOrErrorItem.GetSourceRect();
                    drawPos =
                        drawPosition
                        + new Vector2(32, 32)
                        - new Vector2(baseSrcRect.Width / 2 * scaleSize, baseSrcRect.Height / 2 * scaleSize)
                        + drawLayer.DrawPosition * scaleSize;
                }
                else
                {
                    layerDepth = drawLayer.DrawInBackground ? 0f : furnitureLayerDepth;
                    if (drawSource == DrawSource.NonTile)
                    {
                        layerDepth = furnitureLayerDepth;
                        drawPos = drawPosition + drawLayer.DrawPosition * scaleSize;
                    }
                    else
                    {
                        layerDepth -= drawLayer.SortTileOffset * 64f / 10000f + furniture.TileLocation.X * LAYER_OFFSET;
                        drawPos = Game1.GlobalToLocal(drawPosition + drawLayer.DrawPosition * scaleSize);
                    }
                }

                Rectangle sourceRect = drawLayer.GetSourceRect(
                    (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds
                );
                sourceRect = AdjustSourceRectToSeason(FpData, furniture.Location, sourceRect);
                Texture2D texture;
                if (Game1.content.DoesAssetExist<Texture2D>(drawLayer.Texture))
                {
                    texture = Game1.content.Load<Texture2D>(drawLayer.Texture);
                }
                else
                {
                    dataOrErrorItem ??= ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                    texture = dataOrErrorItem.GetTexture();
                    sourceRect.Offset(connectedTexturesOffset);
                }

                if (drawLayerExt != null)
                {
                    float rotation = 0f;
                    if (drawLayerExt.ContactState is DLContactState contactState)
                    {
                        rotation += contactState.Rotate;
                        if (contactState.OpenPhase != DLContactState.Phase.None)
                            sourceRect = drawLayer.GetSourceRect(contactState.AnimTime);
                    }
                    drawLayerExt.Draw(
                        spriteBatch,
                        texture,
                        drawPos,
                        sourceRect,
                        Color.White * alpha,
                        rotation,
                        Vector2.Zero,
                        scaleSize,
                        SpriteEffects.None,
                        layerDepth
                    );
                }
                else
                {
                    spriteBatch.Draw(
                        texture,
                        drawPos,
                        sourceRect,
                        Color.White * alpha,
                        0f,
                        Vector2.Zero,
                        scaleSize,
                        SpriteEffects.None,
                        layerDepth
                    );
                }
            }
        }
    }

    internal static IEnumerable<DLExtInfo> CurrentLocationDLStatesIter
    {
        get
        {
            if (Game1.currentLocation == null)
                yield break;
            ConditionalWeakTable<Furniture, FurnitureDLState?> cacheImpl = dlExtInfoCacheImpl.Value;
            if (cacheImpl == null)
                yield break;
            foreach (Furniture furniture in Game1.currentLocation.furniture)
            {
                if (cacheImpl.TryGetValue(furniture, out FurnitureDLState? state) && state != null)
                {
                    foreach ((_, DLExtInfo? dLExtInfo) in state.LayerInfo)
                    {
                        if (dLExtInfo != null)
                            yield return dLExtInfo;
                    }
                }
            }
        }
    }

    private static Rectangle AdjustSourceRectToSeason(BuildingData fpData, GameLocation location, Rectangle sourceRect)
    {
        if (fpData.SeasonOffset != Point.Zero)
        {
            int seasonIndexForLocation = Game1.GetSeasonIndexForLocation(location);
            sourceRect.X += fpData.SeasonOffset.X * seasonIndexForLocation;
            sourceRect.Y += fpData.SeasonOffset.Y * seasonIndexForLocation;
        }
        return sourceRect;
    }

    // general furniture draw patch
    private static void Furniture_draw_Prefix(
        Furniture __instance,
        ref (FurnitureDLState?, FurnitureDrawMode, Rectangle?)? __state
    )
    {
        __state = null;
        FurnitureLayerDepthOffset = 0;
        if (__instance.isTemporarilyInvisible || !FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        Rectangle? oldSourceRect = null;
        FurnitureDLState? dlState;
        FurnitureDrawMode prevDraw = FurnitureDraw;

        FurnitureDraw |= FurnitureDrawMode.Base;
        if ((dlState = DlExtInfoCache.GetValue(__instance, FurnitureDLState.GetFurnitureDLState)) is not null)
        {
            FurnitureDraw |= FurnitureDrawMode.Layer;
            if (!fpData.DrawShadow)
                FurnitureDraw &= ~FurnitureDrawMode.Base;
        }

        FurnitureLayerDepthOffset = fpData.SortTileOffset * 64f / 10000f + __instance.TileLocation.X * LAYER_OFFSET;

        if (fpData.DrawShadow)
        {
            oldSourceRect = __instance.sourceRect.Value;
            __instance.sourceRect.Value = AdjustSourceRectToSeason(
                fpData,
                __instance.Location,
                __instance.sourceRect.Value
            );
        }
        __state = new(dlState, prevDraw, oldSourceRect);
    }

    private static void Furniture_draw_Finalizer(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha,
        ref (FurnitureDLState?, FurnitureDrawMode, Rectangle?)? __state
    )
    {
        if (__state == null)
            return;

        if (__state.Value.Item1 is FurnitureDLState state)
        {
            float layerDepth = DrawFurnitureLayerDepthMax;
            FurnitureDraw &= ~FurnitureDrawMode.None;
            state.Draw(
                __instance,
                Furniture.isDrawingLocationFurniture
                    ? ___drawPosition.Value
                    : new Vector2(
                        x * Game1.tileSize,
                        y * Game1.tileSize - (__instance.sourceRect.Height * 4f - __instance.boundingBox.Height)
                    ),
                spriteBatch,
                alpha,
                layerDepth,
                4f
            );
        }

        FurnitureDraw = __state.Value.Item2;

        DrawFurnitureLayerDepthMax = 0;
        FurnitureLayerDepthOffset = 0;

        if (__state.Value.Item3 is Rectangle sourceRect)
            __instance.sourceRect.Value = sourceRect;
    }

    internal static void DrawReplace(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    )
    {
        float overrideLayerDepth = layerDepth;
        FurnitureDrawMode mode = FurnitureDraw;
        if (mode != FurnitureDrawMode.None)
        {
            if (!mode.HasFlag(FurnitureDrawMode.NonTile) && !mode.HasFlag(FurnitureDrawMode.Menu))
                DrawFurnitureLayerDepthMax = Math.Max(layerDepth, DrawFurnitureLayerDepthMax);
            if (!mode.HasFlag(FurnitureDrawMode.Base))
                return;
            if (Furniture.isDrawingLocationFurniture)
                overrideLayerDepth -= FurnitureLayerDepthOffset;
        }
        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, overrideLayerDepth);
    }

    private static IEnumerable<CodeInstruction> Furniture_draw_Transpiler_Inner(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator,
        string target
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            int foundDraw = 0;
            MethodInfo replacedDraw = AccessTools.DeclaredMethod(typeof(FurnitureProperties), nameof(DrawReplace));
            CodeMatch[] callvirtDraw =
            [
                new(
                    OpCodes.Callvirt,
                    AccessTools.DeclaredMethod(
                        typeof(SpriteBatch),
                        nameof(SpriteBatch.Draw),
                        [
                            typeof(Texture2D),
                            typeof(Vector2),
                            typeof(Rectangle?),
                            typeof(Color),
                            typeof(float),
                            typeof(Vector2),
                            typeof(float),
                            typeof(SpriteEffects),
                            typeof(float),
                        ]
                    )
                ),
            ];
            matcher
                .MatchStartForward(callvirtDraw)
                .Repeat(match =>
                {
                    match.Opcode = OpCodes.Call;
                    match.Operand = replacedDraw;
                    match.Advance(1);
                    foundDraw++;
                    if (foundDraw >= 10)
                        match.End();
                });

            ModEntry.Log($"{target}-Transpiler: Replaced {foundDraw} SpriteBatch.Draw calls.");

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Furniture_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.draw");
    }

    // bed furniture draw patch
    private static void BedFurniture_draw_Prefix(
        Furniture __instance,
        ref (FurnitureDLState?, FurnitureDrawMode, Rectangle?)? __state
    )
    {
        if (Furniture.isDrawingLocationFurniture)
            Furniture_draw_Prefix(__instance, ref __state);
    }

    private static void BedFurniture_draw_Finalizer(
        Furniture __instance,
        Netcode.NetVector2 ___drawPosition,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha,
        ref (FurnitureDLState?, FurnitureDrawMode, Rectangle?)? __state
    )
    {
        if (Furniture.isDrawingLocationFurniture)
            Furniture_draw_Finalizer(__instance, ___drawPosition, spriteBatch, x, y, alpha, ref __state);
    }

    private static IEnumerable<CodeInstruction> BedFurniture_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "BedFurniture.draw");
    }

    // draw in menu patch
    private static void Furniture_drawInMenu_Prefix(
        Furniture __instance,
        ref (FurnitureDLState?, FurnitureDrawMode)? __state
    )
    {
        __state = null;
        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        FurnitureDrawMode prevDraw = FurnitureDraw;
        FurnitureDLState? dlState;

        if (FurnitureDraw == FurnitureDrawMode.None)
            FurnitureDraw = FurnitureDrawMode.Base;
        FurnitureDraw |= FurnitureDrawMode.Menu;
        if ((dlState = DlExtInfoCache.GetValue(__instance, FurnitureDLState.GetFurnitureDLState)) is not null)
        {
            FurnitureDraw |= FurnitureDrawMode.Layer;
            if (!fpData.DrawShadow)
                FurnitureDraw &= ~FurnitureDrawMode.Base;
        }

        __state = new(dlState, prevDraw);
    }

    private static IEnumerable<CodeInstruction> Furniture_drawInMenu_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.drawInMenu");
    }

    private static void Furniture_drawInMenu_Finalizer(
        Furniture __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth,
        ref (FurnitureDLState?, FurnitureDrawMode)? __state
    )
    {
        if (__state == null)
            return;

        __state.Value.Item1?.Draw(
            __instance,
            location,
            spriteBatch,
            transparency,
            layerDepth,
            TryGetScaleSize(__instance) * scaleSize,
            drawSource: FurnitureDLState.DrawSource.Menu
        );
        FurnitureDraw &= __state.Value.Item2;
    }

    // draw at non tile spot patch
    private static void Furniture_drawAtNonTileSpot_Prefix(
        Furniture __instance,
        ref (FurnitureDLState?, FurnitureDrawMode)? __state
    )
    {
        __state = null;
        if (!FPData.TryGetValue(__instance.ItemId, out BuildingData? fpData))
            return;

        FurnitureDrawMode prevDraw = FurnitureDraw;
        FurnitureDLState? dlState;

        if (FurnitureDraw == FurnitureDrawMode.None)
            FurnitureDraw = FurnitureDrawMode.Base;
        FurnitureDraw |= FurnitureDrawMode.NonTile;
        if ((dlState = DlExtInfoCache.GetValue(__instance, FurnitureDLState.GetFurnitureDLState)) is not null)
        {
            FurnitureDraw |= FurnitureDrawMode.Layer;
            if (!fpData.DrawShadow)
                FurnitureDraw &= ~FurnitureDrawMode.Base;
        }

        __state = new(dlState, prevDraw);
    }

    private static IEnumerable<CodeInstruction> Furniture_drawAtNonTileSpot_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        return Furniture_draw_Transpiler_Inner(instructions, generator, "Furniture.drawAtNonTileSpot");
    }

    private static void Furniture_drawAtNonTileSpot_Finalizer(
        Furniture __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float layerDepth,
        float alpha,
        ref (FurnitureDLState?, FurnitureDrawMode)? __state
    )
    {
        if (__state == null)
            return;

        __state.Value.Item1?.Draw(
            __instance,
            location,
            spriteBatch,
            alpha,
            layerDepth,
            4f,
            drawSource: FurnitureDLState.DrawSource.NonTile
        );
        FurnitureDraw &= __state.Value.Item2;
    }
    #endregion
}
