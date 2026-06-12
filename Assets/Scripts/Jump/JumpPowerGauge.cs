using UnityEngine;

/// <summary>
/// 플레이어 오른쪽에 표시되는 점프 파워 게이지입니다.
/// 게이지 값은 minPower와 maxPower 사이를 왕복하며, 두 번째 점프 버튼 입력 시 LockCurrentPower로 확정됩니다.
/// </summary>
public class JumpPowerGauge : MonoBehaviour
{
    [Header("Power")]
    [SerializeField] private float minPower = 7f;
    [SerializeField] private float maxPower = 16f;
    [SerializeField] private float cycleDuration = 1.15f;

    [Header("World Space Visual")]
    [SerializeField] private Vector2 localOffset = new Vector2(0.82f, 0.05f);
    [SerializeField] private float barWidth = 0.16f;
    [SerializeField] private float barHeight = 1.05f;
    [SerializeField] private Sprite gaugeSprite;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.85f);
    [SerializeField] private Color fillColor = new Color(0.1f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color lockedFillColor = new Color(1f, 0.82f, 0.15f, 1f);

    [Header("Debug")]
    [SerializeField] private float normalizedValue;
    [SerializeField] private float currentPower;
    [SerializeField] private bool locked;

    private Transform visualRoot;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private float elapsed;

    public float NormalizedValue => normalizedValue;
    public float CurrentPower => currentPower;

    private void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    public void BeginGauge()
    {
        EnsureVisuals();

        elapsed = 0f;
        locked = false;
        normalizedValue = 0f;
        currentPower = minPower;

        SetVisible(true);
        UpdateVisuals();
    }

    public void TickGauge(float deltaTime)
    {
        if (locked)
        {
            return;
        }

        elapsed += deltaTime;
        normalizedValue = Mathf.PingPong(elapsed / Mathf.Max(0.01f, cycleDuration), 1f);
        currentPower = Mathf.Lerp(minPower, maxPower, normalizedValue);

        UpdateVisuals();
    }

    public float LockCurrentPower()
    {
        locked = true;
        currentPower = Mathf.Lerp(minPower, maxPower, normalizedValue);
        UpdateVisuals();
        return currentPower;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void EnsureVisuals()
    {
        if (gaugeSprite == null)
        {
            gaugeSprite = CreateRuntimeSquareSprite();
        }

        if (visualRoot == null)
        {
            Transform foundRoot = transform.Find("JumpPowerGaugeVisual");
            if (foundRoot == null)
            {
                GameObject rootObject = new GameObject("JumpPowerGaugeVisual");
                foundRoot = rootObject.transform;
                foundRoot.SetParent(transform, false);
            }

            visualRoot = foundRoot;
        }

        backgroundRenderer = GetOrCreateRenderer("GaugeBackground", 20);
        fillRenderer = GetOrCreateRenderer("GaugeFill", 21);
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

        renderer.sprite = gaugeSprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null || backgroundRenderer == null || fillRenderer == null)
        {
            return;
        }

        float fillHeight = Mathf.Max(0.01f, barHeight * Mathf.Clamp01(normalizedValue));

        visualRoot.localPosition = localOffset;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        backgroundRenderer.transform.localPosition = Vector3.zero;
        backgroundRenderer.transform.localRotation = Quaternion.identity;
        backgroundRenderer.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        backgroundRenderer.color = backgroundColor;

        fillRenderer.transform.localPosition = new Vector3(0f, -barHeight * 0.5f + fillHeight * 0.5f, -0.01f);
        fillRenderer.transform.localRotation = Quaternion.identity;
        fillRenderer.transform.localScale = new Vector3(barWidth * 0.68f, fillHeight, 1f);
        fillRenderer.color = locked ? lockedFillColor : fillColor;
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
        minPower = Mathf.Max(0f, minPower);
        maxPower = Mathf.Max(minPower, maxPower);
        cycleDuration = Mathf.Max(0.05f, cycleDuration);
        barWidth = Mathf.Max(0.02f, barWidth);
        barHeight = Mathf.Max(0.1f, barHeight);
        currentPower = Mathf.Lerp(minPower, maxPower, normalizedValue);
    }
}
