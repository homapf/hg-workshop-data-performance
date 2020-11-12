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
        Simplest,
        Mono,
        Arrays,
        WithJobs
    }

    public Mode mode;
    private List<GameObject> _gameObjects = new List<GameObject>();
    private List<SimplestCallBehaviour> _simplestClassCalls = new List<SimplestCallBehaviour>();
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

            if (mode == Mode.Simplest)
            {
                var monoComp = g.AddComponent<SimplestCallBehaviour>();
                monoComp.speed = speed;
                _simplestClassCalls.Add(monoComp);
            }
            _gameObjects.Add(g);
        }
    }

    private void Update()
    {
        switch (mode)
        {
            case Mode.Simplest:
                for (int i = 0; i < _simplestClassCalls.Count; i++)
                {
                    _simplestClassCalls[i].MyUpdate();
                }
                break;
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
        transform.position += Vector3.up * speed;
    }

    public void MyUpdate()
    {
        transform.position += Vector3.up * speed;
    }
}

public class SimplestCallBehaviour : MonoBehaviour
{
    public float speed;

    public void MyUpdate()
    {
        transform.position += Vector3.up * speed;
    }
}