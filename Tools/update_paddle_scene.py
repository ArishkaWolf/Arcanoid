"""Change only the Player shape in the existing scene, preserving the other objects."""
from pathlib import Path
import re

path = Path(__file__).resolve().parents[1] / "Assets/Scenes/MainScene.unity"
text = path.read_text(encoding="utf-8")
blocks = list(re.finditer(r"(?ms)^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)", text))
player = next(b for b in blocks if b[1] == "1" and re.search(r'(?m)^  m_Name: "?Player"?$', b[3]))
player_id = player[2]
transform = next(b for b in blocks if b[1] == "4" and f"m_GameObject: {{fileID: {player_id}}}" in b[3])
collider = next(b for b in blocks if b[1] in ("58", "60") and f"m_GameObject: {{fileID: {player_id}}}" in b[3])
replacements = []
changed = re.sub(r"(?m)^  m_LocalPosition:.*$", "  m_LocalPosition: {x: 0, y: -4.85, z: 0}", transform[0])
changed = re.sub(r"(?m)^  m_LocalScale:.*$", "  m_LocalScale: {x: 1.5, y: 1.5, z: 1}", changed)
replacements.append((transform.start(), transform.end(), changed))
material = re.search(r"(?m)^  m_Material: (.*)$", collider[3])[1]
circle = f"""--- !u!58 &{collider[2]}
CircleCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {player_id}}}
  m_Enabled: 1
  m_Density: 1
  m_Material: {material}
  m_IsTrigger: 0
  m_UsedByEffector: 0
  m_UsedByComposite: 0
  m_Offset: {{x: 0, y: 0}}
  serializedVersion: 2
  m_Radius: 0.62
"""
replacements.append((collider.start(), collider.end(), circle))
for start, end, value in sorted(replacements, reverse=True):
    text = text[:start] + value + text[end:]
path.write_text(text, encoding="utf-8")
print("Updated Player only: uniform circle, radius 0.62, scale 1.5, center Y=-4.85.")
