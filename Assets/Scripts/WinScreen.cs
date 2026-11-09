using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Win screen: shows the values the GameManager carried over from the level
/// (run time, attempts). Created automatically in the Win scene by ScreenBootstrap.
/// </summary>
public class WinScreen : MonoBehaviour
{
    void Start()
    {
        GameManager gm = GameManager.Ensure();
        Transform canvas = UIKit.CreateCanvas("Win").transform;

        Image bg = UIKit.NewImage("Background", canvas, UIKit.Background);
        UIKit.Stretch(bg.rectTransform);

        Image diamond = UIKit.NewSprite("Diamond", canvas, Resources.Load<Sprite>("Portraits/Diamond"), Color.white);
        UIKit.Place(diamond.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(260, 230));

        Text title = UIKit.NewText(canvas, "HEIST COMPLETE!", 120, TextAnchor.MiddleCenter, UIKit.Gold, FontStyle.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -310), new Vector2(1600, 150));

        string level = gm.CurrentLevel != null ? gm.CurrentLevel.title : "Heist";
        string extra = gm.CurrentLevel != null && gm.CurrentLevel.isTutorial
            ? "You finished the tutorial - you're ready for real heists!"
            : "Clean getaway!";
        Text stats = UIKit.NewText(canvas,
            $"{level}\nRun time: {gm.LastRunTime:0.0} seconds\nAttempts: {Mathf.Max(1, gm.Attempts)}\n\n{extra}",
            44, TextAnchor.UpperCenter, Color.white);
        UIKit.Place(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -470), new Vector2(1500, 300));

        Button home = UIKit.NewButton(canvas, "Home", () => gm.GoHome(), null, 40);
        UIKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20, 80), new Vector2(360, 110));

        Button again = UIKit.NewButton(canvas, "Play again", () => gm.ChooseLevel(gm.CurrentLevel), UIKit.Green, 40);
        UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20, 80), new Vector2(360, 110));
    }
}
