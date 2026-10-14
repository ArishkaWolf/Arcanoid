using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class BallScript : MonoBehaviour
{
    public Vector2 ballInitialForce = new Vector2(100, 300);
    public GameDataScript gameData;
    public AudioClip hitSound;
    public AudioClip loseSound;
    public int Damage { get; private set; } = 1;
    public float SpeedMultiplier { get; private set; } = 1;
    public bool IsDocked => Body.bodyType == RigidbodyType2D.Kinematic;
    public float Speed => ballInitialForce.magnitude * .02f * SpeedMultiplier;
    public Rigidbody2D Body { get; private set; }
    PlayerScript player;
    float deltaX;
    CircleCollider2D ballCollider;
    bool lost;

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        ballCollider = GetComponent<CircleCollider2D>();
    }

    public void Initialize(PlayerScript owner, float offset, Vector2 force)
    {
        player = owner; gameData = owner.gameData; deltaX = offset; ballInitialForce = force;
        Body.bodyType = RigidbodyType2D.Kinematic;
        Dock();
    }

    void LateUpdate()
    {
        if (player != null && IsDocked && !player.IsPaused) Dock();
    }

    void Dock()
    {
        transform.position = player.GetDockPosition(deltaX, ballCollider.radius * Mathf.Abs(transform.lossyScale.x));
        Body.position = transform.position;
    }

    public void Launch()
    {
        if (!IsDocked || lost) return;
        Body.bodyType = RigidbodyType2D.Dynamic;
        Body.velocity = ballInitialForce.normalized * Speed;
    }

    void FixedUpdate()
    {
        if (!IsDocked && !lost && Body.velocity.sqrMagnitude > .001f)
            Body.velocity = Body.velocity.normalized * Speed;
    }

    public void ChangeSpeed(float factor)
    {
        SpeedMultiplier *= factor;
        if (!IsDocked) Body.velocity *= factor;
    }

    public void SetMaterial(int damage, Color color)
    {
        Damage = damage;
        GetComponent<SpriteRenderer>().color = color;
    }

    public void Nudge()
    {
        if (IsDocked) return;
        Vector2 change = Random.value < .5f ? new Vector2(-.3f, .3f) : new Vector2(.3f, -.3f);
        Body.velocity = (Body.velocity + change).normalized * Speed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (player != null) player.PlayEffect(hitSound);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Bottom")) Lose();
    }

    public void Lose()
    {
        if (lost) return;
        lost = true;
        player.PlayEffect(loseSound);
        GetComponent<Collider2D>().enabled = false;
        player.BallDestroyed(this);
        Destroy(gameObject);
    }
}
