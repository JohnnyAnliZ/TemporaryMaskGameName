using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
	public float deadzoneRight = 1f;
	public float deadzoneLeft = 2f; //asymmetric like mario, maybe
	public float deadzoneTop = 2f;
	public float deadzoneBottom = 2f;

	public float followSpeed = 8f;
	public float zOffset = -10f;

	public bool snapToPixelGrid = true;

	Transform target;
	Vector3 smoothPosition; //internally track smooth position but render snapped

	//Called by GameManager
	public void Init(Transform target) {
		this.target = target;
		smoothPosition = new Vector3(target.position.x, target.position.y, zOffset);
		transform.position = SnapToGrid(smoothPosition);
		transform.rotation = Quaternion.identity;
	}

	void LateUpdate() {
		if (target == null) return;

		Vector3 cameraCenter = new Vector3(smoothPosition.x, smoothPosition.y, 0f);
		Vector3 targetPos = new Vector3(target.position.x, target.position.y, 0f);
		Vector3 offset = targetPos - cameraCenter;
		Vector3 desiredMove = Vector3.zero;

		if (offset.x > deadzoneRight) {
			desiredMove.x = offset.x - deadzoneRight;
		} else if (offset.x < -deadzoneLeft) {
			desiredMove.x = offset.x + deadzoneLeft;
		}
		if (offset.y > deadzoneTop) {
			desiredMove.y = offset.y - deadzoneTop;
		} else if (offset.y < -deadzoneBottom) {
			desiredMove.y = offset.y + deadzoneBottom;
		}

		if (desiredMove != Vector3.zero) {
			float t = Mathf.Min(followSpeed * Time.deltaTime, 1f);
			smoothPosition += desiredMove * t;
		}

		Vector3 finalPosition = new Vector3(smoothPosition.x, smoothPosition.y, zOffset);
		transform.position = snapToPixelGrid ? SnapToGrid(finalPosition) : finalPosition;
	}

	Vector3 SnapToGrid(Vector3 position) {
		var g = Globals.Instance;
		if (g == null) return position;

		float gridSize = g.pixelGridSize;
		return new Vector3(
			Mathf.Round(position.x / gridSize) * gridSize,
			Mathf.Round(position.y / gridSize) * gridSize,
			position.z
		);
	}

	//Debug visualization
	void OnDrawGizmosSelected() {
		if (!enabled) return;

		Vector3 center = Application.isPlaying ? smoothPosition : transform.position;
		center.z = 0f;

		Gizmos.color = new Color(0f, 1f, 0f, 0.5f);

		//Deadzone box
		Vector3 boxSize = new Vector3(deadzoneLeft + deadzoneRight, deadzoneTop + deadzoneBottom, 0f);
		Vector3 boxCenter = center + new Vector3((deadzoneRight - deadzoneLeft) * 0.5f, (deadzoneTop - deadzoneBottom) * 0.5f, 0f);
		Gizmos.DrawCube(boxCenter, boxSize);
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(boxCenter, boxSize);

		//Center
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(center, 0.1f);
	}
}
