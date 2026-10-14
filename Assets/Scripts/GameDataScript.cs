using System;
using UnityEngine;

public enum BonusKind { Points, Slow, Fast, Ball, PlusTwo, PlusTen, Fire, Steel, Normal }

[Serializable]
public class BonusWeight
{
    public BonusKind kind;
    [Min(0)] public float weight = 1;
    public BonusWeight(BonusKind kind, float weight) { this.kind = kind; this.weight = weight; }
}

[CreateAssetMenu(fileName = "GameData", menuName = "Arcanoid/Game Data", order = 51)]
public class GameDataScript : ScriptableObject
{
    [Tooltip("Restore saved progress on application start. Disable to use Inspector values.")]
    public bool resetOnStart = true;
    [Range(1, 30)] public int level = 1;
    [Min(1)] public int balls = 6;
    public int points;
    public int pointsToBall;
    public bool music = true;
    public bool sound = true;
    [Header("Bonus assignments A1, A2, A3")]
    public bool bonusesEnabled = true;
    public BonusWeight[] bonusWeights = {
        new BonusWeight(BonusKind.Points, 4), new BonusWeight(BonusKind.Slow, 2),
        new BonusWeight(BonusKind.Fast, 1), new BonusWeight(BonusKind.Ball, 2),
        new BonusWeight(BonusKind.PlusTwo, 2), new BonusWeight(BonusKind.PlusTen, .5f),
        new BonusWeight(BonusKind.Fire, 2), new BonusWeight(BonusKind.Steel, 1),
        new BonusWeight(BonusKind.Normal, 1)
    };

    public void Reset()
    {
        level = 1; balls = 6; points = 0; pointsToBall = 0;
    }

     public void Save(string prefix = "Arcanoid.")
    {
        PlayerPrefs.SetInt(prefix + "level", level);
        PlayerPrefs.SetInt(prefix + "balls", balls);
        PlayerPrefs.SetInt(prefix + "points", points);
        PlayerPrefs.SetInt(prefix + "pointsToBall", pointsToBall);
        PlayerPrefs.SetInt(prefix + "music", music ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "sound", sound ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void Load(string prefix = "Arcanoid.")
    {
        level = Mathf.Clamp(PlayerPrefs.GetInt(prefix + "level", 1), 1, 30);
        balls = Mathf.Max(1, PlayerPrefs.GetInt(prefix + "balls", 6));
        points = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "points", 0));
        pointsToBall = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "pointsToBall", 0));
        music = PlayerPrefs.GetInt(prefix + "music", 1) == 1;
        sound = PlayerPrefs.GetInt(prefix + "sound", 1) == 1;
    }

    public BonusKind ChooseBonus()
    {
        float total = 0;
        foreach (var entry in bonusWeights) total += Mathf.Max(0, entry.weight);
        if (total <= 0) return BonusKind.Points;
        float value = UnityEngine.Random.value * total;
        foreach (var entry in bonusWeights)
        {
            value -= Mathf.Max(0, entry.weight);
            if (value < 0) return entry.kind;
        }
        return BonusKind.Points;
    }
}
