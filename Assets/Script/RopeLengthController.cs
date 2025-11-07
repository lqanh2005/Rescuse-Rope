using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class RopeLengthController : MonoBehaviour
{

    public float speed = 1;
    [SerializeField] ObiRopeCursor cursor;
    [SerializeField] ObiRope rope;

    void Start()
    {
        cursor = GetComponentInChildren<ObiRopeCursor>();
        rope = cursor.GetComponent<ObiRope>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("Rope rest length before change: " + rope.restLength);
            cursor.ChangeLength(-speed); 
            Debug.Log("Rope rest length after change: " + rope.restLength);
            Debug.Log("W");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            cursor.ChangeLength(speed); 
            Debug.Log("S");
        }
    }
}