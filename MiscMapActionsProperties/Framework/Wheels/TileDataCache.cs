using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace MiscMapActionsProperties.Framework.Wheels;

/// <summary>
/// CWT cache for tile data props
/// </summary>
internal sealed class TileDataCache<TProps>
{
    private readonly ConditionalWeakTable<xTile.Map, Dictionary<Vector2, TProps>> _cache = [];
    private Rectangle queuedUpdateRect = Rectangle.Empty;
    private readonly string propKey;
    private readonly Func<string, MapTile, TProps?> propsValueGetter;
    private readonly string[] layers;

    internal TileDataCache(string propKey, string[] layers, Func<string, MapTile, TProps?> propsValueGetter)
    {
        this.propKey = propKey;
        this.layers = layers;
        this.propsValueGetter = propsValueGetter;
        ModEntry.help.Events.GameLoop.ReturnedToTitle += ClearCache;
        CommonPatch.GameLocation_ApplyMapOverride += GameLocation_ApplyMapOverride_Postfix;
    }

    private void ClearCache(object? sender, EventArgs e) => _cache.Clear();

    private Dictionary<Vector2, TProps> CreateProps(xTile.Map map)
    {
        Dictionary<Vector2, TProps> cacheEntry = [];
        foreach (string layer in layers)
        {
            foreach ((Vector2 pos, MapTile tile) in CommonPatch.IterateMapTiles(map, layer))
            {
                if (propsValueGetter(propKey, tile) is TProps propValue)
                {
                    cacheEntry[pos] = propValue;
                }
            }
        }
        return cacheEntry;
    }

    private void GameLocation_ApplyMapOverride_Postfix(object? sender, CommonPatch.ApplyMapOverrideArgs e)
    {
        QueueUpdateProps(e.Location.Map, e.DestRect);
    }

    internal void QueueUpdateProps(xTile.Map map, Rectangle rectangle)
    {
        if (map == null)
            return;
        Dictionary<Vector2, TProps> cacheEntry = _cache.GetValue(map, CreateProps);
        if (queuedUpdateRect.IsEmpty)
        {
            queuedUpdateRect = rectangle;
        }
        else if (!queuedUpdateRect.Contains(rectangle))
        {
            queuedUpdateRect = Rectangle.Union(queuedUpdateRect, rectangle);
        }
    }

    internal Dictionary<Vector2, TProps> GetProps(xTile.Map map)
    {
        if (map == null)
            return [];
        Dictionary<Vector2, TProps> cacheEntry = _cache.GetValue(map, CreateProps);
        if (!queuedUpdateRect.IsEmpty)
        {
            foreach (string layer in layers)
            {
                foreach ((Vector2 pos, MapTile tile) in CommonPatch.IterateMapTilesInRect(map, layer, queuedUpdateRect))
                {
                    if (propsValueGetter(propKey, tile) is TProps propValue)
                    {
                        cacheEntry[pos] = propValue;
                    }
                    else
                    {
                        cacheEntry.Remove(pos);
                    }
                }
            }
        }
        return cacheEntry;
    }
}
