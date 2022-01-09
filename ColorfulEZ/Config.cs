// -----------------------------------------------------------------------
// <copyright file="Config.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel;
using Mistaken.Updater.Config;

namespace Mistaken.ColorfulEZ
{
    /// <inheritdoc/>
    public class Config : IAutoUpdatableConfig
    {
        /// <inheritdoc/>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether debug should be displayed.
        /// </summary>
        [Description("If true then debug will be displayed")]
        public bool VerbouseOutput { get; set; }

        /// <summary>
        /// Gets or sets time for refreshing objects.
        /// </summary>
        [Description("Sets time for refreshing objects")]
        public float NormalRefreshTime { get; set; } = 1f;

        /// <summary>
        /// Gets or sets time for refreshing objects.
        /// </summary>
        [Description("Sets time for refreshing objects (noclip, 173, 096, 207 effect)")]
        public float FastRefreshTime { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets path to assets inside the Plugins folder.
        /// </summary>
        [Description("Sets path to assets inside the Plugins folder")]
        public string AssetsPath { get; set; } = "ColorfulAssets";

        /// <summary>
        /// Gets or sets colors for stripes.
        /// </summary>
        [Description("Defines colors stripes can be")]
        public List<string> Colors { get; set; } = new List<string>()
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

        /// <inheritdoc/>
        [Description("Auto Update Settings")]
        public System.Collections.Generic.Dictionary<string, string> AutoUpdateConfig { get; set; }
    }
}
