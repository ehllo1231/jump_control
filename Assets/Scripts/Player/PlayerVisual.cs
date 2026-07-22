using UnityEngine;

/// <summary>
/// 플레이어 시각 요소와 상태별 표시를 담당합니다.
/// 캐릭터 스프라이트나 Animator를 붙여도 점프 로직은 PlayerController에 그대로 둘 수 있습니다.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(200)]
public class PlayerVisual : MonoBehaviour
{
    private const float MinimumBodySize = 0.1f;
    private const float MinimumVisualScale = 0.1f;
    private const float AlignmentTolerance = 0.000001f;
    private const float HitboxOutlineWidth = 0.018f;
    private const string HitboxOutlineName = "Hitbox Debug Outline";
    private static readonly Color HitboxOutlineColor = new Color(0.62f, 1f, 0.42f, 0.9f);

    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("Pixel Perfect")]
    [SerializeField] private bool pixelSnapVisual = true;

    [Header("State Colors")]
    [SerializeField] private bool tintByState = true;
    [SerializeField] private Color idleColor = new Color(0.2f, 0.65f, 1f);
    [SerializeField] private Color powerReadyColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color aimingColor = new Color(0.15f, 0.9f, 0.55f);
    [SerializeField] private Color jumpingColor = new Color(1f, 0.42f, 0.3f);
    [SerializeField, HideInInspector] private float configuredBodySize = 0.72f;
    [SerializeField, HideInInspector] private float configuredVisualScale = 1f;
    [SerializeField, HideInInspector] private float configuredVisualXOffset;
    [SerializeField, HideInInspector] private float configuredVisualYOffset;
    [SerializeField, HideInInspector] private bool debugHitboxVisible;

    private static Material hitboxOutlineMaterial;
    private LineRenderer hitboxOutline;
    private bool isSyncing;
    private bool facingLeft;
    private Camera pixelSnapCamera;

    public bool IsFacingLeft => facingLeft;

    private void Awake()
    {
        CacheReferences();
        SyncVisualAndCollider();

        if (Application.isPlaying)
        {
            SetState(PlayerJumpState.Idle);
        }
    }

    private void LateUpdate()
    {
        SyncVisualAndCollider();
        if (Application.isPlaying)
        {
            ApplyVisualPixelSnap();
        }
    }

    private void OnDisable()
    {
        SetHitboxOutlineActive(false);
        pixelSnapCamera = null;
    }

    public void SetState(PlayerJumpState state)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        if (!tintByState)
        {
            bodyRenderer.color = Color.white;
            return;
        }

        switch (state)
        {
            case PlayerJumpState.ChargingPower:
                bodyRenderer.color = powerReadyColor;
                break;
            case PlayerJumpState.PowerGaugeReady:
            case PlayerJumpState.Aiming:
                bodyRenderer.color = aimingColor;
                break;
            case PlayerJumpState.Jumping:
                bodyRenderer.color = jumpingColor;
                break;
            default:
                bodyRenderer.color = idleColor;
                break;
        }
    }

    public void OnJump(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) <= 0.01f)
        {
            return;
        }

        facingLeft = direction.x < 0f;
        CacheReferences();
        SyncVisualAndCollider();
    }

    public void SetBodySize(
        float size,
        float visualScale = 1f,
        float visualXOffset = 0f,
        float visualYOffset = 0f,
        bool showDebugHitbox = false)
    {
        CacheReferences();

        float safeSize = Mathf.Max(MinimumBodySize, size);
        float safeVisualScale = Mathf.Max(MinimumVisualScale, visualScale);
        configuredBodySize = safeSize;
        configuredVisualScale = safeVisualScale;
        configuredVisualXOffset = IsFinite(visualXOffset) ? visualXOffset : 0f;
        configuredVisualYOffset = IsFinite(visualYOffset) ? visualYOffset : 0f;
        debugHitboxVisible = showDebugHitbox;
        if (bodyCollider != null)
        {
            bodyCollider.offset = Vector2.zero;
            bodyCollider.size = Vector2.one * safeSize;
        }

        ApplyVisualTransform(safeSize, safeVisualScale, configuredVisualXOffset, configuredVisualYOffset);
        UpdateHitboxOutline();
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("Visual");
            visualRoot = found != null ? found : transform;
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void SyncVisualAndCollider()
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        CacheReferences();
        SyncColliderOffset();
        ApplyVisualTransform(
            GetConfiguredBodySize(),
            GetConfiguredVisualScale(),
            GetConfiguredVisualXOffset(),
            GetConfiguredVisualYOffset());
        UpdateHitboxOutline();
        isSyncing = false;
    }

    private void ApplyVisualTransform(float bodySize, float visualScale, float visualXOffset, float visualYOffset)
    {
        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        float safeBodySize = Mathf.Max(MinimumBodySize, bodySize);
        float targetVisualHeight = safeBodySize * Mathf.Max(MinimumVisualScale, visualScale);
        float spriteHeight = GetSpriteLocalHeight();
        float visualScaleFactor = targetVisualHeight / spriteHeight;
        float spriteBottom = GetSpriteLocalBottom() * visualScaleFactor;
        float bodyBottom = -safeBodySize * 0.5f;
        float visualOffsetY = bodyBottom - spriteBottom + visualYOffset;
        float mirrorAxisX = GetVisualMirrorAxisX();
        float visualOffsetX = facingLeft ? mirrorAxisX * 2f - visualXOffset : visualXOffset;
        float visualScaleX = facingLeft ? -visualScaleFactor : visualScaleFactor;
        Vector3 targetPosition = new Vector3(visualOffsetX, visualOffsetY, 0f);
        Vector3 targetScale = new Vector3(visualScaleX, visualScaleFactor, 1f);

        if (bodyRenderer != null && bodyRenderer.flipX)
        {
            bodyRenderer.flipX = false;
        }

        if ((visualRoot.localPosition - targetPosition).sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.localPosition = targetPosition;
        }

        if (visualRoot.localRotation != Quaternion.identity)
        {
            visualRoot.localRotation = Quaternion.identity;
        }

        if ((visualRoot.localScale - targetScale).sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.localScale = targetScale;
        }
    }

    private void SyncColliderOffset()
    {
        if (bodyCollider == null)
        {
            return;
        }

        bodyCollider.offset = Vector2.zero;
    }

    private void ApplyVisualPixelSnap()
    {
        if (!pixelSnapVisual || visualRoot == null || visualRoot == transform)
        {
            return;
        }

        if (!TryGetWorldUnitsPerScreenPixel(out float pixelStep))
        {
            return;
        }

        Vector3 worldPosition = visualRoot.position;
        Vector3 snappedPosition = SimpleCameraFollow.SnapToPixelGrid(worldPosition, pixelStep);
        if ((worldPosition - snappedPosition).sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.position = snappedPosition;
        }
    }

    private bool TryGetWorldUnitsPerScreenPixel(out float pixelStep)
    {
        pixelStep = 0f;
        Camera camera = GetPixelSnapCamera();
        if (camera == null || !camera.orthographic)
        {
            return false;
        }

        SimpleCameraFollow pixelPerfectCamera = camera.GetComponent<SimpleCameraFollow>();
        if (pixelPerfectCamera != null && pixelPerfectCamera.enabled)
        {
            pixelStep = pixelPerfectCamera.WorldUnitsPerScreenPixel;
        }
        else
        {
            int pixelHeight = camera.pixelHeight > 0 ? camera.pixelHeight : Screen.height;
            pixelStep = camera.orthographicSize * 2f / Mathf.Max(1, pixelHeight);
        }

        return IsFinite(pixelStep) && pixelStep > 0f;
    }

    private Camera GetPixelSnapCamera()
    {
        if (pixelSnapCamera != null && pixelSnapCamera.isActiveAndEnabled)
        {
            return pixelSnapCamera;
        }

        pixelSnapCamera = Camera.main;
        if (pixelSnapCamera == null)
        {
            pixelSnapCamera = FindFirstObjectByType<Camera>();
        }

        return pixelSnapCamera;
    }

    private float GetVisualMirrorAxisX()
    {
        return bodyCollider != null && IsFinite(bodyCollider.offset.x) ? bodyCollider.offset.x : 0f;
    }

    private float GetConfiguredBodySize()
    {
        if (configuredBodySize >= MinimumBodySize)
        {
            return configuredBodySize;
        }

        if (bodyCollider == null)
        {
            return MinimumBodySize;
        }

        return Mathf.Max(MinimumBodySize, Mathf.Max(bodyCollider.size.x, bodyCollider.size.y));
    }

    private float GetConfiguredVisualScale()
    {
        return Mathf.Max(MinimumVisualScale, configuredVisualScale);
    }

    private float GetConfiguredVisualXOffset()
    {
        return IsFinite(configuredVisualXOffset) ? configuredVisualXOffset : 0f;
    }

    private float GetConfiguredVisualYOffset()
    {
        return IsFinite(configuredVisualYOffset) ? configuredVisualYOffset : 0f;
    }

    private float GetSpriteLocalHeight()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
        {
            return 1f;
        }

        return Mathf.Max(0.0001f, bodyRenderer.sprite.bounds.size.y);
    }

    private float GetSpriteLocalBottom()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
        {
            return -0.5f;
        }

        return bodyRenderer.sprite.bounds.min.y;
    }

    private void UpdateHitboxOutline()
    {
        if (!debugHitboxVisible || bodyCollider == null || !isActiveAndEnabled)
        {
            SetHitboxOutlineActive(false);
            return;
        }

        EnsureHitboxOutline();
        if (hitboxOutline == null)
        {
            return;
        }

        hitboxOutline.gameObject.SetActive(true);
        hitboxOutline.transform.localPosition = Vector3.zero;
        hitboxOutline.transform.localRotation = Quaternion.identity;
        hitboxOutline.transform.localScale = Vector3.one;
        hitboxOutline.startWidth = HitboxOutlineWidth;
        hitboxOutline.endWidth = HitboxOutlineWidth;
        hitboxOutline.startColor = HitboxOutlineColor;
        hitboxOutline.endColor = HitboxOutlineColor;

        Vector2 offset = bodyCollider.offset;
        Vector2 size = bodyCollider.size;
        float left = offset.x - size.x * 0.5f;
        float right = offset.x + size.x * 0.5f;
        float bottom = offset.y - size.y * 0.5f;
        float top = offset.y + size.y * 0.5f;

        hitboxOutline.positionCount = 4;
        hitboxOutline.SetPosition(0, new Vector3(left, bottom, -0.02f));
        hitboxOutline.SetPosition(1, new Vector3(left, top, -0.02f));
        hitboxOutline.SetPosition(2, new Vector3(right, top, -0.02f));
        hitboxOutline.SetPosition(3, new Vector3(right, bottom, -0.02f));
    }

    private void EnsureHitboxOutline()
    {
        if (hitboxOutline != null)
        {
            return;
        }

        Transform existing = transform.Find(HitboxOutlineName);
        if (existing != null)
        {
            hitboxOutline = existing.GetComponent<LineRenderer>();
        }

        if (hitboxOutline == null)
        {
            GameObject outlineObject = new GameObject(HitboxOutlineName);
            if (!Application.isPlaying)
            {
                outlineObject.hideFlags = HideFlags.DontSave;
            }

            outlineObject.transform.SetParent(transform, false);
            hitboxOutline = outlineObject.AddComponent<LineRenderer>();
        }

        hitboxOutline.useWorldSpace = false;
        hitboxOutline.loop = true;
        hitboxOutline.textureMode = LineTextureMode.Stretch;
        hitboxOutline.alignment = LineAlignment.TransformZ;
        hitboxOutline.numCornerVertices = 0;
        hitboxOutline.numCapVertices = 0;
        hitboxOutline.sortingOrder = 100;
        hitboxOutline.sharedMaterial = GetHitboxOutlineMaterial();
    }

    private void SetHitboxOutlineActive(bool active)
    {
        if (hitboxOutline == null)
        {
            Transform existing = transform.Find(HitboxOutlineName);
            if (existing != null)
            {
                hitboxOutline = existing.GetComponent<LineRenderer>();
            }
        }

        if (hitboxOutline != null)
        {
            hitboxOutline.gameObject.SetActive(active);
        }
    }

    private static Material GetHitboxOutlineMaterial()
    {
        if (hitboxOutlineMaterial != null)
        {
            return hitboxOutlineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        hitboxOutlineMaterial = new Material(shader);
        hitboxOutlineMaterial.hideFlags = HideFlags.HideAndDontSave;
        return hitboxOutlineMaterial;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnValidate()
    {
        CacheReferences();
        SyncVisualAndCollider();
    }
}
