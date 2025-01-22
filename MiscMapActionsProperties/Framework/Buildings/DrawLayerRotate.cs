using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;

namespace MiscMapActionsProperties.Framework.Buildings;

/// <summary>Holds info about rotate and origin for draw</summary>
/// <param name="RotateRate"></param>
/// <param name="OriginX"></param>
/// <param name="OriginY"></param>
internal record DrawRotate(float RotateRate, float OriginX, float OriginY)
{
    internal float Current { get; private set; } = 0f;
    internal void Update() => Current = (Current + RotateRate / 60f) % (2 * MathF.PI);
}

/// <summary>
/// Add new BuildingData.Metadata mushymato.MMAP/DrawLayerRotate.<DrawLayerId>: <rotation> <originX> <originY>
/// Rotates the layer by rotation every second (rotation/60 every tick) around originX, originY
/// Can be used with regular draw layer things.
/// </summary>
internal static class DrawLayerRotate
{
    internal static readonly string Metadata_DrawLayerRotate_Prefix = $"{ModEntry.ModId}/DrawLayerRotate.";

    private static readonly Dictionary<BuildingDrawLayer, DrawRotate> drawLayerRotateCache = [];

    internal static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        AddBuildingDrawLayerRotate(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        drawLayerRotateCache.Clear();
        AddBuildingDrawLayerRotate(e.NewLocation);
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (DrawRotate value in drawLayerRotateCache.Values)
        {
            value.Update();
        }
    }

    internal static void AddBuildingDrawLayerRotate(GameLocation location)
    {
        foreach (Building building in location.buildings)
        {
            BuildingData data = building.GetData();
            if (data == null || data.DrawLayers == null)
                continue;
            foreach (BuildingDrawLayer drawLayer in data.DrawLayers)
            {
                if (drawLayer.Id == null)
                    continue;
                string drawRotate = $"{Metadata_DrawLayerRotate_Prefix}{drawLayer.Id}";
                if (data.Metadata.TryGetValue(drawRotate, out string? rotateStr))
                {
                    string[] args = ArgUtility.SplitBySpace(rotateStr);
                    if (
                        ArgUtility.TryGetFloat(args, 0, out float rotateRate, out string _, "float rotateRate")
                        && ArgUtility.TryGetFloat(args, 1, out float originX, out string _, name: "float originX")
                        && ArgUtility.TryGetFloat(args, 2, out float originY, out string _, name: "float originY")
                    )
                    {
                        drawLayerRotateCache[drawLayer] = new(rotateRate, originX, originY);
                    }
                }
            }
        }
    }

    internal static void Patch(Harmony harmony)
    {
        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.draw)),
                transpiler: new HarmonyMethod(typeof(DrawLayerRotate), nameof(Building_draw_Transpiler))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch DrawLayerRotate:\n{err}", LogLevel.Error);
        }
    }

    internal static float GetRotate(float original, BuildingDrawLayer drawLayer)
    {
        if (drawLayerRotateCache.TryGetValue(drawLayer, out DrawRotate? value))
            return value.Current;
        return original;
    }

    internal static float GetOriginX(float original, BuildingDrawLayer drawLayer)
    {
        if (drawLayerRotateCache.TryGetValue(drawLayer, out DrawRotate? value))
            return value.OriginX;
        return original;
    }

    internal static float GetOriginY(float original, BuildingDrawLayer drawLayer)
    {
        if (drawLayerRotateCache.TryGetValue(drawLayer, out DrawRotate? value))
            return value.OriginY;
        return original;
    }

    private static IEnumerable<CodeInstruction> Building_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // find drawlayerloc
            // IL_079d: ldsfld class StardewValley.LocalizedContentManager StardewValley.Game1::content
            // IL_07a2: ldloc.s 17
            // IL_07a4: ldfld string [StardewValley.GameData]StardewValley.GameData.Buildings.BuildingDrawLayer::Texture
            matcher
                .MatchStartForward(
                    [
                        new(OpCodes.Ldsfld, AccessTools.Field(typeof(Game1), nameof(Game1.content))),
                        new((inst) => inst.IsLdloc()),
                        new(
                            OpCodes.Ldfld,
                            AccessTools.Field(typeof(BuildingDrawLayer), nameof(BuildingDrawLayer.Texture))
                        ),
                    ]
                )
                .ThrowIfNotMatch("Did not find BuildingDrawLayer local");
            CodeInstruction drawLayerLoc = matcher.InstructionAt(1);
            // find patch
            // IL_07fa: ldc.r4 0.0
            // IL_07ff: ldc.r4 0.0
            // IL_0804: ldc.r4 0.0
            // IL_0809: newobj instance void [MonoGame.Framework]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
            matcher
                .MatchStartForward(
                    [new(OpCodes.Ldc_R4, 0f), new(OpCodes.Ldc_R4, 0f), new(OpCodes.Ldc_R4, 0f), new(OpCodes.Newobj)]
                )
                .ThrowIfNotMatch("Did not find Draw(... 0f, new Vector2(0f, 0f) ...)");
            matcher
                .Advance(1)
                .InsertAndAdvance(
                    [
                        new(drawLayerLoc.opcode, drawLayerLoc.operand),
                        new(OpCodes.Call, AccessTools.Method(typeof(DrawLayerRotate), nameof(GetRotate))),
                    ]
                )
                .Advance(1)
                .InsertAndAdvance(
                    [
                        new(drawLayerLoc.opcode, drawLayerLoc.operand),
                        new(OpCodes.Call, AccessTools.Method(typeof(DrawLayerRotate), nameof(GetOriginX))),
                    ]
                )
                .Advance(1)
                .InsertAndAdvance(
                    [
                        new(drawLayerLoc.opcode, drawLayerLoc.operand),
                        new(OpCodes.Call, AccessTools.Method(typeof(DrawLayerRotate), nameof(GetOriginY))),
                    ]
                );

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }
}
