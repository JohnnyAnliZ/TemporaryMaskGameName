using UnityEngine;
using UnityEngine.Rendering;

public class MaskDrawer : MonoBehaviour
{
	Material circleMaskMaterial;
	Material blurMaterial;
	Material shardMaterial;

	float shardMinSpawnDistance = 0.1f;
	float shardMaxSpawnDistance = 10f;
	float shardSpawnDistancePullback = 0.9f;
	float shardThickness = 0.05f;

	int currentPass = 0;
	CommandBuffer cmd;
	Camera cam2D;
	Camera cam3D;
	RenderTexture maskRT;
	RenderTexture frozenRT;
	Transform shardParent;
	PhysicsMaterial shardPhysMat;

	RenderTexture blackRT;

	void Start() {
		circleMaskMaterial = new Material(Shader.Find("Custom/CircleMask"));
		blurMaterial = new Material(Shader.Find("Custom/MaskBlur"));
		shardMaterial = new Material(Shader.Find("Custom/Shard"));
	}

	//Called by composite manager
	public void Configure(Camera cam2D, Camera cam3D, RenderTexture maskRT) {
		this.cam2D = cam2D;
		this.cam3D = cam3D;
		this.maskRT = maskRT;
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
		cmd.SetGlobalInt("_NumPasses", Globals.Instance.numBreaks);
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

		cmd.SetGlobalInt("_Num2DTo3DPasses", Globals.Instance.numBreaks);
		cmd.SetGlobalInt("_Num3DToBlackPasses", 3);
        cmd.SetGlobalInt("_PassIndex", currentPass);


        cmd.DrawProcedural(Matrix4x4.identity, circleMaskMaterial, 0, MeshTopology.Triangles, 3, 1);
		Graphics.ExecuteCommandBuffer(cmd);
	}

	public void ResetMask() {
		currentPass = 0;
	}

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

		AudioManager.Instance.PlayShatter(); // Play sfx

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
