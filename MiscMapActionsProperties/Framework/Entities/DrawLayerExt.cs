using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using Mushymato.ExtendedTAS;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;

namespace MiscMapActionsProperties.Framework.Entities;

internal sealed record FloatRange(float Min, float? Max)
{
    internal float Value { get; set; } = Min;

    internal void Recheck()
    {
        Value = Max == null ? Min : Random.Shared.NextSingle(Min, Max.Value);
    }
}

/// <summary>Holds info about draw layer</summary>
/// <param name="RotateRate"></param>
/// <param name="OriginX"></param>
/// <param name="OriginY"></param>
internal sealed record DLExtInfo(
    FloatRange Alpha,
    FloatRange Rotate,
    FloatRange RotateRate,
    Vector2 Origin,
    FloatRange Scale,
    SpriteEffects Effect,
    Color? Color,
    string? GSQ,
    FloatRange ShakeRotate,
    bool OpenAnim
)
{
    internal bool ShouldDraw { get; private set; } = GSQ == null || GameStateQuery.CheckConditions(GSQ);
    internal float CurrRotate { get; private set; } = Rotate.Value;
    internal DLContactState? ContactState = null;

    internal void UpdateTicked()
    {
        if (RotateRate.Value > 0)
        {
            CurrRotate = (CurrRotate + RotateRate.Value / 60f) % MathF.Tau;
        }
        ContactState?.UpdateTicked();
    }

    internal void TimeChanged()
    {
        ShouldDraw = GSQ == null || GameStateQuery.CheckConditions(GSQ);
    }

    internal void StartContact(Rectangle contactBounds, float speed, bool left)
    {
        if (ShakeRotate.Value != 0)
        {
            ContactState ??= new();
            ContactState.StartShaking(speed, left, ShakeRotate.Value);
        }
        if (OpenAnim)
        {
            ContactState ??= new();
            ContactState.StartOpen(contactBounds);
        }
    }

    internal void RecheckRands()
    {
        Alpha.Recheck();
        Rotate.Recheck();
        RotateRate.Recheck();
        Scale.Recheck();
        ShakeRotate.Recheck();
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
            (Color ?? color) * Alpha.Value,
            CurrRotate + rotation,
            Origin,
            scale / 4 * Scale.Value,
            effects ^ Effect,
            layerDepth
        );
        return;
    }
}

internal sealed class DLContactState
{
    // shake
    internal bool Left { get; private set; } = false;
    internal float Rotate { get; private set; } = 0f;
    internal float RotateMax { get; private set; } = 0f;
    internal float RotateRate { get; private set; } = 0f;
    internal int OpenAnimTimeMax { get; private set; } = 0;

    // open/close
    internal enum Phase
    {
        None,
        Closed,
        Opening,
        Opened,
        Closing,
    }

    internal Phase OpenPhase = Phase.None;
    internal Rectangle OpenBounds = Rectangle.Empty;
    internal int AnimTime = 0;

    internal void UpdateTicked()
    {
        if (RotateMax > 0f)
        {
            if (Left)
            {
                Rotate -= RotateRate;
                if (Math.Abs(Rotate) >= RotateMax)
                {
                    Left = false;
                }
            }
            else
            {
                Rotate += RotateRate;
                if (Rotate >= RotateMax)
                {
                    Left = true;
                    Rotate -= RotateRate;
                }
            }
            RotateMax = Math.Max(0f, RotateMax - (float)Math.PI / 300f);
            if (RotateMax <= 0)
            {
                Rotate = 0;
                RotateMax = 0;
                RotateRate = 0;
            }
        }

        if (OpenPhase != Phase.None)
        {
            switch (OpenPhase)
            {
                case Phase.Opened:
                    if (!Game1.player.GetBoundingBox().Intersects(OpenBounds))
                    {
                        OpenPhase = Phase.Closing;
                    }
                    break;
                case Phase.Opening:
                    AnimTime += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                    if (AnimTime >= OpenAnimTimeMax)
                    {
                        AnimTime = OpenAnimTimeMax;
                        OpenPhase = Phase.Opened;
                    }
                    break;
                case Phase.Closing:
                    AnimTime -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                    if (AnimTime <= 0)
                    {
                        AnimTime = 0;
                        OpenPhase = Phase.Closed;
                    }
                    break;
            }
        }
    }

    internal void StartShaking(float speedOfCollision, bool left, float shakeRotate)
    {
        if (RotateMax > 0f || shakeRotate == 0)
            return;

        if (shakeRotate < 0)
        {
            left = !left;
            shakeRotate = Math.Abs(shakeRotate);
        }

        Rotate = 0f;
        RotateRate = (float)(Math.PI / 80f / Math.Min(1f, 5f / speedOfCollision) * shakeRotate);
        RotateMax = (float)(Math.PI / 8f / Math.Min(1f, 5f / speedOfCollision) * shakeRotate);
        Left = left;
    }

    internal void SetOpenAnim(int openAnimTimeMax)
    {
        OpenAnimTimeMax = openAnimTimeMax - 1;
        OpenPhase = Phase.Closed;
    }

    internal void StartOpen(Rectangle contactBounds)
    {
        OpenPhase = Phase.Opening;
        OpenBounds = contactBounds;
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
    private static readonly PerScreenCache<Dictionary<(Guid, string), DLExtInfo>> dlExtInfoCacheImpl =
        PerScreenCache.Make<Dictionary<(Guid, string), DLExtInfo>>();
    private static Dictionary<(Guid, string), DLExtInfo> DlExtInfoCache => dlExtInfoCacheImpl.Value ??= [];
    private static readonly PerScreenCache<Dictionary<(string, string), DLExtInfo>> dlExtInfoInMenuImpl =
        PerScreenCache.Make<Dictionary<(string, string), DLExtInfo>>();
    private static Dictionary<(string, string), DLExtInfo> DlExtInfoInMenu => dlExtInfoInMenuImpl.Value ??= [];

    internal static void Register()
    {
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        ModEntry.help.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        ModEntry.help.Events.GameLoop.TimeChanged += OnTimeChanged;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        ModEntry.help.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        ModEntry.help.Events.Player.Warped += OnWarped;
        ModEntry.help.Events.Display.MenuChanged += OnMenuChanged;
        ModEntry.help.Events.World.BuildingListChanged += OnBuildingListChanged;

        try
        {
            // map draws
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.draw)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
                {
                    after = ["mouahrara.FlipBuildings"],
                }
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.drawBackground)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
                {
                    after = ["mouahrara.FlipBuildings"],
                }
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.isTilePassable)),
                postfix: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_isTilePassable_Postfix))
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
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.drawInConstruction)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_draw_Transpiler))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(typeof(Building), nameof(Building.drawInMenu)),
                transpiler: new HarmonyMethod(typeof(DrawLayerExt), nameof(Building_drawInMenu_Transpiler))
            );
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
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

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        DlExtInfoCache.Clear();
    }

    private static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo("Data/Buildings")))
        {
            DlExtInfoCache.Clear();
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
        DlExtInfoCache.Clear();
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

                if (TryGetDLExtInfo(data, drawLayer, out DLExtInfo? dlExtInfo))
                    DlExtInfoCache[new(building.id.Value, drawLayer.Id)] = dlExtInfo;
            }
        }
    }

    private static bool TryGetFloatRange(
        this Dictionary<string, string> dict,
        string key,
        [NotNullWhen(true)] out FloatRange value,
        float defaultValue
    )
    {
        if (dict.TryGetValue(key, out string? valueStr))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(valueStr);
            if (
                !ArgUtility.TryGetOptionalFloat(
                    args,
                    0,
                    out float randMin,
                    out string error,
                    defaultValue: defaultValue,
                    name: "float randMin"
                )
                || !ArgUtility.TryGetOptionalFloat(
                    args,
                    1,
                    out float randMax,
                    out error,
                    defaultValue: randMin - 1,
                    name: "float randMax"
                )
            )
            {
                ModEntry.Log(error, LogLevel.Error);
                value = new FloatRange(defaultValue, null);
                value.Recheck();
                return false;
            }
            value = new FloatRange(randMin, randMax < randMin ? null : randMax);
            value.Recheck();
            return true;
        }
        value = new FloatRange(defaultValue, null);
        value.Recheck();
        return true;
    }

    internal static bool TryGetDLExtInfo(
        BuildingData data,
        BuildingDrawLayer drawLayer,
        [NotNullWhen(true)] out DLExtInfo? dlExtInfo
    )
    {
        dlExtInfo = null;
        string drawRotatePrefix = $"{Metadata_DrawLayer_Prefix}{drawLayer.Id}.";

        Vector2 origin = Vector2.Zero;
        SpriteEffects effect = SpriteEffects.None;
        Color? color = null;

        bool hasChange = data.Metadata.TryGetFloatRange(
            string.Concat(drawRotatePrefix, "alpha"),
            out FloatRange alpha,
            1f
        );
        hasChange |= data.Metadata.TryGetFloatRange(
            string.Concat(drawRotatePrefix, "rotate"),
            out FloatRange rotate,
            0f
        );
        hasChange |= data.Metadata.TryGetFloatRange(
            string.Concat(drawRotatePrefix, "rotateRate"),
            out FloatRange rotateRate,
            0f
        );

        if (data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "origin"), out string? valueStr))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(valueStr);
            hasChange |= ArgUtility.TryGetVector2(
                args,
                0,
                out origin,
                out string _,
                integerOnly: true,
                name: "Vector2 origin"
            );
        }
        hasChange |= data.Metadata.TryGetFloatRange(string.Concat(drawRotatePrefix, "scale"), out FloatRange scale, 4f);
        hasChange |=
            data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "effect"), out valueStr)
            && Enum.TryParse(valueStr, out effect);

        if (data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "color"), out string? colorStr))
        {
            color = Utility.StringToColor(colorStr);
            hasChange |= color != null;
        }

        hasChange |= data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "condition"), out string? GSQ);

        hasChange |= data.Metadata.TryGetFloatRange(
            string.Concat(drawRotatePrefix, "shakeRotate"),
            out FloatRange shakeRotate,
            0f
        );
        bool openAnim = false;
        if (data.Metadata.TryGetValue(string.Concat(drawRotatePrefix, "openAnim"), out string? openAnimStr))
        {
            hasChange |= bool.TryParse(openAnimStr, out openAnim);
        }

        if (hasChange)
        {
            dlExtInfo = new(alpha, rotate, rotateRate, origin, scale, effect, color, GSQ, shakeRotate, openAnim);
            if (openAnim)
            {
                dlExtInfo.ContactState = new();
                dlExtInfo.ContactState.SetOpenAnim(drawLayer.FrameDuration * drawLayer.FrameCount);
            }
        }

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

            if (TryGetDLExtInfo(data, drawLayer, out DLExtInfo? dlExtInfo))
            {
                DlExtInfoInMenu[new(building.buildingType.Value, drawLayer.Id)] = dlExtInfo;
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
        if (DlExtInfoCache.TryGetValue(new(building.id.Value, drawLayer.Id), out DLExtInfo? value))
        {
            if (value.ContactState is DLContactState contactState)
            {
                rotation += contactState.Rotate;
                if (contactState.OpenPhase != DLContactState.Phase.None)
                    sourceRectangle = drawLayer.GetSourceRect(contactState.AnimTime);
            }
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
        if (DlExtInfoInMenu.TryGetValue(new(building.buildingType.Value, drawLayer.Id), out DLExtInfo? value))
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

    private static void Building_isTilePassable_Postfix(Building __instance, ref bool __result)
    {
        if (!__result)
            return;
        if (!Game1.player.isMoving())
            return;

        BuildingData data = __instance.GetData();
        if (data == null || data.DrawLayers == null)
            return;

        float speed = Game1.player.getMovementSpeed();

        Rectangle buildingBounds = CommonPatch.GetBuildingTileDataBounds(__instance, Game1.tileSize);
        Rectangle playerBounds = Game1.player.GetBoundingBox();

        if (!playerBounds.Intersects(buildingBounds))
            return;
        bool left = playerBounds.Center.X > buildingBounds.Center.X;

        foreach (BuildingDrawLayer drawLayer in data.DrawLayers)
        {
            if (drawLayer.Id == null)
                continue;

            if (DlExtInfoCache.TryGetValue(new(__instance.id.Value, drawLayer.Id), out DLExtInfo? dlExt))
            {
                dlExt.StartContact(buildingBounds, speed, left);
            }
        }
    }
}
