using UnityEngine;

// MaskDrawer.cs - Handles just the mask drawing
public class MaskDrawer : MonoBehaviour
{
    public Material circleMaskMaterial;
    private Mesh quadMesh;
    

    void Start()
    {
        CreateQuadMesh();
    }

    void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-1, -1, 0),
            new Vector3(1, -1, 0),
            new Vector3(1, 1, 0),
            new Vector3(-1, 1, 0)
        };
        quadMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
    }

    public void ClearMask(RenderTexture target)
    {
        RenderTexture.active = target;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
    }

    public void DrawCircle(RenderTexture target, Vector2 screenPos, float radius)
    {
        RenderTexture.active = target;

        GL.PushMatrix();
        GL.LoadOrtho();

        circleMaskMaterial.SetVector("_Center", new Vector4(
            screenPos.x / Screen.width,
            screenPos.y / Screen.height,
            0, 0
        ));
        circleMaskMaterial.SetFloat("_Radius", radius / Screen.width);

        circleMaskMaterial.SetPass(0);
        
        GL.PopMatrix();
        RenderTexture.active = null;
    }
}