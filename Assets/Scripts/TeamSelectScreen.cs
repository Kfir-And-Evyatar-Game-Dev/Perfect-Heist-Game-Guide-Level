using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crew screen: five character cards. Tapping a card opens a small window with the
/// character's abilities and an "Add to crew" button. Characters that aren't part of
/// this heist are shown as dark silhouettes. In the tutorial a guide walks the player
/// through choosing the Hacker and the Sneak.
/// </summary>
public class TeamSelectScreen : MonoBehaviour
{
    GameManager gm;
    LevelInfo level;
    readonly List<CharacterRole> crew = new List<CharacterRole>();
    readonly Dictionary<CharacterRole, RectTransform> cards = new Dictionary<CharacterRole, RectTransform>();
    readonly Dictionary<CharacterRole, GameObject> inCrewBadges = new Dictionary<CharacterRole, GameObject>();

    // The abilities window.
    GameObject popup;
    Image popupPortrait;
    Text popupName, popupTagline, popupAbilities, popupNote;
    Button addButton;
    CharacterInfo shown;

    Text crewText;
    Button startButton;

    void Start()
    {
        gm = GameManager.Ensure();
        level = gm.CurrentLevel;
        Build();
        Refresh();
        if (level.isTutorial) RunTutorialGuide();
    }

    // ------------------------------------------------------------------ actions

    void OpenInfo(CharacterInfo info)
    {
        shown = info;
        Sprite portrait = CharacterCatalog.Portrait(info);
        bool available = level.Allows(info.role) && info.hasArt;
        popupPortrait.sprite = portrait;
        popupPortrait.color = available ? Color.white : new Color(0.08f, 0.08f, 0.1f);
        popupName.text = info.name;
        popupTagline.text = info.tagline;
        popupAbilities.text = "• " + info.abilities[0] + "\n• " + info.abilities[1];
        popup.SetActive(true);
        Refresh();
    }

    void ToggleShown()
    {
        if (shown == null) return;
        if (crew.Contains(shown.role)) crew.Remove(shown.role);
        else if (crew.Count < level.teamSize) crew.Add(shown.role);
        popup.SetActive(false);
        Refresh();
    }

    void Refresh()
    {
        foreach (var pair in inCrewBadges) pair.Value.SetActive(crew.Contains(pair.Key));
        crewText.text = $"Crew: {crew.Count} / {level.teamSize}";
        startButton.interactable = crew.Count == level.teamSize;

        if (shown != null)
        {
            bool available = level.Allows(shown.role) && shown.hasArt;
            bool inCrew = crew.Contains(shown.role);
            addButton.interactable = available && (inCrew || crew.Count < level.teamSize);
            UIKit.SetLabel(addButton, inCrew ? "Remove from crew" : "Add to crew");
            popupNote.text = !available ? "Not part of this heist." :
                             (!inCrew && crew.Count >= level.teamSize ? "Your crew is full." : "");
        }
    }

    // ------------------------------------------------------------------ tutorial

    void RunTutorialGuide()
    {
        RectTransform addRect = (RectTransform)addButton.transform;
        GuideRunner.Run(new List<GuideStep>
        {
            GuideStep.Explain("Every heist needs a crew. Each crew member has a special skill.\n" +
                              "For this heist you need 2 crew members."),
            GuideStep.Tap("Tap the Hacker to see what the Hacker can do.",
                          () => UIKit.ScreenRect(cards[CharacterRole.Hacker]),
                          () => popup.activeSelf && shown.role == CharacterRole.Hacker),
            GuideStep.Explain("The Hacker can switch off a security camera for 8 seconds.\n" +
                              "That will help us sneak past it!"),
            GuideStep.Tap("Tap 'Add to crew'.",
                          () => UIKit.ScreenRect(addRect),
                          () => crew.Contains(CharacterRole.Hacker)),
            GuideStep.Tap("Great! Now tap the Sneak.",
                          () => UIKit.ScreenRect(cards[CharacterRole.Sneak]),
                          () => popup.activeSelf && shown.role == CharacterRole.Sneak),
            GuideStep.Explain("The Sneak moves silently. The Sneak will grab the diamond."),
            GuideStep.Tap("Tap 'Add to crew'.",
                          () => UIKit.ScreenRect(addRect),
                          () => crew.Contains(CharacterRole.Sneak)),
            GuideStep.Explain("The dark characters are locked. You will meet them in later heists."),
            GuideStep.Tap("Your crew is ready! Tap 'Start planning'.",
                          () => UIKit.ScreenRect((RectTransform)startButton.transform),
                          () => false),
        });
    }

    // ------------------------------------------------------------------ building

    void Build()
    {
        Transform canvas = UIKit.CreateCanvas("Crew Select").transform;
        Image bg = UIKit.NewImage("Background", canvas, UIKit.Background);
        UIKit.Stretch(bg.rectTransform);

        Button back = UIKit.NewButton(canvas, "< Home", () => gm.GoHome());
        UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40, -40), new Vector2(220, 80));

        Text title = UIKit.NewText(canvas, "Choose your crew", 80, TextAnchor.MiddleCenter, UIKit.Gold, FontStyle.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(1200, 110));

        Text sub = UIKit.NewText(canvas, $"{level.title}: this heist needs {level.teamSize} crew members. Tap a character to see their skills.",
                                 34, TextAnchor.MiddleCenter, UIKit.Muted);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(1700, 60));

        RectTransform row = UIKit.NewRect("Characters", canvas);
        UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1760, 520));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 30;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        foreach (CharacterInfo info in CharacterCatalog.All) BuildCard(row, info);

        // Bottom bar: crew count and start button.
        crewText = UIKit.NewText(canvas, "", 40, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        UIKit.Place(crewText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80, 50), new Vector2(600, 90));

        startButton = UIKit.NewButton(canvas, "Start planning  >", () => gm.StartPlanning(crew), UIKit.Green, 38);
        UIKit.Place((RectTransform)startButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-80, 50), new Vector2(420, 100));

        BuildPopup(canvas);
    }

    void BuildCard(Transform parent, CharacterInfo info)
    {
        bool available = level.Allows(info.role) && info.hasArt;
        Image card = UIKit.NewImage(info.name, parent, available ? UIKit.Card : new Color(0.11f, 0.12f, 0.16f));
        RectTransform rt = card.rectTransform;
        cards[info.role] = rt;

        Image portrait = UIKit.NewSprite("Portrait", rt, CharacterCatalog.Portrait(info),
                                         available ? Color.white : new Color(0.06f, 0.06f, 0.08f));
        UIKit.Place(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(240, 300));

        Text name = UIKit.NewText(rt, available ? info.name : info.name + "\n(locked)", 40, TextAnchor.UpperCenter,
                                  available ? Color.white : UIKit.Muted, FontStyle.Bold);
        UIKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(320, 120));

        Image badge = UIKit.NewImage("In crew", rt, UIKit.Green);
        badge.raycastTarget = false;
        UIKit.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(200, 50));
        UIKit.NewText(badge.rectTransform, "IN CREW", 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        inCrewBadges[info.role] = badge.gameObject;

        var button = card.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => OpenInfo(info));
    }

    void BuildPopup(Transform canvas)
    {
        // A dark backdrop that closes the window when tapped outside it.
        Image shade = UIKit.NewImage("Info Window", canvas, new Color(0, 0, 0, 0.6f));
        UIKit.Stretch(shade.rectTransform);
        shade.gameObject.AddComponent<Button>().onClick.AddListener(() => popup.SetActive(false));
        popup = shade.gameObject;

        Image panel = UIKit.NewImage("Panel", shade.rectTransform, UIKit.Panel);
        RectTransform prt = panel.rectTransform;
        UIKit.Place(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560));
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // taps on the panel don't close it

        popupPortrait = UIKit.NewSprite("Portrait", prt, null, Color.white);
        UIKit.Place(popupPortrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50, 20), new Vector2(320, 420));

        popupName = UIKit.NewText(prt, "", 64, TextAnchor.UpperLeft, UIKit.Gold, FontStyle.Bold);
        UIKit.Place(popupName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420, -40), new Vector2(640, 80));

        popupTagline = UIKit.NewText(prt, "", 32, TextAnchor.UpperLeft, UIKit.Muted, FontStyle.Italic);
        UIKit.Place(popupTagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420, -125), new Vector2(640, 50));

        Text skills = UIKit.NewText(prt, "Skills", 34, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
        UIKit.Place(skills.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420, -195), new Vector2(640, 50));

        popupAbilities = UIKit.NewText(prt, "", 34, TextAnchor.UpperLeft, Color.white);
        UIKit.Place(popupAbilities.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420, -250), new Vector2(640, 160));

        popupNote = UIKit.NewText(prt, "", 30, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.4f));
        UIKit.Place(popupNote.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420, 130), new Vector2(640, 50));

        addButton = UIKit.NewButton(prt, "Add to crew", ToggleShown, UIKit.Green, 36);
        UIKit.Place((RectTransform)addButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420, 40), new Vector2(380, 90));

        Button close = UIKit.NewButton(prt, "Close", () => popup.SetActive(false));
        UIKit.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40, 40), new Vector2(200, 90));

        popup.SetActive(false);
    }
}
