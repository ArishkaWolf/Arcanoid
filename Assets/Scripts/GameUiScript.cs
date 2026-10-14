using UnityEngine;
using UnityEngine.UI;

public class GameUiScript : MonoBehaviour
{
    public Text levelText, ballsText, scoreText, progressText, noticeText, audioText, launchText;
    public GameObject pausePanel;
    public Button resumeButton, newGameButton, quitButton;
    PlayerScript player;
    float noticeUntil;

    public bool ValidateConfiguration(out string error)
    {
        bool valid = levelText != null && ballsText != null && scoreText != null && progressText != null &&
            noticeText != null && audioText != null && launchText != null && pausePanel != null &&
            resumeButton != null && newGameButton != null && quitButton != null;
        error = valid ? "" : "Game UI text, panel or button references";
        return valid;
    }

    public void Initialize(PlayerScript owner)
    {
        player = owner;
        resumeButton.onClick.AddListener(player.TogglePause);
        newGameButton.onClick.AddListener(player.NewGame);
        quitButton.onClick.AddListener(player.Quit);
        SetPaused(false);
    }

    void Update()
    {
        if (player == null) return;
        var data = player.gameData;
        levelText.text = "УРОВЕНЬ  <b>" + data.level.ToString("00") + " / 30</b>";
        ballsText.text = "ШАРИКИ  <b>" + data.balls.ToString("00") + "</b>";
        scoreText.text = "ОЧКИ  <b>" + data.points.ToString("00000") + "</b>";
        progressText.text = "До нового шарика: " + (player.RequiredPointsToBall - data.pointsToBall) + " очков";
        audioText.text = "M  музыка " + (data.music ? "вкл" : "выкл") + "     S  эффекты " + (data.sound ? "вкл" : "выкл");
        bool docked = false;
        foreach (var ball in player.ActiveBalls) docked |= ball.IsDocked;
        launchText.gameObject.SetActive(docked && !player.IsPaused);
        if (Time.unscaledTime > noticeUntil) noticeText.text = "";
    }

    public void SetPaused(bool paused) { pausePanel.SetActive(paused); }
    public void Notify(string message) { noticeText.text = message; noticeUntil = Time.unscaledTime + 3; }
}
