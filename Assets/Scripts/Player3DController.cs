using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player3DController : MonoBehaviour
{
	public float moveSpeed = 5f;
	public float jumpForce = 10f;
	public float gravity = -20f;

	CharacterController controller;
	Transform lookTransform;
	float verticalVelocity;

	void Awake() {
		controller = GetComponent<CharacterController>();
	}

	void Start() {
		var fpLook = FindAnyObjectByType<FirstPersonLook>();
		if (fpLook != null) lookTransform = fpLook.transform;
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

		Vector3 inputDir = new Vector3(horizontal, 0f, forward);
		if (lookTransform != null && inputDir != Vector3.zero) {
			Vector3 fwd = lookTransform.forward;
			fwd.y = 0f;
			fwd.Normalize();
			Vector3 right = lookTransform.right;
			right.y = 0f;
			right.Normalize();
			inputDir = right * horizontal + fwd * forward;
		}

		Vector3 move = inputDir * moveSpeed;
		move.y = verticalVelocity;
		controller.Move(move * Time.deltaTime);
	}

	void OnControllerColliderHit(ControllerColliderHit hit) {
		if (hit.gameObject.TryGetComponent<PlatformPortalTrigger>(out var trigger)) {
			trigger.TryTrigger(transform.position);
		}
	}
}
