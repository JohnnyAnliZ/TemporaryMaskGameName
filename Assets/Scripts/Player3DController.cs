using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player3DController : MonoBehaviour
{
	CharacterController controller;
	Transform lookTransform;
	float verticalVelocity;
	float coyoteTimer;
	float jumpBufferTimer;
	float spaceTimer;
	bool bIsHoldingSpace;
	Vector3 jumpBoost;
	Platform lastPlatform;

	// Footstep audio
	float footstepTimer;
	float footstepInterval;

	void Awake() {
		controller = GetComponent<CharacterController>();
	}

	void Start() {
		lookTransform = FindAnyObjectByType<FirstPersonLook>().transform;
		footstepInterval = AudioManager.Instance.footstepInterval;
	}

	void Update() {
		if (!GameManager.Instance.bInputEnabled) return;

		Globals g = Globals.Instance;
		Keyboard keyboard = Keyboard.current;

		float forward = 0f, horizontal = 0f;
		if (keyboard.wKey.isPressed) forward += 1f;
		if (keyboard.sKey.isPressed) forward -= 1f;
		if (keyboard.aKey.isPressed) horizontal -= 1f;
		if (keyboard.dKey.isPressed) horizontal += 1f;
		bool bSpaceJustPressed = keyboard.spaceKey.wasPressedThisFrame;

		//Coyote
		if (controller.isGrounded) coyoteTimer = g.coyoteTime;
		else coyoteTimer -= Time.deltaTime;

		//Jump buffer
		if (bSpaceJustPressed) jumpBufferTimer = g.jumpBufferTime;
		else jumpBufferTimer -= Time.deltaTime;

		//Input
		Vector3 inputDir = new Vector3(horizontal, 0f, forward);
		if (inputDir != Vector3.zero) {
			Vector3 fwd = lookTransform.forward;
			fwd.y = 0f;
			fwd.Normalize();
			Vector3 right = lookTransform.right;
			right.y = 0f;
			right.Normalize();
			inputDir = right * horizontal + fwd * forward;
		}

		if (controller.isGrounded) jumpBoost = Vector3.zero;

		//Start spaceTimer, to check if we should charge or not
		if (!bIsHoldingSpace && jumpBufferTimer > 0f && coyoteTimer > 0f) {
			bIsHoldingSpace = true;
			spaceTimer = 0f;
			jumpBufferTimer = 0f;
		}

		if (bIsHoldingSpace) {
			spaceTimer = Mathf.Min(spaceTimer + Time.deltaTime, g.jumpChargeTime);
			//Release
			if (!keyboard.spaceKey.isPressed) {
				//Charge
				float chargeTime = 0f;
				if (spaceTimer > g.jumpTapWindow) {
					float range = g.jumpChargeTime - g.jumpTapWindow;
					chargeTime = range > 0f ? Mathf.Clamp01((spaceTimer - g.jumpTapWindow) / range) : 1f;
				}
				verticalVelocity = Mathf.Lerp(g.jumpForceMin, g.jumpForceMax, chargeTime);
				jumpBoost = inputDir.normalized * g.jumpForwardBoost * chargeTime; //a lil forward boost
				bIsHoldingSpace = false;
				spaceTimer = 0f;
				coyoteTimer = 0f;
			}
		}

		if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f; //ensure grounding

		//Dynamic gravity
		float blendTime = Mathf.Clamp01(Mathf.InverseLerp(g.fallGravityBlend, -g.fallGravityBlend, verticalVelocity));
		blendTime = blendTime * blendTime * (3f - 2f * blendTime); //cubic smoothstep
		float effectiveGravity = g.gravity * Mathf.Lerp(g.riseGravityMulti, g.fallGravityMulti, blendTime);
		verticalVelocity += effectiveGravity * Time.deltaTime;

		float multiplier = controller.isGrounded ? 1f : g.airControl;
		if (bIsHoldingSpace && (spaceTimer > g.jumpTapWindow)) multiplier *= g.chargeMoveMulti; //slow walk when charging
		Vector3 move = inputDir * g.moveSpeed * multiplier + jumpBoost;
		move.y = verticalVelocity;
		controller.Move(move * Time.deltaTime);

		if (controller.isGrounded) {
			if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f)) {
				lastPlatform = hit.collider.GetComponentInParent<Platform>();
			}
		}

		if (transform.position.y < g.fallThreshold) {
			controller.enabled = false;
			transform.position = lastPlatform.spawnPoint.position;
			lookTransform.rotation = Quaternion.identity;
			verticalVelocity = 0f;
			controller.enabled = true;
		}

		// Update footstep sounds
		UpdateFootsteps(inputDir);
	}

	void UpdateFootsteps(Vector3 movementDirection) {
		// Only play footsteps if grounded and moving
		bool isMoving = movementDirection != Vector3.zero && controller.isGrounded;

		if (isMoving) {
			footstepTimer -= Time.deltaTime;

			if (footstepTimer <= 0f) {
				AudioManager.Instance.PlayFootstep();
				footstepTimer = footstepInterval;
			}
		} else {
			// Reset timer if not moving
			footstepTimer = 0f;
		}
	}

	void OnControllerColliderHit(ControllerColliderHit hit) {
		if (hit.gameObject) {

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
