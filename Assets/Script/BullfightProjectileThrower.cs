using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class BullfightProjectileThrower : MonoBehaviour
{
    private const string DefaultProjectilePrefabPath = "Assets/PolyRonin/Viking Warrior Weapons Pack/Prefabs/SeaxKnife.prefab";

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private BullfightPlayerController playerController;
    [SerializeField] private BullAI bullAI;
    [SerializeField] private BullStats bullStats;
    [SerializeField] private BullfightGameFlow gameFlow;
    [SerializeField] private BullfightCapeBinder capeBinder;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnAnchor;

    [Header("Detection")]
    [SerializeField] private string projectileSpawnAnchorNameHint = "hand_r";

    [Header("Throw Timing")]
    [SerializeField] private float throwSpawnNormalizedTime = 0.55f;
    [SerializeField] private float phaseOneDamageDelay = 0.25f;

    [Header("Throw Tuning")]
    [SerializeField] private Vector3 spawnLocalOffset = new Vector3(0.12f, 0f, 0.02f);
    [SerializeField] private float projectileSpeed = 16f;
    [SerializeField] private float upwardBias = 0.05f;
    [SerializeField] private float phaseOneVisualLifetime = 0.45f;
    [SerializeField] private Vector3 projectilePrefabLocalEulerOffset = Vector3.zero;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private bool usePlaceholderWhenMissing = true;

    private bool pendingThrow;
    private bool pendingPhaseOneDamage;
    private float phaseOneDamageResolveAt = -1f;
    private bool defaultProjectilePrefabResolved;
    private PlayerStats subscribedPlayerStats;
    private BullAI syncedDamageBullAI;

    public float ThrowSpawnNormalizedTime => throwSpawnNormalizedTime;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureProjectileSpawnAnchor();
    }

    private void Update()
    {
        if (subscribedPlayerStats != playerStats && playerStats != null)
            Subscribe();

        if (!pendingPhaseOneDamage || Time.time < phaseOneDamageResolveAt)
            return;

        pendingPhaseOneDamage = false;
        phaseOneDamageResolveAt = -1f;
        ResolvePhaseOneDamage();
    }

    private void OnEnable()
    {
        ResolveReferencesIfNeeded();
        EnsureProjectileSpawnAnchor();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();

        pendingPhaseOneDamage = false;
        phaseOneDamageResolveAt = -1f;
    }

    public void NotifyThrowAnimationReachedFrame()
    {
        if (!pendingThrow)
            return;

        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (IsPhaseOneDirectDamageActive())
        {
            pendingThrow = false;
            return;
        }

        EnsureProjectileSpawnAnchor();
        SpawnProjectile();
        pendingThrow = false;
    }

    private void HandleBanderillasPerformed()
    {
        pendingThrow = true;
        if (IsPhaseOneDirectDamageActive())
        {
            pendingPhaseOneDamage = true;
            phaseOneDamageResolveAt = Time.time + phaseOneDamageDelay;
        }
    }

    private void Subscribe()
    {
        if (playerStats == null)
            return;

        if (subscribedPlayerStats == playerStats)
            return;

        Unsubscribe();
        playerStats.OnBanderillasPerformed += HandleBanderillasPerformed;
        subscribedPlayerStats = playerStats;
    }

    private void Unsubscribe()
    {
        if (subscribedPlayerStats == null)
            return;

        subscribedPlayerStats.OnBanderillasPerformed -= HandleBanderillasPerformed;
        subscribedPlayerStats = null;
    }

    private bool HasMissingReferences()
    {
        return playerStats == null ||
               playerController == null ||
               bullAI == null ||
               bullStats == null ||
               gameFlow == null;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>() ?? BullfightSceneCache.FindObject<PlayerStats>();

        if (playerController == null)
            playerController = GetComponent<BullfightPlayerController>() ?? BullfightSceneCache.FindObject<BullfightPlayerController>();

        if (bullAI == null)
            bullAI = BullfightSceneCache.FindObject<BullAI>();

        if (bullStats == null)
            bullStats = bullAI != null ? bullAI.bullStats : BullfightSceneCache.FindObject<BullStats>();

        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        if (capeBinder == null)
            capeBinder = GetComponent<BullfightCapeBinder>() ?? BullfightSceneCache.GetLocalOrScene<BullfightCapeBinder>(this);

        ResolveProjectilePrefabIfNeeded();
        SyncProjectileDamageFromBull();

        if (isActiveAndEnabled && playerStats != null && subscribedPlayerStats != playerStats)
            Subscribe();
    }

    private void EnsureProjectileSpawnAnchor()
    {
        if (projectileSpawnAnchor != null)
            return;

        projectileSpawnAnchor = FindChildRecursive(transform, projectileSpawnAnchorNameHint) ?? FindChildRecursive(transform, "hand_l");
    }

    private void SyncProjectileDamageFromBull()
    {
        if (bullAI == null || bullAI == syncedDamageBullAI || bullAI.banderillasDamage <= 0f)
            return;

        projectileDamage = bullAI.banderillasDamage;
        syncedDamageBullAI = bullAI;
    }

    private void SpawnProjectile()
    {
        if (projectileSpawnAnchor == null)
            return;

        Vector3 spawnPosition = projectileSpawnAnchor.TransformPoint(spawnLocalOffset);
        Vector3 direction = ResolveThrowDirection(spawnPosition);
        Quaternion spawnRotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : projectileSpawnAnchor.rotation;
        ResolveProjectilePrefabIfNeeded();
        bool usingProjectilePrefab = projectilePrefab != null;
        Quaternion projectileRotation = usingProjectilePrefab
            ? spawnRotation * Quaternion.Euler(projectilePrefabLocalEulerOffset)
            : spawnRotation;
        GameObject projectile = usingProjectilePrefab
            ? Instantiate(projectilePrefab, spawnPosition, projectileRotation)
            : (usePlaceholderWhenMissing ? CreatePlaceholderProjectile(spawnPosition, spawnRotation) : null);

        if (projectile == null)
            return;

        projectile.name = "BullfightThrownProjectile";

        Rigidbody rigidbody = projectile.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = projectile.AddComponent<Rigidbody>();

        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        projectile.transform.SetPositionAndRotation(spawnPosition, projectileRotation);
        rigidbody.velocity = direction * projectileSpeed;

        IgnoreOwnerCollisions(projectile);

        BullfightThrownProjectile thrownProjectile = projectile.GetComponent<BullfightThrownProjectile>();
        if (thrownProjectile == null)
            thrownProjectile = projectile.AddComponent<BullfightThrownProjectile>();

        bool phaseOneVisualOnly = IsPhaseOneDirectDamageActive();
        bool enableProjectileDamage = !phaseOneVisualOnly;
        thrownProjectile.Configure(bullAI, bullStats, projectileDamage, enableProjectileDamage);

        if (phaseOneVisualOnly)
        {
            SetProjectileCollidersEnabled(projectile, false);
            Destroy(projectile, Mathf.Max(0.05f, phaseOneVisualLifetime));
        }
    }

    private void ResolveProjectilePrefabIfNeeded()
    {
        if (projectilePrefab != null || defaultProjectilePrefabResolved)
            return;

        defaultProjectilePrefabResolved = true;

#if UNITY_EDITOR
        projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePrefabPath);
#endif
    }

    private void ResolvePhaseOneDamage()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (!IsPhaseOneDirectDamageActive() || bullAI == null || bullStats == null)
            return;

        if (bullStats.currentHealth <= 0f)
            return;

        if (bullAI.CurrentDistanceToPlayer > bullAI.banderillasRange)
            return;

        bullAI.RegisterBanderillasHit(projectileDamage);
    }

    private bool IsPhaseOneDirectDamageActive()
    {
        return gameFlow == null ||
               gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne ||
               gameFlow.IsTutorialAttackStepActive;
    }

    private Vector3 ResolveThrowDirection(Vector3 spawnPosition)
    {
        Transform bullTarget = playerController != null ? playerController.GetBullTarget() : null;
        if (bullTarget != null)
        {
            Vector3 toTarget = bullTarget.position - spawnPosition;
            toTarget.y += upwardBias;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        Transform reference = Camera.main != null ? Camera.main.transform : projectileSpawnAnchor;
        Vector3 fallback = reference != null ? reference.forward : transform.forward;
        fallback.y += upwardBias;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private GameObject CreatePlaceholderProjectile(Vector3 position, Quaternion rotation)
    {
        GameObject placeholder = new GameObject("BullfightThrownProjectileFallback");
        placeholder.transform.SetPositionAndRotation(position, rotation);

        // Preserve the old collision footprint; only the readable silhouette gets larger.
        CapsuleCollider collider = placeholder.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.radius = 0.035f;
        collider.height = 0.42f;

        CreateVisualPrimitive(
            placeholder.transform,
            PrimitiveType.Cylinder,
            "BanderillaShaft",
            Vector3.zero,
            new Vector3(90f, 0f, 0f),
            new Vector3(0.018f, 0.18f, 0.018f),
            new Color(0.46f, 0.29f, 0.12f, 1f));
        CreateVisualPrimitive(
            placeholder.transform,
            PrimitiveType.Cylinder,
            "BanderillaWrap",
            new Vector3(0f, 0f, -0.07f),
            new Vector3(90f, 0f, 0f),
            new Vector3(0.028f, 0.028f, 0.028f),
            new Color(0.96f, 0.82f, 0.24f, 1f));
        CreateVisualPrimitive(
            placeholder.transform,
            PrimitiveType.Capsule,
            "BanderillaTip",
            new Vector3(0f, 0f, 0.2f),
            new Vector3(90f, 0f, 0f),
            new Vector3(0.016f, 0.05f, 0.016f),
            new Color(0.76f, 0.76f, 0.74f, 1f));

        if (!TryAttachCapeClothVisual(placeholder.transform))
        {
            CreateVisualPrimitive(
                placeholder.transform,
                PrimitiveType.Cube,
                "BanderillaRibbonA",
                new Vector3(0.048f, 0.004f, -0.1f),
                new Vector3(10f, 18f, 28f),
                new Vector3(0.09f, 0.008f, 0.18f),
                new Color(0.78f, 0.12f, 0.1f, 1f));
            CreateVisualPrimitive(
                placeholder.transform,
                PrimitiveType.Cube,
                "BanderillaRibbonB",
                new Vector3(-0.045f, -0.008f, -0.1f),
                new Vector3(-8f, -20f, -30f),
                new Vector3(0.085f, 0.008f, 0.16f),
                new Color(0.96f, 0.82f, 0.24f, 1f));
        }

        return placeholder;
    }

    private bool TryAttachCapeClothVisual(Transform parent)
    {
        GameObject clothSource = null;
        if (capeBinder != null)
        {
            clothSource = capeBinder.capePrefab != null
                ? capeBinder.capePrefab
                : capeBinder.CapeInstance != null ? capeBinder.CapeInstance.gameObject : null;
        }

        if (clothSource == null)
            return false;

        GameObject cloth = Instantiate(clothSource, parent);
        cloth.name = "BanderillaCloth";
        cloth.SetActive(true);
        cloth.transform.localPosition = new Vector3(0.03f, -0.02f, -0.08f);
        cloth.transform.localRotation = Quaternion.Euler(14f, -76f, -58f);
        cloth.transform.localScale = Vector3.one * 0.18f;

        foreach (Collider existingCollider in cloth.GetComponentsInChildren<Collider>(true))
        {
            if (existingCollider == null)
                continue;

            existingCollider.enabled = false;
            Destroy(existingCollider);
        }

        foreach (Rigidbody existingBody in cloth.GetComponentsInChildren<Rigidbody>(true))
        {
            if (existingBody == null)
                continue;

            existingBody.isKinematic = true;
            Destroy(existingBody);
        }

        foreach (Animator animator in cloth.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null)
                animator.enabled = false;
        }

        return true;
    }

    private static void CreateVisualPrimitive(
        Transform parent,
        PrimitiveType primitiveType,
        string name,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.Euler(localEulerAngles);
        visual.transform.localScale = localScale;

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material material = renderer.material;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetProjectileCollidersEnabled(GameObject projectile, bool enabled)
    {
        if (projectile == null)
            return;

        foreach (Collider collider in projectile.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void IgnoreOwnerCollisions(GameObject projectile)
    {
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>(true);
        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider projectileCollider in projectileColliders)
        {
            if (projectileCollider == null)
                continue;

            foreach (Collider ownerCollider in ownerColliders)
            {
                if (ownerCollider == null)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }
}
