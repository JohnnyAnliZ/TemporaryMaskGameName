using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ViewportSync
{
	const string SYNC_KEY = "ViewportSync";
	const string GIZMO_KEY = "SpriteGizmos";

	static bool bIsEnabled;
	static bool bIsSyncing;
	static float lastSyncedX;
	static bool bShowSpriteGizmos;

	static ViewportSync() {
		bIsEnabled = EditorPrefs.GetBool(SYNC_KEY, true);
		bShowSpriteGizmos = EditorPrefs.GetBool(GIZMO_KEY, true);
		SceneView.duringSceneGui += OnSceneGUI;
	}

	[MenuItem("Window/Viewport Sync")]
	static void Toggle() {
		bIsEnabled = !bIsEnabled;
		EditorPrefs.SetBool(SYNC_KEY, bIsEnabled); //persist across recompiles
		if (bIsEnabled) {
			SceneView sceneView = EditorWindow.focusedWindow as SceneView;
			if (sceneView != null) lastSyncedX = sceneView.pivot.x;
		}
	}
	[MenuItem("Window/Viewport Sync", true)]
	static bool ToggleValidate() {
		Menu.SetChecked("Window/Viewport Sync", bIsEnabled);
		return true;
	}

	[MenuItem("Window/Sprite Gizmos")]
	static void ToggleGizmos() {
		bShowSpriteGizmos = !bShowSpriteGizmos;
		EditorPrefs.SetBool(GIZMO_KEY, bShowSpriteGizmos);
		SceneView.RepaintAll();
	}
	[MenuItem("Window/Sprite Gizmos", true)]
	static bool ToggleGizmosValidate() {
		Menu.SetChecked("Window/Sprite Gizmos", bShowSpriteGizmos);
		return true;
	}

	static void OnSceneGUI(SceneView view) {
		Globals g = Globals.Instance;


		if (bShowSpriteGizmos) {
			if (view.in2DMode) {
				//3d projection silhouette
				GameObject active = Selection.activeGameObject;
				if (active != null) {
					Platform platform = active.GetComponentInParent<Platform>();
					if (platform != null) {
						Vector3 camForward = view.camera.transform.forward;
						float z = g.world2DZ - 10;
						Handles.color = Color.cyan;
						MeshFilter[] filters = platform.GetComponentsInChildren<MeshFilter>();
						foreach (MeshFilter mf in filters) {
							DrawSilhouette(mf, camForward, z);
						}
					}
				}

				//2D camera preview
				float aspect = 16f / 9f;
				Vector3 c = view.pivot;
				c.z = g.world2DZ;

				float baseSize = g.cameraOrthoSize;
				float minSize = Mathf.Max(baseSize - g.zoomMaxFarAmount, 0.1f);
				float maxSize = baseSize + g.zoomMaxNearAmount;

				DrawViewBox(c, minSize, aspect, new Color(1f, 0.5f, 0f, 0.4f), 0.5f);
				DrawViewBox(c, maxSize, aspect, new Color(1f, 0.5f, 0f, 0.4f), 0.5f);
				DrawViewBox(c, baseSize, aspect, Color.yellow, 0.5f);
			} else {
				//2d projection bounding boxes
				GameObject parent2D = GameObject.Find("2DScene");
				if (parent2D != null) {
					SpriteRenderer[] sprites = parent2D.GetComponentsInChildren<SpriteRenderer>();
					foreach (SpriteRenderer sprite in sprites) {
						if (sprite.sprite == null) continue;
						if (sprite.CompareTag("NoGizmo")) continue;

						float zOffset = (sprite.transform.position.z - g.world2DZ) * g.platformDistance;
						Vector3 center = new Vector3(sprite.bounds.center.x, sprite.bounds.center.y, g.world3DZ + zOffset + g.zOffset);
						Vector3 size = new Vector3(sprite.bounds.size.x, sprite.bounds.size.y, g.projectionSize);

						//Shaded Cube
						Vector3 halfSize = size * 0.5f;
						Color faceColor = new Color(0, 1, 0, 0.05f);

						//Front face (Z+)
						Vector3[] front = {
							center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
							center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(front, faceColor, Color.clear);

						//Back face (Z-)
						Vector3[] back = {
							center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
							center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
							center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, -halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(back, faceColor, Color.clear);

						//Top face (Y+)
						Vector3[] top = {
							center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
							center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(top, faceColor, Color.clear);

						//Bottom face (Y-)
						Vector3[] bottom = {
							center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
							center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(bottom, faceColor, Color.clear);

						//Right face (X+)
						Vector3[] right = {
							center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
							center + new Vector3(halfSize.x, halfSize.y, halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(right, faceColor, Color.clear);

						//Left face (X-)
						Vector3[] left = {
							center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
							center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
							center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
							center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z)
						};
						Handles.DrawSolidRectangleWithOutline(left, faceColor, Color.clear);

						//Wireframe
						Handles.color = new Color(0, 1, 0, 0.8f);
						Handles.DrawWireCube(center, size);
					}
				}

				//World3DZ plane
				float extent = 10000f;
				Vector3[] plane = {
					new Vector3(-extent, -extent, g.world3DZ),
					new Vector3(extent, -extent, g.world3DZ),
					new Vector3(extent, extent, g.world3DZ),
					new Vector3(-extent, extent, g.world3DZ),
				};
				Handles.color = Color.white; //reset from previous use of Handles
				Handles.DrawSolidRectangleWithOutline(plane, new Color(1, 0, 0, 0.02f), Color.clear);
			}
		}

		if (!bIsEnabled || bIsSyncing) return;
		foreach (SceneView sceneView in SceneView.sceneViews) {
			if (sceneView.in2DMode) {
				var settings = sceneView.cameraSettings;
				settings.dynamicClip = false;
				settings.nearClip = g.camera2DNearClip;
				settings.farClip = g.camera2DFarClip;
				sceneView.cameraSettings = settings;

				Vector3 pivot = sceneView.pivot;
				pivot.z = 0;
				sceneView.pivot = pivot;
			}
		}

		if (!view.hasFocus) return;

		float viewX = view.pivot.x;
		if (Mathf.Approximately(viewX, lastSyncedX)) return;
		lastSyncedX = viewX;

		bIsSyncing = true;
		foreach (SceneView other in SceneView.sceneViews) {
			if (other == view) continue;
			Vector3 pivot = other.pivot;
			pivot.x = viewX;
			other.pivot = pivot;
			other.Repaint();
		}
		bIsSyncing = false;
	}

	static void DrawViewBox(Vector3 center, float halfH, float aspect, Color color, float thickness) {
		float halfW = halfH * aspect;
		Vector3 tl = center + new Vector3(-halfW,  halfH, 0f);
		Vector3 tr = center + new Vector3( halfW,  halfH, 0f);
		Vector3 br = center + new Vector3( halfW, -halfH, 0f);
		Vector3 bl = center + new Vector3(-halfW, -halfH, 0f);
		Handles.color = color;
		Handles.DrawLine(tl, tr, thickness);
		Handles.DrawLine(tr, br, thickness);
		Handles.DrawLine(br, bl, thickness);
		Handles.DrawLine(bl, tl, thickness);
	}

	//Don't need to look down here----------------------------------------------
	class SilhouetteCache {
		public Mesh mesh;
		public Quaternion rotation;
		public Vector3 scale;
		public Vector3 camForward;
		public Vector3[] localVerts;
		public int[] edgePairs;       //flat: [a0,b0, a1,b1, ...] local-vert indices
		public Vector3[] worldEdges;  //scratch buffer, same length as edgePairs
		public Matrix4x4 lastMatrix;
		public float lastZ;
	}

	static Dictionary<int, SilhouetteCache> silhouetteCache = new Dictionary<int, SilhouetteCache>();

	//Scratch buffers reused across rebuilds (sized to actual mesh on demand)
	static List<int> s_pairList = new List<int>(1024);
	static List<Vector3> s_meshVerts = new List<Vector3>(1024);
	static List<int> s_meshTris = new List<int>(3072);
	static Vector3[] s_wverts = System.Array.Empty<Vector3>();
	static long[] s_edgeBuf = System.Array.Empty<long>(); //packed: (min<<33)|((uint)max<<1)|frontBit

	static void DrawSilhouette(MeshFilter mf, Vector3 camForward, float z) {
		Mesh mesh = mf.sharedMesh;
		if (mesh == null || !mesh.isReadable) return;

		int key = mf.GetInstanceID();
		Transform tf = mf.transform;
		Quaternion rot = tf.rotation;
		Vector3 scale = tf.lossyScale;

		//Invalidate cache on topology/orientation/camera changes
		if (!silhouetteCache.TryGetValue(key, out SilhouetteCache cache)
			|| cache.mesh != mesh
			|| cache.rotation != rot
			|| cache.scale != scale
			|| cache.camForward != camForward) {
			cache = RebuildSilhouette(mesh, rot, scale, camForward);
			silhouetteCache[key] = cache;
		}

		if (cache.edgePairs.Length == 0) return;

		//Rebuild world endpoints only when transform or z changes
		Matrix4x4 m = tf.localToWorldMatrix;
		if (m != cache.lastMatrix || cache.lastZ != z) {
			int[] pairs = cache.edgePairs;
			Vector3[] verts = cache.localVerts;
			Vector3[] buf = cache.worldEdges;
			for (int i = 0; i < pairs.Length; i++) {
				Vector3 w = m.MultiplyPoint3x4(verts[pairs[i]]);
				w.z = z;
				buf[i] = w;
			}
			cache.lastMatrix = m;
			cache.lastZ = z;
		}

		Handles.DrawLines(cache.worldEdges);
	}

	static SilhouetteCache RebuildSilhouette(Mesh mesh, Quaternion rot, Vector3 scale, Vector3 camForward) {
		//Zero-alloc mesh data fetch
		s_meshVerts.Clear();
		mesh.GetVertices(s_meshVerts);
		s_meshTris.Clear();
		mesh.GetTriangles(s_meshTris, 0);
		int n = s_meshVerts.Count;
		int triCount = s_meshTris.Count / 3;
		int maxEdges = triCount * 3;

		if (s_wverts.Length < n) s_wverts = new Vector3[n];
		if (s_edgeBuf.Length < maxEdges) s_edgeBuf = new long[maxEdges];

		//Orientation-only transform (translation doesn't affect cross-product facing)
		Matrix4x4 rs = Matrix4x4.TRS(Vector3.zero, rot, scale);
		bool flipWinding = scale.x * scale.y * scale.z < 0f;

		for (int i = 0; i < n; i++) {
			s_wverts[i] = rs.MultiplyPoint3x4(s_meshVerts[i]);
		}

		//Emit one packed entry per triangle-edge: (minVert<<33)|((uint)maxVert<<1)|frontBit
		int edgeIdx = 0;
		for (int t = 0; t < triCount; t++) {
			int baseIdx = t * 3;
			int i0 = s_meshTris[baseIdx];
			int i1 = s_meshTris[baseIdx + 1];
			int i2 = s_meshTris[baseIdx + 2];
			Vector3 v0 = s_wverts[i0];
			Vector3 v1 = s_wverts[i1];
			Vector3 v2 = s_wverts[i2];
			Vector3 nrm = Vector3.Cross(v1 - v0, v2 - v0);
			if (nrm.sqrMagnitude < 1e-12f) continue; //degenerate
			//Branchless: xor of "faces camera" with winding flip
			long frontBit = ((Vector3.Dot(nrm, camForward) < 0f) != flipWinding) ? 1L : 0L;
			s_edgeBuf[edgeIdx++] = PackEdge(i0, i1, frontBit);
			s_edgeBuf[edgeIdx++] = PackEdge(i1, i2, frontBit);
			s_edgeBuf[edgeIdx++] = PackEdge(i2, i0, frontBit);
		}

		//Sort groups identical edges (same min,max) consecutively; within a group back (0) sorts before front (1)
		System.Array.Sort(s_edgeBuf, 0, edgeIdx);

		//Scan runs of equal edge-id; silhouette = boundary (count==1) OR mixed front/back
		s_pairList.Clear();
		int i2scan = 0;
		while (i2scan < edgeIdx) {
			long entry = s_edgeBuf[i2scan];
			long edgeId = entry >> 1;
			int count = 0;
			int frontCount = 0;
			int j = i2scan;
			while (j < edgeIdx && (s_edgeBuf[j] >> 1) == edgeId) {
				count++;
				frontCount += (int)(s_edgeBuf[j] & 1L);
				j++;
			}
			if (count == 1 || (frontCount > 0 && frontCount < count)) {
				int a = (int)(edgeId >> 32);
				int b = (int)(edgeId & 0xFFFFFFFFL);
				s_pairList.Add(a);
				s_pairList.Add(b);
			}
			i2scan = j;
		}

		int[] pairs = s_pairList.ToArray();
		Vector3[] localVerts = new Vector3[n];
		s_meshVerts.CopyTo(localVerts);
		return new SilhouetteCache {
			mesh = mesh,
			rotation = rot,
			scale = scale,
			camForward = camForward,
			localVerts = localVerts,
			edgePairs = pairs,
			worldEdges = new Vector3[pairs.Length],
			lastMatrix = Matrix4x4.zero,
			lastZ = float.NaN,
		};
	}

	static long PackEdge(int a, int b, long frontBit) {
		int min = a < b ? a : b;
		int max = a < b ? b : a;
		return ((long)min << 33) | ((long)(uint)max << 1) | frontBit;
	}
}
