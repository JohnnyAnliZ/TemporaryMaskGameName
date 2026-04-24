using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;


public struct CrackSegment {
	public Vector2 start;
	public Vector2 end;
	public float startProgress;
	public float endProgress;
	public float thickness;
}

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

	int currentPass = 0;
	RenderTexture frozenRT;
	Transform shardParent;
	PhysicsMaterial shardPhysMat;

	static readonly float shardMinSpawnDistance = 0.1f;
	static readonly float shardMaxSpawnDistance = 10f;
	static readonly float shardSpawnDistancePullback = 0.9f;
	static readonly float shardThickness = 0.05f;

	float blackProgress = 0f;
	Coroutine blackCoroutine;
	List<CrackSegment> cracks = new();
	bool cracksGenerated = false;

	//Keep track manually, no rigidbody
	class CrackShard {
		public Vector2[] localVerts;
		public Vector2[] origUVs;
		public int[] tris;
		public Vector2 pos;
		public Vector2 vel;
		public float rot;
		public float angVel;
	}
	List<CrackShard> crackShards = new();
	RenderTexture frozen3DRT;

	static readonly int CRACKS_LAYER = 30;
	static readonly float CRACK_THICKNESS_MIN = 0.004f;
	static readonly float CRACK_THICKNESS_MAX = 0.01f;
	GameObject cracksGO;
	Mesh cracksMesh;
	Material cracksMat;

	void Awake() {
		circleMaskMaterial = new Material(Shader.Find("Custom/CircleMask"));
		blurMaterial = new Material(Shader.Find("Custom/MaskBlur"));
		shardMaterial = new Material(Shader.Find("Custom/Shard"));
	}

	//Called by composite manager
	public void Configure(Camera cam2D, Camera cam3D, RenderTexture maskRT) {
		this.cam2D = cam2D;
		this.cam3D = cam3D;
		this.maskRT = maskRT;

		cam3D.cullingMask &= ~(1 << CRACKS_LAYER); //hide from 3d camera
		CreateCrackOverlay();
	}

	void CreateCrackOverlay() {
		cracksGO = new GameObject("CracksMesh");
		cracksGO.layer = CRACKS_LAYER;
		cracksGO.transform.SetParent(transform, false);
		cracksGO.transform.position = Vector3.zero; //prevent frustrum cull
		cracksMesh = new Mesh { name = "CracksMesh" };
		cracksMesh.indexFormat = IndexFormat.UInt32; //just to be safe for our 128 grid size
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
		cmd.SetGlobalFloat("_BlackProgress", blackProgress);
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
			cmd.SetGlobalVector("_BlurDir", new Vector4(1f, 0f, 0f, 0f));
			cmd.SetRenderTarget(tempId);
			cmd.ClearRenderTarget(false, true, Color.black);
			cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3, 1);

			//Vertical
			cmd.SetGlobalTexture("_MainTex", tempId);
			cmd.SetGlobalVector("_BlurDir", new Vector4(0f, 1f, 0f, 0f));
			cmd.SetRenderTarget(maskRT);
			cmd.ClearRenderTarget(false, true, Color.black);
			cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3, 1);
		}

		cmd.ReleaseTemporaryRT(tempId);

		Graphics.ExecuteCommandBuffer(cmd);

		UpdateCrackShards(Time.deltaTime);
		RebuildCracksMesh();
	}

	//Both pre and post shatter cases
	void RebuildCracksMesh() {
		if (cracksMesh == null) return;
		cracksMesh.Clear();
		if (cracks.Count == 0 && crackShards.Count == 0) return;

		var verts = new List<Vector3>();
		var uvs = new List<Vector2>();
		var colors = new List<Color>();
		var tris = new List<int>();
		int vi = 0;

		foreach (CrackSegment seg in cracks) {
			float visT = Mathf.InverseLerp(seg.startProgress, seg.endProgress, blackProgress);
			if (visT <= 0f) continue;
			Vector2 end = Vector2.Lerp(seg.start, seg.end, visT);
			Vector2 delta = end - seg.start;
			if (delta.sqrMagnitude < 1e-8f) continue;
			Vector2 dir = delta.normalized;
			Vector2 perp = new Vector2(-dir.y, dir.x) * (seg.thickness * 0.5f);

			Vector2 v0 = seg.start - perp, v1 = seg.start + perp, v2 = end + perp, v3 = end - perp;
			verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
			uvs.Add(v0);   uvs.Add(v1);   uvs.Add(v2);   uvs.Add(v3);
			colors.Add(Color.black); colors.Add(Color.black); colors.Add(Color.black); colors.Add(Color.black);
			tris.Add(vi + 0); tris.Add(vi + 1); tris.Add(vi + 2);
			tris.Add(vi + 0); tris.Add(vi + 2); tris.Add(vi + 3);
			vi += 4;
		}

		foreach (CrackShard s in crackShards) {
			float cs = Mathf.Cos(s.rot);
			float sn = Mathf.Sin(s.rot);
			int baseIdx = vi;
			for (int i = 0; i < s.localVerts.Length; i++) {
				Vector2 lv = s.localVerts[i];
				Vector2 rotated = new Vector2(lv.x * cs - lv.y * sn, lv.x * sn + lv.y * cs);
				verts.Add((Vector3)(s.pos + rotated));
				uvs.Add(s.origUVs[i]);
				colors.Add(Color.white);
			}
			for (int i = 0; i < s.tris.Length; i++) tris.Add(baseIdx + s.tris[i]);
			vi += s.localVerts.Length;
		}

		cracksMesh.SetVertices(verts);
		cracksMesh.SetUVs(0, uvs);
		cracksMesh.SetColors(colors);
		cracksMesh.SetTriangles(tris, 0, calculateBounds: false);
		cracksMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
	}

	public void ResetMask() {
		currentPass = 0;
		if (blackCoroutine != null) StopCoroutine(blackCoroutine);
		blackProgress = 0f;
		cracks.Clear();
		crackShards.Clear();
		cracksGenerated = false;
	}

	//3d to live------------------------------------------------------------------------
	public void Do_ShrinkToBlack() {
		if (!cracksGenerated) GenerateCracks();
		int steps = Mathf.Max(1, Globals.Instance.num3DBreaks);
		float target = Mathf.Min(1f, blackProgress + 1f / steps);
		StartBlackAnim(target, Globals.Instance.shrinkTime);
	}
	public void Do_ShrinkAll() {
		if (!cracksGenerated) GenerateCracks();
		StartBlackAnim(1f, Globals.Instance.shrinkTime);
	}
	void StartBlackAnim(float target, float duration) {
		if (blackCoroutine != null) StopCoroutine(blackCoroutine);
		blackCoroutine = StartCoroutine(AnimateBlack(target, duration));
	}
	IEnumerator AnimateBlack(float target, float duration) {
		float start = blackProgress;
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			blackProgress = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
			yield return null;
		}
		blackProgress = target;
		blackCoroutine = null;

		if (blackProgress >= 0.999f) {
			yield return new WaitForSeconds(0.5f);
			Do_ShatterCracks();
		}
	}

	public void Do_ShatterCracks() {
		const int GRID = 128;
		bool[,] isCrack = new bool[GRID, GRID];

		//Rasterize
		foreach (var seg in cracks) {
			float visT = Mathf.InverseLerp(seg.startProgress, seg.endProgress, blackProgress);
			if (visT <= 0f) continue;
			Vector2 segEnd = Vector2.Lerp(seg.start, seg.end, visT);
			int widthPx = Mathf.Max(1, Mathf.CeilToInt(seg.thickness * GRID));
			RasterizeSegment(seg.start, segEnd, widthPx, GRID, isCrack);
		}

		//Flood Fill regionId: -2=outside the visible strip (skip entirely),
		//-1=crack cell (absorbed later by BFS), 0=unassigned non-crack, >=1=region index.
		const float STRIP_HALF_UV = 0.15f; //matches DrawMaskShader's video_width_uv * 0.5
		int[,] regionId = new int[GRID, GRID];
		float cellSizeUV = 1f / GRID;
		for (int y = 0; y < GRID; y++) {
			for (int x = 0; x < GRID; x++) {
				float cellCenterX = (x + 0.5f) * cellSizeUV;
				bool inStrip = Mathf.Abs(cellCenterX - 0.5f) <= STRIP_HALF_UV;
				if (!inStrip) regionId[x, y] = -2;
				else regionId[x, y] = isCrack[x, y] ? -1 : 0;
			}
		}

		var regions = new List<List<Vector2Int>>();
		var queue = new Queue<Vector2Int>();
		int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
		for (int y = 0; y < GRID; y++) {
			for (int x = 0; x < GRID; x++) {
				if (regionId[x, y] != 0) continue;
				int id = regions.Count + 1;
				var cells = new List<Vector2Int>();
				queue.Clear();
				queue.Enqueue(new Vector2Int(x, y));
				regionId[x, y] = id;
				while (queue.Count > 0) {
					var c = queue.Dequeue();
					cells.Add(c);
					for (int i = 0; i < 4; i++) {
						int nx = c.x + dx[i], ny = c.y + dy[i];
						if (nx < 0 || nx >= GRID || ny < 0 || ny >= GRID) continue;
						if (regionId[nx, ny] != 0) continue;
						regionId[nx, ny] = id;
						queue.Enqueue(new Vector2Int(nx, ny));
					}
				}
				regions.Add(cells);
			}
		}

		//BFS expand regions so no gaps remain
		queue.Clear();
		for (int y = 0; y < GRID; y++)
			for (int x = 0; x < GRID; x++)
				if (regionId[x, y] >= 1) queue.Enqueue(new Vector2Int(x, y));
		while (queue.Count > 0) {
			var c = queue.Dequeue();
			int myId = regionId[c.x, c.y];
			for (int i = 0; i < 4; i++) {
				int nx = c.x + dx[i], ny = c.y + dy[i];
				if (nx < 0 || nx >= GRID || ny < 0 || ny >= GRID) continue;
				if (regionId[nx, ny] != -1) continue; //only absorb unassigned crack cells
				regionId[nx, ny] = myId;
				regions[myId - 1].Add(new Vector2Int(nx, ny));
				queue.Enqueue(new Vector2Int(nx, ny));
			}
		}

		//Capture 3d camera onto shards
		Texture camBTex = Shader.GetGlobalTexture("_CameraB_Tex");
		if (camBTex != null) {
			if (frozen3DRT == null || frozen3DRT.width != camBTex.width || frozen3DRT.height != camBTex.height) {
				if (frozen3DRT != null) frozen3DRT.Release();
				frozen3DRT = new RenderTexture(camBTex.width, camBTex.height, 0, RenderTextureFormat.ARGB32);
			}
			Graphics.Blit(camBTex, frozen3DRT);
			if (cracksMat != null) cracksMat.mainTexture = frozen3DRT;
		}

		foreach (var cells in regions) {
			if (cells.Count < 4) continue;
			SpawnRegionShard(cells, GRID);
		}

		cracks.Clear();
		RebuildCracksMesh();
	}

	void RasterizeSegment(Vector2 start, Vector2 end, int widthPx, int grid, bool[,] isCrack) {
		float dxf = end.x - start.x, dyf = end.y - start.y;
		float length = Mathf.Sqrt(dxf * dxf + dyf * dyf);
		if (length < 1e-5f) return;
		int steps = Mathf.Max(1, Mathf.CeilToInt(length * grid * 2)); //oversample so no gaps
		int half = widthPx / 2;
		for (int s = 0; s <= steps; s++) {
			float t = (float)s / steps;
			float uvx = start.x + dxf * t;
			float uvy = start.y + dyf * t;
			int cx = Mathf.Clamp(Mathf.FloorToInt(uvx * grid), 0, grid - 1);
			int cy = Mathf.Clamp(Mathf.FloorToInt(uvy * grid), 0, grid - 1);
			for (int oy = -half; oy <= half; oy++) {
				int ny = cy + oy;
				if (ny < 0 || ny >= grid) continue;
				for (int ox = -half; ox <= half; ox++) {
					int nx = cx + ox;
					if (nx < 0 || nx >= grid) continue;
					isCrack[nx, ny] = true;
				}
			}
		}
	}

	void SpawnRegionShard(List<Vector2Int> cells, int grid) {
		float cellSize = 1f / grid;
		float sumX = 0, sumY = 0;
		foreach (var c in cells) { sumX += c.x; sumY += c.y; }
		Vector2 centerUV = new Vector2((sumX / cells.Count + 0.5f) * cellSize, (sumY / cells.Count + 0.5f) * cellSize);

		//Local verts + origUVs so texture rotates with shard as it falls
		var verts = new List<Vector2>(cells.Count * 4);
		var origUVs = new List<Vector2>(cells.Count * 4);
		var tris = new List<int>(cells.Count * 6);
		foreach (var c in cells) {
			float ux0 = c.x * cellSize;
			float uy0 = c.y * cellSize;
			float ux1 = ux0 + cellSize;
			float uy1 = uy0 + cellSize;
			int baseIdx = verts.Count;
			verts.Add(new Vector2(ux0 - centerUV.x, uy0 - centerUV.y));
			verts.Add(new Vector2(ux1 - centerUV.x, uy0 - centerUV.y));
			verts.Add(new Vector2(ux1 - centerUV.x, uy1 - centerUV.y));
			verts.Add(new Vector2(ux0 - centerUV.x, uy1 - centerUV.y));
			origUVs.Add(new Vector2(ux0, uy0));
			origUVs.Add(new Vector2(ux1, uy0));
			origUVs.Add(new Vector2(ux1, uy1));
			origUVs.Add(new Vector2(ux0, uy1));
			tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
			tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
		}

		Vector2 fromCenter = centerUV - new Vector2(0.5f, 0.5f);
		Vector2 outward = fromCenter.sqrMagnitude > 1e-4f ? fromCenter.normalized : Random.insideUnitCircle.normalized;
		Vector2 vel = outward * Random.Range(0.1f, 0.25f) + new Vector2(0f, Random.Range(0.05f, 0.2f));

		crackShards.Add(new CrackShard {
			localVerts = verts.ToArray(),
			origUVs = origUVs.ToArray(),
			tris = tris.ToArray(),
			pos = centerUV,
			vel = vel,
			rot = 0f,
			angVel = Random.Range(-3f, 3f)
		});
	}

	//UV space physics
	void UpdateCrackShards(float dt) {
		if (crackShards.Count == 0) return;
		for (int i = crackShards.Count - 1; i >= 0; i--) {
			var s = crackShards[i];
			s.vel.y += 0.8f * dt;
			s.pos += s.vel * dt;
			s.rot += s.angVel * dt;
			if (s.pos.y > 2f) crackShards.RemoveAt(i);
		}
	}

	void GenerateCracks() {
		cracks.Clear();

		const float REACH_MIN = 0.5f;
		const float REACH_MAX = 1.0f;
		const int MAX_ITER = 80;

		var tips = new List<(Vector2 pos, Vector2 dir, int side, float progress, float reach, float thickness)>();

		//4 corner seeds
		Vector2[] cornerPos = {
			new Vector2(0f, 0.03f),  //bottom-left
			new Vector2(1f, 0.03f),  //bottom-right
			new Vector2(0f, 0.97f),  //top-left
			new Vector2(1f, 0.97f),  //top-right
		};
		int[] cornerSides = { -1, +1, -1, +1 };
		Vector2[] cornerBaseDirs = {
			new Vector2(1f, 1f).normalized,
			new Vector2(-1f, 1f).normalized,
			new Vector2(1f, -1f).normalized,
			new Vector2(-1f, -1f).normalized,
		};
		for (int i = 0; i < 4; i++) {
			float angleJit = Random.Range(-15f, 15f) * Mathf.Deg2Rad;
			float cs = Mathf.Cos(angleJit), sn = Mathf.Sin(angleJit);
			Vector2 d = cornerBaseDirs[i];
			Vector2 dir = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
			float reach = Random.Range(REACH_MIN, REACH_MAX);
			float thickness = Random.Range(CRACK_THICKNESS_MIN, CRACK_THICKNESS_MAX);
			tips.Add((cornerPos[i], dir, cornerSides[i], 0f, reach, thickness));
		}

		//2 middle seeds per side
		const int middleCount = 2;
		for (int i = 0; i < middleCount; i++) {
			float yBase = Mathf.Lerp(0.08f, 0.92f, (i + 1f) / (middleCount + 1f));
			float yL = Mathf.Clamp01(yBase + Random.Range(-0.04f, 0.04f));
			float yR = Mathf.Clamp01(yBase + Random.Range(-0.04f, 0.04f));
			float reachL = Random.Range(REACH_MIN, REACH_MAX);
			float reachR = Random.Range(REACH_MIN, REACH_MAX);
			float thickL = Random.Range(CRACK_THICKNESS_MIN, CRACK_THICKNESS_MAX);
			float thickR = Random.Range(CRACK_THICKNESS_MIN, CRACK_THICKNESS_MAX);
			tips.Add((new Vector2(0f, yL), new Vector2(1f, Random.Range(-0.7f, 0.7f)).normalized, -1, 0f, reachL, thickL));
			tips.Add((new Vector2(1f, yR), new Vector2(-1f, Random.Range(-0.7f, 0.7f)).normalized, +1, 0f, reachR, thickR));
		}

		int iter = 0;
		while (tips.Count > 0 && iter++ < MAX_ITER) {
			var next = new List<(Vector2 pos, Vector2 dir, int side, float progress, float reach, float thickness)>();
			foreach (var tip in tips) {
				float segLen = Mathf.Lerp(0.02f, 0.1f, Mathf.Pow(Random.value, 2f));
				Vector2 jitter = new Vector2(Random.Range(-0.2f, 0.4f), Random.Range(-2f, 2f));
				Vector2 newDir = (tip.dir + jitter).normalized;
				Vector2 centerBias = new Vector2(tip.side == -1 ? 0.25f : -0.25f, 0f);
				newDir = (newDir + centerBias).normalized;
				Vector2 newPos = tip.pos + newDir * segLen;

				bool hitEdge = newPos.y < 0f || newPos.y > 1f;
				if (hitEdge) newPos.y = Mathf.Clamp01(newPos.y);

				float distFromEdge = tip.side == -1 ? newPos.x : 1f - newPos.x;
				float newProgress = Mathf.Clamp01(distFromEdge / tip.reach);

				cracks.Add(new CrackSegment {
					start = tip.pos,
					end = newPos,
					startProgress = tip.progress,
					endProgress = newProgress,
					thickness = tip.thickness
				});

				if (newProgress >= 1f || hitEdge) continue;
				if (newProgress - tip.progress < 0.005f) continue; //kill stuck tips

				if (next.Count < Globals.Instance.maxTips && Random.value < Globals.Instance.branching) {
					float branchReach = Random.Range(REACH_MIN, REACH_MAX);
					if (branchReach > distFromEdge + 0.05f) {
						float branchStartProgress = distFromEdge / branchReach;
						float angleRad = Random.Range(-55f, 55f) * Mathf.Deg2Rad;
						float cs = Mathf.Cos(angleRad);
						float sn = Mathf.Sin(angleRad);
						Vector2 branchDir = new Vector2(newDir.x * cs - newDir.y * sn, newDir.x * sn + newDir.y * cs);
						float branchThickness = tip.thickness * Random.Range(0.5f, 1.1f);
						next.Add((newPos, branchDir, tip.side, branchStartProgress, branchReach, branchThickness));
					}
				}

				next.Add((newPos, newDir, tip.side, newProgress, tip.reach, tip.thickness));
			}
			tips = next;
		}

		cracksGenerated = true;
		Log.Info($"Generated {cracks.Count} segments.");
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
