using UnityEngine;
using UnityEngine.Serialization;

public class BullfightSpawnManager : MonoBehaviour
{
    [Header("Names")]
    public string arenaRootName = "";
    public string arenaBoundaryName = "Pipe";
    public string playerSpawnName = "PlayerSpawnPoint";
    public string bullSpawnName = "BullSpawnPoint";

    [Header("Spawn")]
    public float defaultBullSpawnDistance = 4.2f;
    public float defaultBullSpawnSideOffset = 0f;
    public float groundProbeHeight = 12f;
    public float fallThresholdY = -2f;
    public float resetHeightOffset = 0.1f;
    public float bullResetHeightOffset = 0.02f;

    [Header("Arena Safety")]
    public float floorThicknessExtra = 4f;
    public float wallPaddingExtra = 0.5f;
    public float wallThicknessExtra = 1.5f;
    [FormerlySerializedAs("arenaRadius")]
    public float bullArenaRadius = 4.1f;
    public float arenaEdgePadding = 0.25f;
    public float spawnEdgePadding = 0.25f;
    public float boundaryBounceSpeed = 4.5f;

    public PlayerStats playerStats;
    public BullAI bullAI;

    private Rigidbody playerRigidbody;
    private Rigidbody bullRigidbody;
    private CapsuleCollider playerCapsuleCollider;
    private Transform playerSpawnPoint;
    private Transform bullSpawnPoint;
    private Collider arenaFloorCollider;
    private Collider arenaBoundaryCollider;
    private Vector3 arenaWorldCenter;
    private bool arenaAdjusted;
    private bool initialSpawnApplied;

    public Vector3 ArenaCenter => (arenaFloorCollider != null || arenaBoundaryCollider != null) ? arenaWorldCenter : (playerSpawnPoint != null ? playerSpawnPoint.position : transform.position);
    public float ArenaRadius => bullArenaRadius;
    public Transform BullSpawnPoint => bullSpawnPoint;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        playerRigidbody = GetComponent<Rigidbody>();
        AdjustArenaColliders();
        CacheArenaBounds();
        EnsureSpawnPoints();
        CacheArenaBounds();
    }

    private void Start()
    {
        ResolveReferencesIfNeeded();
        EnsureSpawnPoints();
        CacheArenaBounds();
        ApplyInitialSpawn();
    }

    private void LateUpdate()
    {
        if (playerStats == null || bullAI == null || bullRigidbody == null)
            ResolveReferencesIfNeeded();

        if (!initialSpawnApplied)
            ApplyInitialSpawn();

        if (transform.position.y < fallThresholdY)
            ResetPlayerToSpawn();

        if (bullAI != null && bullAI.transform.position.y < fallThresholdY)
            ResetBullToSpawn();
    }

    public void ResetPlayerToSpawn()
    {
        if (playerSpawnPoint == null)
            EnsureSpawnPoints();

        if (playerSpawnPoint == null)
            return;

        Vector3 targetPosition = playerSpawnPoint.position + Vector3.up * resetHeightOffset;
        Quaternion targetRotation = playerSpawnPoint.rotation;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.position = targetPosition;
            playerRigidbody.rotation = targetRotation;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    public bool KeepPlayerInsideArena(float overflowTolerance = 0.35f, float snapPadding = 0.1f)
    {
        Vector3 currentPosition = playerRigidbody != null && !playerRigidbody.isKinematic
            ? playerRigidbody.position
            : transform.position;

        if (!TryGetPlayerWallPadding(snapPadding, out float wallPadding))
            wallPadding = Mathf.Max(0.05f, snapPadding);

        if (!TryClampPlayerPositionToBoundary(currentPosition, wallPadding, out Vector3 correctedPosition, out Vector3 outwardDirection))
            return false;

        BullfightPlayerController controller = playerStats != null ? playerStats.GetComponent<BullfightPlayerController>() : null;
        controller?.ClearInputBuffers();
        controller?.ForceStopMovement();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (playerRigidbody != null)
        {
            Vector3 velocity = Vector3.Project(playerRigidbody.velocity, Vector3.up);
            velocity += (-outwardDirection) * Mathf.Max(0f, boundaryBounceSpeed);
            playerRigidbody.velocity = velocity;
            playerRigidbody.position = correctedPosition;
        }

        transform.position = correctedPosition;
        return true;
    }

    public bool ConstrainPlayerMotionDelta(Vector3 currentPosition, Vector3 requestedDelta, bool allowWallSlide, float skinWidth, out Vector3 constrainedDelta)
    {
        constrainedDelta = requestedDelta;
        if (requestedDelta.sqrMagnitude <= 0.000001f)
            return false;

        ResolveReferencesIfNeeded();
        if (arenaBoundaryCollider == null)
            CacheArenaBounds();

        if (!TryGetPlayerWallPadding(skinWidth, out float wallPadding))
            return false;

        Vector3 requestedTarget = currentPosition + requestedDelta;
        if (!TryClampPlayerPositionToBoundary(requestedTarget, wallPadding, out Vector3 clampedTarget, out Vector3 outwardDirection))
            return false;

        if (allowWallSlide)
        {
            Vector3 slideDelta = Vector3.ProjectOnPlane(requestedDelta, outwardDirection);
            if (slideDelta.sqrMagnitude > 0.000001f)
            {
                Vector3 slideTarget = currentPosition + slideDelta;
                if (!TryClampPlayerPositionToBoundary(slideTarget, wallPadding, out _, out _))
                {
                    constrainedDelta = slideDelta;
                    return true;
                }
            }
        }

        constrainedDelta = clampedTarget - currentPosition;
        return true;
    }

    public bool ResolvePlayerWallOverlap(float extraBuffer = 0.02f, float inwardBias = 0.02f)
    {
        ResolveReferencesIfNeeded();
        if (arenaBoundaryCollider == null)
            CacheArenaBounds();

        if (arenaBoundaryCollider == null)
            return false;

        if (playerCapsuleCollider == null)
            playerCapsuleCollider = GetComponent<CapsuleCollider>();

        if (playerCapsuleCollider == null || !playerCapsuleCollider.enabled)
            return false;

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        Vector3 currentPosition = playerRigidbody != null && !playerRigidbody.isKinematic
            ? playerRigidbody.position
            : transform.position;
        Quaternion currentRotation = playerRigidbody != null && !playerRigidbody.isKinematic
            ? playerRigidbody.rotation
            : transform.rotation;

        bool moved = false;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            if (!Physics.ComputePenetration(
                    arenaBoundaryCollider,
                    arenaBoundaryCollider.transform.position,
                    arenaBoundaryCollider.transform.rotation,
                    playerCapsuleCollider,
                    currentPosition,
                    currentRotation,
                    out Vector3 separationDirection,
                    out float separationDistance))
            {
                break;
            }

            Vector3 inwardDirection = GetArenaInwardDirection(currentPosition);
            Vector3 correctionDirection = -separationDirection;
            correctionDirection.y = 0f;
            if (correctionDirection.sqrMagnitude <= 0.0001f)
            {
                correctionDirection = inwardDirection;
            }
            else
            {
                correctionDirection.Normalize();
                if (Vector3.Dot(correctionDirection, inwardDirection) < 0f)
                    correctionDirection = inwardDirection;
                else
                    correctionDirection = (correctionDirection + inwardDirection * 0.35f).normalized;
            }

            currentPosition += correctionDirection * Mathf.Max(0.01f, separationDistance + Mathf.Max(0f, extraBuffer));
            moved = true;
        }

        if (TryGetPlayerWallPadding(extraBuffer, out float wallPadding) &&
            TryClampPlayerPositionToBoundary(currentPosition, wallPadding + Mathf.Max(0f, inwardBias), out Vector3 clampedPosition, out _))
        {
            currentPosition = clampedPosition;
            moved = true;
        }

        if (!moved)
            return false;

        if (playerRigidbody != null && !playerRigidbody.isKinematic)
            playerRigidbody.position = currentPosition;

        transform.position = currentPosition;
        return true;
    }

    public void ResetBullToSpawn()
    {
        if (bullAI == null)
            return;

        if (bullSpawnPoint == null)
            EnsureSpawnPoints();

        if (bullSpawnPoint == null)
            return;

        if (bullRigidbody == null)
            bullRigidbody = bullAI.GetComponent<Rigidbody>();

        Vector3 groundedSpawn = SampleGround(bullSpawnPoint.position, bullAI.transform);
        bullSpawnPoint.position = groundedSpawn;

        float bullLift = bullResetHeightOffset > 0f ? bullResetHeightOffset : 0.02f;
        Vector3 targetPosition = groundedSpawn + Vector3.up * Mathf.Clamp(bullLift, 0f, 0.08f);
        Quaternion targetRotation = bullSpawnPoint.rotation;

        if (bullRigidbody != null)
        {
            if (!bullRigidbody.isKinematic)
            {
                bullRigidbody.velocity = Vector3.zero;
                bullRigidbody.angularVelocity = Vector3.zero;
            }

            bullRigidbody.position = targetPosition;
            bullRigidbody.rotation = targetRotation;
        }

        bullAI.transform.SetPositionAndRotation(targetPosition, targetRotation);
        bullAI.ResetCombatState();
        bullAI.ForceSnapToGround();
    }

    public void ResetBullForPhaseTwoRound(float frontDistance = 2.2f, float sideOffset = 0f)
    {
        if (bullAI == null)
            return;

        EnsureSpawnPoints();
        if (bullSpawnPoint == null)
            return;

        Vector3 forward = GetViewForward();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        Vector3 playerOrigin = transform.position;
        float clampedDistance = Mathf.Max(1.25f, frontDistance);
        Vector3 candidate = playerOrigin + forward * clampedDistance + right * sideOffset;
        candidate = ClampToArena(candidate, spawnEdgePadding);
        candidate = SampleGround(candidate, bullAI.transform);

        Vector3 lookDirection = playerOrigin - candidate;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = -forward;

        bullSpawnPoint.position = candidate;
        bullSpawnPoint.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        ResetBullToSpawn();
    }

    public void ResetPlayerAndBullForTutorial(float bullFrontDistance, float bullSideOffset = 0f)
    {
        EnsureSpawnPoints();
        ResetPlayerToSpawn();

        if (bullAI == null || playerSpawnPoint == null || bullSpawnPoint == null)
            return;

        Vector3 forward = playerSpawnPoint.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        Vector3 candidate = playerSpawnPoint.position + forward * Mathf.Max(1.25f, bullFrontDistance) + right * bullSideOffset;
        candidate = ClampToArena(candidate, spawnEdgePadding);
        candidate = SampleGround(candidate, bullAI.transform);

        bullSpawnPoint.position = candidate;
        bullSpawnPoint.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        ResetBullToSpawn();
    }

    private void ApplyInitialSpawn()
    {
        EnsureSpawnPoints();
        ResetPlayerToSpawn();
        if (bullAI != null)
            ResetBullToSpawn();

        initialSpawnApplied = bullAI != null && playerSpawnPoint != null && bullSpawnPoint != null;
    }

    private void EnsureSpawnPoints()
    {
        if (playerSpawnPoint == null)
            playerSpawnPoint = BullfightSceneCache.FindSceneObjectByName<Transform>(playerSpawnName);

        if (bullSpawnPoint == null)
            bullSpawnPoint = BullfightSceneCache.FindSceneObjectByName<Transform>(bullSpawnName);

        if (playerSpawnPoint == null)
        {
            GameObject anchor = new GameObject(playerSpawnName);
            anchor.transform.position = SampleGround(transform.position, transform);
            anchor.transform.rotation = transform.rotation;
            playerSpawnPoint = anchor.transform;
        }

        if (bullSpawnPoint == null)
        {
            GameObject anchor = new GameObject(bullSpawnName);
            Vector3 forward = GetViewForward();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float spawnDistance = Mathf.Min(defaultBullSpawnDistance, Mathf.Max(2f, bullArenaRadius - spawnEdgePadding));
            Vector3 candidate = playerSpawnPoint.position + forward * spawnDistance + right * defaultBullSpawnSideOffset;
            candidate = ClampToArena(candidate, spawnEdgePadding);
            anchor.transform.position = SampleGround(candidate, bullAI != null ? bullAI.transform : null);
            anchor.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            bullSpawnPoint = anchor.transform;
        }
        else
        {
            Vector3 clampedBullSpawn = ClampToArena(bullSpawnPoint.position, spawnEdgePadding);
            bullSpawnPoint.position = SampleGround(clampedBullSpawn, bullAI != null ? bullAI.transform : null);
        }
    }

    private void CacheArenaBounds()
    {
        Vector3 referencePosition = playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        arenaBoundaryCollider = FindArenaBoundaryCollider(referencePosition);
        arenaFloorCollider = FindLargestArenaFloorCollider(referencePosition);

        arenaWorldCenter = arenaBoundaryCollider != null
            ? arenaBoundaryCollider.bounds.center
            : arenaFloorCollider != null
                ? arenaFloorCollider.bounds.center
                : referencePosition;
        arenaWorldCenter.y = referencePosition.y;

        float derivedRadius = bullArenaRadius;
        if (arenaBoundaryCollider != null)
        {
            float sampledRadius = SampleArenaBoundaryRadius(referencePosition);
            if (sampledRadius > 0f)
                derivedRadius = sampledRadius - arenaEdgePadding;
            else
            {
                Bounds bounds = arenaBoundaryCollider.bounds;
                derivedRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) - arenaEdgePadding;
            }
        }
        else if (arenaFloorCollider != null)
        {
            Bounds bounds = arenaFloorCollider.bounds;
            derivedRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) - arenaEdgePadding;
            derivedRadius = Mathf.Min(derivedRadius, bullArenaRadius);
        }

        bullArenaRadius = Mathf.Max(1.5f, derivedRadius);
    }

    private void AdjustArenaColliders()
    {
        if (arenaAdjusted)
            return;

        GameObject arenaRoot = FindArenaRoot();
        if (arenaRoot == null)
            return;

        BoxCollider[] colliders = arenaRoot.GetComponentsInChildren<BoxCollider>(false);
        foreach (BoxCollider box in colliders)
        {
            Vector3 size = box.size;
            Vector3 center = box.center;

            if (size.y <= 1.25f)
            {
                size.y += floorThicknessExtra;
                center.y -= floorThicknessExtra * 0.5f;
            }
            else
            {
                size.x += wallPaddingExtra;
                size.z += wallPaddingExtra;
                size.y += wallThicknessExtra;
            }

            box.size = size;
            box.center = center;
        }

        arenaAdjusted = true;
    }

    private GameObject FindArenaRoot()
    {
        GameObject arenaRoot = string.IsNullOrWhiteSpace(arenaRootName)
            ? null
            : BullfightSceneCache.FindSceneObjectByName<GameObject>(arenaRootName);
        if (arenaRoot != null)
            return arenaRoot;

        GameObject best = null;
        int bestScore = 0;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            int score = root.GetComponentsInChildren<BoxCollider>(false).Length;
            if (score > bestScore)
            {
                best = root;
                bestScore = score;
            }
        }

        return best;
    }

    private Collider FindLargestArenaFloorCollider(Vector3 referencePosition)
    {
        Collider best = null;
        float bestArea = -1f;

        Collider[] colliders = Resources.FindObjectsOfTypeAll<Collider>();
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider collider = colliders[index];
            if (!IsArenaCandidateCollider(collider))
                continue;

            Bounds bounds = collider.bounds;
            if (bounds.size.y > 1.25f || !ContainsReferenceInHorizontalBounds(bounds, referencePosition))
                continue;

            float area = bounds.size.x * bounds.size.z;
            if (area <= bestArea)
                continue;

            best = collider;
            bestArea = area;
        }

        return best;
    }

    private Collider FindArenaBoundaryCollider(Vector3 referencePosition)
    {
        Collider namedBoundary = FindNamedArenaBoundaryCollider();
        if (namedBoundary != null)
            return namedBoundary;

        Collider best = null;
        float bestRadius = float.MaxValue;

        Collider[] colliders = Resources.FindObjectsOfTypeAll<Collider>();
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider collider = colliders[index];
            if (!IsArenaCandidateCollider(collider))
                continue;

            Bounds bounds = collider.bounds;
            if (bounds.size.y <= 1.25f || !ContainsReferenceInHorizontalBounds(bounds, referencePosition))
                continue;

            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            if (radius <= 1.5f || radius >= bestRadius)
                continue;

            best = collider;
            bestRadius = radius;
        }

        return best;
    }

    private Collider FindNamedArenaBoundaryCollider()
    {
        if (string.IsNullOrWhiteSpace(arenaBoundaryName))
            return null;

        GameObject boundaryObject = BullfightSceneCache.FindSceneObjectByName<GameObject>(arenaBoundaryName);
        if (boundaryObject == null || !boundaryObject.activeInHierarchy)
            return null;

        Collider boundaryCollider = boundaryObject.GetComponent<Collider>();
        if (IsArenaCandidateCollider(boundaryCollider))
            return boundaryCollider;

        Collider[] childColliders = boundaryObject.GetComponentsInChildren<Collider>(false);
        for (int index = 0; index < childColliders.Length; index++)
        {
            Collider childCollider = childColliders[index];
            if (IsArenaCandidateCollider(childCollider))
                return childCollider;
        }

        return null;
    }

    private bool IsArenaCandidateCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;

        GameObject colliderObject = collider.gameObject;
        if (colliderObject == null || !colliderObject.activeInHierarchy || !colliderObject.scene.IsValid())
            return false;

        Transform colliderTransform = collider.transform;
        if (colliderTransform == null)
            return false;

        if (playerStats != null && (colliderTransform == playerStats.transform || colliderTransform.IsChildOf(playerStats.transform)))
            return false;

        if (bullAI != null && (colliderTransform == bullAI.transform || colliderTransform.IsChildOf(bullAI.transform)))
            return false;

        return true;
    }

    private static bool ContainsReferenceInHorizontalBounds(Bounds bounds, Vector3 referencePosition)
    {
        const float horizontalPadding = 0.05f;
        return referencePosition.x >= bounds.min.x - horizontalPadding &&
               referencePosition.x <= bounds.max.x + horizontalPadding &&
               referencePosition.z >= bounds.min.z - horizontalPadding &&
               referencePosition.z <= bounds.max.z + horizontalPadding;
    }

    private float SampleArenaBoundaryRadius(Vector3 referencePosition)
    {
        if (arenaBoundaryCollider == null)
            return -1f;

        const int sampleCount = 32;
        float bestDistance = float.MaxValue;
        for (int index = 0; index < sampleCount; index++)
        {
            float angle = (Mathf.PI * 2f * index) / sampleCount;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (!TryGetArenaBoundaryHit(referencePosition.y, direction, out RaycastHit hit))
                continue;

            if (hit.distance < bestDistance)
                bestDistance = hit.distance;
        }

        return bestDistance < float.MaxValue ? bestDistance : -1f;
    }

    private bool TryClampPositionToArena(Vector3 position, float overflowTolerance, float snapPadding, out Vector3 correctedPosition, out Vector3 inwardDirection)
    {
        Vector3 center = ArenaCenter;
        Vector3 flatOffset = new Vector3(position.x - center.x, 0f, position.z - center.z);
        correctedPosition = position;
        inwardDirection = Vector3.zero;

        if (flatOffset.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 outwardDirection = flatOffset.normalized;
        if (TryGetArenaBoundaryHit(position.y, outwardDirection, out RaycastHit boundaryHit))
        {
            float currentDistance = flatOffset.magnitude;
            float maxDistance = Mathf.Max(0.5f, boundaryHit.distance - Mathf.Max(0.05f, snapPadding));
            if (currentDistance <= maxDistance + Mathf.Max(0f, overflowTolerance))
                return false;

            inwardDirection = -outwardDirection;
            correctedPosition = boundaryHit.point + inwardDirection * Mathf.Max(0.1f, snapPadding);
            correctedPosition.y = position.y;
            return true;
        }

        float maxAllowedRadius = Mathf.Max(1f, bullArenaRadius + Mathf.Max(0f, overflowTolerance));
        if (flatOffset.sqrMagnitude <= maxAllowedRadius * maxAllowedRadius)
            return false;

        inwardDirection = -outwardDirection;
        float targetRadius = Mathf.Max(0.5f, bullArenaRadius - Mathf.Max(0f, snapPadding));
        Vector3 clampedOffset = outwardDirection * targetRadius;
        correctedPosition = new Vector3(center.x + clampedOffset.x, position.y, center.z + clampedOffset.z);
        return true;
    }

    private bool TryGetArenaBoundaryHit(float sampleY, Vector3 horizontalDirection, out RaycastHit hit)
    {
        hit = default;
        if (arenaBoundaryCollider == null)
            return false;

        Vector3 direction = new Vector3(horizontalDirection.x, 0f, horizontalDirection.z);
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        Bounds bounds = arenaBoundaryCollider.bounds;
        float clampedY = Mathf.Clamp(sampleY, bounds.min.y + 0.05f, bounds.max.y - 0.05f);
        Vector3 center = ArenaCenter;
        Vector3 origin = new Vector3(center.x, clampedY, center.z);
        float maxDistance = Mathf.Max(bounds.size.x, bounds.size.z) + 4f;
        return arenaBoundaryCollider.Raycast(new Ray(origin, direction), out hit, maxDistance);
    }

    private bool TryClampPlayerPositionToBoundary(Vector3 position, float padding, out Vector3 correctedPosition, out Vector3 outwardDirection)
    {
        correctedPosition = position;
        outwardDirection = Vector3.zero;

        Vector3 center = ArenaCenter;
        Vector3 flatOffset = new Vector3(position.x - center.x, 0f, position.z - center.z);
        if (flatOffset.sqrMagnitude <= 0.0001f)
            return false;

        outwardDirection = flatOffset.normalized;
        float allowedDistance = GetAllowedBoundaryDistance(position.y, outwardDirection, padding);
        if (flatOffset.magnitude <= allowedDistance)
            return false;

        correctedPosition = new Vector3(
            center.x + outwardDirection.x * allowedDistance,
            position.y,
            center.z + outwardDirection.z * allowedDistance);
        return true;
    }

    private float GetAllowedBoundaryDistance(float sampleY, Vector3 outwardDirection, float padding)
    {
        float allowedDistance = Mathf.Max(0.25f, bullArenaRadius - Mathf.Max(0f, padding));
        if (TryGetArenaBoundaryHit(sampleY, outwardDirection, out RaycastHit boundaryHit))
            allowedDistance = Mathf.Max(0.25f, boundaryHit.distance - Mathf.Max(0f, padding));

        return allowedDistance;
    }

    private bool TryGetPlayerWallPadding(float skinWidth, out float wallPadding)
    {
        if (playerCapsuleCollider == null)
            playerCapsuleCollider = GetComponent<CapsuleCollider>();

        if (playerCapsuleCollider == null || !playerCapsuleCollider.enabled)
        {
            wallPadding = 0f;
            return false;
        }

        wallPadding = GetCapsuleWorldRadius(playerCapsuleCollider) + Mathf.Max(0.005f, skinWidth);
        return true;
    }

    private Vector3 GetArenaInwardDirection(Vector3 currentPosition)
    {
        Vector3 inward = ArenaCenter - currentPosition;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.0001f)
            return inward.normalized;

        return Vector3.back;
    }

    private static float GetCapsuleWorldRadius(CapsuleCollider capsule)
    {
        if (capsule == null)
            return 0f;

        Vector3 scale = capsule.transform.lossyScale;
        float scaleX = Mathf.Abs(scale.x);
        float scaleY = Mathf.Abs(scale.y);
        float scaleZ = Mathf.Abs(scale.z);

        float radiusScale = capsule.direction switch
        {
            0 => Mathf.Max(scaleY, scaleZ),
            2 => Mathf.Max(scaleX, scaleY),
            _ => Mathf.Max(scaleX, scaleZ)
        };

        return capsule.radius * radiusScale;
    }

    private Vector3 SampleGround(Vector3 preferredPosition, Transform ignoredRoot)
    {
        Vector3 origin = preferredPosition + Vector3.up * groundProbeHeight;
        float maxDistance = groundProbeHeight * 2f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float referenceGroundY = arenaFloorCollider != null
            ? arenaFloorCollider.bounds.max.y
            : (playerSpawnPoint != null ? playerSpawnPoint.position.y : preferredPosition.y);

        float bestHeightDelta = float.MaxValue;
        float closestDistance = float.MaxValue;
        Vector3 bestPoint = preferredPosition;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (ignoredRoot != null && (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot)))
                continue;

            if (hit.normal.y < 0.2f)
                continue;

            float heightDelta = Mathf.Abs(hit.point.y - referenceGroundY);
            bool isBetterHeight = heightDelta < bestHeightDelta - 0.0001f;
            bool isTieButCloser = Mathf.Abs(heightDelta - bestHeightDelta) <= 0.0001f && hit.distance < closestDistance;
            if (isBetterHeight || isTieButCloser)
            {
                bestHeightDelta = heightDelta;
                closestDistance = hit.distance;
                bestPoint = hit.point;
                found = true;
            }
        }

        if (!found)
            bestPoint.y = preferredPosition.y;

        return bestPoint;
    }

    private Vector3 ClampToArena(Vector3 position, float padding = 0f)
    {
        if (TryClampPositionToArena(position, 0f, padding, out Vector3 correctedPosition, out _))
            return correctedPosition;

        Vector3 center = ArenaCenter;
        Vector3 flatOffset = new Vector3(position.x - center.x, 0f, position.z - center.z);
        float clampedRadius = Mathf.Max(1f, bullArenaRadius - padding);
        if (flatOffset.magnitude > clampedRadius)
            flatOffset = flatOffset.normalized * clampedRadius;

        return new Vector3(center.x + flatOffset.x, position.y, center.z + flatOffset.z);
    }

    private Vector3 GetViewForward()
    {
        Transform reference = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = reference.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();
        return forward;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);

        if (playerCapsuleCollider == null)
            playerCapsuleCollider = GetComponent<CapsuleCollider>();

        if (bullAI == null)
            bullAI = BullfightSceneCache.FindObject<BullAI>();

        if (bullRigidbody == null && bullAI != null)
            bullRigidbody = bullAI.GetComponent<Rigidbody>();
    }
}
