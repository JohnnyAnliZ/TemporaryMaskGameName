using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CompositeManager : Singleton<CompositeManager>
{
	[HideInInspector] public Camera cameraA;
	[HideInInspector] public Camera cameraB;
	RenderTexture rtA;
	RenderTexture rtB;
	int lastInternalHeight;
	bool initialized = false;
	RenderTexture maskRT;
	public MaskDrawer maskDrawer;
	public Camera outputCam;

	//Modulation volume (for gameplay section)
	Volume wobbleVolume;
	ChromaticAberration wobbleCA;
	float caBase;
	public float wobbleMin = 0f;
	public float wobbleMax = 1f;
	public float wobbleSpeed = 1f;
	public float caMultiplier = 1f;

	[HideInInspector] public AspectRatioLocker aspectLocker;

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
			outputCam = go.AddComponent<Camera>();
			outputCam.depth = 420; //after the main 2 cameras
			outputCam.cullingMask = 0;
			outputCam.clearFlags = CameraClearFlags.Nothing;
			outputCam.allowHDR = false;
			outputCam.allowMSAA = false;
			outputCam.useOcclusionCulling = false;
			outputCam.GetUniversalAdditionalCameraData().requiresDepthTexture = false;
			outputCam.GetUniversalAdditionalCameraData().SetRenderer(2);
			aspectLocker = go.AddComponent<AspectRatioLocker>();

			maskDrawer = gameObject.AddComponent<MaskDrawer>();

			wobbleVolume = GameObject.Find("ModulationVolume").GetComponent<Volume>();
			wobbleVolume.profile.TryGet(out wobbleCA);
			caBase = wobbleCA.intensity.value;
			wobbleCA.intensity.max = 100f;

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

		//The buffers are window-independent now, so a resize costs nothing -- only a change to the internal
		//resolution needs a rebuild.
		if (Globals.Instance.internalHeight != lastInternalHeight) {
			ReleaseRTs();
			CreateRenderTextures();
		}

		bool bIs3D = maskDrawer.currentPass >= Globals.Instance.numBreaks;
		wobbleVolume.weight = bIs3D ? Mathf.Lerp(wobbleMin, wobbleMax, Mathf.PerlinNoise(Time.time * wobbleSpeed, 0f)) : 0f;
		wobbleCA.intensity.value = caBase * caMultiplier;
	}

	void CreateRenderTextures() {
		lastInternalHeight = Globals.Instance.internalHeight;

		//Fixed internal resolution at the target aspect, upscaled to the window by the composite pass. Only
		//sharpness rides on these dimensions -- framing comes from outputCam.rect and Composite.shader samples
		//over 0-1 UV -- so the buffers never have to track the window, and screen-space effects measured in
		//buffer pixels stay put across resolutions. AspectRatioLocker fills the black bars.
		int rtH = lastInternalHeight;
		int rtW = Mathf.RoundToInt(rtH * aspectLocker.targetAspect);

		rtA = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.DefaultHDR);
		rtB = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.DefaultHDR);
		rtA.filterMode = FilterMode.Bilinear; //live action wants the soft upscale
		rtB.filterMode = FilterMode.Point; //keeps DarkDither's Bayer cells hard-edged rather than smearing them
		var maskDesc = new RenderTextureDescriptor(rtW, rtH, RenderTextureFormat.RGHalf, 0);
		maskDesc.sRGB = false; //idk otherwise you get an annoying warning in the log
		maskRT = new RenderTexture(maskDesc);
		maskRT.filterMode = FilterMode.Bilinear;
		maskRT.wrapMode = TextureWrapMode.Clamp; //blur reach past edges
		maskRT.Create();

		ClearToBlack(rtA);
		ClearToBlack(rtB);
		ClearToBlack(maskRT);

		cameraA.targetTexture = rtA;
		cameraB.targetTexture = rtB;

		Shader.SetGlobalTexture("_CameraA_Tex", rtA);
		Shader.SetGlobalTexture("_CameraB_Tex", rtB);
		Shader.SetGlobalTexture("_MaskTex", maskRT);

		maskDrawer.Configure(cameraA, cameraB, maskRT);
	}

	static void ClearToBlack(RenderTexture rt) {
		RenderTexture prev = RenderTexture.active;
		RenderTexture.active = rt;
		GL.Clear(true, true, Color.black);
		RenderTexture.active = prev;
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
