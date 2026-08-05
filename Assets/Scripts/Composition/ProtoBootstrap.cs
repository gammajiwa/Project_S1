using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Proto
{
    /// <summary>
    /// Builds the whole prototype scene at runtime so there is nothing to wire by hand.
    /// Drop this on one empty GameObject and press Play.
    /// </summary>
    public class ProtoBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] ContentDatabase _database;
        [SerializeField] GameBalance _balance;

        [Header("Tampilan")]
        [Tooltip("Dipakai bareng scene menu, supaya dua-duanya tidak pernah beda cahaya.")]
        [SerializeField] SceneLook _look;

        void Awake()
        {
            if (_database == null || _balance == null || _look == null)
            {
                Debug.LogError("[ProtoBootstrap] ContentDatabase / GameBalance / SceneLook " +
                               "belum diisi di Inspector.", this);
                enabled = false;
                return;
            }

            Application.runInBackground = true;

            // Options are owned by the menu, but a run started from it must inherit them.
            GameSettings.Load().Apply();

            _look.ApplyEnvironment();

            var cam = BuildCamera();
            BuildVolume();
            BuildLight();
            BuildGround();

            var playerGo = BuildPlayer();

            var managerGo = new GameObject("EnemyManager");
            managerGo.transform.SetParent(transform, false);
            var enemies = managerGo.AddComponent<EnemyManager>();

            var caster = playerGo.AddComponent<PlayerCaster>();

            enemies.Init(playerGo.transform, caster, _balance, _database);
            caster.Init(enemies, _database, _balance);

            var shake = cam.gameObject.AddComponent<CameraShake>();

            // Burst radius drives the kick, so a screen-clearing reaction outweighs a small one.
            enemies.OnReaction += (at, reaction) => shake.Add(0.16f + reaction.BurstRadius * 0.045f);

            // Single-target casts stay quiet; only the ones that cover ground get a nudge.
            caster.OnCast += inst => { if (IsHeavy(inst.Def.Kind)) shake.Add(0.07f); };

            var uiGo = new GameObject("GrimoireUI");
            uiGo.transform.SetParent(transform, false);
            var ui = uiGo.AddComponent<GrimoireUI>();
            ui.Init(caster, enemies, cam, _database, _balance);
        }

        static bool IsHeavy(CastKind kind) =>
            kind == CastKind.Nova || kind == CastKind.AreaAtTarget || kind == CastKind.Line;

        Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 18.5f, -7.5f);
            go.transform.rotation = Quaternion.Euler(68f, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderColor.Of(_look.HorizonColor);

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            return cam;
        }

        void BuildVolume()
        {
            if (_look.PostProcess == null) return;

            var go = new GameObject("Global Volume");
            go.transform.SetParent(transform, false);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = _look.PostProcess;
        }

        void BuildLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            _look.ApplySun(go.AddComponent<Light>());
        }

        void BuildGround()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(5f, 1f, 5f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _look.CreateSurface(_look.GroundColor);

            // The floor is what the long shadows fall on — that is most of the look.
            r.receiveShadows = true;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        GameObject BuildPlayer()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0.9f, 0f);
            go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _look.CreateSurface(_look.PlayerColor);

            // Enemy shadows stay off for the 200-enemy budget; the player is a single caster.
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }
    }
}
