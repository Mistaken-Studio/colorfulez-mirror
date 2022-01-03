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
            Exiled.Events.Handlers.Player.Left += this.Player_Left;
            Exiled.Events.Handlers.Player.Verified += this.Player_Verified;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= this.Server_WaitingForPlayers;
            Exiled.Events.Handlers.Player.Left -= this.Player_Left;
            Exiled.Events.Handlers.Player.Verified -= this.Player_Verified;
        }

        internal static ColorfulHandler Instance { get; private set; }

        internal void RemoveObjects()
        {
            if (RoomsObjects.Count == 0)
                return;

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

        internal bool ChangeObjectsColor(Color color)
        {
            if (RoomsObjects.Count == 0)
                return false;

            foreach (var values in RoomsObjects.Values)
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

        private static readonly Dictionary<Room, HashSet<Room>> ToLoad = new Dictionary<Room, HashSet<Room>>();

        private static readonly Dictionary<Room, HashSet<NetworkIdentity>> RoomsObjects = new Dictionary<Room, HashSet<NetworkIdentity>>();

        private readonly Dictionary<Player, HashSet<Room>> loadedFor = new Dictionary<Player, HashSet<Room>>();

        private readonly string assetsPath;

        private ushort spawnedAmount;

        private void Server_WaitingForPlayers()
        {
            this.loadedFor.Clear();
            RoomsObjects.Clear();
            ToLoad.Clear();

            foreach (var room in Map.Rooms.Where(x => PrefabConversion.ContainsValue(x.Type)))
            {
                foreach (var item in Map.Rooms)
                {
                    if (Vector3.Distance(room.Position, item.Position) < PluginHandler.Instance.Config.RenderDistance)
                    {
                        if (!ToLoad.ContainsKey(item))
                            ToLoad.Add(item, new HashSet<Room>());
                        ToLoad[item].Add(room);
                    }
                }
            }

            this.LoadAssets();

            // Timing.CallDelayed(20f, () => this.RunCoroutine(this.UpdateColor()));
            this.ChangeObjectsColor(Color.HSVToRGB(UnityEngine.Random.Range(0f, 1f), 1f, 1f, true));
            var cor = this.RunCoroutine(this.UpdateObjectsForPlayers(), "colorfulez_updateobjectsforplayers");
            this.RunCoroutine(this.UpdateObjectsForFastPlayers(), "colorfulez_updateobjectsforfastplayers");
        }

        private void Player_Left(Exiled.Events.EventArgs.LeftEventArgs ev)
        {
            if (this.loadedFor.ContainsKey(ev.Player))
                this.loadedFor.Remove(ev.Player);
        }

        private void Player_Verified(Exiled.Events.EventArgs.VerifiedEventArgs ev)
        {
            if (!this.loadedFor.ContainsKey(ev.Player))
                this.loadedFor.Add(ev.Player, Map.Rooms.Where(x => PrefabConversion.ContainsValue(x.Type)).ToHashSet());
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
                toy = this.GetPrimitiveObjectToy();
                gameObject = toy.gameObject;
            }

            if (!(parent is null))
                gameObject.transform.parent = parent.transform;
            gameObject.name = toConvert.name;
            gameObject.transform.localPosition = toConvert.transform.localPosition;
            this.Log.Debug($"Position: {toConvert.transform.position}", PluginHandler.Instance.Config.VerbouseOutput);
            gameObject.transform.localRotation = toConvert.transform.localRotation;
            gameObject.transform.localScale = toConvert.transform.localScale;

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
                    if (!RoomsObjects.ContainsKey(room))
                        RoomsObjects.Add(room, new HashSet<NetworkIdentity>());
                    RoomsObjects[room].Add(toy.netIdentity);
                }
            }

            this.spawnedAmount++;
            return gameObject;
        }

        private PrimitiveObjectToy GetPrimitiveObjectToy()
        {
            foreach (var item in NetworkClient.prefabs.Values)
            {
                if (item.TryGetComponent<PrimitiveObjectToy>(out PrimitiveObjectToy adminToyBase))
                {
                    PrimitiveObjectToy toy = UnityEngine.Object.Instantiate<PrimitiveObjectToy>(adminToyBase);
                    toy.SpawnerFootprint = new Footprint(Server.Host.ReferenceHub);
                    NetworkServer.Spawn(toy.gameObject);
                    toy.NetworkPrimitiveType = PrimitiveType.Sphere;
                    toy.NetworkMaterialColor = Color.gray;
                    toy.transform.position = Vector3.zero;
                    toy.transform.eulerAngles = Vector3.zero;
                    toy.transform.localScale = Vector3.one;
                    toy.NetworkScale = toy.transform.localScale;
                    return toy;
                }
            }

            return null;
        }

        private IEnumerator<float> UpdateObjectsForPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.NormalRefreshTime);
                foreach (var player in RealPlayers.List.Where(x => !x.GetEffectActive<Scp207>() && !x.GetEffectActive<MovementBoost>() && x.Role != RoleType.Scp173 && x.Role != RoleType.Scp096 && !x.NoClipEnabled))
                {
                    try
                    {
                        if (player.Role == RoleType.Spectator)
                        {
                            this.LoadForSpectator(player);
                            continue;
                        }

                        var room = player.CurrentRoom;
                        if (room is null)
                        {
                            this.UnloadFor(player);
                            continue;
                        }

                        if (!this.loadedFor.ContainsKey(player))
                            this.loadedFor.Add(player, new HashSet<Room>());
                        if (ToLoad.ContainsKey(room))
                            this.LoadFor(player, room);
                        else
                            this.UnloadFor(player);
                    }
                    catch (Exception ex)
                    {
                        this.Log.Error(ex);
                    }
                }
            }
        }

        private IEnumerator<float> UpdateObjectsForFastPlayers()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(PluginHandler.Instance.Config.FastRefreshTime);
                foreach (var player in RealPlayers.List.Where(x => x.GetEffectActive<Scp207>() || x.GetEffectActive<MovementBoost>() || x.Role == RoleType.Scp173 || x.Role == RoleType.Scp096 || x.NoClipEnabled))
                {
                    try
                    {
                        var room = player.CurrentRoom;
                        if (room is null)
                        {
                            this.UnloadFor(player);
                            continue;
                        }

                        if (!this.loadedFor.ContainsKey(player))
                            this.loadedFor.Add(player, new HashSet<Room>());
                        if (ToLoad.ContainsKey(room))
                            this.LoadFor(player, room);
                        else
                            this.UnloadFor(player);
                    }
                    catch (Exception ex)
                    {
                        this.Log.Error(ex);
                    }
                }
            }
        }

        private void LoadForSpectator(Player spectator)
        {
            var spectated = spectator.SpectatedPlayer;
            if (spectated is null)
                return;
            if (this.loadedFor[spectator] == this.loadedFor[spectated])
                return;
            foreach (var r in this.loadedFor[spectated].Where(x => !this.loadedFor[spectator].Contains(x)))
            {
                try
                {
                    foreach (var obj in RoomsObjects[r])
                    {
                        if (Server.SendSpawnMessage is null)
                            continue;
                        if (spectator.ReferenceHub.networkIdentity.connectionToClient is null)
                            continue;
                        Server.SendSpawnMessage.Invoke(null, new object[] { obj, spectator.Connection });
                    }
                }
                catch (Exception ex)
                {
                    this.Log.Error(ex);
                }

                this.loadedFor[spectator].Add(r);
            }

            if (this.loadedFor[spectator].Count > this.loadedFor[spectated].Count)
            {
                var room = spectated.CurrentRoom;
                if (!(room is null))
                {
                    if (ToLoad.ContainsKey(room))
                        this.UnloadFor(spectator, room);
                    else
                        this.UnloadFor(spectator);
                }
                else
                    this.UnloadFor(spectator);
            }
        }

        private void LoadFor(Player player, Room room)
        {
            if (this.loadedFor[player] == ToLoad[room])
                return;
            foreach (var r in ToLoad[room].Where(x => !this.loadedFor[player].Contains(x)))
            {
                try
                {
                    foreach (var obj in RoomsObjects[r])
                    {
                        if (Server.SendSpawnMessage is null)
                            continue;
                        if (player.ReferenceHub.networkIdentity.connectionToClient is null)
                            continue;
                        Server.SendSpawnMessage.Invoke(null, new object[] { obj, player.Connection });
                    }
                }
                catch (Exception ex)
                {
                    this.Log.Error(ex);
                }

                this.loadedFor[player].Add(r);
            }

            if (this.loadedFor[player].Count > ToLoad[room].Count)
                this.UnloadFor(player, room);
        }

        private void UnloadFor(Player player, Room room = null)
        {
            var rooms = this.loadedFor[player].ToList();
            if (!(room is null))
                rooms.RemoveAll(x => ToLoad[room].Contains(x));
            foreach (var r in rooms)
            {
                try
                {
                    foreach (var obj in RoomsObjects[r])
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

                this.loadedFor[player].Remove(r);
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
