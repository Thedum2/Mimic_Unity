using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Mimic.Gameplay.Network.Spawn
{
    internal static class PlayerSpawnPointResolver
    {
        private static readonly Dictionary<int, (Vector3 Position, Quaternion Rotation)> SpawnPointByPlayer = new();
        private static Vector3 _defaultFallbackSpawnPosition = new(0f, 1f, 0f);
        private static Quaternion _defaultFallbackSpawnRotation = Quaternion.identity;

        private const float InvalidSpawnEpsilonSqr = 0.0001f;

        public static void Clear()
        {
            SpawnPointByPlayer.Clear();
        }

        public static void Remove(int playerId)
        {
            SpawnPointByPlayer.Remove(playerId);
        }

        public static void SetSceneFallbackSpawn(Vector3 fallbackPosition, Quaternion fallbackRotation)
        {
            _defaultFallbackSpawnPosition = fallbackPosition;
            _defaultFallbackSpawnRotation = fallbackRotation;
        }

        public static void SetCachedSpawnTransform(PlayerRef player, Vector3 position, Quaternion rotation)
        {
            if (player == PlayerRef.None)
            {
                return;
            }

            SpawnPointByPlayer[player.PlayerId] = (position, rotation);
        }

        public static bool TryGetSpawnTransform(PlayerRef player, out Vector3 position, out Quaternion rotation)
        {
            if (player == PlayerRef.None)
            {
                position = _defaultFallbackSpawnPosition;
                rotation = _defaultFallbackSpawnRotation;
                return false;
            }

            if (SpawnPointByPlayer.TryGetValue(player.PlayerId, out var entry))
            {
                position = entry.Position;
                rotation = entry.Rotation;
                return true;
            }

            position = _defaultFallbackSpawnPosition;
            rotation = _defaultFallbackSpawnRotation;
            return false;
        }

        public static (Vector3 position, Quaternion rotation) ResolveAndCacheSpawnTransform(
            PlayerRef player,
            Transform[] spawnPoints,
            Vector3 fallbackSpawnPosition,
            Quaternion fallbackSpawnRotation)
        {
            SetSceneFallbackSpawn(fallbackSpawnPosition, fallbackSpawnRotation);

            var cached = ResolveSpawnTransformInternal(player, spawnPoints, fallbackSpawnPosition, fallbackSpawnRotation);
            var safe = EnsureSafeSpawnTransform(player, cached.position, cached.rotation, fallbackSpawnPosition, fallbackSpawnRotation);
            SpawnPointByPlayer[player.PlayerId] = (safe.Position, safe.Rotation);
            return safe;
        }

        public static (Vector3 position, Quaternion rotation) ResolveSpawnTransformInternal(
            PlayerRef player,
            Transform[] spawnPoints,
            Vector3 fallbackSpawnPosition,
            Quaternion fallbackSpawnRotation)
        {
            if (player == PlayerRef.None)
            {
                return (fallbackSpawnPosition, fallbackSpawnRotation);
            }

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var validSpawnPoints = new List<Transform>();
                for (var i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] != null)
                    {
                        validSpawnPoints.Add(spawnPoints[i]);
                    }
                }

                if (validSpawnPoints.Count > 0)
                {
                    var normalizedPlayerIndex = player.PlayerId > 0 ? player.PlayerId - 1 : 0;
                    var index = normalizedPlayerIndex % validSpawnPoints.Count;
                    if (index < 0)
                    {
                        index += validSpawnPoints.Count;
                    }

                    var spawnPoint = validSpawnPoints[index];
                    return (spawnPoint.position, spawnPoint.rotation);
                }
            }

            return (fallbackSpawnPosition, fallbackSpawnRotation);
        }

        public static (Vector3 Position, Quaternion Rotation) EnsureSafeSpawnTransform(
            PlayerRef player,
            Vector3 position,
            Quaternion rotation,
            Vector3 fallbackSpawnPosition,
            Quaternion fallbackSpawnRotation)
        {
            if (IsInvalidSpawnPosition(position) == false)
            {
                return (position, rotation);
            }

            var sceneFallback = GetFallbackSpawnTransformFromScene();
            if (sceneFallback.HasValue)
            {
                return (sceneFallback.Value.Position, sceneFallback.Value.Rotation);
            }

            if (_defaultFallbackSpawnPosition.sqrMagnitude > 0.01f)
            {
                return (_defaultFallbackSpawnPosition, _defaultFallbackSpawnRotation);
            }

            return (fallbackSpawnPosition, fallbackSpawnRotation);
        }

        private static bool IsInvalidSpawnPosition(Vector3 position)
        {
            return position.sqrMagnitude < InvalidSpawnEpsilonSqr;
        }

        private static (Vector3 Position, Quaternion Rotation)? GetFallbackSpawnTransformFromScene()
        {
            var allTransforms = Object.FindObjectsOfType<Transform>();
            for (var i = 0; i < allTransforms.Length; i++)
            {
                var candidate = allTransforms[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name.IndexOf("spawnpoint", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (candidate.position, candidate.rotation);
                }
            }

            return null;
        }
    }
}
