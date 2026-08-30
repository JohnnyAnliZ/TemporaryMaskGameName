using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShrinkTrigger : MonoBehaviour {
	public Transform lookTarget;
	public Transform floatTarget;

	public GameObject handMesh;
	public Vector3 handFromPos, handFromEuler;
	public Vector3 handToPos, handToEuler;
	public float handReachDuration = 1f;

	[HideInInspector] public bool bEntered; //cleared on loop start

	void OnTriggerEnter(Collider other) {
		if (bEntered) return;
		if(other.GetComponentInParent<Player3DController>() == null) return;
        bEntered = true;
		StartCoroutine(Sequence(other.GetComponentInParent<Player3DController>()));
	}

	IEnumerator Sequence(Player3DController pc) {
		yield return new WaitForSeconds(0.25f);
		
		Globals g = Globals.Instance;

		GameManager.Instance.bInputEnabled = false;
		pc.BeginFloatTo(floatTarget.position, g.panDuration + 1);

		yield return new WaitForSeconds(1);

		FirstPersonLook look = FindAnyObjectByType<FirstPersonLook>();
		yield return look.PanToTarget(lookTarget, g.panDuration);

		Vector3 fromPos = handFromPos + Vector3.forward * 200f;
		Vector3 toPos = handToPos + Vector3.forward * 200f;
		handMesh.SetActive(true);
		Quaternion fromRot = Quaternion.Euler(handFromEuler);
		Quaternion toRot = Quaternion.Euler(handToEuler);
		for (float t = 0f; t < handReachDuration; t += Time.deltaTime) {
			float u = Mathf.Clamp01(t / handReachDuration);
			u = u * u * (3f - 2f * u);
			handMesh.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, u), Quaternion.Slerp(fromRot, toRot, u));
			yield return null;
		}
		handMesh.transform.SetPositionAndRotation(toPos, toRot);

		yield return new WaitForSeconds(1);

		CompositeManager.Instance.maskDrawer.Do_ShrinkAll();
		yield return new WaitForSeconds(g.waitDuration);

		handMesh.SetActive(false);
		GameManager.Instance.runner.CompleteSection();
	}
}
