using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GameManager : Singleton<GameManager>
{
	public GameObject player3DPrefab, player2DPrefab;
	[HideInInspector]
	public GameObject player3D, player2D;

	[HideInInspector] public SectionRunner runner;
	public bool bInputEnabled = true;

	void Start() {
		Globals g = Globals.Instance;

		bool bSpawnFromPanel = false;
		Section startSection = Section.Intro;
		int startSubsection = 0;

		//Kind of hacky way to communicate between editor panel and unity play mode system
		#if UNITY_EDITOR
		int raw = UnityEditor.SessionState.GetInt("startSection", -1);
		startSubsection = UnityEditor.SessionState.GetInt("startSubsection", 0);
		UnityEditor.SessionState.SetInt("startSection", -1); //reset back to "false" value cuz session state is until editor close
		UnityEditor.SessionState.SetInt("startSubsection", 0);
		bSpawnFromPanel = raw >= 0;
		if (bSpawnFromPanel) startSection = (Section)raw;
		#endif

		GameObject.Find("Reference")?.SetActive(false); //hide the 2d reference image

		GameplayStart startPoint = default;
		if (startSection == Section.Gameplay) {
			SectionAsset asset = Resources.Load<SectionAsset>($"Sections/Section_{startSection}");
			if (asset != null && startSubsection < asset.subsections.Count
				&& asset.subsections[startSubsection] is GameplaySubsection gs) startPoint = gs.start;
		}

		SectionStart sectionStart = null;
		foreach (SectionStart s in FindObjectsByType<SectionStart>(FindObjectsSortMode.None)) {
			if (s.section != startSection) continue;
			if (startSection == Section.Gameplay && s.gameplayStart != startPoint) continue;
			sectionStart = s;
			break;
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
		//Identity, not spawnRot: this is a billboard sprite facing the orthographic 2D camera, and nothing
		//ever rewrites its rotation. Inheriting the marker's yaw turns it edge-on and it vanishes.
		player2D = Instantiate(player2DPrefab, spawn2D, Quaternion.identity);
		player2D.name = "2DPlayer";

		GameObject.Find("2DVolume").layer = LayerMask.NameToLayer("PP2D");
		GameObject.Find("3DVolume").layer = LayerMask.NameToLayer("PP3D");

		GameObject camera3D = new GameObject("3DCamera");
		camera3D.SetActive(false);
		Camera cam3D = camera3D.AddComponent<Camera>();
		cam3D.nearClipPlane = 0.01f;
		cam3D.GetUniversalAdditionalCameraData().SetRenderer(1);
		cam3D.GetUniversalAdditionalCameraData().renderPostProcessing = true;
		cam3D.GetUniversalAdditionalCameraData().volumeLayerMask = LayerMask.GetMask("PP3D", "Default");
		camera3D.AddComponent<CompositeCamera>().index = 1;
		camera3D.AddComponent<FirstPersonLook>().Init(player3D.transform);
		camera3D.AddComponent<AudioListener>();
		camera3D.SetActive(true);

		GameObject hand3D = GameObject.Find("hand");
		hand3D.transform.SetParent(camera3D.transform);
		//move hand in front of camera to debug
		hand3D.transform.localPosition = new Vector3(0, -0.4f, 0.1f);

		player2D.GetComponent<Player2DVisual>().Init(player3D.transform); //create FirstPersonLook before Player2DVisual.Init()

		GameObject camera2D = new GameObject("2DCamera");
		camera2D.SetActive(false); //so that OnEnable runs after CompositeCamera component is added
		Camera cam = camera2D.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = g.cameraOrthoSize;
		cam.nearClipPlane = g.camera2DNearClip;
		cam.farClipPlane = g.camera2DFarClip;
		cam.GetUniversalAdditionalCameraData().SetRenderer(0);
		cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
		cam.GetUniversalAdditionalCameraData().volumeLayerMask = LayerMask.GetMask("PP2D", "Default");
		cam.GetUniversalAdditionalCameraData().requiresDepthTexture = false;
		cam.allowMSAA = false;

		camera2D.AddComponent<CompositeCamera>().index = 0;
		CameraFollow2D follow = camera2D.AddComponent<CameraFollow2D>();
		follow.Init(player2D.transform, player3D.transform);
		camera2D.AddComponent<StreakBlurDriver>().enabled = false;
		CutscenePlayer cutscenePlayer = camera2D.AddComponent<CutscenePlayer>();
		cutscenePlayer.Init(cam, follow);
		camera2D.SetActive(true);

		//Sections
		#if !UNITY_EDITOR
		bSpawnFromPanel = true;
		#endif
		if (bSpawnFromPanel) {
			player2D.SetActive(false);
			if (runner == null) runner = gameObject.AddComponent<SectionRunner>();
			runner.Init(cutscenePlayer);
			runner.PlaySection(startSection, startSubsection);
		}
	}
}
