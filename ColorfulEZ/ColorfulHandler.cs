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
using MEC;
using Mirror;
using UnityEngine;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Mistaken.ColorfulEZ
{
    internal class ColorfulHandler : API.Diagnostics.Module
    {
        public static int LoadAssets()
        {
            Prefabs.Clear();

            string path = Path.Combine(AssetsPath, "colorfulez");
            if (!File.Exists(path))
            {
                Debug.LogError("[ColorfulEZ]: Could not find AssetBundle for this plugin!");
                return 0;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            foreach (var prefab in bundle.LoadAllAssets<GameObject>())
            {
                if (prefab == null)
                {
                    Instance.Log.Error("Failed to load prefab. Prefab was null");
                    continue;
                }

                if (!PrefabConversion.ContainsKey(prefab.name))
                {
                    Instance.Log.Info($"Skipped loading: {prefab.name}. Prefab not found in Dictionary");
                    continue;
                }

                Instance.Log.Info(Prefabs.Add(prefab)
                    ? $"Successfully loaded: {prefab.name}"
                    : $"Skipped loading: {prefab.name}. Prefab was already loaded");
            }

            if (PrefabConversion.Count == Prefabs.Count)
                Instance.Log.Info("Successfully loaded all assets!");
            else
                Instance.Log.Warn("Some prefabs were not loaded!");

            bundle.Unload(false);
            return Prefabs.Count;
        }

        public static void SpawnPrefabs()
        {
            colorSyncMeshRenderer = new GameObject().AddComponent<MeshRenderer>();
            foreach (var prefab in Prefabs)
            {
                foreach (var room in Room.List.ToArray())
                {
                    if (PrefabConversion[prefab.name] != room.Type)
                        continue;

                    if (room.Type == RoomType.HczEzCheckpoint)
                    {
                        var checkpoint = room.transform.Find("Checkpoint");

                        var obj = ConvertToToy(prefab, checkpoint);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        var obj = ConvertToToy(prefab, room.transform);
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            Instance.Log.Debug($"Spawned {Spawned.Count} objects", PluginHandler.Instance.Config.VerbouseOutput);

            var color = Color.black;
            if (PluginHandler.Instance.Config.Colors != null)
            {
                var rawColor = PluginHandler.Instance.Config.Colors[Random.Range(0, PluginHandler.Instance.Config.Colors.Count)];
                if (!ColorUtility.TryParseHtmlString(rawColor, out color))
                    Instance.Log.Warn($"Invalid color \"{rawColor}\"");
            }

            ChangeObjectsColor(color);
        }

        public static void ChangeObjectsColor(Color color)
        {
            colorSyncMeshRenderer.material.color = color;
        }

        public static void RemoveObjects()
        {
            foreach (var networkIdentity in Spawned.ToArray())
            {
                if (networkIdentity is null)
                    continue;

                NetworkServer.Destroy(networkIdentity.gameObject);
            }

            if (colorSyncMeshRenderer is not null)
                Object.Destroy(colorSyncMeshRenderer);
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
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= this.Server_WaitingForPlayers;
        }

        private static readonly Dictionary<string, RoomType> PrefabConversion = new()
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

        private static readonly HashSet<GameObject> Prefabs = new();
        private static readonly HashSet<NetworkIdentity> Spawned = new();
        private static readonly string AssetsPath = Path.Combine(Paths.Plugins, "AssetBoundle");
        private static MeshRenderer colorSyncMeshRenderer;

        private static ColorfulHandler Instance { get; set; }

        private static GameObject ConvertToToy(GameObject toConvert, Transform parent)
        {
            if (!toConvert.activeSelf)
                return null;

            var ignore = toConvert.name.Contains("(ignore)");

            Instance.Log.Debug($"Loading {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);
            var meshFilter = toConvert.GetComponent<MeshFilter>();
            GameObject gameObject;
            PrimitiveObjectToy toy = null;
            if (meshFilter is null)
                gameObject = new GameObject();
            else
            {
                toy = Toy.API.ToyHandler.SpawnPrimitive(
                    Toy.API.ToyHandler.GetPrimitiveType(meshFilter),
                    parent,
                    toConvert.GetComponent<MeshRenderer>().material.color,
                    true,
                    false,
                    null,
                    ignore ? null : colorSyncMeshRenderer);
                gameObject = toy.gameObject;

                Spawned.Add(toy.netIdentity);
            }

            if (parent is not null)
                gameObject.transform.parent = parent.transform;
            gameObject.name = toConvert.name;
            gameObject.transform.localPosition = toConvert.transform.localPosition;
            Instance.Log.Debug($"Position: {toConvert.transform.position}", PluginHandler.Instance.Config.VerbouseOutput);
            gameObject.transform.localRotation = toConvert.transform.localRotation;
            gameObject.transform.localScale = toConvert.transform.localScale;

            toy?.UpdatePositionServer();

            for (var i = 0; i < toConvert.transform.childCount; i++)
            {
                var child = toConvert.transform.GetChild(i);
                ConvertToToy(child.gameObject, gameObject.transform);
            }

            Instance.Log.Debug($"Loaded {toConvert.name}", PluginHandler.Instance.Config.VerbouseOutput);

            return gameObject;
        }

        private IEnumerator<float> UpdateColor()
        {
            float hue = 0;
            while (this.Enabled)
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
            Spawned.Clear();

            if (Prefabs.Count != PrefabConversion.Count)
            {
                if (LoadAssets() == 0)
                {
                    Debug.LogError("[ColorfulEZ]: Couldn't spawn any prefab. Prefabs failed to load!");
                    return;
                }
            }

            SpawnPrefabs();

            // ReSharper disable StringLiteralTypo
            if (PluginHandler.Instance.Config.RainbowMode)
                this.RunCoroutine(this.UpdateColor(), "colorfulez_updatecolor", true);
        }
    }
}
