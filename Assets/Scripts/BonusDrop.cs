using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(Rigidbody2D))]
public class BonusDrop : MonoBehaviour
{
    public float fallSpeed = 2;
    public Text label;
    public PlayerScript Player { get; private set; }
    bool consumed;

    public void Initialize(PlayerScript owner) { Player = owner; }

    void FixedUpdate() { GetComponent<Rigidbody2D>().velocity = Vector2.down * fallSpeed; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (other.CompareTag("Player"))
        {
            consumed = true;
            GetComponent<BonusBase>().BonusActivate();
            Destroy(gameObject);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Bottom"))
        {
            consumed = true;
            Destroy(gameObject);
        }
    }
}
