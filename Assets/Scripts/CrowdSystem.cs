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
    }

    public CrowdSettings settings;
    private TransformAccessArray _transformAccessArray;
    private NativeArray<float3> _positions;
    private NativeArray<float3> _directions;
    private NativeArray<SpherecastCommand> _surfaceNormalsCommands;
    private NativeArray<RaycastHit> _surfaceNormalsHits;
    public int crowdSize;
    public float spawnSphereRadius;
    public GameObject crowdPrefab;

    private void Awake()
    {
        _surfaceNormalsCommands = new NativeArray<SpherecastCommand>(crowdSize, Allocator.Persistent);
        _surfaceNormalsHits = new NativeArray<RaycastHit>(crowdSize, Allocator.Persistent);
        _transformAccessArray = new TransformAccessArray(crowdSize, 64);
        _positions = new NativeArray<float3>(crowdSize, Allocator.Persistent);
        _directions = new NativeArray<float3>(crowdSize, Allocator.Persistent);
        for (int i = 0; i < crowdSize; i++)
        {
            GameObject g = Instantiate(crowdPrefab, UnityEngine.Random.insideUnitSphere * spawnSphereRadius,
                quaternion.identity);
            g.transform.position = new Vector3(g.transform.position.x, 0, g.transform.position.z);
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

        var boidSystemJob = new CrowdSystemJob()
        {
            settings = settings
        };
        var applyBoidPositionJob = new ApplyPositionJob()
        {
            settings = settings,
            underHits = _surfaceNormalsHits
        };
        JobHandle crowdSimulation = boidSystemJob.Schedule(crowdSize, 64);
        JobHandle surfaceHandle = SphereCastUnder(_transformAccessArray, crowdSimulation);
        JobHandle finalHandle = applyBoidPositionJob.Schedule(_transformAccessArray,
            JobHandle.CombineDependencies(surfaceHandle, crowdSimulation));
        finalHandle.Complete();
    }

    [BurstCompile]
    private struct CrowdSystemJob : IJobParallelFor
    {
        public CrowdSettings settings;

        public void Execute(int index)
        {
        }
    }

    private struct ApplyPositionJob : IJobParallelForTransform
    {
        public CrowdSettings settings;
        [ReadOnly] public NativeArray<RaycastHit> underHits;

        public void Execute(int index, TransformAccess transform)
        {
            if (underHits[index].distance != 0)
            {
                transform.position = underHits[index].point;
                transform.rotation =
                    Quaternion.LookRotation(transform.rotation * Vector3.forward, underHits[index].normal);
            }

            transform.position += transform.rotation * Vector3.forward * 0.1f;
        }
    }

    private JobHandle SphereRaycastAhead([ReadOnly] TransformAccessArray transformAccessArray,
        JobHandle dependency = default)
    {
        float radius = 0.5f;

        for (int i = 0; i < _surfaceNormalsCommands.Length; i++)
        {
            _surfaceNormalsCommands[i] = new SpherecastCommand(
                transformAccessArray[i].position - transformAccessArray[i].up * 10, radius,
                transformAccessArray[i].forward, 100);
        }

        var handle = SpherecastCommand.ScheduleBatch(_surfaceNormalsCommands, _surfaceNormalsHits, 64, dependency);

        return handle;
    }

    private JobHandle SphereCastUnder([ReadOnly] TransformAccessArray transformAccessArray,
        JobHandle dependency = default)
    {
        float radius = 0.5f;

        for (int i = 0; i < _surfaceNormalsCommands.Length; i++)
        {
            _surfaceNormalsCommands[i] = new SpherecastCommand(
                transformAccessArray[i].position + transformAccessArray[i].up * 10, radius,
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
        _positions.Dispose();
        _directions.Dispose();
    }
}