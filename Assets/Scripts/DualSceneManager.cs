using UnityEngine;
using System.Collections.Generic;

public class DualSceneManager : MonoBehaviour
{
    public static DualSceneManager Instance { get; private set; }

    [Header("Cameras")]
    public Camera camera2D;
    public Camera camera3D;

    [Header("Mask & Composite")]
    public Material circleMaskMaterial; // Custom/CircleMask
    public Material compositeMaterial;  // Custom/CompositeMask

    private RenderTexture scene2D_RT;
    private RenderTexture scene3D_RT;
    private RenderTexture mask_RT;

    private List<RevealPortal> portals = new List<RevealPortal>();

    public RenderTexture Scene2DRT => scene2D_RT;
    public RenderTexture Scene3DRT => scene3D_RT;
    public RenderTexture MaskTexture => mask_RT;

    private int lastWidth, lastHeight;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        SetupCameras();
        CreateRenderTextures();
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            RecreateRenderTextures();
        }
    }

    void SetupCameras()
    {
        if (camera2D != null)
        {
            camera2D.depth = -2;
            camera2D.orthographic = true;
            camera2D.clearFlags = CameraClearFlags.SolidColor;
        }
        if (camera3D != null)
        {
            camera3D.depth = -1;
            camera3D.clearFlags = CameraClearFlags.Skybox;
        }
    }

    void CreateRenderTextures()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        scene2D_RT = new RenderTexture(Screen.width, Screen.height, 24);
        scene3D_RT = new RenderTexture(Screen.width, Screen.height, 24);
        mask_RT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.R8);

        if (camera2D) camera2D.targetTexture = scene2D_RT;
        if (camera3D) camera3D.targetTexture = scene3D_RT;
    }

    void RecreateRenderTextures()
    {
        if (scene2D_RT != null) scene2D_RT.Release();
        if (scene3D_RT != null) scene3D_RT.Release();
        if (mask_RT != null) mask_RT.Release();

        CreateRenderTextures();
    }

    public void AddPortal(Vector2 screenPos, float radius)
    {
        portals.Add(new RevealPortal { position = screenPos, radius = radius, active = true });
    }

    public void ClearPortals()
    {
        portals.Clear();
    }

    public void UpdateMask()
    {
        if (circleMaskMaterial == null || mask_RT == null) return;

        RenderTexture.active = mask_RT;
        GL.Clear(true, true, Color.black);

        Mesh quad = CreateQuad();

        foreach (var portal in portals)
        {
            if (!portal.active) continue;

            circleMaskMaterial.SetVector("_Center", new Vector4(
                portal.position.x / Screen.width,
                portal.position.y / Screen.height, 0, 0));
            circleMaskMaterial.SetFloat("_Radius", portal.radius / Screen.width);

            circleMaskMaterial.SetPass(0);
            Graphics.DrawMeshNow(quad, Matrix4x4.identity);
        }

        RenderTexture.active = null;
    }

    Mesh CreateQuad()
    {
        Mesh quad = new Mesh();
        quad.vertices = new Vector3[]
        {
            new Vector3(-1,-1,0),
            new Vector3(1,-1,0),
            new Vector3(1,1,0),
            new Vector3(-1,1,0)
        };
        quad.uv = new Vector2[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1)
        };
        quad.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        return quad;
    }

    void OnDestroy()
    {
        if (scene2D_RT != null) scene2D_RT.Release();
        if (scene3D_RT != null) scene3D_RT.Release();
        if (mask_RT != null) mask_RT.Release();
    }
}

[System.Serializable]
public class RevealPortal
{
    public Vector2 position;
    public float radius;
    public bool active;
}