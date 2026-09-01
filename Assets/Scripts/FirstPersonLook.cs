using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonLook : MonoBehaviour
{
	Transform target;
	float yaw = 90f;
	float pitch;
	Vector2 mouseDelta;

	float dip;
	float dipVelocity;

	public void AddLandingDip(float impactVelocity) {
		var g = Globals.Instance;
		float u = Mathf.Clamp01(Mathf.InverseLerp(g.landingDipMinSpeed, g.landingDipMaxSpeed, -impactVelocity));
		dip = -g.landingDipMax * u;
		dipVelocity = 0f;
	}

	public void Init(Transform target) {
		this.target = target;
		Cursors.Set(CursorLockMode.Locked);
	}

	public void SetLook(float yawDegrees, float pitchDegrees = 0f) {
		yaw = yawDegrees;
		pitch = Mathf.Clamp(pitchDegrees, -Globals.Instance.pitchClamp, Globals.Instance.pitchClamp);
		mouseDelta = Vector2.zero;
		transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}
	public void SetLook(Quaternion rotation) {
		Vector3 e = rotation.eulerAngles;
		SetLook(e.y, e.x > 180f ? e.x - 360f : e.x);
	}

	void Update() {
		if (!GameManager.Instance.bInputEnabled) {
			mouseDelta = Vector2.zero;
			return;
		}

		if (Cursor.lockState == CursorLockMode.Locked) {
			mouseDelta += Mouse.current.delta.ReadValue();
		}
	}

	void LateUpdate() {
		if (target == null) return;
		var g = Globals.Instance;

		//Damped spring
		dipVelocity += (-dip * g.landingDipSpring - dipVelocity * g.landingDipDamping) * Time.deltaTime;
		dip += dipVelocity * Time.deltaTime;
		transform.position = target.position + Vector3.up * (g.eyeOffset + dip);

		yaw += mouseDelta.x * g.mouseSensitivity;
		pitch -= mouseDelta.y * g.mouseSensitivity;
		pitch = Mathf.Clamp(pitch, -g.pitchClamp, g.pitchClamp);
		mouseDelta = Vector2.zero;

		transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}

	public System.Collections.IEnumerator PanTo(float targetYaw, float targetPitch, float duration) {
		float startYaw = yaw;
		float startPitch = pitch;
		targetYaw = startYaw + Mathf.DeltaAngle(startYaw, targetYaw);
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
			yaw = Mathf.Lerp(startYaw, targetYaw, u);
			pitch = Mathf.Lerp(startPitch, targetPitch, u);
			yield return null;
		}
		yaw = targetYaw;
		pitch = targetPitch;
	}
	public System.Collections.IEnumerator PanToTarget(Transform lookTarget, float duration) {
		Globals g = Globals.Instance;
		float startYaw = yaw;
		float startPitch = pitch;

		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

			Vector3 dir = lookTarget.position - transform.position;
			if (dir.sqrMagnitude < 1e-6f) yield break;
			dir.Normalize();

			float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
			float targetPitch = Mathf.Clamp(-Mathf.Asin(dir.y) * Mathf.Rad2Deg, -g.pitchClamp, g.pitchClamp);

			yaw = Mathf.Lerp(startYaw, startYaw + Mathf.DeltaAngle(startYaw, targetYaw), u);
			pitch = Mathf.Lerp(startPitch, targetPitch, u);
			yield return null;
		}
	}
}
