using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class AnimationOffset : MonoBehaviour
{
    private float offset;

    private void Start()
    {
        offset = Random.Range(0f, 5f);
        GetComponent<Animator>().Play("Normal", 0, offset);
    }
}
