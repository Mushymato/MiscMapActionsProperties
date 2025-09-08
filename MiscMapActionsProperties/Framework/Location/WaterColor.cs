using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiscMapActionsProperties.Framework.Wheels;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MiscMapActionsProperties.Framework.Location;

public sealed record WaterDrawCtx(Texture2D Tx, Point Pnt, float Scale);

/// <summary>
/// Add new map property mushymato.MMAP_WaterColor T|color [T|color T|color T|color]
/// Overrides the watercolor
/// Can provide 4 colors for seasonal
/// </summary>
internal static class WaterColor
{
    internal const string Asset_Water = $"{ModEntry.ModId}/Water";
    internal const string MapProp_WaterColor = $"{ModEntry.ModId}_WaterColor";
    internal const string MapProp_WaterDraw = $"{ModEntry.ModId}_WaterDraw";

    // abusing the fact that content patcher always does a copy to not actually invalidate these :)
    private static WaterDrawCtx? T_WaterCtx = null;
    private static Rectangle T_Rect = new(0, 0, 640, 256);

    private static readonly PerScreenCache<WaterDrawCtx?> psWaterCtx = PerScreenCache.Make<WaterDrawCtx?>();
    private static WaterDrawCtx? WaterCtx
    {
        get => psWaterCtx.Value;
        set => psWaterCtx.Value = value;
    }

    internal static void Register()
    {
        CommonPatch.GameLocation_resetLocalState += GameLocation_resetLocalState_Postfix;
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
        ModEntry.help.Events.GameLoop.GameLaunched += OnGameLaunched;
        try
        {
            ModEntry.harm.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(GameLocation),
                    nameof(GameLocation.drawWaterTile),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(Color)]
                ),
                transpiler: new HarmonyMethod(typeof(WaterColor), nameof(GameLocation_drawWaterTile_Transpiler))
            );
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WaterDraw:\n{err}", LogLevel.Error);
        }
    }

    private static void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (
            ModEntry.help.ModRegistry.Get("blueberry.WaterFlow") is not IModInfo modInfo
            || modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo) is not IMod mod
        )
        {
            return;
        }
        Assembly assembly = mod.GetType().Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.Name.Contains("DisplayClass"))
                continue;
            foreach (MethodInfo methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!methodInfo.Name.StartsWith("<GameLocation_DrawWaterTile_Prefix>g__draw"))
                {
                    continue;
                }
                ModEntry.Log($"Patching blueberry.WaterFlow: {methodInfo}", LogLevel.Warn);
                ModEntry.harm.Patch(
                    methodInfo,
                    transpiler: new HarmonyMethod(typeof(WaterColor), nameof(GameLocation_drawWaterTile_Transpiler))
                );
            }
        }
    }

    private static void DrawReplace(
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
        if (WaterCtx is WaterDrawCtx ctx && sourceRectangle is Rectangle rect)
        {
            Rectangle overrideRect;
            float overrideScale = ctx.Scale;
            float scaleMod = scale / ctx.Scale;
            overrideRect = new Rectangle(
                (int)(ctx.Pnt.X + rect.X * scaleMod),
                ctx.Pnt.Y + (int)((rect.Y - 2064) * scaleMod),
                (int)(rect.Width * scaleMod),
                (int)(rect.Height * scaleMod)
            );
            b.Draw(ctx.Tx, position, overrideRect, color, rotation, origin, overrideScale, effects, layerDepth);
            return;
        }
        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    private static IEnumerable<CodeInstruction> GameLocation_drawWaterTile_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo replacedDraw = AccessTools.DeclaredMethod(typeof(WaterColor), nameof(DrawReplace));
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
            for (int i = 0; i < 2; i++)
            {
                matcher.MatchEndForward(callvirtDraw);
                if (matcher.IsInvalid)
                    break;
                matcher.Opcode = OpCodes.Call;
                matcher.Operand = replacedDraw;
            }
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Failed to patch WaterDraw:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(Asset_Water))
        {
            e.LoadFromModFile<Texture2D>("assets/base_water.png", AssetLoadPriority.Exclusive);
        }
    }

    private static void GameLocation_resetLocalState_Postfix(object? sender, GameLocation location)
    {
        // water color
        if (CommonPatch.TryGetLocationalProperty(location, MapProp_WaterColor, out string? waterColors))
        {
            SetupWaterColor(location, waterColors);
        }

        WaterCtx = null;
        if (CommonPatch.TryGetLocationalProperty(location, MapProp_WaterDraw, out string? waterDraw))
        {
            SetupWaterDraw(waterDraw);
        }
    }

    private static void SetupWaterDraw(string waterDraw)
    {
        if (waterDraw == "T")
        {
            T_WaterCtx ??= new(Game1.content.Load<Texture2D>(Asset_Water), Point.Zero, 1f);
            WaterCtx = T_WaterCtx;
            return;
        }
        string[] args = ArgUtility.SplitBySpaceQuoteAware(waterDraw);

        if (
            !ArgUtility.TryGet(args, 0, out string waterDrawTx, out string _, allowBlank: false, "string waterDrawTx")
            || !Game1.content.DoesAssetExist<Texture2D>(waterDrawTx)
        )
        {
            ModEntry.Log($"Failed to get water texture '{waterDrawTx}'", LogLevel.Error);
            return;
        }
        if (
            !ArgUtility.TryGetOptionalFloat(args, 1, out float scale, out _, defaultValue: 1, name: "float scale")
            || scale <= 0
        )
        {
            ModEntry.Log($"Failed to get water draw scale", LogLevel.Error);
            return;
        }
        ArgUtility.TryGetPoint(args, 2, out Point sourcePnt, out _, name: "point source");

        Texture2D waterTx = Game1.content.Load<Texture2D>(waterDrawTx);
        WaterCtx = new(waterTx, sourcePnt, scale);
    }

    private static void SetupWaterColor(GameLocation location, string waterColors)
    {
        string[] args = ArgUtility.SplitBySpaceQuoteAware(waterColors);
        Season season = location.GetSeason();
        Color? waterColorOverride = null;

        if (
            ArgUtility.TryGet(
                args,
                (int)season,
                out string seasonColor,
                out _,
                allowBlank: false,
                name: "string seasonWaterColor"
            )
        )
        {
            if (seasonColor != "T")
                waterColorOverride = Utility.StringToColor(seasonColor);
        }

        if (
            !waterColorOverride.HasValue
            && ArgUtility.TryGet(
                args,
                0,
                out string springColor,
                out _,
                allowBlank: false,
                name: "string seasonWaterColor"
            )
        )
        {
            if (springColor != "T")
                waterColorOverride = Utility.StringToColor(springColor);
        }

        if (waterColorOverride.HasValue)
        {
            location.waterColor.Value = waterColorOverride.Value;
        }
    }
}
