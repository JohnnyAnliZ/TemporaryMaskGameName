using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Shootable : MonoBehaviour {
	public Rigidbody body;
	public float shotImpulse = 40f;

	[Range(0f, 1f)] public float bounciness = 0.9f;

	public UnityEvent onShot;

	IEnumerator Start() {

		PhysicsMaterial bounce = new PhysicsMaterial("ShootableBounce") {
			bounciness = bounciness,
			bounceCombine = PhysicsMaterialCombine.Maximum
		};

		Collider[] colliders = body.GetComponentsInChildren<Collider>();
		foreach (Collider c in colliders) c.sharedMaterial = bounce;

		yield return null;

		CharacterController player = GameManager.Instance.player3D.GetComponent<CharacterController>();
		foreach (Collider c in colliders) Physics.IgnoreCollision(c, player);
	}

	public void Hit(Vector3 direction, Vector3 point) {
		body.AddForceAtPosition(direction * shotImpulse, point, ForceMode.Impulse);
		onShot.Invoke();
	}
}
