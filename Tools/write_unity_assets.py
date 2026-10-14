"""Write the initial native Unity scene/prefabs without running the licensed editor.

The editor menu Arcanoid/Recreate scene and prefabs is the canonical regeneration
path once Unity is available. GUIDs remain stable when rerunning this script.
"""
from pathlib import Path
import json
import math
import re
import tarfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
EDITOR = Path(r"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data")
UGUI = EDITOR / "Resources/PackageManager/BuiltInPackages/com.unity.ugui"


def metadata(path, kind=None):
    path = Path(path)
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "arcanoid-study/" + str(path.relative_to(ROOT))).hex
        importer = "MonoImporter" if path.suffix == ".cs" else "PrefabImporter" if path.suffix == ".prefab" else "NativeFormatImporter" if path.suffix in (".asset", ".physicsMaterial2D") else "DefaultImporter"
        details = ""
        if importer == "MonoImporter":
            details = "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
        if importer == "NativeFormatImporter":
            details = "  mainObjectFileID: " + str(6200000 if path.suffix == ".physicsMaterial2D" else 11400000) + "\n"
        meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + ("folderAsset: yes\n" if path.is_dir() else "") + importer + ":\n  externalObjects: {}\n" + details + "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    return re.search(r"guid: ([a-f0-9]{32})", meta.read_text(encoding="utf-8-sig"))[1]


class Flow(dict):
    pass


def ref(file_id=0, guid=None, kind=3):
    return Flow(fileID=file_id, **({"guid": guid, "type": kind} if guid else {}))


def asset(path, file_id):
    # PPtr type 2 is a native Unity asset; type 3 is an imported asset.
    # Using type 3 for ScriptableObject/PhysicsMaterial leaves null references in Unity.
    kind = 2 if Path(path).suffix in (".asset", ".physicsMaterial2D", ".mat") else 3
    return ref(file_id, metadata(ROOT / path), kind)


def vec(*values):
    return Flow(zip("xyzw", values))


def color(*values):
    return Flow(zip("rgba", values))


def scalar(value):
    if isinstance(value, Flow):
        return "{" + ", ".join(str(k) + ": " + (str(v) if k == "guid" else scalar(v)) for k, v in value.items()) + "}"
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, bool):
        return "1" if value else "0"
    if value is None:
        return ""
    return str(value)


def dump(value, indent=0):
    lines = []
    pad = " " * indent
    for key, item in value.items():
        if isinstance(item, dict) and not isinstance(item, Flow):
            if item:
                lines += [pad + key + ":", dump(item, indent + 2)]
            else:
                lines.append(pad + key + ": {}")
        elif isinstance(item, list):
            if not item:
                lines.append(pad + key + ": []")
            else:
                lines.append(pad + key + ":")
                for entry in item:
                    if isinstance(entry, dict) and not isinstance(entry, Flow):
                        sub = dump(entry, indent + 2).splitlines()
                        lines += [pad + "- " + sub[0].lstrip()] + sub[1:]
                    elif isinstance(entry, list):
                        lines += [pad + "- - " + scalar(entry[0])] + [pad + "  - " + scalar(e) for e in entry[1:]]
                    else:
                        lines.append(pad + "- " + scalar(entry))
        else:
            lines.append(pad + key + ": " + scalar(item))
    return "\n".join(lines)


COMMON = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(), m_PrefabInstance=ref(), m_PrefabAsset=ref())
UI = {}
for name in ("Text", "Image", "Button", "CanvasScaler", "GraphicRaycaster", "EventSystem", "StandaloneInputModule"):
    path = next(UGUI.rglob(name + ".cs.meta"))
    UI[name] = re.search(r"guid: ([a-f0-9]{32})", path.read_text(encoding="utf-8-sig"))[1]


class File:
    def __init__(self):
        self.docs = []
        self.next_id = 100000

    def add(self, cls, name, fields):
        self.next_id += 1
        self.docs.append((cls, self.next_id, name, fields))
        return self.next_id

    def go(self, name, parent=None, position=(0, 0, 0), scale=(1, 1, 1), layer=0, tag="Untagged", rect=False, active=True):
        obj = dict(COMMON, serializedVersion=6, m_Component=[], m_Layer=layer, m_Name=name,
                   m_TagString=tag, m_Icon=ref(), m_NavMeshLayer=0, m_StaticEditorFlags=0, m_IsActive=int(active))
        oid = self.add(1, "GameObject", obj)
        tf = dict(COMMON, m_GameObject=ref(oid), m_LocalRotation=vec(0, 0, 0, 1), m_LocalPosition=vec(*position),
                  m_LocalScale=vec(*scale), m_ConstrainProportionsScale=0, m_Children=[],
                  m_Father=ref(parent["tid"] if parent else 0), m_RootOrder=0, m_LocalEulerAnglesHint=vec(0, 0, 0))
        if rect:
            tf.update(m_AnchorMin=vec(.5, .5), m_AnchorMax=vec(.5, .5), m_AnchoredPosition=vec(0, 0), m_SizeDelta=vec(100, 100), m_Pivot=vec(.5, .5))
        tid = self.add(224 if rect else 4, "RectTransform" if rect else "Transform", tf)
        obj["m_Component"].append({"component": ref(tid)})
        if parent:
            parent["tf"]["m_Children"].append(ref(tid))
        return dict(id=oid, tid=tid, tf=tf, go=obj)

    def component(self, obj, cls, name, fields, enabled=True):
        body = dict(COMMON, m_GameObject=ref(obj["id"]))
        if cls != 222:
            body["m_Enabled"] = int(enabled)
        body.update(fields)
        cid = self.add(cls, name, body)
        obj["go"]["m_Component"].append({"component": ref(cid)})
        return cid

    def script(self, obj, name, fields):
        guid = UI[name] if name in UI else metadata(ROOT / ("Assets/Scripts/" + name + ".cs"))
        return self.component(obj, 114, "MonoBehaviour", dict(m_EditorHideFlags=0, m_Script=ref(11500000, guid), m_Name="", m_EditorClassIdentifier="", **fields))

    def sprite(self, obj, name, order=1, tint=(1, 1, 1, 1), enabled=True, background=False):
        path = "Assets/Resources/" + name + ".png" if background else "Assets/Sprites/" + name + ".png"
        return self.component(obj, 212, "SpriteRenderer", dict(m_CastShadows=0, m_ReceiveShadows=0,
            m_DynamicOccludee=1, m_StaticShadowCaster=0, m_MotionVectors=1, m_LightProbeUsage=1,
            m_ReflectionProbeUsage=1, m_RayTracingMode=0, m_RayTraceProcedural=0, m_RenderingLayerMask=1,
            m_RendererPriority=0, m_Materials=[ref(10754, "0000000000000000f000000000000000", 0)],
            m_SortingLayerID=0, m_SortingLayer=0, m_SortingOrder=order, m_Sprite=asset(path, 21300000),
            m_Color=color(*tint), m_FlipX=0, m_FlipY=0, m_DrawMode=0, m_Size=vec(1.28, .64),
            m_AdaptiveModeThreshold=.5, m_SpriteTileMode=0, m_WasSpriteAssigned=1, m_MaskInteraction=0,
            m_SpriteSortPoint=0), enabled)

    def collider(self, obj, shape="Box", size=(1.17, .57), trigger=False, material=None, radius=.62):
        fields = dict(m_Density=1, m_Material=material or ref(), m_IsTrigger=int(trigger),
            m_UsedByEffector=0, m_UsedByComposite=0, m_Offset=vec(0, 0))
        cls = {"Box": 61, "Circle": 58, "Polygon": 60}[shape]
        if shape == "Box":
            fields.update(serializedVersion=2, m_Size=vec(*size), m_EdgeRadius=0, m_AutoTiling=0)
        elif shape == "Circle":
            fields.update(serializedVersion=2, m_Radius=radius)
        else:
            fields["m_Points"] = {"m_Paths": [[vec(math.cos(i * math.tau / 32) * .62, math.sin(i * math.tau / 32) * .62) for i in range(32)]]}
        return self.component(obj, cls, shape + "Collider2D", fields)

    def body(self, obj, kinematic=False):
        return self.component(obj, 50, "Rigidbody2D", dict(serializedVersion=4, m_BodyType=int(kinematic),
            m_Simulated=1, m_UseFullKinematicContacts=0, m_UseAutoMass=0, m_Mass=1,
            m_LinearDrag=0, m_AngularDrag=.05, m_GravityScale=0, m_Material=ref(),
            m_Interpolate=1, m_SleepingMode=1, m_CollisionDetection=1, m_Constraints=4))

    def canvas(self, obj, world=False):
        return self.component(obj, 223, "Canvas", dict(serializedVersion=3, m_RenderMode=2 if world else 0,
            m_Camera=ref(), m_PlaneDistance=100, m_PixelPerfect=0, m_ReceivesEvents=1,
            m_OverrideSorting=int(world), m_OverridePixelPerfect=0, m_SortingBucketNormalizedSize=0,
            m_AdditionalShaderChannelsFlag=25, m_SortingLayerID=0, m_SortingOrder=7 if world else 0, m_TargetDisplay=0))

    def text(self, parent, name, text, size=18, tint=(1, 1, 1, 1), align=4):
        obj = self.go(name, parent, layer=5, rect=True)
        self.component(obj, 222, "CanvasRenderer", dict(m_CullTransparentMesh=1))
        cid = self.script(obj, "Text", dict(m_Material=ref(), m_Color=color(*tint), m_RaycastTarget=0,
            m_RaycastPadding=vec(0, 0, 0, 0), m_Maskable=1,
            m_OnCullStateChanged={"m_PersistentCalls": {"m_Calls": []}},
            m_FontData=dict(m_Font=ref(12800000, "0000000000000000e000000000000000", 0),
                m_FontSize=size, m_FontStyle=0, m_BestFit=0, m_MinSize=10, m_MaxSize=40, m_Alignment=align,
                m_AlignByGeometry=0, m_RichText=1, m_HorizontalOverflow=1, m_VerticalOverflow=1, m_LineSpacing=1), m_Text=text))
        return obj, cid

    def panel(self, parent, name, tint):
        obj = self.go(name, parent, layer=5, rect=True)
        self.component(obj, 222, "CanvasRenderer", dict(m_CullTransparentMesh=1))
        cid = self.script(obj, "Image", dict(m_Material=ref(), m_Color=color(*tint), m_RaycastTarget=1,
            m_RaycastPadding=vec(0, 0, 0, 0), m_Maskable=1,
            m_OnCullStateChanged={"m_PersistentCalls": {"m_Calls": []}},
            m_Sprite=ref(), m_Type=0, m_PreserveAspect=0, m_FillCenter=1, m_FillMethod=4,
            m_FillAmount=1, m_FillClockwise=1, m_FillOrigin=0, m_UseSpriteMesh=0, m_PixelsPerUnitMultiplier=1))
        return obj, cid

    def write(self, path, prefix=None):
        content = prefix or "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
        for cls, cid, name, fields in self.docs:
            content += "--- !u!" + str(cls) + " &" + str(cid) + "\n" + name + ":\n" + dump(fields, 2) + "\n"
        path = ROOT / path
        path.write_text(content, encoding="utf-8")
        metadata(path)


def place(obj, anchor=(.5, .5), position=(0, 0), size=(100, 100), pivot=(.5, .5), maximum=None):
    obj["tf"].update(m_AnchorMin=vec(*anchor), m_AnchorMax=vec(*(maximum or anchor)),
                     m_AnchoredPosition=vec(*position), m_SizeDelta=vec(*size), m_Pivot=vec(*pivot))


def label(f, parent, size, bonus=False):
    canvas = f.go("Label Canvas", parent, scale=(.01, .02 if bonus else .01, 1), layer=5, rect=True)
    f.canvas(canvas, True)
    text, cid = f.text(canvas, "Bonus Text" if bonus else "Hits", "" if bonus else "4", size)
    place(text, size=(120, 64))
    return cid


def bootstrap_settings():
    archive = EDITOR / "Resources/PackageManager/ProjectTemplates/com.unity.template.2d-7.0.4.tgz"
    with tarfile.open(archive) as t:
        for member in t.getmembers():
            if "/ProjectSettings/" in member.name and member.isfile():
                path = ROOT / "ProjectSettings" / Path(member.name).name
                if not path.exists():
                    path.write_bytes(t.extractfile(member).read())
        template = t.extractfile("package/ProjectData~/Assets/Scenes/SampleScene.unity").read().decode()
    path = ROOT / "ProjectSettings/ProjectSettings.asset"
    if path.exists():
        text = path.read_text(encoding="utf-8-sig")
        for key, value in dict(companyName="ArcanoidStudy", productName="Arcanoid 2D", defaultScreenWidth=1024,
                               defaultScreenHeight=768, resizableWindow=0, runInBackground=1, fullscreenMode=3).items():
            text = re.sub(r"(?m)^  " + key + r":.*$", "  " + key + ": " + str(value), text)
        path.write_text(text, encoding="utf-8")
    path = ROOT / "ProjectSettings/EditorSettings.asset"
    if path.exists():
        path.write_text(re.sub(r"(?m)^  m_DefaultBehaviorMode:.*$", "  m_DefaultBehaviorMode: 1", path.read_text(encoding="utf-8-sig")), encoding="utf-8")
    return template.split("--- !u!1 &")[0]


def main():
    for path in (ROOT / "Assets").rglob("*"):
        if path.suffix != ".meta":
            metadata(path)
    prefix = bootstrap_settings()
    mat = File()
    mat.docs.append((62, 6200000, "PhysicsMaterial2D", dict(COMMON, m_Name="BallMaterial", friction=0, bounciness=1)))
    mat.write("Assets/Physics/BallMaterial.physicsMaterial2D")
    data = File()
    fields = dict(COMMON, m_GameObject=ref(), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=asset("Assets/Scripts/GameDataScript.cs", 11500000), m_Name="GameData", m_EditorClassIdentifier="",
        resetOnStart=1, level=1, balls=6, points=0, pointsToBall=0, music=1, sound=1, bonusesEnabled=1,
        bonusWeights=[dict(kind=i, weight=w) for i, w in enumerate([4, 2, 1, 2, 2, .5, 2, 1, 1])])
    data.docs.append((114, 11400000, "MonoBehaviour", fields))
    data.write("Assets/Data/GameData.asset")
    prefabs = {}
    for name, sprite, hits, points, optional, bonus in [
        ("Blue Block", "cube_blue", 30, 100, True, False), ("Red Block", "cube_red", 4, 40, False, False),
        ("Green Block", "cube_green", 2, 20, False, True), ("Yellow Block", "cube_yellow", 1, 10, False, False)]:
        f = File()
        obj = f.go(name, layer=11, tag="Wall" if optional else "Block")
        f.sprite(obj, sprite); f.collider(obj)
        label_id = label(f, obj, 34) if hits in (2, 4) else 0
        if hits == 2:
            next(d[3] for d in f.docs if d[1] == label_id)["m_Text"] = "2"
        f.script(obj, "BlockScript", dict(hitsToDestroy=hits, points=points, optional=int(optional),
                                         dropsBonus=int(bonus), textComponent=ref(label_id)))
        path = "Assets/Prefabs/" + name + ".prefab"; f.write(path); prefabs[name] = asset(path, obj["id"])

    f = File(); obj = f.go("Ball", scale=(.22, .22, .22), layer=10, tag="Ball")
    f.sprite(obj, "ball", 4); f.collider(obj, "Circle", material=asset("Assets/Physics/BallMaterial.physicsMaterial2D", 6200000)); f.body(obj, True)
    f.script(obj, "BallScript", dict(ballInitialForce=vec(100, 300), gameData=asset("Assets/Data/GameData.asset", 11400000),
        hitSound=asset("Assets/Audio/hit.mp3", 8300000), loseSound=asset("Assets/Audio/lose.mp3", 8300000)))
    f.write("Assets/Prefabs/Ball.prefab"); prefabs["Ball"] = asset("Assets/Prefabs/Ball.prefab", obj["id"])
    f = File(); obj = f.go("Bonus", scale=(1, .5, 1), layer=13)
    f.sprite(obj, "ball", 5); f.collider(obj, size=(1.12, 1.12), trigger=True); f.body(obj)
    f.script(obj, "BonusDrop", dict(fallSpeed=2, label=ref(label(f, obj, 32, True))))
    f.write("Assets/Prefabs/Bonus.prefab"); prefabs["Bonus"] = asset("Assets/Prefabs/Bonus.prefab", obj["id"])

    f = File(); camera = f.go("Main Camera", position=(0, 0, -10), tag="MainCamera")
    f.component(camera, 20, "Camera", dict(serializedVersion=2, m_ClearFlags=2,
        m_BackGroundColor=color(.045, .075, .12, 1), m_projectionMatrixMode=1, m_GateFitMode=2,
        m_FOVAxisMode=0, m_SensorSize=vec(36, 24), m_LensShift=vec(0, 0), m_FocalLength=50,
        m_NormalizedViewPortRect=dict(serializedVersion=2, x=0, y=0, width=1, height=1),
        **{"near clip plane": .3, "far clip plane": 1000, "field of view": 60, "orthographic": 1, "orthographic size": 5},
        m_Depth=-1, m_CullingMask=dict(serializedVersion=2, m_Bits=4294967295), m_RenderingPath=-1,
        m_TargetTexture=ref(), m_TargetDisplay=0, m_TargetEye=0, m_HDR=0, m_AllowMSAA=0,
        m_AllowDynamicResolution=0, m_ForceIntoRT=0, m_OcclusionCulling=0))
    f.component(camera, 81, "AudioListener", {})
    audio = []
    for music in (True, False):
        audio.append(f.component(camera, 82, "AudioSource", dict(serializedVersion=4,
            OutputAudioMixerGroup=ref(), m_audioClip=asset("Assets/Audio/Ricochet Xtreme.mp3", 8300000) if music else ref(),
            m_PlayOnAwake=0, m_Volume=.2 if music else .8, m_Pitch=1, Loop=int(music), Mute=0,
            Spatialize=0, SpatializePostEffects=0, Priority=128, DopplerLevel=0,
            MinDistance=1, MaxDistance=500, Pan2D=0, rolloffMode=0, BypassEffects=0,
            BypassListenerEffects=0, BypassReverbZones=0)))
    bg = f.go("Background", scale=(1.3020833, 1.3020833, 1)); bg_renderer = f.sprite(bg, "01", -10, background=True)
    walls = f.go("Walls")
    for name, pos, size, bottom in [("Left Wall", (-6.56, 0, 0), (.22, 10), False),
        ("Right Wall", (6.56, 0, 0), (.22, 10), False), ("Top Wall", (0, 4.5, 0), (13.33, 1), False),
        ("Bottom Wall", (0, -5.7, 0), (16, .35), True)]:
        wall = f.go(name, walls, pos, (size[0] / 1.28, size[1] / .64, 1), 12 if bottom else 8, "Wall")
        f.sprite(wall, "cube_blue", 2, (.4, .8, .9, 1), not bottom)
        f.collider(wall, size=(1.28, .64), trigger=bottom)
    paddle = f.go("Player", position=(0, -4.85, 0), scale=(1.5, 1.5, 1), layer=9, tag="Player")
    f.sprite(paddle, "ball", 3); f.body(paddle, True); f.collider(paddle, "Circle")
    ui_fields = {}
    root = f.go("Game UI", rect=True, layer=5); f.canvas(root)
    f.script(root, "CanvasScaler", dict(m_UiScaleMode=1, m_ReferencePixelsPerUnit=100, m_ScaleFactor=1,
        m_ReferenceResolution=vec(1024, 768), m_ScreenMatchMode=0, m_MatchWidthOrHeight=.5,
        m_PhysicalUnit=3, m_FallbackScreenDPI=96, m_DefaultSpriteDPI=96, m_DynamicPixelsPerUnit=1, m_PresetInfoIsWorld=0))
    f.script(root, "GraphicRaycaster", dict(m_IgnoreReversedGraphics=1, m_BlockingObjects=0,
                                         m_BlockingMask=dict(serializedVersion=2, m_Bits=4294967295)))
    ink = (.045, .075, .12, .94); cyan = (.25, .95, .92, 1); muted = (.58, .72, .80, 1)
    header, _ = f.panel(root, "Header", ink)
    place(header, (0, 1), (0, -36), (0, 72), maximum=(1, 1))
    for field, name, value, size, tint, alignment, pos, dimensions, pivot in [
        (None, "Title", "ARCANOID", 23, cyan, 3, (30, 47), (220, 32), (0, .5)),
        (None, "Subtitle", "2D  /  UNITY", 11, muted, 3, (30, 22), (220, 18), (0, .5)),
        ("levelText", "Level", "", 18, (1, 1, 1, 1), 4, (420, 38), (240, 40), (.5, .5)),
        ("ballsText", "Balls", "", 18, (1, 1, 1, 1), 4, (656, 38), (200, 40), (.5, .5)),
        ("scoreText", "Score", "", 18, cyan, 5, (990, 38), (220, 40), (1, .5))]:
        text, cid = f.text(header, name, value, size, tint, alignment); place(text, (0, 0), pos, dimensions, pivot)
        if field: ui_fields[field] = ref(cid)
    footer, _ = f.panel(root, "Controls", ink); place(footer, (0, 0), (0, 26), (0, 52), maximum=(1, 0))
    keys = "МЫШЬ — ракетка    ЛКМ / CTRL — запуск    ПРОБЕЛ / ПКМ — пауза    J — толчок    N — новая    ESC — выход"
    text, _ = f.text(footer, "Keys", keys, 13, align=3); place(text, (0, 0), (26, 35), (970, 22), (0, .5))
    text, cid = f.text(footer, "Audio", "", 12, muted, 3); place(text, (0, 0), (26, 14), (550, 20), (0, .5)); ui_fields["audioText"] = ref(cid)
    text, cid = f.text(footer, "Extra ball progress", "", 12, muted, 5); place(text, (0, 0), (994, 14), (360, 20), (1, .5)); ui_fields["progressText"] = ref(cid)
    text, cid = f.text(root, "Notification", "", 17, cyan); place(text, (.5, 1), (0, -105), (800, 36)); ui_fields["noticeText"] = ref(cid)
    text, cid = f.text(root, "Launch hint", "ЛКМ / CTRL   ·   ЗАПУСТИТЬ ШАРИКИ", 17); place(text, (.5, 0), (0, 135), (650, 38)); ui_fields["launchText"] = ref(cid)
    shade, _ = f.panel(root, "Pause overlay", (.015, .025, .05, .86)); place(shade, (0, 0), size=(0, 0), maximum=(1, 1))
    shade["go"]["m_IsActive"] = 0; ui_fields["pausePanel"] = ref(shade["id"])
    card, _ = f.panel(shade, "Pause card", ink); place(card, size=(450, 410))
    text, _ = f.text(card, "Pause title", "ПАУЗА", 36, cyan); place(text, position=(0, 130), size=(420, 60))
    text, _ = f.text(card, "Pause hint", "Пробел или ПКМ — продолжить\nM — музыка  ·  S — звуковые эффекты", 15, muted); place(text, position=(0, 68), size=(420, 55))
    for field, title, y, tint, text_tint in [("resumeButton", "Продолжить", 0, cyan, ink),
        ("newGameButton", "Новая игра", -64, (.15, .25, .32, 1), (1, 1, 1, 1)),
        ("quitButton", "Сохранить и выйти", -128, (.15, .25, .32, 1), (1, 1, 1, 1))]:
        button, image = f.panel(card, title, tint); place(button, position=(0, y), size=(350, 50))
        cid = f.script(button, "Button", dict(m_Navigation=dict(m_Mode=3, m_WrapAround=0, m_SelectOnUp=ref(), m_SelectOnDown=ref(), m_SelectOnLeft=ref(), m_SelectOnRight=ref()),
            m_Transition=1, m_Colors=dict(m_NormalColor=color(1, 1, 1, 1), m_HighlightedColor=color(.88, .95, 1, 1),
            m_PressedColor=color(.6, .8, .9, 1), m_SelectedColor=color(.88, .95, 1, 1),
            m_DisabledColor=color(.5, .5, .5, .5), m_ColorMultiplier=1, m_FadeDuration=.1),
            m_Interactable=1, m_TargetGraphic=ref(image), m_OnClick={"m_PersistentCalls": {"m_Calls": []}}))
        ui_fields[field] = ref(cid)
        text, _ = f.text(button, "Label", title, 18, text_tint); place(text, (0, 0), size=(0, 0), maximum=(1, 1))
    ui = f.script(root, "GameUiScript", ui_fields)
    f.script(paddle, "PlayerScript", dict(gameData=asset("Assets/Data/GameData.asset", 11400000), ballVelocityMult=.02,
        bluePrefab=prefabs["Blue Block"], redPrefab=prefabs["Red Block"], greenPrefab=prefabs["Green Block"],
        yellowPrefab=prefabs["Yellow Block"], ballPrefab=prefabs["Ball"], bonusPrefab=prefabs["Bonus"],
        background=ref(bg_renderer), musicSource=ref(audio[0]), effectsSource=ref(audio[1]),
        pointSound=asset("Assets/Audio/point.mp3", 8300000), gameUi=ref(ui)))
    event = f.go("EventSystem")
    f.script(event, "EventSystem", dict(m_FirstSelected=ref(), m_sendNavigationEvents=1, m_DragThreshold=10))
    f.script(event, "StandaloneInputModule", dict(m_HorizontalAxis="Horizontal", m_VerticalAxis="Vertical", m_SubmitButton="Submit", m_CancelButton="Cancel", m_InputActionsPerSecond=10, m_RepeatDelay=.5, m_ForceModuleActive=0))
    f.write("Assets/Scenes/MainScene.unity", prefix)
    settings = File(); settings.docs.append((1045, 1, "EditorBuildSettings", dict(m_ObjectHideFlags=0, serializedVersion=2,
        m_Scenes=[dict(enabled=1, path="Assets/Scenes/MainScene.unity", guid=metadata(ROOT / "Assets/Scenes/MainScene.unity"))], m_configObjects={})))
    settings.write("ProjectSettings/EditorBuildSettings.asset")
    tags = ["Untagged", "TransparentFX", "Ignore Raycast", "", "Water", "UI", "", "", "Wall", "Player", "Ball", "Block", "Bottom", "Bonus"] + [""] * 18
    settings = File(); settings.docs.append((78, 1, "TagManager", dict(serializedVersion=2,
        tags=["Wall", "Ball", "Block"], layers=tags, m_SortingLayers=[dict(name="Default", uniqueID=0, locked=0)], m_RenderingLayers=["Default"])))
    settings.write("ProjectSettings/TagManager.asset")
    axes = []
    for name, positive, alt, negative, alt_neg in [("Fire1", "left ctrl", "mouse 0", "", ""), ("Pause", "space", "mouse 1", "", ""),
        ("Horizontal", "right", "d", "left", "a"), ("Vertical", "up", "", "down", ""), ("Submit", "return", "", "", ""), ("Cancel", "escape", "", "", "")]:
        axes.append(dict(serializedVersion=3, m_Name=name, descriptiveName="", descriptiveNegativeName="", negativeButton=negative,
            positiveButton=positive, altNegativeButton=alt_neg, altPositiveButton=alt, gravity=3, dead=.001, sensitivity=3, snap=1, invert=0, type=0, axis=0, joyNum=0))
    settings = File(); settings.docs.append((13, 1, "InputManager", dict(m_ObjectHideFlags=0, serializedVersion=2, m_Axes=axes)))
    settings.write("ProjectSettings/InputManager.asset")
    # Preserve the template's physical defaults but persist the same interaction matrix used by PlayerScript.Awake.
    path = ROOT / "ProjectSettings/Physics2DSettings.asset"
    if path.exists():
        masks = [(1 << 32) - 1] * 32
        pairs = [(10, 8), (10, 9), (10, 11), (10, 12), (13, 9), (13, 12)]
        for i in range(8, 14):
            for j in range(8, 14):
                if (i, j) not in pairs and (j, i) not in pairs:
                    masks[i] &= ~(1 << j)
        masks[13] = (1 << 9) | (1 << 12)
        for i in range(32):
            if i not in (9, 12):
                masks[i] &= ~(1 << 13)
        matrix = b"".join(n.to_bytes(4, "little") for n in masks).hex()
        path.write_text(re.sub(r"(?m)^  m_LayerCollisionMatrix:.*$", "  m_LayerCollisionMatrix: " + matrix, path.read_text(encoding="utf-8-sig")), encoding="utf-8")
    for path in (ROOT / "ProjectSettings").glob("*.meta"):
        path.unlink()  # ProjectSettings are settings files, not importable Assets.
    print("Wrote native MainScene, six prefabs, GameData, ball material and project settings.")


if __name__ == "__main__":
    main()
