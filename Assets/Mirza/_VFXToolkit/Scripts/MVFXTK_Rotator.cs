using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirza.VFXToolKit
{
    public class MVFXTK_Rotator : MonoBehaviour
    {
        public Vector3 rotation;
        public Space space = Space.Self;

        // Internal rotation state to accumulate cleanly over time.

        Quaternion currentRotation;

        void Start()
        {
            // Store initial rotation.

            currentRotation = transform.localRotation;
        }

        void Update()
        {
            // Compute delta rotation this frame.

            Quaternion deltaRotation = Quaternion.Euler(rotation * Time.deltaTime);

            // Apply depending on world vs. local space.

            if (space == Space.Self)
            {
                currentRotation *= deltaRotation;

                // Apply the accumulated quaternion.

                transform.localRotation = currentRotation;
            }
            else
            {
                // World space: multiply from the *outside*.

                currentRotation = deltaRotation * currentRotation;

                transform.rotation = currentRotation;
            }
        }
    }
}
