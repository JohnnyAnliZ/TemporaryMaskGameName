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
	Material outlineMaterial;
	static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
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
		for (int i = 0; i < 4; i++) {
			GameObject go = new GameObject("Outline_" + i);
			go.transform.SetParent(transform, false);
			SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
			sr.sharedMaterial = outlineMaterial;
			sr.sortingLayerID = spriteRenderer.sortingLayerID;
			sr.sortingOrder = spriteRenderer.sortingOrder - 1;
			sr.enabled = false;
			outlineRenderers[i] = sr;
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

		float spriteZ = g.world2DZ + (zOffset * g.spriteZPerPlayerZ); //for interaction with foreground layers
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

		//Directions
		float yaw = lookTransform.eulerAngles.y;
		int direction = Mathf.RoundToInt(yaw / 90f) % 4;
		animator.SetFloat("direction", direction); //blend trees must use float not int

		spriteRenderer.flipX = direction == 3;

		//Outline
		float offCardinal = Mathf.Abs(Mathf.DeltaAngle(yaw, direction * 90f));
		float outlineT = 1f - Mathf.Clamp01((offCardinal - g.angleFull) / Mathf.Max(g.angleFade, 0.001f));
		if (outlineT > 0f) {
			Sprite currentSprite = spriteRenderer.sprite;
			bool currentFlip = spriteRenderer.flipX;
			float texelLocal = g.outlineThickness / g.pixelsPerUnit;
			Color outCol = g.outlineColor;
			outCol.a *= outlineT * alpha * g.outlineMaxOpacity;
			outlineMaterial.SetColor(OutlineColorId, outCol);
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
