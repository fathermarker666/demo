using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BullChargePathFogVfx : MonoBehaviour
{
    private const string DefaultFogResourcePath = "BullfightVfx/Bull Charge Fog Stamp";

    [SerializeField] private ParticleSystem fogStampPrefab;
    [SerializeField] private float spawnSpacing = 0.65f;
    [SerializeField] private float trailBackOffset = 0.55f;
    [SerializeField] private float groundProbeHeight = 3f;
    [SerializeField] private float groundOffset = 0.02f;
    [SerializeField] private float fadeStepDelay = 0.08f;
    [SerializeField] private float destroyDelayBuffer = 0.2f;
    [SerializeField] private int maxActiveStamps = 18;

    private readonly List<ParticleSystem> activeStamps = new List<ParticleSystem>();
    private Coroutine fadeRoutine;
    private Vector3 lastSamplePosition;
    private float distanceSinceLastSpawn;
    private bool chargeActive;
    private bool missingPrefabWarningLogged;

    private void Awake()
    {
        ResolvePrefabIfNeeded();
    }

    private void OnDisable()
    {
        ForceClear();
    }

    public void BeginCharge(Vector3 startPosition, Vector3 direction)
    {
        StopFadeRoutine();
        ForceClearActiveStamps();

        chargeActive = true;
        distanceSinceLastSpawn = 0f;
        lastSamplePosition = startPosition;

        if (ResolvePrefabIfNeeded() == null)
            chargeActive = false;
    }

    public void SampleCharge(Vector3 bullPosition, Vector3 direction)
    {
        if (!chargeActive || ResolvePrefabIfNeeded() == null)
            return;

        Vector3 currentPosition = bullPosition;
        Vector3 segmentDelta = currentPosition - lastSamplePosition;
        segmentDelta.y = 0f;

        float segmentDistance = segmentDelta.magnitude;
        if (segmentDistance <= 0.0001f)
            return;

        Vector3 segmentDirection = segmentDelta / segmentDistance;
        Vector3 spawnDirection = GetHorizontalDirection(direction, segmentDirection);
        Vector3 segmentStart = lastSamplePosition;
        float remainingDistance = segmentDistance;

        while (distanceSinceLastSpawn + remainingDistance >= Mathf.Max(0.05f, spawnSpacing))
        {
            float distanceToNext = Mathf.Max(0.05f, spawnSpacing) - distanceSinceLastSpawn;
            float spawnLerp = remainingDistance <= 0.0001f ? 1f : distanceToNext / remainingDistance;
            Vector3 spawnAnchor = Vector3.Lerp(segmentStart, currentPosition, spawnLerp);
            SpawnStamp(spawnAnchor, spawnDirection);

            segmentStart = spawnAnchor;
            remainingDistance -= distanceToNext;
            distanceSinceLastSpawn = 0f;
        }

        distanceSinceLastSpawn += remainingDistance;
        lastSamplePosition = currentPosition;
    }

    public void EndCharge()
    {
        chargeActive = false;
        distanceSinceLastSpawn = 0f;

        if (fadeRoutine != null || activeStamps.Count == 0)
            return;

        fadeRoutine = StartCoroutine(FadeStampsInOrder());
    }

    public void ForceClear()
    {
        chargeActive = false;
        distanceSinceLastSpawn = 0f;
        StopFadeRoutine();
        ForceClearActiveStamps();
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void ForceClearActiveStamps()
    {
        for (int i = activeStamps.Count - 1; i >= 0; i--)
        {
            ParticleSystem stamp = activeStamps[i];
            if (stamp == null)
                continue;

            stamp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stamp.gameObject.SetActive(false);
            DestroySafely(stamp.gameObject);
        }

        activeStamps.Clear();
    }

    private IEnumerator FadeStampsInOrder()
    {
        float perStampDelay = Mathf.Max(0f, fadeStepDelay);

        for (int i = 0; i < activeStamps.Count; i++)
        {
            ParticleSystem stamp = activeStamps[i];
            if (stamp != null)
            {
                stamp.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                DestroySafely(stamp.gameObject, GetDestroyDelay(stamp));
            }

            if (perStampDelay > 0f && i < activeStamps.Count - 1)
                yield return new WaitForSeconds(perStampDelay);
        }

        activeStamps.Clear();
        fadeRoutine = null;
    }

    private void SpawnStamp(Vector3 spawnAnchor, Vector3 direction)
    {
        ParticleSystem prefab = ResolvePrefabIfNeeded();
        if (prefab == null)
            return;

        Vector3 spawnDirection = GetHorizontalDirection(direction, transform.forward);
        Vector3 spawnPosition = spawnAnchor - (spawnDirection * Mathf.Max(0f, trailBackOffset));
        spawnPosition = ProjectToGround(spawnPosition);

        Quaternion rotation = Quaternion.LookRotation(spawnDirection, Vector3.up);
        ParticleSystem instance = Instantiate(prefab, spawnPosition, rotation);
        instance.Clear(true);
        instance.Play(true);
        activeStamps.Add(instance);

        TrimActiveStamps();
    }

    private void TrimActiveStamps()
    {
        int stampLimit = Mathf.Max(1, maxActiveStamps);
        while (activeStamps.Count > stampLimit)
        {
            ParticleSystem oldestStamp = activeStamps[0];
            activeStamps.RemoveAt(0);
            if (oldestStamp == null)
                continue;

            oldestStamp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            oldestStamp.gameObject.SetActive(false);
            DestroySafely(oldestStamp.gameObject);
        }
    }

    private Vector3 ProjectToGround(Vector3 worldPosition)
    {
        float probeHeight = Mathf.Max(0.5f, groundProbeHeight);
        Vector3 origin = worldPosition + Vector3.up * probeHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            worldPosition.y += groundOffset;
            return worldPosition;
        }

        float referenceGroundY = worldPosition.y;
        float bestHeightDelta = float.MaxValue;
        float closestDistance = float.MaxValue;
        Vector3 bestPoint = worldPosition;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform) || hit.normal.y < 0.2f)
                continue;

            float heightDelta = Mathf.Abs(hit.point.y - referenceGroundY);
            bool betterHeight = heightDelta < bestHeightDelta - 0.0001f;
            bool closerTie = Mathf.Abs(heightDelta - bestHeightDelta) <= 0.0001f && hit.distance < closestDistance;
            if (!betterHeight && !closerTie)
                continue;

            bestHeightDelta = heightDelta;
            closestDistance = hit.distance;
            bestPoint = hit.point;
            foundGround = true;
        }

        if (!foundGround)
        {
            worldPosition.y += groundOffset;
            return worldPosition;
        }

        bestPoint.y += groundOffset;
        return bestPoint;
    }

    private ParticleSystem ResolvePrefabIfNeeded()
    {
        if (fogStampPrefab != null)
            return fogStampPrefab;

        GameObject prefabObject = Resources.Load<GameObject>(DefaultFogResourcePath);
        fogStampPrefab = prefabObject != null ? prefabObject.GetComponent<ParticleSystem>() : null;
        if (fogStampPrefab == null && !missingPrefabWarningLogged)
        {
            Debug.LogWarning($"Missing bull charge fog stamp prefab at Resources/{DefaultFogResourcePath}.");
            missingPrefabWarningLogged = true;
        }

        return fogStampPrefab;
    }

    private float GetDestroyDelay(ParticleSystem stamp)
    {
        if (stamp == null)
            return Mathf.Max(0.05f, destroyDelayBuffer);

        ParticleSystem.MainModule main = stamp.main;
        float maxLifetime = Mathf.Max(0.05f, main.startLifetime.constantMax);
        return maxLifetime + Mathf.Max(0.05f, destroyDelayBuffer);
    }

    private static Vector3 GetHorizontalDirection(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.0001f)
            return value.normalized;

        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return Vector3.forward;
    }

    private static void DestroySafely(Object target, float delay = 0f)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(target, Mathf.Max(0f, delay));
        else
            Object.DestroyImmediate(target);
    }
}
