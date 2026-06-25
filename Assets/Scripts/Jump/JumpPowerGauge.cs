using UnityEngine;

/// <summary>
/// 플레이어 오른쪽에 표시되는 점프 파워 게이지입니다.
/// 두 번째 점프 버튼을 누르는 동안 정규화 값 0~1 사이를 왕복하며,
/// 버튼을 뗄 때 JumpTuningConfig의 최소·최대값과 반응 곡선으로 실제 점프 세기를 확정합니다.
/// </summary>
public class JumpPowerGauge : MonoBehaviour
{
    private const float DefaultCycleDuration = 1.15f;

    [Header("World Space Visual")]
    [SerializeField] private Vector2 localOffset = new Vector2(0.82f, 0.05f);
    [SerializeField] private float barWidth = 0.16f;
    [SerializeField] private float barHeight = 1.05f;
    [SerializeField] private Sprite gaugeSprite;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.85f);
    [SerializeField] private Gradient fillColorGradient = CreateDefaultFillColorGradient();

    [Header("Debug")]
    [SerializeField] private float normalizedValue;
    [SerializeField] private float currentPower;
    [SerializeField] private bool locked;

    private Transform visualRoot;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private JumpTuningConfig tuningConfig;
    private float elapsed;

    public float NormalizedValue => normalizedValue;
    public float CurrentPower => currentPower;

    public void SetTuningConfig(JumpTuningConfig config)
    {
        tuningConfig = config;
        currentPower = EvaluatePower(normalizedValue);
        UpdateVisuals();
    }

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
        currentPower = EvaluatePower(normalizedValue);

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
        normalizedValue = Mathf.PingPong(elapsed / GetChargeDuration(), 1f);
        currentPower = EvaluatePower(normalizedValue);

        UpdateVisuals();
    }

    public float LockCurrentPower()
    {
        locked = true;
        currentPower = EvaluatePower(normalizedValue);
        UpdateVisuals();
        return currentPower;
    }

    public float ShowLockedValue(float gaugeValue)
    {
        EnsureVisuals();
        locked = true;
        normalizedValue = Mathf.Clamp01(gaugeValue);
        currentPower = EvaluatePower(normalizedValue);
        SetVisible(true);
        UpdateVisuals();
        return currentPower;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private float GetChargeDuration()
    {
        return tuningConfig != null ? tuningConfig.GaugeChargeDuration : DefaultCycleDuration;
    }

    private float EvaluatePower(float gaugeValue)
    {
        return tuningConfig != null
            ? tuningConfig.EvaluateJumpPower(gaugeValue)
            : Mathf.Lerp(7f, 16f, Mathf.Clamp01(gaugeValue));
    }

    private void EnsureVisuals()
    {
        EnsureFillColorGradient();

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
        fillRenderer.color = EvaluateFillColor(normalizedValue);
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

    private Color EvaluateFillColor(float gaugeValue)
    {
        EnsureFillColorGradient();
        return fillColorGradient.Evaluate(Mathf.Clamp01(gaugeValue));
    }

    private void EnsureFillColorGradient()
    {
        if (fillColorGradient == null || fillColorGradient.colorKeys.Length == 0)
        {
            fillColorGradient = CreateDefaultFillColorGradient();
        }
    }

    private static Gradient CreateDefaultFillColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.1f, 0.55f, 1f), 0f),
                new GradientColorKey(new Color(0.02f, 0.85f, 1f), 0.35f),
                new GradientColorKey(new Color(1f, 0.88f, 0.16f), 0.68f),
                new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.86f),
                new GradientColorKey(new Color(1f, 0.08f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.95f, 0.7f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }

    private void OnValidate()
    {
        barWidth = Mathf.Max(0.02f, barWidth);
        barHeight = Mathf.Max(0.1f, barHeight);
        normalizedValue = Mathf.Clamp01(normalizedValue);
        EnsureFillColorGradient();
        currentPower = EvaluatePower(normalizedValue);
    }
}
