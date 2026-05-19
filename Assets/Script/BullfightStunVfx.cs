using UnityEngine;
using UnityEngine.UI;
using InfimaGames.LowPolyShooterPack;

public class BullfightStunVfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Visual")]
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float grayAlpha = 0.5f;
    [SerializeField] private float vignetteAlpha = 0.82f;
    [SerializeField] private float damageFlashAlpha = 0.42f;
    [SerializeField] private float damageFlashFadeSpeed = 4.5f;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.25f;
    [SerializeField] private float lowHealthPulseSpeed = 1.8f;
    [SerializeField] private float lowHealthOverlayAlpha = 0.18f;
    [SerializeField] private Color lowHealthColor = new Color(0.92f, 0.22f, 0.22f, 1f);

    [Header("Charge Lock Warning")]
    [SerializeField] private Color chargeLockColor = new Color(1f, 0.5f, 0.12f, 1f);
    [SerializeField] private float chargeLockFadeSpeed = 9.5f;
    [SerializeField] private float chargeLockGrayAlpha = 0.16f;
    [SerializeField] private float chargeLockOverlayAlpha = 0.19f;
    [SerializeField] private float chargeLockVignetteAlpha = 0.42f;
    [SerializeField] private float chargeLockPulseFrequency = 1.8f;
    [SerializeField] private float chargeLockFovPull = 3.35f;
    [SerializeField] private float chargeLockFovWave = 1.55f;

    [Header("Dizzy Feel")]
    [SerializeField] private float lingerDuration = 1.7f;
    [SerializeField] private float pulseFrequency = 2.1f;
    [SerializeField] private float vignetteOverscan = 1.18f;
    [SerializeField] private float shockDuration = 0.65f;
    [SerializeField] private float shockStrength = 1.1f;
    [SerializeField] private float cameraFovKick = 13f;
    [SerializeField] private float cameraFovWave = 7f;
    [SerializeField] private float cameraFovRecoverSpeed = 6f;

    [Header("Impact Burst")]
    [SerializeField] private Color impactBurstColor = new Color(1f, 0.12f, 0.02f, 1f);
    [SerializeField] private float impactBurstDuration = 0.95f;
    [SerializeField] private float impactBurstFlashAlpha = 0.92f;
    [SerializeField] private float impactBurstGrayBoost = 0.18f;
    [SerializeField] private float impactBurstVignetteBoost = 0.48f;
    [SerializeField] private float impactBurstShockDuration = 1.05f;
    [SerializeField] private float impactBurstLingerDuration = 2.15f;
    [SerializeField] private float impactBurstFovKick = 22f;
    [SerializeField] private float impactBurstFovWave = 12.5f;
    [SerializeField] private float impactBurstPulseFrequency = 7.2f;

    [Header("Overlay Layout")]
    [SerializeField] private int overlaySortingOrder = 5000;
    [SerializeField] private Vector2 overlayReferenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(64, 1024)] private int vignetteTextureSize = 256;
    private Canvas overlayCanvas;
    private Image grayOverlay;
    private Image warningOverlay;
    private Image damageOverlay;
    private Image lowHealthOverlay;
    private RawImage vignetteOverlay;
    private RectTransform vignetteRect;
    private float currentWeight;
    private float chargeLockWeight;
    private float damageFlashWeight;
    private Texture2D vignetteTexture;
    private float lingerTimer;
    private float shockTimer;
    private float impactBurstTimer;
    private float effectTime;
    private bool stunActive;
    private bool chargeLockActive;
    private Camera worldCamera;
    private float baseFieldOfView;
    private bool hasBaseFieldOfView;
    private PlayerStats subscribedPlayerStats;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureOverlay();
        SyncStateFromPlayer(true);
    }

    private void OnEnable()
    {
        ResolveReferencesIfNeeded();
        SyncStateFromPlayer(true);
    }

    private void Update()
    {
        if (playerStats == null || subscribedPlayerStats != playerStats)
            ResolveReferencesIfNeeded();

        UpdateStateWeights();
        damageFlashWeight = Mathf.MoveTowards(damageFlashWeight, 0f, Time.unscaledDeltaTime * damageFlashFadeSpeed);
        shockTimer = Mathf.Max(0f, shockTimer - Time.unscaledDeltaTime);
        impactBurstTimer = Mathf.Max(0f, impactBurstTimer - Time.unscaledDeltaTime);
        effectTime = HasActivePresentation() ? effectTime + Time.unscaledDeltaTime : 0f;

        ApplyWeight();
        ApplyDistortion();
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null)
            return;

        GameObject root = new GameObject("BullfightStunOverlay");
        root.transform.SetParent(transform, false);

        overlayCanvas = root.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = overlayReferenceResolution;
        root.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject gray = new GameObject("GrayOverlay");
        gray.transform.SetParent(root.transform, false);
        grayOverlay = gray.AddComponent<Image>();
        grayOverlay.raycastTarget = false;
        grayOverlay.color = new Color(0.5f, 0.5f, 0.5f, 0f);
        StretchFullScreen(grayOverlay.rectTransform);

        GameObject warning = new GameObject("ChargeLockOverlay");
        warning.transform.SetParent(root.transform, false);
        warningOverlay = warning.AddComponent<Image>();
        warningOverlay.raycastTarget = false;
        warningOverlay.color = new Color(chargeLockColor.r, chargeLockColor.g, chargeLockColor.b, 0f);
        StretchFullScreen(warningOverlay.rectTransform);

        GameObject damage = new GameObject("DamageOverlay");
        damage.transform.SetParent(root.transform, false);
        damageOverlay = damage.AddComponent<Image>();
        damageOverlay.raycastTarget = false;
        damageOverlay.color = new Color(0.9f, 0.1f, 0.1f, 0f);
        StretchFullScreen(damageOverlay.rectTransform);

        GameObject lowHealth = new GameObject("LowHealthOverlay");
        lowHealth.transform.SetParent(root.transform, false);
        lowHealthOverlay = lowHealth.AddComponent<Image>();
        lowHealthOverlay.raycastTarget = false;
        lowHealthOverlay.color = new Color(lowHealthColor.r, lowHealthColor.g, lowHealthColor.b, 0f);
        StretchFullScreen(lowHealthOverlay.rectTransform);

        GameObject vignette = new GameObject("VignetteOverlay");
        vignette.transform.SetParent(root.transform, false);
        vignetteOverlay = vignette.AddComponent<RawImage>();
        vignetteOverlay.raycastTarget = false;
        vignetteOverlay.color = new Color(0f, 0f, 0f, 0f);
        vignetteOverlay.texture = CreateVignetteTexture();
        vignetteRect = vignetteOverlay.rectTransform;
        StretchFullScreen(vignetteRect);
        ApplyVignetteOverscan();
    }

    private void ApplyWeight()
    {
        float shockWeight = GetShockWeight();
        float stunPulse = GetPulse01();
        float warningPulse = GetChargeLockPulse01();
        float impactWeight = GetImpactBurstWeight();
        float impactPulse = GetImpactBurstPulse01();
        float stunVisualWeight = Mathf.Clamp01(currentWeight + (shockWeight * 0.22f));
        float warningVisualWeight = chargeLockWeight * (0.82f + (0.18f * warningPulse));
        float boostedGrayAlpha = grayAlpha * Mathf.Lerp(1f, 1.28f, stunPulse) * Mathf.Lerp(1f, 1.35f, shockWeight);
        float boostedVignetteAlpha = vignetteAlpha * Mathf.Lerp(1f, 1.35f, stunPulse) * Mathf.Lerp(1f, 1.5f, shockWeight);
        float grayCompositeAlpha = Mathf.Clamp01(
            (boostedGrayAlpha * stunVisualWeight) +
            (chargeLockGrayAlpha * warningVisualWeight) +
            (impactBurstGrayBoost * impactWeight * (0.8f + (0.2f * impactPulse))));

        if (grayOverlay != null)
            grayOverlay.color = new Color(0.5f, 0.5f, 0.5f, grayCompositeAlpha);

        if (warningOverlay != null)
        {
            float warningAlpha = chargeLockOverlayAlpha * warningVisualWeight;
            warningOverlay.color = new Color(chargeLockColor.r, chargeLockColor.g, chargeLockColor.b, warningAlpha);
        }

        if (damageOverlay != null)
        {
            float flashAlpha = Mathf.Clamp01((damageFlashAlpha * Mathf.Clamp01(damageFlashWeight)) + (impactBurstFlashAlpha * impactWeight * (0.82f + (0.18f * impactPulse))));
            Color flashColor = Color.Lerp(new Color(1f, 0.42f, 0.16f, 1f), impactBurstColor, Mathf.Clamp01(impactWeight * 1.15f));
            damageOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
        }

        if (lowHealthOverlay != null)
        {
            bool lowHealthActive = playerStats != null && !playerStats.IsDead && playerStats.HealthNormalized <= lowHealthThreshold;
            float lowHealthPulse = lowHealthActive
                ? 0.45f + (0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed * Mathf.PI * 2f)))
                : 0f;
            lowHealthOverlay.color = new Color(lowHealthColor.r, lowHealthColor.g, lowHealthColor.b, lowHealthOverlayAlpha * lowHealthPulse);
        }

        if (vignetteOverlay != null)
        {
            float warningAlpha = chargeLockVignetteAlpha * warningVisualWeight;
            float stunAlpha = boostedVignetteAlpha * stunVisualWeight;
            float impactAlpha = impactBurstVignetteBoost * impactWeight * (0.84f + (0.16f * impactPulse));
            ApplyCompositeVignette(warningAlpha, stunAlpha, impactAlpha, impactWeight);
        }
    }

    public void TriggerDamageFlash()
    {
        damageFlashWeight = Mathf.Max(damageFlashWeight, 1f);
        PromoteDamageOverlay();
        ApplyWeight();
    }

    public void TriggerBullImpactBurst()
    {
        damageFlashWeight = Mathf.Max(damageFlashWeight, 1.35f);
        impactBurstTimer = Mathf.Max(impactBurstTimer, impactBurstDuration);
        shockTimer = Mathf.Max(shockTimer, impactBurstShockDuration);
        lingerTimer = Mathf.Max(lingerTimer, impactBurstLingerDuration);
        effectTime = 0f;
        PromoteDamageOverlay();
        ApplyWeight();
    }

    private Texture2D CreateVignetteTexture()
    {
        if (vignetteTexture != null)
            return vignetteTexture;

        int size = vignetteTextureSize;
        vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        vignetteTexture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDistance = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, distance));
                vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        vignetteTexture.Apply();
        return vignetteTexture;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);

        if (subscribedPlayerStats != playerStats)
            ResubscribePlayerEvents();
    }

    private void ResubscribePlayerEvents()
    {
        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnStunStateChanged -= HandleStunStateChanged;
            subscribedPlayerStats.OnBullChargeLockStateChanged -= HandleBullChargeLockStateChanged;
        }

        subscribedPlayerStats = playerStats;

        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnStunStateChanged += HandleStunStateChanged;
            subscribedPlayerStats.OnBullChargeLockStateChanged += HandleBullChargeLockStateChanged;
        }

        SyncStateFromPlayer(false);
    }

    private void SyncStateFromPlayer(bool snapWeights)
    {
        bool nextStunState = subscribedPlayerStats != null && !subscribedPlayerStats.IsDead && subscribedPlayerStats.isStunned;
        bool nextChargeLockState = subscribedPlayerStats != null && !subscribedPlayerStats.IsDead && subscribedPlayerStats.IsBullChargeLocked;

        stunActive = nextStunState;
        chargeLockActive = nextChargeLockState;

        if (stunActive)
        {
            lingerTimer = Mathf.Max(lingerTimer, lingerDuration);
            if (snapWeights)
                currentWeight = Mathf.Max(currentWeight, 1f);
        }

        if (chargeLockActive && snapWeights)
            chargeLockWeight = Mathf.Max(chargeLockWeight, 1f);
    }

    private void HandleStunStateChanged(bool isStunned)
    {
        if (stunActive == isStunned)
            return;

        stunActive = isStunned;
        if (!stunActive)
            return;

        lingerTimer = Mathf.Max(lingerTimer, lingerDuration);
        shockTimer = Mathf.Max(shockTimer, shockDuration);
        effectTime = 0f;
    }

    private void HandleBullChargeLockStateChanged(bool active)
    {
        if (chargeLockActive == active)
            return;

        chargeLockActive = active;
        if (chargeLockActive)
            effectTime = 0f;
    }

    private void UpdateStateWeights()
    {
        if (stunActive)
            lingerTimer = Mathf.Max(lingerTimer, lingerDuration);
        else if (lingerTimer > 0f)
            lingerTimer = Mathf.Max(0f, lingerTimer - Time.unscaledDeltaTime);

        currentWeight = Mathf.MoveTowards(currentWeight, GetStunTargetWeight(), Time.unscaledDeltaTime * fadeSpeed);
        chargeLockWeight = Mathf.MoveTowards(chargeLockWeight, GetChargeLockTargetWeight(), Time.unscaledDeltaTime * chargeLockFadeSpeed);
    }

    private float GetStunTargetWeight()
    {
        if (stunActive)
            return 1f;

        if (lingerDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(lingerTimer / lingerDuration) * 0.9f;
    }

    private float GetChargeLockTargetWeight()
    {
        return chargeLockActive && !stunActive ? 1f : 0f;
    }

    private void ApplyDistortion()
    {
        float shockWeight = GetShockWeight();
        float stunPulse = GetPulseSigned();
        float warningPulse = GetChargeLockPulseSigned();
        float impactWeight = GetImpactBurstWeight();
        float impactPulse = GetImpactBurstPulseSigned();
        float stunVisualWeight = Mathf.Clamp01(currentWeight + (shockWeight * shockStrength));

        ApplyCameraFov(stunVisualWeight, stunPulse, shockWeight, warningPulse, impactWeight, impactPulse);
    }

    private void ApplyCameraFov(float stunVisualWeight, float stunPulse, float shockWeight, float warningPulse, float impactWeight, float impactPulse)
    {
        EnsureCameraReference();
        if (worldCamera == null || !hasBaseFieldOfView)
            return;

        float targetFov = baseFieldOfView;
        if (chargeLockWeight > 0.001f)
        {
            targetFov -= chargeLockFovPull * chargeLockWeight;
            targetFov += chargeLockFovWave * chargeLockWeight * Mathf.Abs(warningPulse);
        }

        if (stunVisualWeight > 0.001f)
        {
            float targetOffset = (cameraFovKick * stunVisualWeight) + (cameraFovWave * currentWeight * Mathf.Abs(stunPulse)) + (cameraFovWave * 0.6f * shockWeight);
            targetFov += targetOffset;
        }

        if (impactWeight > 0.001f)
            targetFov += (impactBurstFovKick * impactWeight) + (impactBurstFovWave * impactWeight * (0.65f + (0.35f * Mathf.Abs(impactPulse))));

        float nextFov = Mathf.Lerp(worldCamera.fieldOfView, targetFov, Time.unscaledDeltaTime * cameraFovRecoverSpeed);
        if (!HasActivePresentation() && Mathf.Abs(nextFov - baseFieldOfView) <= 0.01f)
            nextFov = baseFieldOfView;

        worldCamera.fieldOfView = nextFov;
    }

    private void EnsureCameraReference()
    {
        Character character = GetComponent<Character>() ?? (playerStats != null ? playerStats.GetComponent<Character>() : null);
        Camera resolvedCamera = character != null ? character.GetCameraWorld() : Camera.main;
        if (resolvedCamera == null)
            return;

        if (resolvedCamera == worldCamera && hasBaseFieldOfView)
            return;

        worldCamera = resolvedCamera;
        baseFieldOfView = worldCamera.fieldOfView;
        hasBaseFieldOfView = true;
    }

    private float GetShockWeight()
    {
        if (shockDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(shockTimer / shockDuration);
    }

    private float GetPulse01()
    {
        return 0.5f + 0.5f * GetPulseSigned();
    }

    private float GetPulseSigned()
    {
        return Mathf.Sin(effectTime * pulseFrequency * Mathf.PI * 2f);
    }

    private float GetChargeLockPulse01()
    {
        return 0.5f + (0.5f * GetChargeLockPulseSigned());
    }

    private float GetChargeLockPulseSigned()
    {
        return Mathf.Sin(effectTime * chargeLockPulseFrequency * Mathf.PI * 2f);
    }

    private float GetImpactBurstWeight()
    {
        if (impactBurstDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(impactBurstTimer / impactBurstDuration);
    }

    private float GetImpactBurstPulse01()
    {
        return 0.5f + (0.5f * GetImpactBurstPulseSigned());
    }

    private float GetImpactBurstPulseSigned()
    {
        return Mathf.Sin(effectTime * impactBurstPulseFrequency * Mathf.PI * 2f);
    }

    private bool HasActivePresentation()
    {
        return currentWeight > 0.001f ||
               chargeLockWeight > 0.001f ||
               damageFlashWeight > 0.001f ||
               impactBurstTimer > 0.001f;
    }

    private void ApplyCompositeVignette(float warningAlpha, float stunAlpha, float impactAlpha, float impactWeight)
    {
        if (vignetteOverlay == null)
            return;

        float totalAlpha = Mathf.Clamp01(warningAlpha + stunAlpha + impactAlpha);
        if (totalAlpha <= 0.001f)
        {
            vignetteOverlay.color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        Color stunColor = new Color(0.06f, 0.01f, 0.01f, 1f);
        Color weightedColor =
            ((chargeLockColor * warningAlpha) +
             (stunColor * stunAlpha) +
             (impactBurstColor * (impactAlpha * Mathf.Lerp(0.75f, 1f, impactWeight)))) /
            Mathf.Max(0.0001f, warningAlpha + stunAlpha + impactAlpha);

        vignetteOverlay.color = new Color(weightedColor.r, weightedColor.g, weightedColor.b, totalAlpha);
    }

    private void PromoteDamageOverlay()
    {
        if (damageOverlay == null || damageOverlay.transform.parent == null)
            return;

        int desiredIndex = Mathf.Max(0, damageOverlay.transform.parent.childCount - 2);
        damageOverlay.transform.SetSiblingIndex(desiredIndex);
    }

    private void OnDisable()
    {
        UnsubscribePlayerEvents();
        ResetPresentation();
    }

    private void OnDestroy()
    {
        UnsubscribePlayerEvents();
        ResetPresentation();
    }

    private void ResetPresentation()
    {
        currentWeight = 0f;
        chargeLockWeight = 0f;
        damageFlashWeight = 0f;
        lingerTimer = 0f;
        shockTimer = 0f;
        impactBurstTimer = 0f;
        effectTime = 0f;

        if (vignetteRect != null)
            vignetteRect.localScale = new Vector3(vignetteOverscan, vignetteOverscan, 1f);

        if (worldCamera != null && hasBaseFieldOfView)
            worldCamera.fieldOfView = baseFieldOfView;

        if (grayOverlay != null)
            grayOverlay.color = new Color(0.5f, 0.5f, 0.5f, 0f);

        if (warningOverlay != null)
            warningOverlay.color = new Color(chargeLockColor.r, chargeLockColor.g, chargeLockColor.b, 0f);

        if (damageOverlay != null)
            damageOverlay.color = new Color(impactBurstColor.r, impactBurstColor.g, impactBurstColor.b, 0f);

        if (vignetteOverlay != null)
            vignetteOverlay.color = new Color(0f, 0f, 0f, 0f);
    }

    private void UnsubscribePlayerEvents()
    {
        if (subscribedPlayerStats == null)
            return;

        subscribedPlayerStats.OnStunStateChanged -= HandleStunStateChanged;
        subscribedPlayerStats.OnBullChargeLockStateChanged -= HandleBullChargeLockStateChanged;
        subscribedPlayerStats = null;
    }

    private void ApplyVignetteOverscan()
    {
        if (vignetteRect != null)
            vignetteRect.localScale = new Vector3(vignetteOverscan, vignetteOverscan, 1f);
    }

    private static void StretchFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

public class BullfightPerfectDodgeVfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Visual")]
    [SerializeField] private Color baseFlowColor = new Color(0.16f, 0.74f, 1f, 0.52f);
    [SerializeField] private Color accentFlowColor = new Color(1f, 0.86f, 0.22f, 0.34f);
    [SerializeField] private Color burstColor = new Color(0.7f, 0.94f, 1f, 0.68f);
    [SerializeField] private float fadeSpeed = 5.75f;
    [SerializeField] private float baseFlowScrollSpeed = 0.3f;
    [SerializeField] private float accentFlowScrollSpeed = 0.68f;
    [SerializeField] private float basePulseSpeed = 2.3f;
    [SerializeField] private float accentPulseSpeed = 4.1f;
    [SerializeField] private float burstPulseSpeed = 8.6f;
    [SerializeField] private float baseFlowAlpha = 0.58f;
    [SerializeField] private float accentFlowAlpha = 0.34f;
    [SerializeField] private float burstAlpha = 0.92f;
    [SerializeField] private float burstDuration = 0.24f;
    [SerializeField] private float overscan = 1.14f;
    [SerializeField] private float burstOverscan = 1.2f;

    [Header("Overlay Layout")]
    [SerializeField] private int overlaySortingOrder = 4900;
    [SerializeField] private Vector2 overlayReferenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float overlayMatchWidthOrHeight = 0.5f;
    private Canvas overlayCanvas;
    private RawImage baseFlowOverlay;
    private RawImage accentFlowOverlay;
    private RawImage activationBurstOverlay;
    private RectTransform baseFlowRect;
    private RectTransform accentFlowRect;
    private RectTransform activationBurstRect;
    private Texture2D baseFlowTexture;
    private Texture2D accentFlowTexture;
    private Texture2D burstTexture;
    private float currentWeight;
    private float burstTimer;
    private float baseScrollOffset;
    private float accentScrollOffset;
    private float basePulseTime;
    private float accentPulseTime;
    private float burstPulseTime;
    private bool buffActive;
    private PlayerStats subscribedPlayerStats;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureOverlay();
        SyncStateFromPlayer(true);
    }

    private void OnEnable()
    {
        ResolveReferencesIfNeeded();
        SyncStateFromPlayer(true);
    }

    private void Update()
    {
        if (playerStats == null || subscribedPlayerStats != playerStats)
            ResolveReferencesIfNeeded();

        currentWeight = Mathf.MoveTowards(currentWeight, buffActive ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        burstTimer = Mathf.Max(0f, burstTimer - Time.unscaledDeltaTime);
        if (currentWeight <= 0.001f && burstTimer <= 0.001f)
        {
            ApplyFlowState(0f, 0f, 0f, 0f, 0f);
            return;
        }

        baseScrollOffset += Time.unscaledDeltaTime * baseFlowScrollSpeed;
        accentScrollOffset += Time.unscaledDeltaTime * accentFlowScrollSpeed;
        basePulseTime += Time.unscaledDeltaTime * basePulseSpeed;
        accentPulseTime += Time.unscaledDeltaTime * accentPulseSpeed;
        burstPulseTime += Time.unscaledDeltaTime * burstPulseSpeed;

        float basePulse = 0.84f + (Mathf.Sin(basePulseTime * Mathf.PI * 2f) * 0.16f);
        float accentPulse = 0.72f + (Mathf.Sin(accentPulseTime * Mathf.PI * 2f) * 0.28f);
        float burstWeight = burstDuration <= 0f ? 0f : Mathf.Clamp01(burstTimer / burstDuration);
        float burstPulse = 0.84f + (Mathf.Sin(burstPulseTime * Mathf.PI * 2f) * 0.16f);

        ApplyFlowState(basePulse, accentPulse, burstPulse, baseScrollOffset, accentScrollOffset);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);

        if (subscribedPlayerStats != playerStats)
            ResubscribePlayerEvents();
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null)
            return;

        GameObject root = new GameObject("BullfightPerfectDodgeOverlay");
        root.transform.SetParent(transform, false);

        overlayCanvas = root.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = overlayReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = overlayMatchWidthOrHeight;

        root.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject baseFlow = new GameObject("PerfectDodgeBaseFlow");
        baseFlow.transform.SetParent(root.transform, false);
        baseFlowOverlay = baseFlow.AddComponent<RawImage>();
        baseFlowOverlay.raycastTarget = false;
        baseFlowOverlay.texture = CreateBaseFlowTexture();
        baseFlowOverlay.color = new Color(baseFlowColor.r, baseFlowColor.g, baseFlowColor.b, 0f);
        baseFlowRect = baseFlowOverlay.rectTransform;
        StretchOverlay(baseFlowRect);
        baseFlowRect.localScale = new Vector3(overscan, overscan, 1f);

        GameObject accentFlow = new GameObject("PerfectDodgeAccentFlow");
        accentFlow.transform.SetParent(root.transform, false);
        accentFlowOverlay = accentFlow.AddComponent<RawImage>();
        accentFlowOverlay.raycastTarget = false;
        accentFlowOverlay.texture = CreateAccentFlowTexture();
        accentFlowOverlay.color = new Color(accentFlowColor.r, accentFlowColor.g, accentFlowColor.b, 0f);
        accentFlowRect = accentFlowOverlay.rectTransform;
        StretchOverlay(accentFlowRect);
        accentFlowRect.localScale = new Vector3(overscan, overscan, 1f);

        GameObject burstFlow = new GameObject("PerfectDodgeActivationBurst");
        burstFlow.transform.SetParent(root.transform, false);
        activationBurstOverlay = burstFlow.AddComponent<RawImage>();
        activationBurstOverlay.raycastTarget = false;
        activationBurstOverlay.texture = CreateBurstTexture();
        activationBurstOverlay.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0f);
        activationBurstRect = activationBurstOverlay.rectTransform;
        StretchOverlay(activationBurstRect);
        activationBurstRect.localScale = new Vector3(burstOverscan, burstOverscan, 1f);
    }

    private void ResubscribePlayerEvents()
    {
        if (subscribedPlayerStats != null)
            subscribedPlayerStats.OnPerfectDodgeBuffStateChanged -= HandlePerfectDodgeBuffStateChanged;

        subscribedPlayerStats = playerStats;
        if (subscribedPlayerStats != null)
            subscribedPlayerStats.OnPerfectDodgeBuffStateChanged += HandlePerfectDodgeBuffStateChanged;

        SyncStateFromPlayer(false);
    }

    private void SyncStateFromPlayer(bool snapWeight)
    {
        buffActive = subscribedPlayerStats != null && subscribedPlayerStats.IsPerfectDodgeBuffActive;
        if (snapWeight)
            currentWeight = buffActive ? 1f : 0f;
    }

    private void HandlePerfectDodgeBuffStateChanged(bool active)
    {
        if (buffActive == active)
            return;

        buffActive = active;
        if (active)
            TriggerActivationBurst();
    }

    private void TriggerActivationBurst()
    {
        burstTimer = Mathf.Max(burstTimer, burstDuration);
        burstPulseTime = 0f;
        if (activationBurstOverlay != null)
            activationBurstOverlay.transform.SetAsLastSibling();
    }

    private void ApplyFlowState(float basePulse, float accentPulse, float burstPulse, float baseUvOffset, float accentUvOffset)
    {
        float burstWeight = burstDuration <= 0f ? 0f : Mathf.Clamp01(burstTimer / burstDuration);

        if (baseFlowOverlay != null)
        {
            baseFlowOverlay.uvRect = new Rect(0f, -baseUvOffset, 1f, 1.85f);
            baseFlowOverlay.color = new Color(baseFlowColor.r, baseFlowColor.g, baseFlowColor.b, baseFlowAlpha * currentWeight * basePulse);
        }

        if (accentFlowOverlay != null)
        {
            accentFlowOverlay.uvRect = new Rect(0f, -accentUvOffset, 1f, 2.05f);
            accentFlowOverlay.color = new Color(accentFlowColor.r, accentFlowColor.g, accentFlowColor.b, accentFlowAlpha * currentWeight * accentPulse);
        }

        if (activationBurstOverlay != null)
        {
            activationBurstOverlay.uvRect = new Rect(0f, -(baseUvOffset * 1.45f), 1f, 1.55f);
            activationBurstOverlay.color = new Color(burstColor.r, burstColor.g, burstColor.b, burstAlpha * burstWeight * burstPulse);
        }
    }

    private Texture2D CreateBaseFlowTexture()
    {
        if (baseFlowTexture != null)
            return baseFlowTexture;

        baseFlowTexture = CreateFlowTexture(256, 512, 8f, 17f, 0.36f, 0.65f, 0.48f);
        return baseFlowTexture;
    }

    private Texture2D CreateAccentFlowTexture()
    {
        if (accentFlowTexture != null)
            return accentFlowTexture;

        accentFlowTexture = CreateFlowTexture(256, 512, 13f, 31f, 0.48f, 0.95f, 0.72f);
        return accentFlowTexture;
    }

    private Texture2D CreateBurstTexture()
    {
        if (burstTexture != null)
            return burstTexture;

        const int width = 256;
        const int height = 512;
        burstTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < height; y++)
        {
            float vertical = y / (float)(height - 1);
            float bottomBias = Mathf.Pow(1f - vertical, 0.32f);
            float surge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.72f, vertical));
            for (int x = 0; x < width; x++)
            {
                float horizontal = x / (float)(width - 1);
                float beamA = Mathf.Abs(Mathf.Sin((horizontal * Mathf.PI * 9f) + (vertical * 5.2f)));
                float beamB = Mathf.Abs(Mathf.Sin((horizontal * Mathf.PI * 21f) - (vertical * 9.6f)));
                float beam = Mathf.Clamp01((beamA * 0.52f) + (beamB * 0.38f) - 0.26f);
                float alpha = beam * Mathf.Lerp(bottomBias, surge, 0.45f);
                burstTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        burstTexture.Apply();
        return burstTexture;
    }

    private static Texture2D CreateFlowTexture(int width, int height, float bandA, float bandB, float threshold, float bottomBiasExponent, float crestWeight)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < height; y++)
        {
            float vertical = y / (float)(height - 1);
            float bottomBias = Mathf.Pow(1f - vertical, bottomBiasExponent);
            float crest = Mathf.SmoothStep(0.08f, 1f, vertical);
            for (int x = 0; x < width; x++)
            {
                float horizontal = x / (float)(width - 1);
                float stripeA = Mathf.Abs(Mathf.Sin((horizontal * Mathf.PI * bandA) + (vertical * 7.4f)));
                float stripeB = Mathf.Abs(Mathf.Sin((horizontal * Mathf.PI * bandB) - (vertical * 11.1f)));
                float stripe = Mathf.Clamp01((stripeA * 0.5f) + (stripeB * 0.36f) - threshold);
                float alpha = stripe * Mathf.Clamp01((bottomBias * 0.72f) + (crest * crestWeight));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private void OnDisable()
    {
        UnsubscribePlayerEvents();
        ResetPresentation();
    }

    private void OnDestroy()
    {
        UnsubscribePlayerEvents();
        ResetPresentation();
    }

    private void ResetPresentation()
    {
        currentWeight = 0f;
        burstTimer = 0f;
        baseScrollOffset = 0f;
        accentScrollOffset = 0f;
        basePulseTime = 0f;
        accentPulseTime = 0f;
        burstPulseTime = 0f;
        ApplyFlowState(0f, 0f, 0f, 0f, 0f);

        if (baseFlowRect != null)
            baseFlowRect.localScale = new Vector3(overscan, overscan, 1f);

        if (accentFlowRect != null)
            accentFlowRect.localScale = new Vector3(overscan, overscan, 1f);

        if (activationBurstRect != null)
            activationBurstRect.localScale = new Vector3(burstOverscan, burstOverscan, 1f);
    }

    private void UnsubscribePlayerEvents()
    {
        if (subscribedPlayerStats == null)
            return;

        subscribedPlayerStats.OnPerfectDodgeBuffStateChanged -= HandlePerfectDodgeBuffStateChanged;
        subscribedPlayerStats = null;
    }

    private static void StretchOverlay(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }
}
