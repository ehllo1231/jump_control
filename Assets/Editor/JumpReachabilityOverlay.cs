using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Player의 현재 위치와 점프 튜닝값으로 Scene View에 도달 가능한 공간을 표시합니다.
/// </summary>
[InitializeOnLoad]
public static class JumpReachabilityOverlay
{
    private const string EnabledKey = "JumpTiming.JumpReachabilityOverlay.Enabled";
    private const string MenuPath = "Tools/Jump Timing/Show Jump Reachability";
    private const int AngleSampleCount = 25;
    private const int PowerSampleCount = 9;
    private const int TrajectoryStepCount = 42;
    private const float MinimumPreviewSeconds = 0.75f;
    private const float MaximumPreviewSeconds = 4.5f;
    private const float CandidateTopInset = 0.02f;
    private const float RebuildIntervalSeconds = 0.1f;
    private const float HashPrecision = 1000f;
    private const float CollisionSkin = 0.01f;
    private const float BroadPhasePadding = 0.02f;
    private const float InitialContactIgnoreDistance = 0.015f;
    private const float MinimumSimulationDeltaTime = 0.001f;
    private const float DefaultMinimumWallNormalX = 0.55f;
    private const float DefaultMinimumWallBounceExitSpeed = 1.25f;
    private const float DefaultWallBounceSeparationDistance = 0.015f;
    private const int MaximumWallBouncesPerTrajectory = 4;

    private static readonly Color boundaryColor = new Color(0.2f, 0.74f, 1f, 0.72f);
    private static readonly Color candidateColor = new Color(0.2f, 1f, 0.55f, 0.95f);
    private static readonly Color startColor = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color32 areaVertexColor = new Color32(46, 158, 255, 32);

    private static readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
    private static readonly List<Vector3> meshVertices = new List<Vector3>(48000);
    private static readonly List<int> meshTriangles = new List<int>(72000);
    private static readonly List<Color32> meshColors = new List<Color32>(48000);
    private static readonly List<ObstacleBounds> obstacleBoundsBuffer = new List<ObstacleBounds>(512);
    private static readonly List<LandingTarget> landingTargetsBuffer = new List<LandingTarget>(128);
    private static readonly Dictionary<Platform2D, ReachCandidate> cachedCandidates = new Dictionary<Platform2D, ReachCandidate>();
    private static readonly ContactFilter2D obstacleFilter = new ContactFilter2D
    {
        useTriggers = false
    };

    private static GUIStyle labelStyle;
    private static GUIStyle compactLabelStyle;
    private static Mesh areaMesh;
    private static Material areaMaterial;
    private static int cachedHash;
    private static bool hasCachedPreview;
    private static double lastPreviewCheckTime = -1000d;
    private static Vector2 cachedStart;
    private static float cachedPlayerHalfWidth;
    private static float cachedMinAngle;
    private static float cachedMaxAngle;
    private static float cachedMinPower;
    private static float cachedMaxPower;

    public static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

    static JumpReachabilityOverlay()
    {
        SceneView.duringSceneGui += DrawReachability;
        Selection.selectionChanged += InvalidatePreview;
        EditorApplication.hierarchyChanged += InvalidatePreview;
        Undo.undoRedoPerformed += InvalidatePreview;
        EditorApplication.playModeStateChanged += _ => SceneView.RepaintAll();
        EditorApplication.update += RepaintWhilePlaying;
    }

    public static void SetEnabled(bool enabled)
    {
        EditorPrefs.SetBool(EnabledKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
        lastPreviewCheckTime = -1000d;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, false, 31)]
    private static void ToggleReachability()
    {
        SetEnabled(!IsEnabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleReachability()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static void RepaintWhilePlaying()
    {
        if (Application.isPlaying && IsEnabled)
        {
            SceneView.RepaintAll();
        }
    }

    private static void InvalidatePreview()
    {
        lastPreviewCheckTime = -1000d;
        SceneView.RepaintAll();
    }

    private static void DrawReachability(SceneView sceneView)
    {
        if (!IsEnabled)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent != null && currentEvent.type != EventType.Repaint)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (!hasCachedPreview || now - lastPreviewCheckTime >= RebuildIntervalSeconds)
        {
            RefreshPreviewIfNeeded(now);
        }

        if (!hasCachedPreview)
        {
            return;
        }

        DrawAreaMesh();
        DrawStartMarker(cachedStart, cachedMinAngle, cachedMaxAngle, cachedMinPower, cachedMaxPower);
        DrawReachableCandidates(cachedCandidates, cachedPlayerHalfWidth);
    }

    private static void RefreshPreviewIfNeeded(double now)
    {
        lastPreviewCheckTime = now;
        PlayerController player = FindPlayerController();
        if (player == null || player.JumpTuning == null)
        {
            ClearCachedPreview();
            return;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerJumpMotor jumpMotor = player.GetComponent<PlayerJumpMotor>();
        JumpTuningConfig tuning = player.JumpTuning;
        Vector2 start = GetStartPosition(player, body);
        Vector2 gravity = GetGravity(body);
        float linearDamping = GetLinearDamping(body);
        Vector2 playerHalfSize = GetPlayerHalfSize(player, tuning);
        List<Platform2D> platforms = FindScenePlatforms();
        List<Collider2D> obstacles = FindSceneObstacleColliders(player);
        int previewHash = ComputePreviewHash(player, body, jumpMotor, tuning, start, gravity, playerHalfSize, platforms, obstacles);

        if (!hasCachedPreview || previewHash != cachedHash)
        {
            RebuildPreview(
                player,
                body,
                jumpMotor,
                tuning,
                start,
                gravity,
                linearDamping,
                playerHalfSize,
                platforms,
                obstacles,
                previewHash);
        }
    }

    private static void ClearCachedPreview()
    {
        cachedHash = 0;
        cachedCandidates.Clear();
        hasCachedPreview = false;
        if (areaMesh != null)
        {
            areaMesh.Clear();
        }
    }

    private static void RebuildPreview(
        PlayerController player,
        Rigidbody2D body,
        PlayerJumpMotor jumpMotor,
        JumpTuningConfig tuning,
        Vector2 start,
        Vector2 gravity,
        float linearDamping,
        Vector2 playerHalfSize,
        List<Platform2D> platforms,
        List<Collider2D> obstacles,
        int previewHash)
    {
        Trajectory[,] trajectories = new Trajectory[PowerSampleCount, AngleSampleCount];
        Dictionary<Platform2D, ReachCandidate> candidates = new Dictionary<Platform2D, ReachCandidate>();
        Vector2 playerSize = new Vector2(playerHalfSize.x * 2f, playerHalfSize.y * 2f);
        Vector2 castSize = new Vector2(
            Mathf.Max(0.01f, playerSize.x - CollisionSkin * 2f),
            Mathf.Max(0.01f, playerSize.y - CollisionSkin * 2f));
        WallBouncePreviewSettings wallBounceSettings = GetWallBouncePreviewSettings(tuning, jumpMotor);
        BuildObstacleBounds(obstacles, obstacleBoundsBuffer);
        BuildLandingTargets(platforms, playerHalfSize.x, playerHalfSize.y, landingTargetsBuffer);

        for (int powerIndex = 0; powerIndex < PowerSampleCount; powerIndex++)
        {
            float normalizedPower = PowerSampleCount == 1 ? 1f : powerIndex / (float)(PowerSampleCount - 1);
            float power = tuning.EvaluateJumpPower(normalizedPower);

            for (int angleIndex = 0; angleIndex < AngleSampleCount; angleIndex++)
            {
                float normalizedAngle = AngleSampleCount == 1 ? 0.5f : angleIndex / (float)(AngleSampleCount - 1);
                float angle = Mathf.Lerp(tuning.MinDirectionAngle, tuning.MaxDirectionAngle, normalizedAngle);
                Vector2 launchVelocity = CalculateLaunchVelocity(angle, power, body, jumpMotor);
                Trajectory trajectory = BuildTrajectory(
                    player,
                    start,
                    launchVelocity,
                    gravity,
                    linearDamping,
                    castSize,
                    obstacleBoundsBuffer,
                    wallBounceSettings);
                trajectories[powerIndex, angleIndex] = trajectory;

                CollectReachableCandidates(
                    trajectory,
                    landingTargetsBuffer,
                    normalizedPower,
                    angle,
                    candidates);
            }
        }

        RebuildAreaMesh(trajectories);
        cachedCandidates.Clear();
        foreach (KeyValuePair<Platform2D, ReachCandidate> pair in candidates)
        {
            cachedCandidates.Add(pair.Key, pair.Value);
        }

        cachedHash = previewHash;
        cachedStart = start;
        cachedPlayerHalfWidth = playerHalfSize.x;
        cachedMinAngle = tuning.MinDirectionAngle;
        cachedMaxAngle = tuning.MaxDirectionAngle;
        cachedMinPower = tuning.MinimumJumpPower;
        cachedMaxPower = tuning.MaximumJumpPower;
        hasCachedPreview = true;
    }

    private static Trajectory BuildTrajectory(
        PlayerController player,
        Vector2 start,
        Vector2 launchVelocity,
        Vector2 gravity,
        float linearDamping,
        Vector2 castSize,
        List<ObstacleBounds> obstacleBounds,
        WallBouncePreviewSettings wallBounceSettings)
    {
        float previewSeconds = CalculatePreviewSeconds(launchVelocity, gravity);
        float pointDeltaTime = previewSeconds / TrajectoryStepCount;
        float simulationDeltaTime = GetSimulationDeltaTime(pointDeltaTime);
        Vector2 position = start;
        Vector2 velocity = launchVelocity;
        int wallBounceCount = 0;
        Trajectory trajectory = new Trajectory();
        trajectory.Add(start);

        for (int i = 1; i <= TrajectoryStepCount; i++)
        {
            float remainingTime = pointDeltaTime;
            bool hitObstacle = false;

            while (remainingTime > 0f)
            {
                float deltaTime = Mathf.Min(simulationDeltaTime, remainingTime);
                Vector2 velocityBeforeStep = velocity;
                velocity += gravity * deltaTime;
                velocity = ApplyLinearDamping(velocity, linearDamping, deltaTime);

                Vector2 nextPosition = position + velocity * deltaTime;
                if (TryFindObstacleHit(
                    player,
                    start,
                    position,
                    nextPosition,
                    castSize,
                    obstacleBounds,
                    out ObstacleHit obstacleHit))
                {
                    trajectory.Add(obstacleHit.Point);

                    if (wallBounceCount >= MaximumWallBouncesPerTrajectory
                        || !TryCalculateWallBounceVelocity(
                            obstacleHit,
                            velocityBeforeStep,
                            velocity,
                            wallBounceSettings,
                            out Vector2 bouncedVelocity))
                    {
                        hitObstacle = true;
                        break;
                    }

                    velocity = bouncedVelocity;
                    position = obstacleHit.Point + obstacleHit.Normal * wallBounceSettings.SeparationDistance;
                    wallBounceCount++;
                    remainingTime -= Mathf.Min(remainingTime, Mathf.Max(MinimumSimulationDeltaTime, deltaTime * obstacleHit.Fraction));
                    continue;
                }

                position = nextPosition;
                remainingTime -= deltaTime;
            }

            if (hitObstacle)
            {
                break;
            }

            trajectory.Add(position);
        }

        return trajectory;
    }

    private static bool TryFindObstacleHit(
        PlayerController player,
        Vector2 start,
        Vector2 from,
        Vector2 to,
        Vector2 castSize,
        List<ObstacleBounds> obstacleBounds,
        out ObstacleHit obstacleHit)
    {
        obstacleHit = default;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        Vector2 direction = delta / distance;
        if (!MightHitObstacle(from, to, castSize, obstacleBounds))
        {
            return false;
        }

        int hitCount = Physics2D.BoxCast(from, castSize, 0f, direction, obstacleFilter, castHits, distance);
        float nearestDistance = float.PositiveInfinity;
        Vector2 nearestNormal = Vector2.zero;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            Collider2D collider = hit.collider;
            if (collider == null || collider.transform.IsChildOf(player.transform))
            {
                continue;
            }

            bool nearStart = (from - start).sqrMagnitude <= 0.04f;
            if (nearStart && hit.distance <= InitialContactIgnoreDistance)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestNormal = hit.normal;
                foundHit = true;
            }
        }

        if (!foundHit)
        {
            return false;
        }

        float fraction = distance > 0.0001f ? Mathf.Clamp01(nearestDistance / distance) : 1f;
        obstacleHit = new ObstacleHit(from + direction * nearestDistance, nearestNormal, fraction);
        return true;
    }

    private static bool MightHitObstacle(
        Vector2 from,
        Vector2 to,
        Vector2 castSize,
        List<ObstacleBounds> obstacleBounds)
    {
        if (obstacleBounds.Count == 0)
        {
            return false;
        }

        Vector2 halfSize = castSize * 0.5f;
        Vector2 min = Vector2.Min(from, to) - halfSize - Vector2.one * BroadPhasePadding;
        Vector2 max = Vector2.Max(from, to) + halfSize + Vector2.one * BroadPhasePadding;

        for (int i = 0; i < obstacleBounds.Count; i++)
        {
            if (obstacleBounds[i].Intersects(min, max))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCalculateWallBounceVelocity(
        ObstacleHit obstacleHit,
        Vector2 velocityBeforeStep,
        Vector2 currentVelocity,
        WallBouncePreviewSettings settings,
        out Vector2 bouncedVelocity)
    {
        bouncedVelocity = currentVelocity;
        if (!settings.Enabled || Mathf.Abs(obstacleHit.Normal.x) < settings.MinimumWallNormalX)
        {
            return false;
        }

        float wallDirection = Mathf.Sign(obstacleHit.Normal.x);
        float impactSpeed = Mathf.Abs(currentVelocity.x);
        float exitSpeedFloor = settings.MinimumExitSpeed * Mathf.Clamp01(settings.Elasticity);
        float bounceSpeed = Mathf.Max(impactSpeed * settings.Elasticity, exitSpeedFloor);
        float currentOutwardSpeed = currentVelocity.x * wallDirection;
        if (currentOutwardSpeed >= bounceSpeed)
        {
            return false;
        }

        float bounceX = wallDirection * bounceSpeed;
        float bounceY = settings.VerticalMode == WallBounceVerticalVelocityMode.PreservePreCollisionVelocity
            ? velocityBeforeStep.y
            : currentVelocity.y;
        bouncedVelocity = new Vector2(bounceX, bounceY);
        return true;
    }

    private static void RebuildAreaMesh(Trajectory[,] trajectories)
    {
        Mesh mesh = GetAreaMesh();
        mesh.Clear();
        meshVertices.Clear();
        meshTriangles.Clear();
        meshColors.Clear();

        for (int powerIndex = 0; powerIndex < PowerSampleCount; powerIndex++)
        {
            for (int angleIndex = 0; angleIndex < AngleSampleCount - 1; angleIndex++)
            {
                AddTrajectoryStrip(trajectories[powerIndex, angleIndex], trajectories[powerIndex, angleIndex + 1]);
            }
        }

        mesh.indexFormat = meshVertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(meshVertices);
        mesh.SetColors(meshColors);
        mesh.SetTriangles(meshTriangles, 0);
        mesh.RecalculateBounds();
    }

    private static void AddTrajectoryStrip(Trajectory a, Trajectory b)
    {
        int count = Mathf.Min(a.Count, b.Count);
        for (int i = 1; i < count; i++)
        {
            AddQuad(a.Points[i - 1], a.Points[i], b.Points[i], b.Points[i - 1]);
        }
    }

    private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        if (IsDegenerate(a, b, c, d))
        {
            return;
        }

        int startIndex = meshVertices.Count;
        meshVertices.Add(a);
        meshVertices.Add(b);
        meshVertices.Add(c);
        meshVertices.Add(d);

        meshColors.Add(areaVertexColor);
        meshColors.Add(areaVertexColor);
        meshColors.Add(areaVertexColor);
        meshColors.Add(areaVertexColor);

        meshTriangles.Add(startIndex);
        meshTriangles.Add(startIndex + 1);
        meshTriangles.Add(startIndex + 2);
        meshTriangles.Add(startIndex);
        meshTriangles.Add(startIndex + 2);
        meshTriangles.Add(startIndex + 3);
    }

    private static bool IsDegenerate(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        float areaA = Mathf.Abs(Vector3.Cross(b - a, c - a).z);
        float areaB = Mathf.Abs(Vector3.Cross(c - a, d - a).z);
        return areaA + areaB <= 0.00001f;
    }

    private static void DrawAreaMesh()
    {
        Mesh mesh = GetAreaMesh();
        if (mesh.vertexCount == 0 || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Material material = GetAreaMaterial();
        if (material == null)
        {
            return;
        }

        material.SetPass(0);
        Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
    }

    private static Mesh GetAreaMesh()
    {
        if (areaMesh == null)
        {
            areaMesh = new Mesh
            {
                name = "Jump Reachability Area",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return areaMesh;
    }

    private static Material GetAreaMaterial()
    {
        if (areaMaterial != null)
        {
            return areaMaterial;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            return null;
        }

        areaMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        areaMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        areaMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        areaMaterial.SetInt("_Cull", (int)CullMode.Off);
        areaMaterial.SetInt("_ZWrite", 0);
        areaMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        return areaMaterial;
    }

    private static PlayerController FindPlayerController()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            PlayerController selectedPlayer = selectedObject.GetComponentInParent<PlayerController>();
            if (selectedPlayer == null)
            {
                selectedPlayer = selectedObject.GetComponentInChildren<PlayerController>(true);
            }

            if (selectedPlayer != null)
            {
                return selectedPlayer;
            }
        }

        return Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    private static List<Platform2D> FindScenePlatforms()
    {
        Platform2D[] allPlatforms = Resources.FindObjectsOfTypeAll<Platform2D>();
        List<Platform2D> scenePlatforms = new List<Platform2D>(allPlatforms.Length);

        foreach (Platform2D platform in allPlatforms)
        {
            if (platform == null
                || EditorUtility.IsPersistent(platform)
                || !platform.gameObject.scene.IsValid()
                || !platform.gameObject.activeInHierarchy)
            {
                continue;
            }

            scenePlatforms.Add(platform);
        }

        scenePlatforms.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        return scenePlatforms;
    }

    private static List<Collider2D> FindSceneObstacleColliders(PlayerController player)
    {
        Collider2D[] allColliders = Resources.FindObjectsOfTypeAll<Collider2D>();
        List<Collider2D> sceneColliders = new List<Collider2D>(allColliders.Length);

        foreach (Collider2D collider in allColliders)
        {
            if (collider == null
                || !collider.enabled
                || collider.isTrigger
                || EditorUtility.IsPersistent(collider)
                || !collider.gameObject.scene.IsValid()
                || !collider.gameObject.activeInHierarchy
                || collider.transform.IsChildOf(player.transform))
            {
                continue;
            }

            sceneColliders.Add(collider);
        }

        sceneColliders.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        return sceneColliders;
    }

    private static void BuildObstacleBounds(List<Collider2D> obstacles, List<ObstacleBounds> bounds)
    {
        bounds.Clear();
        foreach (Collider2D collider in obstacles)
        {
            if (collider == null)
            {
                continue;
            }

            bounds.Add(new ObstacleBounds(collider.bounds));
        }
    }

    private static void BuildLandingTargets(
        List<Platform2D> platforms,
        float playerHalfWidth,
        float playerHalfHeight,
        List<LandingTarget> targets)
    {
        targets.Clear();
        foreach (Platform2D platform in platforms)
        {
            if (platform == null
                || !platform.TryGetTopLandingSegment(
                    playerHalfWidth,
                    out float minStandX,
                    out float maxStandX,
                    out float platformTopY))
            {
                continue;
            }

            targets.Add(new LandingTarget(
                platform,
                minStandX,
                maxStandX,
                platformTopY + playerHalfHeight));
        }
    }

    private static Vector2 GetStartPosition(PlayerController player, Rigidbody2D body)
    {
        if (Application.isPlaying && body != null)
        {
            return body.position;
        }

        return player.transform.position;
    }

    private static Vector2 GetGravity(Rigidbody2D body)
    {
        float gravityScale = body != null ? body.gravityScale : 1f;
        Vector2 gravity = Physics2D.gravity * gravityScale;
        return gravity.sqrMagnitude > 0.0001f ? gravity : new Vector2(0f, -9.81f);
    }

    private static float GetLinearDamping(Rigidbody2D body)
    {
        return body != null ? Mathf.Max(0f, body.linearDamping) : 0f;
    }

    private static WallBouncePreviewSettings GetWallBouncePreviewSettings(
        JumpTuningConfig tuning,
        PlayerJumpMotor jumpMotor)
    {
        float elasticity = tuning != null ? tuning.WallBounceElasticity : 0f;
        WallBounceVerticalVelocityMode verticalMode = tuning != null
            ? tuning.WallBounceVerticalVelocityMode
            : WallBounceVerticalVelocityMode.PreservePreCollisionVelocity;
        float minimumWallNormalX = jumpMotor != null
            ? jumpMotor.MinimumWallNormalX
            : DefaultMinimumWallNormalX;
        float minimumExitSpeed = jumpMotor != null
            ? jumpMotor.MinimumWallBounceExitSpeed
            : DefaultMinimumWallBounceExitSpeed;
        float separationDistance = jumpMotor != null
            ? jumpMotor.WallBounceSeparationDistance
            : DefaultWallBounceSeparationDistance;

        return new WallBouncePreviewSettings(elasticity, verticalMode, minimumWallNormalX, minimumExitSpeed, separationDistance);
    }

    private static Vector2 GetPlayerHalfSize(PlayerController player, JumpTuningConfig tuning)
    {
        BoxCollider2D collider = player.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            return new Vector2(Mathf.Max(0.01f, bounds.extents.x), Mathf.Max(0.01f, bounds.extents.y));
        }

        float halfSize = tuning.PlayerSquareSize * 0.5f;
        return new Vector2(halfSize, halfSize);
    }

    private static Vector2 CalculateLaunchVelocity(
        float angle,
        float power,
        Rigidbody2D body,
        PlayerJumpMotor jumpMotor)
    {
        Vector2 baseVelocity = Vector2.zero;
        if (Application.isPlaying && body != null)
        {
            baseVelocity = body.linearVelocity;
        }

        if (jumpMotor != null && jumpMotor.ClearVelocityBeforeJump)
        {
            baseVelocity = new Vector2(jumpMotor.KeepHorizontalVelocityOnJump ? baseVelocity.x : 0f, 0f);
        }

        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
        float impulseMultiplier = jumpMotor != null ? jumpMotor.ImpulseMultiplier : 1f;
        float mass = body != null ? Mathf.Max(0.0001f, body.mass) : 1f;
        return baseVelocity + direction * power * impulseMultiplier / mass;
    }

    private static float CalculatePreviewSeconds(Vector2 launchVelocity, Vector2 gravity)
    {
        float verticalGravity = Mathf.Abs(gravity.y);
        if (verticalGravity < 0.0001f)
        {
            return 2f;
        }

        float airborneSeconds = Mathf.Max(0f, launchVelocity.y) * 2f / verticalGravity;
        return Mathf.Clamp(airborneSeconds + 0.6f, MinimumPreviewSeconds, MaximumPreviewSeconds);
    }

    private static float GetSimulationDeltaTime(float pointDeltaTime)
    {
        float fixedDeltaTime = Mathf.Max(MinimumSimulationDeltaTime, Time.fixedDeltaTime);
        return Mathf.Clamp(fixedDeltaTime, MinimumSimulationDeltaTime, Mathf.Max(MinimumSimulationDeltaTime, pointDeltaTime));
    }

    private static Vector2 ApplyLinearDamping(Vector2 velocity, float linearDamping, float deltaTime)
    {
        if (linearDamping <= 0f)
        {
            return velocity;
        }

        return velocity / (1f + linearDamping * deltaTime);
    }

    private static void CollectReachableCandidates(
        Trajectory trajectory,
        List<LandingTarget> landingTargets,
        float normalizedPower,
        float angle,
        Dictionary<Platform2D, ReachCandidate> candidates)
    {
        for (int i = 1; i < trajectory.Count; i++)
        {
            Vector2 previous = trajectory.Points[i - 1];
            Vector2 current = trajectory.Points[i];
            if (current.y > previous.y)
            {
                continue;
            }

            foreach (LandingTarget target in landingTargets)
            {
                if (candidates.ContainsKey(target.Platform))
                {
                    continue;
                }

                if (TryFindLandingPoint(previous, current, target, out Vector2 landingPoint))
                {
                    candidates.Add(target.Platform, new ReachCandidate(landingPoint, normalizedPower, angle));
                }
            }
        }
    }

    private static bool TryFindLandingPoint(
        Vector2 previous,
        Vector2 current,
        LandingTarget target,
        out Vector2 landingPoint)
    {
        landingPoint = default;
        float centerLandingY = target.CenterLandingY;
        if (previous.y < centerLandingY || current.y > centerLandingY)
        {
            return false;
        }

        float yDelta = current.y - previous.y;
        if (Mathf.Abs(yDelta) < 0.0001f)
        {
            return false;
        }

        float ratio = (centerLandingY - previous.y) / yDelta;
        if (ratio < 0f || ratio > 1f)
        {
            return false;
        }

        float x = Mathf.Lerp(previous.x, current.x, ratio);
        if (target.MinStandX > target.MaxStandX || x < target.MinStandX || x > target.MaxStandX)
        {
            return false;
        }

        landingPoint = new Vector2(x, centerLandingY);
        return true;
    }

    private static void DrawStartMarker(
        Vector2 start,
        float minAngle,
        float maxAngle,
        float minPower,
        float maxPower)
    {
        float handleSize = HandleUtility.GetHandleSize(start) * 0.08f;
        Color previousColor = Handles.color;
        Handles.color = startColor;
        Handles.DrawWireDisc(start, Vector3.forward, handleSize);

        string label =
            $"Reach Area\nAngle {minAngle:0.#}°~{maxAngle:0.#}°\nPower {minPower:0.#}~{maxPower:0.#}";
        Handles.Label(start + Vector2.up * handleSize * 2.4f, label, GetLabelStyle());
        Handles.color = previousColor;
    }

    private static void DrawReachableCandidates(Dictionary<Platform2D, ReachCandidate> candidates, float playerHalfWidth)
    {
        Color previousColor = Handles.color;
        foreach (KeyValuePair<Platform2D, ReachCandidate> pair in candidates)
        {
            Platform2D platform = pair.Key;
            ReachCandidate candidate = pair.Value;
            if (platform == null)
            {
                continue;
            }

            Bounds bounds = platform.WorldBounds;
            float minStandX = bounds.min.x + playerHalfWidth;
            float maxStandX = bounds.max.x - playerHalfWidth;
            float topY = bounds.max.y + CandidateTopInset;
            Vector3 topLeft = new Vector3(minStandX, topY, 0f);
            Vector3 topRight = new Vector3(maxStandX, topY, 0f);
            float handleSize = HandleUtility.GetHandleSize(candidate.Point) * 0.08f;

            Handles.color = candidateColor;
            Handles.DrawAAPolyLine(5f, topLeft, topRight);
            Handles.DrawSolidDisc(candidate.Point, Vector3.forward, handleSize);
            Handles.color = boundaryColor;
            Handles.DrawWireDisc(candidate.Point, Vector3.forward, handleSize * 1.25f);

            string label = $"Reachable\n{candidate.Angle:0.#}° / {candidate.NormalizedPower:P0}";
            Handles.Label(candidate.Point + Vector2.up * handleSize * 2.2f, label, GetCompactLabelStyle());
        }

        Handles.color = previousColor;
    }

    private static int ComputePreviewHash(
        PlayerController player,
        Rigidbody2D body,
        PlayerJumpMotor jumpMotor,
        JumpTuningConfig tuning,
        Vector2 start,
        Vector2 gravity,
        Vector2 playerHalfSize,
        List<Platform2D> platforms,
        List<Collider2D> obstacles)
    {
        unchecked
        {
            int hash = 17;
            AddHash(ref hash, player.GetInstanceID());
            AddHash(ref hash, start);
            AddHash(ref hash, gravity);
            AddHash(ref hash, playerHalfSize);
            AddHash(ref hash, tuning.MinDirectionAngle);
            AddHash(ref hash, tuning.MaxDirectionAngle);
            AddHash(ref hash, tuning.MinimumJumpPower);
            AddHash(ref hash, tuning.MaximumJumpPower);
            AddHash(ref hash, tuning.WallBounceElasticity);
            AddHash(ref hash, (int)tuning.WallBounceVerticalVelocityMode);

            for (int i = 0; i < PowerSampleCount; i++)
            {
                float normalizedPower = PowerSampleCount == 1 ? 1f : i / (float)(PowerSampleCount - 1);
                AddHash(ref hash, tuning.EvaluateJumpPower(normalizedPower));
            }

            if (jumpMotor != null)
            {
                AddHash(ref hash, jumpMotor.ImpulseMultiplier);
                AddHash(ref hash, jumpMotor.ClearVelocityBeforeJump ? 1 : 0);
                AddHash(ref hash, jumpMotor.KeepHorizontalVelocityOnJump ? 1 : 0);
                AddHash(ref hash, jumpMotor.MinimumWallNormalX);
                AddHash(ref hash, jumpMotor.MinimumWallBounceExitSpeed);
                AddHash(ref hash, jumpMotor.WallBounceSeparationDistance);
            }

            if (body != null)
            {
                AddHash(ref hash, body.mass);
                AddHash(ref hash, body.gravityScale);
                AddHash(ref hash, body.linearDamping);
                if (Application.isPlaying)
                {
                    AddHash(ref hash, body.linearVelocity);
                }
            }

            foreach (Platform2D platform in platforms)
            {
                AddHash(ref hash, platform.GetInstanceID());
                AddHash(ref hash, (int)platform.Shape);
                AddHash(ref hash, platform.WorldBounds);
            }

            foreach (Collider2D collider in obstacles)
            {
                AddHash(ref hash, collider.GetInstanceID());
                AddHash(ref hash, collider.bounds);
            }

            return hash;
        }
    }

    private static void AddHash(ref int hash, int value)
    {
        hash = hash * 31 + value;
    }

    private static void AddHash(ref int hash, float value)
    {
        hash = hash * 31 + Mathf.RoundToInt(value * HashPrecision);
    }

    private static void AddHash(ref int hash, Vector2 value)
    {
        AddHash(ref hash, value.x);
        AddHash(ref hash, value.y);
    }

    private static void AddHash(ref int hash, Bounds bounds)
    {
        AddHash(ref hash, bounds.center);
        AddHash(ref hash, bounds.size);
    }

    private static GUIStyle GetLabelStyle()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        return labelStyle;
    }

    private static GUIStyle GetCompactLabelStyle()
    {
        if (compactLabelStyle == null)
        {
            compactLabelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10
            };
        }

        return compactLabelStyle;
    }

    private sealed class Trajectory
    {
        public readonly Vector3[] Points = new Vector3[TrajectoryStepCount + MaximumWallBouncesPerTrajectory + 1];
        public int Count { get; private set; }
        public Vector2 LastPoint => Points[Mathf.Max(0, Count - 1)];

        public void Add(Vector2 point)
        {
            if (Count >= Points.Length)
            {
                return;
            }

            Points[Count] = new Vector3(point.x, point.y, 0f);
            Count++;
        }
    }

    private readonly struct ObstacleBounds
    {
        public ObstacleBounds(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Min = new Vector2(min.x, min.y);
            Max = new Vector2(max.x, max.y);
        }

        private Vector2 Min { get; }
        private Vector2 Max { get; }

        public bool Intersects(Vector2 min, Vector2 max)
        {
            return min.x <= Max.x
                && max.x >= Min.x
                && min.y <= Max.y
                && max.y >= Min.y;
        }
    }

    private readonly struct LandingTarget
    {
        public LandingTarget(Platform2D platform, float minStandX, float maxStandX, float centerLandingY)
        {
            Platform = platform;
            MinStandX = minStandX;
            MaxStandX = maxStandX;
            CenterLandingY = centerLandingY;
        }

        public Platform2D Platform { get; }
        public float MinStandX { get; }
        public float MaxStandX { get; }
        public float CenterLandingY { get; }
    }

    private readonly struct ObstacleHit
    {
        public ObstacleHit(Vector2 point, Vector2 normal, float fraction)
        {
            Point = point;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.zero;
            Fraction = Mathf.Clamp01(fraction);
        }

        public Vector2 Point { get; }
        public Vector2 Normal { get; }
        public float Fraction { get; }
    }

    private readonly struct WallBouncePreviewSettings
    {
        public WallBouncePreviewSettings(
            float elasticity,
            WallBounceVerticalVelocityMode verticalMode,
            float minimumWallNormalX,
            float minimumExitSpeed,
            float separationDistance)
        {
            Elasticity = Mathf.Max(0f, elasticity);
            VerticalMode = verticalMode;
            MinimumWallNormalX = Mathf.Clamp01(minimumWallNormalX);
            MinimumExitSpeed = Mathf.Max(0f, minimumExitSpeed);
            SeparationDistance = Mathf.Max(0f, separationDistance);
        }

        public float Elasticity { get; }
        public WallBounceVerticalVelocityMode VerticalMode { get; }
        public float MinimumWallNormalX { get; }
        public float MinimumExitSpeed { get; }
        public float SeparationDistance { get; }
        public bool Enabled => Elasticity > 0f;
    }

    private readonly struct ReachCandidate
    {
        public ReachCandidate(Vector2 point, float normalizedPower, float angle)
        {
            Point = point;
            NormalizedPower = normalizedPower;
            Angle = angle;
        }

        public Vector2 Point { get; }
        public float NormalizedPower { get; }
        public float Angle { get; }
    }
}
