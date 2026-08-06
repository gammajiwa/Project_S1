using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Walks the caster out of trouble on its own.
    ///
    /// This does not hand control back to the player — you still never steer. What changes is that
    /// movement becomes part of the build instead of being absent: <see cref="StatKind.MoveSpeed"/>
    /// is a stat you invest in, exactly like damage or defence.
    ///
    /// <b>It is meant to fail.</b> A dodge that always works would delete every HP and defence piece
    /// in the book, because nothing would ever touch you. So the speed sits only a little above the
    /// swarm's, the turn rate is slow enough to be caught wrong-footed, and — the important part —
    /// a ring closing from every side cancels itself out. Surrounded, the flee vector is nearly zero
    /// and the caster stands still and dies. That is the intended shape of the failure.
    /// </summary>
    public class PlayerMotor : MonoBehaviour
    {
        EnemyManager _enemies;
        PlayerCaster _caster;
        GameBalance _balance;

        Vector3 _heading;

        public void Init(EnemyManager enemies, PlayerCaster caster, GameBalance balance)
        {
            _enemies = enemies;
            _caster = caster;
            _balance = balance;
        }

        void Update()
        {
            if (_caster == null || !_caster.Alive) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            Vector3 want = _enemies.CrowdPressure(pos, _balance.DangerRadius);

            // Tidak ada yang dekat: berhenti di tempat.
            //
            // Sebelumnya ia menyeret diri kembali ke tengah tiap kali lapangan sepi. Itu dibuat
            // untuk mengembalikan posisi antar-wave tanpa kasus khusus, dan efek sampingnya jauh
            // lebih besar dari gunanya: tiap kali pemain berhasil melepaskan diri ke satu sisi,
            // ia langsung ditarik balik ke tengah tanpa diminta — jadi lari ke sudut tidak pernah
            // benar-benar berarti, dan tempat berdiri berhenti jadi keputusan pemain.
            if (want.sqrMagnitude < 0.0001f) want = Vector3.zero;

            _heading = Vector3.MoveTowards(_heading, want, _balance.TurnRate * dt);

            float speed = Mathf.Max(0f, _balance.BaseMoveSpeed + _caster.Total(StatKind.MoveSpeed));
            pos += _heading * (speed * dt);

            transform.position = Clamp(pos);
        }

        /// <summary>
        /// Menahan pemain di dalam elips arena.
        ///
        /// Sempat dicopot waktu lapangannya dibuat tak berujung, dan itu keliru. Seluruh perilaku
        /// motor ini adalah MENJAUH dari tekanan kerumunan — tanpa dinding, menjauh selalu
        /// berhasil. Pemain berjalan lurus selamanya, gerombolan mengekor tanpa pernah menyusul,
        /// dan tidak ada satu pun alasan untuk melawan. Wave tidak pernah selesai karena tidak
        /// ada yang mati.
        ///
        /// Jadi dinding ini bukan pembatas teknis, dia bagian dari permainannya: terjepit di tepi
        /// adalah cara kalah yang sah, dan justru itu yang membuat kerumunan jadi ancaman.
        /// </summary>
        Vector3 Clamp(Vector3 pos)
        {
            float nx = pos.x / _balance.ArenaHalfX;
            float nz = pos.z / _balance.ArenaHalfZ;
            float outside = nx * nx + nz * nz;

            if (outside > 1f)
            {
                float scale = 1f / Mathf.Sqrt(outside);
                pos.x *= scale;
                pos.z *= scale;
            }

            return pos;
        }
    }
}
