using UnityEngine;

public class BonusBase : MonoBehaviour
{
    public string bonusText = "+100";
    public Color backgroundColor = Color.yellow;
    public Color textColor = Color.black;
    protected PlayerScript Player => GetComponent<BonusDrop>().Player;

    protected virtual void Start()
    {
        GetComponent<SpriteRenderer>().color = backgroundColor;
        var label = GetComponent<BonusDrop>().label;
        label.text = bonusText; label.color = textColor;
    }

    public virtual void BonusActivate()
    {
        Player.AddPoints(100);
        Player.Notify("+100 очков");
    }
}
