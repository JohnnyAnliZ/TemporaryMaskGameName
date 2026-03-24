using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player3DController : MonoBehaviour
{
	public float moveSpeed = 5f;
	public float jumpForce = 10f;
	public float gravity = -20f;

	CharacterController controller;
	float verticalVelocity;

	void Awake() {
		controller = GetComponent<CharacterController>();
	}

	void Update() {
		var keyboard = Keyboard.current;

		//FIXME: We should prob use the actual input event system instead of this
		float forward = 0f, horizontal = 0f;
		if (keyboard.wKey.isPressed) forward += 1f;
		if (keyboard.sKey.isPressed) forward -= 1f;
		if (keyboard.aKey.isPressed) horizontal -= 1f;
		if (keyboard.dKey.isPressed) horizontal += 1f;

		if (controller.isGrounded && verticalVelocity < 0f) {
			verticalVelocity = -1f;
		}
		if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame) {
			verticalVelocity = jumpForce;
		}
		verticalVelocity += gravity * Time.deltaTime;

		Vector3 move = new Vector3(horizontal * moveSpeed, verticalVelocity, forward * moveSpeed);
		controller.Move(move * Time.deltaTime);
	}

	void OnControllerColliderHit(ControllerColliderHit hit) {
		if (hit.gameObject.TryGetComponent<PlatformPortalTrigger>(out var trigger))
			trigger.TryTrigger(transform.position);
	}
}
