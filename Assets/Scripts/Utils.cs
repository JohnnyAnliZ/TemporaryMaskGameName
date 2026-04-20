using UnityEngine;

public static class Rays {
	public static bool Cast(Vector3 origin, Vector3 direction, out RaycastHit hit,
		float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
		QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal,
		bool visualize = false, float visualizeDuration = 0f) {
		bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, layerMask, triggerInteraction);
		#if UNITY_EDITOR
		if (visualize) {
			Vector3 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
			float drawLen = float.IsInfinity(maxDistance) ? 1000f : maxDistance;
			float duration = visualizeDuration <= 0f ? 1e9f : visualizeDuration;
			if (didHit) {
				Debug.DrawLine(origin, hit.point, Color.green, duration, false);
			}
			else {
				Debug.DrawRay(origin, dir * drawLen, Color.red, duration, false);
			}
		}
		#endif
		return didHit;
	}
}
