using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerScript : MonoBehaviour
{
    public const int MaxLevel = 30;
    public GameDataScript gameData;
    public float ballVelocityMult = .02f;
    public GameObject bluePrefab, redPrefab, greenPrefab, yellowPrefab, ballPrefab, bonusPrefab;
    public SpriteRenderer background;
    public AudioSource musicSource, effectsSource;
    public AudioClip pointSound;
    public GameUiScript gameUi;
    public IReadOnlyList<BallScript> ActiveBalls => activeBalls;
    public int RemainingBlocks { get; private set; }
    public int RequiredPointsToBall => 400 + (gameData.level - 1) * 20;
    public bool IsPaused => Time.timeScale == 0;
    public bool IsSmokeTest { get; private set; }
    static bool gameStarted;
    static GameDataScript smokeData;
    readonly List<BallScript> activeBalls = new List<BallScript>();
    bool changingLevel, recoveringBalls, initialized;
    Rigidbody2D body;
    Camera mainCamera;
    SpriteRenderer paddleRenderer;
    CircleCollider2D paddleCollider;
    Collider2D leftWall, rightWall;
    float targetX;
    Vector3 previousMousePosition = new Vector3(-1, -1, 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { gameStarted = false; smokeData = null; Time.timeScale = 1; }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        paddleRenderer = GetComponent<SpriteRenderer>();
        paddleCollider = GetComponent<CircleCollider2D>();
        leftWall = GameObject.Find("Left Wall")?.GetComponent<Collider2D>();
        rightWall = GameObject.Find("Right Wall")?.GetComponent<Collider2D>();
        Application.targetFrameRate = 60;

        string[] names = { "Wall", "Player", "Ball", "Block", "Bottom", "Bonus" };
        foreach (string a in names)
            foreach (string b in names)
            {
                bool allowed = (a == "Ball" && b != "Ball" && b != "Bonus") ||
                    (b == "Ball" && a != "Ball" && a != "Bonus") ||
                    (a == "Bonus" && (b == "Player" || b == "Bottom")) ||
                    (b == "Bonus" && (a == "Player" || a == "Bottom"));
                Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer(a), LayerMask.NameToLayer(b), !allowed);
            }
        int bonusLayer = LayerMask.NameToLayer("Bonus");
        for (int layer = 0; layer < 32; layer++)
            Physics2D.IgnoreLayerCollision(bonusLayer, layer,
                layer != LayerMask.NameToLayer("Player") && layer != LayerMask.NameToLayer("Bottom"));
    }

    void Start()
    {
        string error;
        if (!ValidateConfiguration(out error))
        {
            Debug.LogError("Arcanoid: scene initialization failed. " + error, this);
            enabled = false;
            if (gameUi != null) gameUi.enabled = false;
            Cursor.visible = true;
            return;
        }
        IsSmokeTest = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-arcanoidSmoke") >= 0;
#if UNITY_EDITOR
        IsSmokeTest |= UnityEditor.SessionState.GetBool("Arcanoid.EditorSmokeCheck", false);
#endif
        if (IsSmokeTest)
        {
            if (smokeData == null)
            {
                smokeData = Instantiate(gameData);
                smokeData.Reset(); smokeData.music = false; smokeData.sound = false;
            }
            gameData = smokeData;
        }
        else if (!gameStarted && gameData.resetOnStart) gameData.Load();
        gameStarted = true;
        Time.timeScale = 1;
        body = GetComponent<Rigidbody2D>(); mainCamera = Camera.main;
        SetTargetX(transform.position.x);
        gameUi.Initialize(this);
        SetMusic();
        StartLevel();
        initialized = true;
        Cursor.visible = false;
        if (IsSmokeTest && FindObjectOfType<ArcanoidSmokeCheck>() == null)
            new GameObject("Arcanoid integration check").AddComponent<ArcanoidSmokeCheck>();
    }

    void Update()
    {
        if (!initialized) return;
        if (Input.GetButtonDown("Pause")) TogglePause();
        if (Input.GetKeyDown(KeyCode.M)) { gameData.music = !gameData.music; SetMusic(); }
        if (Input.GetKeyDown(KeyCode.S)) gameData.sound = !gameData.sound;
        if (Input.GetKeyDown(KeyCode.N)) { NewGame(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Quit(); return; }
        if (IsPaused || changingLevel || IsSmokeTest) return;
        float keyboard = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(keyboard) > .1f) SetTargetX(targetX + keyboard * 12 * Time.deltaTime);
        else if ((Input.mousePosition - previousMousePosition).sqrMagnitude > .1f &&
            Input.mousePosition.x >= 0 && Input.mousePosition.x <= Screen.width)
            SetTargetX(mainCamera.ScreenToWorldPoint(Input.mousePosition).x);
        previousMousePosition = Input.mousePosition;
        if (Input.GetButtonDown("Fire1")) foreach (var ball in activeBalls) ball.Launch();
        if (Input.GetKeyDown(KeyCode.J)) foreach (var ball in activeBalls) ball.Nudge();
    }

    void FixedUpdate()
    {
        if (initialized && !changingLevel)
        {
            SetTargetX(targetX);
            body.MovePosition(new Vector2(targetX, body.position.y));
        }
    }

    public float PaddleRadius => paddleCollider.radius * Mathf.Abs(transform.lossyScale.x);

    public void SetTargetX(float x)
    {
        float halfWidth = Mathf.Max(paddleRenderer.bounds.extents.x, PaddleRadius);
        targetX = Mathf.Clamp(x, leftWall.bounds.max.x + halfWidth + .02f,
            rightWall.bounds.min.x - halfWidth - .02f);
    }

    public Vector2 GetDockPosition(float offset, float ballRadius)
    {
        float distance = PaddleRadius + ballRadius + .035f;
        offset = Mathf.Clamp(offset, -distance * .9f, distance * .9f);
        Vector2 center = transform.TransformPoint(paddleCollider.offset);
        return center + new Vector2(offset, Mathf.Sqrt(distance * distance - offset * offset));
    }

    public bool ValidateConfiguration(out string error)
    {
        var missing = new List<string>();
        if (GetComponent<Rigidbody2D>() == null) missing.Add("Player.Rigidbody2D");
        if (GetComponent<CircleCollider2D>() == null) missing.Add("Player.CircleCollider2D");
        if (GetComponent<SpriteRenderer>() == null) missing.Add("Player.SpriteRenderer");
        if (GameObject.Find("Left Wall")?.GetComponent<Collider2D>() == null ||
            GameObject.Find("Right Wall")?.GetComponent<Collider2D>() == null) missing.Add("Side wall colliders");
        if (Camera.main == null) missing.Add("Main Camera (tag MainCamera)");
        if (gameData == null) missing.Add("Player.Game Data");
        if (background == null) missing.Add("Player.Background");
        if (musicSource == null || musicSource.clip == null) missing.Add("Player.Music Source / music clip");
        if (effectsSource == null) missing.Add("Player.Effects Source");
        if (pointSound == null) missing.Add("Player.Point Sound");
        foreach (var prefab in new[] { bluePrefab, redPrefab, greenPrefab, yellowPrefab })
            if (prefab == null || prefab.GetComponent<BlockScript>() == null) missing.Add("Block prefab / BlockScript");
        if (ballPrefab == null || ballPrefab.GetComponent<BallScript>() == null) missing.Add("Player.Ball Prefab");
        if (bonusPrefab == null || bonusPrefab.GetComponent<BonusDrop>() == null) missing.Add("Player.Bonus Prefab");
        if (gameUi == null) missing.Add("Player.Game UI");
        else
        {
            string uiError;
            if (!gameUi.ValidateConfiguration(out uiError)) missing.Add(uiError);
        }
        error = missing.Count == 0 ? "" : "Missing references: " + string.Join(", ", missing) + ". Open Assets/Scenes/MainScene.unity.";
        return missing.Count == 0;
    }

    void StartLevel()
    {
        gameData.level = Mathf.Clamp(gameData.level, 1, MaxLevel);
        background.sprite = Resources.Load<Sprite>(gameData.level.ToString("d2"));
        var cells = new List<Vector3>();
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 10; col++)
                cells.Add(new Vector3(-5.49f + col * 1.22f, .35f + row * .68f, 0));
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 value = cells[i]; cells[i] = cells[j]; cells[j] = value;
        }
        int index = 0;
        CreateBlocks(bluePrefab, Mathf.Min(gameData.level, 8), cells, ref index);
        CreateBlocks(redPrefab, Mathf.Min(1 + gameData.level, 10), cells, ref index);
        CreateBlocks(greenPrefab, Mathf.Min(1 + gameData.level, 12), cells, ref index);
        CreateBlocks(yellowPrefab, Mathf.Min(2 + gameData.level, 15), cells, ref index);
        CreateBalls();
        Notify("Уровень " + gameData.level + " · ЛКМ / Ctrl — запуск");
    }

    void CreateBlocks(GameObject prefab, int count, List<Vector3> cells, ref int index)
    {
        for (int i = 0; i < count; i++)
        {
            var block = Instantiate(prefab, cells[index++], Quaternion.identity).GetComponent<BlockScript>();
            block.Initialize(this);
            if (!block.optional) RemainingBlocks++;
        }
    }

    void CreateBalls()
    {
        int count = Mathf.Min(2, gameData.balls);
        for (int i = 0; i < count; i++) SpawnBall(count == 1 ? .4f : -.4f + i * .8f, i, false);
    }

    BallScript SpawnBall(float offset, int index, bool launch)
    {
        var ball = Instantiate(ballPrefab).GetComponent<BallScript>();
        Vector2 force = new Vector2(100 + index * 10, 300) * (1 + gameData.level * ballVelocityMult);
        ball.Initialize(this, offset, force);
        activeBalls.Add(ball);
        if (launch) ball.Launch();
        return ball;
    }

    public void SpawnExtraBalls(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ball = SpawnBall(Random.Range(-.65f, .65f), i, true);
            Vector2 direction = new Vector2(Mathf.Sin(Mathf.Lerp(-1.1f, 1.1f, (i + .5f) / count)), 1);
            ball.Body.velocity = direction.normalized * ball.Speed;
        }
    }

    public void BallDestroyed(BallScript ball)
    {
        activeBalls.Remove(ball);
        gameData.balls = Mathf.Max(0, gameData.balls - 1);
        if (activeBalls.Count == 0 && !recoveringBalls && !changingLevel)
        {
            recoveringBalls = true;
            StartCoroutine(RecoverBalls());
        }
    }

    IEnumerator RecoverBalls()
    {
        yield return new WaitForSeconds(.15f);
        recoveringBalls = false;
        if (changingLevel || activeBalls.Count > 0) yield break;
        if (gameData.balls > 0) { CreateBalls(); Notify("ЛКМ / Ctrl — новые шарики"); }
        else
        {
            Notify("Шарики закончились · новая игра");
            yield return new WaitForSeconds(1);
            NewGame();
        }
    }

    public void BlockDestroyed(BlockScript block, int points)
    {
        AddPoints(points);
        if (!block.optional) RemainingBlocks--;
        if (RemainingBlocks == 0 && !changingLevel)
        {
            changingLevel = true;
            StartCoroutine(NextLevel());
        }
    }

    public void AddPoints(int points)
    {
        gameData.points += points;
        gameData.pointsToBall += points;
        PlayEffect(pointSound);
        bool earnedBall = false;
        while (gameData.pointsToBall >= RequiredPointsToBall)
        {
            gameData.pointsToBall -= RequiredPointsToBall;
            gameData.balls++; earnedBall = true;
        }
        if (earnedBall) { Notify("+1 шарик за набранные очки"); StartCoroutine(ExtraBallSound()); }
    }

    IEnumerator ExtraBallSound()
    {
        for (int i = 0; i < 10; i++) { yield return new WaitForSeconds(.2f); PlayEffect(pointSound); }
    }

    IEnumerator NextLevel()
    {
        Notify(gameData.level == MaxLevel ? "Все 30 уровней пройдены!" : "Уровень пройден!");
        yield return new WaitForSeconds(.75f);
        gameData.level = Mathf.Min(MaxLevel, gameData.level + 1);
        ReloadScene();
    }

    public BonusBase SpawnBonus(Vector3 position, BonusKind? requested = null)
    {
        var obj = Instantiate(bonusPrefab, position, Quaternion.identity);
        obj.GetComponent<BonusDrop>().Initialize(this);
        BonusKind kind = requested ?? gameData.ChooseBonus();
        BonusBase result;
        if (kind == BonusKind.Slow || kind == BonusKind.Fast)
        {
            var speed = obj.AddComponent<SpeedBonus>();
            speed.factor = kind == BonusKind.Slow ? .9f : 1.1f;
            speed.bonusText = kind == BonusKind.Slow ? "Slow" : "Fast";
            speed.backgroundColor = kind == BonusKind.Slow ? Color.green : Color.red;
            speed.textColor = Color.white; result = speed;
        }
        else if (kind == BonusKind.Ball || kind == BonusKind.PlusTwo || kind == BonusKind.PlusTen)
        {
            var extra = obj.AddComponent<ExtraBallsBonus>();
            extra.count = kind == BonusKind.Ball ? 1 : kind == BonusKind.PlusTwo ? 2 : 10;
            extra.spawnImmediately = kind != BonusKind.Ball;
            extra.bonusText = kind == BonusKind.Ball ? "Ball" : "+" + extra.count;
            extra.backgroundColor = kind == BonusKind.Ball ? Color.green : new Color(.2f, .45f, 1);
            extra.textColor = Color.white; result = extra;
        }
        else if (kind == BonusKind.Fire || kind == BonusKind.Steel || kind == BonusKind.Normal)
        {
            var material = obj.AddComponent<MaterialBonus>();
            material.damage = kind == BonusKind.Fire ? 4 : kind == BonusKind.Steel ? 40 : 1;
            material.bonusText = kind == BonusKind.Fire ? "Fire" : kind == BonusKind.Steel ? "Steel" : "Norm";
            material.backgroundColor = kind == BonusKind.Fire ? new Color(1, .5f, 0) : kind == BonusKind.Steel ? Color.gray : Color.white;
            material.textColor = Color.black; result = material;
        }
        else result = obj.AddComponent<BonusBase>();
        return result;
    }

    public void TogglePause()
    {
        Time.timeScale = IsPaused ? 1 : 0;
        Cursor.visible = IsPaused;
        gameUi.SetPaused(IsPaused);
    }

    public void NewGame() { gameData.Reset(); ReloadScene(); }

    void ReloadScene()
    {
        Time.timeScale = 1;
        if (!IsSmokeTest) gameData.Save();
        SceneManager.LoadScene("MainScene");
    }

    void SetMusic()
    {
        if (gameData.music) { if (!musicSource.isPlaying) musicSource.Play(); }
        else musicSource.Stop();
    }

    public void PlayEffect(AudioClip clip)
    {
        if (gameData.sound && clip != null) effectsSource.PlayOneShot(clip);
    }

    public void Notify(string message) { if (gameUi != null) gameUi.Notify(message); }

    public void Quit()
    {
        if (!IsSmokeTest) gameData.Save();
        Time.timeScale = 1; Cursor.visible = true;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnApplicationQuit() { if (!IsSmokeTest && gameData != null) gameData.Save(); }
}
