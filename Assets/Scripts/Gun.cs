using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


[RequireComponent(typeof(Collider), typeof(AudioSource))]
public class GunController : Singleton<GunController> {
	[Header("Pickup")]
	public float spinSpeed = 90f;

	[Header("Viewmodel")]
	public Transform muzzle;
	public Vector3 viewmodelLocalPos = new(0.25f, -0.22f, 0.4f);
	public Vector3 viewmodelLocalEuler;

	[Header("Firing")]
	public float fireInterval = 0.25f;
	public LayerMask shootMask = ~0;
	public float recoilKick = 0.06f;
	public float recoilReturn = 12f;

	[Header("Muzzle Flash")]
	public Material flashMaterial;
	public float flashSize = 0.15f;
	public float flashDuration = 0.04f;
	public Color flashLightColor = new(1f, 0.8f, 0.45f);
	public float flashLightIntensity = 5f;
	public float flashLightRange = 8f;

	[Header("Tracer")]
	public Material tracerMaterial;
	public Color tracerColor = new(1f, 0.9f, 0.6f, 0.35f);
	public float tracerWidth = 0.02f;
	public float tracerSpeed = 120f;
	public float tracerLength = 2f;

	[Header("Sound")]
	public AudioClip pickupClip;
	public AudioClip[] shotClips;
	public Vector2 shotPitchRange = new(0.94f, 1.06f);

	[Header("Impact")]
	public ParticleSystem impactPrefab;
	public float impactLifetime = 2f;

	[Header("Decals")]
	public Material decalMaterial;
	public float decalSize = 0.08f;
	[Range(0f, 1f)] public float decalSizeVariance = 0.3f;
	[Range(0f, 1f)] public float decalAlphaMin = 0.6f;
	public int maxDecals = 32;

	public bool bLogShots;

	bool bEquipped;
	bool bPendingEquip;
	Camera cam3D;
	Renderer[] gunRenderers;
	bool bVisible = true;
	Transform flash;
	Light flashLight;
	float flashTimer;
	AudioSource shotSource;
	LineRenderer tracer;
	Vector3 tracerStart, tracerDir;
	float tracerDist, tracerTravel;
	float lastShotTime;
	float recoil;

	//Ring buffer
	MeshRenderer[] decals;
	int nextDecal;
	MaterialPropertyBlock decalProps;
	Color decalBaseColor;
	static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

	//Flagged here, done in Update. Equip flips both composite cameras' enabled state, and doing that from
	//inside a physics callback left URP's UberPost pass resolving a texture handle that no longer existed.
	void OnTriggerEnter(Collider other) {
		if (!bEquipped && other.GetComponentInParent<Player3DController>() != null) {
			bPendingEquip = true;
		}
	}

	void Equip() {
		bEquipped = true;
		GetComponent<Collider>().enabled = false;

		shotSource = GetComponent<AudioSource>();
		shotSource.PlayOneShot(pickupClip);

		CompositeManager.Instance.maskDrawer.ResetMask3D();

		cam3D = FindAnyObjectByType<FirstPersonLook>().GetComponent<Camera>();
		transform.SetParent(cam3D.transform);
		transform.localPosition = viewmodelLocalPos;
		transform.localEulerAngles = viewmodelLocalEuler;
		gunRenderers = GetComponentsInChildren<Renderer>(true); //before the flash exists, so it stays separate

		int noFog = LayerMask.NameToLayer("NoFog");

		//One unit quad in the XY plane, front face toward -Z like Unity's own Quad, shared by the flash card
		//and every decal. Both of them orient by pointing +Z away from the viewer.
		Mesh quad = new Mesh { name = "GunQuad" };
		quad.vertices = new[] {
			new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
		};
		quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
		quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
		quad.RecalculateNormals();

		GameObject flashGO = new GameObject("MuzzleFlash");
		flashGO.layer = noFog;

		flashGO.transform.SetParent(cam3D.transform, false);
		flashGO.transform.localScale = Vector3.one * flashSize;
		flashGO.AddComponent<MeshFilter>().sharedMesh = quad;
		flashGO.AddComponent<MeshRenderer>().sharedMaterial = flashMaterial;
		flashLight = flashGO.AddComponent<Light>();
		flashLight.color = flashLightColor;
		flashLight.range = flashLightRange;
		flashLight.shadows = LightShadows.None;
		flash = flashGO.transform;
		flashGO.SetActive(false);

		GameObject tracerGO = new GameObject("Tracer");
		tracerGO.layer = noFog;
		tracer = tracerGO.AddComponent<LineRenderer>();
		tracer.sharedMaterial = tracerMaterial;
		tracer.widthMultiplier = tracerWidth;
		tracer.positionCount = 2;
		tracer.useWorldSpace = true;
		tracer.numCapVertices = 0;
		tracer.shadowCastingMode = ShadowCastingMode.Off;
		tracer.receiveShadows = false;
		tracerGO.SetActive(false);

		//Decal pool
		decalProps = new MaterialPropertyBlock();
		decalBaseColor = decalMaterial.GetColor(BaseColorId);
		Transform decalRoot = new GameObject("BulletDecals").transform;
		decals = new MeshRenderer[maxDecals];
		for (int i = 0; i < maxDecals; i++) {
			GameObject d = new GameObject("Decal");
			d.layer = noFog;
			d.transform.SetParent(decalRoot, false);
			d.AddComponent<MeshFilter>().sharedMesh = quad;
			decals[i] = d.AddComponent<MeshRenderer>();
			decals[i].sharedMaterial = decalMaterial;
			d.SetActive(false);
		}
	}

	void Update() {
		if (!bEquipped) {
			if (bPendingEquip) Equip();
			else transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
			return;
		}

		bool bLive = GameManager.Instance.bInputEnabled && cam3D.enabled;
		if (bVisible != bLive) {
			bVisible = bLive;
			foreach (Renderer r in gunRenderers) r.enabled = bLive;
		}

		if (flashTimer > 0f) {
			flashTimer -= Time.deltaTime;
			flash.position = muzzle.position;
			flashLight.intensity = flashLightIntensity * Mathf.Max(flashTimer / flashDuration, 0f);
			if (flashTimer <= 0f) flash.gameObject.SetActive(false);
		}

		if (!bLive) return;

		recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-recoilReturn * Time.deltaTime));
		transform.localPosition = viewmodelLocalPos - Vector3.forward * recoil;

		//Holding the button keeps firing
		if (Mouse.current.leftButton.isPressed && Time.time - lastShotTime >= fireInterval) Fire();

		//The round in flight: a streak of tracerLength whose head runs ahead at tracerSpeed. Both ends clamp to
		//the shot line, so it grows out of the muzzle, flies, then disappears into the impact rather than
		//hanging in the air. Gone once the tail arrives.
		//Placed after Fire so a new shot gets its first step this frame -- otherwise its first rendered frame
		//is a zero-length line, and the tracer reads as ramping in rather than leaving the barrel at speed.
		if (tracer.gameObject.activeSelf) {
			tracerTravel += tracerSpeed * Time.deltaTime;
			float tail = tracerTravel - tracerLength;
			if (tail >= tracerDist) tracer.gameObject.SetActive(false);
			else {
				tracer.SetPosition(0, tracerStart + tracerDir * Mathf.Max(tail, 0f));
				tracer.SetPosition(1, tracerStart + tracerDir * Mathf.Min(tracerTravel, tracerDist));
			}
		}
	}

	void Fire() {
		lastShotTime = Time.time;
		recoil = recoilKick;

		shotSource.pitch = Random.Range(shotPitchRange.x, shotPitchRange.y);
		shotSource.PlayOneShot(shotClips[Random.Range(0, shotClips.Length)]);

		Transform cam = cam3D.transform;


		flash.gameObject.SetActive(true);
		flash.position = muzzle.position;
		flash.rotation = Quaternion.LookRotation(flash.position - cam.position) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
		flashLight.intensity = flashLightIntensity;
		flashTimer = flashDuration;

		bool bHit = Rays.Cast(cam.position, cam.forward, out RaycastHit hit, 100f, shootMask,
			QueryTriggerInteraction.Ignore, visualize: bLogShots, visualizeDuration: 2f);

		tracerStart = muzzle.position;
		Vector3 delta = (bHit ? hit.point : cam.position + cam.forward * 100f) - tracerStart;
		tracerDir = delta.normalized;
		tracerDist = delta.magnitude;
		tracerTravel = 0f;

		Color tail = tracerColor;
		tail.a = 0f;
		tracer.startColor = tail;
		tracer.endColor = tracerColor;
		tracer.gameObject.SetActive(true);

		if (bLogShots) {
			Log.Info(bHit
				? $"hit {hit.collider.name} ({LayerMask.LayerToName(hit.collider.gameObject.layer)}) at {hit.distance:F1}m"
				: "missed", screen: true);
		}
		if (!bHit) return;

		ParticleSystem impact = Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
		impact.Play();
		Destroy(impact.gameObject, impactLifetime);

		//Static geometry only
		if (hit.collider.attachedRigidbody == null) {
			MeshRenderer decal = decals[nextDecal];
			nextDecal = (nextDecal + 1) % decals.Length;
			decal.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f,
				Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
			decal.transform.localScale = Vector3.one * decalSize
				* Random.Range(1f - decalSizeVariance, 1f + decalSizeVariance);


			Color c = decalBaseColor;
			c.a *= Random.Range(decalAlphaMin, 1f);
			decalProps.SetColor(BaseColorId, c);
			decal.SetPropertyBlock(decalProps);

			decal.gameObject.SetActive(true);
		}

		hit.collider.GetComponentInParent<Shootable>()?.Hit(cam.forward, hit.point);
	}
}
