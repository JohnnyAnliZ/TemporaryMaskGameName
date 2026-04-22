using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
	Transform target;
	Transform player3D;
	Camera cam;
	Vector3 smoothPosition;

	public void Init(Transform target, Transform player3D) {
		this.target = target;
		this.player3D = player3D;
		cam = GetComponent<Camera>();
		smoothPosition = new Vector3(target.position.x, target.position.y, Globals.Instance.cameraZOffset);
		transform.position = SnapToGrid(smoothPosition);
		transform.rotation = Quaternion.identity;
	}

	void LateUpdate() {
		if (target == null) return;

		Globals g = Globals.Instance;
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

		//Zoom (clamp orthoSize so frustum fits within bounds)
		float zOffset = player3D.position.z - g.world3DZ;
		float desiredOrtho = Mathf.Max(g.cameraOrthoSize - ComputeZoomDelta(zOffset, g), 0.1f);
		float maxOrthoFromH = (g.cameraBoundTop - g.cameraBoundBottom) * 0.5f;
		float maxOrthoFromW = (g.cameraBoundRight - g.cameraBoundLeft) * 0.5f / Mathf.Max(cam.aspect, 0.001f);
		desiredOrtho = Mathf.Min(desiredOrtho, Mathf.Min(maxOrthoFromH, maxOrthoFromW));
		cam.orthographicSize = desiredOrtho;

		//Pan
		float halfH = desiredOrtho;
		float halfW = halfH * cam.aspect;
		float minX = g.cameraBoundLeft + halfW;
		float maxX = g.cameraBoundRight - halfW;
		float minY = g.cameraBoundBottom + halfH;
		float maxY = g.cameraBoundTop - halfH;
		smoothPosition.x = Mathf.Clamp(smoothPosition.x, minX, maxX);
		smoothPosition.y = Mathf.Clamp(smoothPosition.y, minY, maxY);

		Vector3 finalPosition = new Vector3(smoothPosition.x, smoothPosition.y, g.cameraZOffset);
		transform.position = g.cameraSnapToPixelGrid ? SnapToGrid(finalPosition) : finalPosition;
	}

	static float ComputeZoomDelta(float z, Globals g) {
		if (z >= 0f) {
			float endDelta = g.cameraOrthoSize - g.zoomMaxFarSize;
			if (z <= g.zoomMinFar) return z * g.zoomDeadzoneRate;
			if (z >= g.zoomMaxFar) return endDelta + (z - g.zoomMaxFar) * g.zoomDeadzoneRate;
			float edge = g.zoomMinFar * g.zoomDeadzoneRate;
			float raw = Mathf.InverseLerp(g.zoomMinFar, g.zoomMaxFar, z);
			float t = raw * raw * (3f - 2f * raw);
			return Mathf.Lerp(edge, endDelta, t);
		}
		else {
			float endDelta = g.cameraOrthoSize - g.zoomMaxNearSize;
			float abs = -z;
			if (abs <= g.zoomMinNear) return z * g.zoomDeadzoneRate;
			if (abs >= g.zoomMaxNear) return endDelta - (abs - g.zoomMaxNear) * g.zoomDeadzoneRate;
			float edge = -g.zoomMinNear * g.zoomDeadzoneRate;
			float raw = Mathf.InverseLerp(g.zoomMinNear, g.zoomMaxNear, abs);
			float t = raw * raw * (3f - 2f * raw);
			return Mathf.Lerp(edge, endDelta, t);
		}
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
