using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanciateALot : MonoBehaviour
{
    public GameObject gameObject;

    public int count;

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Instantiate(gameObject);
        }
    }
}
