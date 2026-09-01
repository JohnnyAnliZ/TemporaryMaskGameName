using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider), typeof(AudioSource))]
public class GoalTrigger : MonoBehaviour {
	public Rigidbody target;
	public SpriteRenderer banner;
	public AudioClip song;
	public float fadeOutTime = 2f;
	public float panDuration = 30f;
	public float startXOffset;
	public float endXOffset;
	public float endHold;

	static readonly int FadeToBlackId = Shader.PropertyToID("_FadeToBlack");
	AnimationCurve panEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	bool bReached;

	void OnTriggerEnter(Collider other) {
		if (bReached || other.attachedRigidbody != target) return;
		bReached = true;
		StartCoroutine(RunCredits());
	}

	IEnumerator RunCredits() {
		GameManager.Instance.bInputEnabled = false;
		Cursors.Set(CursorLockMode.None);

		yield return FadeOut();

		FindAnyObjectByType<CameraFollow2D>().enabled = false;
		GameManager.Instance.player2D.SetActive(false);

		Camera cam = CompositeManager.Instance.cameraA;
		Bounds bounds = banner.bounds;
		cam.orthographicSize = bounds.extents.y;

		float halfView = cam.orthographicSize * cam.aspect;
		float z = bounds.center.z - (cam.nearClipPlane + cam.farClipPlane) * 0.5f;
		Vector3 from = new Vector3(bounds.min.x + halfView + startXOffset, bounds.center.y, z);
		Vector3 to = new Vector3(bounds.max.x - halfView + endXOffset, bounds.center.y, z);
		cam.transform.position = from;

		CompositeManager.Instance.maskDrawer.ResetMask();

		AudioSource source = GetComponent<AudioSource>();
		source.clip = song;
		source.spatialBlend = 0f;
		source.ignoreListenerVolume = true;
		source.Play();

		for (float time = 0f; time < panDuration; time += Time.deltaTime) {
			cam.transform.position = Vector3.Lerp(from, to, panEase.Evaluate(time / panDuration));
			yield return null;
		}
		cam.transform.position = to;

		float songFrom = source.volume;
		for (float time = 0f; time < endHold; time += Time.deltaTime) {
			source.volume = Mathf.Lerp(songFrom, 0f, time / endHold);
			yield return null;
		}
		source.volume = 0f;
		
		#if UNITY_EDITOR
		AudioListener.volume = 1f;
		UnityEditor.EditorApplication.isPlaying = false;
		#else
		Application.Quit();
		#endif
	}

	IEnumerator FadeOut() {
		float listenerFrom = AudioListener.volume;
		for (float time = 0f; time < fadeOutTime; time += Time.deltaTime) {
			float u = Mathf.Clamp01(time / fadeOutTime);
			Shader.SetGlobalFloat(FadeToBlackId, u);
			AudioListener.volume = Mathf.Lerp(listenerFrom, 0f, u);
			yield return null;
		}
		Shader.SetGlobalFloat(FadeToBlackId, 1f);
		AudioListener.volume = 0f;
	}
}