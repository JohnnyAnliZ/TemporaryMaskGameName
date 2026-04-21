using UnityEngine;

[DefaultExecutionOrder(100)]
public class ParallaxLayer : MonoBehaviour
{
	[Range(0f, 2f)] public float factorMultiplier = 1f;
	[Range(0f, 2f)] public float scaleFactorMultiplier = 1f;
	public Transform syncPoint;

	Camera cam;
	Vector3 restPosition;
	Vector3 restScale;
	float initialOrthoSize;
	bool bIsInitialized;

	void LateUpdate() {
		if (!bIsInitialized && !TryInit()) return;

		Globals g = Globals.Instance;
		float globalFactor = g.GetParallaxFactorForZ(restPosition.z);
		float factor = Mathf.Clamp(globalFactor * factorMultiplier, 0f, 2f);
		float scaleBlend = Mathf.Clamp01(g.parallaxScaleFactor * scaleFactorMultiplier);

		Vector3 anchorPos = syncPoint != null ? syncPoint.position : restPosition;
		Vector2 anchorXY = new Vector2(anchorPos.x, anchorPos.y);
		Vector2 camXY = new Vector2(cam.transform.position.x, cam.transform.position.y);
		Vector2 restXY = new Vector2(restPosition.x, restPosition.y);
		Vector2 layerXY = restXY + (camXY - anchorXY) * (1f - factor);

		transform.position = new Vector3(layerXY.x, layerXY.y, restPosition.z);

		float zoomRatio = cam.orthographicSize / initialOrthoSize;
		float counterScale = Mathf.Pow(zoomRatio, (1f - globalFactor) * scaleBlend);
		transform.localScale = new Vector3(restScale.x * counterScale, restScale.y * counterScale, restScale.z);
	}

	bool TryInit() {
		CameraFollow2D follow = FindAnyObjectByType<CameraFollow2D>();
		if (follow == null) return false;
		cam = follow.GetComponent<Camera>();
		if (cam == null) return false;

		restPosition = transform.position;
		restScale = transform.localScale;
		initialOrthoSize = Globals.Instance.cameraOrthoSize;
		bIsInitialized = true;
		return true;
	}

	void OnDrawGizmosSelected() {
		Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
		Gizmos.DrawWireSphere(syncPoint.position, 0.25f);
		Gizmos.DrawLine(syncPoint.position + Vector3.left * 0.5f, syncPoint.position + Vector3.right * 0.5f);
		Gizmos.DrawLine(syncPoint.position + Vector3.down * 0.5f, syncPoint.position + Vector3.up * 0.5f);
	}
}
