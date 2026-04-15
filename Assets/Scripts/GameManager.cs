using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject player3DPrefab, player2DPrefab;
	GameObject player3D, player2D;

	void Start() {
		Globals g = Globals.Instance;
		PlayerStart playerStart = FindAnyObjectByType<PlayerStart>();
		Vector3 spawnPos = playerStart != null ? playerStart.transform.position : new Vector3(0, 2, 0);
		Quaternion spawnRot = playerStart != null ? playerStart.transform.rotation : Quaternion.identity;

		Vector3 spawn3D = new Vector3(spawnPos.x, spawnPos.y, g.world3DZ);
		player3D = Instantiate(player3DPrefab, spawn3D, spawnRot);

		Vector3 spawn2D = new Vector3(spawnPos.x, spawnPos.y, g.world2DZ);
		player2D = Instantiate(player2DPrefab, spawn2D, spawnRot);
		player2D.GetComponent<Player2DVisual>().Init(player3D.transform);

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

		GameObject camera3D = new GameObject("Camera3D");
		camera3D.SetActive(false);
		camera3D.AddComponent<Camera>();
		camera3D.AddComponent<CompositeCamera>().index = 1;
		camera3D.AddComponent<FirstPersonLook>().Init(player3D.transform);
		camera3D.SetActive(true);
	}
}
