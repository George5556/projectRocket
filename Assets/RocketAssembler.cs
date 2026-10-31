using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketAssembler : MonoBehaviour
{
    [Header("VR РУКИ (Перетащи сюда Left Hand и Right Hand из XR Origin)")]
    public Transform[] vrHands;
    public float touchDistance = 0.2f;

    [Header("Настройки посадки")]
    public bool useSmoothLanding = true;
    public float slowDownDistance = 15f;

    [Header("Эффект БАБАХА при стыковке (общий префаб)")]
    public GameObject defaultLandingExplosionPrefab;
    public float explosionScale = 1f;
    public float explosionDestroyDelay = 4f;

    [System.Serializable]
    public class RocketPartStage
    {
        public string stageName;
        public GameObject buttonCube;
        public Transform partObject;
        public Transform targetPoint;

        [Header("Огонь при полёте и точки крепления")]
        public GameObject[] fireEffects;
        public Transform[] fireAttachPoints;
        public Vector3 firePositionOffset = Vector3.zero;

        [Header("Индивидуальный БАБАХ при посадке")]
        public GameObject stageLandingPrefab;
        public GameObject[] sceneLandingEffects;
        public Transform landingEffectPoint;
        public Vector3 landingEffectOffset = Vector3.zero;

        [Header("Параметры полёта")]
        public float spawnHeightOffset = 100f;
        public float flySpeed = 25f;

        [HideInInspector] public Vector3 cachedTargetPos;
        [HideInInspector] public Collider cachedCubeCollider;
    }

    [Header("Этапы сборки по очереди")]
    public RocketPartStage[] stages;

    private int currentStageIndex = 0;
    private bool isAnimating = false;

    void Start()
    {
        if (stages == null) return;

        for (int i = 0; i != stages.Length; i++)
        {
            RocketPartStage stage = stages[i];

            ToggleEffects(stage.fireEffects, false);
            ToggleEffects(stage.sceneLandingEffects, false);
            AttachFireToPoints(stage);

            if (stage.partObject != null && stage.targetPoint != null)
            {
                stage.cachedTargetPos = CalculateBottomAlignedPosition(stage.partObject, stage.targetPoint.position);
                stage.partObject.position = stage.cachedTargetPos + Vector3.up * stage.spawnHeightOffset;
            }

            if (stage.buttonCube != null)
            {
                stage.cachedCubeCollider = (Collider)stage.buttonCube.GetComponent(typeof(Collider));
                stage.buttonCube.SetActive(i == 0);
            }
        }
    }

    void Update()
    {
        if (isAnimating || stages == null || currentStageIndex == stages.Length) return;

        RocketPartStage currentStage = stages[currentStageIndex];
        GameObject activeCube = currentStage.buttonCube;

        if (activeCube == null || !activeCube.activeInHierarchy) return;

        if (vrHands != null)
        {
            Collider cubeCol = currentStage.cachedCubeCollider;

            for (int i = 0; i != vrHands.Length; i++)
            {
                Transform hand = vrHands[i];
                if (hand == null) continue;

                Vector3 closestPoint = (cubeCol != null)
                    ? cubeCol.ClosestPoint(hand.position)
                    : activeCube.transform.position;

                float dist = Vector3.Distance(hand.position, closestPoint);

                if (Mathf.Min(dist, touchDistance) == dist)
                {
                    StartCoroutine(FlyAndNextCubeRoutine(currentStage));
                    return;
                }
            }
        }
    }

    private IEnumerator FlyAndNextCubeRoutine(RocketPartStage stage)
    {
        isAnimating = true;

        if (stage.buttonCube != null)
        {
            stage.buttonCube.SetActive(false);
        }

        if (stage.partObject != null && stage.targetPoint != null)
        {
            Vector3 targetPos = stage.cachedTargetPos;
            stage.partObject.gameObject.SetActive(true);

            AttachFireToPoints(stage);
            ToggleEffects(stage.fireEffects, true);

            while (!Mathf.Approximately(Vector3.Distance(stage.partObject.position, targetPos), 0f))
            {
                float distanceRemaining = Vector3.Distance(stage.partObject.position, targetPos);
                if (Mathf.Min(distanceRemaining, 0.01f) == distanceRemaining) break;

                float currentSpeed = stage.flySpeed;

                if (useSmoothLanding && Mathf.Min(distanceRemaining, slowDownDistance) == distanceRemaining)
                {
                    float speedMultiplier = Mathf.Clamp(distanceRemaining / Mathf.Max(slowDownDistance, 0.1f), 0.15f, 1f);
                    currentSpeed = stage.flySpeed * speedMultiplier;
                }

                stage.partObject.position = Vector3.MoveTowards(
                    stage.partObject.position,
                    targetPos,
                    currentSpeed * Time.deltaTime
                );

                yield return null;
            }

            stage.partObject.position = targetPos;

            // 1. Гасим огонь сопла
            ToggleEffects(stage.fireEffects, false);

            // 2. Запускаем БАБАХ при стыковке
            TriggerLandingExplosion(stage);
        }

        currentStageIndex++;
        isAnimating = false;

        if (currentStageIndex != stages.Length && stages[currentStageIndex].buttonCube != null)
        {
            stages[currentStageIndex].buttonCube.SetActive(true);
        }
    }

    private void TriggerLandingExplosion(RocketPartStage stage)
    {
        Vector3 basePos = (stage.landingEffectPoint != null)
            ? stage.landingEffectPoint.position
            : (stage.targetPoint != null ? stage.targetPoint.position : stage.partObject.position);

        Vector3 spawnPos = basePos + stage.landingEffectOffset;
        Quaternion spawnRot = (stage.landingEffectPoint != null)
            ? stage.landingEffectPoint.rotation
            : Quaternion.identity;

        if (stage.sceneLandingEffects != null)
        {
            for (int i = 0; i != stage.sceneLandingEffects.Length; i++)
            {
                GameObject sceneFx = stage.sceneLandingEffects[i];
                if (sceneFx == null) continue;
                if (stage.landingEffectPoint != null || stage.landingEffectOffset != Vector3.zero)
                {
                    sceneFx.transform.position = spawnPos;
                }
            }
            ToggleEffects(stage.sceneLandingEffects, true);
        }

        GameObject prefabToSpawn = (stage.stageLandingPrefab != null)
            ? stage.stageLandingPrefab
            : defaultLandingExplosionPrefab;

        if (prefabToSpawn != null)
        {
            GameObject boomInstance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            if (!Mathf.Approximately(explosionScale, 1f) && !Mathf.Approximately(explosionScale, 0f))
            {
                boomInstance.transform.localScale = Vector3.one * explosionScale;
            }
            boomInstance.SetActive(true);

            Component[] particles = boomInstance.GetComponentsInChildren(typeof(ParticleSystem), true);
            for (int i = 0; i != particles.Length; i++)
            {
                ParticleSystem ps = (ParticleSystem)particles[i];
                ps.Clear();
                ps.Play(true);
            }

            Component[] audios = boomInstance.GetComponentsInChildren(typeof(AudioSource), true);
            for (int i = 0; i != audios.Length; i++)
            {
                AudioSource a = (AudioSource)audios[i];
                a.Play();
            }

            if (!Mathf.Approximately(explosionDestroyDelay, 0f))
            {
                Destroy(boomInstance, explosionDestroyDelay);
            }
        }
    }

    private void AttachFireToPoints(RocketPartStage stage)
    {
        if (stage.fireEffects == null || stage.partObject == null) return;

        for (int i = 0; i != stage.fireEffects.Length; i++)
        {
            GameObject fx = stage.fireEffects[i];
            if (fx == null) continue;

            Transform attachPoint = null;
            if (stage.fireAttachPoints != null && stage.fireAttachPoints.Length != 0)
            {
                int pointIndex = Mathf.Min(i, stage.fireAttachPoints.Length - 1);
                attachPoint = stage.fireAttachPoints[pointIndex];
            }

            if (attachPoint != null)
            {
                if (!attachPoint.IsChildOf(stage.partObject))
                {
                    attachPoint.SetParent(stage.partObject, true);
                }

                fx.transform.SetParent(attachPoint, false);
                fx.transform.localPosition = stage.firePositionOffset;
                fx.transform.localRotation = Quaternion.identity;
            }
            else if (stage.firePositionOffset != Vector3.zero)
            {
                if (fx.transform.parent != stage.partObject)
                {
                    fx.transform.SetParent(stage.partObject, false);
                }
                fx.transform.localPosition = stage.firePositionOffset;
            }
        }
    }

    private Vector3 CalculateBottomAlignedPosition(Transform part, Vector3 targetBottomPos)
    {
        Component[] renderers = part.GetComponentsInChildren(typeof(Renderer), true);
        if (renderers.Length == 0) return targetBottomPos;

        Bounds combinedBounds = new Bounds(part.position, Vector3.zero);
        bool hasValidBounds = false;

        for (int i = 0; i != renderers.Length; i++)
        {
            Renderer r = (Renderer)renderers[i];
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;

            if (!hasValidBounds)
            {
                combinedBounds = r.bounds;
                hasValidBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasValidBounds) return targetBottomPos;

        float pivotToBottomDistance = part.position.y - combinedBounds.min.y;
        return new Vector3(targetBottomPos.x, targetBottomPos.y + pivotToBottomDistance, targetBottomPos.z);
    }

    private void ToggleEffects(GameObject[] effects, bool state)
    {
        if (effects == null) return;

        for (int i = 0; i != effects.Length; i++)
        {
            GameObject fx = effects[i];
            if (fx == null) continue;

            if (state)
            {
                fx.SetActive(true);

                Component[] particles = fx.GetComponentsInChildren(typeof(ParticleSystem), true);
                for (int j = 0; j != particles.Length; j++)
                {
                    ParticleSystem ps = (ParticleSystem)particles[j];
                    ps.Clear();
                    ps.Play(true);
                }

                Component[] lights = fx.GetComponentsInChildren(typeof(Light), true);
                for (int j = 0; j != lights.Length; j++)
                {
                    Light l = (Light)lights[j];
                    l.enabled = true;
                }

                Component[] audios = fx.GetComponentsInChildren(typeof(AudioSource), true);
                for (int j = 0; j != audios.Length; j++)
                {
                    AudioSource a = (AudioSource)audios[j];
                    a.Play();
                }
            }
            else
            {
                Component[] particles = fx.GetComponentsInChildren(typeof(ParticleSystem), true);
                for (int j = 0; j != particles.Length; j++)
                {
                    ParticleSystem ps = (ParticleSystem)particles[j];
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                Component[] lights = fx.GetComponentsInChildren(typeof(Light), true);
                for (int j = 0; j != lights.Length; j++)
                {
                    Light l = (Light)lights[j];
                    l.enabled = false;
                }

                Component[] audios = fx.GetComponentsInChildren(typeof(AudioSource), true);
                for (int j = 0; j != audios.Length; j++)
                {
                    AudioSource a = (AudioSource)audios[j];
                    a.Stop();
                }

                fx.SetActive(false);
            }
        }
    }
}
