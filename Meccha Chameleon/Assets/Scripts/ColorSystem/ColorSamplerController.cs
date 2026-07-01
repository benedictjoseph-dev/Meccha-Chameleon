using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ColorSystem
{
    /// <summary>
    /// A modular system that allows players to sample colors from the environment
    /// using a raycast, preview them in UI, lock them, and paint their character.
    /// </summary>
    public class ColorSamplerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The camera used for raycasting. Defaults to Camera.main if null.")]
        [SerializeField] private Camera _mainCamera;
        
        [Tooltip("The UI Image used to preview the sampled color.")]
        [SerializeField] private Image _colorSamplingUI;
        
        [Tooltip("The renderers on the player that will be painted.")]
        [SerializeField] private Renderer[] _playerRenderers;
        
        [Header("Settings")]
        [Tooltip("Which layers can be sampled for color?")]
        [SerializeField] private LayerMask _sampleLayerMask = ~0; // ~0 means Everything
        [SerializeField] private Color _defaultFallbackColor = Color.white;
        [SerializeField] private float _raycastDistance = 100f;

        // Internal State
        public bool IsSamplingMode { get; private set; }
        public Color LockedColor { get; private set; }
        
        private Color _hoveredColor;

        // Cached materials to avoid creating instances every frame during painting
        private Material[] _playerMaterials;

        private void Awake()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            
            _hoveredColor = _defaultFallbackColor;
            LockedColor = _defaultFallbackColor;
            
            if (_colorSamplingUI != null)
                _colorSamplingUI.gameObject.SetActive(false);

            CachePlayerMaterials();
        }

        private void Update()
        {
            HandleInput(out bool lockColorRequested, out bool paintRequested);
            
            if (IsSamplingMode)
            {
                PerformColorRaycast();
                UpdateUI();
            }

            if (lockColorRequested)
            {
                LockSampledColor();
            }

            if (paintRequested)
            {
                ApplyPainting();
            }
        }

        // ==========================================
        // 1. INPUT HANDLING
        // ==========================================
        private void HandleInput(out bool lockColorRequested, out bool paintRequested)
        {
            bool qPressed = false;
            bool qReleased = false;
            lockColorRequested = false;
            paintRequested = false;

            // Support both New Input System and Legacy Input System for drop-in flexibility
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Mouse.current != null)
            {
                qPressed = Keyboard.current.qKey.wasPressedThisFrame;
                qReleased = Keyboard.current.qKey.wasReleasedThisFrame;
                lockColorRequested = Mouse.current.leftButton.wasPressedThisFrame;
                paintRequested = Mouse.current.leftButton.isPressed;
            }
#else
            qPressed = Input.GetKeyDown(KeyCode.Q);
            qReleased = Input.GetKeyUp(KeyCode.Q);
            lockColorRequested = Input.GetMouseButtonDown(0);
            paintRequested = Input.GetMouseButton(0);
#endif

            // Toggle UI and sampling state based on Q key
            if (qPressed)
            {
                IsSamplingMode = true;
                if (_colorSamplingUI != null) _colorSamplingUI.gameObject.SetActive(true);
            }
            else if (qReleased)
            {
                IsSamplingMode = false;
                if (_colorSamplingUI != null) _colorSamplingUI.gameObject.SetActive(false);
            }
        }

        // ==========================================
        // 2. RAYCASTING & COLOR SAMPLING
        // ==========================================
        private GameObject _lastHitObject; // To prevent console spam

        private void PerformColorRaycast()
        {
            Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) mousePos = Mouse.current.position.ReadValue();
#else
            mousePos = Input.mousePosition;
#endif

            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            
            // Draw a red line in the Scene view so you can visually verify where the ray is going
            Debug.DrawRay(ray.origin, ray.direction * _raycastDistance, Color.red);
            
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _sampleLayerMask))
            {
                // Print what we hit! Very useful to see if the raycast is hitting the player's own body instead of the environment
                if (hit.collider.gameObject != _lastHitObject)
                {
                    Debug.Log($"[ColorSampler] Raycast hit object: '{hit.collider.gameObject.name}'");
                    _lastHitObject = hit.collider.gameObject;
                }

                // Try to find the renderer on the hit object, its children, or its parent
                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                if (hitRenderer == null) hitRenderer = hit.collider.GetComponentInChildren<Renderer>();
                if (hitRenderer == null) hitRenderer = hit.collider.GetComponentInParent<Renderer>();
                
                if (hitRenderer != null && hitRenderer.sharedMaterial != null)
                {
                    Color extractedColor = Color.white;
                    bool colorFound = false;

                    // 1. Get the base tint of the material
                    if (hitRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    {
                        extractedColor = hitRenderer.sharedMaterial.GetColor("_BaseColor");
                        colorFound = true;
                    }
                    else if (hitRenderer.sharedMaterial.HasProperty("_Color")) 
                    {
                        extractedColor = hitRenderer.sharedMaterial.color;
                        colorFound = true;
                    }

                    // 2. Try to get the texture color (because most objects use textures instead of solid colors)
                    Texture2D tex = null;
                    if (hitRenderer.sharedMaterial.HasProperty("_BaseMap"))
                        tex = hitRenderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                    else if (hitRenderer.sharedMaterial.HasProperty("_MainTex"))
                        tex = hitRenderer.sharedMaterial.GetTexture("_MainTex") as Texture2D;

                    if (tex != null)
                    {
                        if (tex.isReadable)
                        {
                            // Texture sampling requires UV coordinates, which are ONLY provided by MeshColliders
                            if (hit.collider is MeshCollider)
                            {
                                Color pixelColor = tex.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                                extractedColor *= pixelColor; // Combine the texture's pixel color with the base tint
                                if (hit.collider.gameObject == _lastHitObject) Debug.Log($"[ColorSampler] SUCCESS! Sampled TEXTURE Pixel: {extractedColor} from {hit.collider.gameObject.name}");
                            }
                            else
                            {
                                if (hit.collider.gameObject == _lastHitObject) Debug.LogWarning($"[ColorSampler] To sample the texture of {hit.collider.gameObject.name}, you must use a MeshCollider. Box/Sphere colliders do not provide UV coordinates.");
                            }
                        }
                        else
                        {
                            if (hit.collider.gameObject == _lastHitObject) Debug.LogWarning($"[ColorSampler] Texture on {hit.collider.gameObject.name} is not readable! Go to the texture file in your Project window, check 'Read/Write Enabled', and click Apply.");
                        }
                    }
                    else if (colorFound)
                    {
                        if (hit.collider.gameObject == _lastHitObject) Debug.Log($"[ColorSampler] SUCCESS! Sampled Base Tint: {extractedColor} from {hit.collider.gameObject.name}");
                    }

                    if (colorFound)
                    {
                        _hoveredColor = extractedColor;
                    }
                    else
                    {
                        if (hit.collider.gameObject == _lastHitObject) Debug.LogWarning($"[ColorSampler] The material on {hit.collider.gameObject.name} does not have a recognizable color property.");
                    }
                }
                else
                {
                    if (hit.collider.gameObject == _lastHitObject) Debug.LogWarning($"[ColorSampler] Hit {hit.collider.gameObject.name}, but it has no Renderer attached!");
                }
            }
            else
            {
                if (_lastHitObject != null)
                {
                    Debug.Log("[ColorSampler] Raycast is not hitting anything right now.");
                    _lastHitObject = null;
                }
            }
        }

        private void LockSampledColor()
        {
            LockedColor = _hoveredColor;
        }

        // ==========================================
        // 3. UI UPDATES
        // ==========================================
        private void UpdateUI()
        {
            if (_colorSamplingUI != null)
            {
                _colorSamplingUI.color = _hoveredColor;
            }
        }

        // ==========================================
        // 4. PAINTING LOGIC
        // ==========================================
        private void CachePlayerMaterials()
        {
            if (_playerRenderers == null || _playerRenderers.Length == 0) return;

            // Extract and cache material instances on Awake so we don't allocate memory every frame while holding click
            _playerMaterials = new Material[_playerRenderers.Length];
            
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] != null)
                {
                    // Accessing .material automatically creates a unique instance for the player
                    _playerMaterials[i] = _playerRenderers[i].material; 
                }
            }
        }

        private void ApplyPainting()
        {
            if (_playerMaterials == null) return;

            foreach (var mat in _playerMaterials)
            {
                if (mat != null)
                {
                    if (mat.HasProperty("_Color"))
                        mat.color = LockedColor;
                    else if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", LockedColor);
                }
            }
        }
    }
}
