using UnityEngine;

public class Shard : MonoBehaviour {
	public float gravityScale = 0.67f;

	Mesh mesh;
	Rigidbody rb;

	public void Init(Mesh cellMesh) {
		mesh = cellMesh;
		rb = GetComponent<Rigidbody>();
	}

	void FixedUpdate() {
		rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
	}

	void OnDestroy() {
		Destroy(mesh);
	}
}
