using UnityEngine;

//Basically we treat the 3d player as the source of truth and simply copy transform instead of directing input to here as well

//		(back)
//		  0
//		  |
//		  |
// (left) 3----------------1 (right)
//		  |
//		  |
//		  2
//	       (front)

public class Player2DVisual : MonoBehaviour {
	public bool snapToPixelGrid = true;

	Transform source;
	Transform lookTransform;
	Animator animator;
	SpriteRenderer spriteRenderer;
	CharacterController characterController;

	public void Init(Transform source) {
		this.source = source;
		characterController = source.GetComponent<CharacterController>();
		animator = GetComponent<Animator>();
		spriteRenderer = GetComponent<SpriteRenderer>();
		lookTransform = FindAnyObjectByType<FirstPersonLook>().transform;
	}

	void LateUpdate() {
		if (source == null) return;
		Globals g = Globals.Instance;

		float y = source.position.y;
		y += characterController.center.y - (characterController.height * 0.5f);
		Vector3 position = new Vector3(source.position.x, y, g.world2DZ);

		if (snapToPixelGrid) {
			float gridSize = g.pixelGridSize;
			position.x = Mathf.Round(position.x / gridSize) * gridSize;
			position.y = Mathf.Round(position.y / gridSize) * gridSize;
		}

		transform.position = position;

		float zOffset = source.position.z - g.world3DZ;
		float denom = Mathf.Max(g.depthScale + zOffset, 0.01f); //avoid sign flip across pole
		float scale = g.depthScale / denom;
		scale = Mathf.Clamp(scale, g.playerMin, g.playerMax) * g.playerScale;
		transform.localScale = Vector3.one * scale;

		if (animator != null && lookTransform != null) {
			float yaw = lookTransform.eulerAngles.y;
			int direction = Mathf.RoundToInt(yaw / 90f) % 4;
			animator.SetFloat("direction", direction); //blend trees must use float not int

			if (spriteRenderer != null) {
				spriteRenderer.flipX = direction == 3;
			}
		}
	}
}
