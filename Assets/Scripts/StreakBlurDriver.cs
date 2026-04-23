using UnityEngine;

public class StreakBlurDriver : MonoBehaviour
{
	public float speedToStrength = 0.15f; //0-1
	public float streakLength = 0.25f; //uv space
	public float streakBands = 0f;
	public float smoothing = 12f;

	static readonly int BlurId = Shader.PropertyToID("_BlurStrength");
	static readonly int StreakLengthId = Shader.PropertyToID("_StreakLength");
	static readonly int StreakBandsId = Shader.PropertyToID("_StreakBands");

	Vector3 lastPos;
	float smoothed;

	void OnEnable() {
		lastPos = transform.position;
		smoothed = 0f;
		Shader.SetGlobalFloat(BlurId, 0f);
		Shader.SetGlobalFloat(StreakLengthId, streakLength);
		Shader.SetGlobalFloat(StreakBandsId, streakBands);
	}

	void OnDisable() {
		Shader.SetGlobalFloat(BlurId, 0f);
	}

	void LateUpdate() {
		float dt = Mathf.Max(Time.deltaTime, 0.0001f);
		float speed = (transform.position - lastPos).magnitude / dt;
		lastPos = transform.position;

		float target = Mathf.Clamp01(speed * speedToStrength);
		smoothed = Mathf.Lerp(smoothed, target, 1f - Mathf.Exp(-smoothing * dt));
		Shader.SetGlobalFloat(BlurId, smoothed);
		Shader.SetGlobalFloat(StreakLengthId, streakLength);
		Shader.SetGlobalFloat(StreakBandsId, streakBands);
	}
}
