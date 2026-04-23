using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRatioLocker : MonoBehaviour
{
	public float targetAspect = 16f / 9f;

	Camera cam;
	Camera background;

	void Awake() {
		cam = GetComponent<Camera>();

		GameObject bg = new GameObject("LetterboxBackground");
		bg.transform.SetParent(transform, false);
		background = bg.AddComponent<Camera>();
		background.clearFlags = CameraClearFlags.SolidColor;
		background.backgroundColor = Color.black;
		background.cullingMask = 0;
		background.depth = cam.depth - 1;
		background.allowHDR = false;
		background.allowMSAA = false;
		background.useOcclusionCulling = false;
	}

	void LateUpdate() {
		float windowAspect = (float)Screen.width / Screen.height;
		float scaleHeight = windowAspect / targetAspect;
		Rect rect = cam.rect;
		if (scaleHeight < 1f) {
			//window too tall -> letterbox top/bottom
			rect.width = 1f;
			rect.height = scaleHeight;
			rect.x = 0f;
			rect.y = (1f - scaleHeight) * 0.5f;
		} else {
			//window too wide -> pillarbox left/right
			float scaleWidth = 1f / scaleHeight;
			rect.width = scaleWidth;
			rect.height = 1f;
			rect.x = (1f - scaleWidth) * 0.5f;
			rect.y = 0f;
		}
		cam.rect = rect;
	}
}
