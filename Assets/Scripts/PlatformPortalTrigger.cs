using UnityEngine;

public class PlatformPortalTrigger : MonoBehaviour
{
	public float portalRadius = 150f;

	Camera cameraA;
	MaskDrawer maskDrawer;
	bool triggered;

	void Start() {
		maskDrawer = FindAnyObjectByType<MaskDrawer>();

		var cameras = FindObjectsByType<CompositeCamera>(FindObjectsSortMode.None);
		foreach (var cc in cameras) {
			if (cc.index == 0) {
				cameraA = cc.GetComponent<Camera>();
				break;
			}
		}
	}

	public void TryTrigger(Vector3 playerWorldPos) {
		if (triggered) return;
		if (cameraA == null || maskDrawer == null) return;

		triggered = true;
		Vector3 screenPos = cameraA.WorldToScreenPoint(playerWorldPos);
		maskDrawer.AddPortal(new Vector2(screenPos.x, screenPos.y), portalRadius);
	}
}
