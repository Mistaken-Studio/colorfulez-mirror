// -----------------------------------------------------------------------
// <copyright file="Config.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

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
        /// Gets or sets object rendering distance.
        /// </summary>
        [Description("Sets object rendering distance")]
        public float RenderDistance { get; set; } = 41f;

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

        /// <inheritdoc/>
        [Description("Auto Update Settings")]
        public System.Collections.Generic.Dictionary<string, string> AutoUpdateConfig { get; set; }
    }
}
