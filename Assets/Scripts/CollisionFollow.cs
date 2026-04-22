using UnityEngine;

[DefaultExecutionOrder(101)]
public class CollisionFollow : MonoBehaviour
{
	public Transform source;
	Vector3 offset;

	void Awake() {
		offset = transform.position - source.position;
	}

	void LateUpdate() {
		transform.position = source.position + offset;
	}
}
