using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Memasang capsule collider di tulang lengan/badan lalu mendaftarkannya ke komponen Cloth
/// pada jubah, supaya jubah tidak ditembus tangan.
///
/// Cloth di Unity hanya mengenali collider yang terdaftar di daftarnya sendiri, dan hanya
/// tipe Capsule dan Sphere. Collider lain di scene diabaikan sepenuhnya - itulah sebabnya
/// memasang collider saja tidak cukup, harus didaftarkan.
/// </summary>
public static class LizMageClothSetup
{
    // tulang yang perlu menolak jubah, beserta jari-jari capsule-nya (meter)
    static readonly (string bone, float radius)[] Targets =
    {
        ("mixamorig:LeftArm",      0.055f),
        ("mixamorig:LeftForeArm",  0.045f),
        ("mixamorig:LeftHand",     0.045f),
        ("mixamorig:RightArm",     0.055f),
        ("mixamorig:RightForeArm", 0.045f),
        ("mixamorig:RightHand",    0.045f),
        ("mixamorig:Spine1",       0.130f),   // torso: menahan jubah agar tidak masuk ke badan
        ("mixamorig:LeftUpLeg",    0.070f),
        ("mixamorig:RightUpLeg",   0.070f),
    };

    const string ChildName = "ClothCollider";

    [MenuItem("Tools/LizMage/Pasang Collider Jubah")]
    static void Setup()
    {
        var root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("LizMage", "Pilih dulu GameObject karakternya di Hierarchy.", "OK");
            return;
        }

        var cloth = root.GetComponentInChildren<Cloth>(true);
        if (cloth == null)
        {
            EditorUtility.DisplayDialog("LizMage",
                "Tidak ada komponen Cloth di bawah objek ini.\n\n" +
                "Pasang dulu Cloth di objek jubah (LizMage_Cape), baru jalankan lagi.", "OK");
            return;
        }

        var bones = root.GetComponentsInChildren<Transform>(true)
                        .ToDictionary(t => t.name, t => t, System.StringComparer.Ordinal);

        var made = new List<CapsuleCollider>();
        var missing = new List<string>();

        foreach (var (boneName, radius) in Targets)
        {
            if (!bones.TryGetValue(boneName, out var bone)) { missing.Add(boneName); continue; }

            // pakai kembali anak yang sudah ada supaya menjalankan ulang tidak menumpuk objek
            var holder = bone.Find(ChildName);
            if (holder == null)
            {
                holder = new GameObject(ChildName).transform;
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "Buat collider jubah");
                holder.SetParent(bone, false);
            }
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;

            var cap = holder.GetComponent<CapsuleCollider>();
            if (cap == null) cap = Undo.AddComponent<CapsuleCollider>(holder.gameObject);

            // Panjang capsule diambil dari jarak ke tulang anak. Tulang ujung (hand) tidak punya
            // anak yang berarti, jadi dipakai panjang minimum supaya tetap berbentuk kapsul.
            float length = radius * 2f;
            var child = FirstChildBone(bone);
            if (child != null) length = Mathf.Max(child.localPosition.magnitude, radius * 2f);

            cap.radius = radius;
            cap.height = length;
            cap.direction = LongestAxis(child != null ? child.localPosition : Vector3.up);
            cap.center = (child != null ? child.localPosition : Vector3.up * length) * 0.5f;
            cap.isTrigger = false;

            made.Add(cap);
        }

        Undo.RecordObject(cloth, "Daftarkan collider ke Cloth");
        cloth.capsuleColliders = made.ToArray();
        EditorUtility.SetDirty(cloth);

        var msg = $"{made.Count} capsule collider dipasang dan didaftarkan ke Cloth '{cloth.name}'.";
        if (missing.Count > 0)
            msg += $"\n\nTulang tidak ditemukan (dilewati): {string.Join(", ", missing)}";
        msg += "\n\nCloth hanya bersimulasi saat Play. Tekan Play untuk melihat hasilnya.";
        Debug.Log("[LizMage] " + msg, cloth);
        EditorUtility.DisplayDialog("LizMage", msg, "OK");
    }

    [MenuItem("Tools/LizMage/Hapus Collider Jubah")]
    static void Clear()
    {
        var root = Selection.activeGameObject;
        if (root == null) return;

        int n = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t != null && t.name == ChildName) { Undo.DestroyObjectImmediate(t.gameObject); n++; }
        }
        var cloth = root.GetComponentInChildren<Cloth>(true);
        if (cloth != null)
        {
            Undo.RecordObject(cloth, "Kosongkan collider Cloth");
            cloth.capsuleColliders = new CapsuleCollider[0];
            EditorUtility.SetDirty(cloth);
        }
        Debug.Log($"[LizMage] {n} collider jubah dihapus.");
    }

    /// <summary>Anak pertama yang merupakan tulang, bukan objek bantu yang kita buat sendiri.</summary>
    static Transform FirstChildBone(Transform bone)
    {
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (c.name != ChildName) return c;
        }
        return null;
    }

    /// <summary>0 = X, 1 = Y, 2 = Z — sumbu yang paling searah dengan arah tulang.</summary>
    static int LongestAxis(Vector3 v)
    {
        var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return 0;
        if (a.y >= a.z) return 1;
        return 2;
    }
}
