using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Lumen
{
    [ExecuteAlways]
    public class LumenSun : MonoBehaviour
    {


        // Update is called once per frame
        void LateUpdate()
        {

            Shader.SetGlobalVector("LUMEN_SunDir", -transform.forward);

        }
    }
}