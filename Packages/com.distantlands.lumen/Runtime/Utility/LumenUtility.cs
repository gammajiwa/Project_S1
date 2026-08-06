// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Provides utility functions for the Lumen system, including math helpers and resource management.

using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Provides utility functions for the Lumen system, including mesh access and effect refresh events.
    /// </summary>
    public class LumenUtility
    {
        static LumenSettings m_Meshes;
        /// <summary>
        /// Gets the global Lumen mesh settings from resources.
        /// </summary>
        public static LumenSettings GetMeshes
        {
            get
            {
                if (m_Meshes == null)
                    m_Meshes = Resources.Load("Lumen Meshes") as LumenSettings;
                return m_Meshes;
            }
        }

        /// <summary>
        /// Delegate that is called to refresh all on-screen lumen effect players.
        /// </summary>
        public delegate void RedoEntireEffectsDelegate();
        public static event RedoEntireEffectsDelegate OnRedoEntireEffect;

        /// <summary>
        /// Invokes the OnRedoEntireEffect event to refresh all effects.
        /// </summary>
        public static void RedoEntireEffect() => OnRedoEntireEffect?.Invoke();
    }
}