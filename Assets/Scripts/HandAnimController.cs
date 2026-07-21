using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.Collections.Unicode;


public class AnimationController : MonoBehaviour
{
    private Animator anim;
    private bool cleared = true;
    public bool play = false;
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (play == true)//this triggers hand animation
        {
            // Play a specific state directly by name
            Log.Info("Playing rubFaceAction animation");
            anim.Play("rubFaceAction", 0, 0f); // Play the "TiggerRubFace" animation on layer 0, starting at the beginning
            cleared = false;
            play = false;
        }

        //get the current frame
        if (cleared) return;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clipInfo = anim.GetCurrentAnimatorClipInfo(0);
        if(clipInfo.Length == 0) return; // No clip info available
        if(stateInfo.IsName("rubFaceAction") == false) return; // N ot in the rubFaceAction state
           
        AnimationClip clip = clipInfo[0].clip;

        float currentTime = (stateInfo.normalizedTime % 1) * clip.length;
        int currentFrame = Mathf.FloorToInt(currentTime * clip.frameRate);

        if (currentFrame >= 189)
        {
            Log.Info("Live-action done -> completing section, looping to Intro");
            cleared = true;

            GameManager.Instance.runner.CompleteSection();
        }
    }
}