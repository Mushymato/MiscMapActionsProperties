using StardewModdingAPI;
using StardewModdingAPI.Utilities;

// is this more perf? i have no clue lol
internal sealed class PerScreenCache<T>(PerScreen<T> perScreen)
{
    private int lastScreenId = Context.ScreenId;
    private T lastValue = perScreen.Value;

    internal T Value
    {
        get
        {
            if (lastScreenId != Context.ScreenId)
            {
                lastScreenId = Context.ScreenId;
                lastValue = perScreen.Value;
            }
            return lastValue;
        }
        set
        {
            perScreen.Value = value;
            lastValue = value;
            lastScreenId = Context.ScreenId;
        }
    }
}

internal static class PerScreenCache
{
    internal static PerScreenCache<T> Make<T>(Func<T>? CreateNewState = null)
    {
        if (CreateNewState != null)
            return new PerScreenCache<T>(new PerScreen<T>(CreateNewState));
        return new PerScreenCache<T>(new PerScreen<T>());
    }
}
