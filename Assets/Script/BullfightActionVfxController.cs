using System.Collections.Generic;
using UnityEngine;
using CartoonFX;

[DisallowMultipleComponent]
public sealed class BullfightActionVfxController : MonoBehaviour
{
    private const string ResourceRoot = "BullfightVfx/";
    private const string FlashResource = ResourceRoot + "CFXR Flash";
    private const string BullHitResource = ResourceRoot + "CFXR Hit A (Red)";
    private const string BullBloodResource = ResourceRoot + "CFXR2 Blood (Directional)";
    private const string GroundHitResource = ResourceRoot + "CFXR2 Ground Hit";
    private const string PerfectDodgeResource = ResourceRoot + "CFXR Impact Glowing HDR (Blue)";
    private const string SwordTrailResource = ResourceRoot + "CFXR4 Sword Trail PLAIN (360 Thin Spiral)";
    private const string SwordHitResource = ResourceRoot + "CFXR4 Sword Hit PLAIN (Cross)";

    private static readonly string[] BullImpactAnchorNames =
    {
        "Spine_04",
        "Spine_03",
        "Spine_05",
        "neck_01",
        "neck_02",
        "head"
    };

    private static BullfightActionVfxController activeInstance;

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private BullAI bullAI;

    [Header("Global Effect Safety")]
    [SerializeField] private bool disableJmoCameraShake = true;
    [SerializeField] private bool disableJmoLights = true;

    [Header("Player Effect Placement")]
    [SerializeField] private float dashForwardOffset = 0.55f;
    [SerializeField] private float dashHeightOffset = 0.1f;
    [SerializeField] private float perfectDodgeForwardOffset = 0.45f;
    [SerializeField] private float perfectDodgeHeightOffset = 1.1f;
    [SerializeField] private float throwForwardOffset = 0.08f;
    [SerializeField] private float throwHeightOffset = 0.03f;

    [Header("Bull Effect Placement")]
    [SerializeField] private float bullHitOutwardOffset = 0.08f;
    [SerializeField] private float bullHitHeightOffset = 0.06f;
    [SerializeField] private float bullGroundImpactForwardOffset = 0.8f;
    [SerializeField] private float bullGroundImpactProbeHeight = 1.8f;
    [SerializeField] private float bullGroundImpactFallbackHeight = 0.04f;

    [Header("Sword Effect Placement")]
    [SerializeField] private float swordTrailForwardOffset = 0.16f;
    [SerializeField] private float swordTrailHeightOffset = 0.03f;
    [SerializeField] private Vector3 swordTrailEulerOffset = Vector3.zero;
    [SerializeField] private float swordHitOutwardOffset = 0.12f;
    [SerializeField] private float swordHitHeightOffset = 0.06f;

    private readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private readonly HashSet<string> missingResourceWarnings = new HashSet<string>();
    private readonly List<Transform> cachedBullImpactAnchors = new List<Transform>();
    private PlayerStats subscribedPlayerStats;
    private BullAI subscribedBullAI;
    private Transform cachedBullAnchorRoot;
    private int nextBullAnchorIndex;

    private void Awake()
    {
        if (activeInstance == null || activeInstance == this)
            activeInstance = this;

        ApplyGlobalSafetyFlags();
        ResolveReferencesIfNeeded();
    }

    private void OnEnable()
    {
        if (activeInstance == null || activeInstance == this)
            activeInstance = this;

        ApplyGlobalSafetyFlags();
        ResolveReferencesIfNeeded();
        SubscribeIfNeeded();
    }

    private void Update()
    {
        ResolveReferencesIfNeeded();
        SubscribeIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
        if (activeInstance == this)
            activeInstance = null;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    public static void PlayBanderillasThrowVfx(Transform throwAnchor)
    {
        BullfightActionVfxController controller = GetActiveController();
        controller?.SpawnThrowFlash(throwAnchor);
    }

    public static void PlayBullChargeImpactVfx(Vector3 impactSource, Vector3 playerPosition)
    {
        BullfightActionVfxController controller = GetActiveController();
        controller?.SpawnBullGroundImpact(impactSource, playerPosition);
    }

    public static void PlayPhaseTwoSwordSwingVfx()
    {
        BullfightActionVfxController controller = GetActiveController();
        controller?.SpawnSwordTrail();
    }

    public static void PlayPhaseTwoSwordHitVfx()
    {
        BullfightActionVfxController controller = GetActiveController();
        controller?.SpawnSwordHit();
    }

    public static void PlayPerfectDodgeSuccessVfx()
    {
        BullfightActionVfxController controller = GetActiveController();
        controller?.SpawnPerfectDodgeSuccess();
    }

    private static BullfightActionVfxController GetActiveController()
    {
        if (activeInstance != null)
            return activeInstance;

        activeInstance = BullfightSceneCache.FindObject<BullfightActionVfxController>();
        return activeInstance;
    }

    private void ApplyGlobalSafetyFlags()
    {
        CFXR_Effect.GlobalDisableCameraShake = disableJmoCameraShake;
        CFXR_Effect.GlobalDisableLights = disableJmoLights;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>() ?? BullfightSceneCache.FindObject<PlayerStats>();

        BullAI resolvedBullAI = bullAI != null ? bullAI : BullfightSceneCache.FindObject<BullAI>();
        if (bullAI != resolvedBullAI)
        {
            bullAI = resolvedBullAI;
            RebuildBullAnchorCache();
        }
    }

    private void SubscribeIfNeeded()
    {
        if (playerStats != null && subscribedPlayerStats != playerStats)
        {
            if (subscribedPlayerStats != null)
            {
                subscribedPlayerStats.OnDashPerformed -= HandleDashPerformed;
            }

            playerStats.OnDashPerformed += HandleDashPerformed;
            subscribedPlayerStats = playerStats;
        }

        if (bullAI != null && subscribedBullAI != bullAI)
        {
            if (subscribedBullAI != null)
                subscribedBullAI.OnBanderillasHit -= HandleBullBanderillasHit;

            bullAI.OnBanderillasHit += HandleBullBanderillasHit;
            subscribedBullAI = bullAI;
            RebuildBullAnchorCache();
        }
    }

    private void UnsubscribeAll()
    {
        if (subscribedPlayerStats != null)
        {
            subscribedPlayerStats.OnDashPerformed -= HandleDashPerformed;
            subscribedPlayerStats = null;
        }

        if (subscribedBullAI != null)
        {
            subscribedBullAI.OnBanderillasHit -= HandleBullBanderillasHit;
            subscribedBullAI = null;
        }
    }

    private void HandleDashPerformed()
    {
        if (playerStats == null || playerStats.IsDead)
            return;

        Vector3 position = playerStats.transform.position + playerStats.transform.forward * dashForwardOffset + Vector3.up * dashHeightOffset;
        Quaternion rotation = Quaternion.LookRotation(GetHorizontalDirection(playerStats.transform.forward, Vector3.forward), Vector3.up);
        SpawnDetached(FlashResource, position, rotation);
    }

    private void SpawnPerfectDodgeSuccess()
    {
        if (playerStats == null || playerStats.IsDead)
            return;

        Vector3 forward = GetHorizontalDirection(playerStats.transform.forward, Vector3.forward);
        Vector3 position = playerStats.transform.position + forward * perfectDodgeForwardOffset + Vector3.up * perfectDodgeHeightOffset;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        SpawnDetached(PerfectDodgeResource, position, rotation);
    }

    private void HandleBullBanderillasHit(float _)
    {
        if (bullAI == null)
            return;

        Transform anchor = GetNextBullImpactAnchor();
        if (anchor == null)
            return;

        Vector3 outward = GetBullOutwardDirection(anchor);
        Vector3 position = anchor.position + outward * bullHitOutwardOffset + Vector3.up * bullHitHeightOffset;
        Quaternion rotation = Quaternion.LookRotation(outward, Vector3.up);
        SpawnDetached(BullHitResource, position, rotation);
        SpawnAttached(BullBloodResource, anchor, position, rotation);
    }

    private void SpawnThrowFlash(Transform throwAnchor)
    {
        Transform anchor = throwAnchor != null ? throwAnchor : FindPlayerAnchor("hand_r");
        if (anchor == null)
            return;

        Vector3 forward = GetHorizontalDirection(anchor.forward, playerStats != null ? playerStats.transform.forward : transform.forward);
        Vector3 position = anchor.position + forward * throwForwardOffset + Vector3.up * throwHeightOffset;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        SpawnDetached(FlashResource, position, rotation);
    }

    private void SpawnBullGroundImpact(Vector3 impactSource, Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - impactSource;
        direction = GetHorizontalDirection(direction, bullAI != null ? bullAI.transform.forward : Vector3.forward);
        Vector3 position = impactSource + direction * bullGroundImpactForwardOffset;
        position = ProjectToGround(position, bullGroundImpactProbeHeight, bullGroundImpactFallbackHeight);
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        SpawnDetached(GroundHitResource, position, rotation);
    }

    private void SpawnSwordTrail()
    {
        Transform swordAnchor = FindPlayerAnchor("Rapier_lowpoly") ?? FindPlayerAnchor("hand_r");
        if (swordAnchor == null)
            return;

        Vector3 forward = GetHorizontalDirection(swordAnchor.forward, playerStats != null ? playerStats.transform.forward : transform.forward);
        Vector3 position = swordAnchor.position + forward * swordTrailForwardOffset + Vector3.up * swordTrailHeightOffset;
        Quaternion rotation = swordAnchor.rotation * Quaternion.Euler(swordTrailEulerOffset);
        SpawnDetached(SwordTrailResource, position, rotation);
    }

    private void SpawnSwordHit()
    {
        Transform anchor = GetNextBullImpactAnchor();
        if (anchor == null)
            return;

        Vector3 outward = GetBullOutwardDirection(anchor);
        Vector3 position = anchor.position + outward * swordHitOutwardOffset + Vector3.up * swordHitHeightOffset;
        Quaternion rotation = Quaternion.LookRotation(outward, Vector3.up);
        SpawnDetached(SwordHitResource, position, rotation);
    }

    private void SpawnDetached(string resourcePath, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = LoadPrefab(resourcePath);
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, position, rotation);
        PrepareInstance(instance);
    }

    private void SpawnAttached(string resourcePath, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = LoadPrefab(resourcePath);
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        PrepareInstance(instance);
    }

    private void PrepareInstance(GameObject instance)
    {
        if (instance == null)
            return;

        if (disableJmoLights)
        {
            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].enabled = false;
            }
        }

        CFXR_Effect[] effects = instance.GetComponentsInChildren<CFXR_Effect>(true);
        for (int i = 0; i < effects.Length; i++)
        {
            CFXR_Effect effect = effects[i];
            if (effect == null)
                continue;

            if (disableJmoCameraShake && effect.cameraShake != null)
                effect.cameraShake.enabled = false;
        }
    }

    private GameObject LoadPrefab(string resourcePath)
    {
        if (prefabCache.TryGetValue(resourcePath, out GameObject cachedPrefab) && cachedPrefab != null)
            return cachedPrefab;

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            if (missingResourceWarnings.Add(resourcePath))
                Debug.LogWarning($"Missing VFX resource at '{resourcePath}'.");
            return null;
        }

        prefabCache[resourcePath] = prefab;
        return prefab;
    }

    private Transform FindPlayerAnchor(string anchorName)
    {
        if (playerStats == null || string.IsNullOrWhiteSpace(anchorName))
            return null;

        return FindChildRecursive(playerStats.transform, anchorName);
    }

    private void RebuildBullAnchorCache()
    {
        cachedBullImpactAnchors.Clear();
        cachedBullAnchorRoot = bullAI != null ? bullAI.transform : null;
        nextBullAnchorIndex = 0;

        if (cachedBullAnchorRoot == null)
            return;

        for (int i = 0; i < BullImpactAnchorNames.Length; i++)
        {
            Transform anchor = FindChildRecursive(cachedBullAnchorRoot, BullImpactAnchorNames[i]);
            if (anchor != null && !cachedBullImpactAnchors.Contains(anchor))
                cachedBullImpactAnchors.Add(anchor);
        }

        if (cachedBullImpactAnchors.Count == 0)
            cachedBullImpactAnchors.Add(cachedBullAnchorRoot);
    }

    private Transform GetNextBullImpactAnchor()
    {
        if (bullAI == null)
            return null;

        if (cachedBullAnchorRoot != bullAI.transform || cachedBullImpactAnchors.Count == 0)
            RebuildBullAnchorCache();

        if (cachedBullImpactAnchors.Count == 0)
            return bullAI.transform;

        Transform anchor = cachedBullImpactAnchors[nextBullAnchorIndex % cachedBullImpactAnchors.Count];
        nextBullAnchorIndex++;
        return anchor != null ? anchor : bullAI.transform;
    }

    private Vector3 GetBullOutwardDirection(Transform anchor)
    {
        if (anchor == null || bullAI == null)
            return Vector3.forward;

        Vector3 outward = anchor.position - bullAI.transform.position;
        return GetHorizontalDirection(outward, bullAI.transform.forward);
    }

    private static Vector3 GetHorizontalDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = fallback;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    private static Vector3 ProjectToGround(Vector3 position, float probeHeight, float fallbackHeight)
    {
        Vector3 origin = position + Vector3.up * Mathf.Max(0.2f, probeHeight);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        else
            position.y += fallbackHeight;

        return position;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == targetName)
                return children[i];
        }

        return null;
    }
}
