using UnityEngine;
using System.Collections.Generic;

public class SectionAsset : ScriptableObject
{
	public Section section;
	[SerializeReference] public List<Subsection> subsections = new();
}
