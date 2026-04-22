using UnityEngine;

public class GameManager : Singleton<GameManager>
{
	public bool bInputEnabled = true;
	SectionRunner runner;

	public GameObject player3DPrefab, player2DPrefab;
	GameObject player3D, player2D;

	public void AdvanceSubsection() {
		if (runner != null) runner.Advance();
	}

	void Start() {
		Globals g = Globals.Instance;

		//Section panel -> GameManager
		bool bSpawnFromPanel = false;
		Section startSection = Section.Intro;
		#if UNITY_EDITOR
		int raw = UnityEditor.SessionState.GetInt("startSection", -1);
		UnityEditor.SessionState.SetInt("startSection", -1); //reset back to "false" value cuz session state is until editor close
		bSpawnFromPanel = raw >= 0;
		if (bSpawnFromPanel) startSection = (Section)raw;
		#endif

		GameObject reference = GameObject.Find("Reference");
		if (reference != null) reference.SetActive(false);

		SectionStart sectionStart = null;
		foreach (SectionStart s in FindObjectsByType<SectionStart>(FindObjectsSortMode.None)) {
			if (s.section == startSection) {
				sectionStart = s;
				break;
			}
		}

		Vector3 fallbackPos = sectionStart != null ? sectionStart.transform.position : new Vector3(0, 0, 0);
		Quaternion spawnRot = sectionStart != null ? sectionStart.transform.rotation : Quaternion.identity;
		float spawnX = fallbackPos.x;
		float spawnY = fallbackPos.y;
		float spawnZ = g.world3DZ;

		#if UNITY_EDITOR
		if (!bSpawnFromPanel) {
			var sceneView = UnityEditor.SceneView.lastActiveSceneView;
			if (sceneView != null) {
				spawnX = sceneView.camera.transform.position.x;
				float camZ = sceneView.camera.transform.position.z;
				int[] offsets = {-1, 1, 0, -2, 2, 3, -3, 4, -4, 5, -5};
				bool hitFound = false;
				foreach (int off in offsets) {
					float rayZ = g.world3DZ + (off*3);
					if (Rays.Cast(new Vector3(spawnX, 100, rayZ), Vector3.down, out RaycastHit hit, 150, visualize: true)) {
						spawnY = hit.point.y + 2;
						spawnZ = rayZ;
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
					spawnZ = closest.spawnPoint.position.z;
				}
			}
		}
		#endif

		Vector3 spawn3D = new Vector3(spawnX, spawnY, spawnZ);
		player3D = Instantiate(player3DPrefab, spawn3D, spawnRot);
		player3D.name = "3DPlayer";

		Vector3 spawn2D = new Vector3(spawnX, spawnY, g.world2DZ);
		player2D = Instantiate(player2DPrefab, spawn2D, spawnRot);
		player2D.name = "2DPlayer";

		GameObject camera3D = new GameObject("3DCamera");
		camera3D.SetActive(false);
		camera3D.AddComponent<Camera>();
		camera3D.AddComponent<CompositeCamera>().index = 1;
		camera3D.AddComponent<FirstPersonLook>().Init(player3D.transform);
		camera3D.AddComponent<AudioListener>();
		camera3D.SetActive(true);

		player2D.GetComponent<Player2DVisual>().Init(player3D.transform); //create FirstPersonLook before Player2DVisual.Init()

		GameObject camera2D = new GameObject("2DCamera");
		camera2D.SetActive(false); //so that OnEnable runs after CompositeCamera component is added
		Camera cam = camera2D.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = g.cameraOrthoSize;
		cam.nearClipPlane = g.camera2DNearClip;
		cam.farClipPlane = g.camera2DFarClip;
		camera2D.AddComponent<CompositeCamera>().index = 0;
		CameraFollow2D follow = camera2D.AddComponent<CameraFollow2D>();
		follow.Init(player2D.transform, player3D.transform);
		camera2D.SetActive(true);

		//Sections
		#if !UNITY_EDITOR
		bSpawnFromPanel = true;
		#endif
		if (bSpawnFromPanel) {
			runner = gameObject.AddComponent<SectionRunner>();
			runner.Init(cam, follow);

			SectionAsset sectionAsset = Resources.Load<SectionAsset>($"Sections/Section_{startSection}");
			if (sectionAsset != null) runner.PlaySection(sectionAsset);
			else Log.Warn($"No SectionAsset at Resources/Sections/Section_{startSection}");
		}
	}
}
