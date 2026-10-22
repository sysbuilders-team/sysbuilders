using UnityEngine;

namespace UI
{
    /// <summary>
    /// Throttled scene-wide service lookup for the cases where a MonoBehaviour
    /// cannot receive its dependency through the Inspector.
    ///
    /// Without the throttle, a missing port turns EnsureSource-style helpers
    /// into per-frame FindObjectsByType calls. That method allocates an array
    /// and walks every MonoBehaviour in every loaded scene, so on a
    /// misconfigured lobby panel it can eat a millisecond or more per frame
    /// while silently returning null.
    ///
    /// Usage: keep the cache and the next-retry timestamp as instance fields
    /// on the caller and pass them by ref. The helper performs at most one
    /// scene scan per retrySeconds; every other call is O(1).
    /// </summary>
    public static class SceneServices
    {
        private const float DefaultRetrySeconds = 1f;

        public static bool TryResolve<T>(ref T cache, ref float nextTry, float retrySeconds = DefaultRetrySeconds)
            where T : class
        {
            if (cache != null) return true;

            float now = Time.unscaledTime;
            if (now < nextTry) return false;
            nextTry = now + retrySeconds;

            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is T resolved)
                {
                    cache = resolved;
                    return true;
                }
            }

            return false;
        }
    }
}