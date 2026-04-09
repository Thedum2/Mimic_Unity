using System.Linq;
using UnityEngine;
using Mimic.Gameplay;
using Fusion;

[AddComponentMenu("Mimic/Isometric Follow Camera")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-1000)]
public sealed class IsometricFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private float followHeight = 10f;
    [SerializeField] private float followDistance = 8.5f;
    [SerializeField] private float followLerpSpeed = 10f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float pitch = 48f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private bool autoLookAtTarget = false;
    [SerializeField] private bool orthographicMode = true;
    [SerializeField] private float orthographicSize = 10f;

    private Camera _camera;
    private Transform _resolvedTargetCache;
    private float _nextTargetLogTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (orthographicMode)
        {
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
        }
    }

    private void LateUpdate()
    {
        var target = ResolveTarget();
        if (_resolvedTargetCache != target)
        {
            _resolvedTargetCache = target;
        }

        if (target == null)
        {
            if (Time.time >= _nextTargetLogTime)
            {
                Debug.LogWarning("[IsometricFollowCamera] target still not assigned. Waiting for local player spawn/authority.");
                _nextTargetLogTime = Time.time + 2f;
            }

            return;
        }

        var wantedRotation = Quaternion.Euler(pitch, yaw, 0f);
        var wantedPosition = target.position + targetOffset - wantedRotation * Vector3.forward * followDistance;
        wantedPosition.y = target.position.y + followHeight;

        var t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, wantedPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, t);

        if (autoLookAtTarget)
        {
            transform.LookAt(target.position + targetOffset + Vector3.up * 1.2f);
        }
    }

    private Transform ResolveTarget()
    {
        var localPlayer = VillagePlayerSync.LocalPlayer;
        if (localPlayer != null && localPlayer.transform != null && IsLocalAuthority(localPlayer))
        {
            return localPlayer.transform;
        }

        var runnerLocalTarget = ResolveByRunnerLocalPlayer();
        if (runnerLocalTarget != null)
        {
            return runnerLocalTarget;
        }

        if (targetTransform != null)
        {
            return targetTransform;
        }

        var syncPlayer = UnityEngine.Object.FindObjectsOfType<VillagePlayerSync>()
            .FirstOrDefault(x => x != null && x.Runner != null && x.Runner.IsRunning && IsLocalAuthority(x));
        if (syncPlayer != null)
        {
            return syncPlayer.transform;
        }

        var localMovementPlayer = UnityEngine.Object.FindObjectsOfType<VillageCharacterMovement>()
            .FirstOrDefault(x => x != null && x.IsLocalPlayer);
        if (localMovementPlayer != null)
        {
            return localMovementPlayer.transform;
        }

        var anyStateAuthority = UnityEngine.Object.FindObjectsOfType<VillagePlayerSync>()
            .FirstOrDefault(x => x != null && x.Runner != null && x.Runner.IsRunning && x.HasStateAuthority);
        if (anyStateAuthority != null)
        {
            return anyStateAuthority.transform;
        }

        return null;
    }

    private static Transform ResolveByRunnerLocalPlayer()
    {
        var runners = UnityEngine.Object.FindObjectsOfType<NetworkRunner>();
        for (var i = 0; i < runners.Length; i++)
        {
            var runner = runners[i];
            if (runner == null || runner.LocalPlayer == PlayerRef.None)
            {
                continue;
            }

            var playerObject = runner.GetPlayerObject(runner.LocalPlayer);
            if (playerObject == null)
            {
                continue;
            }

            var sync = playerObject.GetComponent<VillagePlayerSync>();
            if (sync == null || sync.transform == null)
            {
                continue;
            }

            return sync.transform;
        }

        return null;
    }

    private static bool IsLocalAuthority(VillagePlayerSync candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.HasInputAuthority)
        {
            return true;
        }

        return candidate.Object != null
            && candidate.Runner != null
            && candidate.Runner.LocalPlayer != PlayerRef.None
            && candidate.Object.InputAuthority == candidate.Runner.LocalPlayer;
    }

    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    public void ForceRefreshTarget()
    {
        targetTransform = ResolveTarget();
    }
}
