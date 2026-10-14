public class ExtraBallsBonus : BonusBase
{
    public int count = 1;
    public bool spawnImmediately;
    public override void BonusActivate()
    {
        Player.gameData.balls += count;
        if (spawnImmediately) Player.SpawnExtraBalls(count);
        Player.Notify("Запас шариков +" + count);
    }
}
