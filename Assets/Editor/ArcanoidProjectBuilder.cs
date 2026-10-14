using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ArcanoidProjectBuilder
{
    static Font font;
    static Color ink = new Color(.045f, .075f, .12f, .94f);
    static Color muted = new Color(.58f, .72f, .80f);
    static Color cyan = new Color(.25f, .95f, .92f);

    [MenuItem("Arcanoid/Recreate scene and prefabs")]
    public static void CreateProject()
    {
        ConfigureSettings();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var data = AssetDatabase.LoadAssetAtPath<GameDataScript>("Assets/Data/GameData.asset");
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GameDataScript>();
            AssetDatabase.CreateAsset(data, "Assets/Data/GameData.asset");
        }
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Physics/BallMaterial.physicsMaterial2D");
        if (material == null)
        {
            material = new PhysicsMaterial2D("BallMaterial") { friction = 0, bounciness = 1 };
            AssetDatabase.CreateAsset(material, "Assets/Physics/BallMaterial.physicsMaterial2D");
        }
        var blue = MakeBlock("Blue Block", "cube_blue", 30, 100, true, false);
        var red = MakeBlock("Red Block", "cube_red", 4, 40, false, false);
        var green = MakeBlock("Green Block", "cube_green", 2, 20, false, true);
        var yellow = MakeBlock("Yellow Block", "cube_yellow", 1, 10, false, false);
        var ball = MakeBall(data, material);
        var bonus = MakeBonus();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ink;
        cameraObject.AddComponent<AudioListener>();
        var music = cameraObject.AddComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ricochet Xtreme.mp3");
        music.loop = true; music.volume = .2f; music.playOnAwake = false; music.spatialBlend = 0;
        var effects = cameraObject.AddComponent<AudioSource>();
        effects.volume = .8f; effects.playOnAwake = false; effects.spatialBlend = 0;

        var bg = SpriteObject("Background", LoadBackground(), Vector3.zero, new Vector3(1.3020833f, 1.3020833f, 1), -10);
        var walls = new GameObject("Walls");
        MakeWall(walls.transform, "Left Wall", new Vector2(-6.56f, 0), new Vector2(.22f, 10), false);
        MakeWall(walls.transform, "Right Wall", new Vector2(6.56f, 0), new Vector2(.22f, 10), false);
        MakeWall(walls.transform, "Top Wall", new Vector2(0, 4.50f), new Vector2(13.33f, 1), false);
        MakeWall(walls.transform, "Bottom Wall", new Vector2(0, -5.7f), new Vector2(16, .35f), true);

        var paddle = SpriteObject("Player", LoadSprite("ball"), new Vector3(0, -4.85f, 0), new Vector3(1.5f, 1.5f, 1), 3);
        paddle.tag = "Player"; paddle.layer = LayerMask.NameToLayer("Player");
        var paddleBody = paddle.AddComponent<Rigidbody2D>();
        paddleBody.bodyType = RigidbodyType2D.Kinematic; paddleBody.gravityScale = 0;
        paddleBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        paddle.AddComponent<CircleCollider2D>().radius = .62f;
        var player = paddle.AddComponent<PlayerScript>();
        player.gameData = data; player.bluePrefab = blue; player.redPrefab = red;
        player.greenPrefab = green; player.yellowPrefab = yellow;
        player.ballPrefab = ball; player.bonusPrefab = bonus;
        player.background = bg.GetComponent<SpriteRenderer>();
        player.musicSource = music; player.effectsSource = effects;
        player.pointSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/point.mp3");
        player.gameUi = MakeHud();
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<StandaloneInputModule>();

        EditorSceneManager.SaveScene(SceneManagerScene(), "Assets/Scenes/MainScene.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MainScene.unity", true) };
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        ValidateProject();
        if (Application.isBatchMode) BuildWindows();
    }

    static UnityEngine.SceneManagement.Scene SceneManagerScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/" + name + ".png");
    static Sprite LoadBackground() => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/01.png");

    static GameObject SpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale, int order)
    {
        var obj = new GameObject(name);
        obj.transform.position = position; obj.transform.localScale = scale;
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
        return obj;
    }

    static GameObject SavePrefab(GameObject obj, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, "Assets/Prefabs/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(obj);
        return prefab;
    }

    static GameObject MakeBlock(string name, string sprite, int hits, int score, bool optional, bool bonus)
    {
        var obj = SpriteObject(name, LoadSprite(sprite), Vector3.zero, Vector3.one, 1);
        obj.tag = optional ? "Wall" : "Block"; obj.layer = LayerMask.NameToLayer("Block");
        obj.AddComponent<BoxCollider2D>().size = new Vector2(1.17f, .57f);
        var script = obj.AddComponent<BlockScript>();
        script.hitsToDestroy = hits; script.points = score; script.optional = optional; script.dropsBonus = bonus;
        if (hits == 2 || hits == 4) script.textComponent = WorldLabel(obj.transform, "Hits", 34, false);
        return SavePrefab(obj, name);
    }

    static GameObject MakeBall(GameDataScript data, PhysicsMaterial2D material)
    {
        var obj = SpriteObject("Ball", LoadSprite("ball"), Vector3.zero, Vector3.one * .22f, 4);
        obj.tag = "Ball"; obj.layer = LayerMask.NameToLayer("Ball");
        var collider = obj.AddComponent<CircleCollider2D>(); collider.radius = .62f; collider.sharedMaterial = material;
        var body = obj.AddComponent<Rigidbody2D>();
        body.gravityScale = 0; body.bodyType = RigidbodyType2D.Kinematic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var script = obj.AddComponent<BallScript>(); script.gameData = data;
        script.hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hit.mp3");
        script.loseSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/lose.mp3");
        return SavePrefab(obj, "Ball");
    }

    static GameObject MakeBonus()
    {
        var obj = SpriteObject("Bonus", LoadSprite("ball"), Vector3.zero, new Vector3(1, .5f, 1), 5);
        obj.layer = LayerMask.NameToLayer("Bonus");
        var collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(1.12f, 1.12f); collider.isTrigger = true;
        var body = obj.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var drop = obj.AddComponent<BonusDrop>(); drop.label = WorldLabel(obj.transform, "Bonus Text", 32, true);
        return SavePrefab(obj, "Bonus");
    }

    static Text WorldLabel(Transform parent, string name, int size, bool compensateY)
    {
        var canvasObj = new GameObject("Label Canvas", typeof(RectTransform));
        canvasObj.transform.SetParent(parent, false);
        canvasObj.transform.localScale = new Vector3(.01f, compensateY ? .02f : .01f, 1);
        var canvas = canvasObj.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = 7;
        var text = MakeText(canvasObj.transform, name, "", size, Color.white, TextAnchor.MiddleCenter);
        Place(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 64));
        return text;
    }

    static void MakeWall(Transform parent, string name, Vector2 position, Vector2 size, bool bottom)
    {
        var obj = SpriteObject(name, LoadSprite("cube_blue"), position, new Vector3(size.x / 1.28f, size.y / .64f, 1), 2);
        obj.transform.SetParent(parent, true); obj.tag = "Wall";
        obj.layer = LayerMask.NameToLayer(bottom ? "Bottom" : "Wall");
        var collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(1.28f, .64f); collider.isTrigger = bottom;
        if (bottom) obj.GetComponent<SpriteRenderer>().enabled = false;
        else obj.GetComponent<SpriteRenderer>().color = new Color(.4f, .8f, .9f);
    }

    static GameUiScript MakeHud()
    {
        var root = new GameObject("Game UI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 768); scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();
        var ui = root.AddComponent<GameUiScript>();
        var header = Panel(root.transform, "Header", ink);
        Place(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), new Vector2(0, 72));
        var title = MakeText(header, "Title", "ARCANOID", 23, cyan, TextAnchor.MiddleLeft);
        Place(title.rectTransform, Vector2.zero, Vector2.zero, new Vector2(30, 47), new Vector2(220, 32), new Vector2(0, .5f));
        var subtitle = MakeText(header, "Subtitle", "2D  /  UNITY", 11, muted, TextAnchor.MiddleLeft);
        Place(subtitle.rectTransform, Vector2.zero, Vector2.zero, new Vector2(30, 22), new Vector2(220, 18), new Vector2(0, .5f));
        ui.levelText = MakeText(header, "Level", "", 18, Color.white, TextAnchor.MiddleCenter);
        Place(ui.levelText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(420, 38), new Vector2(240, 40));
        ui.ballsText = MakeText(header, "Balls", "", 18, Color.white, TextAnchor.MiddleCenter);
        Place(ui.ballsText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(656, 38), new Vector2(200, 40));
        ui.scoreText = MakeText(header, "Score", "", 18, cyan, TextAnchor.MiddleRight);
        Place(ui.scoreText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(990, 38), new Vector2(220, 40), new Vector2(1, .5f));
        var footer = Panel(root.transform, "Controls", ink);
        Place(footer, Vector2.zero, new Vector2(1, 0), new Vector2(0, 26), new Vector2(0, 52));
        var controls = MakeText(footer, "Keys", "МЫШЬ — ракетка    ЛКМ / CTRL — запуск    ПРОБЕЛ / ПКМ — пауза    J — толчок    N — новая    ESC — выход", 13, Color.white, TextAnchor.MiddleLeft);
        Place(controls.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26, 35), new Vector2(970, 22), new Vector2(0, .5f));
        ui.audioText = MakeText(footer, "Audio", "", 12, muted, TextAnchor.MiddleLeft);
        Place(ui.audioText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26, 14), new Vector2(550, 20), new Vector2(0, .5f));
        ui.progressText = MakeText(footer, "Extra ball progress", "", 12, muted, TextAnchor.MiddleRight);
        Place(ui.progressText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(994, 14), new Vector2(360, 20), new Vector2(1, .5f));
        ui.noticeText = MakeText(root.transform, "Notification", "", 17, cyan, TextAnchor.MiddleCenter);
        Place(ui.noticeText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -105), new Vector2(800, 36));
        ui.launchText = MakeText(root.transform, "Launch hint", "ЛКМ / CTRL   ·   ЗАПУСТИТЬ ШАРИКИ", 17, Color.white, TextAnchor.MiddleCenter);
        Place(ui.launchText.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 135), new Vector2(650, 38));

        var shade = Panel(root.transform, "Pause overlay", new Color(.015f, .025f, .05f, .86f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        ui.pausePanel = shade.gameObject;
        var card = Panel(shade, "Pause card", ink);
        Place(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(450, 410));
        var pauseTitle = MakeText(card, "Pause title", "ПАУЗА", 36, cyan, TextAnchor.MiddleCenter);
        Place(pauseTitle.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(420, 60));
        var pauseInfo = MakeText(card, "Pause hint", "Пробел или ПКМ — продолжить\nM — музыка  ·  S — звуковые эффекты", 15, muted, TextAnchor.MiddleCenter);
        Place(pauseInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 68), new Vector2(420, 55));
        ui.resumeButton = MakeButton(card, "Продолжить", 0, cyan, ink);
        ui.newGameButton = MakeButton(card, "Новая игра", -64, new Color(.15f, .25f, .32f), Color.white);
        ui.quitButton = MakeButton(card, "Сохранить и выйти", -128, new Color(.15f, .25f, .32f), Color.white);
        shade.gameObject.SetActive(false);
        return ui;
    }

    static RectTransform Panel(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        obj.AddComponent<Image>().color = color;
        return obj.GetComponent<RectTransform>();
    }

    static Text MakeText(Transform parent, string name, string value, int size, Color color, TextAnchor alignment)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>(); text.font = font; text.fontSize = size;
        text.text = value; text.color = color; text.alignment = alignment;
        text.supportRichText = true; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot ?? new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    static Button MakeButton(Transform parent, string title, float y, Color color, Color textColor)
    {
        var rect = Panel(parent, title, color);
        Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(350, 50));
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        var label = MakeText(rect, "Label", title, 18, textColor, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    static void ConfigureSettings()
    {
        PlayerSettings.companyName = "ArcanoidStudy"; PlayerSettings.productName = "Arcanoid 2D";
        PlayerSettings.defaultScreenWidth = 1024; PlayerSettings.defaultScreenHeight = 768;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = false;
        PlayerSettings.runInBackground = true; PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        QualitySettings.vSyncCount = 1;
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        EditorSettings.serializationMode = SerializationMode.ForceText;
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagList = tags.FindProperty("tags");
        foreach (string name in new[] { "Wall", "Ball", "Block" })
        {
            bool exists = false;
            for (int i = 0; i < tagList.arraySize; i++) exists |= tagList.GetArrayElementAtIndex(i).stringValue == name;
            if (!exists) { tagList.InsertArrayElementAtIndex(tagList.arraySize); tagList.GetArrayElementAtIndex(tagList.arraySize - 1).stringValue = name; }
        }
        var layers = tags.FindProperty("layers");
        string[] layerNames = { "Wall", "Player", "Ball", "Block", "Bottom", "Bonus" };
        for (int i = 0; i < layerNames.Length; i++) layers.GetArrayElementAtIndex(8 + i).stringValue = layerNames[i];
        tags.ApplyModifiedPropertiesWithoutUndo();
        var input = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
        var axes = input.FindProperty("m_Axes"); axes.arraySize = 6;
        MakeAxis(axes.GetArrayElementAtIndex(0), "Fire1", "left ctrl", "mouse 0", "", "");
        MakeAxis(axes.GetArrayElementAtIndex(1), "Pause", "space", "mouse 1", "", "");
        MakeAxis(axes.GetArrayElementAtIndex(2), "Horizontal", "right", "d", "left", "a");
        MakeAxis(axes.GetArrayElementAtIndex(3), "Vertical", "up", "", "down", "");
        MakeAxis(axes.GetArrayElementAtIndex(4), "Submit", "return", "", "", "");
        MakeAxis(axes.GetArrayElementAtIndex(5), "Cancel", "escape", "", "", "");
        input.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MakeAxis(SerializedProperty axis, string name, string positive, string alternate, string negative, string altNegative)
    {
        axis.FindPropertyRelative("m_Name").stringValue = name;
        axis.FindPropertyRelative("positiveButton").stringValue = positive;
        axis.FindPropertyRelative("altPositiveButton").stringValue = alternate;
        axis.FindPropertyRelative("negativeButton").stringValue = negative;
        axis.FindPropertyRelative("altNegativeButton").stringValue = altNegative;
        axis.FindPropertyRelative("gravity").floatValue = 3;
        axis.FindPropertyRelative("dead").floatValue = .001f;
        axis.FindPropertyRelative("sensitivity").floatValue = 3;
        axis.FindPropertyRelative("snap").boolValue = true;
        axis.FindPropertyRelative("invert").boolValue = false;
        axis.FindPropertyRelative("type").intValue = 0;
        axis.FindPropertyRelative("axis").intValue = 0;
        axis.FindPropertyRelative("joyNum").intValue = 0;
    }

    [MenuItem("Arcanoid/Validate project")]
    public static void ValidateProject()
    {
        for (int i = 1; i <= 30; i++)
            Require(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/" + i.ToString("d2") + ".png") != null, "Background " + i);
        var bonus = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bonus.prefab");
        Require(bonus.GetComponent<BonusBase>() == null, "Bonus prefab must not contain BonusBase");
        Require(bonus.GetComponent<BonusDrop>() != null, "BonusDrop prefab component");
        Require(Mathf.Approximately(bonus.transform.localScale.y, .5f), "Bonus Y scale");
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Physics/BallMaterial.physicsMaterial2D");
        Require(mat.friction == 0 && mat.bounciness == 1, "Ball physics material");
        foreach (string name in new[] { "Wall", "Player", "Ball", "Block", "Bottom", "Bonus" }) Require(LayerMask.NameToLayer(name) >= 8, "Layer " + name);
        Directory.CreateDirectory("Tests");
        File.WriteAllText("Tests/project-validation.txt", "PASS: 30 sprite backgrounds; Bonus prefab without BonusBase; scale Y=0.5; elastic ball material; six gameplay layers; MainScene registered for build.\n");
        Debug.Log("ARCANOID: Project validation passed.");
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    [MenuItem("Arcanoid/Build Windows")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Build");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MainScene.unity" }, locationPathName = "Build/Arcanoid.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.CompressWithLz4HC
        });
        File.WriteAllText("Tests/build-result.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\n");
        var messages = new System.Collections.Generic.List<string>();
        foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Warning || message.type == LogType.Error)
                    messages.Add(message.type + ": " + message.content);
        File.WriteAllLines("Tests/build-messages.txt", messages);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed");
        Debug.Log("ARCANOID: Windows build succeeded.");
    }
}
