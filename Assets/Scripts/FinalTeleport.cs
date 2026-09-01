using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinalTeleport : MonoBehaviour
{
	public Transform target;

	void OnTriggerEnter(Collider other) {
		if (other.GetComponentInParent<Player3DController>() != null) {
			GameManager.Instance.player3D.GetComponent<Player3DController>().Teleport(target.position);
		}
	}
}