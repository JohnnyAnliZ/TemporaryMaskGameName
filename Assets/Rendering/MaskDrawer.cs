using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class MaskDrawer : MonoBehaviour
{
	public Material circleMaskMaterial;
	public float pixelSize = 8f; //FIXME: should prob hook up to ppu scriptable object
	public float expandSpeed = 300f;

	List<RevealPortal> portals = new List<RevealPortal>();
	CommandBuffer cmd;

	void LateUpdate() {
		var maskRT = CompositeManager.Instance?.maskRT;
		if (maskRT == null || circleMaskMaterial == null) return;

		if (cmd == null) cmd = new CommandBuffer { name = "MaskDraw" };
		cmd.Clear();
		cmd.SetRenderTarget(maskRT);
		cmd.ClearRenderTarget(true, true, Color.black);

		circleMaskMaterial.SetVector("_Resolution", new Vector4(maskRT.width, maskRT.height, 0, 0));
		circleMaskMaterial.SetFloat("_PixelSize", pixelSize);

		foreach (RevealPortal portal in portals) {
			portal.currentRadius = Mathf.MoveTowards(portal.currentRadius, portal.targetRadius, expandSpeed * Time.deltaTime);
			circleMaskMaterial.SetVector("_Center", new Vector4(
				portal.position.x / maskRT.width,
				1f - portal.position.y / maskRT.height, 0, 0));
			circleMaskMaterial.SetFloat("_Radius", portal.currentRadius / maskRT.width);
			cmd.DrawProcedural(Matrix4x4.identity, circleMaskMaterial, 0, MeshTopology.Triangles, 3, 1); //will be nicely batched comapred to blit
		}

		Graphics.ExecuteCommandBuffer(cmd);
	}

	public void AddPortal(Vector2 screenPos, float radius) {
		portals.Add(new RevealPortal { position = screenPos, targetRadius = radius });
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
