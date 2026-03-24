using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CompositeCamera : MonoBehaviour
{
	public int index;

	void OnEnable() {
		if (CompositeManager.Instance != null) {
			CompositeManager.Instance.RegisterCamera(GetComponent<Camera>(), index);
		}
	}
	void OnDisable() {
		if (CompositeManager.Instance != null) {
			CompositeManager.Instance.UnregisterCamera(index);
		}
	}
}
