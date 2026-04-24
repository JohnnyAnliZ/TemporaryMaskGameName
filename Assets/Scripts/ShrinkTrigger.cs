using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShrinkTrigger : MonoBehaviour {
	public Transform lookTarget;

	public GameObject handMesh;
	public Animator handAnimator;
	public string handAnimTrigger = "reach";
	public float handAnimDuration = 1f;

	bool bEntered;

	void OnTriggerEnter(Collider other) {
		if (bEntered) return;
		bEntered = true;
		StartCoroutine(Sequence(other.GetComponentInParent<Player3DController>()));
	}

	IEnumerator Sequence(Player3DController pc) {
		Globals g = Globals.Instance;
		pc.BeginFreeze(g.freezeDuration);

		FirstPersonLook look = FindAnyObjectByType<FirstPersonLook>();
		yield return look.PanToTarget(lookTarget, g.panDuration);

		if (handMesh != null) handMesh.SetActive(true);
		if (handAnimator != null && !string.IsNullOrEmpty(handAnimTrigger)) handAnimator.SetTrigger(handAnimTrigger);
		if (handAnimDuration > 0f) yield return new WaitForSeconds(handAnimDuration);

		CompositeManager.Instance.maskDrawer.Do_ShrinkAll();
		yield return new WaitForSeconds(g.waitDuration);
		GameManager.Instance.AdvanceSubsection();
	}
}
