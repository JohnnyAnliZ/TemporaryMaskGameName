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

	SpriteRenderer[] outlineRenderers;
	MaterialPropertyBlock[] outlineMPBs;
	Material outlineMaterial;
	static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
	static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
	static readonly int OffsetDirId = Shader.PropertyToID("_OffsetDir");
	static readonly int UvMinId = Shader.PropertyToID("_UvMin");
	static readonly int UvMaxId = Shader.PropertyToID("_UvMax");
	static readonly Vector2[] outlineOffsets = {
		new Vector2(1, 0),
		new Vector2(-1, 0),
		new Vector2(0, 1),
		new Vector2(0, -1),
	};

	public void Init(Transform source) {
		this.source = source;
		characterController = source.GetComponent<CharacterController>();
		animator = GetComponent<Animator>();
		spriteRenderer = GetComponent<SpriteRenderer>();
		lookTransform = FindAnyObjectByType<FirstPersonLook>().transform;

		outlineMaterial = new Material(Shader.Find("Custom/SpriteOutline"));
		outlineRenderers = new SpriteRenderer[4];
		outlineMPBs = new MaterialPropertyBlock[4];
		for (int i = 0; i < 4; i++) {
			GameObject go = new GameObject("Outline_" + i);
			go.transform.SetParent(transform, false);
			SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
			sr.sharedMaterial = outlineMaterial;
			sr.sortingLayerID = spriteRenderer.sortingLayerID;
			sr.sortingOrder = spriteRenderer.sortingOrder;
			sr.enabled = false;
			outlineRenderers[i] = sr;

			//Each child samples main's alpha at (uv - offsetDir * texelSize * thickness); MPB carries the per-child direction.
			MaterialPropertyBlock mpb = new MaterialPropertyBlock();
			mpb.SetVector(OffsetDirId, new Vector4(outlineOffsets[i].x, outlineOffsets[i].y, 0f, 0f));
			sr.SetPropertyBlock(mpb);
			outlineMPBs[i] = mpb;
		}
	}

	void OnDestroy() {
		if (outlineMaterial != null) Destroy(outlineMaterial);
	}

	void LateUpdate() {
		if (source == null) return;
		Globals g = Globals.Instance;

		float zOffset = source.position.z - g.world3DZ;

		float y = source.position.y;
		y += characterController.center.y - (characterController.height * 0.5f); //account for capsule height

		float spriteZ = g.world2DZ + (zOffset * g.spriteZPerPlayerZ) + 24; //for interaction with foreground layers
		Vector3 position = new Vector3(source.position.x, y, spriteZ);

		// if (snapToPixelGrid) {
		// 	float gridSize = g.pixelGridSize;
		// 	position.x = Mathf.Round(position.x / gridSize) * gridSize;
		// 	position.y = Mathf.Round(position.y / gridSize) * gridSize;
		// }

		transform.position = position;

		//Depth Scale
		float denom = Mathf.Max(g.depthScale + zOffset, 0.01f); //avoid sign flip across pole
		float scale = g.depthScale / denom;
		scale = Mathf.Clamp(scale, g.playerMin, g.playerMax) * g.playerScale;
		transform.localScale = Vector3.one * scale;

		//Depth value and opacity
		float farT = Mathf.Clamp01(zOffset / g.fadeDistance);
		float nearT = Mathf.Clamp01(-zOffset / g.nearDistance);
		float alpha = Mathf.Lerp(1f, g.minOpacity, farT);
		float rgb = Mathf.Lerp(1f, g.farBrightness, farT) * Mathf.Lerp(1f, g.nearBrightness, nearT);
		spriteRenderer.color = new Color(rgb, rgb, rgb, alpha);

		//Directions. `direction` (walk/jump, 0-3) is pushed by Player3DController from world-space input yaw.
		//`idleDirection` (0-11) comes from look yaw — idle is pure look-based since there's no movement.
		float yaw = lookTransform.eulerAngles.y;
		int idleDirection = Mathf.RoundToInt(yaw / 30f) % 12;
		animator.SetFloat("idleDirection", idleDirection);

		int direction = Mathf.RoundToInt(animator.GetFloat("direction"));

		//Idle sprites mirror right→left for clockwise indices 3-5 and 10-11; walk/jump mirror only at cardinal left (dir 3).
		bool bIsIdle = animator.GetBool("isGrounded") && !animator.GetBool("isMoving");
		if (bIsIdle) {
			spriteRenderer.flipX = idleDirection >= 3 && idleDirection <= 5 || idleDirection == 10 || idleDirection == 11;
		} else {
			spriteRenderer.flipX = direction == 3;
		}

		//Outline — cardinal reference is look yaw, not movement, so outline thickens when camera looks axis-aligned.
		int lookCardinal = ((Mathf.RoundToInt(yaw / 90f) % 4) + 4) % 4;
		float offCardinal = Mathf.Abs(Mathf.DeltaAngle(yaw, lookCardinal * 90f));
		float outlineT = 1f - Mathf.Clamp01((offCardinal - g.angleFull) / Mathf.Max(g.angleFade, 0.001f));
		if (outlineT > 0f) {
			Sprite currentSprite = spriteRenderer.sprite;
			bool currentFlip = spriteRenderer.flipX;
			float texelLocal = g.outlineThickness / g.pixelsPerUnit;
			Color outCol = g.outlineColor;
			outCol.a *= outlineT * alpha * g.outlineMaxOpacity;
			outlineMaterial.SetColor(OutlineColorId, outCol);
			outlineMaterial.SetFloat(OutlineThicknessId, g.outlineThickness);

			//Pass the current sprite's atlas UV rect so the shader can bounds-check main sampling.
			if (currentSprite != null && currentSprite.texture != null) {
				Rect texRect = currentSprite.textureRect;
				float tw = currentSprite.texture.width;
				float th = currentSprite.texture.height;
				outlineMaterial.SetVector(UvMinId, new Vector4(texRect.xMin / tw, texRect.yMin / th, 0f, 0f));
				outlineMaterial.SetVector(UvMaxId, new Vector4(texRect.xMax / tw, texRect.yMax / th, 0f, 0f));
			}

			//flipX reverses UV→world X mapping: negate offsetDir.x so main-sampling lands on the correct world pixel.
			float flipMul = currentFlip ? -1f : 1f;
			for (int i = 0; i < 4; i++) {
				Vector2 d = outlineOffsets[i];
				outlineMPBs[i].SetVector(OffsetDirId, new Vector4(d.x * flipMul, d.y, 0f, 0f));
				outlineRenderers[i].SetPropertyBlock(outlineMPBs[i]);
			}
			for (int i = 0; i < 4; i++) {
				SpriteRenderer sr = outlineRenderers[i];
				sr.enabled = true;
				sr.sprite = currentSprite;
				sr.flipX = currentFlip;
				sr.transform.localPosition = new Vector3(outlineOffsets[i].x * texelLocal, outlineOffsets[i].y * texelLocal);
			}
		}
		else {
			for (int i = 0; i < 4; i++) outlineRenderers[i].enabled = false;
		}
	}
}
