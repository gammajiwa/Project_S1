using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pooled LineRenderers for everything the caster draws as a line: chain-lightning hops and
    /// sweeping beams.
    ///
    /// It owns its own material rather than borrowing the caster's. URP's Unlit shader ignores
    /// LineRenderer vertex colours, so a shared material would render every bolt white and give no
    /// way to fade one out — <c>Sprites/Default</c> is alpha-blended and vertex-coloured, which is
    /// exactly the two things a bolt needs.
    /// </summary>
    public class BoltPool
    {
        /// <summary>Points per arc. Enough kinks to read as lightning, few enough to stay cheap.</summary>
        const int ArcPoints = 7;

        const int MaxBolts = 48;

        class Bolt
        {
            public LineRenderer Line;
            public float Life;
            public float MaxLife;
            public Color Tint;
            public bool Active;
        }

        readonly List<Bolt> _bolts = new List<Bolt>(16);
        readonly Transform _root;
        readonly Material _material;

        public Material Material => _material;

        public BoltPool(Transform root)
        {
            _root = root;

            // Grimoire/LaserBeam: inti panas + halo lembut, additive — pita datar
            // Sprites/Default terbaca sebagai penanda prototype ("kasar banget",
            // 2026-08-12). Kontraknya sama persis: vertex color untuk tint, alpha
            // untuk fade, jadi seluruh kode di bawah tidak berubah satu baris pun.
            var shader = Shader.Find("Grimoire/LaserBeam");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            _material = new Material(shader);
        }

        /// <summary>A jagged hop, for chain lightning. Jitter is perpendicular to the hop.</summary>
        public void Arc(Vector3 from, Vector3 to, Color color, float width = 0.18f, float life = 0.18f)
        {
            var bolt = Take();
            if (bolt == null) return;

            Vector3 delta = to - from;
            Vector3 side = Vector3.Cross(delta.normalized, Vector3.up);
            float spread = Mathf.Min(1.1f, delta.magnitude * 0.14f);

            bolt.Line.positionCount = ArcPoints;
            for (int i = 0; i < ArcPoints; i++)
            {
                float t = i / (float)(ArcPoints - 1);
                Vector3 p = Vector3.Lerp(from, to, t);

                // Ends stay pinned to the two enemies; only the middle is allowed to wander.
                float wobble = Mathf.Sin(t * Mathf.PI) * spread;
                p += side * Random.Range(-wobble, wobble);
                p.y = 0.75f;

                bolt.Line.SetPosition(i, p);
            }

            Arm(bolt, color, width, life);
        }

        /// <summary>A straight sweep, for line skills. Wider and flatter than an arc.</summary>
        public void Beam(Vector3 from, Vector3 to, Color color, float width, float life = 0.22f)
        {
            var bolt = Take();
            if (bolt == null) return;

            bolt.Line.positionCount = 2;
            bolt.Line.SetPosition(0, new Vector3(from.x, 0.5f, from.z));
            bolt.Line.SetPosition(1, new Vector3(to.x, 0.5f, to.z));

            Arm(bolt, color, width, life);
        }

        static void Arm(Bolt bolt, Color color, float width, float life)
        {
            bolt.Line.widthMultiplier = width;
            bolt.Tint = color;
            bolt.Life = life;
            bolt.MaxLife = life;
            bolt.Active = true;
            bolt.Line.enabled = true;
            bolt.Line.startColor = bolt.Line.endColor = color;
        }

        Bolt Take()
        {
            for (int i = 0; i < _bolts.Count; i++)
            {
                if (!_bolts[i].Active) return _bolts[i];
            }

            // Saturated: steal the one closest to expiring rather than growing without bound.
            if (_bolts.Count >= MaxBolts)
            {
                Bolt weakest = _bolts[0];
                for (int i = 1; i < _bolts.Count; i++)
                {
                    if (_bolts[i].Life < weakest.Life) weakest = _bolts[i];
                }

                return weakest;
            }

            var go = new GameObject("Bolt");
            go.transform.SetParent(_root, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = _material;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var bolt = new Bolt { Line = line };
            _bolts.Add(bolt);
            return bolt;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _bolts.Count; i++)
            {
                var bolt = _bolts[i];
                if (!bolt.Active) continue;

                bolt.Life -= dt;
                if (bolt.Life <= 0f)
                {
                    bolt.Active = false;
                    bolt.Line.enabled = false;
                    continue;
                }

                var faded = bolt.Tint;
                faded.a = bolt.Tint.a * Mathf.Clamp01(bolt.Life / bolt.MaxLife);
                bolt.Line.startColor = bolt.Line.endColor = faded;
            }
        }
    }
}
