using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class CrowdSystem : MonoBehaviour
{
    [Serializable]
    public struct CrowdSettings
    {
        public float speed;
        public float height;
        public float avoidanceStrength;
    }

    public CrowdSettings settings;
    private TransformAccessArray _transformAccessArray;
    private NativeArray<SpherecastCommand> _surfaceNormalsCommands;
    private NativeArray<RaycastHit> _surfaceNormalsHits;
    private NativeArray<SpherecastCommand> _avoidanceCommands;
    private NativeArray<RaycastHit> _avoidanceHits;
    public int crowdSize;
    public float spawnSphereRadius;
    public GameObject crowdPrefab;

    private void Awake()
    {
        _surfaceNormalsCommands = new NativeArray<SpherecastCommand>(crowdSize, Allocator.Persistent);
        _surfaceNormalsHits = new NativeArray<RaycastHit>(crowdSize, Allocator.Persistent);
        _avoidanceCommands = new NativeArray<SpherecastCommand>(crowdSize, Allocator.Persistent);
        _avoidanceHits = new NativeArray<RaycastHit>(crowdSize, Allocator.Persistent);
        _transformAccessArray = new TransformAccessArray(crowdSize, 64);
        for (int i = 0; i < crowdSize; i++)
        {
            GameObject g = Instantiate(crowdPrefab, UnityEngine.Random.insideUnitSphere * spawnSphereRadius,
                Quaternion.Euler(0,UnityEngine.Random.Range(0,360),0));
            g.transform.position = new Vector3(g.transform.position.x, 0, g.transform.position.z);
            _transformAccessArray.Add(g.transform);
        }
    }

    private void Update()
    {
        var applyBoidPositionJob = new ApplyPositionJob()
        {
            settings = settings,
            underHits = _surfaceNormalsHits,
            avoidanceHits = _avoidanceHits
        };
        JobHandle crowdSimulation = SphereRaycastAhead(_transformAccessArray);
        JobHandle surfaceHandle = SphereCastUnder(_transformAccessArray);
        JobHandle finalHandle = applyBoidPositionJob.Schedule(_transformAccessArray,
            JobHandle.CombineDependencies(surfaceHandle, crowdSimulation));
        finalHandle.Complete();
    }

    [BurstCompile]
    private struct ApplyPositionJob : IJobParallelForTransform
    {
        public CrowdSettings settings;
        [ReadOnly] public NativeArray<RaycastHit> underHits;
        [ReadOnly] public NativeArray<RaycastHit> avoidanceHits;

        public void Execute(int index, TransformAccess transform)
        {
            if (underHits[index].distance != 0)
            {
                transform.position = underHits[index].point + (transform.rotation * Vector3.up * settings.height) / 2;
                transform.rotation =
                    Quaternion.LookRotation(
                        ProjectOnPlane(
                            transform.rotation * Vector3.forward +
                            Avoidance(index,transform), underHits[index].normal),
                        underHits[index].normal);

                transform.position += transform.rotation * Vector3.forward * settings.speed;
            }
        }

        private Vector3 Avoidance(int index,TransformAccess transform)
        {
            if(math.abs(math.dot(avoidanceHits[index].normal, transform.rotation * Vector3.up))<0.1f)
                return avoidanceHits[index].normal * settings.avoidanceStrength;
            return Vector3.zero;
        }
    }

    private JobHandle SphereRaycastAhead([ReadOnly] TransformAccessArray transformAccessArray,
        JobHandle dependency = default)
    {
        float radius = 0.5f;

        for (int i = 0; i < _avoidanceCommands.Length; i++)
        {
            _avoidanceCommands[i] = new SpherecastCommand(
                transformAccessArray[i].position, radius,
                transformAccessArray[i].forward, 10);
        }

        var handle = SpherecastCommand.ScheduleBatch(_avoidanceCommands, _avoidanceHits, 64, dependency);

        return handle;
    }

    private JobHandle SphereCastUnder([ReadOnly] TransformAccessArray transformAccessArray,
        JobHandle dependency = default)
    {
        float radius = 0.5f;

        for (int i = 0; i < _surfaceNormalsCommands.Length; i++)
        {
            _surfaceNormalsCommands[i] = new SpherecastCommand(
                transformAccessArray[i].position + transformAccessArray[i].up * settings.height, radius,
                -transformAccessArray[i].up, 100);
        }

        var handle = SpherecastCommand.ScheduleBatch(_surfaceNormalsCommands, _surfaceNormalsHits, 64, dependency);

        return handle;
    }

    private void OnDestroy()
    {
        _transformAccessArray.Dispose();
        _surfaceNormalsCommands.Dispose();
        _surfaceNormalsHits.Dispose();
        _avoidanceCommands.Dispose();
        _avoidanceHits.Dispose();
    }

    public static float3 ProjectOnPlane(float3 vector, float3 planeNormal)
    {
        float sqrMag = math.dot(planeNormal, planeNormal);
        if (sqrMag < math.FLT_MIN_NORMAL)
            return vector;
        else
        {
            var dot = math.dot(vector, planeNormal);
            return new float3(vector.x - planeNormal.x * dot / sqrMag,
                vector.y - planeNormal.y * dot / sqrMag,
                vector.z - planeNormal.z * dot / sqrMag);
        }
    }
}