using UnityEngine;

//Pretty cool way to have a universal manager
public static class Bootstrap {
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Init() {
		LoadPrefab<CompositeManager>("CompositeManager");
		LoadPrefab<GameManager>("GameManager");
	}

	static void LoadPrefab<T>(string resourceName) where T : MonoBehaviour {
		var prefab = Resources.Load<T>(resourceName);
		if (prefab == null) {
			Debug.LogError($"Bootstrap: Missing Resources/{resourceName} prefab.");
			return;
		}

		var instance = Object.Instantiate(prefab);
		instance.name = resourceName;
		Object.DontDestroyOnLoad(instance.gameObject);
	}
}
