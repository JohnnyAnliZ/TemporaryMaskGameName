using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonLook : MonoBehaviour
{
	Transform target;
	float yaw, pitch;
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
}
