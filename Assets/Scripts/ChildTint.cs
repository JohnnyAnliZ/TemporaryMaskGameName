using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ChildTint : MonoBehaviour
{
	public Color tint = Color.white;

	[SerializeField, HideInInspector] List<SpriteRenderer> cached = new();
	[SerializeField, HideInInspector] List<Color> baselines = new();

	void OnEnable() { Apply(); }
	void OnValidate() { Apply(); }

	[ContextMenu("Recapture Baselines")]
	void Capture() {
		cached.Clear();
		baselines.Clear();
		foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>()) {
			cached.Add(sr);
			baselines.Add(sr.color);
		}
	}

	void Apply() {
		if (cached.Count == 0) Capture();
		for (int i = 0; i < cached.Count; i++) {
			if (cached[i] == null) continue;
			cached[i].color = baselines[i] * tint;
		}
	}
}
