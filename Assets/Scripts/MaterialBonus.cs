public class MaterialBonus : BonusBase
{
    public int damage = 4;
    public override void BonusActivate()
    {
        foreach (var ball in Player.ActiveBalls) ball.SetMaterial(damage, backgroundColor);
        Player.Notify(bonusText + " · сила удара ×" + damage);
    }
}
