using UnityEngine;

//Basically we treat the 3d player as the source of truth and simply copy transform instead of directing input to here as well
public class Player2DVisual : MonoBehaviour {
	public bool snapToPixelGrid = true;

	Transform source;

	public void Init(Transform source) {
		this.source = source;
	}

	void LateUpdate() {
		if (source == null) return;
		var g = Globals.Instance;

		Vector3 position = new Vector3(source.position.x, source.position.y, g.world2DZ);

		if (snapToPixelGrid) {
			float gridSize = g.pixelGridSize;
			position.x = Mathf.Round(position.x / gridSize) * gridSize;
			position.y = Mathf.Round(position.y / gridSize) * gridSize;
		}

		transform.position = position;
	}
}
