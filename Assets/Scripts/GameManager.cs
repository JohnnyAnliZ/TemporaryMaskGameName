using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject player3DPrefab, player2DPrefab;
	GameObject player3D, player2D;

	void Start() {
		Globals g = Globals.Instance;
		PlayerStart playerStart = FindAnyObjectByType<PlayerStart>();
		Vector3 fallbackPos = playerStart != null ? playerStart.transform.position : new Vector3(0, 2, 0);
		Quaternion spawnRot = playerStart != null ? playerStart.transform.rotation : Quaternion.identity;
		float spawnX = fallbackPos.x;
		float spawnY = fallbackPos.y;

		#if UNITY_EDITOR
		if (g.spawnAtCamera) {
			var sceneView = UnityEditor.SceneView.lastActiveSceneView;
			if (sceneView != null) {
				spawnX = sceneView.camera.transform.position.x;
				float camZ = sceneView.camera.transform.position.z;
				int[] offsets = { -4, -3, -2, -1, 0, 1, 2, 3, 4 };
				System.Array.Sort(offsets, (a, b) => Mathf.Abs(camZ - (g.world3DZ + a)).CompareTo(Mathf.Abs(camZ - (g.world3DZ + b))));
				bool hitFound = false;
				foreach (int off in offsets) {
					if (Physics.Raycast(new Vector3(spawnX, 100, g.world3DZ + off), Vector3.down, out RaycastHit hit, 150)) {
						spawnY = hit.point.y + 2;
						hitFound = true;
						break;
					}
				}
				if (!hitFound) {
					Vector3 camPos = sceneView.camera.transform.position;
					Platform closest = null;
					float closestDist = float.MaxValue;
					foreach (Platform p in FindObjectsByType<Platform>(FindObjectsSortMode.None)) {
						if (p.spawnPoint == null) continue;
						float d = Vector3.Distance(p.spawnPoint.position, camPos);
						if (d < closestDist) {
							closestDist = d;
							closest = p;
						}
					}
					spawnX = closest.spawnPoint.position.x;
					spawnY = closest.spawnPoint.position.y;
				}
			}
		}
		#endif

		Vector3 spawn3D = new Vector3(spawnX, spawnY, g.world3DZ);
		player3D = Instantiate(player3DPrefab, spawn3D, spawnRot);

		Vector3 spawn2D = new Vector3(spawnX, spawnY, g.world2DZ);
		player2D = Instantiate(player2DPrefab, spawn2D, spawnRot);

		GameObject camera3D = new GameObject("Camera3D");
		camera3D.SetActive(false);
		camera3D.AddComponent<Camera>();
		camera3D.AddComponent<CompositeCamera>().index = 1;
		camera3D.AddComponent<FirstPersonLook>().Init(player3D.transform);
		camera3D.SetActive(true);

		player2D.GetComponent<Player2DVisual>().Init(player3D.transform); //create FirstPersonLook before Player2DVisual.Init()

		GameObject camera2D = new GameObject("Camera2D");
		camera2D.SetActive(false); //so that OnEnable runs after CompositeCamera component is added
		Camera cam = camera2D.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = g.cameraOrthoSize;
		cam.nearClipPlane = g.camera2DNearClip;
		cam.farClipPlane = g.camera2DFarClip;
		camera2D.AddComponent<CompositeCamera>().index = 0;
		camera2D.AddComponent<CameraFollow2D>().Init(player2D.transform);
		camera2D.SetActive(true);
	}
}
