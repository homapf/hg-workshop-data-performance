using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public class AccelerationParallelFor : MonoBehaviour
{
    public int objectCount;
    public float radius;
    
    public Vector3 m_Acceleration = new Vector3(0.0002f, 0.0001f, 0.0002f);
    public Vector3 m_AccelerationMod = new Vector3(.0001f, 0.0001f, 0.0001f);

    NativeArray<float3> m_Velocities;
    TransformAccessArray m_TransformsAccessArray;
    
    PositionUpdateJob m_Job;
    AccelerationJob m_AccelJob;

    JobHandle m_PositionJobHandle;
    JobHandle m_AccelJobHandle;

    protected void Start()
    {
        m_Velocities = new NativeArray<float3>(objectCount, Allocator.Persistent);
        GameObject[] list = SetupUtils.PlaceRandomCubes(objectCount, radius);
        m_TransformsAccessArray = new TransformAccessArray(objectCount);
        for (int i = 0; i < objectCount; i++)
        {
            m_TransformsAccessArray.Add(list[i].transform);
        }
    }

    [BurstCompile]
    struct PositionUpdateJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float3> velocity;  // the velocities from AccelerationJob

        public float deltaTime;

        public void Execute(int i, TransformAccess transform)
        {
            transform.position += new Vector3(velocity[i].x,velocity[i].y,velocity[i].z) * deltaTime;
        }
    }
    
    [BurstCompile]
    struct AccelerationJob : IJobParallelFor
    {
        public NativeArray<float3> velocity;

        public float3 acceleration;
        public float3 accelerationMod;

        public float deltaTime;

        public void Execute(int i)
        {
            velocity[i] += (acceleration + i * accelerationMod) * deltaTime;
        }
    }

    public void Update()
    {
        m_AccelJob = new AccelerationJob()
        {
            deltaTime = Time.deltaTime,
            velocity = m_Velocities,
            acceleration = m_Acceleration,
            accelerationMod = m_AccelerationMod
        };

        m_Job = new PositionUpdateJob()
        {
            deltaTime = Time.deltaTime,
            velocity = m_Velocities,
        };

        m_AccelJobHandle = m_AccelJob.Schedule(objectCount, 64);
        m_PositionJobHandle = m_Job.Schedule(m_TransformsAccessArray, m_AccelJobHandle);
    }

    public void LateUpdate()
    {
        m_PositionJobHandle.Complete();
    }

    private void OnDestroy()
    {
        m_Velocities.Dispose();
        m_TransformsAccessArray.Dispose();
    }
}