// Distant Lands 2025
// Lumen: Stylized Light FX 2
// The contents of this file are protected under the Unity Asset Store EULA
//
// Updates a Lumen Effect Player's light direction to follow a specific target

using UnityEngine;

namespace DistantLands.Lumen
{
    [RequireComponent(typeof(LumenEffectPlayer))]
    public class SetLightRayDirection : MonoBehaviour
    {
        private LumenEffectPlayer player;
        [SerializeField]
        private Transform target;

        void OnEnable()
        {
            player = GetComponent<LumenEffectPlayer>();
        }

        // Update is called once per frame
        void Update()
        {
            player.localSunDirection = -(target.position - transform.position).normalized;
            player.RedoEffect();
        }
    }
}
