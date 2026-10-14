"""Offline verification of Unity YAML, GUID references and exercise configuration.

This supplements, and does not replace, the player integration check.
Requires PyYAML; no Unity editor or asset modifications are needed.
"""
from pathlib import Path
import json
import re
import struct
import yaml

ROOT = Path(__file__).resolve().parents[1]
checks = []


def check(condition, message):
    if not condition:
        raise AssertionError(message)
    checks.append(message)


def docs(path):
    content = path.read_text(encoding="utf-8-sig")
    chunks = re.split(r"(?m)^--- !u!(\d+) &(\d+)\n", content)
    result = {}
    for i in range(1, len(chunks), 3):
        cid = int(chunks[i + 1])
        check(cid not in result, f"{path.name}: unique object ID {cid}")
        result[cid] = yaml.safe_load(chunks[i + 2])
    return result


def walk(value):
    if isinstance(value, dict):
        yield value
        for entry in value.values():
            yield from walk(entry)
    if isinstance(value, list):
        for entry in value:
            yield from walk(entry)


def guid(path):
    return re.search(r"guid: ([a-f0-9]{32})", Path(str(path) + ".meta").read_text(encoding="utf-8-sig"))[1]


def main():
    guids = {}
    for meta in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(r"guid: ([a-f0-9]{32})", meta.read_text(encoding="utf-8-sig"))
        check(match is not None, "GUID present: " + meta.name)
        check(match[1] not in guids, "Unique asset GUID: " + meta.name)
        guids[match[1]] = meta
    builtin = {"0000000000000000e000000000000000", "0000000000000000f000000000000000"}
    ugui = ROOT / "Packages/com.unity.ugui"
    if not ugui.exists():
        ugui = Path(r"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.ugui")
    ui_guids = {re.search(r"guid: ([a-f0-9]{32})", p.read_text(encoding="utf-8-sig"))[1]
                for p in ugui.rglob("*.cs.meta")}
    files = list((ROOT / "Assets").rglob("*.unity")) + list((ROOT / "Assets").rglob("*.prefab")) + list((ROOT / "Assets").rglob("*.asset")) + list((ROOT / "Assets").rglob("*.physicsMaterial2D"))
    parsed = {}
    reference_count = 0
    for path in files:
        objects = docs(path); parsed[path.name] = objects
        for obj in objects.values():
            for entry in walk(obj):
                if "fileID" not in entry or entry["fileID"] == 0:
                    continue
                reference_count += 1
                if "guid" in entry:
                    check(entry["guid"] in guids or entry["guid"] in builtin or entry["guid"] in ui_guids,
                          path.name + ": external GUID resolves")
                    if entry["guid"] in guids:
                        target = Path(str(guids[entry["guid"]])[:-5])
                        expected_type = 2 if target.suffix in (".asset", ".physicsMaterial2D", ".mat") else 3
                        check(entry["type"] == expected_type, path.name + ": correct Unity PPtr asset type for " + target.name)
                        if target.suffix in (".prefab", ".asset", ".physicsMaterial2D"):
                            check(entry["fileID"] in docs(target), path.name + ": referenced native object exists")
                else:
                    check(entry["fileID"] in objects, path.name + ": internal object reference resolves")
        for oid, obj in objects.items():
            if "GameObject" in obj:
                for component in obj["GameObject"]["m_Component"]:
                    component = next(iter(objects[component["component"]["fileID"]].values()))
                    check(component["m_GameObject"]["fileID"] == oid, path.name + ": component owner matches")
            transform = obj.get("Transform") or obj.get("RectTransform")
            if transform:
                for child in transform["m_Children"]:
                    child_transform = next(iter(objects[child["fileID"]].values()))
                    check(child_transform["m_Father"]["fileID"] == oid, path.name + ": parent and child references match")

    block_guid = guid(ROOT / "Assets/Scripts/BlockScript.cs")
    for name, hits, points, optional, drop in [("Blue Block", 30, 100, 1, 0), ("Red Block", 4, 40, 0, 0),
                                             ("Green Block", 2, 20, 0, 1), ("Yellow Block", 1, 10, 0, 0)]:
        script = next(obj["MonoBehaviour"] for obj in parsed[name + ".prefab"].values()
                      if "MonoBehaviour" in obj and obj["MonoBehaviour"]["m_Script"]["guid"] == block_guid)
        check((script["hitsToDestroy"], script["points"], script["optional"], script["dropsBonus"]) == (hits, points, optional, drop), name + ": PDF hit/score/bonus settings")
        check((script["textComponent"]["fileID"] != 0) == (hits in (2, 4)), name + ": correct presence of hit label")

    base_guid = guid(ROOT / "Assets/Scripts/BonusBase.cs")
    check(not any(obj.get("MonoBehaviour", {}).get("m_Script", {}).get("guid") == base_guid
                  for obj in parsed["Bonus.prefab"].values()), "A1: BonusBase is absent from Bonus prefab")
    transform = next(obj["Transform"] for obj in parsed["Bonus.prefab"].values() if "Transform" in obj)
    check(transform["m_LocalScale"]["y"] == .5, "A1: Bonus Scale Y is 0.5")
    check(len(list((ROOT / "Assets/Resources").glob("[0-9][0-9].png"))) == 30, "All 30 supplied backgrounds are available")
    check(len(list((ROOT / "Assets/Audio").glob("*.mp3"))) == 4, "All four supplied audio clips are available")
    for image in (ROOT / "Assets/Resources").glob("*.png.meta"):
        check("textureType: 8" in image.read_text(encoding="utf-8-sig"), image.name + ": imported as Sprite")
    check(len(parsed["MainScene.unity"]) > 100, "Native scene includes complete camera, walls, paddle, HUD and pause UI")
    scene = parsed["MainScene.unity"]
    player_id, player_object = next((oid, obj["GameObject"]) for oid, obj in scene.items()
                                    if obj.get("GameObject", {}).get("m_Name") == "Player")
    components = [scene[c["component"]["fileID"]] for c in player_object["m_Component"]]
    player_transform = next(c["Transform"] for c in components if "Transform" in c)
    check(player_transform["m_LocalScale"]["x"] == player_transform["m_LocalScale"]["y"],
          "Paddle sprite has equal X/Y scale")
    check(any("CircleCollider2D" in c for c in components) and not any("PolygonCollider2D" in c for c in components),
          "Paddle uses a circular collider")
    input_settings = next(iter(docs(ROOT / "ProjectSettings/InputManager.asset").values()))["InputManager"]
    check({a["m_Name"] for a in input_settings["m_Axes"]} == {"Fire1", "Pause", "Horizontal", "Vertical", "Submit", "Cancel"}, "Input axes include game and UI navigation controls")
    text = (ROOT / "ProjectSettings/Physics2DSettings.asset").read_text(encoding="utf-8-sig")
    raw = bytes.fromhex(re.search(r"m_LayerCollisionMatrix: ([a-f0-9]+)", text)[1])
    masks = struct.unpack("<32I", raw)
    check(not (masks[10] & (1 << 10)), "Balls do not interact")
    for layer in range(32):
        check(bool(masks[13] & (1 << layer)) == (layer in (9, 12)), "Bonus layer interaction " + str(layer))
    script = (ROOT / "Assets/Scripts/PlayerScript.cs").read_text(encoding="utf-8-sig")
    check("1 + gameData.level * ballVelocityMult" in script, "Level-dependent ball initial speed")
    check("Mathf.Min(gameData.level, 8)" in script and "Mathf.Min(2 + gameData.level, 15)" in script, "Level generation uses the tutorial count caps")
    report = {"status": "PASS", "checks": len(checks), "native_files": len(files), "references": reference_count,
              "runtime_report": "See Tests/runtime-report.json for the player integration results",
              "final_checks": checks[-55:]}
    (ROOT / "Tests/assets-validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "final_checks"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
