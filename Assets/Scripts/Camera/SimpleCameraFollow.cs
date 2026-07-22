using UnityEngine;

/// <summary>
/// LateUpdate에서 플레이어를 부드럽게 따라가는 간단한 2D 카메라입니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SimpleCameraFollow : MonoBehaviour
{
    private const float PositionTolerance = 0.0000000001f;
    public const int DefaultAssetsPixelsPerUnit = 16;
    public const int DefaultReferenceWidth = 384;
    public const int DefaultReferenceHeight = 216;
    public const float DefaultOrthographicSize =
        DefaultReferenceHeight / (2f * DefaultAssetsPixelsPerUnit);

    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.6f, -10f);
    [SerializeField] private float smoothTime = 0.18f;
    [SerializeField] private float minY = -0.2f;

    [Header("Pixel Perfect")]
    [SerializeField] private bool pixelPerfect = true;
    [SerializeField, Min(1)] private int assetsPixelsPerUnit = DefaultAssetsPixelsPerUnit;
    [SerializeField] private Vector2Int referenceResolution =
        new Vector2Int(DefaultReferenceWidth, DefaultReferenceHeight);
    [SerializeField] private bool snapCameraPosition = true;

    private Vector3 velocity;
    private Camera attachedCamera;

    public int CurrentPixelScale { get; private set; } = 1;

    public float WorldUnitsPerScreenPixel =>
        1f / (Mathf.Max(1, assetsPixelsPerUnit) * Mathf.Max(1, CurrentPixelScale));

    private void Awake()
    {
        CacheCamera();
        ApplyPixelPerfectProjection(Screen.width, Screen.height);
    }

    private void LateUpdate()
    {
        Vector3 cameraPosition = transform.position;
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            desiredPosition.y = Mathf.Max(minY, desiredPosition.y);

            cameraPosition = Vector3.SmoothDamp(
                cameraPosition,
                desiredPosition,
                ref velocity,
                smoothTime);
        }

        ApplyPixelPerfectProjection(Screen.width, Screen.height);
        if (pixelPerfect && snapCameraPosition)
        {
            cameraPosition = SnapToPixelGrid(cameraPosition, WorldUnitsPerScreenPixel);
        }

        if ((transform.position - cameraPosition).sqrMagnitude > PositionTolerance)
        {
            transform.position = cameraPosition;
        }
    }

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0.01f, smoothTime);
        assetsPixelsPerUnit = Mathf.Max(1, assetsPixelsPerUnit);
        referenceResolution.x = Mathf.Max(1, referenceResolution.x);
        referenceResolution.y = Mathf.Max(1, referenceResolution.y);
    }

    public static int CalculatePixelScale(
        int screenWidth,
        int screenHeight,
        int referenceWidth,
        int referenceHeight)
    {
        int safeScreenWidth = Mathf.Max(1, screenWidth);
        int safeScreenHeight = Mathf.Max(1, screenHeight);
        int safeReferenceWidth = Mathf.Max(1, referenceWidth);
        int safeReferenceHeight = Mathf.Max(1, referenceHeight);
        int widthScale = safeScreenWidth / safeReferenceWidth;
        int heightScale = safeScreenHeight / safeReferenceHeight;

        return Mathf.Max(1, Mathf.Min(widthScale, heightScale));
    }

    public static float CalculateOrthographicSize(
        int screenHeight,
        int pixelsPerUnit,
        int pixelScale)
    {
        int safeScreenHeight = Mathf.Max(1, screenHeight);
        int safePixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
        int safePixelScale = Mathf.Max(1, pixelScale);

        return safeScreenHeight / (2f * safePixelsPerUnit * safePixelScale);
    }

    public static Vector3 SnapToPixelGrid(Vector3 position, float worldUnitsPerScreenPixel)
    {
        float safeStep = Mathf.Max(Mathf.Epsilon, worldUnitsPerScreenPixel);
        position.x = Mathf.Round(position.x / safeStep) * safeStep;
        position.y = Mathf.Round(position.y / safeStep) * safeStep;
        return position;
    }

    private void ApplyPixelPerfectProjection(int screenWidth, int screenHeight)
    {
        if (!pixelPerfect)
        {
            return;
        }

        CacheCamera();
        if (attachedCamera == null)
        {
            return;
        }

        int pixelScale = CalculatePixelScale(
            screenWidth,
            screenHeight,
            referenceResolution.x,
            referenceResolution.y);
        float orthographicSize = CalculateOrthographicSize(
            screenHeight,
            assetsPixelsPerUnit,
            pixelScale);

        if (CurrentPixelScale != pixelScale)
        {
            CurrentPixelScale = pixelScale;
        }

        if (!attachedCamera.orthographic)
        {
            attachedCamera.orthographic = true;
        }

        if (!Mathf.Approximately(attachedCamera.orthographicSize, orthographicSize))
        {
            attachedCamera.orthographicSize = orthographicSize;
        }
    }

    private void CacheCamera()
    {
        if (attachedCamera == null)
        {
            attachedCamera = GetComponent<Camera>();
        }
    }
}
