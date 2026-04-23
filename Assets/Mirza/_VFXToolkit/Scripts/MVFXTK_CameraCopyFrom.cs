using UnityEngine;

namespace Mirza.VFXToolKit
{
    // This script copies all settings from the target camera, *except* for the target texture.
    // Because it's typically used for that purpose-> same camera view, different render and texture.

    [ExecuteAlways]
    public class MVFXTK_CameraCopyFrom : MonoBehaviour
    {
        Camera camera;
        public Camera target;

        public float priorityOffset = -1;

        void LateUpdate()
        {
            if (!camera)
            {
                camera = GetComponent<Camera>();
            }

            RenderTexture targetTexture = camera.targetTexture;

            camera.CopyFrom(target);
            camera.depth += priorityOffset;

            camera.targetTexture = targetTexture;
        }
    }
}