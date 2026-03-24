using UnityEngine;
using System;
using System.Collections.Generic;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
	public static T Instance { get; private set; }

	static Singleton() {
		SingletonRegistry.Register(() => Instance = null);
	}

	protected virtual void Awake() {
		if (Instance != null && Instance != this) {
			Destroy(gameObject);
			return;
		}
		Instance = (T)this;
	}
}

//We need this because domain reload is disabled because it's too slow so we need to manually clear statics
public static class SingletonRegistry {
	static readonly List<Action> resets = new();

	public static void Register(Action reset) => resets.Add(reset);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetAll() {
		foreach (var r in resets) r();
	}
}
