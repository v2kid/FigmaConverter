using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple sprite caching service - stores all sprites for the lifetime of a conversion run.
/// No eviction: the cache is cleared explicitly after conversion finishes.
/// </summary>
public class SpriteCacheService
{
    private readonly Dictionary<string, Sprite> _cache;

    public int ItemCount => _cache.Count;

    public SpriteCacheService()
    {
        _cache = new Dictionary<string, Sprite>();
    }

    /// <summary>
    /// Adds or updates a sprite in the cache
    /// </summary>
    public void Add(string key, Sprite sprite)
    {
        if (sprite == null || string.IsNullOrEmpty(key))
            return;

        _cache[key] = sprite;
    }

    /// <summary>
    /// Gets a sprite from cache. Returns null if not found.
    /// </summary>
    public Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        return _cache.TryGetValue(key, out var sprite) ? sprite : null;
    }

    /// <summary>
    /// Checks if a sprite exists in cache
    /// </summary>
    public bool Contains(string key)
    {
        return !string.IsNullOrEmpty(key) && _cache.ContainsKey(key);
    }

    /// <summary>
    /// Removes a sprite from cache
    /// </summary>
    public void Remove(string key)
    {
        if (!string.IsNullOrEmpty(key))
            _cache.Remove(key);
    }

    /// <summary>
    /// Clears the entire cache
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
}
