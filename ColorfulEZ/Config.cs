// -----------------------------------------------------------------------
// <copyright file="Config.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel;
using Exiled.API.Interfaces;

namespace Mistaken.ColorfulEZ
{
    internal sealed class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;

        [Description("If true then debug will be displayed")]
        public bool VerboseOutput { get; set; }

        [Description("If true then entrance zone rooms 'stripes' will change color rapidly")]
        public bool RainbowMode { get; set; } = false;

        [Description("Defines colors stripes can be")]
        public List<string> Colors { get; set; } = new()
        {
            "#35493e", // KeycardChaosInsurgency
            "#b6887f", // KeycardContainmentEngineer
            "#ba1846", // KeycardFacilityManager
            "#606770", // KeycardGuard
            "#bcb1e4", // KeycardJanitor
            "#1841c8", // KeycardNTFCommander
            "#5180f7", // KeycardNTFLieutenant
            "#5b5b5b", // KeycardO5
            "#e7d678", // KeycardScientist
            "#ddab20", // KeycardResearchCoordinator
            "#a2cade", // KeycardNTFOfficer
            "#217778", // KeycardZoneManager
            "#FFFFFF", // White
        };
    }
}
