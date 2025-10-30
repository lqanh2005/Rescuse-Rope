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
        Debug.LogError("Rope rest length at start: " + rope.restLength);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.LogError("Rope rest length before change: " + rope.restLength);
            cursor.ChangeLength(-speed); 
            Debug.LogError("Rope rest length after change: " + rope.restLength);
            Debug.LogError("W");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            cursor.ChangeLength(speed); 
            Debug.LogError("S");
        }
    }
}