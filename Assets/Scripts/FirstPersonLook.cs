using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonLook : MonoBehaviour
{
	Transform target;
	float yaw, pitch;

	public void Init(Transform target) {
		this.target = target;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void LateUpdate() {
		if (target == null) return;
		var g = Globals.Instance;

		transform.position = target.position + Vector3.up * g.eyeOffset;

		Vector2 delta = Mouse.current.delta.ReadValue();
		yaw += delta.x * g.mouseSensitivity;
		pitch -= delta.y * g.mouseSensitivity;
		pitch = Mathf.Clamp(pitch, -g.pitchClamp, g.pitchClamp);

		transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}
}
