namespace UnityEngine.Rendering {
    public static class MelodyColorUtils {
        /// <summary>
        /// Factor used for lens system w.r.t. exposure calculation. Modifying this will lead to a change on how linear exposure
        /// multipliers are computed from EV100 values (and viceversa). s_LensAttenuation models transmission attenuation and lens vignetting.
        /// Note that according to the standard ISO 12232, a lens saturates at s_LensAttenuation = 0.78f (under ISO 100).
        /// </summary>
        static public float s_LensAttenuation = 0.65f;

        /// <summary>
        /// Scale applied to exposure caused by lens imperfection. It is computed from s_LensAttenuation as follow:
        ///  (78 / ( S * q )) where S = 100 and q = s_LensAttenuation
        /// </summary>
        static public float lensImperfectionExposureScale {
            get => (78.0f / (100.0f * s_LensAttenuation));
        }


    }
}
