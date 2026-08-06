// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Stores and manages global Lumen settings for the project.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DistantLands.Lumen
{
    /// <summary>
    /// Hold access reference to all meshes used by Lumen.
    /// </summary>
    [CreateAssetMenu(fileName = "New Lumen Settings", menuName = "Distant Lands/Lumen/Lumen Settings", order = 231)]
    public class LumenSettings : ScriptableObject
    {
        static LumenSettings m_LumenSettings;
        /// <summary>
        /// Gets the global Lumen settings from resources.
        /// </summary>
        public static LumenSettings Global
        {
            get
            {
                if (m_LumenSettings == null)
                    m_LumenSettings = Resources.Load<LumenSettings>("Lumen Settings");

                return m_LumenSettings;
            }
        }

        /// <summary>
        /// Gets the associated name for a mesh (if included in the lumen mesh settings).
        /// </summary>
        public string GetNameForMesh(Mesh mesh)
        {
            string n = "";

            if (lumenMeshes.Any(x => x.mesh == mesh))
                n = lumenMeshes.Find(x => x.mesh == mesh).displayName;

            return n;
        }

        /// <summary>
        /// Gets the associated mesh for a given name (if included in the lumen mesh settings)
        /// </summary>
        public Mesh GetMeshByName(string name)
        {
            Mesh n = null;

            if (lumenMeshes.Any(x => x.displayName == name))
                n = lumenMeshes.Find(x => x.displayName == name).mesh;

            return n;
        }
        
        public List<LumenMesh> lumenMeshesForLayerType(LinkedEffectType effectType)
        {
            return lumenMeshes.Where(x => x.linkedEffectType == effectType).ToList();
        }

        public enum LinkedEffectType { StaticRay, DynamicRay, Flare, Light, EffectStack }
        [System.Serializable]
        public struct LumenMesh
        {
            public string displayName;
            public Mesh mesh;
            public LinkedEffectType linkedEffectType;
        }
        public List<LumenMesh> lumenMeshes;
    }
}