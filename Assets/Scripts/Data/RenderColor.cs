using UnityEngine;

namespace Proto
{
    /// <summary>
    /// This project renders in Linear space, and colours written from script reach the shader
    /// untouched — so an authored sRGB value shows up washed out. Materials, camera backgrounds
    /// and UI graphics all need converting.
    ///
    /// Lights and ambient are the exception: <c>GraphicsSettings.lightsUseLinearIntensity</c> is
    /// on, so Unity already converts those. Passing them through here would darken them twice.
    /// </summary>
    public static class RenderColor
    {
        public static Color Of(Color authored) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? authored.linear : authored;
    }
}
