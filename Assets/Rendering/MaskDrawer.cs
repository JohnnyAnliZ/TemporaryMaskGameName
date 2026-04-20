using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

public enum MaskMode { Portals, TwoD, ThreeD, Split }

public class MaskDrawer : MonoBehaviour
{
	public Material circleMaskMaterial;
	public float pixelSize = 8f; //FIXME: should prob hook up to ppu scriptable object
	public float expandSpeed = 300f;
	public MaskMode mode;

	List<RevealPortal> portals = new List<RevealPortal>();
	int total_passes = 8;
	int current_pass_index = 0;
	CommandBuffer cmd;

	void LateUpdate() {
		var maskRT = CompositeManager.Instance?.maskRT;
		if (maskRT == null) return;

		if (cmd == null) cmd = new CommandBuffer { name = "MaskDraw" };
		cmd.Clear();
		cmd.SetRenderTarget(maskRT);

		if (mode == MaskMode.TwoD) {
			cmd.ClearRenderTarget(true, true, Color.black);
			Graphics.ExecuteCommandBuffer(cmd);
			return;
		}
		if (mode == MaskMode.ThreeD) {
			cmd.ClearRenderTarget(true, true, Color.white);
			Graphics.ExecuteCommandBuffer(cmd);
			return;
		}
		if (mode == MaskMode.Split) {
			cmd.ClearRenderTarget(true, true, Color.black);
			cmd.SetViewport(new Rect(maskRT.width * 0.5f, 0, maskRT.width * 0.5f, maskRT.height));
			cmd.ClearRenderTarget(false, true, Color.white);
			Graphics.ExecuteCommandBuffer(cmd);
			return;
		}

		if (circleMaskMaterial == null) return;
		cmd.ClearRenderTarget(true, true, Color.black);

		cmd.SetGlobalVector("_Resolution", new Vector4(maskRT.width, maskRT.height, 0, 0));
		cmd.SetGlobalFloat("_PixelSize", pixelSize);

        GameObject c = GameObject.Find("Camera2D");
        Debug.Log("Camera2D, position of x, y are: " + c.transform.position.x +"  "+ c.transform.position.y);
		cmd.SetGlobalVector("_CameraPos", new Vector2(c.transform.position.x, c.transform.position.y));
        cmd.SetGlobalInt("_NumPasses", total_passes);
        for (int i = 0; i < Mathf.Min(current_pass_index, total_passes); i++)
        {
			
            cmd.SetGlobalInt("_PassIndex", i);
            cmd.DrawProcedural(Matrix4x4.identity, circleMaskMaterial, 0, MeshTopology.Triangles, 3, 1);
        }

		//this is for drawing portal by clicking on the screen, might use later for other stuff

		//foreach (RevealPortal portal in portals) {
		//	portal.currentRadius = Mathf.MoveTowards(portal.currentRadius, portal.targetRadius, expandSpeed * Time.deltaTime);
		//	cmd.SetGlobalVector("_Center", new Vector4(
		//		portal.position.x / maskRT.width,
		//		portal.position.y / maskRT.height, 0, 0));
		//	cmd.SetGlobalFloat("_Radius", portal.currentRadius / maskRT.width);
			
		//}

		Graphics.ExecuteCommandBuffer(cmd);
	}

	public void AddPortal(Vector2 screenPos, float radius) {
		portals.Add(new RevealPortal { position = screenPos, targetRadius = radius });
	}

	public void Do_Shatter()
	{
		current_pass_index += 1;
	}
	public void ClearPortals() {
		portals.Clear();
	}
}

public class RevealPortal
{
	public Vector2 position;
	public float targetRadius;
	public float currentRadius;
}
