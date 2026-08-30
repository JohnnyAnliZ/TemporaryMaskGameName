using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player3DController : MonoBehaviour
{
	CharacterController controller;
	FirstPersonLook look;
	Transform lookTransform;
	float groundedStepOffset;
	float verticalVelocity;
	float coyoteTimer;
	float airTimer;
	bool bJumped;
	float jumpBufferTimer;
	float spaceTimer;
	bool bIsHoldingSpace;
	Vector3 horizontalVelocity;
	Platform lastPlatform;
	Vector3 spawnPoint;
	bool bInputWasOn;

	static readonly int FadeToBlackId = Shader.PropertyToID("_FadeToBlack");
	float fadeAmount;

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

		Vector3 origin = pos + Vector3.up;
		if (Physics.SphereCast(origin, controller.radius, Vector3.down, out RaycastHit hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
			float bottomSphereY = origin.y - hit.distance;
			pos.y = bottomSphereY + (controller.height * 0.5f - controller.radius) - controller.center.y + controller.skinWidth;
		}
		transform.position = pos;
		controller.enabled = true;

		controller.Move(Vector3.down * (controller.skinWidth * 2f));

		verticalVelocity = 0f;
		horizontalVelocity = Vector3.zero;
		coyoteTimer = 0f;
		airTimer = 0f;
		bJumped = false;
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
		groundedStepOffset = controller.stepOffset;
	}

	void Start() {
		look = FindAnyObjectByType<FirstPersonLook>();
		lookTransform = look.transform;
	}

	void Update() {
		Globals g = Globals.Instance;
		float dt = Time.deltaTime;

		if (controller.isGrounded) {
			airTimer = 0f;
			bJumped = false;
		} else airTimer += dt;
		bool bGrounded = controller.isGrounded || (!bJumped && airTimer < g.groundedGraceTime);

		Animator animator = GameManager.Instance.player2D.GetComponent<Animator>();
		animator?.SetBool("isGrounded", bGrounded);

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
			bInputWasOn = false;
			animator?.SetBool("isMoving", false);
			animator?.SetBool("isGrounded", true);
			return;
		}
		bool bInputJustEnabled = !bInputWasOn;
		bInputWasOn = true;

		Keyboard keyboard = Keyboard.current;

		float forward = 0f, horizontal = 0f;
		if (keyboard.wKey.isPressed) forward += 1f;
		if (keyboard.sKey.isPressed) forward -= 1f;
		if (keyboard.aKey.isPressed) horizontal -= 1f;
		if (keyboard.dKey.isPressed) horizontal += 1f;
		//wasPressedThisFrame only fires on the frame the key goes down, and that edge is lost entirely if the
		//press happened while input was off - through a respawn fade, or a cutscene. Treat the key already
		//being held on the first frame back as a fresh press, otherwise the first jump after control returns
		//does nothing and the key has to be released and pressed again.
		bool bSpaceJustPressed = keyboard.spaceKey.wasPressedThisFrame || (bInputJustEnabled && keyboard.spaceKey.isPressed);

		//Coyote
		if (bGrounded) coyoteTimer = g.coyoteTime;
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

		animator?.SetBool("isMoving", inputDir != Vector3.zero);

		//Start spaceTimer, to check if we should charge or not
		if (!bIsHoldingSpace && jumpBufferTimer > 0f && coyoteTimer > 0f) {
			bIsHoldingSpace = true;
			spaceTimer = 0f;
			jumpBufferTimer = 0f;
		}

		//Cancel coyote
		if (bIsHoldingSpace && coyoteTimer <= 0f) {
			bIsHoldingSpace = false;
			spaceTimer = 0f;
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
				horizontalVelocity += inputDir.normalized * g.jumpForwardBoost * chargeTime; //a lil forward boost
				bIsHoldingSpace = false;
				spaceTimer = 0f;
				coyoteTimer = 0f;
				bJumped = true;
			}
		}

		if (controller.isGrounded) {
			if (isFalling) {
				AudioManager.Instance.HandleImpact(verticalVelocity);
				look.AddLandingDip(verticalVelocity);
				Log.Info($"{verticalVelocity}");
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

		float multiplier = bGrounded ? 1f : g.airControl;
		if (bIsHoldingSpace && (spaceTimer > g.jumpTapWindow)) multiplier *= g.chargeMoveMulti; //slow walk when charging

		//Momentum
		Vector3 target = inputDir * g.moveSpeed * multiplier;
		float rate = inputDir != Vector3.zero
			? (bGrounded ? g.groundAccel : g.airAccel)
			: (bGrounded ? g.groundDecel : g.airDecel);
		horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, rate * dt);

		Vector3 move = horizontalVelocity;
		move.y = verticalVelocity;

		controller.stepOffset = controller.isGrounded ? groundedStepOffset : 0f;
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
			StartCoroutine(Respawn());
		}

		// Handle footstepsounds
		AudioManager.Instance.HandleFootsteps(inputDir, bGrounded);
	}

	System.Collections.IEnumerator Respawn() {
		Globals g = Globals.Instance;
		GameManager.Instance.bInputEnabled = false;

		yield return FadeToBlack(1f, g.respawnFadeOut);

		bool hasCheckpoint = lastPlatform != null && lastPlatform.spawnPoint != null;
		Teleport(hasCheckpoint ? lastPlatform.spawnPoint.position : spawnPoint);

		yield return new WaitForSeconds(g.respawnHold);
		yield return FadeToBlack(0f, g.respawnFadeIn);

		GameManager.Instance.bInputEnabled = true;
	}
	System.Collections.IEnumerator FadeToBlack(float target, float duration) {
		float start = fadeAmount;
		for (float t = 0f; t < duration; t += Time.deltaTime) {
			fadeAmount = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
			Shader.SetGlobalFloat(FadeToBlackId, fadeAmount);
			yield return null;
		}
		fadeAmount = target;
		Shader.SetGlobalFloat(FadeToBlackId, target);
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
