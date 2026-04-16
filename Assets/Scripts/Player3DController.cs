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
	Platform lastPlatform;

	void Awake() {
		controller = GetComponent<CharacterController>();
	}

	void Start() {
		lookTransform = FindAnyObjectByType<FirstPersonLook>().transform;
	}

	void Update() {
		Keyboard keyboard = Keyboard.current;

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

		if (controller.isGrounded) {
			if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f)) {
				lastPlatform = hit.collider.GetComponentInParent<Platform>();
			}
		}

		if (transform.position.y < Globals.Instance.fallThreshold) {
			controller.enabled = false;
			transform.position = lastPlatform.spawnPoint.position;
			lookTransform.rotation = Quaternion.identity;
			verticalVelocity = 0f;
			controller.enabled = true;
		}
	}

	void OnControllerColliderHit(ControllerColliderHit hit) {
		if (hit.gameObject.TryGetComponent<PlatformPortalTrigger>(out PlatformPortalTrigger trigger)) {
			trigger.TryTrigger(transform.position);
		}
	}

	void OnDrawGizmos() {
		if (controller == null) controller = GetComponent<CharacterController>();
		if (controller == null) return;

		Gizmos.color = new Color(1, 0, 0, 0.5f);
		Vector3 center = transform.position + controller.center;
		float radius = controller.radius;
		float height = controller.height;

		//Capsule
		float halfHeight = height * 0.5f - radius;
		Vector3 top = center + Vector3.up * halfHeight;
		Vector3 bottom = center + Vector3.down * halfHeight;

		//Top and bottom spheres
		Gizmos.DrawWireSphere(top, radius);
		Gizmos.DrawWireSphere(bottom, radius);

		//Vertical lines connecting the spheres
		Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
		Gizmos.DrawLine(top + Vector3.back * radius, bottom + Vector3.back * radius);
		Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
		Gizmos.DrawLine(top + Vector3.left * radius, bottom + Vector3.left * radius);
	}
}
