using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
	Transform target;
	Vector3 smoothPosition;

	public void Init(Transform target) {
		this.target = target;
		smoothPosition = new Vector3(target.position.x, target.position.y, Globals.Instance.cameraZOffset);
		transform.position = SnapToGrid(smoothPosition);
		transform.rotation = Quaternion.identity;
	}

	void LateUpdate() {
		if (target == null) return;

		var g = Globals.Instance;
		Vector3 cameraCenter = new Vector3(smoothPosition.x, smoothPosition.y, 0f);
		Vector3 targetPos = new Vector3(target.position.x, target.position.y, 0f);
		Vector3 offset = targetPos - cameraCenter;
		Vector3 desiredMove = Vector3.zero;

		if (offset.x > g.cameraDeadzoneRight)
			desiredMove.x = offset.x - g.cameraDeadzoneRight;
		else if (offset.x < -g.cameraDeadzoneLeft)
			desiredMove.x = offset.x + g.cameraDeadzoneLeft;

		if (offset.y > g.cameraDeadzoneTop)
			desiredMove.y = offset.y - g.cameraDeadzoneTop;
		else if (offset.y < -g.cameraDeadzoneBottom)
			desiredMove.y = offset.y + g.cameraDeadzoneBottom;

		if (desiredMove != Vector3.zero) {
			float t = Mathf.Min(g.cameraFollowSpeed * Time.deltaTime, 1f);
			smoothPosition += desiredMove * t;
		}

		Vector3 finalPosition = new Vector3(smoothPosition.x, smoothPosition.y, g.cameraZOffset);
		transform.position = g.cameraSnapToPixelGrid ? SnapToGrid(finalPosition) : finalPosition;
	}

	Vector3 SnapToGrid(Vector3 position) {
		var g = Globals.Instance;
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
		var g = Globals.Instance;

		Vector3 center = Application.isPlaying ? smoothPosition : transform.position;
		center.z = 0f;

		//Deadzone Box
		Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
		Vector3 boxSize = new Vector3(g.cameraDeadzoneLeft + g.cameraDeadzoneRight, g.cameraDeadzoneTop + g.cameraDeadzoneBottom, 0f);
		Vector3 boxCenter = center + new Vector3((g.cameraDeadzoneRight - g.cameraDeadzoneLeft) * 0.5f, (g.cameraDeadzoneTop - g.cameraDeadzoneBottom) * 0.5f, 0f);
		Gizmos.DrawCube(boxCenter, boxSize);
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(boxCenter, boxSize);

		//Center
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(center, 0.1f);
	}
}
