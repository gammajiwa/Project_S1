// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Defines the main Lumen effect asset, containing a list of effect layers and their configuration.

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Stores a list of effect layers that are instantiated via a Lumen Effect Player.
    /// </summary>
    [CreateAssetMenu(fileName = "New Lumen Effect", menuName = "Distant Lands/Lumen/Lumen Effect", order = 231)]
    public class LumenEffectProfile : ScriptableObject
    {
        /// <summary>
        /// If true, the sun will be auto-assigned for this effect.
        /// </summary>
        public bool autoAssignSun;
        
        /// <summary>
        /// The layers used for the effect.
        /// </summary>
        [SerializeReference]
        public List<LumenEffectLayer> layers = new List<LumenEffectLayer>()
        {
        };
    }
}