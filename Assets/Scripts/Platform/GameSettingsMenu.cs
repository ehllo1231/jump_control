using System.Collections.Generic;
using UnityEngine;

public sealed class GameSettingsMenu : MonoBehaviour
{
    private const float ReferenceWidth = 1280f;
    private const float ReferenceHeight = 720f;
    private const float ScreenPadding = 16f;
    private const float SettingsButtonSize = 48f;
    private const float PanelWidth = 368f;
    private const float PanelHeight = 228f;
    private const float PanelPadding = 24f;
    private const float HeaderHeight = 50f;
    private const float SettingRowHeight = 68f;
    private const float ToggleWidth = 84f;
    private const float ToggleHeight = 40f;
    private const float MinimumUiScale = 0.75f;
    private const float MaximumUiScale = 2.25f;

    private static readonly Color PanelColor = new Color(0.065f, 0.075f, 0.085f, 0.97f);
    private static readonly Color PrimaryTextColor = new Color(0.96f, 0.97f, 0.98f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.74f, 0.78f, 0.82f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.62f, 0.40f, 1f);

    private static GameSettingsMenu instance;
    private static bool menuOpen;
    private static bool suppressJumpUntilRelease;

    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();

    private Texture2D gearTexture;
    private GUIStyle panelStyle;
    private GUIStyle shadowStyle;
    private GUIStyle settingsButtonStyle;
    private GUIStyle settingsButtonOpenStyle;
    private GUIStyle closeButtonStyle;
    private GUIStyle toggleHitStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private GUIStyle toggleKnobStyle;
    private GUIStyle titleStyle;
    private GUIStyle settingLabelStyle;
    private GUIStyle toggleTextStyle;
    private float styleScale = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        menuOpen = false;
        suppressJumpUntilRelease = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameSettingsMenu existingMenu = Object.FindFirstObjectByType<GameSettingsMenu>();
        if (existingMenu != null)
        {
            instance = existingMenu;
            return;
        }

        GameObject menuObject = new GameObject("Game Settings Menu");
        instance = menuObject.AddComponent<GameSettingsMenu>();
        DontDestroyOnLoad(menuObject);
    }

    public static bool BlocksJumpInput(Vector2 screenPosition, TouchPhase pointerPhase)
    {
        if (menuOpen || suppressJumpUntilRelease)
        {
            return true;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        if (pointerPhase != TouchPhase.Began
            || !CalculateLayout().SettingsButtonRect.Contains(guiPosition))
        {
            return false;
        }

        suppressJumpUntilRelease = true;
        return true;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (menuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetMenuOpen(false);
        }

        if (suppressJumpUntilRelease && Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            suppressJumpUntilRelease = false;
        }
    }

    private void OnGUI()
    {
        GUI.depth = -1000;

        LayoutInfo layout = CalculateLayout();
        EnsureStyles(layout.Scale);

        if (menuOpen)
        {
            DrawBackdrop();
            HandleBackdropClick(layout);
            DrawSettingsPanel(layout);
        }

        DrawSettingsButton(layout);
    }

    private void DrawBackdrop()
    {
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.46f));
    }

    private void HandleBackdropClick(LayoutInfo layout)
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown
            || layout.PanelRect.Contains(currentEvent.mousePosition)
            || layout.SettingsButtonRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        SetMenuOpen(false);
        currentEvent.Use();
    }

    private void DrawSettingsButton(LayoutInfo layout)
    {
        GUIStyle buttonStyle = menuOpen ? settingsButtonOpenStyle : settingsButtonStyle;
        if (GUI.Button(layout.SettingsButtonRect, new GUIContent(string.Empty, "Settings"), buttonStyle))
        {
            SetMenuOpen(!menuOpen);
        }

        float iconInset = 12f * layout.Scale;
        Rect iconRect = Inset(layout.SettingsButtonRect, iconInset);
        Color previousColor = GUI.color;
        GUI.color = PrimaryTextColor;
        GUI.DrawTexture(iconRect, gearTexture, ScaleMode.ScaleToFit, true);
        GUI.color = previousColor;
    }

    private void DrawSettingsPanel(LayoutInfo layout)
    {
        float scale = layout.Scale;
        Rect shadowRect = layout.PanelRect;
        shadowRect.y += 4f * scale;
        GUI.Box(shadowRect, GUIContent.none, shadowStyle);
        GUI.Box(layout.PanelRect, GUIContent.none, panelStyle);

        float contentLeft = layout.PanelRect.x + PanelPadding * scale;
        float contentRight = layout.PanelRect.xMax - PanelPadding * scale;
        float headerTop = layout.PanelRect.y + 12f * scale;
        Rect headerRect = new Rect(
            contentLeft,
            headerTop,
            contentRight - contentLeft,
            HeaderHeight * scale);
        GUI.Label(headerRect, "SETTINGS", titleStyle);

        float closeSize = 38f * scale;
        Rect closeRect = new Rect(
            contentRight - closeSize,
            headerTop + (HeaderHeight * scale - closeSize) * 0.5f,
            closeSize,
            closeSize);
        if (GUI.Button(closeRect, new GUIContent(string.Empty, "Close"), closeButtonStyle))
        {
            SetMenuOpen(false);
        }

        DrawCloseIcon(closeRect, scale);

        float dividerY = headerRect.yMax + 2f * scale;
        DrawSolidRect(
            new Rect(contentLeft, dividerY, contentRight - contentLeft, Mathf.Max(1f, scale)),
            new Color(1f, 1f, 1f, 0.11f));

        float firstRowY = dividerY + 6f * scale;
        Rect vibrationRow = new Rect(
            contentLeft,
            firstRowY,
            contentRight - contentLeft,
            SettingRowHeight * scale);
        bool hapticsEnabled = GameHaptics.Enabled;
        bool nextHapticsEnabled = DrawSettingToggle(vibrationRow, "VIBRATION", hapticsEnabled, scale);
        if (nextHapticsEnabled != hapticsEnabled)
        {
            GameHaptics.Enabled = nextHapticsEnabled;
        }

        float rowDividerY = vibrationRow.yMax;
        DrawSolidRect(
            new Rect(contentLeft, rowDividerY, contentRight - contentLeft, Mathf.Max(1f, scale)),
            new Color(1f, 1f, 1f, 0.07f));

        Rect musicRow = new Rect(
            contentLeft,
            rowDividerY,
            contentRight - contentLeft,
            SettingRowHeight * scale);
        bool musicEnabled = GameMusic.Enabled;
        bool nextMusicEnabled = DrawSettingToggle(musicRow, "MUSIC", musicEnabled, scale);
        if (nextMusicEnabled != musicEnabled)
        {
            GameMusic.Enabled = nextMusicEnabled;
        }
    }

    private bool DrawSettingToggle(Rect rowRect, string label, bool enabled, float scale)
    {
        float toggleWidth = ToggleWidth * scale;
        float toggleHeight = ToggleHeight * scale;
        Rect labelRect = new Rect(
            rowRect.x,
            rowRect.y,
            Mathf.Max(0f, rowRect.width - toggleWidth - 16f * scale),
            rowRect.height);
        Rect toggleRect = new Rect(
            rowRect.xMax - toggleWidth,
            rowRect.y + (rowRect.height - toggleHeight) * 0.5f,
            toggleWidth,
            toggleHeight);

        GUI.Label(labelRect, label, settingLabelStyle);
        bool nextEnabled = GUI.Toggle(toggleRect, enabled, GUIContent.none, toggleHitStyle);
        DrawToggleVisual(toggleRect, nextEnabled, scale);
        return nextEnabled;
    }

    private void DrawToggleVisual(Rect toggleRect, bool enabled, float scale)
    {
        GUI.Box(toggleRect, GUIContent.none, enabled ? toggleOnStyle : toggleOffStyle);

        float knobMargin = 4f * scale;
        float knobSize = toggleRect.height - knobMargin * 2f;
        float knobX = enabled
            ? toggleRect.xMax - knobMargin - knobSize
            : toggleRect.x + knobMargin;
        Rect knobRect = new Rect(knobX, toggleRect.y + knobMargin, knobSize, knobSize);
        GUI.Box(knobRect, GUIContent.none, toggleKnobStyle);

        Rect statusRect;
        if (enabled)
        {
            statusRect = new Rect(
                toggleRect.x + 6f * scale,
                toggleRect.y,
                toggleRect.width - knobSize - 12f * scale,
                toggleRect.height);
        }
        else
        {
            statusRect = new Rect(
                toggleRect.x + knobSize + 6f * scale,
                toggleRect.y,
                toggleRect.width - knobSize - 12f * scale,
                toggleRect.height);
        }

        GUI.Label(statusRect, enabled ? "ON" : "OFF", toggleTextStyle);
    }

    private void DrawCloseIcon(Rect closeRect, float scale)
    {
        Vector2 center = closeRect.center;
        float lineLength = 15f * scale;
        float lineThickness = Mathf.Max(1.5f, 2f * scale);
        DrawRotatedLine(center, lineLength, lineThickness, 45f, SecondaryTextColor);
        DrawRotatedLine(center, lineLength, lineThickness, -45f, SecondaryTextColor);
    }

    private static void DrawRotatedLine(
        Vector2 center,
        float length,
        float thickness,
        float angle,
        Color color)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        GUIUtility.RotateAroundPivot(angle, center);
        GUI.color = color;
        GUI.DrawTexture(
            new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness),
            Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void SetMenuOpen(bool shouldOpen)
    {
        if (menuOpen == shouldOpen)
        {
            return;
        }

        menuOpen = shouldOpen;
        suppressJumpUntilRelease = true;
    }

    private void EnsureStyles(float scale)
    {
        if (gearTexture == null)
        {
            CreateVisualResources();
        }

        if (Mathf.Abs(styleScale - scale) < 0.01f)
        {
            return;
        }

        styleScale = scale;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(20f * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = PrimaryTextColor }
        };
        settingLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(16f * scale),
            fontStyle = FontStyle.Normal,
            normal = { textColor = SecondaryTextColor }
        };
        toggleTextStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * scale)),
            fontStyle = FontStyle.Bold,
            normal = { textColor = PrimaryTextColor }
        };
    }

    private void CreateVisualResources()
    {
        gearTexture = Register(CreateGearTexture(96));

        Texture2D panelTexture = Register(CreateRoundedRectTexture(32, 8f, PanelColor));
        Texture2D shadowTexture = Register(CreateRoundedRectTexture(32, 8f, new Color(0f, 0f, 0f, 0.42f)));
        Texture2D buttonTexture = Register(CreateRoundedRectTexture(32, 8f, new Color(0.04f, 0.05f, 0.06f, 0.62f)));
        Texture2D buttonHoverTexture = Register(CreateRoundedRectTexture(32, 8f, new Color(0.11f, 0.13f, 0.14f, 0.86f)));
        Texture2D buttonActiveTexture = Register(CreateRoundedRectTexture(32, 8f, new Color(0.12f, 0.46f, 0.30f, 0.92f)));
        Texture2D closeHoverTexture = Register(CreateRoundedRectTexture(32, 8f, new Color(1f, 1f, 1f, 0.08f)));
        Texture2D toggleOnTexture = Register(CreateRoundedRectTexture(32, 16f, AccentColor));
        Texture2D toggleOffTexture = Register(CreateRoundedRectTexture(32, 16f, new Color(0.24f, 0.27f, 0.29f, 1f)));
        Texture2D knobTexture = Register(CreateRoundedRectTexture(32, 16f, new Color(0.96f, 0.97f, 0.98f, 1f)));

        panelStyle = CreateBackgroundStyle(panelTexture, 8);
        shadowStyle = CreateBackgroundStyle(shadowTexture, 8);
        settingsButtonStyle = CreateButtonStyle(buttonTexture, buttonHoverTexture, buttonHoverTexture, 8);
        settingsButtonOpenStyle = CreateButtonStyle(buttonActiveTexture, buttonActiveTexture, buttonHoverTexture, 8);
        closeButtonStyle = CreateButtonStyle(null, closeHoverTexture, closeHoverTexture, 8);
        toggleHitStyle = new GUIStyle();
        toggleOnStyle = CreateBackgroundStyle(toggleOnTexture, 16);
        toggleOffStyle = CreateBackgroundStyle(toggleOffTexture, 16);
        toggleKnobStyle = CreateBackgroundStyle(knobTexture, 16);
    }

    private static GUIStyle CreateBackgroundStyle(Texture2D background, int border)
    {
        GUIStyle style = new GUIStyle();
        style.normal.background = background;
        style.border = new RectOffset(border, border, border, border);
        return style;
    }

    private static GUIStyle CreateButtonStyle(
        Texture2D normal,
        Texture2D hover,
        Texture2D active,
        int border)
    {
        GUIStyle style = new GUIStyle();
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = active;
        style.focused.background = normal;
        style.border = new RectOffset(border, border, border, border);
        return style;
    }

    private Texture2D Register(Texture2D texture)
    {
        generatedTextures.Add(texture);
        return texture;
    }

    private static Texture2D CreateRoundedRectTexture(int size, float radius, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Settings UI",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float pixelX = x + 0.5f;
                float pixelY = y + 0.5f;
                float nearestX = Mathf.Clamp(pixelX, radius, size - radius);
                float nearestY = Mathf.Clamp(pixelY, radius, size - radius);
                float distance = Vector2.Distance(
                    new Vector2(pixelX, pixelY),
                    new Vector2(nearestX, nearestY));
                float coverage = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateGearTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Settings Gear",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        const int sampleCount = 2;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int coveredSamples = 0;
                for (int sampleY = 0; sampleY < sampleCount; sampleY++)
                {
                    for (int sampleX = 0; sampleX < sampleCount; sampleX++)
                    {
                        float normalizedX = ((x + (sampleX + 0.5f) / sampleCount) / size) - 0.5f;
                        float normalizedY = ((y + (sampleY + 0.5f) / sampleCount) / size) - 0.5f;
                        if (IsInsideGear(normalizedX, normalizedY))
                        {
                            coveredSamples++;
                        }
                    }
                }

                float alpha = coveredSamples / (float)(sampleCount * sampleCount);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static bool IsInsideGear(float x, float y)
    {
        float radius = Mathf.Sqrt(x * x + y * y);
        if (radius < 0.135f || radius > 0.46f)
        {
            return false;
        }

        if (radius <= 0.325f)
        {
            return true;
        }

        float toothSegment = Mathf.PI * 0.25f;
        float angle = Mathf.Atan2(y, x);
        float centeredToothAngle = Mathf.Repeat(angle + toothSegment * 0.5f, toothSegment) - toothSegment * 0.5f;
        return radius >= 0.27f && Mathf.Abs(centeredToothAngle) <= toothSegment * 0.22f;
    }

    private static Rect Inset(Rect rect, float amount)
    {
        return new Rect(
            rect.x + amount,
            rect.y + amount,
            Mathf.Max(0f, rect.width - amount * 2f),
            Mathf.Max(0f, rect.height - amount * 2f));
    }

    private static LayoutInfo CalculateLayout()
    {
        Rect safeArea = GetGuiSafeArea();
        float scale = GetUiScale(safeArea);
        float padding = ScreenPadding * scale;
        float buttonSize = SettingsButtonSize * scale;

        Rect settingsButtonRect = new Rect(
            safeArea.xMax - padding - buttonSize,
            safeArea.y + padding,
            buttonSize,
            buttonSize);
        Rect panelRect = new Rect(
            safeArea.center.x - PanelWidth * scale * 0.5f,
            safeArea.center.y - PanelHeight * scale * 0.5f,
            PanelWidth * scale,
            PanelHeight * scale);

        float panelRightLimit = settingsButtonRect.x - padding;
        if (panelRect.xMax > panelRightLimit)
        {
            panelRect.x = Mathf.Max(safeArea.x + padding, panelRightLimit - panelRect.width);
        }

        panelRect.y = Mathf.Clamp(
            panelRect.y,
            safeArea.y + padding,
            safeArea.yMax - padding - panelRect.height);

        return new LayoutInfo(scale, settingsButtonRect, panelRect);
    }

    private static Rect GetGuiSafeArea()
    {
        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        return new Rect(
            safeArea.x,
            Screen.height - safeArea.yMax,
            safeArea.width,
            safeArea.height);
    }

    private static float GetUiScale(Rect safeArea)
    {
        float resolutionScale = Mathf.Min(
            safeArea.width / ReferenceWidth,
            safeArea.height / ReferenceHeight);
        float preferredScale = Screen.dpi >= 120f
            ? Screen.dpi / 160f
            : resolutionScale;
        preferredScale = Mathf.Clamp(preferredScale, MinimumUiScale, MaximumUiScale);

        float horizontalFit = safeArea.width
            / (PanelWidth + SettingsButtonSize + ScreenPadding * 3f);
        float verticalFit = safeArea.height / (PanelHeight + ScreenPadding * 2f);
        float maximumFitScale = Mathf.Max(0.5f, Mathf.Min(horizontalFit, verticalFit));
        return Mathf.Min(preferredScale, maximumFitScale);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        for (int i = 0; i < generatedTextures.Count; i++)
        {
            Texture2D texture = generatedTextures[i];
            if (texture == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        generatedTextures.Clear();
    }

    private readonly struct LayoutInfo
    {
        public LayoutInfo(float scale, Rect settingsButtonRect, Rect panelRect)
        {
            Scale = scale;
            SettingsButtonRect = settingsButtonRect;
            PanelRect = panelRect;
        }

        public float Scale { get; }
        public Rect SettingsButtonRect { get; }
        public Rect PanelRect { get; }
    }
}
