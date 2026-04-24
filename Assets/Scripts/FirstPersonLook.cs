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
		if (lookTarget == null) yield break;
		Vector3 dir = (lookTarget.position - transform.position).normalized;
		float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
		float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
		yield return PanTo(targetYaw, targetPitch, duration);
	}
}
