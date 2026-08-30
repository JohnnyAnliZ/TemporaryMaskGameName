using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;


public class MaskDrawer : MonoBehaviour
{
	CommandBuffer cmd;
	Camera cam2D;
	Camera cam3D;
	Camera overlayCam;
	RenderTexture maskRT;

	Material circleMaskMaterial;
	Material blurMaterial;
	Material shardMaterial;

	public int currentPass = 0;
	bool bSuspended = false;
	RenderTexture frozenRT;
	Transform shardParent;
	PhysicsMaterial shardPhysMat;

	static readonly float shardMinSpawnDistance = 0.1f;
	static readonly float shardMaxSpawnDistance = 10f;
	static readonly float shardSpawnDistancePullback = 0.9f;
	static readonly float shardThickness = 0.05f;

	float stripWidth = 1f;
	bool bIsBlackedOut = false;
	Coroutine breakCoroutine;

	public float StripWidth => stripWidth;

	float breakProgress = 0f;

	//Voronoi glass for the 3D->Live break. Cells are built once, then two fronts sweep in from the sides:
	//cracks appear at CrackFront, shards detach and fall once ShatterFront reaches them. The black left
	//behind is the glass that actually fell, not a separate wipe.
	//A piece of glass lives as a GlassShard while it's still in the frame, then becomes a FallingShard.
	struct CrackSegment {
		public Vector2 start;      //the end it grows out from
		public Vector2 end;
		public float startDepth;   //front value when it starts drawing
		public float endDepth;     //front value when it's fully drawn
		public float thickness;
		public float glint;        //0 for most; only a few catch the light
		public int shardA, shardB; //either side; the crack goes when either one falls
	}
	class GlassCell {
		public int id;           //index into the cell array, so cracks can find the shard owning it
		public Vector2[] poly;
		public Vector2 centroid;
		public float depth;      //when the cracking reached it, in reveal units
	}
	//One or more cells merged across undrawn edges. Falls as a single piece.
	class GlassShard {
		public List<GlassCell> cells = new();
		public Vector2 centroid;
		public float depth;
		public bool fallen;
	}
	//Detached, mid-air. Hand-rolled rather than rigidbodies since it's UV-space, not world-space.
	class FallingShard {
		public Vector2[] localVerts;
		public Vector2[] origUVs;
		public int[] tris;
		public Vector2 pos;
		public Vector2 vel;
		public float rot;
		public float angVel;
	}

	List<CrackSegment> cracks = new();
	List<GlassShard> glassShards = new(); //sorted by depth, which is also the order they fall
	List<FallingShard> fallingShards = new();
	List<Vector2[]> blackPolys = new();   //footprints of shards that have fallen
	bool cracksGenerated = false;
	int nextFall;
	float lastFallTime;
	bool bFinalCollapse; //last break landed: the ceiling comes off and the whole mirror lets go
	RenderTexture frozen3DRT;

	//Mesh scratch, reused every rebuild rather than reallocated. uv2.x = -1..1 across a crack's width;
	//uv2.y picks the shading (0 = frozen capture, 1 = crack, 2 = live 3D view); uv2.z = glint.
	//Vertex colour alpha carries the crack tip taper.
	readonly List<Vector3> verts = new();
	readonly List<Vector2> uvs = new();
	readonly List<Vector3> uv2s = new();
	readonly List<Color> colors = new();
	readonly List<int> tris = new();

	static readonly int GLASS_ROWS = 14;
	static readonly float GLASS_EDGE_BIAS = 0.6f;      //<1 packs sites toward the side edges
	static readonly float GLASS_MERGE_CHANCE = 0.1f;  //edges left undrawn, merging their cells into bigger shards
	static readonly float CRACK_REACH = 1f;            //how deep cracks get by the end; 1 = the centre line
	static readonly float SHARD_FALL_INTERVAL = 0.05f; //one shard at a time, so it crumbles instead of dropping in slabs
	static readonly int CRACK_SUBDIV = 3;
	static readonly float CRACK_WANDER = 0.1f;        //perpendicular wander as a fraction of edge length
	static readonly int CRACK_SEEDS_PER_SIDE = 8;      //more seeds = more uniform front, fewer = distinct fingers
	static readonly float CRACK_SPEED_VARIANCE = 3f;   //fastest:slowest route ratio; this is what makes fingers
	static readonly float CRACK_PATH_WEIGHT = 0.5f;   //0 = flat x front, 1 = unbounded network fingers
	static readonly float SHARD_DRIFT = 0.1f;        //ceiling on how far a loosened shard refracts, in UV
	static readonly float SHATTER_EAGERNESS = 0.8f;  //<1 runs the shatter front closer behind the cracks, so more falls per break
	static readonly float FINAL_COLLAPSE_SCALE = 0.15f; //the last of the mirror comes down this much quicker as it goes
	static readonly float CRACK_GLINT_CHANCE = 0.2f; //fraction of cracks that catch the light; the rest stay dark
	static readonly float CRACK_TIP_FADE = 0.03f;     //depth band the advancing tip fades over, so it comes to a point

	//Both fronts run over the reveal metric from GenerateCracks: 0 at the side edges, 1 at the centre line.
	//Stated as where each front lands at the end rather than as a speed ratio -- as a ratio the cracks hit
	//the centre at half progress and had nothing left to do. The lead falls out of the gap between them.
	float ShatterReach => 1f - Globals.Instance.stripWidthUV; //the strip edge; the final collapse takes the rest
	//Curved so the shatter front sits closer behind the cracks; the gap is the cracked-but-standing band.
	float ShatterFront => bFinalCollapse ? 1f : Mathf.Pow(breakProgress, SHATTER_EAGERNESS) * ShatterReach;
	float CrackFront => breakProgress * CRACK_REACH;

	int noFogLayer = -1;
	static readonly int CRACKS_LAYER = 30;
	static readonly float CRACK_THICKNESS_MIN = 0.0015f;
	static readonly float CRACK_THICKNESS_MAX = 0.004f;
	GameObject cracksGO;
	Mesh cracksMesh;
	Material cracksMat;

	void Awake() {
		circleMaskMaterial = new Material(Shader.Find("Custom/CircleMask"));
		blurMaterial = new Material(Shader.Find("Custom/MaskBlur"));
		shardMaterial = new Material(Shader.Find("Custom/Shard"));

		noFogLayer = LayerMask.NameToLayer("NoFog");

		CreateCrackOverlay();
	}

	//Called by composite manager
	public void Configure(Camera cam2D, Camera cam3D, RenderTexture maskRT) {
		this.cam2D = cam2D;
		this.cam3D = cam3D;
		this.maskRT = maskRT;

		cam3D.cullingMask &= ~(1 << CRACKS_LAYER); //hide from 3d camera
		SyncCameras();
	}

	public void SetCamerasSuspended(bool suspended) {
		bSuspended = suspended;
		SyncCameras();
	}

	void SyncCameras() {
		bool b3D = !bSuspended && currentPass > 0;
		bool b2D = !bSuspended && currentPass < Globals.Instance.numBreaks;
		if (cam2D.enabled != b2D || cam3D.enabled != b3D) {
			string reason = bSuspended ? "suspended" : $"pass {currentPass}/{Globals.Instance.numBreaks}";
			Log.Info($"2D {(b2D ? "on" : "off")}, 3D {(b3D ? "on" : "off")} ({reason})");
		}

		cam3D.enabled = b3D;
		cam2D.enabled = b2D;
	}

	void CreateCrackOverlay() {
		cracksGO = new GameObject("CracksMesh");
		cracksGO.layer = CRACKS_LAYER;
		cracksGO.transform.SetParent(transform, false);
		cracksGO.transform.position = Vector3.zero; //prevent frustrum cull
		cracksMesh = new Mesh { name = "CracksMesh" };
		cracksMesh.indexFormat = IndexFormat.UInt32; //cracks + shards overrun 16-bit indices
		cracksMesh.MarkDynamic();
		cracksMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
		cracksGO.AddComponent<MeshFilter>().sharedMesh = cracksMesh;
		cracksMat = new Material(Shader.Find("Custom/CrackOverlay"));
		cracksGO.AddComponent<MeshRenderer>().sharedMaterial = cracksMat;

		GameObject overlay = new GameObject("CracksOverlayCamera");
		overlay.transform.SetParent(transform, false);
		overlay.transform.position = Vector3.zero; //prevent frustrum
		overlayCam = overlay.AddComponent<Camera>();
		overlayCam.clearFlags = CameraClearFlags.Nothing;
		overlayCam.cullingMask = 1 << CRACKS_LAYER;
		overlayCam.orthographic = true;
		overlayCam.allowHDR = false;
		overlayCam.allowMSAA = false;
		overlayCam.useOcclusionCulling = false;

		//URP camera stack with overlay camera type
		UniversalAdditionalCameraData overlayData = overlayCam.GetUniversalAdditionalCameraData();
		overlayData.renderType = CameraRenderType.Overlay;
		overlayData.SetRenderer(0); //use the 2D renderer with no features
		UniversalAdditionalCameraData outputData = CompositeManager.Instance.outputCam.GetUniversalAdditionalCameraData();
		if (!outputData.cameraStack.Contains(overlayCam)) outputData.cameraStack.Add(overlayCam);
	}

	void LateUpdate() {
		if (maskRT == null) return;

		if (cmd == null) cmd = new CommandBuffer { name = "MaskDraw" };
		cmd.Clear();
		cmd.SetRenderTarget(maskRT);
		cmd.ClearRenderTarget(true, true, Color.black);

		float halfH = cam2D.orthographicSize;
		float halfW = halfH * cam2D.aspect;
		cmd.SetGlobalVector("_CameraPos", new Vector4(cam2D.transform.position.x, cam2D.transform.position.y, halfW, halfH));
		cmd.SetGlobalFloat("_CellSize", Globals.Instance.shardSize);
		cmd.SetGlobalFloat("_ShatterBias", Globals.Instance.shatterBias);
		//Purely the section's framing now. The break leaves this alone and puts its black down as the
		//footprints of shards that actually fell, so there's no rectangle closing in over the top of them.
		cmd.SetGlobalFloat("_StripWidth", stripWidth);
		cmd.SetGlobalInt("_IsBlackedOut", bIsBlackedOut ? 1 : 0);
		cmd.SetGlobalInt("_Num2DTo3DPasses", Globals.Instance.numBreaks);
		cmd.SetGlobalInt("_PassIndex", currentPass);
		cmd.DrawProcedural(Matrix4x4.identity, circleMaskMaterial, 0, MeshTopology.Triangles, 3, 1);

		//Blur mask texture
		int tempId = Shader.PropertyToID("_MaskBlurTmp");
		var desc = maskRT.descriptor;
		desc.depthBufferBits = 0;
		desc.sRGB = false; //will throw an error
		cmd.GetTemporaryRT(tempId, desc, FilterMode.Bilinear);

		Vector4 texelSize = new Vector4(1f / maskRT.width, 1f / maskRT.height, maskRT.width, maskRT.height);
		cmd.SetGlobalVector("_MainTex_TexelSize", texelSize);
		cmd.SetGlobalFloat("_BlurRadius", Globals.Instance.maskBlurRadius);

		//Twice for smoothness
		for (int i = 0; i < 2; i++) {
			//Horizontal
			cmd.SetGlobalTexture("_MainTex", maskRT);
			cmd.SetGlobalVector("_BlurDir", new Vector4(0.1f, 0f, 0f, 0f));
			cmd.SetRenderTarget(tempId);
			cmd.ClearRenderTarget(false, true, Color.black);
			cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3, 1);

			//Vertical
			cmd.SetGlobalTexture("_MainTex", tempId);
			cmd.SetGlobalVector("_BlurDir", new Vector4(0f, 0.1f, 0f, 0f));
			cmd.SetRenderTarget(maskRT);
			cmd.ClearRenderTarget(false, true, Color.black);
			cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3, 1);
		}

		cmd.ReleaseTemporaryRT(tempId);

		Graphics.ExecuteCommandBuffer(cmd);

		UpdateShatter();
		UpdateFallingShards(Time.deltaTime);
		RebuildCracksMesh();
	}

	//Both pre and post shatter cases
	void RebuildCracksMesh() {
		if (cracksMesh == null) return;
		cracksMesh.Clear();
		if (cracks.Count == 0 && fallingShards.Count == 0 && blackPolys.Count == 0) return;

		verts.Clear(); uvs.Clear(); uv2s.Clear(); colors.Clear(); tris.Clear();
		int vi = 0;

		//Where glass has already fallen: ragged black following the cell boundaries, so the hole matches
		//the shard that left rather than the hard rectangular strip edge underneath it.
		foreach (Vector2[] poly in blackPolys) {
			for (int i = 0; i < poly.Length; i++) {
				verts.Add(poly[i]); uvs.Add(poly[i]); uv2s.Add(Vector3.zero); colors.Add(Color.black);
			}
			for (int i = 1; i < poly.Length - 1; i++) {
				tris.Add(vi); tris.Add(vi + i); tris.Add(vi + i + 1);
			}
			vi += poly.Length;
		}

		//Cracked but still standing: no longer part of one flat mirror, so it's drawn as its own geometry
		//sampling the live view through an offset, refracting out of line with its neighbours. The offset
		//grows as the shatter front closes in, so a shard works its way loose before it drops.
		float shatterFront = ShatterFront;
		for (int gi = 0; gi < glassShards.Count; gi++) {
			GlassShard g = glassShards[gi];
			if (g.fallen || g.depth > CrackFront) continue;
			float loose = g.depth > 0f ? Mathf.Clamp01(shatterFront / g.depth) : 1f;
			Vector2 drift = ShardDrift(gi) * (loose * SHARD_DRIFT);
			foreach (GlassCell cell in g.cells) {
				for (int i = 0; i < cell.poly.Length; i++) {
					verts.Add(cell.poly[i]);
					uvs.Add(cell.poly[i] + drift);
					uv2s.Add(new Vector3(0f, 2f, 0f));
					colors.Add(Color.white);
				}
				for (int i = 1; i < cell.poly.Length - 1; i++) {
					tris.Add(vi); tris.Add(vi + i); tris.Add(vi + i + 1);
				}
				vi += cell.poly.Length;
			}
		}

		float front = CrackFront;
		foreach (CrackSegment seg in cracks) {
			//A crack is the gap between two shards, so it exists only while both are still standing.
			if (glassShards[seg.shardA].fallen || glassShards[seg.shardB].fallen) continue;
			//Draw the segment part-way as the front passes over it, so a crack visibly runs outward from
			//the end it grew from instead of popping in whole.
			float visT = Mathf.Clamp01(Mathf.InverseLerp(seg.startDepth, seg.endDepth, front));
			if (visT <= 0f) continue;
			Vector2 tip = Vector2.Lerp(seg.start, seg.end, visT);
			Vector2 delta = tip - seg.start;
			if (delta.sqrMagnitude < 1e-8f) continue;
			Vector2 dir = delta.normalized;
			Vector2 perp = new Vector2(-dir.y, dir.x) * (seg.thickness * 0.5f);

			//Taper the advancing tip to nothing. Keyed on depth rather than on the segment, so the fade is
			//continuous as the front crosses from one segment into the next instead of resetting at joints.
			//At the growing tip the lerped depth equals the front exactly, so it comes to a true point.
			float tipDepth = Mathf.Lerp(seg.startDepth, seg.endDepth, visT);
			Color cStart = new(0f, 0f, 0f, Mathf.Clamp01((front - seg.startDepth) / CRACK_TIP_FADE));
			Color cTip = new(0f, 0f, 0f, Mathf.Clamp01((front - tipDepth) / CRACK_TIP_FADE));

			Vector2 v0 = seg.start - perp, v1 = seg.start + perp, v2 = tip + perp, v3 = tip - perp;
			verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
			uvs.Add(v0);   uvs.Add(v1);   uvs.Add(v2);   uvs.Add(v3);
			uv2s.Add(new Vector3(-1f, 1f, seg.glint)); uv2s.Add(new Vector3(1f, 1f, seg.glint));
			uv2s.Add(new Vector3(1f, 1f, seg.glint));  uv2s.Add(new Vector3(-1f, 1f, seg.glint));
			colors.Add(cStart); colors.Add(cStart); colors.Add(cTip); colors.Add(cTip);
			tris.Add(vi + 0); tris.Add(vi + 1); tris.Add(vi + 2);
			tris.Add(vi + 0); tris.Add(vi + 2); tris.Add(vi + 3);
			vi += 4;
		}

		foreach (FallingShard s in fallingShards) {
			float cs = Mathf.Cos(s.rot);
			float sn = Mathf.Sin(s.rot);
			int baseIdx = vi;
			for (int i = 0; i < s.localVerts.Length; i++) {
				Vector2 lv = s.localVerts[i];
				Vector2 rotated = new Vector2(lv.x * cs - lv.y * sn, lv.x * sn + lv.y * cs);
				verts.Add((Vector3)(s.pos + rotated));
				uvs.Add(s.origUVs[i]);
				uv2s.Add(Vector3.zero);
				colors.Add(Color.white);
			}
			for (int i = 0; i < s.tris.Length; i++) tris.Add(baseIdx + s.tris[i]);
			vi += s.localVerts.Length;
		}

		cracksMesh.SetVertices(verts);
		cracksMesh.SetUVs(0, uvs);
		cracksMesh.SetUVs(1, uv2s);
		cracksMesh.SetColors(colors);
		cracksMesh.SetTriangles(tris, 0, calculateBounds: false);
		cracksMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
	}

	public void ResetMask() {
		currentPass = 0;
		stripWidth = 1f;
		breakProgress = 0f;
		bIsBlackedOut = false;
		cracks.Clear();
		fallingShards.Clear();
		cracksGenerated = false;
		ClearGlass();
		ClearSpawnedShards();
		SyncCameras();
	}

	//The voronoi glass rebuilds itself on the next shrink, but the fallen-cell footprints and the fall queue
	//would otherwise carry over and leave the mirror already broken on a second run through.
	void ClearGlass() {
		//Cancel the propagation too -- a reset landing inside its blackout wait would otherwise still black
		//out and shatter half a second into whatever came next.
		if (breakCoroutine != null) StopCoroutine(breakCoroutine);
		breakCoroutine = null;
		glassShards.Clear();
		blackPolys.Clear();
		nextFall = 0;
		bFinalCollapse = false;
	}

	//Destroy the physical 2D->3D glass shards spawned by Do_Shatter so they don't pile up across loops.
	void ClearSpawnedShards() {
		if (shardParent == null) return;
		for (int i = shardParent.childCount - 1; i >= 0; i--) {
			Destroy(shardParent.GetChild(i).gameObject);
		}
	}

	public void ResetMask3D() {
		currentPass = Globals.Instance.numBreaks;
		stripWidth = 1f;
		breakProgress = 0f;
		bIsBlackedOut = false;
		cracks.Clear();
		fallingShards.Clear();
		cracksGenerated = false;
		ClearGlass();
		SyncCameras();
	}

	//3d to live------------------------------------------------------------------------
	//The break has its own clock, not stripWidth: driving it from the strip meant a section setting its
	//framing also declared the mirror half-broken. Falling isn't tied to the propagation either -- shards
	//leave one at a time and trail behind the front.
	public void Do_ShrinkToBlack() {
		if (!cracksGenerated) GenerateCracks();
		int steps = Mathf.Max(1, Globals.Instance.num3DBreaks);
		StartBreakAnim(Mathf.Clamp01(breakProgress + 1f / steps), Globals.Instance.shrinkTime);
		AudioManager.Instance.HandleShrink(false);
	}
	public void Do_ShrinkAll() {
		if (!cracksGenerated) GenerateCracks();
		StartBreakAnim(1f, Globals.Instance.shrinkTime);
		AudioManager.Instance.HandleShrink(true);
	}

	public void SetStripWidth(float width) {
		stripWidth = Mathf.Clamp01(width);
		bIsBlackedOut = false;
	}

	void StartBreakAnim(float target, float duration) {
		if (breakCoroutine != null) StopCoroutine(breakCoroutine);
		breakCoroutine = StartCoroutine(AnimateBreak(target, duration));
	}
	IEnumerator AnimateBreak(float target, float duration) {
		float start = breakProgress;
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			breakProgress = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
			yield return null;
		}
		breakProgress = target;
		breakCoroutine = null;

		//Lifts the ceiling on the shatter front so the centre band becomes eligible too, and the usual
		//cascade takes the rest down. Dumping it all in one frame read as a wall of glass moving together.
		if (breakProgress >= 0.999f) bFinalCollapse = true;
	}

	//Exactly one shard per interval, never more -- keeping pace with the front made it come down in clumps.
	//Total cascade length is shard count x SHARD_FALL_INTERVAL; the count is logged on generate.
	void UpdateShatter() {
		if (nextFall < glassShards.Count && glassShards[nextFall].depth <= ShatterFront) {
			//Coming down for good: each piece follows the last a little quicker, so the ending accelerates.
			float interval = SHARD_FALL_INTERVAL;
			if (bFinalCollapse) {
				interval *= Mathf.Lerp(1f, FINAL_COLLAPSE_SCALE, (float)nextFall / glassShards.Count);
			}
			if (Time.time - lastFallTime < interval) return;
			lastFallTime = Time.time;
			CaptureCurrentView();
			DetachShard(glassShards[nextFall++]);
			return;
		}

		//Nothing left standing. Black out only now -- doing it when the last break finished meant shards
		//were still coming down over a frame that had already gone black.
		if (bFinalCollapse && nextFall >= glassShards.Count) bIsBlackedOut = true;
	}

	//Re-taken on every detach, so a shard freezes the view as it was at the instant it came away. Capturing
	//once at the first fall meant only that first shard matched the screen and everything after it carried
	//a stale frame. One blit per shard, and they're tens of milliseconds apart, so the cost is nothing.
	void CaptureCurrentView() {
		Texture camBTex = Shader.GetGlobalTexture("_CameraB_Tex");
		if (camBTex == null) return;

		if (frozen3DRT == null || frozen3DRT.width != camBTex.width || frozen3DRT.height != camBTex.height) {
			if (frozen3DRT != null) frozen3DRT.Release();
			frozen3DRT = new RenderTexture(camBTex.width, camBTex.height, 0, RenderTextureFormat.ARGB32);
		}
		Graphics.Blit(camBTex, frozen3DRT);
		if (cracksMat != null) cracksMat.mainTexture = frozen3DRT;
	}

	void DetachShard(GlassShard g) {
		g.fallen = true; //every crack touching this shard stops drawing from here on
		//The mesh is just the group's cell fans concatenated. A merged group can be concave, but a falling
		//shard never needs a single outline, so fans are enough and no triangulation is required.
		var verts = new List<Vector2>();
		var origUVs = new List<Vector2>();
		var tris = new List<int>();
		foreach (GlassCell c in g.cells) {
			int b = verts.Count;
			for (int i = 0; i < c.poly.Length; i++) {
				verts.Add(c.poly[i] - g.centroid);
				origUVs.Add(c.poly[i]);
			}
			for (int i = 1; i < c.poly.Length - 1; i++) {
				tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1);
			}
			blackPolys.Add(c.poly);
		}
		if (tris.Count == 0) return;

		//Drift out from whichever side it broke off, then fall. UV y is screen-down here, so +y is down.
		float side = g.centroid.x < 0.5f ? -1f : 1f;
		fallingShards.Add(new FallingShard {
			localVerts = verts.ToArray(),
			origUVs = origUVs.ToArray(),
			tris = tris.ToArray(),
			pos = g.centroid,
			vel = new Vector2(side * Random.Range(0.02f, 0.10f), Random.Range(0.05f, 0.20f)),
			rot = 0f,
			angVel = Random.Range(-3f, 3f)
		});
	}

	//UV space physics
	void UpdateFallingShards(float dt) {
		if (fallingShards.Count == 0) return;
		for (int i = fallingShards.Count - 1; i >= 0; i--) {
			var s = fallingShards[i];
			s.vel.y += 0.8f * dt;
			s.pos += s.vel * dt;
			s.rot += s.angVel * dt;
			if (s.pos.y > 2f) fallingShards.RemoveAt(i);
		}
	}

	//Build the voronoi glass: sites, cell polygons, the crack network along their shared edges, and the
	//merge groups that fall as single shards.
	void GenerateCracks() {
		cracks.Clear();
		glassShards.Clear();
		blackPolys.Clear();
		nextFall = 0;

		float aspect = maskRT != null ? (float)maskRT.width / maskRT.height : 16f / 9f;
		int rows = GLASS_ROWS;
		int cols = Mathf.Max(2, Mathf.RoundToInt(rows * aspect));

		//Sites on a jittered grid, warped along x so they pack toward the left and right edges. Density is
		//most of what sells a fracture: fine where it started, coarse out in the middle.
		Vector2[] sites = new Vector2[cols * rows];
		for (int i = 0; i < cols; i++) {
			for (int j = 0; j < rows; j++) {
				uint hx = Hash2D(i, j);
				uint hy = Hash2D(i + 1337, j + 7919);
				float u = (i + hx / 4294967295f) / cols;
				float v = (j + hy / 4294967295f) / rows;
				sites[i * rows + j] = new Vector2(EdgeWarp(u), v);
			}
		}

		//Each cell is the unit rect clipped by the perpendicular bisector against every nearby site. Two
		//neighbours derive their shared edge from the same bisector, so the polygons meet with no gaps --
		//which is the whole reason this replaces the old rasterise-and-flood-fill.
		GlassCell[] cells = new GlassCell[cols * rows];
		var poly = new List<Vector2>();
		var tmp = new List<Vector2>();
		for (int i = 0; i < cols; i++) {
			for (int j = 0; j < rows; j++) {
				int id = i * rows + j;
				poly.Clear();
				poly.Add(new Vector2(0f, 0f)); poly.Add(new Vector2(1f, 0f));
				poly.Add(new Vector2(1f, 1f)); poly.Add(new Vector2(0f, 1f));

				for (int di = -2; di <= 2 && poly.Count >= 3; di++) {
					for (int dj = -2; dj <= 2 && poly.Count >= 3; dj++) {
						if (di == 0 && dj == 0) continue;
						int ni = i + di, nj = j + dj;
						if (ni < 0 || ni >= cols || nj < 0 || nj >= rows) continue;
						ClipByBisector(poly, sites[id], sites[ni * rows + nj], tmp);
					}
				}
				if (poly.Count < 3) continue;

				Vector2 centroid = Vector2.zero;
				foreach (Vector2 p in poly) centroid += p;
				centroid /= poly.Count;

				cells[id] = new GlassCell {
					id = id,
					poly = poly.ToArray(),
					centroid = centroid,
					depth = Mathf.Min(centroid.x, 1f - centroid.x)
				};
			}
		}

		//Collect shared edges. Each edge is emitted once per cell that owns it, so anything seen twice is
		//an interior boundary between two cells and anything seen once is the frame border, which never cracks.
		var edges = new Dictionary<long, (int a, int b, Vector2 p, Vector2 q)>();
		for (int id = 0; id < cells.Length; id++) {
			GlassCell c = cells[id];
			if (c == null) continue;
			for (int i = 0; i < c.poly.Length; i++) {
				Vector2 p = c.poly[i], q = c.poly[(i + 1) % c.poly.Length];
				long key = EdgeKey(p, q);
				if (edges.TryGetValue(key, out var e)) edges[key] = (e.a, id, e.p, e.q);
				else edges[key] = (id, -1, p, q);
			}
		}

		//Union-find: an edge we choose not to draw merges the cells behind it, so shards come in a range of
		//sizes instead of all being one cell. A crack that ends up inside a merged group reads as a fracture
		//that didn't break all the way through, which is what stops the network looking like a diagram.
		int[] parent = new int[cells.Length];
		for (int i = 0; i < parent.Length; i++) parent[i] = i;

		var interior = new List<(int a, int b, Vector2 p, Vector2 q)>();
		foreach (var e in edges.Values) {
			if (e.b < 0 || cells[e.a] == null || cells[e.b] == null) continue;
			if (Random.value < GLASS_MERGE_CHANCE) Union(parent, e.a, e.b);
			else interior.Add(e);
		}
		//Propagation. Revealing on raw x made every crack advance as one tidy vertical front. Instead, walk
		//the network with Dijkstra from a few seeds per side, random weight per edge: cheap routes race
		//ahead into fingers, slow ones lag, and it still branches at real junctions.
		var vertIds = new Dictionary<long, int>();
		var vertPos = new List<Vector2>();
		var adj = new List<List<(int to, float cost)>>();
		var edgeVerts = new List<(int a, int b)>();

		int VertId(Vector2 p) {
			long key = PointKey(p);
			if (vertIds.TryGetValue(key, out int existing)) return existing;
			int id = vertPos.Count;
			vertIds[key] = id;
			vertPos.Add(p);
			adj.Add(new List<(int, float)>());
			return id;
		}

		foreach (var e in interior) {
			int ia = VertId(e.p), ib = VertId(e.q);
			float cost = (e.q - e.p).magnitude * Random.Range(1f, CRACK_SPEED_VARIANCE);
			adj[ia].Add((ib, cost));
			adj[ib].Add((ia, cost));
			edgeVerts.Add((ia, ib));
		}

		//Only a few seeds per side -- seeding every border vertex just rebuilds the uniform front.
		var seeds = new List<int>();
		for (int side = 0; side < 2; side++) {
			float sx = side == 0 ? 0f : 1f;
			for (int k = 0; k < CRACK_SEEDS_PER_SIDE; k++) {
				float targetY = (k + 0.5f) / CRACK_SEEDS_PER_SIDE + Random.Range(-0.08f, 0.08f);
				int best = -1;
				float bestD = float.MaxValue;
				for (int v = 0; v < vertPos.Count; v++) {
					if (Mathf.Abs(vertPos[v].x - sx) > 0.02f) continue;
					float d = Mathf.Abs(vertPos[v].y - targetY);
					if (d < bestD) { bestD = d; best = v; }
				}
				if (best >= 0 && !seeds.Contains(best)) seeds.Add(best);
			}
		}

		float[] dist = new float[vertPos.Count];
		for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;
		var frontier = new List<(float d, int v)>();
		foreach (int s in seeds) {
			dist[s] = 0f;
			frontier.Add((0f, s));
		}
		while (frontier.Count > 0) {
			int bi = 0;
			for (int i = 1; i < frontier.Count; i++) if (frontier[i].d < frontier[bi].d) bi = i;
			var cur = frontier[bi];
			frontier.RemoveAt(bi);
			if (cur.d > dist[cur.v]) continue;
			foreach (var (to, cost) in adj[cur.v]) {
				float nd = cur.d + cost;
				if (nd >= dist[to]) continue;
				dist[to] = nd;
				frontier.Add((nd, to));
			}
		}

		float maxDist = 0f;
		foreach (float d in dist) if (d < float.MaxValue) maxDist = Mathf.Max(maxDist, d);
		if (maxDist <= 0f) maxDist = 1f;

		//Path distance alone lets a cheap route snake into the middle while its neighbours sit untouched.
		//Blending against x keeps x as the general stopping point, while the path term still lets fingers
		//run ahead inside that band. Normalised against the half-width so 1 is the centre line -- against
		//the strip edge instead, the whole middle band collapses to one value and loses its ordering.
		float[] reveal = new float[vertPos.Count];
		for (int v = 0; v < reveal.Length; v++) {
			float xNorm = Mathf.Clamp01(Mathf.Min(vertPos[v].x, 1f - vertPos[v].x) * 2f);
			float pathNorm = dist[v] >= float.MaxValue ? 1f : dist[v] / maxDist;
			reveal[v] = Mathf.Lerp(xNorm, pathNorm, CRACK_PATH_WEIGHT);
		}

		//A cell is ready to fall once the cracks all the way around it have arrived.
		for (int id = 0; id < cells.Length; id++) {
			GlassCell c = cells[id];
			if (c == null) continue;
			float reached = 0f;
			bool any = false;
			foreach (Vector2 p in c.poly) {
				if (!vertIds.TryGetValue(PointKey(p), out int v)) continue;
				reached = Mathf.Max(reached, reveal[v]);
				any = true;
			}
			//Cells the network never touched still have to go, so fall back to their own x depth.
			c.depth = any ? reached : Mathf.Clamp01(Mathf.Min(c.centroid.x, 1f - c.centroid.x) * 2f);
		}

		//Gather cells into their merged groups, then order by when the cracking got to them -- that is the
		//order they fall, so the mirror comes apart following the fracture rather than in a straight line.
		var byRoot = new Dictionary<int, GlassShard>();
		for (int id = 0; id < cells.Length; id++) {
			if (cells[id] == null) continue;
			int root = Find(parent, id);
			if (!byRoot.TryGetValue(root, out GlassShard g)) {
				g = new GlassShard();
				byRoot[root] = g;
			}
			g.cells.Add(cells[id]);
		}
		foreach (GlassShard g in byRoot.Values) {
			Vector2 c = Vector2.zero;
			float d = 0f;
			foreach (GlassCell cell in g.cells) {
				c += cell.centroid;
				d = Mathf.Max(d, cell.depth); //the last cell to be cracked is what holds the group on
			}
			g.centroid = c / g.cells.Count;
			g.depth = d;
			glassShards.Add(g);
		}
		glassShards.Sort((x, y) => x.depth.CompareTo(y.depth));

		//Emitted last so each crack can name the two shards it sits between. A crack is the gap between
		//them, so once either side falls it's just the edge of a hole -- owning the shards outright means
		//cracks vanish exactly when their glass does, with no second front to keep in sync.
		var shardOfCell = new Dictionary<int, int>();
		for (int gi = 0; gi < glassShards.Count; gi++) {
			foreach (GlassCell cell in glassShards[gi].cells) shardOfCell[cell.id] = gi;
		}
		for (int i = 0; i < interior.Count; i++) {
			var e = interior[i];
			var (ia, ib) = edgeVerts[i];
			bool flip = reveal[ib] < reveal[ia];
			//Each edge grows from whichever end the crack reached first, so segments draw outward like a
			//tip advancing rather than popping in whole.
			EmitCrack(flip ? e.q : e.p, flip ? e.p : e.q,
				Mathf.Min(reveal[ia], reveal[ib]), Mathf.Max(reveal[ia], reveal[ib]),
				shardOfCell[e.a], shardOfCell[e.b]);
		}

		cracksGenerated = true;
		Log.Info($"Glass: {glassShards.Count} shards, {cracks.Count} crack segments.");
	}

	//Stable per-shard direction and magnitude, so a piece keeps refracting the same way instead of
	//jittering each frame. Magnitude is 0..1 of SHARD_DRIFT, making that constant a ceiling rather than
	//a fixed amount every shard shares -- some sit almost true, others sit well out of line.
	static Vector2 ShardDrift(int index) {
		float a = Hash2D(index, 7919) / 4294967295f * Mathf.PI * 2f;
		float m = Hash2D(index, 104729) / 4294967295f;
		return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m;
	}

	//Uniform u -> x, packed toward 0 and 1.
	static float EdgeWarp(float u) {
		float t = u * 2f - 1f;
		return 0.5f + 0.5f * Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), GLASS_EDGE_BIAS);
	}

	//Sutherland-Hodgman: keep the half of the polygon nearer to `site` than to `other`.
	static void ClipByBisector(List<Vector2> poly, Vector2 site, Vector2 other, List<Vector2> tmp) {
		Vector2 dir = other - site;
		Vector2 mid = (site + other) * 0.5f;
		tmp.Clear();
		for (int i = 0; i < poly.Count; i++) {
			Vector2 a = poly[i];
			Vector2 b = poly[(i + 1) % poly.Count];
			float da = Vector2.Dot(a - mid, dir);
			float db = Vector2.Dot(b - mid, dir);
			if (da <= 0f) tmp.Add(a);
			if ((da <= 0f) != (db <= 0f)) tmp.Add(Vector2.Lerp(a, b, da / (da - db)));
		}
		poly.Clear();
		poly.AddRange(tmp);
	}

	//Neighbouring cells derive shared geometry from the same bisector, so rounding a point identifies it
	//from either side despite the float noise.
	static long PointKey(Vector2 p) {
		long x = (long)Mathf.Round(p.x * 10000f);
		long y = (long)Mathf.Round(p.y * 10000f);
		return (x << 32) ^ (y & 0xffffffffL);
	}
	static long EdgeKey(Vector2 a, Vector2 b) => PointKey((a + b) * 0.5f);

	static int Find(int[] parent, int i) {
		while (parent[i] != i) {
			parent[i] = parent[parent[i]];
			i = parent[i];
		}
		return i;
	}
	static void Union(int[] parent, int a, int b) {
		int ra = Find(parent, a), rb = Find(parent, b);
		if (ra != rb) parent[rb] = ra;
	}

	//One voronoi edge becomes a few segments with a little perpendicular wander, so the network doesn't read
	//as a diagram. The ends stay pinned so junctions still meet cleanly. Thickness tapers inward, away from
	//the side the fracture started at.
	void EmitCrack(Vector2 a, Vector2 b, float d0, float d1, int shardA, int shardB) {
		Vector2 delta = b - a;
		float len = delta.magnitude;
		if (len < 1e-5f) return;
		Vector2 perp = new Vector2(-delta.y, delta.x) / len;

		//Decided once per edge so a glint runs the whole length of a crack rather than flickering segment
		//to segment. Most cracks get none at all -- a bright core on every one reads as glowing veins.
		float glint = Random.value < CRACK_GLINT_CHANCE ? Random.Range(0.5f, 1f) : 0f;

		Vector2 prev = a;
		for (int s = 1; s <= CRACK_SUBDIV; s++) {
			Vector2 p = Vector2.Lerp(a, b, (float)s / CRACK_SUBDIV);
			if (s < CRACK_SUBDIV) p += perp * (Random.Range(-CRACK_WANDER, CRACK_WANDER) * len);
			//Spread the edge's arrival window across its segments so the crack runs along its own length.
			cracks.Add(new CrackSegment {
				start = prev,
				end = p,
				startDepth = Mathf.Lerp(d0, d1, (float)(s - 1) / CRACK_SUBDIV),
				endDepth = Mathf.Lerp(d0, d1, (float)s / CRACK_SUBDIV),
				//Thickest at the seeds and thinning as it runs out, the way a fracture loses energy.
				thickness = Mathf.Lerp(CRACK_THICKNESS_MAX, CRACK_THICKNESS_MIN, Mathf.Clamp01(d0)),
				glint = glint,
				shardA = shardA,
				shardB = shardB
			});
			prev = p;
		}
	}

	//2d to 3d--------------------------------------------------------------------------------
	public void Do_ShatterAll() {
		while (currentPass < Globals.Instance.numBreaks) Do_Shatter();
	}
	public void Do_Shatter() {
		if (cam2D == null) return;
		if (currentPass >= Globals.Instance.numBreaks) return;

		int revealingPass = currentPass;
		currentPass++;

		SyncCameras();

		//Capture current 2D camera to put on shards
		Texture camATex = Shader.GetGlobalTexture("_CameraA_Tex");
		if (camATex != null) {
			if (frozenRT == null || frozenRT.width != camATex.width || frozenRT.height != camATex.height) {
				if (frozenRT != null) frozenRT.Release();
				frozenRT = new RenderTexture(camATex.width, camATex.height, 0, RenderTextureFormat.ARGB32);
			}
			Graphics.Blit(camATex, frozenRT);
		}

		AudioManager.Instance.HandleShatter(); // Handle audio things

		SpawnShardsForPass(revealingPass);
	}

	void SpawnShardsForPass(int pass) {
		if (shardParent == null) {
			shardParent = new GameObject("Shards").transform;
			shardParent.SetParent(transform, false);
		}

		Vector3 camPos = cam2D.transform.position;
		float halfH = cam2D.orthographicSize;
		float halfW = halfH * cam2D.aspect;

		int cxMin = Mathf.FloorToInt((camPos.x - halfW) / Globals.Instance.shardSize) - 1;
		int cxMax = Mathf.CeilToInt((camPos.x + halfW) / Globals.Instance.shardSize) + 1;
		int cyMin = Mathf.FloorToInt((camPos.y - halfH) / Globals.Instance.shardSize) - 1;
		int cyMax = Mathf.CeilToInt((camPos.y + halfH) / Globals.Instance.shardSize) + 1;

		for (int cx = cxMin; cx <= cxMax; cx++) {
			for (int cy = cyMin; cy <= cyMax; cy++) {
				if (AssignPass(cx, cy) != pass) continue;
				Vector2 cellCenter = CellCenter(cx, cy);
				if (cellCenter.x < camPos.x - halfW || cellCenter.x > camPos.x + halfW) continue; //skip off-screen
				if (cellCenter.y < camPos.y - halfH || cellCenter.y > camPos.y + halfH) continue;

				SpawnShardAt(new Vector2Int(cx, cy), cellCenter, camPos, halfW, halfH);
			}
		}
	}

	void SpawnShardAt(Vector2Int cellCoord, Vector2 cellCenter, Vector3 camPos, float halfW, float halfH) {
		Mesh cellMesh = BuildCellPolygonMesh(cellCoord, cellCenter, camPos, halfW, halfH);
		if (cellMesh == null) return;

		//Project uv onto a plane (dynamically far)
		Vector2 screenUV = new Vector2(
			(cellCenter.x - (camPos.x - halfW)) / (2f * halfW),
			(cellCenter.y - (camPos.y - halfH)) / (2f * halfH)
		);
		float spawnDistance = ComputeShardSpawnDistance(screenUV);
		Vector3 worldPos3D = cam3D.ViewportToWorldPoint(new Vector3(screenUV.x, screenUV.y, spawnDistance));

		//Constant screen size
		float view2DH = 2f * cam2D.orthographicSize;
		float view3DH = 2f * spawnDistance * Mathf.Tan(cam3D.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float scale = view3DH / view2DH;

		GameObject go = new GameObject("Shard");
		if (noFogLayer >= 0) go.layer = noFogLayer;
		go.transform.SetParent(shardParent, false);
		go.transform.position = worldPos3D;
		go.transform.rotation = Quaternion.LookRotation(cam3D.transform.position - worldPos3D, cam3D.transform.up);
		go.transform.localScale = new Vector3(scale, scale, scale);

		go.AddComponent<MeshFilter>().sharedMesh = cellMesh;
		Material instance = new Material(shardMaterial);
		go.AddComponent<MeshRenderer>().material = instance;

		//Collider
		BoxCollider box = go.AddComponent<BoxCollider>();
		box.center = new Vector3(cellMesh.bounds.center.x, cellMesh.bounds.center.y, 0f);
		box.size = new Vector3(Mathf.Max(cellMesh.bounds.size.x, 0.01f), Mathf.Max(cellMesh.bounds.size.y, 0.01f), shardThickness);
		box.material = GetShardPhysicsMaterial();

		//Physics
		Rigidbody rb = go.AddComponent<Rigidbody>();
		rb.mass = 0.3f;
		rb.useGravity = false;
		float sizeThreshold = Mathf.InverseLerp(0.1f, 0.75f, scale);
		float gravityScale = Mathf.Lerp(0.5f, 1, sizeThreshold);
		Vector3 randomDir2D = Random.insideUnitCircle.normalized;
		float speed = Random.Range(Globals.Instance.shardSpeedRange.x, Globals.Instance.shardSpeedRange.y);
		Vector3 worldVel = cam3D.transform.right * randomDir2D.x * speed
				 + cam3D.transform.up * randomDir2D.y * speed * 0.3f
				 + cam3D.transform.forward * speed * 0.5f;
		rb.linearVelocity = worldVel;
		rb.angularVelocity = new Vector3(
			Random.Range(Globals.Instance.shardSpinRange.x, Globals.Instance.shardSpinRange.y),
			Random.Range(Globals.Instance.shardSpinRange.x, Globals.Instance.shardSpinRange.y),
			Random.Range(Globals.Instance.shardSpinRange.x, Globals.Instance.shardSpinRange.y)
		) * Mathf.Deg2Rad;

		Shard shard = go.AddComponent<Shard>();
		shard.gravityScale = gravityScale;
		shard.Init(instance, frozenRT, cellMesh);
	}

	//A little bounce
	PhysicsMaterial GetShardPhysicsMaterial() {
		if (shardPhysMat == null) {
			shardPhysMat = new PhysicsMaterial("ShardPhysics") {
				bounceCombine = PhysicsMaterialCombine.Maximum,
				frictionCombine = PhysicsMaterialCombine.Average
			};
		}
		shardPhysMat.bounciness = 0.2f;
		return shardPhysMat;
	}

	//Superliminal type beat
	float ComputeShardSpawnDistance(Vector2 screenUV) {
		Ray ray = cam3D.ViewportPointToRay(new Vector3(screenUV.x, screenUV.y, 0f));
		if (Physics.Raycast(ray, out RaycastHit hit, shardMaxSpawnDistance * 20f)) {
			float depth = cam3D.transform.InverseTransformPoint(hit.point).z * Random.Range(0.5f, 1.2f);
			float soft = shardMaxSpawnDistance * (1f - Mathf.Exp(-depth / shardMaxSpawnDistance));
			return Mathf.Max(soft * shardSpawnDistancePullback, shardMinSpawnDistance);
		}
		return shardMaxSpawnDistance;
	}

	//Build mesh for voronoi cell by ray-casting outward from cellCenter and binary searching the boundary then extrude
	Mesh BuildCellPolygonMesh(Vector2Int cellCoord, Vector2 cellCenter, Vector3 camPos, float halfW, float halfH) {
		if (NearestCellCoord(cellCenter) != cellCoord) return null;

		int rays = Mathf.Max(3, 16);
		Vector2[] boundary = new Vector2[rays];
		for (int i = 0; i < rays; i++) {
			float angle = i * Mathf.PI * 2f / rays;
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			boundary[i] = FindCellBoundary(cellCenter, dir, 40, cellCoord);
		}

		//Extrude along local Z by halfT each side
		float halfT = shardThickness * 0.5f;
		int BACK = rays + 1;
		int vertCount = 2 * (rays + 1);
		Vector3[] verts = new Vector3[vertCount];
		Vector2[] uvs = new Vector2[vertCount];
		int[] tris = new int[rays * 3 * 2 + rays * 6];

		Vector2 centerUV = WorldToUV(cellCenter, camPos, halfW, halfH);
		verts[0] = new Vector3(0f, 0f, halfT);
		verts[BACK] = new Vector3(0f, 0f, -halfT);
		uvs[0] = centerUV;
		uvs[BACK] = centerUV;

		for (int i = 0; i < rays; i++) {
			Vector2 worldVert = boundary[i];
			Vector2 local = new Vector2(worldVert.x - cellCenter.x, worldVert.y - cellCenter.y);
			Vector2 uv = WorldToUV(worldVert, camPos, halfW, halfH);
			verts[i + 1] = new Vector3(local.x, local.y, halfT);
			verts[BACK + i + 1] = new Vector3(local.x, local.y, -halfT);
			uvs[i + 1] = uv;
			uvs[BACK + i + 1] = uv;
		}

		int t = 0;
		for (int i = 0; i < rays; i++) { //Front
			tris[t++] = 0;
			tris[t++] = i + 1;
			tris[t++] = ((i + 1) % rays) + 1;
		}
		for (int i = 0; i < rays; i++) { //Back
			tris[t++] = BACK;
			tris[t++] = BACK + i + 1;
			tris[t++] = BACK + ((i + 1) % rays) + 1;
		}
		for (int i = 0; i < rays; i++) { //Sides
			int fa = i + 1;
			int fb = ((i + 1) % rays) + 1;
			int ba = BACK + i + 1;
			int bb = BACK + ((i + 1) % rays) + 1;
			tris[t++] = fa;
			tris[t++] = bb;
			tris[t++] = ba;
			tris[t++] = fa;
			tris[t++] = fb;
			tris[t++] = bb;
		}

		Mesh mesh = new Mesh();
		mesh.vertices = verts;
		mesh.uv = uvs;
		mesh.triangles = tris;
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();
		return mesh;
	}

	Vector2 FindCellBoundary(Vector2 origin, Vector2 dir, float maxR, Vector2Int targetCell) {
		if (NearestCellCoord(origin + dir * maxR) == targetCell) return origin + dir * maxR;
		float low = 0f;
		float high = maxR;
		for (int i = 0; i < 12; i++) {
			float mid = (low + high) * 0.5f;
			if (NearestCellCoord(origin + dir * mid) == targetCell) low = mid;
			else high = mid;
		}
		return origin + dir * low;
	}

	Vector2 WorldToUV(Vector2 worldPos, Vector3 camPos, float halfW, float halfH) {
		return new Vector2(
			(worldPos.x - (camPos.x - halfW)) / (2f * halfW),
			(worldPos.y - (camPos.y - halfH)) / (2f * halfH)
		);
	}

	float GlassDist(Vector2 a, Vector2 b) {
		float biasWeightX = Globals.Instance.shardSize * 0.05f;
		float biasWeightY = Globals.Instance.shardSize * 0.5f;
		float s = Mathf.Sin(b.x * 12.9898f + b.y * 78.233f);
		float c = Mathf.Cos(b.x * 12.9898f + b.y * 78.233f);
		Vector2 bias = new Vector2(s * biasWeightX, c * biasWeightY);
		return (a - b + bias).magnitude;
	}

	Vector2Int NearestCellCoord(Vector2 worldPos) {
		int baseCx = Mathf.FloorToInt(worldPos.x / Globals.Instance.shardSize);
		int baseCy = Mathf.FloorToInt(worldPos.y / Globals.Instance.shardSize);
		float minDist = float.MaxValue;
		Vector2Int nearest = new Vector2Int(baseCx, baseCy);
		for (int dx = -1; dx <= 1; dx++) {
			for (int dy = -1; dy <= 1; dy++) {
				Vector2Int neighbor = new Vector2Int(baseCx + dx, baseCy + dy);
				Vector2 center = CellCenter(neighbor.x, neighbor.y);
				float d = GlassDist(worldPos, center);
				if (d < minDist) {
					minDist = d;
					nearest = neighbor;
				}
			}
		}
		return nearest;
	}

	//CPU voronoi
	static uint Hash2D(int x, int y) {
		uint h = (uint)x * 1664525u + (uint)y * 22695477u + 2891336453u;
		h ^= h >> 16;
		h *= 0x45d9f3bu;
		h ^= h >> 16;
		return h;
	}

	Vector2 CellCenter(int cx, int cy) {
		uint hx = Hash2D(cx, cy);
		uint hy = Hash2D(cx + 1337, cy + 7919);
		Vector2 offset = new Vector2(hx / 4294967295f, hy / 4294967295f);
		return (new Vector2(cx, cy) + offset) * Globals.Instance.shardSize;
	}

	int AssignPass(int cx, int cy) {
		uint h = Hash2D(cx, cy);
		float noise = h / 4294967295f;
		float biased = 1f - Mathf.Pow(noise, Globals.Instance.shatterBias);
		return Mathf.Min((int)(biased * Globals.Instance.numBreaks), Globals.Instance.numBreaks - 1);
	}
}
