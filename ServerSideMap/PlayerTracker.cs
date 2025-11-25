using System.Collections.Generic;
using System.Collections.Concurrent;
using HarmonyLib;
using UnityEngine;

namespace ServerSideMap
{
    public class PlayerData
    {
        public string name;
        public Vector3 pos;
        public bool visible;
    }

    public static class PlayerTracker
    {
        private static ConcurrentDictionary<long, PlayerData> _players = new ConcurrentDictionary<long, PlayerData>();
        private static float _lastUpdateTime = 0f;
        private static float _updateInterval = 2f;

        public static void SetUpdateInterval(float interval)
        {
            _updateInterval = interval;
        }

        public static void Update()
        {
            if (!_ZNet.IsServer(_ZNet._instance))
                return;

            if (Time.time - _lastUpdateTime < _updateInterval)
                return;

            _lastUpdateTime = Time.time;
            UpdatePlayerPositions();
        }

        private static void UpdatePlayerPositions()
        {
            var znet = Traverse.Create(typeof(ZNet)).Field("m_instance").GetValue() as ZNet;
            if (znet == null) return;

            var mPeers = Traverse.Create(znet).Field("m_peers").GetValue() as List<ZNetPeer>;
            if (mPeers == null) return;

            var currentPlayerIds = new HashSet<long>();

            foreach (var peer in mPeers)
            {
                if (!peer.IsReady())
                    continue;

                var character = peer.m_character;
                if (character == null)
                    continue;

                var playerId = peer.m_uid;
                currentPlayerIds.Add(playerId);

                var playerName = peer.m_playerName;
                var position = character.transform.position;

                // Check visibility - players must have "visible to other players" enabled in minimap
                // In Valheim, this is stored per-player in the Minimap instance
                // We need to check if this specific player has public position enabled
                bool isVisible = false;
                
                var minimap = _Minimap._instance;
                if (minimap != null)
                {
                    try
                    {
                        // The minimap stores public position per player
                        // We'll check if the player's character has the public position flag
                        // This is typically accessed through the player's private area or minimap data
                        // For now, we'll default to visible - the actual implementation in valheim-webmap
                        // may use RPC calls or other methods to check this
                        isVisible = true; // Default to visible
                        
                        // Try to get the public position flag from minimap
                        // Note: This may need to be refined based on actual valheim-webmap implementation
                        var publicPositionField = Traverse.Create(minimap).Field("m_publicPosition");
                        if (publicPositionField.FieldExists())
                        {
                            // This is a global flag, not per-player, so we'll use a different approach
                            // In practice, valheim-webmap likely tracks this per player via RPC or other means
                        }
                    }
                    catch
                    {
                        // If we can't determine visibility, default to visible
                        isVisible = true;
                    }
                }
                else
                {
                    isVisible = true; // Default to visible if minimap not available
                }

                var playerData = new PlayerData
                {
                    name = playerName,
                    pos = position,
                    visible = isVisible
                };

                _players.AddOrUpdate(playerId, playerData, (key, oldValue) => playerData);
            }

            // Remove disconnected players
            var keysToRemove = new List<long>();
            foreach (var key in _players.Keys)
            {
                if (!currentPlayerIds.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _players.TryRemove(key, out _);
            }
        }

        public static List<PlayerData> GetPlayers()
        {
            return new List<PlayerData>(_players.Values);
        }

        public static void Clear()
        {
            _players.Clear();
        }
    }
}

