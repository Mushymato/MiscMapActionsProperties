using Microsoft.Xna.Framework;

namespace MiscMapActionsProperties.Framework.Wheels;

internal static class Rand
{
    internal static float NextSingle(this Random rand, float min, float max) => min + rand.NextSingle() * (max - min);

    internal static double NextDouble(this Random rand, double min, double max) =>
        min + rand.NextDouble() * (max - min);

    internal static Vector2 NextVector2(this Random rand, Vector2 min, Vector2 max) =>
        new(rand.NextSingle(min.X, max.X), rand.NextSingle(min.Y, max.Y));
}
