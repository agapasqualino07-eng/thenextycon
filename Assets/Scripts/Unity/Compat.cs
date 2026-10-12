using UnityEngine;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>Thin wrappers for APIs renamed between Unity versions (keeps the rest of the code warning-free on Unity 6).</summary>
    public static class Compat
    {
        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
