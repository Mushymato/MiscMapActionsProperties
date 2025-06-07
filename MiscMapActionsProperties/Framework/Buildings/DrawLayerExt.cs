using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;

namespace MiscMapActionsProperties.Framework.Buildings;

/// <summary>Holds info about draw layer</summary>
/// <param name="RotateRate"></param>
/// <param name="OriginX"></param>
/// <param name="OriginY"></param>
internal record DLExtInfo(
    float Alpha,
    float Rotate,
    float RotateRate,
    Vector2 Origin,
    float Scale,
    SpriteEffects Effect,
    string? GSQ
)
{
    internal bool ShouldDraw { get; private set; } = GSQ == null || GameStateQuery.CheckConditions(GSQ);
    internal float CurrRotate { get; private set; } = Rotate;

    internal void UpdateTicked()
    {
        if (RotateRate > 0)
            CurrRotate = (CurrRotate + RotateRate / 60f) % MathF.Tau;
    }

    internal void TimeChanged()
    {
        ShouldDraw = GSQ == null || GameStateQuery.CheckConditions(GSQ);
    }

    internal void Draw(
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
        if (!ShouldDraw)
            return;
        b.Draw(
            texture,
            position,
            sourceRectangle,
            color * Alpha,
            CurrRotate,
            Origin,
            Scale,
            effects ^ Effect,
            layerDepth
        );
        return;
    }
}

/// <summary>
/// Add new BuildingData.Metadata mushymato.MMAP/DrawLayer.<DrawLayerId>: <rotation> <originX> <originY>
/// Rotates the layer by rotation every second (rotation/60 every tick) around originX, originY
/// Can be used with regular draw layer things.
/// </summary>
internal static class DrawLayerExt
{
    internal const string Metadata_DrawLayer_Prefix = $"{ModEntry.ModId}/DrawLayer.";
    private static readonly PerScreen<Dictionary<string, DLExtInfo>> dlExtInfoCacheImpl = new();
    private static Dictionary<string, DLExtInfo> DlExtInfoCache => dlExtInfoCacheImpl.Value ??= [];
    private static readonly PerScreen<Dictionary<string, DLExtInfo>> dlExtInfoInMenuImpl = new();
    private static Dictionary<string, DLExtInfo> DlExtInfoInMenu => dlExtInfoInMenuImpl.Value ??= [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.Display.MenuChanged += OnMenuChanged;
        ModEntry.help.Events.World.BuildingListChanged += OnBuildingListChanged;

        try
        {
            // map draws
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.draw)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
                {
                    after = ["mouahrara.FlipBuildings"],
                }
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.drawBackground)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
                {
                    after = ["mouahrara.FlipBuildings"],
                }
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch DrawLayerExt(core draw methods):\n{err}", LogLevel.Error);
            return;
        }

        try
        {
            // method draws
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.drawInConstruction)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.drawInMenu)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_drawInMenu_Transpiler))
            );
            ModEntry.harm.Patch(
                original: AccessTools.Method(
                    typeof(CarpenterMenu),
                    nameof(CarpenterMenu.SetNewActiveBlueprint),
                    [typeof(int)]
                ),
                postfix: new HarmonyMethod(typeof(DrawLayerExt), nameof(CarpenterMenu_SetNewActiveBlueprint_Postfix))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch DrawLayerExt(nice to have draws):\n{err}", LogLevel.Warn);
        }
    }

    private static void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (e.IsCurrentLocation)
            AddBuildingDrawLayer(Game1.currentLocation);
    }

    private static void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (e.OldMenu is CarpenterMenu)
            DlExtInfoInMenu.Clear();
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        AddBuildingDrawLayer(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        DlExtInfoCache.Clear();
        AddBuildingDrawLayer(e.NewLocation);
    }

    private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (DLExtInfo value in DlExtInfoCache.Values)
            value.UpdateTicked();
        foreach (DLExtInfo value in DlExtInfoInMenu.Values)
            value.UpdateTicked();
    }

    private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        foreach (DLExtInfo value in DlExtInfoCache.Values)
            value.TimeChanged();
        foreach (DLExtInfo value in DlExtInfoInMenu.Values)
            value.TimeChanged();
    }

    internal static void AddBuildingDrawLayer(GameLocation location)
    {
        if (Game1.currentLocation == null)
            return;
        foreach (Building building in location.buildings)
        {
            BuildingData data = building.GetData();
            if (data == null || data.DrawLayers == null)
                continue;
            foreach (BuildingDrawLayer drawLayer in data.DrawLayers)
            {
                if (drawLayer.Id == null)
                    continue;

                if (TryGetDRExtInfo(data, drawLayer, out DLExtInfo? dlExtInfo))
                    DlExtInfoCache[$"{building.id.Value}/{drawLayer.Id}"] = dlExtInfo;
            }
        }
    }

    private static bool TryGetFloatOrRandom(
        this Dictionary<string, string> dict,
        string key,
        out float value,
        float defaultValue
    )
    {
        value = defaultValue;
        if (dict.TryGetValue(key, out string? valueStr))
        {
            string[] args = ArgUtility.SplitBySpace(valueStr);
            if (ArgUtility.TryGetFloat(args, 0, out float randMin, out _, name: "float randMin"))
                value = randMin;
            else
                return false;
            if (ArgUtility.TryGetFloat(args, 1, out float randMax, out _, name: "float randMax"))
                value = Random.Shared.NextSingle(randMin, randMax);
            return true;
        }
        return false;
    }

    internal static bool TryGetDRExtInfo(
        BuildingData data,
        BuildingDrawLayer drawLayer,
        [NotNullWhen(true)] out DLExtInfo? dlExtInfo
    )
    {
        dlExtInfo = null;
        string drawRotatePrefix = $"{Metadata_DrawLayer_Prefix}{drawLayer.Id}.";

        Vector2 origin = Vector2.Zero;
        SpriteEffects effect = SpriteEffects.None;

        bool hasChange = data.Metadata.TryGetFloatOrRandom(
            string.Concat(drawRotatePrefix, "alpha"),
            out float alpha,
            1f
        );
        hasChange |= data.Metadata.TryGetFloatOrRandom(string.Concat(drawRotatePrefix, "rotate"), out float rotate, 0f);
        hasChange |= data.Metadata.TryGetFloatOrRandom(
            string.Concat(drawRotatePrefix, "rotateRate"),
            out float rotateRate,
            0f
        );
        if (data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "origin"), out string? valueStr))
        {
            string[] args = ArgUtility.SplitBySpace(valueStr);
            hasChange |= ArgUtility.TryGetVector2(
                args,
                0,
                out origin,
                out string _,
                integerOnly: true,
                name: "Vector2 origin"
            );
        }
        hasChange |= data.Metadata.TryGetFloatOrRandom(string.Concat(drawRotatePrefix, "scale"), out float scale, 4f);
        hasChange |=
            data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "effect"), out valueStr)
            && SpriteEffects.TryParse(valueStr, out effect);
        hasChange |= data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "condition"), out string? GSQ);

        if (hasChange)
            dlExtInfo = new(alpha, rotate, rotateRate, origin, scale, effect, GSQ);

        return hasChange;
    }

    private static void CarpenterMenu_SetNewActiveBlueprint_Postfix(CarpenterMenu __instance)
    {
        Building building = __instance.currentBuilding;
        BuildingData data = building.GetData();
        if (data == null || data.DrawLayers == null)
            return;
        foreach (BuildingDrawLayer drawLayer in data.DrawLayers)
        {
            if (drawLayer.Id == null)
                continue;

            if (TryGetDRExtInfo(data, drawLayer, out DLExtInfo? dlExtInfo))
            {
                DlExtInfoInMenu[$"{building.buildingType.Value}+{drawLayer.Id}"] = dlExtInfo;
                ModEntry.LogOnce($"{building.buildingType.Value}+{drawLayer.Id}");
            }
        }
    }

    internal static void DrawLayerOverride(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth,
        Building building,
        BuildingDrawLayer drawLayer
    )
    {
        if (DlExtInfoCache.TryGetValue($"{building.id.Value}/{drawLayer.Id}", out DLExtInfo? value))
        {
            value.Draw(b, texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
            return;
        }

        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    internal static void DrawLayerOverrideInMenu(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth,
        Building building,
        BuildingDrawLayer drawLayer
    )
    {
        if (DlExtInfoInMenu.TryGetValue($"{building.buildingType.Value}+{drawLayer.Id}", out DLExtInfo? value))
        {
            value.Draw(b, texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
            return;
        }
        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    private static void Building_draw_Transpiler_shared(CodeMatcher matcher)
    {
        // Targets this block
        // if (drawLayer.Texture != null)
        // {
        //     texture2D = Game1.content.Load<Texture2D>(drawLayer.Texture);
        // }
        // b.Draw(..., rotate, new Vector2(originX, originY), ...)

        // find drawlayerloc
        // IL_079d: ldsfld class StardewValley.LocalizedContentManager StardewValley.Game1::content
        // IL_07a2: ldloc.s 17
        // IL_07a4: ldfld string [StardewValley.GameData]StardewValley.GameData.Buildings.BuildingDrawLayer::Texture
        matcher.Start();
        matcher
            .MatchStartForward(
                [
                    new(OpCodes.Ldsfld, AccessTools.Field(typeof(Game1), nameof(Game1.content))),
                    new((inst) => inst.IsLdloc()),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(BuildingDrawLayer), nameof(BuildingDrawLayer.Texture))),
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
            .MatchEndForward(
                [
                    new((inst) => inst.IsLdloc() || (inst.opcode == OpCodes.Ldc_R4 && (float)inst.operand == 0f)),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.Method(
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
                ]
            )
            .InsertAndAdvance([new(OpCodes.Ldarg_0), new(drawLayerLoc.opcode, drawLayerLoc.operand)]);
        matcher.Opcode = OpCodes.Call;
    }

    private static IEnumerable<CodeInstruction> Building_drawInMenu_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            Building_draw_Transpiler_shared(matcher);
            matcher.Operand = AccessTools.Method(typeof(DrawLayerExt), nameof(DrawLayerOverrideInMenu));
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_drawInMenu_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> Building_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            Building_draw_Transpiler_shared(matcher);
            matcher.Operand = AccessTools.Method(typeof(DrawLayerExt), nameof(DrawLayerOverride));
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }
}
