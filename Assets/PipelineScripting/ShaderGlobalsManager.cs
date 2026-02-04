using UnityEngine;

public class ShaderGlobalsManager : MonoBehaviour
{
    public RenderTexture cameraART;
    public RenderTexture cameraBRT;
    void Start()
    {
        Shader.SetGlobalTexture("_CameraA_Tex", cameraART);
        Shader.SetGlobalTexture("_CameraB_Tex", cameraBRT);
    }
}
