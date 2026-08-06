using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Floating damage numbers over the swarm.
    ///
    /// The hard part is not drawing them, it is <b>not</b> drawing forty of them. One Blizzard
    /// covers thirty enemies in a single frame, and a zone re-ticks every 0.4s on the same crowd —
    /// one label per hit turns the battlefield into unreadable soup. So hits that land close
    /// together in space and time merge into one growing number instead.
    ///
    /// Size and warmth ride on the share of the target's max HP, not on the raw number: 40 damage
    /// means something very different at wave 2 and at wave 20, and the popup should read the same
    /// way the player feels it.
    /// </summary>
    public class DamagePopups
    {
        const int PoolSize = 48;

        const float LifeSpan = 0.75f;

        /// <summary>How long a number stays open for more hits to be folded into it.</summary>
        const float MergeWindow = 0.35f;

        const float MergeRadius = 1.4f;

        const float RiseSpeed = 1.9f;

        static readonly Color LightHit = new Color(1f, 0.96f, 0.86f);
        static readonly Color HeavyHit = new Color(1f, 0.45f, 0.16f);

        class Popup
        {
            public Text Label;
            public RectTransform Rect;
            public Vector3 World;
            public float Amount;
            public float Life;
            public float Age;
            public float Bump;
        }

        readonly Popup[] _pool = new Popup[PoolSize];
        readonly Camera _camera;

        public DamagePopups(Transform canvas, Font font, Camera camera)
        {
            _camera = camera;

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Dmg_{i}");
                go.transform.SetParent(canvas, false);

                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = 18;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.text = "";

                // The floor is dark but spell flashes are not — without a drop shadow the numbers
                // vanish exactly when the screen is busiest.
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);

                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(220f, 40f);

                _pool[i] = new Popup { Label = text, Rect = rect };
            }
        }

        /// <summary>
        /// One damage event. <paramref name="maxHp"/> is what turns a raw number into a readable
        /// one — it decides how big and how hot the label gets.
        /// </summary>
        public void Push(Vector3 world, float amount, float maxHp)
        {
            if (amount <= 0f) return;

            var target = FindMergeTarget(world);
            if (target != null)
            {
                target.Amount += amount;
                target.Life = LifeSpan;
                target.Bump = 1f;
                Apply(target, maxHp);
                return;
            }

            var slot = FreeSlot();
            slot.World = world + Vector3.up * 1.1f;
            slot.Amount = amount;
            slot.Life = LifeSpan;
            slot.Age = 0f;
            slot.Bump = 1f;
            Apply(slot, maxHp);
        }

        Popup FindMergeTarget(Vector3 world)
        {
            float bestSqr = MergeRadius * MergeRadius;
            Popup best = null;

            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];
                if (p.Life <= 0f || p.Age > MergeWindow) continue;

                Vector3 d = p.World - world;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = p;
            }

            return best;
        }

        /// <summary>An idle label, or the one closest to expiring when the pool is saturated.</summary>
        Popup FreeSlot()
        {
            Popup weakest = _pool[0];

            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];
                if (p.Life <= 0f) return p;
                if (p.Life < weakest.Life) weakest = p;
            }

            return weakest;
        }

        /// <summary>
        /// Font size is set here rather than per frame on purpose: changing it rebuilds the text
        /// mesh, and doing that every frame for 48 labels shows up in the profiler. The per-frame
        /// animation rides on localScale instead, which is free.
        /// </summary>
        static void Apply(Popup p, float maxHp)
        {
            float share = maxHp <= 0f ? 0f : Mathf.Clamp01(p.Amount / maxHp);

            p.Label.text = Format(p.Amount);
            p.Label.fontSize = Mathf.RoundToInt(Mathf.Lerp(16f, 34f, Mathf.Clamp01(share * 1.8f)));
            p.Label.color = Color.Lerp(LightHit, HeavyHit, Mathf.Clamp01(share * 2.2f));
        }

        static string Format(float amount) =>
            amount >= 10f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#");

        /// <summary>
        /// Driven with unscaled time: at 5x speed a scaled label would be gone before the eye
        /// registers it, and between waves time is stopped entirely while numbers still need to fade.
        /// </summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];

                if (p.Life <= 0f)
                {
                    if (p.Label.text.Length > 0) p.Label.text = "";
                    continue;
                }

                p.Life -= dt;
                p.Age += dt;

                if (p.Life <= 0f)
                {
                    p.Label.text = "";
                    continue;
                }

                p.World += Vector3.up * (RiseSpeed * dt);
                p.Bump = Mathf.MoveTowards(p.Bump, 0f, dt * 5f);

                var screen = _camera.WorldToScreenPoint(p.World);
                p.Rect.anchoredPosition = new Vector2(screen.x, screen.y);
                p.Rect.localScale = Vector3.one * (1f + p.Bump * 0.45f);

                var c = p.Label.color;
                c.a = Mathf.Clamp01(p.Life / (LifeSpan * 0.5f));
                p.Label.color = c;
            }
        }
    }
}
