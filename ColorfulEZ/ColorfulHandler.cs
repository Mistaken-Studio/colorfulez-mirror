// -----------------------------------------------------------------------
// <copyright file="ColorfulHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdminToys;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using MEC;
using Mirror;
using Mistaken.API;
using Mistaken.API.Extensions;
using UnityEngine;

namespace Mistaken.ColorfulEZ
{
    internal class ColorfulHandler : API.Diagnostics.Module
    {
        public static readonly string AssetsPath = Path.Combine(Paths.Plugins, "AssetBoundle");

        public static ColorfulHandler Instance { get; private set; }

        public static void LoadAssets()
        {
            Prefabs.Clear();

            string path = Path.Combine(AssetsPath, "colorfulez");
            if (!File.Exists(path))
            {
                Debug.LogError("[ColorfulEZ]: Could not find AssetBoundle for this plugin!");
                return;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            foreach (var prefab in bundle.LoadAllAssets<GameObject>())
            {
                if (prefab == null)
                {
                    Instance.Log.Error("Failed to load prefab. Prefab was null");
                    continue;
                }
                else if (!PrefabConversion.ContainsKey(prefab.name))
                {
                    Instance.Log.Info($"Skipped loading: {prefab.name}. Prefab not found in Dictionary");
                    continue;
                }

                if (Prefabs.Add(prefab))
                    Instance.Log.Info($"Successfully loaded: {prefab.name}");
                else
                    Instance.Log.Info($"Skipped loading: {prefab.name}. Prefab was already loaded");
            }

            if (PrefabConversion.Count == Prefabs.Count)
                Instance.Log.Info($"Successfully loaded all assets!");
            else
                Instance.Log.Warn($"Some prefabs were not loaded!");

            bundle.Unload(false);
        }

        public static void SpawnPrefabs()
        {
            if (Prefabs.Count == 0)
            {
                Debug.LogError("[ColorfulEZ]: Couldn't spawn any prefab. Prefabs not loaded!");
                return;
            }

            foreach (var prefab in Prefabs)
            {
                foreach (var room in Room.List.ToArray())
                {
                    if (PrefabConversion[prefab.name] != room.Type)
                        continue;

                    if (room.Type == RoomType.HczEzCheckpoint)
                    {
                        var checkpoint = room.transform.Find("Checkpoint");

                        var obj = ConvertToToy(prefab, checkpoint, room);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        var obj = ConvertToToy(prefab, room.transform, room);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            Instance.Log.Debug($"Spawned {spawnedAmount} objects", PluginHandler.Instance.Config.VerbouseOutput);

            Color color = Color.black;
            if (PluginHandler.Instance.Config.Colors != null)
            {
                var rawColor = PluginHandler.Instance.Config.Colors[UnityEngine.Random.Range(0, PluginHandler.Instance.Config.Colors.Count)];
                if (!ColorUtility.TryParseHtmlString(rawColor, out color))
                    Instance.Log.Warn($"Invalid color \"{rawColor}\"");
            }

            ChangeObjectsColor(color);
        }

        public static void ChangeObjectsColor(Color color)
        {
            foreach (var values in RoomsObjects.Values.ToArray())
            {
                foreach (var netid in values)
                {
                    if (netid is null)
                        continue;

                    netid.GetComponent<PrimitiveObjectToy>().NetworkMaterialColor = color;
                }
            }
        }

        public static void RemoveObjects()
        {
            foreach (var pairs in RoomsObjects.ToArray())
            {
                foreach (var netid in pairs.Value.ToArray())
                {
                    if (netid is null)
                        continue;

                    NetworkServer.Destroy(netid.gameObject);
                }

                RoomsObjects.Remove(pairs.Key);
            }
        }

        public ColorfulHandler(IPlugin<IConfig> plugin)
            : base(plugin)
        {
            Instance = this;
            if (!Directory.Exists(AssetsPath))
                Directory.CreateDirectory(AssetsPath);
        }

        public override string Name => nameof(ColorfulHandler);

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers += this.Server_WaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified += this.Player_Verified;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= this.Server_WaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified -= this.Player_Verified;
        }

        private static readonly Dictionary<string, RoomType> PrefabConversion = new Dictionary<string, RoomType>()
        {
            { "EZ_Straight_Stripes", RoomType.EzStraight },                 // done
            { "EZ_Cafeteria_Stripes", RoomType.EzCafeteria },               // done
            { "EZ_Collapsed_Tunnels_Stripes", RoomType.EzCollapsedTunnel }, // done
            { "EZ_Conference_Stripes", RoomType.EzConference },             // done
            { "EZ_Three_Way_Stripes", RoomType.EzTCross },                  // done
            { "EZ_Crossing_Stripes", RoomType.EzCrossing },                 // done
            { "EZ_Curve_Stripes", RoomType.EzCurve },                       // done
            { "EZ_Intercom_Stripes", RoomType.EzIntercom },                 // done
            { "EZ_GateA_Stripes", RoomType.EzGateA },                       // done
            { "EZ_GateB_Stripes", RoomType.EzGateB },                       // done
            { "EZ_Shelter_Stripes", RoomType.EzShelter },                   // done
            { "EZ_Pcs_Stripes", RoomType.EzPcs },                           // done
            { "EZ_Pcs_Downstairs_Stripes", RoomType.EzDownstairsPcs },      // done
            { "EZ_Pcs_Upstairs_Stripes", RoomType.EzUpstairsPcs },          // done
            { "EZ_HCZ_Checkpoint_Stripes", RoomType.HczEzCheckpoint },      // done
        };

        private static readonly HashSet<GameObject> Prefabs = new HashSet<GameObject>();
        private static readonly HashSet<API.Utilities.Room> Rooms = new HashSet<API.Utilities.Room>();
        private static readonly Dictionary<Room, HashSet<NetworkIdentity>> RoomsObjects = new Dictionary<Room, HashSet<NetworkIdentity>>();
        private static readonly Dictionary<Player, API.Utilities.Room> LastRooms = new Dictionary<Player, API.Utilities.Room>();
        private static ushort spawnedAmount;

        private static GameObject ConvertToToy(GameObject toConvert, Transform parent, Room room)
        {
            if (!toConvert.activeSelf)
                return null;

            Instance.Log.Debug($"Loading {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);
            var meshFilter = toConvert.GetComponent<MeshFilter>();
            GameObject gameObject;
            PrimitiveObjectToy toy = null;
            if (meshFilter is null)
                gameObject = new GameObject();
            else
            {
                toy = MapPlus.SpawnPrimitive(PrimitiveType.Quad, parent, Color.gray, false);
                gameObject = toy.gameObject;
            }

            if (!(parent is null))
                gameObject.transform.parent = parent.transform;
            gameObject.name = toConvert.name;
            gameObject.transform.localPosition = toConvert.transform.localPosition;
            Instance.Log.Debug($"Position: {toConvert.transform.position}", PluginHandler.Instance.Config.VerbouseOutput);
            gameObject.transform.localRotation = toConvert.transform.localRotation;
            gameObject.transform.localScale = toConvert.transform.localScale;

            toy?.UpdatePositionServer();

            var meshRenderer = toConvert.GetComponent<MeshRenderer>();
            if (!(meshFilter is null))
            {
                toy.NetworkMaterialColor = meshRenderer.material.color;
                string mesh = meshFilter.mesh.name.Split(' ')[0];
                Instance.Log.Debug($"Mesh: {mesh}", PluginHandler.Instance.Config.VerbouseOutput);
                if (System.Enum.TryParse<PrimitiveType>(mesh, out var type))
                    toy.NetworkPrimitiveType = type;
                else
                {
                    Instance.Log.Error("PrimitiveType was none!");
                    return null;
                }
            }

            for (int i = 0; i < toConvert.transform.childCount; i++)
            {
                var child = toConvert.transform.GetChild(i);
                ConvertToToy(child.gameObject, gameObject.transform, room);
            }

            Instance.Log.Debug($"Loaded {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);
            if (!(toy is null))
            {
                if (!gameObject.name.Contains("(ignore)"))
                {
                    if (!RoomsObjects.ContainsKey(room))
                        RoomsObjects.Add(room, new HashSet<NetworkIdentity>());
                    RoomsObjects[room].Add(toy.netIdentity);
                }
            }

            spawnedAmount++;
            return gameObject;
        }

        private static IEnumerator<float> UpdateObjectsForPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.NormalRefreshTime);

                foreach (var player in RealPlayers.List)
                {
                    if (player.GetEffectActive<Scp207>() || player.GetEffectActive<MovementBoost>() || player.Role.Type == RoleType.Scp173 || player.Role.Type == RoleType.Scp096 || player.NoClipEnabled)
                        continue;
                    if (player.IsDead)
                        UpdateForSpectator(player);
                    else
                        UpdateForAlive(player);
                }
            }
        }

        private static IEnumerator<float> UpdateObjectsForFastPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.FastRefreshTime);

                foreach (var player in RealPlayers.List)
                {
                    if (player.GetEffectActive<Scp207>() || player.GetEffectActive<MovementBoost>() || player.Role.Type == RoleType.Scp173 || player.Role.Type == RoleType.Scp096 || player.NoClipEnabled)
                        UpdateForAlive(player);
                }
            }
        }

        private static void UpdateFor(Player player, API.Utilities.Room room)
        {
            try
            {
                if (!LastRooms.TryGetValue(player, out var lastRoom))
                    lastRoom = null;

                if (lastRoom == room)
                    return;

                HashSet<API.Utilities.Room> loaded;
                if (!(lastRoom is null))
                {
                    loaded = lastRoom.FarNeighbors.ToHashSet();
                    loaded.Add(lastRoom);
                }
                else
                    loaded = new HashSet<API.Utilities.Room>();

                HashSet<API.Utilities.Room> toLoad;
                if (!(room is null))
                {
                    toLoad = room.FarNeighbors.ToHashSet();
                    toLoad.Add(room);
                }
                else
                    toLoad = new HashSet<API.Utilities.Room>();

                var intersect = loaded.Intersect(toLoad).ToArray();

                foreach (var item in intersect)
                {
                    loaded.Remove(item);
                    toLoad.Remove(item);
                }

                foreach (var item in loaded)
                    UnloadRoomFor(player, item);

                foreach (var item in toLoad)
                    LoadRoomFor(player, item);

                LastRooms[player] = room;
            }
            catch (Exception ex)
            {
                Instance.Log.Error(ex);
            }
        }

        private static void UpdateForAlive(Player player)
        {
            var room = API.Utilities.Room.Get(player.CurrentRoom);
            UpdateFor(player, room);
        }

        private static void UpdateForSpectator(Player spectator)
        {
            UpdateFor(spectator, API.Utilities.Room.Get(spectator.GetSpectatedPlayer()?.CurrentRoom));
        }

        private static void LoadRoomFor(Player player, API.Utilities.Room room)
        {
            if (!Rooms.Contains(room))
                return;

            try
            {
                foreach (var obj in RoomsObjects[room.ExiledRoom])
                {
                    if (Server.SendSpawnMessage is null)
                        continue;

                    if (player.Connection is null)
                        continue;

                    Server.SendSpawnMessage.Invoke(null, new object[] { obj, player.Connection });
                }
            }
            catch (Exception ex)
            {
                Instance.Log.Error(ex);
            }
        }

        private static void UnloadRoomFor(Player player, API.Utilities.Room room)
        {
            if (!Rooms.Contains(room))
                return;

            try
            {
                foreach (var obj in RoomsObjects[room.ExiledRoom])
                {
                    if (player.Connection.identity.connectionToClient is null)
                        continue;

                    player.Connection.Send(new ObjectDestroyMessage { netId = obj.netId }, 0);
                }
            }
            catch (Exception ex)
            {
                Instance.Log.Error(ex);
            }
        }

        private static IEnumerator<float> UpdateColor()
        {
            float hue = 0;
            while (true)
            {
                yield return Timing.WaitForSeconds(0.1f);
                ChangeObjectsColor(Color.HSVToRGB(hue / 360f, 1f, 1f, true));

                hue += 2f;

                if (hue >= 360f)
                    hue = 0;
            }
        }

        private void Server_WaitingForPlayers()
        {
            RoomsObjects.Clear();
            LastRooms.Clear();
            Rooms.Clear();
            spawnedAmount = 0;

            foreach (var room in API.Utilities.Room.Rooms.Values)
            {
                if (!PrefabConversion.ContainsValue(room.ExiledRoom.Type))
                    continue;
                Rooms.Add(room);
            }

            if (Prefabs.Count != PrefabConversion.Count)
                LoadAssets();

            SpawnPrefabs();

            this.RunCoroutine(UpdateObjectsForPlayers(), "colorfulez_updateobjectsforplayers", true);
            this.RunCoroutine(UpdateObjectsForFastPlayers(), "colorfulez_updateobjectsforfastplayers", true);

            if (PluginHandler.Instance.Config.RainbowMode)
                this.RunCoroutine(UpdateColor(), "colorfulez_updatecolor", true);
        }

        private void Player_Verified(Exiled.Events.EventArgs.VerifiedEventArgs ev)
        {
            foreach (var item in Rooms)
                UnloadRoomFor(ev.Player, item);
        }
    }
}
