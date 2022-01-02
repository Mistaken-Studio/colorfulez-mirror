// -----------------------------------------------------------------------
// <copyright file="ColorfulHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using Footprinting;
using MEC;
using Mirror;
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

        public override string Name => "ColorfulEZHandler";

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers += this.Server_WaitingForPlayers;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= this.Server_WaitingForPlayers;
        }

        internal static ColorfulHandler Instance { get; private set; }

        internal bool ReloadObjects()
        {
            if (this.spawnedObjects.Count == 0)
                return false;

            foreach (var obj in this.spawnedObjects.ToArray())
            {
                if (obj is null)
                    continue;

                NetworkServer.Destroy(obj.gameObject);
                this.spawnedObjects.Remove(obj);
            }

            this.LoadAssets();
            return true;
        }

        // naprawić zmiane koloru
        internal bool ChangeObjectsColor(Color color)
        {
            if (this.spawnedObjects.Count == 0)
                return false;

            foreach (var obj in this.spawnedObjects)
            {
                if (obj is null)
                    continue;

                obj.NetworkMaterialColor = color;
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

        private static readonly Dictionary<Room, HashSet<GameObject>> RoomsObjects = new Dictionary<Room, HashSet<GameObject>>();

        private readonly HashSet<PrimitiveObjectToy> spawnedObjects = new HashSet<PrimitiveObjectToy>();

        private readonly string assetsPath;

        private void Server_WaitingForPlayers()
        {
            this.spawnedObjects.Clear();
            this.LoadAssets();

            // Timing.CallDelayed(20f, () => this.RunCoroutine(this.UpdateColor()));
            this.ChangeColor(Color.HSVToRGB(UnityEngine.Random.Range(0f, 1f), 1f, 1f, true));
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
                    this.spawnedObjects.Add(toy);
            }

            if (!RoomsObjects.ContainsKey(room))
                RoomsObjects.Add(room, new HashSet<GameObject>());
            RoomsObjects[room].Add(gameObject);

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

        private IEnumerator<float> UpdateColor()
        {
            float hue = 0;
            while (true)
            {
                yield return Timing.WaitForSeconds(0.01f);
                this.ChangeColor(Color.HSVToRGB(hue / 360f, 1f, 1f, true));

                hue += 2f;

                if (hue >= 360f)
                    hue = 0;
            }
        }

        private void ChangeColor(Color color)
        {
            foreach (var item in this.spawnedObjects.ToArray())
                item.NetworkMaterialColor = color;
        }
    }
}
