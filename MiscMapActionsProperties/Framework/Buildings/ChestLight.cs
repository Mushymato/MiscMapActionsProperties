using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.GameData.Buildings;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace MiscMapActionsProperties.Framework.Buildings;

/// <summary>
/// Add new BuildingData.Metadata mushymato.MMAP/ChestLight.<ChestId>: [radius] [color] [type|texture] [offsetX] [offsetY]
/// Place a light source on a tile, with optional offset
/// [type|texture] is either a light id (1-10 except for 3) or a texture (must be loaded).
/// </summary>
internal static class ChestLight
{
    internal static readonly string Metadata_ChestLight_Prefix = $"{ModEntry.ModId}/ChestLight.";

    private static readonly ConditionalWeakTable<Chest, BuildingChestLightWatcher> watchers = [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        AddBuildingChestLightWatcher(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        foreach (var kv in watchers)
            kv.Value.Unsubscribe();
        AddBuildingChestLightWatcher(e.NewLocation);
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        foreach (var kv in watchers)
            kv.Value.Dispose();
        watchers.Clear();
    }

    private static void AddBuildingChestLightWatcher(GameLocation location)
    {
        foreach (Building building in location.buildings)
        {
            BuildingData data = building.GetData();
            if (data == null)
                continue;

            foreach (Chest buildingChest in building.buildingChests)
            {
                string lightName = $"{Metadata_ChestLight_Prefix}{buildingChest.Name}";
                if (!data.Metadata.TryGetValue(lightName, out string? lightProps))
                    continue;
                var watch = watchers.GetValue(
                    buildingChest,
                    (chest) => new BuildingChestLightWatcher(building, chest, lightName, lightProps)
                );
                watch.Subscribe();
            }
        }
    }
}

/// <summary>
/// Shenanigans for watching building chest changes.
/// Use with WeakReference or ConditionalWeakTable;
/// </summary>
internal sealed class BuildingChestLightWatcher(Building building, Chest chest, string lightName, string lightProps)
    : IDisposable
{
    private Building building = building;
    private Chest chest = chest;
    private readonly string lightName = lightName;
    private readonly string lightProps = lightProps;
    internal bool wasDisposed = false;

    ~BuildingChestLightWatcher() => DisposeValues();

    private void DisposeValues()
    {
        if (wasDisposed)
            return;
        chest.Items.OnSlotChanged -= OnSlotChanged;
        building = null!;
        chest = null!;
        wasDisposed = true;
    }

    public void Dispose()
    {
        DisposeValues();
        GC.SuppressFinalize(this);
    }

    public void Subscribe()
    {
        UpdateBuildingLights();
        chest.Items.OnSlotChanged += OnSlotChanged;
    }

    public void Unsubscribe()
    {
        chest.Items.OnSlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(Inventory inventory, int index, Item before, Item after)
    {
        UpdateBuildingLights();
    }

    internal void UpdateBuildingLights()
    {
        if (chest.Items.HasAny())
        {
            if (
                !Game1.currentLightSources.ContainsKey(lightName)
                && Light.MakeLightFromProps(
                    ArgUtility.SplitBySpaceQuoteAware(lightProps),
                    lightName,
                    new Vector2(building.tileX.Value, building.tileY.Value) * Game1.tileSize
                )
                    is LightSource light
            )
            {
                Game1.currentLightSources.Add(light);
            }
        }
        else
        {
            if (Game1.currentLightSources.ContainsKey(lightName))
            {
                Game1.currentLightSources.Remove(lightName);
            }
        }
    }
}
