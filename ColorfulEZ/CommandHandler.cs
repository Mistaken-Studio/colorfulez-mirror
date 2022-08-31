// -----------------------------------------------------------------------
// <copyright file="CommandHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using CommandSystem;
using Mistaken.API.Commands;
using UnityEngine;

namespace Mistaken.ColorfulEZ
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    internal class CommandHandler : IBetterCommand
    {
        public override string Command => "colorfulez";

        public override string[] Aliases => new string[] { "cez" };

        public override string[] Execute(ICommandSender sender, string[] args, out bool success)
        {
            success = false;
            if (args.Length == 0)
                return this.GetUsage();

            switch (args[0].ToLower())
            {
                case "changecolor":
                case "cc":
                    {
                        if (args.Length < 2)
                            return new string[] { "You must provide color in hex or name" };
                        if (!ColorUtility.TryParseHtmlString(args[1], out var color))
                            return new string[] { "Invalid parameter" };
                        try
                        {
                            ColorfulHandler.ChangeObjectsColor(color);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError(ex);
                            return new string[] { ex.ToString() };
                        }
                    }

                    break;

                case "reloadassets":
                case "ra":
                    {
                        try
                        {
                            ColorfulHandler.RemoveObjects();

                            if (ColorfulHandler.LoadAssets() == 0)
                                return new string[] { "Couldn't spawn any prefab. Prefabs not loaded!" };

                            ColorfulHandler.SpawnPrefabs();
                            ColorfulHandler.RunCoroutines();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError(ex);
                            return new string[] { ex.ToString() };
                        }
                    }

                    break;
                default:
                    return this.GetUsage();
            }

            success = true;
            return new string[] { "Done" };
        }

        private string[] GetUsage()
        {
            return new string[]
            {
                "colorfulez changecolor - changes the color of objects spawned by ColorfulEZ",
                "colorfulez reloadassets - reload's all assets used by ColorfulEZ",
            };
        }
    }
}
