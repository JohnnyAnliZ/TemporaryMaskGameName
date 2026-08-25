using UnityEngine;

public class SectionStart : MonoBehaviour
{
	public Section section;
	public GameplayStart gameplayStart;

	public static SectionStart Find(Section section, GameplayStart start = default) {
		foreach (SectionStart s in FindObjectsByType<SectionStart>(FindObjectsSortMode.None)) {
			if (s.section == section && (section != Section.Gameplay || s.gameplayStart == start)) {
				return s;
			}
		}
		Log.Error($"No SectionStart marker for {section}/{start}");
		return null;
	}

	void OnDrawGizmos() {
		Gizmos.color = Color.darkGreen;
		Vector3 p = transform.position;
		Gizmos.DrawWireSphere(p, 0.5f);
		Gizmos.DrawWireSphere(new Vector3(p.x, p.y, Globals.Instance.world2DZ), 0.5f);
	}
}