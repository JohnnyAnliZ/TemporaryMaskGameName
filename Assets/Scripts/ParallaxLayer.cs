using UnityEngine;

[DefaultExecutionOrder(100)]
public class ParallaxLayer : MonoBehaviour
{
	[Range(0f, 1f)] public float factor = 0.5f;

	Camera cam;
	Vector3 anchor;
	float initialOrthoSize;
	bool initialized;

	void LateUpdate() {
		if (!initialized && !TryInit()) return;

		Vector2 camXY = new Vector2(cam.transform.position.x, cam.transform.position.y);
		Vector2 anchorXY = new Vector2(anchor.x, anchor.y);
		Vector2 layerXY = anchorXY + (camXY - anchorXY) * (1f - factor);

		transform.position = new Vector3(layerXY.x, layerXY.y, anchor.z);

		float zoomRatio = cam.orthographicSize / initialOrthoSize;
		float counterScale = Mathf.Pow(zoomRatio, 1f - factor);
		transform.localScale = new Vector3(counterScale, counterScale, 1f);
	}

	bool TryInit() {
		var follow = FindAnyObjectByType<CameraFollow2D>();
		if (follow == null) return false;
		cam = follow.GetComponent<Camera>();
		if (cam == null) return false;

		anchor = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);
		initialOrthoSize = Globals.Instance.cameraOrthoSize;

		int count = transform.childCount;
		Vector3[] worlds = new Vector3[count];
		for (int i = 0; i < count; i++) worlds[i] = transform.GetChild(i).position;
		transform.position = anchor;
		for (int i = 0; i < count; i++) transform.GetChild(i).position = worlds[i];

		initialized = true;
		return true;
	}
}
