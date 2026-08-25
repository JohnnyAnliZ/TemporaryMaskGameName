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
	Vector3 spawnPoint;

	// Impact logic
	bool isFalling = false;

	//Shrink transition
	Vector3 floatStart, floatEnd;
	float floatElapsed, floatDuration;
	bool bFloating;
	public void BeginFloatTo(Vector3 target, float duration) {
		floatStart = transform.position;
		floatEnd = target;
		floatElapsed = 0f;
		floatDuration = Mathf.Max(0.001f, duration);
		bFloating = true;
		controller.enabled = false; //scripted move: no collision, no gravity
		GameManager.Instance.bInputEnabled = false;
	}

	public void Teleport(Vector3 pos) {
		controller.enabled = false;
		if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
			pos.y = hit.point.y - (controller.center.y - controller.height * 0.5f) + controller.skinWidth;
		}
		transform.position = pos;
		controller.enabled = true;

		verticalVelocity = 0f;
		jumpBoost = Vector3.zero;
		coyoteTimer = 0f;
		jumpBufferTimer = 0f;
		spaceTimer = 0f;
		bIsHoldingSpace = false;
		isFalling = false;
	}

	//The point a fall returns to before any platform is touched; set by the flow on entering an area.
	public void SetSpawnPoint(Vector3 pos) {
		spawnPoint = pos;
		lastPlatform = null;
	}

	void Awake() {
		controller = GetComponent<CharacterController>();
	}

	void Start() {
		lookTransform = FindAnyObjectByType<FirstPersonLook>().transform;
	}

	void Update() {
		Globals g = Globals.Instance;

		Animator animator = GameManager.Instance.player2D.GetComponent<Animator>();
		animator?.SetBool("isGrounded", controller.isGrounded);

		if (bFloating) {
			floatElapsed += Time.deltaTime;
			float u = Mathf.Clamp01(floatElapsed / floatDuration);
			transform.position = Vector3.Lerp(floatStart, floatEnd, u * u * (3f - 2f * u));
			if (floatElapsed >= floatDuration) {
				bFloating = false;
			}
			return;
		}

		bool bInputOn = GameManager.Instance.bInputEnabled;
		if (!bInputOn) {
			animator?.SetBool("isMoving", false);
			animator?.SetBool("isGrounded", true);
			return;
		}

		float dt = Time.deltaTime;

		Keyboard keyboard = Keyboard.current;

		float forward = 0f, horizontal = 0f;
		if (keyboard.wKey.isPressed) forward += 1f;
		if (keyboard.sKey.isPressed) forward -= 1f;
		if (keyboard.aKey.isPressed) horizontal -= 1f;
		if (keyboard.dKey.isPressed) horizontal += 1f;
		bool bSpaceJustPressed = keyboard.spaceKey.wasPressedThisFrame;

		//Coyote
		if (controller.isGrounded) coyoteTimer = g.coyoteTime;
		else coyoteTimer -= dt;

		//Jump buffer
		if (bSpaceJustPressed) jumpBufferTimer = g.jumpBufferTime;
		else jumpBufferTimer -= dt;

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

		animator?.SetBool("isMoving", inputDir != Vector3.zero);

		//Start spaceTimer, to check if we should charge or not
		if (!bIsHoldingSpace && jumpBufferTimer > 0f && coyoteTimer > 0f) {
			bIsHoldingSpace = true;
			spaceTimer = 0f;
			jumpBufferTimer = 0f;
		}

		if (bIsHoldingSpace) {
			spaceTimer = Mathf.Min(spaceTimer + dt, g.jumpChargeTime);
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

		if (controller.isGrounded) {
			if (isFalling) {
				AudioManager.Instance.HandleImpact(verticalVelocity);
				isFalling = false;
			}
		}

		if (!controller.isGrounded && verticalVelocity < -8f) {
			isFalling = true;
		}

		if (controller.isGrounded && verticalVelocity < 0f) {
			verticalVelocity = -1f; //ensure grounding
		}

		//Dynamic gravity
		float blendTime = Mathf.Clamp01(Mathf.InverseLerp(g.fallGravityBlend, -g.fallGravityBlend, verticalVelocity));
		blendTime = blendTime * blendTime * (3f - 2f * blendTime); //cubic smoothstep
		float effectiveGravity = g.gravity * Mathf.Lerp(g.riseGravityMulti, g.fallGravityMulti, blendTime);
		verticalVelocity += effectiveGravity * dt;

		float multiplier = controller.isGrounded ? 1f : g.airControl;
		if (bIsHoldingSpace && (spaceTimer > g.jumpTapWindow)) multiplier *= g.chargeMoveMulti; //slow walk when charging
		Vector3 move = inputDir * g.moveSpeed * multiplier + jumpBoost;
		move.y = verticalVelocity;
		controller.Move(move * dt);

		if (controller.isGrounded) {
			Platform platform = null;
			float sweep = controller.height * 0.5f - controller.radius + 0.25f;
			if (Physics.SphereCast(transform.position, controller.radius, Vector3.down, out RaycastHit hit, sweep,
					Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
				platform = hit.collider.GetComponentInParent<Platform>();
			}

			if (platform != null) {
				if (platform != lastPlatform) {
					if (platform.bCanBreak && !platform.bIsBroken) {
						platform.bIsBroken = true;
						if (platform.bLastBreak) CompositeManager.Instance.maskDrawer.Do_ShatterAll();
						else CompositeManager.Instance.maskDrawer.Do_Shatter();
					}
					if (platform.bShrinkToBlack && !platform.bHasShrunk) {
						platform.bHasShrunk = true;
						CompositeManager.Instance.maskDrawer.Do_ShrinkToBlack();
					}
				}
				lastPlatform = platform;
			}
		}

		if (transform.position.y < g.fallThreshold) {
			bool hasCheckpoint = lastPlatform != null && lastPlatform.spawnPoint != null;
			Teleport(hasCheckpoint ? lastPlatform.spawnPoint.position : spawnPoint);
			lookTransform.rotation = Quaternion.identity; //FIXME
		}

		// Handle footstepsounds
		AudioManager.Instance.HandleFootsteps(inputDir, controller.isGrounded);
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
