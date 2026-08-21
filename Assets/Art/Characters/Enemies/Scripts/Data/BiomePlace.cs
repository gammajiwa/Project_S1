using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Satu TEMPAT: sekumpulan wajah arena (siang, senja, malam, tengah malam) yang menceritakan
    /// lokasi yang sama pada jam yang berbeda.
    ///
    /// Lapisan ini perlu ada karena "biome" di project ini sudah telanjur berarti WAJAH — empat
    /// aset forest yang ada bukan empat lokasi melainkan empat jam di satu hutan, dan seluruh
    /// undian waktu-hari per wave dibangun di atas pengertian itu. Salju bukan jam kelima; ia
    /// hutan yang lain, dengan empat jamnya sendiri.
    ///
    /// <see cref="BiomeDresser"/> tetap hanya mengenal satu daftar wajah. Yang berpindah tempat
    /// cuma menukar daftar itu lewat <c>UseFaces</c> — undian waktu-hari, bilah demo, dan cuaca
    /// tidak tahu apa-apa soal tempat, dan memang tidak perlu tahu.
    /// </summary>
    [CreateAssetMenu(fileName = "Place_", menuName = "Grimoire/Biome Place")]
    public class BiomePlace : ScriptableObject
    {
        public string Id;
        public string DisplayName = "Forest";

        [Tooltip("Wajah-wajah tempat ini, urut: siang, malam, senja, tengah malam — urutan yang " +
                 "sama dengan undian waktu-hari di BiomeDresser.")]
        public BiomeDefinition[] Faces;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("place_", "");
        }
    }
}
