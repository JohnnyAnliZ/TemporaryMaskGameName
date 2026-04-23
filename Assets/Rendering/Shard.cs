using UnityEngine;

public class Shard : MonoBehaviour {
	public float gravityScale = 1f;

	Material mat;
	Mesh mesh;
	Rigidbody rb;

	public void Init(Material material, Texture capturedTex, Mesh cellMesh) {
		mat = material;
		mesh = cellMesh;
		mat.mainTexture = capturedTex;
		rb = GetComponent<Rigidbody>();
	}

	void FixedUpdate() {
		rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
	}

	void OnDestroy() {
		Destroy(mat);
		Destroy(mesh);
	}
}
