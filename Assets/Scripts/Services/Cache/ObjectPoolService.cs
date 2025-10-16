using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic object pooling service for GameObject reuse
/// Reduces GC pressure and improves performance
/// </summary>
public class ObjectPoolService
{
    private readonly Dictionary<string, Queue<GameObject>> _pools;
    private readonly Dictionary<string, GameObject> _prefabs;
    private readonly Transform _poolRoot;
    private readonly int _defaultCapacity;

    public ObjectPoolService(Transform poolRoot = null, int defaultCapacity = 20)
    {
        _pools = new Dictionary<string, Queue<GameObject>>();
        _prefabs = new Dictionary<string, GameObject>();
        _defaultCapacity = defaultCapacity;

        // Create pool root if not provided
        if (poolRoot == null)
        {
            var poolRootGO = new GameObject("ObjectPool");
            poolRootGO.SetActive(false); // Keep pooled objects inactive
            _poolRoot = poolRootGO.transform;
        }
        else
        {
            _poolRoot = poolRoot;
        }
    }

    /// <summary>
    /// Registers a prefab for pooling
    /// </summary>
    public void RegisterPrefab(string key, GameObject prefab, int preWarmCount = 0)
    {
        if (string.IsNullOrEmpty(key) || prefab == null)
            return;

        _prefabs[key] = prefab;

        if (!_pools.ContainsKey(key))
        {
            _pools[key] = new Queue<GameObject>(_defaultCapacity);
        }

        // Pre-warm the pool
        for (int i = 0; i < preWarmCount; i++)
        {
            var obj = Object.Instantiate(prefab, _poolRoot);
            obj.SetActive(false);
            _pools[key].Enqueue(obj);
        }
    }

    /// <summary>
    /// Gets an object from the pool or creates a new one
    /// </summary>
    public GameObject Get(string key, Transform parent = null)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        // Get from pool if available
        if (_pools.TryGetValue(key, out var pool) && pool.Count > 0)
        {
            var obj = pool.Dequeue();
            obj.transform.SetParent(parent, false);
            obj.SetActive(true);
            return obj;
        }

        // Create new if pool is empty
        if (_prefabs.TryGetValue(key, out var prefab))
        {
            var obj = Object.Instantiate(prefab, parent);
            obj.name = prefab.name;
            return obj;
        }

        return null;
    }

    /// <summary>
    /// Returns an object to the pool
    /// </summary>
    public void Return(string key, GameObject obj)
    {
        if (string.IsNullOrEmpty(key) || obj == null)
            return;

        if (!_pools.ContainsKey(key))
        {
            _pools[key] = new Queue<GameObject>(_defaultCapacity);
        }

        obj.SetActive(false);
        obj.transform.SetParent(_poolRoot, false);
        _pools[key].Enqueue(obj);
    }

    /// <summary>
    /// Clears a specific pool
    /// </summary>
    public void ClearPool(string key, bool destroyObjects = false)
    {
        if (!_pools.TryGetValue(key, out var pool))
            return;

        if (destroyObjects)
        {
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                    Object.Destroy(obj);
            }
        }

        pool.Clear();
    }

    /// <summary>
    /// Clears all pools
    /// </summary>
    public void ClearAll(bool destroyObjects = false)
    {
        foreach (var key in _pools.Keys)
        {
            ClearPool(key, destroyObjects);
        }

        if (destroyObjects)
        {
            _pools.Clear();
            _prefabs.Clear();
        }
    }

    /// <summary>
    /// Gets pool statistics
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        int totalPooled = 0;
        foreach (var pool in _pools.Values)
        {
            totalPooled += pool.Count;
        }

        return new PoolStatistics
        {
            PoolCount = _pools.Count,
            TotalPooledObjects = totalPooled,
            RegisteredPrefabs = _prefabs.Count,
        };
    }
}

public struct PoolStatistics
{
    public int PoolCount;
    public int TotalPooledObjects;
    public int RegisteredPrefabs;

    public override string ToString()
    {
        return $"Pools: {PoolCount}, Pooled Objects: {TotalPooledObjects}, Prefabs: {RegisteredPrefabs}";
    }
}
