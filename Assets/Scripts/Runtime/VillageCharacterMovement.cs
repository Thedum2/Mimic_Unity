using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3000)]
    public sealed class VillageCharacterMovement : MonoBehaviour
    {
        [SerializeField] private bool isLocalPlayer = true;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float rotateSpeed = 12f;
        [SerializeField] private float externalInputTimeout = 0.25f;
        [SerializeField] private bool enableManualInput = true;
        [SerializeField] private KeyCode forwardKey = KeyCode.W;
        [SerializeField] private KeyCode backwardKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float clickReachTolerance = 0.15f;
        [SerializeField] private bool useMouseClickMovement = true;
        [SerializeField] private int mouseButtonForMove = 0;
        [SerializeField] private GameObject clickEffectPrefab;
        [SerializeField] private float clickEffectDuration = 1f;
        [SerializeField] private float clickEffectFadeDuration = 0.25f;
        [SerializeField] private float clickEffectScale = 1f;
        [SerializeField] private bool useFallbackClickMarker = true;
        [SerializeField] private Color clickEffectColor = Color.yellow;

        private const float DirectionMagnitudeEpsilon = 0.0001f;

        private CharacterController _characterController;
        private Rigidbody _rigidbody;
        private Vector3 _moveDirection;
        private bool _hasInput;
        private Vector2? _externalInput;
        private float _externalInputTime;
        private Vector3 _mouseDestination;
        private bool _hasMouseDestination;
        private GameObject _activeClickEffect;
        private Coroutine _activeClickEffectFadeRoutine;

        public bool IsLocalPlayer => isLocalPlayer;
        public float MoveSpeed => moveSpeed;
        public float LastSpeed { get; private set; }
        public Vector3 Forward => _moveDirection.sqrMagnitude > DirectionMagnitudeEpsilon
            ? _moveDirection.normalized
            : transform.forward;

        public void SetLocalPlayer(bool local)
        {
            isLocalPlayer = local;
            if (!isLocalPlayer)
            {
                _externalInput = null;
            }
        }

        public void SetManualInputEnabled(bool enabled)
        {
            enableManualInput = enabled;
            if (!enableManualInput)
            {
                _externalInput = null;
                _hasInput = false;
                LastSpeed = 0f;
            }
        }

        public void ApplyInput(Vector2 input)
        {
            _externalInput = input;
            _externalInputTime = Time.time;
        }

        public void ApplyNetworkMovement(Vector2 input, float deltaTime)
        {
            ApplyMovementInternal(input, deltaTime, true);
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = true;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                return;
            }

            var input = ReadInput();
            ApplyMovementInternal(input, Time.deltaTime, true);
        }

        public void ApplyMovementInternal(Vector2 input, float deltaTime, bool shouldRotate)
        {
            var clampedInput = Vector2.ClampMagnitude(input, 1f);
            _moveDirection = new Vector3(clampedInput.x, 0f, clampedInput.y);
            _hasInput = _moveDirection.sqrMagnitude > DirectionMagnitudeEpsilon;

            if (_hasInput)
            {
                _moveDirection = _moveDirection.normalized;
                LastSpeed = moveSpeed;
                var velocity = _moveDirection * moveSpeed;
                Move(velocity * deltaTime);

                if (shouldRotate)
                {
                    var look = Quaternion.LookRotation(_moveDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotateSpeed * Time.deltaTime));
                }
            }
            else
            {
                LastSpeed = 0f;
            }
        }

        private void Move(Vector3 worldDelta)
        {
            if (_characterController != null)
            {
                _characterController.Move(worldDelta);
            }
            else if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.MovePosition(_rigidbody.position + worldDelta);
            }
            else
            {
                transform.position += worldDelta;
            }
        }

        private Vector2 ReadInput()
        {
            var external = ReadExternalInput();
            if (external.HasValue)
            {
                return external.Value;
            }

            if (useMouseClickMovement)
            {
                var mouseInput = ReadMouseInput();
                if (mouseInput.HasValue)
                {
                    return mouseInput.Value;
                }

                return Vector2.zero;
            }

            if (!enableManualInput)
            {
                return Vector2.zero;
            }
            return Vector2.zero;
        }

        private Vector2? ReadMouseInput()
        {
            if (!enableManualInput || !useMouseClickMovement)
            {
                return null;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return null;
            }

            if (Input.GetMouseButtonDown(mouseButtonForMove) && TrySetMouseDestinationFromPointer())
            {
                return BuildMouseInput();
            }

            if (_hasMouseDestination)
            {
                return BuildMouseInput();
            }

            return Vector2.zero;
        }

        private bool TrySetMouseDestinationFromPointer()
        {
            var activeCamera = inputCamera != null ? inputCamera : Camera.main;
            if (activeCamera == null)
            {
                return false;
            }

            var ray = activeCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, clickMask, QueryTriggerInteraction.Ignore))
            {
                _mouseDestination = hit.point;
                _hasMouseDestination = true;
                SpawnClickEffect(hit.point);
                return true;
            }

            return false;
        }

        private Vector2 BuildMouseInput()
        {
            if (!_hasMouseDestination)
            {
                return Vector2.zero;
            }

            var delta = _mouseDestination - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude <= clickReachTolerance * clickReachTolerance)
            {
                _hasMouseDestination = false;
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(new Vector2(delta.x, delta.z), 1f);
        }

        private void SpawnClickEffect(Vector3 worldPosition)
        {
            if (clickEffectDuration <= 0f)
            {
                return;
            }

            RemoveActiveClickEffect();

            GameObject marker;
            if (clickEffectPrefab != null)
            {
                marker = Instantiate(clickEffectPrefab, worldPosition, Quaternion.identity);
                marker.transform.localScale = Vector3.one * clickEffectScale;
                ApplyMarkerColor(marker);
            }
            else if (useFallbackClickMarker)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "FallbackMovePointer";
                marker.transform.position = worldPosition;
                marker.transform.localScale = new Vector3(clickEffectScale, 0.02f, clickEffectScale);

                var collider = marker.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
                    renderer.material = fallbackMaterial;
                }

                ApplyMarkerColor(marker);
            }
            else
            {
                return;
            }

            marker.transform.position = worldPosition;
            _activeClickEffect = marker;
            _activeClickEffectFadeRoutine = StartCoroutine(FadeOutAndDestroyClickEffect(marker, clickEffectDuration));
        }

        private void ApplyMarkerColor(GameObject marker)
        {
            SetMarkerColor(marker, clickEffectColor);
        }

        private void SetMarkerColor(GameObject marker, Color color)
        {
            if (marker == null)
            {
                return;
            }

            var renderers = marker.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    if (TrySetColor(material, color))
                    {
                        continue;
                    }
                }
            }
        }

        private bool TrySetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                material.color = color;
                return true;
            }

            return false;
        }

        private void SetMarkerAlpha(GameObject marker, float alpha)
        {
            if (marker == null)
            {
                return;
            }

            var renderers = marker.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    Color? color = null;
                    if (material.HasProperty("_BaseColor"))
                    {
                        color = material.GetColor("_BaseColor");
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        color = material.GetColor("_Color");
                    }

                    if (color.HasValue == false)
                    {
                        continue;
                    }

                    var next = color.Value;
                    next.a = alpha;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", next);
                    }
                    else
                    {
                        material.color = next;
                    }
                }
            }
        }

        private IEnumerator FadeOutAndDestroyClickEffect(GameObject marker, float lifetime)
        {
            if (marker == null)
            {
                yield break;
            }

            var fadeDuration = Mathf.Max(0.01f, Mathf.Min(clickEffectFadeDuration, lifetime));
            var visibleDuration = Mathf.Max(0f, lifetime - fadeDuration);
            if (visibleDuration > 0f)
            {
                yield return new WaitForSeconds(visibleDuration);
                if (marker == null)
                {
                    yield break;
                }
            }

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (marker == null)
                {
                    break;
                }

                var t = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                SetMarkerAlpha(marker, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }

            if (ReferenceEquals(_activeClickEffect, marker))
            {
                _activeClickEffect = null;
            }

            _activeClickEffectFadeRoutine = null;
        }

        private void RemoveActiveClickEffect()
        {
            if (_activeClickEffect != null)
            {
                if (_activeClickEffectFadeRoutine != null)
                {
                    StopCoroutine(_activeClickEffectFadeRoutine);
                    _activeClickEffectFadeRoutine = null;
                }

                Destroy(_activeClickEffect);
                _activeClickEffect = null;
            }
        }

        private Vector2? ReadExternalInput()
        {
            if (!_externalInput.HasValue)
            {
                return null;
            }

            if (Time.time - _externalInputTime > externalInputTimeout)
            {
                _externalInput = null;
                return null;
            }

            return _externalInput;
        }
    }
}
