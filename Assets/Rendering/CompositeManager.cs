using UnityEngine;

public class CompositeManager : Singleton<CompositeManager>
{
	Camera cameraA;
	Camera cameraB;
	RenderTexture rtA;
	RenderTexture rtB;
	int lastWidth, lastHeight;
	bool initialized = false;
	RenderTexture maskRT;
	MaskDrawer maskDrawer;

	//Sorta lazy initialization where the cameras find this manager and only then initializes
	public void RegisterCamera(Camera cam, int index) {
		if (index == 0) cameraA = cam;
		else if (index == 1) cameraB = cam;

		//When we change cameras and we unregister then register a new one
		if (initialized) {
			if (index == 0 && rtA != null) cam.targetTexture = rtA;
			else if (index == 1 && rtB != null) cam.targetTexture = rtB;
		} else if (cameraA != null && cameraB != null) {
			var go = new GameObject("CompositeOutputCamera");
			go.transform.SetParent(transform);
			var outputCam = go.AddComponent<Camera>();
			outputCam.depth = 420; //after the main 2 cameras
			outputCam.cullingMask = 0;
			outputCam.clearFlags = CameraClearFlags.Nothing;
			outputCam.allowHDR = false;
			outputCam.allowMSAA = false;
			outputCam.useOcclusionCulling = false;

			maskDrawer = gameObject.AddComponent<MaskDrawer>();

			CreateRenderTextures();
			initialized = true;
		}
	}
	public void UnregisterCamera(int index) {
		if (index == 0) cameraA = null;
		else if (index == 1) cameraB = null;
	}

	void Update() {
		if (!initialized) return;

		if (Screen.width != lastWidth || Screen.height != lastHeight) {
			ReleaseRTs();
			CreateRenderTextures();
		}
	}

	void CreateRenderTextures() {
		lastWidth = Screen.width;
		lastHeight = Screen.height;

		rtA = new RenderTexture(lastWidth, lastHeight, 24);
		rtB = new RenderTexture(lastWidth, lastHeight, 24);
		var maskDesc = new RenderTextureDescriptor(lastWidth, lastHeight, RenderTextureFormat.RGInt, 0);
		maskDesc.sRGB = false; //idk otherwise you get an annoying warning in the log
		maskRT = new RenderTexture(maskDesc);
		maskRT.filterMode = FilterMode.Bilinear;

		cameraA.targetTexture = rtA;
		cameraB.targetTexture = rtB;

		Shader.SetGlobalTexture("_CameraA_Tex", rtA);
		Shader.SetGlobalTexture("_CameraB_Tex", rtB);
		Shader.SetGlobalTexture("_MaskTex", maskRT);

		maskDrawer.Configure(cameraA, cameraB, maskRT);
	}

	void ReleaseRTs() {
		if (rtA != null) rtA.Release();
		if (rtB != null) rtB.Release();
		if (maskRT != null) maskRT.Release();
	}

	void OnDestroy() {
		ReleaseRTs();
	}
}
