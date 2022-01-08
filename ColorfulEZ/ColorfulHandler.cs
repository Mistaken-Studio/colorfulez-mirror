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
using Footprinting;
using MEC;
using Mirror;
using Mistaken.API;
using Mistaken.API.Diagnostics;
using UnityEngine;

namespace Mistaken.ColorfulEZ
{
    internal class ColorfulHandler : Module
    {
        public ColorfulHandler(IPlugin<IConfig> plugin)
            : base(plugin)
        {
            Instance = this;
            this.assetsPath = Path.Combine(Paths.Plugins, PluginHandler.Instance.Config.AssetsPath);
            if (!Directory.Exists(this.assetsPath))
                Directory.CreateDirectory(this.assetsPath);
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

        internal static ColorfulHandler Instance { get; private set; }

        internal void RemoveObjects()
        {
            if (this.roomsObjects.Count == 0)
                return;

            foreach (var pairs in this.roomsObjects.ToArray())
            {
                foreach (var netid in pairs.Value.ToArray())
                {
                    if (netid is null)
                        continue;

                    NetworkServer.Destroy(netid.gameObject);
                }

                this.roomsObjects.Remove(pairs.Key);
            }
        }

        internal bool ChangeObjectsColor(Color color)
        {
            if (this.roomsObjects.Count == 0)
                return false;

            foreach (var values in this.roomsObjects.Values)
            {
                foreach (var netid in values)
                {
                    if (netid is null)
                        continue;

                    netid.GetComponent<PrimitiveObjectToy>().NetworkMaterialColor = color;
                }
            }

            return true;
        }

        private static readonly Dictionary<string, RoomType> PrefabConversion = new Dictionary<string, RoomType>()
        {
            { "ez_straight_stripes", RoomType.EzStraight },                 // done
            { "ez_cafeteria_stripes", RoomType.EzCafeteria },               // done
            { "ez_collapsed_tunnels_stripes", RoomType.EzCollapsedTunnel }, // done
            { "ez_conference_stripes", RoomType.EzConference },             // done
            { "ez_three_way_stripes", RoomType.EzTCross },                  // done
            { "ez_crossing_stripes", RoomType.EzCrossing },                 // done
            { "ez_curve_stripes", RoomType.EzCurve },                       // done
            { "ez_intercom_stripes", RoomType.EzIntercom },                 // done
            { "ez_gatea_stripes", RoomType.EzGateA },                       // done
            { "ez_gateb_stripes", RoomType.EzGateB },                       // done
            { "ez_shelter_stripes", RoomType.EzShelter },                   // done
            { "ez_pcs_stripes", RoomType.EzPcs },                           // done
            { "ez_pcs_downstairs_stripes", RoomType.EzDownstairsPcs },      // done
            { "ez_pcs_upstairs_stripes", RoomType.EzUpstairsPcs },          // done
            { "ez_hcz_checkpoint_stripes", RoomType.HczEzCheckpoint },      // done
        };

        private readonly HashSet<API.Utilities.Room> rooms = new HashSet<API.Utilities.Room>();

        private readonly Dictionary<Room, HashSet<NetworkIdentity>> roomsObjects = new Dictionary<Room, HashSet<NetworkIdentity>>();
        private readonly Dictionary<Player, API.Utilities.Room> lastRooms = new Dictionary<Player, API.Utilities.Room>();

        private readonly string assetsPath;

        private ushort spawnedAmount;

        private void Server_WaitingForPlayers()
        {
            this.roomsObjects.Clear();
            this.lastRooms.Clear();
            this.rooms.Clear();
            this.spawnedAmount = 0;

            foreach (var room in API.Utilities.Room.Rooms.Values)
            {
                if (!PrefabConversion.ContainsValue(room.ExiledRoom.Type))
                    continue;
                this.rooms.Add(room);
            }

            this.LoadAssets();

            var rawColor = PluginHandler.Instance.Config.Colors[UnityEngine.Random.Range(0, PluginHandler.Instance.Config.Colors.Count)];
            if (!ColorUtility.TryParseHtmlString(rawColor, out var color))
            {
                this.Log.Warn($"Invalid color \"{rawColor}\"");
                color = Color.black;
            }

            this.ChangeObjectsColor(color);
            this.RunCoroutine(this.UpdateObjectsForPlayers(), "colorfulez_updateobjectsforplayers");
            this.RunCoroutine(this.UpdateObjectsForFastPlayers(), "colorfulez_updateobjectsforfastplayers");
        }

        private void Player_Verified(Exiled.Events.EventArgs.VerifiedEventArgs ev)
        {
            foreach (var item in this.rooms)
                this.UnloadRoomFor(ev.Player, item);
        }

        private void LoadAssets()
        {
            foreach (var filePath in Directory.GetFiles(this.assetsPath))
            {
                var file = Path.GetFileName(filePath);
                this.Log.Debug(filePath, PluginHandler.Instance.Config.VerbouseOutput);
                var boundle = AssetBundle.LoadFromFile(filePath);
                var prefab = boundle.LoadAsset<GameObject>(file);

                if (prefab == null)
                {
                    this.Log.Error($"{file} was not found in the boundle");
                    continue;
                }

                if (!PrefabConversion.ContainsKey(file))
                {
                    this.Log.Error($"{file} does not have any representation in Dictionary");
                    continue;
                }

                foreach (var room in Map.Rooms.Where(x => x.Type == PrefabConversion[file]))
                {
                    if (room.Type == RoomType.HczEzCheckpoint)
                    {
                        var checkpoint = room.transform.Find("Checkpoint");

                        var obj = this.ConvertToToy(prefab, checkpoint, room);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                        /*var objrot = obj.transform.rotation;
                        var objpos = obj.transform.position;
                        obj.transform.localRotation = objrot;
                        obj.transform.localPosition = objpos;*/
                    }
                    else
                    {
                        var obj = this.ConvertToToy(prefab, room.transform, room);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }
                }

                boundle.Unload(false);
                this.Log.Info($"Loaded {file}");
            }

            this.Log.Info($"Successfully spawned {this.spawnedAmount} objects");
        }

        private GameObject ConvertToToy(GameObject toConvert, Transform parent, Room room)
        {
            if (!toConvert.activeSelf)
                return null;

            this.Log.Debug($"Loading {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);
            var meshFilter = toConvert.GetComponent<MeshFilter>();
            GameObject gameObject;
            PrimitiveObjectToy toy = null;
            if (meshFilter is null)
                gameObject = new GameObject();
            else
            {
                toy = this.GetPrimitiveObjectToy(parent);
                gameObject = toy.gameObject;
            }

            if (!(parent is null))
                gameObject.transform.parent = parent.transform;
            gameObject.name = toConvert.name;
            gameObject.transform.localPosition = toConvert.transform.localPosition;
            this.Log.Debug($"Position: {toConvert.transform.position}", PluginHandler.Instance.Config.VerbouseOutput);
            gameObject.transform.localRotation = toConvert.transform.localRotation;
            gameObject.transform.localScale = toConvert.transform.localScale;

            toy.UpdatePositionServer();

            var meshRenderer = toConvert.GetComponent<MeshRenderer>();
            if (!(meshFilter is null))
            {
                toy.NetworkMaterialColor = meshRenderer.material.color;
                string mesh = meshFilter.mesh.name.Split(' ')[0];
                this.Log.Debug($"Mesh: {mesh}", PluginHandler.Instance.Config.VerbouseOutput);
                if (System.Enum.TryParse<PrimitiveType>(mesh, out var type))
                    toy.NetworkPrimitiveType = type;
                else
                {
                    this.Log.Error("PrimitiveType was none!");
                    return null;
                }
            }

            for (int i = 0; i < toConvert.transform.childCount; i++)
            {
                var child = toConvert.transform.GetChild(i);
                this.ConvertToToy(child.gameObject, gameObject.transform, room);
            }

            this.Log.Debug($"Loaded {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);
            if (!(toy is null))
            {
                if (!gameObject.name.Contains("(ignore)"))
                {
                    if (!this.roomsObjects.ContainsKey(room))
                        this.roomsObjects.Add(room, new HashSet<NetworkIdentity>());
                    this.roomsObjects[room].Add(toy.netIdentity);
                }
            }

            this.spawnedAmount++;
            return gameObject;
        }

        private PrimitiveObjectToy GetPrimitiveObjectToy(Transform parent)
        {
            return API.Extensions.Extensions.SpawnPrimitive(PrimitiveType.Quad, parent, Color.gray, false);
        }

        private IEnumerator<float> UpdateObjectsForPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.NormalRefreshTime);

                foreach (var player in RealPlayers.List.Where(x => !x.GetEffectActive<Scp207>() && !x.GetEffectActive<MovementBoost>() && x.Role != RoleType.Scp173 && x.Role != RoleType.Scp096 && !x.NoClipEnabled))
                {
                    if (player.IsDead)
                        this.UpdateForSpectator(player);
                    else
                        this.UpdateForAlive(player);
                }
            }
        }

        private IEnumerator<float> UpdateObjectsForFastPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.FastRefreshTime);

                foreach (var player in RealPlayers.List.Where(x => x.GetEffectActive<Scp207>() || x.GetEffectActive<MovementBoost>() || x.Role == RoleType.Scp173 || x.Role == RoleType.Scp096 || x.NoClipEnabled))
                    this.UpdateForAlive(player);
            }
        }

        private void UpdateFor(Player player, API.Utilities.Room room)
        {
            try
            {
                if (!this.lastRooms.TryGetValue(player, out var lastRoom))
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
                    this.UnloadRoomFor(player, item);

                foreach (var item in toLoad)
                    this.LoadRoomFor(player, item);

                this.lastRooms[player] = room;
            }
            catch (Exception ex)
            {
                this.Log.Error(ex);
            }
        }

        private void UpdateForAlive(Player player)
        {
            var room = API.Utilities.Room.Get(player.CurrentRoom);

            this.UpdateFor(player, room);
        }

        private void UpdateForSpectator(Player spectator)
        {
            this.UpdateFor(spectator, API.Utilities.Room.Get(spectator.SpectatedPlayer?.CurrentRoom));
        }

        private void LoadRoomFor(Player player, API.Utilities.Room room)
        {
            if (!this.rooms.Contains(room))
                return;

            try
            {
                foreach (var obj in this.roomsObjects[room.ExiledRoom])
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
                this.Log.Error(ex);
            }
        }

        private void UnloadRoomFor(Player player, API.Utilities.Room room)
        {
            if (!this.rooms.Contains(room))
                return;

            try
            {
                foreach (var obj in this.roomsObjects[room.ExiledRoom])
                {
                    if (player.ReferenceHub.networkIdentity.connectionToClient is null)
                        continue;
                    player.Connection.Send(new ObjectDestroyMessage { netId = obj.netId }, 0);
                }
            }
            catch (Exception ex)
            {
                this.Log.Error(ex);
            }
        }

        private IEnumerator<float> UpdateColor()
        {
            float hue = 0;
            while (true)
            {
                yield return Timing.WaitForSeconds(0.01f);
                this.ChangeObjectsColor(Color.HSVToRGB(hue / 360f, 1f, 1f, true));

                hue += 2f;

                if (hue >= 360f)
                    hue = 0;
            }
        }
    }
}
