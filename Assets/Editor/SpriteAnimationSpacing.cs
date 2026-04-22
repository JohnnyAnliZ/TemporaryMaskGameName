using UnityEditor;
using UnityEngine;

public class SpriteAnimationSpacingWindow : EditorWindow {
	int frameSpacing = 1;
	float frameRate = 60f;
	bool changeFrameRate = false;

	[MenuItem("Window/Sprite Animation Spacing")]
	static void Open() => GetWindow<SpriteAnimationSpacingWindow>("Sprite Anim Spacing");

	void OnGUI() {
		EditorGUILayout.HelpBox("Select one or more AnimationClip assets in the Project window, then Apply. Sprite keyframes are re-timed to (index * frameSpacing / frameRate).", MessageType.Info);

		frameSpacing = Mathf.Max(1, EditorGUILayout.IntField("Frame Spacing (samples)", frameSpacing));

		changeFrameRate = EditorGUILayout.Toggle("Change Frame Rate", changeFrameRate);
		using (new EditorGUI.DisabledScope(!changeFrameRate)) {
			frameRate = Mathf.Max(1f, EditorGUILayout.FloatField("New Frame Rate", frameRate));
		}

		AnimationClip[] selected = Selection.GetFiltered<AnimationClip>(SelectionMode.Assets);
		EditorGUILayout.LabelField($"Selected clips: {selected.Length}");
		foreach (AnimationClip c in selected) {
			EditorGUILayout.LabelField($"  - {c.name}   (frameRate: {c.frameRate})");
		}

		using (new EditorGUI.DisabledScope(selected.Length == 0)) {
			if (GUILayout.Button("Apply")) {
				foreach (AnimationClip clip in selected) Apply(clip, frameSpacing, changeFrameRate, frameRate);
				AssetDatabase.SaveAssets();
			}
		}
	}

	static void Apply(AnimationClip clip, int spacingFrames, bool setFrameRate, float newFrameRate) {
		if (setFrameRate) clip.frameRate = newFrameRate;

		EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
		bool anyChanged = false;
		foreach (EditorCurveBinding binding in bindings) {
			if (binding.propertyName != "m_Sprite") continue;

			ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
			if (keys == null || keys.Length == 0) continue;

			float step = spacingFrames / clip.frameRate;
			for (int i = 0; i < keys.Length; i++) {
				keys[i].time = i * step;
			}
			AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
			anyChanged = true;
		}
		if (anyChanged || setFrameRate) EditorUtility.SetDirty(clip);
	}
}
