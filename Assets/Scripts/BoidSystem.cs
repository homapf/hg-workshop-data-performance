using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class BoidSystem : MonoBehaviour
{
    [Serializable]
    public struct BoidSettings
    {
        [Header("General")] public float speed;
        [Header("Boid Behaviour")] public float separation;
        public float separationRadius;
        public float alignment;
        public float alignmentRadius;
        public float cohesion;
        public float cohesionRadius;
        [Header("Avoidance")] public float obstacleAvoidance;
        public float obstacleAvoidanceDistance;
        [Header("Noise")] public float noiseFrequency;
        public float noiseStrength;
    }

    public BoidSettings settings;
    private NativeArray<float3> _boidResultant;
    private TransformAccessArray _transformAccessArray;
    private NativeArray<float3> _positions;
    private NativeArray<float3> _directions;
    private NativeArray<SpherecastCommand> _avoidanceCommands;
    private NativeArray<RaycastHit> _avoidanceHits;
    public int boidNumber;
    public float spawnSphereRadius;
    public GameObject boidPrefab;

    private void Awake()
    {
        _avoidanceCommands = new NativeArray<SpherecastCommand>(boidNumber, Allocator.Persistent);
        _avoidanceHits = new NativeArray<RaycastHit>(boidNumber, Allocator.Persistent);
        _transformAccessArray = new TransformAccessArray(boidNumber, 64);
        _positions = new NativeArray<float3>(boidNumber, Allocator.Persistent);
        _directions = new NativeArray<float3>(boidNumber, Allocator.Persistent);
        _boidResultant = new NativeArray<float3>(boidNumber, Allocator.Persistent);
        for (int i = 0; i < boidNumber; i++)
        {
            GameObject g = Instantiate(boidPrefab, UnityEngine.Random.insideUnitSphere * spawnSphereRadius,
                UnityEngine.Random.rotation);
            _transformAccessArray.Add(g.transform);
        }
    }

    private void Update()
    {
        for (int i = 0; i < _transformAccessArray.length; i++)
        {
            _positions[i] = new float3(_transformAccessArray[i].position);
            _directions[i] = new float3(_transformAccessArray[i].forward);
        }

        var boidSystemJob = new BoidSystemJob()
        {
            boidResultant = _boidResultant,
            boidPositions = _positions,
            boidDirections = _directions,
            settings = settings
        };
        var applyBoidPositionJob = new ApplyPositionJob()
        {
            boidResultant = _boidResultant,
            avoidancehits = _avoidanceHits,
            settings = settings
        };
        JobHandle boidSimulation = boidSystemJob.Schedule(boidNumber, 64);
        JobHandle avoidanceHandle = SphereRaycastAhead(_transformAccessArray, boidSimulation);
        JobHandle finalHandle = applyBoidPositionJob.Schedule(_transformAccessArray,
            JobHandle.CombineDependencies(avoidanceHandle, boidSimulation));
        finalHandle.Complete();
    }

    [BurstCompile]
    private struct BoidSystemJob : IJobParallelFor
    {
        public BoidSettings settings;
        public NativeArray<float3> boidResultant;
        [ReadOnly] public NativeArray<float3> boidPositions;
        [ReadOnly] public NativeArray<float3> boidDirections;

        public void Execute(int index)
        {
            boidResultant[index] = math.normalizesafe(ComputeDirection(index) +
                                                      ComputeSeparation(index) + ComputeCohesion(index));
        }

        private float3 ComputeDirection(int index)
        {
            float3 direction = float3.zero;

            for (int i = 0; i < boidDirections.Length; i++)
            {
                if (i != index && IsInRange(index, i, settings.alignmentRadius))
                    direction += boidDirections[i];
            }

            return math.normalizesafe(direction) * settings.alignment;
        }

        private float3 ComputeSeparation(int index)
        {
            float3 separation = float3.zero;
            for (int i = 0; i < boidPositions.Length; i++)
            {
                if (i != index && IsInRange(index, i, settings.separationRadius))
                    separation += boidPositions[index] - boidPositions[i];
            }

            return math.normalizesafe(separation) * settings.separation;
        }

        private float3 ComputeCohesion(int index)
        {
            float3 center = float3.zero;
            int count = 0;
            for (int i = 0; i < boidPositions.Length; i++)
            {
                if (IsInRange(index, i, settings.cohesionRadius))
                {
                    center += new float3(boidPositions[i]);
                    count++;
                }
            }

            center /= count;
            return math.normalizesafe(center - boidPositions[index]) * settings.cohesion;
        }

        private bool IsInRange(int index, int otherIndex, float range)
        {
            return math.distance(boidPositions[index], boidPositions[otherIndex]) < range;
        }
    }

    private struct ApplyPositionJob : IJobParallelForTransform
    {
        public BoidSettings settings;
        [ReadOnly] public NativeArray<float3> boidResultant;
        [ReadOnly] public NativeArray<RaycastHit> avoidancehits;

        public void Execute(int index, TransformAccess transform)
        {
            float3 baseDirection = transform.rotation * Vector3.forward;
            var direction = baseDirection + math.normalizesafe(boidResultant[index] + Avoidance(index)) +
                            Noise(index, transform);
            transform.position = new float3(transform.position) + direction * settings.speed;
            transform.rotation =
                Unity.Mathematics.quaternion.LookRotation(direction, new float3(0, 0, 1));
        }

        private float3 Avoidance(int index)
        {
            float str = (avoidancehits[index].distance < settings.obstacleAvoidanceDistance &&
                         avoidancehits[index].distance != 0)
                ? math.lerp(settings.obstacleAvoidance, 0,
                    avoidancehits[index].distance / settings.obstacleAvoidanceDistance)
                : 0;
            return new float3(avoidancehits[index].normal) * str;
        }

        private float3 Noise(int index, TransformAccess transformAccess)
        {
            return Perlin.Noise(new float3(transformAccess.position)) * settings.noiseStrength;
        }
    }

    private JobHandle SphereRaycastAhead([ReadOnly] TransformAccessArray transformAccessArray,
        JobHandle dependency = default)
    {
        float radius = 0.5f;

        for (int i = 0; i < _avoidanceCommands.Length; i++)
        {
            _avoidanceCommands[i] = new SpherecastCommand(transformAccessArray[i].position, radius,
                transformAccessArray[i].forward, 100);
        }

        var handle = SpherecastCommand.ScheduleBatch(_avoidanceCommands, _avoidanceHits, 64, dependency);

        return handle;
    }

    private void OnDestroy()
    {
        _transformAccessArray.Dispose();
        _avoidanceCommands.Dispose();
        _avoidanceHits.Dispose();
        _boidResultant.Dispose();
        _positions.Dispose();
        _directions.Dispose();
    }
}