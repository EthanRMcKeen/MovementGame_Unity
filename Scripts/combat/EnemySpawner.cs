using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private NetworkObject _enemyPrefab;
    [SerializeField] [Min(1)] private int _spawnCountOnStart = 3;
    [SerializeField] [Min(1)] private int _maxTrackedEnemies = 3;
    [SerializeField] [Min(0f)] private float _spawnIntervalSeconds = 5f;
    [SerializeField] private bool _spawnOnServerStart = true;
    [SerializeField] private bool _respawnToMaintainCount = true;
    [SerializeField] private bool _replaceDefeatedEnemies;

    [Header("Spawn Area")]
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private bool _useRandomSpawnPoint = true;
    [SerializeField] [Min(0f)] private float _spawnRadius = 0f;
    [SerializeField] private bool _randomizeYaw = true;
    [SerializeField] private bool _alignFeetToSpawnPlane = true;
    [SerializeField] [Min(0f)] private float _spawnPlanePadding = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool _debugLogs;

    private readonly List<NetworkObject> _trackedEnemies = new List<NetworkObject>();

    private bool _serverWasStarted;
    private float _nextSpawnAt;

    public int TrackedEnemyCount { get { return _trackedEnemies.Count; } }

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted)
        {
            if (_serverWasStarted)
            {
                _serverWasStarted = false;
                _trackedEnemies.Clear();
            }

            return;
        }

        if (!_serverWasStarted)
            InitializeServerSpawner();

        CleanupTrackedEnemies();

        if (!_respawnToMaintainCount)
            return;
        if (_trackedEnemies.Count >= Mathf.Max(1, _maxTrackedEnemies))
            return;
        if (Time.time < _nextSpawnAt)
            return;

        SpawnEnemy();
        _nextSpawnAt = Time.time + Mathf.Max(0.01f, _spawnIntervalSeconds);
    }

    public void SpawnNow()
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        CleanupTrackedEnemies();
        if (_trackedEnemies.Count >= Mathf.Max(1, _maxTrackedEnemies))
            return;

        SpawnEnemy();
        _nextSpawnAt = Time.time + Mathf.Max(0.01f, _spawnIntervalSeconds);
    }

    private void InitializeServerSpawner()
    {
        _serverWasStarted = true;
        _nextSpawnAt = Time.time;

        if (!_spawnOnServerStart)
            return;

        int initialCount = Mathf.Clamp(_spawnCountOnStart, 0, Mathf.Max(1, _maxTrackedEnemies));
        for (int i = 0; i < initialCount; i++)
            SpawnEnemy();

        _nextSpawnAt = Time.time + Mathf.Max(0.01f, _spawnIntervalSeconds);
    }

    private void CleanupTrackedEnemies()
    {
        for (int i = _trackedEnemies.Count - 1; i >= 0; i--)
        {
            NetworkObject enemy = _trackedEnemies[i];
            if (enemy == null || !enemy.IsSpawned)
            {
                _trackedEnemies.RemoveAt(i);
                continue;
            }

            if (!_replaceDefeatedEnemies)
                continue;

            DamageHandler damageHandler = enemy.GetComponent<DamageHandler>();
            if (damageHandler == null || !damageHandler.IsDead)
                continue;

            InstanceFinder.ServerManager.Despawn(enemy);
            _trackedEnemies.RemoveAt(i);
        }
    }

    private void SpawnEnemy()
    {
        if (_enemyPrefab == null)
        {
            if (_debugLogs)
                Debug.LogWarning($"{name} cannot spawn enemies because no enemy prefab is assigned.", this);
            return;
        }

        if (InstanceFinder.ServerManager == null)
            return;

        GetSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation);
        NetworkObject enemyInstance = Instantiate(_enemyPrefab, spawnPosition, spawnRotation);
        if (_alignFeetToSpawnPlane)
            AlignInstanceToSpawnPlane(enemyInstance, spawnPosition.y);
        InstanceFinder.ServerManager.Spawn(enemyInstance);
        _trackedEnemies.Add(enemyInstance);

        if (_debugLogs)
            Debug.Log($"{name} spawned {enemyInstance.name}. Tracked={_trackedEnemies.Count}", this);
    }

    private void GetSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        Transform anchor = GetSpawnAnchor();
        Vector3 basePosition = anchor != null ? anchor.position : transform.position;
        Vector3 baseForward = anchor != null ? anchor.forward : transform.forward;

        if (_spawnRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            basePosition += new Vector3(offset.x, 0f, offset.y);
        }

        position = basePosition;

        if (_randomizeYaw)
        {
            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            return;
        }

        baseForward.y = 0f;
        if (baseForward.sqrMagnitude <= 0.001f)
            baseForward = Vector3.forward;

        rotation = Quaternion.LookRotation(baseForward.normalized, Vector3.up);
    }

    private Transform GetSpawnAnchor()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            return transform;

        if (_useRandomSpawnPoint)
        {
            int startIndex = Random.Range(0, _spawnPoints.Length);
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                Transform candidate = _spawnPoints[(startIndex + i) % _spawnPoints.Length];
                if (candidate != null)
                    return candidate;
            }
        }
        else
        {
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                    return _spawnPoints[i];
            }
        }

        return transform;
    }

    private void AlignInstanceToSpawnPlane(NetworkObject enemyInstance, float spawnPlaneY)
    {
        if (enemyInstance == null)
            return;

        Collider[] colliders = enemyInstance.GetComponentsInChildren<Collider>(true);
        bool foundCollider = false;
        float lowestPointY = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;

            foundCollider = true;
            lowestPointY = Mathf.Min(lowestPointY, collider.bounds.min.y);
        }

        if (!foundCollider)
            return;

        float offsetY = (spawnPlaneY + Mathf.Max(0f, _spawnPlanePadding)) - lowestPointY;
        if (Mathf.Abs(offsetY) <= 0.0001f)
            return;

        enemyInstance.transform.position += Vector3.up * offsetY;
    }
}
