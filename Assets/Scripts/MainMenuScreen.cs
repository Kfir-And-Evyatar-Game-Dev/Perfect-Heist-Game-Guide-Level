using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home screen: the game title and a card for every heist. Only the tutorial is
/// open; the others are locked ("Coming soon"). Created automatically in the
/// MainMenu scene by ScreenBootstrap.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    RectTransform tutorialCard;

    void Start()
    {
        GameManager gm = GameManager.Ensure();
        Build(gm);

        if (!gm.TutorialCompleted)
        {
            GuideRunner.Run(new List<GuideStep>
            {
                GuideStep.Explain("Welcome to PERFECT HEIST! Here you plan heists step by step,\n" +
                                  "then press play and watch your crew follow the plan.", "Let's go"),
                GuideStep.Tap("Start with the Tutorial. Tap it to begin.",
                              () => UIKit.ScreenRect(tutorialCard), () => false),
            });
        }
    }

    void Build(GameManager gm)
    {
        Transform canvas = UIKit.CreateCanvas("Main Menu").transform;

        Image bg = UIKit.NewImage("Background", canvas, UIKit.Background);
        UIKit.Stretch(bg.rectTransform);

        Text title = UIKit.NewText(canvas, "PERFECT HEIST", 120, TextAnchor.MiddleCenter, UIKit.Gold, FontStyle.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1600, 150));

        Text sub = UIKit.NewText(canvas, "Plan it.  Run it.  Get away clean.", 42, TextAnchor.MiddleCenter, UIKit.Muted);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(1600, 60));

        Text choose = UIKit.NewText(canvas, "Choose a heist", 40, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        UIKit.Place(choose.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -310), new Vector2(1600, 60));

        RectTransform row = UIKit.NewRect("Heists", canvas);
        UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1700, 460));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        Sprite diamond = Resources.Load<Sprite>("Portraits/Diamond");
        foreach (LevelInfo level in LevelInfo.All)
        {
            RectTransform card = BuildCard(row, level, diamond, gm);
            if (level.isTutorial) tutorialCard = card;
        }

        Text credit = UIKit.NewText(canvas, "Kfir Nizri & Evyatar Aharon", 28, TextAnchor.MiddleCenter, UIKit.Muted);
        UIKit.Place(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1200, 40));
    }

    RectTransform BuildCard(Transform parent, LevelInfo level, Sprite icon, GameManager gm)
    {
        Image card = UIKit.NewImage(level.title, parent, level.locked ? new Color(0.11f, 0.12f, 0.16f) : UIKit.Card);
        RectTransform rt = card.rectTransform;

        Image art = UIKit.NewSprite("Icon", rt, icon, level.locked ? new Color(0.08f, 0.08f, 0.1f) : Color.white);
        UIKit.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(200, 180));

        Text name = UIKit.NewText(rt, level.title, 44, TextAnchor.MiddleCenter, level.locked ? UIKit.Muted : Color.white, FontStyle.Bold);
        UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(380, 60));

        Text sub = UIKit.NewText(rt, level.subtitle, 28, TextAnchor.UpperCenter, UIKit.Muted);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -305), new Vector2(340, 80));

        string badge = level.locked ? "LOCKED" : (level.isTutorial && gm.TutorialCompleted ? "COMPLETED" : "START HERE");
        Color badgeColor = level.locked ? new Color(0.3f, 0.3f, 0.35f) : (badge == "COMPLETED" ? UIKit.Green : new Color(0.85f, 0.45f, 0.1f));
        Image badgeBg = UIKit.NewImage("Badge", rt, badgeColor);
        badgeBg.raycastTarget = false;
        UIKit.Place(badgeBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(260, 56));
        UIKit.NewText(badgeBg.rectTransform, badge, 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

        if (!level.locked)
        {
            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => gm.ChooseLevel(level));
        }
        return rt;
    }
}
