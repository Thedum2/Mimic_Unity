using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Mimic.Gameplay.Network.Spawn
{
    internal sealed class PlayerSpawnCoordinator
    {
        private readonly Dictionary<int, NetworkObject> _spawnedByPlayerId = new();
        private readonly HashSet<int> _spawnInProgressByPlayerId = new();

        public void Clear()
        {
            _spawnedByPlayerId.Clear();
            _spawnInProgressByPlayerId.Clear();
        }

        public bool HasSpawnedById(NetworkRunner runningRunner, PlayerRef player)
        {
            if (runningRunner == null || player == PlayerRef.None)
            {
                return false;
            }

            if (runningRunner.GetPlayerObject(player) != null)
            {
                return true;
            }

            return _spawnedByPlayerId.TryGetValue(player.PlayerId, out var cachedObject) && cachedObject != null;
        }

        public void RemovePlayer(PlayerRef player)
        {
            if (player == PlayerRef.None)
            {
                return;
            }

            _spawnedByPlayerId.Remove(player.PlayerId);
            _spawnInProgressByPlayerId.Remove(player.PlayerId);
            PlayerSpawnPointResolver.Remove(player.PlayerId);
        }

        public bool TrySpawn(
            NetworkRunner runningRunner,
            PlayerRef player,
            NetworkObject playerPrefab,
            Func<PlayerRef, (Vector3 Position, Quaternion Rotation)> resolveSpawnTransform,
            Func<NetworkRunner, PlayerRef, bool> canSpawnForPlayer,
            out NetworkObject spawnedPlayer,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            spawnedPlayer = null;
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;

            if (runningRunner == null || player == PlayerRef.None || playerPrefab == null)
            {
                return false;
            }

            var playerKey = player.PlayerId;
            if (_spawnInProgressByPlayerId.Add(playerKey) == false)
            {
                return false;
            }

            try
            {
                var existing = runningRunner.GetPlayerObject(player);
                if (existing != null)
                {
                    _spawnedByPlayerId[playerKey] = existing;
                    return false;
                }

                if (canSpawnForPlayer(runningRunner, player) == false)
                {
                    return false;
                }

                if (_spawnedByPlayerId.TryGetValue(playerKey, out var existingSpawned) && existingSpawned != null)
                {
                    if (existingSpawned.InputAuthority == player)
                    {
                        return false;
                    }

                    _spawnedByPlayerId.Remove(playerKey);
                }

                (spawnPosition, spawnRotation) = resolveSpawnTransform(player);
                spawnedPlayer = runningRunner.Spawn(playerPrefab, spawnPosition, spawnRotation, player);
                if (spawnedPlayer == null)
                {
                    return false;
                }

                spawnedPlayer.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                _spawnedByPlayerId[playerKey] = spawnedPlayer;
                PlayerSpawnPointResolver.SetCachedSpawnTransform(player, spawnPosition, spawnRotation);

                return true;
            }
            finally
            {
                _spawnInProgressByPlayerId.Remove(playerKey);
            }
        }
    }
}
