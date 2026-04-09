using Mimic.Bridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimic.Gameplay
{
    public sealed class RuntimeReadyBootstrapEmitter : MonoBehaviour
    {
        private const string MatchManagerRoute = "MatchManager";
        private bool _sentForCurrentScene;
        private string _lastSceneName = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("RuntimeReadyBootstrapEmitter");
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeReadyBootstrapEmitter>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            TryEmitRuntimeReady();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastSceneName = scene.name;
            _sentForCurrentScene = false;
            TryEmitRuntimeReady();
        }

        private void TryEmitRuntimeReady()
        {
            if (_sentForCurrentScene)
            {
                return;
            }

            // When the scene has the dedicated bridge adapter, it owns RuntimeReady emission.
            if (FindObjectOfType<LobbySceneBridgeAdapter>() != null)
            {
                _sentForCurrentScene = true;
                return;
            }

            var bridge = BridgeManager.Instance;
            if (bridge == null)
            {
                return;
            }

            var matchHandler = bridge.GetHandler<MatchHandler>(MatchManagerRoute);
            if (matchHandler == null)
            {
                return;
            }

            var sceneName = string.IsNullOrWhiteSpace(_lastSceneName)
                ? SceneManager.GetActiveScene().name
                : _lastSceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneName = "LobbyScene";
            }

            matchHandler.RuntimeReady(string.Empty, true, sceneName);
            _sentForCurrentScene = true;
            Debug.Log($"[RuntimeReadyBootstrapEmitter] Emit RuntimeReady scene={sceneName}");
        }
    }
}
