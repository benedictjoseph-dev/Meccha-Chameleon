using UnityEngine;
using Unity.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ColorSystem
{
    /// <summary>
    /// Extension system to allow UV-based texture painting on a SkinnedMeshRenderer.
    /// Operates with ZERO per-frame allocations by directly modifying the NativeArray texture data.
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    public class PlayerTexturePainter : MonoBehaviour
    {
        [Header("System Links")]
        [Tooltip("Reference to the sampler to get the active Locked Color")]
        [SerializeField] private ColorSamplerController _colorSampler;
        [SerializeField] private Camera _mainCamera;
        
        [Header("Mesh Target")]
        [Tooltip("The SkinnedMeshRenderer to paint on.")]
        [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
        
        [Header("Brush Settings")]
        [SerializeField, Range(1, 100)] private int _brushRadius = 10;
        [Tooltip("0 = Hard Edge, 1 = Soft Airbrush")]
        [SerializeField, Range(0f, 1f)] private float _brushHardness = 0.5f;
        
        [Header("Dynamic Brush Control")]
        [SerializeField] private int _minBrushRadius = 1;
        [SerializeField] private int _maxBrushRadius = 100;
        [SerializeField] private int _brushStepSize = 5;
        [Tooltip("Assign a 3D Sphere or Quad here to act as a visual cursor on the mesh.")]
        [SerializeField] private Transform _brushIndicator;

        // Components
        private MeshCollider _meshCollider;
        private Mesh _bakedMesh;
        
        // Texture Data
        private Texture2D _runtimeTexture;
        private NativeArray<Color32> _textureData;
        private int _texWidth;
        private int _texHeight;
        
        // State
        private bool _textureReady = false;
        private Vector2 _lastPaintedUV = -Vector2.one;

        private void Awake()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            
            _meshCollider = GetComponent<MeshCollider>();
            
            // We reuse a SINGLE mesh instance to prevent allocating a new Mesh object every frame!
            _bakedMesh = new Mesh();
            _bakedMesh.name = "Baked_Painting_Collider";
            
            InitializeTexture();
        }

        private void InitializeTexture()
        {
            if (_skinnedMeshRenderer == null) return;
            
            // Accessing .material creates a safe unique instance for this player
            Material mat = _skinnedMeshRenderer.material; 
            Texture2D sourceTex = null;
            
            if (mat.HasProperty("_BaseMap")) sourceTex = mat.GetTexture("_BaseMap") as Texture2D;
            else if (mat.HasProperty("_MainTex")) sourceTex = mat.GetTexture("_MainTex") as Texture2D;

            if (sourceTex == null)
            {
                Debug.LogWarning("[TexturePainter] No texture found on SkinnedMeshRenderer! Generating a blank 1024x1024 white texture to paint on.");
                _texWidth = 1024;
                _texHeight = 1024;
                _runtimeTexture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false);
                
                // Fill with solid white
                Color32[] whitePixels = new Color32[_texWidth * _texHeight];
                for (int i = 0; i < whitePixels.Length; i++) whitePixels[i] = new Color32(255, 255, 255, 255);
                _runtimeTexture.SetPixels32(whitePixels);
                _runtimeTexture.Apply();

                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _runtimeTexture);
                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _runtimeTexture);
                else 
                {
                    Debug.LogError("[TexturePainter] The material on your player doesn't support textures! Please use a Standard or URP Lit material.");
                    return;
                }
            }
            else
            {
                if (!sourceTex.isReadable)
                {
                    Debug.LogError("[TexturePainter] Source texture is NOT Read/Write enabled! Find the texture in your project, check 'Read/Write Enabled', and click Apply.");
                    return;
                }

                _texWidth = sourceTex.width;
                _texHeight = sourceTex.height;

                // Create RGBA32 format to guarantee we can safely edit the NativeArray directly
                _runtimeTexture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false);
                
                // Copy pixels. GetPixels() handles format conversions (e.g., if source is DXT compressed)
                Color[] pixels = sourceTex.GetPixels();
                _runtimeTexture.SetPixels(pixels);
                _runtimeTexture.Apply();

                // Replace the material's texture with our new writable clone
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _runtimeTexture);
                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _runtimeTexture);
            }

            // Fetch a direct pointer to the texture's memory space. 
            // Modifying this NativeArray edits the texture with ZERO garbage collection allocations!
            _textureData = _runtimeTexture.GetPixelData<Color32>(0);
            _textureReady = true;
        }

        private void Update()
        {
            if (!_textureReady || _skinnedMeshRenderer == null) return;

            // 1. UPDATE COLLIDER TO MATCH ANIMATION (MANDATORY FOR SKINNED MESH UV RAYCASTS)
            // By passing _bakedMesh, we overwrite the existing data instead of creating a new mesh.
            _skinnedMeshRenderer.BakeMesh(_bakedMesh);
            _meshCollider.sharedMesh = _bakedMesh;

            // 2. CHECK INPUT
            bool isPainting = false;
            bool increaseBrush = false;
            bool decreaseBrush = false;
            Vector2 mousePos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                isPainting = Mouse.current.leftButton.isPressed;
                mousePos = Mouse.current.position.ReadValue();
            }
            if (Keyboard.current != null)
            {
                increaseBrush = Keyboard.current.numpadPlusKey.wasPressedThisFrame;
                decreaseBrush = Keyboard.current.numpadMinusKey.wasPressedThisFrame;
            }
#else
            isPainting = Input.GetMouseButton(0);
            mousePos = Input.mousePosition;
            increaseBrush = Input.GetKeyDown(KeyCode.KeypadPlus);
            decreaseBrush = Input.GetKeyDown(KeyCode.KeypadMinus);
#endif

            // Handle Brush Size Changes
            if (increaseBrush)
            {
                _brushRadius = Mathf.Clamp(_brushRadius + _brushStepSize, _minBrushRadius, _maxBrushRadius);
                Debug.Log($"[TexturePainter] Brush Size Increased to: {_brushRadius}");
            }
            if (decreaseBrush)
            {
                _brushRadius = Mathf.Clamp(_brushRadius - _brushStepSize, _minBrushRadius, _maxBrushRadius);
                Debug.Log($"[TexturePainter] Brush Size Decreased to: {_brushRadius}");
            }

            // 3. RAYCAST & INDICATOR UPDATE
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            bool hitSuccess = _meshCollider.Raycast(ray, out RaycastHit hit, 100f);

            // Do not paint or show indicator if the user is in "Sampling" mode (e.g., holding Q)
            if (_colorSampler != null && _colorSampler.IsSamplingMode)
            {
                _lastPaintedUV = -Vector2.one; // Break stroke
                UpdateBrushIndicator(null);
                return; 
            }

            if (hitSuccess)
            {
                UpdateBrushIndicator(hit);
                
                if (isPainting)
                {
                    PerformPaintingRaycast(hit);
                }
                else
                {
                    _lastPaintedUV = -Vector2.one; // Break stroke when mouse released
                }
            }
            else
            {
                UpdateBrushIndicator(null);
                _lastPaintedUV = -Vector2.one;
            }
        }

        private void UpdateBrushIndicator(RaycastHit? hit)
        {
            if (_brushIndicator == null) return;

            if (hit.HasValue)
            {
                if (!_brushIndicator.gameObject.activeSelf) _brushIndicator.gameObject.SetActive(true);
                
                _brushIndicator.position = hit.Value.point;
                // Align indicator to surface normal so it lies flat on the mesh
                _brushIndicator.rotation = Quaternion.LookRotation(hit.Value.normal);
                
                // Scale the indicator roughly based on brush size in pixels relative to texture size.
                // Note: True world scale depends heavily on the model's UV mapping density!
                float scale = (_brushRadius / (float)Mathf.Max(_texWidth, _texHeight)) * 3f; 
                _brushIndicator.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                if (_brushIndicator.gameObject.activeSelf) _brushIndicator.gameObject.SetActive(false);
            }
        }

        private void PerformPaintingRaycast(RaycastHit hit)
        {
            Vector2 uv = hit.textureCoord;
            Color32 paintColor = _colorSampler != null ? (Color32)_colorSampler.LockedColor : new Color32(255, 0, 0, 255);

            if (_lastPaintedUV != -Vector2.one)
            {
                float dist = Vector2.Distance(_lastPaintedUV, uv);
                
                // Prevent giant lines if the raycast jumped across a UV seam
                if (dist > 0.2f) 
                {
                    PaintAtUV(uv, paintColor);
                }
                else
                {
                    // Interpolate between the last frame's hit and this frame's hit
                    // This guarantees no gaps if the player moves the mouse extremely fast
                    float pixelDist = dist * Mathf.Max(_texWidth, _texHeight);
                    int steps = Mathf.CeilToInt(pixelDist / (_brushRadius * 0.5f));
                    steps = Mathf.Clamp(steps, 1, 100); // Safety cap

                    for (int i = 1; i <= steps; i++)
                    {
                        Vector2 interpolatedUV = Vector2.Lerp(_lastPaintedUV, uv, (float)i / steps);
                        PaintAtUV(interpolatedUV, paintColor);
                    }
                }
            }
            else
            {
                PaintAtUV(uv, paintColor);
            }

            _lastPaintedUV = uv;
            
            // Upload changes to the GPU. 
            // Because we modified NativeArray, this is the only expensive call.
            _runtimeTexture.Apply(); 
        }

        private void PaintAtUV(Vector2 uv, Color32 color)
        {
            // Convert UV (0.0 to 1.0) into pixel coordinates
            int centerX = Mathf.Clamp(Mathf.FloorToInt(uv.x * _texWidth), 0, _texWidth - 1);
            int centerY = Mathf.Clamp(Mathf.FloorToInt(uv.y * _texHeight), 0, _texHeight - 1);

            int radiusSqr = _brushRadius * _brushRadius;

            // Bounds checking to prevent going outside the texture limits
            int startX = Mathf.Max(0, centerX - _brushRadius);
            int endX = Mathf.Min(_texWidth - 1, centerX + _brushRadius);
            int startY = Mathf.Max(0, centerY - _brushRadius);
            int endY = Mathf.Min(_texHeight - 1, centerY + _brushRadius);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distSqr = dx * dx + dy * dy;

                    if (distSqr <= radiusSqr)
                    {
                        float dist = Mathf.Sqrt(distSqr);
                        float alpha = 1f;

                        // Calculate soft brush falloff
                        if (_brushHardness < 1f)
                        {
                            float softRadius = _brushRadius * _brushHardness;
                            if (dist > softRadius)
                            {
                                alpha = 1f - ((dist - softRadius) / (_brushRadius - softRadius));
                            }
                        }

                        int index = y * _texWidth + x;
                        
                        if (alpha >= 0.99f)
                        {
                            // Hard overwrite (fastest)
                            _textureData[index] = color;
                        }
                        else
                        {
                            // Soft Blend (Alpha lerping)
                            Color32 existingColor = _textureData[index];
                            byte r = (byte)Mathf.Lerp(existingColor.r, color.r, alpha);
                            byte g = (byte)Mathf.Lerp(existingColor.g, color.g, alpha);
                            byte b = (byte)Mathf.Lerp(existingColor.b, color.b, alpha);
                            byte a = (byte)Mathf.Lerp(existingColor.a, color.a, alpha);
                            _textureData[index] = new Color32(r, g, b, a);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up dynamically created assets to prevent memory leaks when changing scenes
            if (_bakedMesh != null) Destroy(_bakedMesh);
            if (_runtimeTexture != null) Destroy(_runtimeTexture);
        }
    }
}
