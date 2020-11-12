using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class MostSimpleExample : MonoBehaviour
{
    public enum Mode
    {
        Mono,
        Arrays,
        WithJobs
    }

    public Mode mode;
    private List<GameObject> _gameObjects = new List<GameObject>();
    private TransformAccessArray _transformAccessArray;
    public int objectCount;
    public float speed;

    private void Awake()
    {
        _transformAccessArray = new TransformAccessArray(objectCount);
        for (int i = 0; i < objectCount; i++)
        {
            GameObject g = new GameObject();
            _transformAccessArray.Add(g.transform);
            if (mode == Mode.Mono)
            {
                var monoComp = g.AddComponent<MoveUpMonoBehaviour>();
                monoComp.speed = speed;
            }
            _gameObjects.Add(g);
        }
    }

    private void Update()
    {
        switch (mode)
        {
            case Mode.Mono:
                break;
            case Mode.Arrays:
                for (int i = 0; i < _gameObjects.Count; i++)
                {
                    _gameObjects[i].transform.position += Vector3.up * speed;
                }
                break;
            case Mode.WithJobs:
                var job = new MoveUpJob()
                {
                    speed = speed
                };
                job.Schedule(_transformAccessArray);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [BurstCompile]
    private struct MoveUpJob : IJobParallelForTransform
    {
        public float speed;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position += Vector3.up * speed;
        }
    }

    private void OnDestroy()
    {
        _transformAccessArray.Dispose();
    }
}

public class MoveUpMonoBehaviour : MonoBehaviour
{
    public float speed;
    private void Update()
    {
        transform.position += Vector3.up;
    }
}