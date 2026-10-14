using UnityEngine;

public class SpeedBonus : BonusBase
{
    public float factor = .9f;
    public override void BonusActivate()
    {
        foreach (var ball in Player.ActiveBalls) ball.ChangeSpeed(factor);
        Player.Notify(factor < 1 ? "Slow · скорость −10%" : "Fast · скорость +10%");
    }
}
