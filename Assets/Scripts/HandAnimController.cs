using UnityEngine;
using UnityEngine.InputSystem;


public class AnimationController : MonoBehaviour
{
    private Animator anim;


    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard.pKey.wasPressedThisFrame)//this triggers hand animation
        {
            // Play a specific state directly by name
            Debug.Log("Playing TiggerRubFace animation");
            anim.Play("rubFaceAction", 0, 0f); // Play the "TiggerRubFace" animation on layer 0, starting at the beginning
        }
    }
}