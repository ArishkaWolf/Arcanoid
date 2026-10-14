using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in player integration check: -arcanoidSmoke. It uses an isolated GameData clone and save prefix.
public class ArcanoidSmokeCheck : MonoBehaviour
{
    [Serializable] class Report { public List<string> passed = new List<string>(); public List<string> failed = new List<string>(); }
    readonly Report report = new Report();
    string directory;
    PlayerScript Player => FindObjectOfType<PlayerScript>();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        directory = Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tests")
            : Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tests"));
        Directory.CreateDirectory(directory);
        Application.logMessageReceived += LogMessage;
    }

    void LogMessage(string text, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.failed.Add("Runtime error: " + text);
    }

    void Check(bool condition, string description)
    {
        (condition ? report.passed : report.failed).Add(description);
        Debug.Log("ARCANOID QA " + (condition ? "PASS: " : "FAIL: ") + description);
    }

    void CaptureFrame(string name)
    {
        // Explicit camera render also works when the test player's window is hidden.
        var camera = Camera.main;
        var canvas = Player.gameUi.GetComponent<Canvas>();
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        int oldOrder = canvas.sortingOrder;
        float oldDistance = canvas.planeDistance;
        var target = new RenderTexture(1024, 768, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.sortingOrder = 100;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
        camera.targetTexture = oldTarget;
        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCamera;
        canvas.planeDistance = oldDistance;
        canvas.sortingOrder = oldOrder;
        RenderTexture.active = oldActive;
        target.Release();
        Destroy(target); Destroy(pixels);
        Canvas.ForceUpdateCanvases();
    }

    IEnumerator Start()
    {
        yield return null; yield return new WaitForSeconds(.25f);
        var player = Player;
        Check(player.ActiveBalls.Count == 2 && player.gameData.balls == 6, "Two docked start balls; total reserve six");
        Check(player.RemainingBlocks == 7 && FindObjectsOfType<BlockScript>().Length == 8, "Level 1 block counts and optional blue block");
        Check(player.background.sprite.name == "01", "Level 1 supplied background");
        Check(Physics2D.GetIgnoreLayerCollision(10, 10), "Balls do not collide with each other");
        Check(Physics2D.GetIgnoreLayerCollision(13, 10) && Physics2D.GetIgnoreLayerCollision(13, 11) &&
            Physics2D.GetIgnoreLayerCollision(13, 8) && !Physics2D.GetIgnoreLayerCollision(13, 9) &&
            !Physics2D.GetIgnoreLayerCollision(13, 12), "Bonus collision matrix: only paddle and bottom");
        var paddleCircle = player.GetComponent<CircleCollider2D>();
        var paddleSprite = player.GetComponent<SpriteRenderer>();
        var leftWall = GameObject.Find("Left Wall").GetComponent<Collider2D>();
        var rightWall = GameObject.Find("Right Wall").GetComponent<Collider2D>();
        Check(paddleCircle != null && player.GetComponent<PolygonCollider2D>() == null &&
            Mathf.Approximately(player.transform.lossyScale.x, player.transform.lossyScale.y),
            "Paddle is a uniform circle with a CircleCollider2D");
        player.SetTargetX(-1000);
        yield return new WaitForSeconds(.25f);
        bool leftContained = paddleSprite.bounds.min.x >= leftWall.bounds.max.x &&
            paddleCircle.bounds.min.x >= leftWall.bounds.max.x;
        foreach (var item in player.ActiveBalls)
            leftContained &= item.GetComponent<Collider2D>().bounds.min.x >= leftWall.bounds.max.x;
        Check(leftContained, "Left movement limit keeps the entire paddle and docked balls inside the wall");
        player.SetTargetX(1000);
        yield return new WaitForSeconds(.25f);
        bool rightContained = paddleSprite.bounds.max.x <= rightWall.bounds.min.x &&
            paddleCircle.bounds.max.x <= rightWall.bounds.min.x;
        foreach (var item in player.ActiveBalls)
            rightContained &= item.GetComponent<Collider2D>().bounds.max.x <= rightWall.bounds.min.x;
        Check(rightContained, "Right movement limit keeps the entire paddle and docked balls inside the wall");
        CaptureFrame("paddle-right.png");
        player.SetTargetX(0);
        yield return new WaitForSeconds(.25f);
        bool clearSurface = true;
        foreach (var item in player.ActiveBalls)
            clearSurface &= !Physics2D.Distance(paddleCircle, item.GetComponent<Collider2D>()).isOverlapped;
        Check(clearSurface, "Start balls sit above the circular surface without overlapping the paddle");
        yield return new WaitForEndOfFrame();
        CaptureFrame("gameplay.png");
        yield return new WaitForSeconds(.1f);

        var ball = player.ActiveBalls[0];
        ball.Launch();
        Check(!ball.IsDocked && ball.Body.velocity.y > 0, "Launch switches ball to dynamic upward movement");
        ball.Body.position = new Vector2(6.05f, -1);
        ball.Body.velocity = Vector2.right * ball.Speed;
        yield return new WaitForSeconds(.13f);
        Check(ball.Body.velocity.x < 0, "Real physics: ball reflects from right wall");
        ball.Body.position = new Vector2(player.transform.position.x + .3f, -3.1f);
        ball.Body.velocity = Vector2.down * ball.Speed;
        yield return new WaitForSeconds(.2f);
        Check(ball.Body.velocity.y > 0, "Real physics: ball reflects from circular paddle");
        var red = Instantiate(player.redPrefab, new Vector3(0, -1, 0), Quaternion.identity).GetComponent<BlockScript>();
        red.optional = true; red.Initialize(player);
        ball.Body.position = new Vector2(0, -2.2f);
        ball.Body.velocity = Vector2.up * ball.Speed;
        yield return new WaitForSeconds(.18f);
        Check(red.hitsToDestroy == 3 && red.textComponent.text == "3", "Real physics: normal ball removes one red hit and updates the label");
        int collisionScore = player.gameData.points;
        ball.SetMaterial(4, new Color(1, .5f, 0));
        ball.Body.position = new Vector2(0, -2.2f);
        ball.Body.velocity = Vector2.up * ball.Speed;
        yield return new WaitForSeconds(.18f);
        Check(red == null && player.gameData.points == collisionScore + 40, "Real physics: fire ball destroys red block and awards forty points");
        var blue = Instantiate(player.bluePrefab, new Vector3(0, -1, 0), Quaternion.identity).GetComponent<BlockScript>();
        blue.Initialize(player);
        collisionScore = player.gameData.points;
        ball.SetMaterial(40, Color.gray);
        ball.Body.position = new Vector2(0, -2.2f);
        ball.Body.velocity = Vector2.up * ball.Speed;
        yield return new WaitForSeconds(.18f);
        Check(blue == null && player.gameData.points == collisionScore + 100, "Real physics: steel ball destroys thirty-hit blue block with one collision");
        ball.SetMaterial(1, Color.white);
        var green = Instantiate(player.greenPrefab, new Vector3(0, -1, 0), Quaternion.identity).GetComponent<BlockScript>();
        green.optional = true; green.Initialize(player);
        green.TakeHit(1);
        Check(green.hitsToDestroy == 1 && green.textComponent.text == "1", "Green block requires two hits and updates its label");
        int dropsBefore = FindObjectsOfType<BonusDrop>().Length;
        green.TakeHit(1);
        Check(FindObjectsOfType<BonusDrop>().Length == dropsBefore + 1, "A1: destroying a green block actually creates a bonus");
        foreach (var drop in FindObjectsOfType<BonusDrop>()) Destroy(drop.gameObject);
        player.TogglePause();
        Vector2 frozen = ball.Body.position;
        yield return new WaitForSecondsRealtime(.15f);
        Check(player.IsPaused && player.gameUi.pausePanel.activeSelf && Vector2.Distance(frozen, ball.Body.position) < .001f, "Pause freezes physics and shows interactive overlay");
        yield return new WaitForEndOfFrame();
        CaptureFrame("pause.png");
        player.TogglePause();
        foreach (var item in player.ActiveBalls) { item.Body.bodyType = RigidbodyType2D.Kinematic; item.Body.velocity = Vector2.zero; }
        yield return null;

        int score = player.gameData.points;
        var pointsDrop = player.SpawnBonus(new Vector3(player.transform.position.x, -3, 0), BonusKind.Points);
        yield return new WaitForSeconds(.6f);
        Check(pointsDrop == null && player.gameData.points == score + 100, "A1: real falling +100 bonus is caught and consumed");
        score = player.gameData.points;
        var missed = player.SpawnBonus(new Vector3(5, -5.1f, 0), BonusKind.Points);
        yield return new WaitForSeconds(.5f);
        Check(missed == null && player.gameData.points == score, "A1: missed bonus disappears at bottom without activation");

        float originalSpeed = ball.Speed;
        var slow = player.SpawnBonus(new Vector3(-4, -2, 0), BonusKind.Slow);
        slow.BonusActivate(); Destroy(slow.gameObject);
        Check(Mathf.Approximately(ball.Speed, originalSpeed * .9f), "A2: Slow reduces existing ball speed exactly ten percent");
        var fast = player.SpawnBonus(new Vector3(-4, -2, 0), BonusKind.Fast);
        fast.BonusActivate(); Destroy(fast.gameObject);
        Check(Mathf.Approximately(ball.Speed, originalSpeed * .99f), "A2: Fast increases current ball speed exactly ten percent");
        int reserve = player.gameData.balls;
        var extraOne = player.SpawnBonus(Vector3.zero, BonusKind.Ball);
        extraOne.BonusActivate(); Destroy(extraOne.gameObject);
        Check(player.gameData.balls == reserve + 1 && player.ActiveBalls.Count == 2, "A2: Ball adds only one reserve ball");

        var fire = player.SpawnBonus(Vector3.zero, BonusKind.Fire); fire.BonusActivate(); Destroy(fire.gameObject);
        Check(ball.Damage == 4 && ball.GetComponent<SpriteRenderer>().color == new Color(1, .5f, 0), "A3: Fire sets orange balls and four-hit damage");
        int active = player.ActiveBalls.Count; reserve = player.gameData.balls;
        var two = player.SpawnBonus(Vector3.zero, BonusKind.PlusTwo); two.BonusActivate(); Destroy(two.gameObject);
        Check(player.ActiveBalls.Count == active + 2 && player.gameData.balls == reserve + 2, "A2: +2 immediately launches two new balls and increases reserve");
        Check(player.ActiveBalls[active].Damage == 1 && player.ActiveBalls[active].SpeedMultiplier == 1, "Bonuses are not inherited by newly added balls");
        active = player.ActiveBalls.Count; reserve = player.gameData.balls;
        var ten = player.SpawnBonus(Vector3.zero, BonusKind.PlusTen); ten.BonusActivate(); Destroy(ten.gameObject);
        Check(player.ActiveBalls.Count == active + 10 && player.gameData.balls == reserve + 10, "A2: +10 immediately launches ten new balls and increases reserve");
        var steel = player.SpawnBonus(Vector3.zero, BonusKind.Steel); steel.BonusActivate(); Destroy(steel.gameObject);
        bool allSteel = true;
        foreach (var item in player.ActiveBalls) allSteel &= item.Damage == 40 && item.GetComponent<SpriteRenderer>().color == Color.gray;
        Check(allSteel, "A3: Steel sets all existing balls gray with forty-hit damage");
        var normal = player.SpawnBonus(Vector3.zero, BonusKind.Normal); normal.BonusActivate(); Destroy(normal.gameObject);
        bool allNormal = true;
        foreach (var item in player.ActiveBalls)
        {
            allNormal &= item.Damage == 1 && item.GetComponent<SpriteRenderer>().color == Color.white;
            item.Body.bodyType = RigidbodyType2D.Kinematic;
        }
        Check(allNormal, "A3: Norm restores white balls and one-hit damage");

        player.gameData.pointsToBall = player.RequiredPointsToBall - 10;
        reserve = player.gameData.balls;
        player.AddPoints(10);
        Check(player.gameData.balls == reserve + 1 && player.gameData.pointsToBall == 0, "Extra reserve ball at score threshold");
        var weights = player.gameData.bonusWeights;
        player.gameData.bonusWeights = new[] { new BonusWeight(BonusKind.Steel, 1), new BonusWeight(BonusKind.Points, 0) };
        bool selection = true;
        for (int i = 0; i < 100; i++) selection &= player.gameData.ChooseBonus() == BonusKind.Steel;
        Check(selection, "GameData weighted selection respects configurable zero weights");
        player.gameData.bonusWeights = weights;

        player.gameData.Save("Arcanoid.QA.");
        var restored = ScriptableObject.CreateInstance<GameDataScript>(); restored.Load("Arcanoid.QA.");
        Check(restored.level == player.gameData.level && restored.balls == player.gameData.balls &&
            restored.points == player.gameData.points && restored.pointsToBall == player.gameData.pointsToBall &&
            restored.music == player.gameData.music && restored.sound == player.gameData.sound, "Save/load round trip preserves all six state fields");
        foreach (string key in new[] { "level", "balls", "points", "pointsToBall", "music", "sound" }) PlayerPrefs.DeleteKey("Arcanoid.QA." + key);
        PlayerPrefs.Save(); Destroy(restored);

        // Real collision with the loss trigger followed by coroutine-based replenishment.
        var copy = new List<BallScript>(player.ActiveBalls);
        foreach (var item in copy) item.Lose();
        yield return new WaitForSeconds(.3f);
        Check(player.ActiveBalls.Count == 2 && player.ActiveBalls[0].IsDocked, "Losing all active balls creates two docked replacements from reserve");
        ball = player.ActiveBalls[0]; reserve = player.gameData.balls;
        ball.Launch(); ball.Body.position = new Vector2(4, -5.1f); ball.Body.velocity = Vector2.down * ball.Speed;
        yield return new WaitForSeconds(.2f);
        Check(ball == null && player.gameData.balls == reserve - 1, "Real physics: crossing bottom trigger removes a ball and decrements reserve");

        int before = player.gameData.level;
        foreach (var block in FindObjectsOfType<BlockScript>()) if (!block.optional) block.TakeHit(40);
        yield return new WaitForSeconds(1);
        player = Player;
        Check(player.gameData.level == before + 1 && player.background.sprite.name == "02", "Clearing required blocks advances level and changes background");
        bool reset = true;
        foreach (var item in player.ActiveBalls) reset &= item.Damage == 1 && item.SpeedMultiplier == 1;
        Check(reset, "Speed and material effects reset on level transition");

        player.gameData.level = 30;
        SceneManager.LoadScene("MainScene");
        yield return null; yield return new WaitForSeconds(.2f);
        player = Player;
        var blocks = FindObjectsOfType<BlockScript>();
        Check(blocks.Length == 45 && player.RemainingBlocks == 37 && player.background.sprite.name == "30", "Level 30 has capped counts: 8 blue, 10 red, 12 green, 15 yellow");
        bool noOverlap = true;
        for (int i = 0; i < blocks.Length; i++)
            for (int j = i + 1; j < blocks.Length; j++)
                noOverlap &= !blocks[i].GetComponent<Collider2D>().bounds.Intersects(blocks[j].GetComponent<Collider2D>().bounds);
        Check(noOverlap, "Maximum-density level has no overlapping blocks");
        Check(Mathf.Abs(player.ActiveBalls[0].Speed - new Vector2(100, 300).magnitude * .02f * 1.6f) < .001f, "Ball initial speed increases with level");
        yield return new WaitForEndOfFrame();
        CaptureFrame("level30.png");
        foreach (var block in blocks) if (!block.optional) block.TakeHit(40);
        yield return new WaitForSeconds(1);
        player = Player;
        Check(player.gameData.level == 30, "Final level repeats at 30, matching the base tutorial");

        player.gameData.balls = player.ActiveBalls.Count;
        copy = new List<BallScript>(player.ActiveBalls);
        foreach (var item in copy) item.Lose();
        yield return new WaitForSeconds(1.4f);
        player = Player;
        Check(player.gameData.level == 1 && player.gameData.points == 0 && player.gameData.balls == 6, "Exhausting reserve starts a new game with six balls");
        player.TogglePause(); player.NewGame();
        yield return null; yield return new WaitForSeconds(.15f);
        Check(Time.timeScale == 1 && !Player.IsPaused, "New game from pause restores normal time scale");

        File.WriteAllText(Path.Combine(directory, Application.isEditor ? "editor-runtime-report.json" : "runtime-report.json"), JsonUtility.ToJson(report, true));
        Debug.Log("ARCANOID QA COMPLETE: " + report.passed.Count + " passed, " + report.failed.Count + " failed.");
        Application.logMessageReceived -= LogMessage;
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("Arcanoid.EditorSmokeCheck", false);
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(report.failed.Count == 0 ? 0 : 1);
#endif
    }
}
