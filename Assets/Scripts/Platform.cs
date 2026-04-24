using UnityEngine;

public class Platform : MonoBehaviour {
	[HideInInspector]
	public Transform spawnPoint;

	public bool bCanBreak;
	public bool bLastBreak;
	public bool bShrinkToBlack;
	[HideInInspector] public bool bIsBroken;
	[HideInInspector] public bool bHasShrunk;

	void Reset() {
		if (spawnPoint == null) {
			Transform found = transform.Find("SpawnPoint");
			if (found != null) {
				spawnPoint = found;
			} else {
				GameObject spawn = new GameObject("SpawnPoint");
				spawn.transform.SetParent(transform);

				Collider[] colliders = GetComponentsInChildren<Collider>();
				Bounds? bounds = null;
				foreach (Collider collider in colliders) {
					if (bounds == null) {
						bounds = collider.bounds;
					} else {
						Bounds b = bounds.Value;
						b.Encapsulate(collider.bounds);
						bounds = b;
					}
				}

				spawn.transform.position = new Vector3(0, 1.5f, 0) + (bounds?.center ?? transform.position);
				spawnPoint = spawn.transform;
			}
		}
	}

	void OnDrawGizmos() {
		Gizmos.color = new Color(0, 1, 1, 0.8f);
		Gizmos.DrawWireSphere(spawnPoint.position, 0.2f);
	}
}
