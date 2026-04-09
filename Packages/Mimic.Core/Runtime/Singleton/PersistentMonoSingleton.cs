using UnityEngine;

namespace Mimic.Core
{
    public abstract class PersistentMonoSingleton<T> : MonoSingleton<T> where T : MonoSingleton<T>
    {
        [Tooltip("if this is true, this singleton will auto detach if it finds itself parented on awake")]
        [SerializeField] private bool unparentOnAwake = true;

        protected override void OnInitializing()
        {
            if (unparentOnAwake)
            {
                transform.SetParent(null);
            }

            base.OnInitializing();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}
