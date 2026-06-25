using UnityEngine;

/// <summary>
/// 플레이어 위에 표시되는 각도 화살표입니다.
/// 0도는 위쪽, 음수는 왼쪽, 양수는 오른쪽 점프 방향입니다.
/// </summary>
public class JumpAngleAim : MonoBehaviour
{
    private const float DefaultMinAngle = -62f;
    private const float DefaultMaxAngle = 62f;
    private const float DefaultSweepSpeed = 145f;
    private const float DefaultStartNormalized = 0.5f;

    [Header("World Space Visual")]
    [SerializeField] private Vector2 localOffset = new Vector2(0f, 0.86f);
    [SerializeField] private float arrowLength = 0.86f;
    [SerializeField] private float shaftWidth = 0.07f;
    [SerializeField] private float headLength = 0.28f;
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Color arrowColor = new Color(0.2f, 1f, 0.55f, 0.95f);

    [Header("Debug")]
    [SerializeField] private float currentAngle;

    private Transform visualRoot;
    private SpriteRenderer shaftRenderer;
    private SpriteRenderer headLeftRenderer;
    private SpriteRenderer headRightRenderer;
    private JumpTuningConfig tuningConfig;
    private float normalizedPosition;
    private int sweepDirection = 1;

    public float CurrentAngle => currentAngle;

    public Vector2 CurrentDirection
    {
        get
        {
            float radians = currentAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
        }
    }

    public void SetTuningConfig(JumpTuningConfig config)
    {
        tuningConfig = config;
        UpdateCurrentAngle();
        UpdateVisuals();
    }

    private void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    public void BeginAim()
    {
        EnsureVisuals();

        normalizedPosition = GetStartNormalizedPosition();
        sweepDirection = normalizedPosition >= 1f ? -1 : 1;
        UpdateCurrentAngle();

        SetVisible(true);
        UpdateVisuals();
    }

    public void TickAim(float deltaTime)
    {
        float minAngle = GetMinAngle();
        float maxAngle = GetMaxAngle();
        float angleRange = Mathf.Max(1f, maxAngle - minAngle);
        normalizedPosition += sweepDirection * (GetSweepSpeed() / angleRange) * deltaTime;

        if (normalizedPosition >= 1f)
        {
            normalizedPosition = 1f;
            sweepDirection = -1;
        }
        else if (normalizedPosition <= 0f)
        {
            normalizedPosition = 0f;
            sweepDirection = 1;
        }

        UpdateCurrentAngle();
        UpdateVisuals();
    }

    public void ShowAngle(float angle)
    {
        EnsureVisuals();
        currentAngle = angle;
        SetVisible(true);
        UpdateVisuals();
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void UpdateCurrentAngle()
    {
        currentAngle = Mathf.Lerp(GetMinAngle(), GetMaxAngle(), normalizedPosition);
    }

    private float GetMinAngle()
    {
        return tuningConfig != null ? tuningConfig.MinDirectionAngle : DefaultMinAngle;
    }

    private float GetMaxAngle()
    {
        return tuningConfig != null ? tuningConfig.MaxDirectionAngle : DefaultMaxAngle;
    }

    private float GetSweepSpeed()
    {
        return tuningConfig != null ? tuningConfig.DirectionSweepSpeed : DefaultSweepSpeed;
    }

    private float GetStartNormalizedPosition()
    {
        return tuningConfig != null ? tuningConfig.DirectionStartNormalized : DefaultStartNormalized;
    }

    private void EnsureVisuals()
    {
        if (arrowSprite == null)
        {
            arrowSprite = CreateRuntimeSquareSprite();
        }

        if (visualRoot == null)
        {
            Transform foundRoot = transform.Find("JumpAngleAimVisual");
            if (foundRoot == null)
            {
                GameObject rootObject = new GameObject("JumpAngleAimVisual");
                foundRoot = rootObject.transform;
                foundRoot.SetParent(transform, false);
            }

            visualRoot = foundRoot;
        }

        shaftRenderer = GetOrCreateRenderer("ArrowShaft", 30);
        headLeftRenderer = GetOrCreateRenderer("ArrowHeadLeft", 31);
        headRightRenderer = GetOrCreateRenderer("ArrowHeadRight", 31);
    }

    private SpriteRenderer GetOrCreateRenderer(string childName, int sortingOrder)
    {
        Transform child = visualRoot.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(visualRoot, false);
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = arrowSprite;
        renderer.sortingOrder = sortingOrder;
        renderer.color = arrowColor;
        return renderer;
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null || shaftRenderer == null || headLeftRenderer == null || headRightRenderer == null)
        {
            return;
        }

        visualRoot.localPosition = localOffset;
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        visualRoot.localScale = Vector3.one;

        shaftRenderer.color = arrowColor;
        shaftRenderer.transform.localPosition = new Vector3(0f, arrowLength * 0.5f, 0f);
        shaftRenderer.transform.localRotation = Quaternion.identity;
        shaftRenderer.transform.localScale = new Vector3(shaftWidth, arrowLength, 1f);

        headLeftRenderer.color = arrowColor;
        headLeftRenderer.transform.localPosition = new Vector3(-headLength * 0.22f, arrowLength - headLength * 0.18f, -0.01f);
        headLeftRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        headLeftRenderer.transform.localScale = new Vector3(shaftWidth, headLength, 1f);

        headRightRenderer.color = arrowColor;
        headRightRenderer.transform.localPosition = new Vector3(headLength * 0.22f, arrowLength - headLength * 0.18f, -0.01f);
        headRightRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        headRightRenderer.transform.localScale = new Vector3(shaftWidth, headLength, 1f);
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(visible);
        }
    }

    private static Sprite CreateRuntimeSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void OnValidate()
    {
        arrowLength = Mathf.Max(0.1f, arrowLength);
        shaftWidth = Mathf.Max(0.01f, shaftWidth);
        headLength = Mathf.Max(0.05f, headLength);
        UpdateCurrentAngle();
    }
}
