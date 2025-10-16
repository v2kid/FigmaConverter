using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-performance sprite caching service with LRU eviction policy
/// Reduces memory usage and improves sprite loading performance
/// </summary>
public class SpriteCacheService
{
    private readonly Dictionary<string, CachedSprite> _cache;
    private readonly LinkedList<string> _lruList;
    private readonly int _maxCacheSize;
    private int _currentSize;

    public int MaxCacheSize => _maxCacheSize;
    public int CurrentSize => _currentSize;
    public int ItemCount => _cache.Count;

    public SpriteCacheService(int maxCacheSize = 100)
    {
        _maxCacheSize = maxCacheSize;
        _cache = new Dictionary<string, CachedSprite>(maxCacheSize);
        _lruList = new LinkedList<string>();
        _currentSize = 0;
    }

    /// <summary>
    /// Adds or updates a sprite in the cache
    /// </summary>
    public void Add(string key, Sprite sprite)
    {
        if (sprite == null || string.IsNullOrEmpty(key))
            return;

        // If sprite already exists, remove it first
        if (_cache.ContainsKey(key))
        {
            Remove(key);
        }

        // Calculate sprite size (rough estimate)
        int spriteSize = CalculateSpriteSize(sprite);

        // Evict items if needed
        while (_currentSize + spriteSize > _maxCacheSize && _lruList.Count > 0)
        {
            EvictLRU();
        }

        // Add new sprite
        var node = _lruList.AddFirst(key);
        _cache[key] = new CachedSprite
        {
            Sprite = sprite,
            Size = spriteSize,
            LRUNode = node,
            AccessCount = 1,
        };
        _currentSize += spriteSize;
    }

    /// <summary>
    /// Gets a sprite from cache. Returns null if not found
    /// </summary>
    public Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key) || !_cache.TryGetValue(key, out var cached))
            return null;

        // Move to front (most recently used)
        _lruList.Remove(cached.LRUNode);
        cached.LRUNode = _lruList.AddFirst(key);
        cached.AccessCount++;

        return cached.Sprite;
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
        if (string.IsNullOrEmpty(key) || !_cache.TryGetValue(key, out var cached))
            return;

        _lruList.Remove(cached.LRUNode);
        _currentSize -= cached.Size;
        _cache.Remove(key);
    }

    /// <summary>
    /// Clears the entire cache
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _lruList.Clear();
        _currentSize = 0;
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalItems = _cache.Count,
            TotalSize = _currentSize,
            MaxSize = _maxCacheSize,
            UsagePercentage = (float)_currentSize / _maxCacheSize * 100f,
        };
    }

    private void EvictLRU()
    {
        if (_lruList.Count == 0)
            return;

        string key = _lruList.Last.Value;
        Remove(key);
    }

    private int CalculateSpriteSize(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return 1;

        // Rough estimate: width * height * bytes per pixel
        int width = sprite.texture.width;
        int height = sprite.texture.height;
        int bytesPerPixel = 4; // RGBA32

        return (width * height * bytesPerPixel) / (1024 * 1024); // Size in MB
    }

    private class CachedSprite
    {
        public Sprite Sprite;
        public int Size;
        public LinkedListNode<string> LRUNode;
        public int AccessCount;
    }
}

public struct CacheStatistics
{
    public int TotalItems;
    public int TotalSize;
    public int MaxSize;
    public float UsagePercentage;

    public override string ToString()
    {
        return $"Cache: {TotalItems} items, {TotalSize}/{MaxSize} MB ({UsagePercentage:F1}%)";
    }
}
