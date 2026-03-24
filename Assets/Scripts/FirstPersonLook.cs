using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonLook : MonoBehaviour
{
	public float mouseSensitivity = 2f;
	public float eyeOffset = 0.6f;
	public float pitchClamp = 85f;

	Transform target;
	float yaw, pitch;

	//Called by GameManager
	public void Init(Transform target) {
		this.target = target;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void LateUpdate() {
		if (target == null) return;

		transform.position = target.position + Vector3.up * eyeOffset;

		Vector2 delta = Mouse.current.delta.ReadValue();
		yaw += delta.x * mouseSensitivity;
		pitch -= delta.y * mouseSensitivity;
		pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

		transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}
}
