// -----------------------------------------------------------------------
// <copyright file="CommandHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using CommandSystem;
using Mistaken.API.Commands;

namespace Mistaken.ColorfulEZ
{
    [CommandSystem.CommandHandler(typeof(CommandSystem.RemoteAdminCommandHandler))]
    internal class CommandHandler : IBetterCommand
    {
        public override string Command => "Colorfulez";

        public override string[] Execute(ICommandSender sender, string[] args, out bool success)
        {
            success = false;
            if (args.Length == 0)
                return this.GetUsage();

            switch (args[0])
            {
                case "reload":
                    {
                        if (ColorfulHandler.Instance.ReloadObjects())
                        {
                            success = true;
                            return new string[] { "Success" };
                        }

                        return new string[] { "Failed to reload objects" };
                    }

                case string i when i == "changecolor" || i == "cc":
                    {
                        if (args.Length < 2)
                            return new string[] { "You must provide color in hex or name" };
                        if (!UnityEngine.ColorUtility.TryParseHtmlString(args[1], out var color))
                            return new string[] { "Invalid parameters" };
                        if (ColorfulHandler.Instance.ChangeObjectsColor(color))
                        {
                            success = true;
                            return new string[] { "Success" };
                        }

                        return new string[] { "Failed to change color" };
                    }

                default:
                    return this.GetUsage();
            }
        }

        private string[] GetUsage()
        {
            return new string[]
            {
                "Colorfulez reload - reloads all objects spawned by ColorfulEZ",
                "Colorfulez changecolor (Alias cc) - changes the color of objects spawned by ColorfulEZ",
            };
        }
    }
}
