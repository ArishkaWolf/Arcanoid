using UnityEngine;
using UnityEngine.UI;

public class BlockScript : MonoBehaviour
{
    public int hitsToDestroy = 4;
    public int points = 40;
    public bool optional;
    public bool dropsBonus;
    public Text textComponent;
    PlayerScript player;
    bool destroyed;

    public void Initialize(PlayerScript owner)
    {
        player = owner;
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (textComponent != null) textComponent.text = hitsToDestroy.ToString();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var ball = collision.gameObject.GetComponent<BallScript>();
        if (ball != null) TakeHit(ball.Damage);
    }

    public void TakeHit(int damage)
    {
        if (destroyed || damage <= 0) return;
        hitsToDestroy -= damage;
        if (hitsToDestroy > 0) { UpdateLabel(); return; }
        destroyed = true;
        GetComponent<Collider2D>().enabled = false;
        if (dropsBonus && player.gameData.bonusesEnabled) player.SpawnBonus(transform.position);
        player.BlockDestroyed(this, points);
        Destroy(gameObject);
    }
}
