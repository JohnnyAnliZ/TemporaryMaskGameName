using UnityEngine;
using UnityEngine.SceneManagement;

//Pretty cool way to have a universal manager
public static class Bootstrap {
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Init() {
		string scene = SceneManager.GetActiveScene().name;
		if (System.Array.IndexOf(Globals.Instance.gameLevels, scene) < 0) return;

		LoadPrefab<CompositeManager>("CompositeManager");
		LoadPrefab<GameManager>("GameManager");
		LoadPrefab<AudioManager>("AudioManager");
		LoadPrefab<VideoManager>("VideoManager");
	}

	static void LoadPrefab<T>(string resourceName) where T : MonoBehaviour {
		var prefab = Resources.Load<T>(resourceName);
		if (prefab == null) {
			Log.Error($"Missing Resources/{resourceName} prefab");
			return;
		}

		var instance = Object.Instantiate(prefab);
		instance.name = resourceName;
		Object.DontDestroyOnLoad(instance.gameObject);
	}
}
