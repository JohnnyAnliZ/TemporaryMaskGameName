using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject player3DPrefab;
	public GameObject player2DPrefab;

	GameObject player3D;
	GameObject player2D;

	void Start() {
		var playerStart = FindAnyObjectByType<PlayerStart>();
		Vector3 spawnPos = playerStart != null ? playerStart.transform.position : new Vector3(0, 2, 0);
		Quaternion spawnRot = playerStart != null ? playerStart.transform.rotation : Quaternion.identity;

		Vector3 spawn3D = new Vector3(spawnPos.x, spawnPos.y, 20f);
		player3D = Instantiate(player3DPrefab, spawn3D, spawnRot);

		Vector3 spawn2D = new Vector3(spawnPos.x, spawnPos.y, 0f);
		player2D = Instantiate(player2DPrefab, spawn2D, spawnRot);
		player2D.GetComponent<Player2DVisual>().Init(player3D.transform);

		var cameras = FindObjectsByType<CompositeCamera>(FindObjectsSortMode.None);
		foreach (var camera in cameras) {
			if (camera.index == 0) {
				camera.gameObject.AddComponent<CameraFollow2D>().Init(player2D.transform);
			} else if (camera.index == 1) {
				camera.gameObject.AddComponent<FirstPersonLook>().Init(player3D.transform);
			}
		}
	}
}
