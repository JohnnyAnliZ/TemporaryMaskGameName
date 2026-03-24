using UnityEngine;

public class PlayerStart : MonoBehaviour
{
	void OnDrawGizmos() {
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(transform.position, 0.5f);
		Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
	}
}
