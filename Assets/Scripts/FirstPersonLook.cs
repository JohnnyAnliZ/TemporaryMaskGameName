using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonLook : MonoBehaviour
{
	Transform target;
	float yaw = 90f;
	float pitch;
	Vector2 mouseDelta;

	public void Init(Transform target) {
		this.target = target;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void Update() {
		if (Keyboard.current.escapeKey.wasPressedThisFrame) {
			bool locked = Cursor.lockState == CursorLockMode.Locked;
			Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
			Cursor.visible = locked;
		}

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

		transform.position = target.position + Vector3.up * g.eyeOffset;

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
		if (lookTarget == null) { Debug.LogWarning("PanToTarget: lookTarget is null — did you forget to assign it on the trigger?"); yield break; }

		Quaternion startRot = Quaternion.Euler(pitch, yaw, 0f);
		Vector3 dir = lookTarget.position - transform.position;
		if (dir.sqrMagnitude < 1e-6f) { Debug.LogWarning("PanToTarget: target is at camera position"); yield break; }
		Quaternion endRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
		Debug.Log($"PanToTarget: start=({pitch:F1},{yaw:F1}) end={endRot.eulerAngles} dur={duration}");

		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
			Quaternion cur = Quaternion.Slerp(startRot, endRot, u);
			Vector3 e = cur.eulerAngles;
			yaw = e.y;
			pitch = e.x > 180f ? e.x - 360f : e.x;
			yield return null;
		}
		Vector3 fe = endRot.eulerAngles;
		yaw = fe.y;
		pitch = fe.x > 180f ? fe.x - 360f : fe.x;
	}
}
