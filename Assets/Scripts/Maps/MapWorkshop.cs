using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapWorkshop : MonoBehaviour
{
    enum Screen
    {
        Menu,
        Create,
        Edit,
        SaveName,
        Playing
    }

    PlayableMapBuilder builder;
    GameObject canvasObject;
    GameObject backdrop;
    GameObject sidePanel;
    GameObject postMatchPanel;
    InputField nameInput;
    Text statusText;
    Screen screen = Screen.Menu;
    MapEditHandle dragHandle;
    string armedCatalogId;
    string palettePressId;
    readonly List<PaletteChoice> paletteChoices = new List<PaletteChoice>();
    bool layingRoad;
    bool roadStartSet;
    Vector3 roadStart;

    string draftName = "Blank Map";
    int draftSectorCount = 3;
    readonly List<int> draftPoints = new List<int> { 2, 1, 1 };
    int queuedTestRuns;
    static readonly float[] TestSpeeds = { 1f, 2f, 5f, 10f, 20f, 50f };

    void Awake()
    {
        Time.timeScale = 1f;
    }

    IEnumerator Start()
    {
        yield return null;

        if (GameManager.Instance == null)
        {
            Destroy(gameObject);
            yield break;
        }

        PlayableMapBuilder.ForgetHiddenOriginal();
        builder = new PlayableMapBuilder();
        HideOldFactionMenu();
        CreateCanvas();
        PrepareSession();
        yield return null;
        BeginMatchIfNeeded();
    }

    void Update()
    {
        if (MapSession.continueTest && screen == Screen.Playing)
        {
            MapSession.continueTest = false;
            ContinueTestRun();
            return;
        }

        if (screen == Screen.Edit) HandleDrag();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (screen == Screen.SaveName)
            {
                ShowEdit();
                return;
            }

            if (screen == Screen.Edit)
            {
                if (layingRoad)
                {
                    CancelRoadLay(true);
                    return;
                }

                if (!string.IsNullOrEmpty(armedCatalogId))
                {
                    armedCatalogId = null;
                    palettePressId = null;
                    RefreshPaletteHighlight();
                    if (statusText != null) statusText.text = "Placement cancelled.";
                    return;
                }

                ReturnToMenu();
                return;
            }
        }

        if (screen == Screen.Playing && GameManager.Instance != null && GameManager.Instance.IsMatchOver && postMatchPanel == null && !MapSession.continueTest)
        {
            ShowPostMatch();
        }
    }

    void PrepareSession()
    {
        MapSession.allowLookAround = MapSession.phase == MapSession.Phase.Menu || MapSession.phase == MapSession.Phase.Edit;

        if (MapSession.phase == MapSession.Phase.Edit && MapSession.workingCopy != null)
        {
            ShowGenerated(editing: true);
            screen = Screen.Edit;
            ShowEdit();
            return;
        }

        if (MapSession.phase == MapSession.Phase.Play || MapSession.phase == MapSession.Phase.QuickTest)
        {
            screen = Screen.Playing;
            HideSidePanel();
            if (IsOriginalSelected())
            {
                PlayableMapBuilder.RestoreOriginalBattlefield();
                builder.ClearGenerated();
                PlayableMapBuilder.LinkOriginal(GameManager.Instance);
            }
            else if (MapSession.workingCopy != null)
            {
                ShowGenerated(editing: false);
                builder.BakeNavigation();
            }
            else
            {
                MapSession.phase = MapSession.Phase.Menu;
                MapSession.selectedId = MapSession.OriginalId;
                ShowModeSelect();
            }

            return;
        }

        MapSession.phase = MapSession.Phase.Menu;
        PlayableMapBuilder.RestoreOriginalBattlefield();
        builder.ClearGenerated();
        ShowModeSelect();
    }

    void BeginMatchIfNeeded()
    {
        if (screen != Screen.Playing || GameManager.Instance == null) return;

        PlayableMapBuilder.EnsureGameplayLinks(GameManager.Instance);
        GameManager.Instance.PrepareForRematch();

        if (MapSession.phase == MapSession.Phase.QuickTest)
        {
            GameManager.Instance.SetTestSpeed(MapSession.testSpeed);
            GameManager.Instance.BeginWorkshopTest();
            SetMatchChrome(showReadout: true, showCommands: false);
            return;
        }

        GameManager.Instance.BeginWorkshopPlay(MapSession.chosenFaction);
        SetMatchChrome(showReadout: true, showCommands: true);
    }

    void HideOldFactionMenu()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.factionSelectionUI != null)
        {
            gameManager.factionSelectionUI.SetActive(false);
            return;
        }

        GameObject oldMenu = GameObject.Find("Canvas_FactionSelect");
        if (oldMenu != null) oldMenu.SetActive(false);
    }

    void ShowGenerated(bool editing)
    {
        PlayableMapBuilder.HideOriginalBattlefield();
        builder.Build(MapSession.workingCopy, editing);
        PlayableMapBuilder.FrameCamera(MapSession.workingCopy.WorldBounds());
    }

    bool IsOriginalSelected()
    {
        return MapSession.selectedId == MapSession.OriginalId;
    }

    void HandleDrag()
    {
        if (Mouse.current == null || Camera.main == null || MapSession.workingCopy == null) return;

        if (!string.IsNullOrEmpty(palettePressId) && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            string id = palettePressId;
            palettePressId = null;
            if (!OverEditorPanel())
            {
                if (PlacePalettePiece(id))
                {
                    armedCatalogId = null;
                    if (statusText != null) statusText.text = "Placed " + DecorationCatalog.DisplayName(id) + ". It stays when you dress again.";
                }
                else
                {
                    armedCatalogId = id;
                }
            }
            else if (armedCatalogId == id)
            {
                armedCatalogId = null;
                if (statusText != null) statusText.text = "Placement cancelled.";
            }
            else
            {
                CancelRoadLay(false);
                armedCatalogId = id;
                if (statusText != null) statusText.text = "Click the map to place " + DecorationCatalog.DisplayName(id) + ". Click it again or press Esc to stop.";
            }

            RefreshPaletteHighlight();
            return;
        }

        if (!string.IsNullOrEmpty(armedCatalogId) && Mouse.current.leftButton.wasPressedThisFrame && !PointerOverUi())
        {
            if (PlacePalettePiece(armedCatalogId) && statusText != null)
                statusText.text = "Placed " + DecorationCatalog.DisplayName(armedCatalogId) + ". Click again for another, or press Esc to stop.";
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && !PointerOverUi())
        {
            dragHandle = PickHandle(Mouse.current.position.ReadValue());
            if (dragHandle != null && dragHandle.kind == MapEditHandle.Kind.DecorationRemove)
            {
                RemoveDecorationPiece(dragHandle.decorationId);
                dragHandle = null;
                return;
            }

            if (dragHandle != null && dragHandle.kind == MapEditHandle.Kind.PlacedRoadRemove)
            {
                RemovePlacedRoad(dragHandle.decorationId);
                dragHandle = null;
                return;
            }

            if (layingRoad)
            {
                Vector3 world = GroundPoint(Mouse.current.position.ReadValue());
                if (!roadStartSet)
                {
                    roadStart = world;
                    roadStartSet = true;
                    if (builder != null) builder.SetRoadDraft(true, world);
                    if (statusText != null) statusText.text = "Click the other end. The road stays straight.";
                }
                else if (MapSession.workingCopy.TryAddRoad(roadStart, world))
                {
                    roadStartSet = false;
                    if (builder != null) builder.SetRoadDraft(false, world);
                    MapSession.workingCopyDirty = true;
                    builder.Sync();
                    if (statusText != null) statusText.text = "Road added. Click the next start, or press Esc to stop.";
                }
                else if (statusText != null)
                {
                    statusText.text = "That road is too short. Click a farther end.";
                }

                dragHandle = null;
                return;
            }
        }

        if (Mouse.current.leftButton.isPressed && dragHandle != null)
        {
            Vector3 world = GroundPoint(Mouse.current.position.ReadValue());
            ApplyDrag(dragHandle, world);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) dragHandle = null;
    }

    void ApplyDrag(MapEditHandle handle, Vector3 world)
    {
        PlayableMapDefinition map = MapSession.workingCopy;
        switch (handle.kind)
        {
            case MapEditHandle.Kind.WidthMin:
                map.MoveWidthEdge(true, world.x);
                break;
            case MapEditHandle.Kind.WidthMax:
                map.MoveWidthEdge(false, world.x);
                break;
            case MapEditHandle.Kind.DepthEdge:
                map.MoveDepthEdge(handle.depthEdgeIndex, world.z);
                break;
            case MapEditHandle.Kind.ControlPoint:
                map.sectors[handle.sectorIndex].controlPoints[handle.pointIndex] = map.ClampInside(handle.sectorIndex, world);
                break;
            case MapEditHandle.Kind.AttackerSpawn:
                map.sectors[handle.sectorIndex].attackerSpawn = map.ClampInside(handle.sectorIndex, world);
                break;
            case MapEditHandle.Kind.DefenderSpawn:
                map.sectors[handle.sectorIndex].defenderSpawn = map.ClampInside(handle.sectorIndex, world);
                break;
            case MapEditHandle.Kind.Decoration:
                map.KeepDecoration(handle.decorationId, world);
                break;
        }

        MapSession.workingCopyDirty = true;
        builder.Sync();
    }

    MapEditHandle PickHandle(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 800f);
        float best = float.MaxValue;
        MapEditHandle found = null;
        foreach (RaycastHit hit in hits)
        {
            MapEditHandle handle = hit.collider.GetComponentInParent<MapEditHandle>();
            if (handle != null && hit.distance < best)
            {
                best = hit.distance;
                found = handle;
            }
        }

        return found;
    }

    Vector3 GroundPoint(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance);
        return Vector3.zero;
    }

    bool PointerOverUi()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId);
    }

    bool OverEditorPanel()
    {
        if (sidePanel == null || !sidePanel.activeInHierarchy || Mouse.current == null) return false;
        RectTransform rect = sidePanel.GetComponent<RectTransform>();
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Mouse.current.position.ReadValue(), null);
    }

    void ShowModeSelect()
    {
        screen = Screen.Menu;
        MapSession.phase = MapSession.Phase.Menu;
        MapSession.allowLookAround = false;
        Time.timeScale = 1f;
        SetMatchChrome(showReadout: false, showCommands: false);
        PlayableMapBuilder.HideOriginalBattlefield();
        if (builder != null) builder.ClearGenerated();
        ClearSidePanel();
        AddTitle("Breakthrough");
        AddBody("Play a match as the side you choose, run a sped-up test, or edit a map.");
        AddButton(sidePanel.transform, "Play", ShowPlayFaction);
        AddButton(sidePanel.transform, "Test", ShowTestSetup);
        AddButton(sidePanel.transform, "Edit", ShowEditPick);
        AddSpacer();
        AddBody("These open their own scenes. Main menu on that screen brings you back.");
        AddButton(sidePanel.transform, "ChatGPT Map", () => OpenOutsideScene("GreyboxBattlefield01"));
        AddButton(sidePanel.transform, "Unit Sandbox", () => OpenOutsideScene("UnitSandbox"));
        statusText = AddBody("");
    }

    void ShowPlayFaction()
    {
        screen = Screen.Menu;
        ClearSidePanel();
        AddTitle("Play");
        AddBody("Which side do you want to command? The match starts at normal speed, with tickets and Auto, Assist, and Manual.");
        AddButton(sidePanel.transform, "Attacker", () =>
        {
            MapSession.chosenFaction = Faction.Attacker;
            ShowPlayMaps();
        });
        AddButton(sidePanel.transform, "Defender", () =>
        {
            MapSession.chosenFaction = Faction.Defender;
            ShowPlayMaps();
        });
        AddButton(sidePanel.transform, "Back", ShowModeSelect);
    }

    void ShowPlayMaps()
    {
        screen = Screen.Menu;
        ClearSidePanel();
        string side = MapSession.chosenFaction == Faction.Defender ? "Defender" : "Attacker";
        AddTitle("Play as " + side);
        AddBody("Pick a map. The match starts straight away.");
        AddPlayableMaps(StartPlayOnMap);
        var row = AddRow();
        AddButton(row.transform, "Back", ShowPlayFaction);
    }

    void ShowTestSetup()
    {
        screen = Screen.Menu;
        ClearSidePanel();
        AddTitle("Test");
        AddBody("Choose a speed and how many matches to run, then pick a map. They play themselves, one after another, on that map.");
        AddSpeedStepper();
        AddStepper("Matches", MapSession.testRunCount, 1, 20, value =>
        {
            MapSession.testRunCount = value;
            ShowTestSetup();
        });
        AddSpacer();
        AddBody("Map");
        AddPlayableMaps(StartTestOnMap);
        var row = AddRow();
        AddButton(row.transform, "Back", ShowModeSelect);
    }

    void ShowEditPick()
    {
        screen = Screen.Menu;
        ClearSidePanel();
        AddTitle("Edit");
        AddBody("Create a blank map, or open one you saved. Original stays as it was built.");
        var row = AddRow();
        AddButton(row.transform, "Create new", CreateNewMap);
        AddSpacer();
        bool any = false;
        if (MapSession.workingCopy != null && MapSession.workingCopyDirty)
        {
            any = true;
            AddMapChoice(MapSession.workingCopy.mapName + " (unsaved)", "Still in this session", MapSession.UnsavedId, OpenMapForEdit);
        }

        foreach (string fileName in PlayableMapDefinition.ListSavedFileNames())
        {
            any = true;
            string label = fileName.EndsWith(".json") ? fileName.Substring(0, fileName.Length - 5) : fileName;
            AddMapChoice(label, "Saved map", fileName, OpenMapForEdit);
        }

        if (!any) AddBody("No saved maps yet. Create new starts a blank one.");
        row = AddRow();
        AddButton(row.transform, "Back", ShowModeSelect);
    }

    void CreateNewMap()
    {
        draftName = "Blank Map";
        draftSectorCount = 3;
        draftPoints.Clear();
        draftPoints.Add(2);
        draftPoints.Add(1);
        draftPoints.Add(1);
        MapSession.workingCopy = PlayableMapDefinition.CreateBlank(draftName, draftPoints);
        MapSession.selectedId = MapSession.UnsavedId;
        MapSession.workingCopyDirty = true;
        EnterEditMode();
    }

    void OpenMapForEdit()
    {
        if (IsOriginalSelected()) return;
        if (!LoadSelectionIntoWorkingCopy())
        {
            if (statusText != null) statusText.text = "That map could not be opened.";
            return;
        }

        EnterEditMode();
    }

    void ShowEdit()
    {
        screen = Screen.Edit;
        MapSession.phase = MapSession.Phase.Edit;
        MapSession.allowLookAround = true;
        SyncDraftFromMap();
        SetMatchChrome(showReadout: false, showCommands: false);
        ClearSidePanel();
        PlayableMapDefinition map = MapSession.workingCopy;
        AddTitle(map != null ? map.mapName : "Edit map");
        AddBody("Drag borders, gold points, and the red or blue spawns. Population sets how many pieces each sector tries to place. Click a piece in the list, then click the map to place it. Lay road uses two clicks and keeps the road straight. The centre road stays. A red sphere deletes a road you added. Press Esc to stop placing.");
        AddStepper("Sectors", draftSectorCount, 1, 6, SetEditSectorCount);
        for (int i = 0; i < draftSectorCount && i < draftPoints.Count; i++)
        {
            int index = i;
            AddStepper("Sector " + (char)('A' + i) + " points", draftPoints[i], 1, 4, value => SetEditPointCount(index, value));
        }

        int density = map != null ? map.decorationDensity : 8;
        AddDensitySlider(density);
        AddPalette();

        var row = AddRow();
        AddButton(row.transform, "Auto centre all", CentreAll);
        AddButton(row.transform, "Dress again", DressMapAgain);
        AddButton(row.transform, "Lay road", ToggleLayRoad);
        row = AddRow();
        AddButton(row.transform, "Save", ShowNameAndSave);
        AddButton(row.transform, "Play", PlayWorkingCopy);
        row = AddRow();
        AddButton(row.transform, "Quick Test", QuickTestWorkingCopy);
        AddButton(row.transform, "Main menu", ReturnToMenu);
        statusText = AddBody(MapSession.workingCopyDirty ? "Unsaved changes." : "Saved maps stay in the menu.");
    }

    void ShowNameAndSave()
    {
        if (MapSession.workingCopy == null) return;
        screen = Screen.SaveName;
        string current = string.IsNullOrWhiteSpace(MapSession.workingCopy.mapName) ? "Blank Map" : MapSession.workingCopy.mapName;
        ClearSidePanel();
        AddTitle("Save map");
        AddBody("This name is how you will find the map later, under Edit, Play, and Test. Saving with a name you already used replaces that map. Your dragged layout is kept.");
        nameInput = AddInput(current);
        if (nameInput != null) nameInput.ActivateInputField();
        var row = AddRow();
        AddButton(row.transform, "Save", ConfirmSaveName);
        AddButton(row.transform, "Back", ShowEdit);
        statusText = AddBody("");
    }

    void ConfirmSaveName()
    {
        if (MapSession.workingCopy == null) return;
        string next = nameInput != null && nameInput.text != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(next))
        {
            if (statusText != null) statusText.text = "Type a name first.";
            return;
        }

        MapSession.workingCopy.mapName = next;
        string fileName = MapSession.workingCopy.Save();
        MapSession.selectedId = fileName;
        MapSession.workingCopyDirty = false;
        ShowEdit();
        if (statusText != null) statusText.text = "Saved as " + next + ". Open it later from Edit.";
    }

    void EnterEditMode()
    {
        MapSession.continueTest = false;
        MapSession.phase = MapSession.Phase.Edit;
        MapSession.allowLookAround = true;
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PrepareForRematch();
            GameManager.Instance.SetAutoTestMode(false);
        }

        ShowGenerated(true);
        ShowEdit();
    }

    void StartPlayOnMap()
    {
        if (!PrepareSelectedMap()) return;
        queuedTestRuns = 0;
        StartMatch(quickTest: false);
    }

    void StartTestOnMap()
    {
        if (!PrepareSelectedMap()) return;
        queuedTestRuns = Mathf.Max(1, MapSession.testRunCount);
        StartMatch(quickTest: true);
    }

    bool PrepareSelectedMap()
    {
        if (IsOriginalSelected()) return true;
        if (LoadSelectionIntoWorkingCopy()) return true;
        if (statusText != null) statusText.text = "Choose a map first.";
        return false;
    }

    void PlayWorkingCopy()
    {
        CancelRoadLay(false);
        CommitName();
        if (MapSession.workingCopy == null) return;
        if (MapSession.selectedId == MapSession.OriginalId) MapSession.selectedId = MapSession.UnsavedId;
        queuedTestRuns = 0;
        StartMatch(quickTest: false);
    }

    void QuickTestWorkingCopy()
    {
        CancelRoadLay(false);
        CommitName();
        if (MapSession.workingCopy == null) return;
        if (MapSession.selectedId == MapSession.OriginalId) MapSession.selectedId = MapSession.UnsavedId;
        queuedTestRuns = 1;
        StartMatch(quickTest: true);
    }

    void StartMatch(bool quickTest)
    {
        Time.timeScale = 1f;
        MapSession.continueTest = false;
        MapSession.phase = quickTest ? MapSession.Phase.QuickTest : MapSession.Phase.Play;
        MapSession.returnAfterMatch = false;
        MapSession.allowLookAround = false;
        if (quickTest)
        {
            int runs = queuedTestRuns > 0 ? queuedTestRuns : Mathf.Max(1, MapSession.testRunCount);
            queuedTestRuns = 0;
            MapSession.activeTestRuns = runs;
            MapSession.testRunsFinished = 0;
        }
        else
        {
            MapSession.activeTestRuns = 0;
            MapSession.testRunsFinished = 0;
        }

        screen = Screen.Playing;
        if (postMatchPanel != null)
        {
            Destroy(postMatchPanel);
            postMatchPanel = null;
        }

        HideSidePanel();

        if (IsOriginalSelected())
        {
            PlayableMapBuilder.RestoreOriginalBattlefield();
            builder.ClearGenerated();
            PlayableMapBuilder.LinkOriginal(GameManager.Instance);
        }
        else if (MapSession.workingCopy != null)
        {
            ShowGenerated(editing: false);
            builder.BakeNavigation();
        }
        else
        {
            ShowModeSelect();
            return;
        }

        BeginMatchIfNeeded();
    }

    bool LoadSelectionIntoWorkingCopy()
    {
        if (MapSession.selectedId == MapSession.UnsavedId) return MapSession.workingCopy != null;

        if (MapSession.workingCopy != null && MapSession.workingCopyDirty)
        {
            string workingFile = PlayableMapDefinition.FileNameFor(MapSession.workingCopy.mapName);
            if (MapSession.selectedId == workingFile) return true;
        }

        PlayableMapDefinition loaded = PlayableMapDefinition.Load(MapSession.selectedId);
        if (loaded == null) return false;
        MapSession.workingCopy = loaded;
        MapSession.workingCopyDirty = false;
        return true;
    }

    void ReturnToMenu()
    {
        CancelRoadLay(false);
        Time.timeScale = 1f;
        MapSession.phase = MapSession.Phase.Menu;
        MapSession.returnAfterMatch = false;
        MapSession.continueTest = false;
        MapSession.allowLookAround = true;
        MapSession.activeTestRuns = 0;
        TestDashboardOverlay.CurrentMatchNumber = 0;
        TestDashboardOverlay.TargetMatchCount = 0;
        if (postMatchPanel != null)
        {
            Destroy(postMatchPanel);
            postMatchPanel = null;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PrepareForRematch();
            GameManager.Instance.SetAutoTestMode(false);
        }

        PlayableMapBuilder.RestoreOriginalBattlefield();
        if (builder != null) builder.ClearGenerated();
        ShowModeSelect();
    }

    void ShowPostMatch()
    {
        if (canvasObject == null) return;

        postMatchPanel = new GameObject("Post Match");
        postMatchPanel.transform.SetParent(canvasObject.transform, false);
        Image image = postMatchPanel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);
        RectTransform rect = postMatchPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(560f, 88f);
        rect.anchoredPosition = new Vector2(0f, 24f);

        HorizontalLayoutGroup row = postMatchPanel.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(12, 12, 12, 12);
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        if (!IsOriginalSelected() && MapSession.workingCopy != null)
        {
            AddButton(postMatchPanel.transform, "Edit this map", BackToEdit);
        }

        AddButton(postMatchPanel.transform, "Main menu", ReturnToMenu);
    }

    void BackToEdit()
    {
        if (postMatchPanel != null)
        {
            Destroy(postMatchPanel);
            postMatchPanel = null;
        }

        TestDashboardOverlay.CurrentMatchNumber = 0;
        TestDashboardOverlay.TargetMatchCount = 0;
        MapSession.returnAfterMatch = false;
        MapSession.continueTest = false;
        MapSession.activeTestRuns = 0;
        if (MapSession.workingCopy == null && !IsOriginalSelected())
        {
            MapSession.workingCopy = PlayableMapDefinition.Load(MapSession.selectedId);
        }

        if (MapSession.workingCopy == null || IsOriginalSelected())
        {
            ReturnToMenu();
            return;
        }

        EnterEditMode();
    }

    void ContinueTestRun()
    {
        if (postMatchPanel != null)
        {
            Destroy(postMatchPanel);
            postMatchPanel = null;
        }

        screen = Screen.Playing;
        HideSidePanel();
        if (UIManager.Instance != null) UIManager.Instance.HideGameOver();
        if (GameManager.Instance == null) return;

        PlayableMapBuilder.EnsureGameplayLinks(GameManager.Instance);
        GameManager.Instance.PrepareForRematch();
        GameManager.Instance.SetTestSpeed(MapSession.testSpeed);
        GameManager.Instance.BeginWorkshopTest();
        SetMatchChrome(showReadout: true, showCommands: false);
    }

    void SyncDraftFromMap()
    {
        PlayableMapDefinition map = MapSession.workingCopy;
        if (map == null || map.sectors == null || map.sectors.Count == 0) return;

        draftName = map.mapName;
        draftSectorCount = Mathf.Clamp(map.sectors.Count, 1, 6);
        draftPoints.Clear();
        for (int i = 0; i < draftSectorCount; i++)
        {
            int points = map.sectors[i].controlPoints != null ? map.sectors[i].controlPoints.Count : 1;
            draftPoints.Add(Mathf.Clamp(Mathf.Max(1, points), 1, 4));
        }
    }

    void SetEditSectorCount(int count)
    {
        CommitName();
        draftSectorCount = Mathf.Clamp(count, 1, 6);
        while (draftPoints.Count < draftSectorCount) draftPoints.Add(1);
        while (draftPoints.Count > draftSectorCount) draftPoints.RemoveAt(draftPoints.Count - 1);
        RebuildLayoutFromCounts();
    }

    void SetEditPointCount(int index, int value)
    {
        CommitName();
        if (index >= 0 && index < draftPoints.Count) draftPoints[index] = Mathf.Clamp(value, 1, 4);
        RebuildLayoutFromCounts();
    }

    void RebuildLayoutFromCounts()
    {
        if (MapSession.workingCopy == null) return;
        CommitName();
        string name = MapSession.workingCopy.mapName;
        MapSession.workingCopy = PlayableMapDefinition.CreateBlank(name, draftPoints);
        MapSession.workingCopyDirty = true;
        if (MapSession.selectedId == MapSession.OriginalId) MapSession.selectedId = MapSession.UnsavedId;
        ShowGenerated(true);
        ShowEdit();
    }

    void CentreAll()
    {
        if (MapSession.workingCopy == null) return;
        CommitName();
        MapSession.workingCopy.CentreContents();
        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        if (statusText != null) statusText.text = "Spawns and control points recentred. Sector sizes stayed as they were.";
    }

    void ApplyDecorationDensity(int density)
    {
        PlayableMapDefinition map = MapSession.workingCopy;
        if (map == null || map.decorationDensity == density) return;
        map.SetDecorationDensity(density);
        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        if (statusText != null) statusText.text = "Population " + density + " per sector. Pieces you moved or placed stay put. Save keeps this level.";
    }

    bool PlacePalettePiece(string catalogId)
    {
        PlayableMapDefinition map = MapSession.workingCopy;
        if (map == null || Mouse.current == null) return false;
        Vector3 world = GroundPoint(Mouse.current.position.ReadValue());
        if (!map.TryPlaceKeptDecoration(catalogId, world))
        {
            if (statusText != null) statusText.text = "Drop the piece inside the map.";
            return false;
        }

        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        return true;
    }

    void DressMapAgain()
    {
        if (MapSession.workingCopy == null) return;
        CommitName();
        MapSession.workingCopy.DressAgain();
        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        if (statusText != null) statusText.text = "New dressing placed. Pieces you already moved stay put, and deleted spots stay empty.";
    }

    void RemoveDecorationPiece(string pieceId)
    {
        if (MapSession.workingCopy == null) return;
        MapSession.workingCopy.RemoveDecoration(pieceId);
        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        if (statusText != null) statusText.text = "Piece removed. Dress again will not put one back on that spot.";
    }

    void ToggleLayRoad()
    {
        if (layingRoad)
        {
            CancelRoadLay(true);
            return;
        }

        layingRoad = true;
        roadStartSet = false;
        armedCatalogId = null;
        palettePressId = null;
        RefreshPaletteHighlight();
        if (statusText != null) statusText.text = "Click one end of the road, then the other. It stays straight. Esc stops.";
    }

    void CancelRoadLay(bool announce)
    {
        layingRoad = false;
        roadStartSet = false;
        if (builder != null) builder.SetRoadDraft(false, Vector3.zero);
        if (announce && statusText != null) statusText.text = "Road laying cancelled.";
    }

    void RemovePlacedRoad(string roadId)
    {
        if (MapSession.workingCopy == null) return;
        MapSession.workingCopy.RemoveRoad(roadId);
        MapSession.workingCopyDirty = true;
        if (builder != null) builder.Sync();
        if (statusText != null) statusText.text = "Road removed. The centre road stays.";
    }

    void SetMatchChrome(bool showReadout, bool showCommands)
    {
        if (UIManager.Instance != null) UIManager.Instance.SetMatchReadoutVisible(showReadout);
        ControlModeHUD commandHud = FindAnyObjectByType<ControlModeHUD>(FindObjectsInactive.Include);
        if (commandHud != null) commandHud.SetVisible(showCommands);
        if (showCommands) StoreManager.ShowForCurrentMatch();
        else StoreManager.Hide();
        if (ControlModeManager.Instance == null) return;
        if (showCommands) ControlModeManager.Instance.SetMode(ControlMode.Assist);
        else if (showReadout) ControlModeManager.Instance.SetMode(ControlMode.Auto);
    }

    void AddPlayableMaps(UnityEngine.Events.UnityAction onPick)
    {
        AddMapChoice("Original", "The built-in battlefield", MapSession.OriginalId, onPick);
        if (MapSession.workingCopy != null && MapSession.workingCopyDirty)
        {
            AddMapChoice(MapSession.workingCopy.mapName + " (unsaved)", "Still in this session", MapSession.UnsavedId, onPick);
        }

        foreach (string fileName in PlayableMapDefinition.ListSavedFileNames())
        {
            string label = fileName.EndsWith(".json") ? fileName.Substring(0, fileName.Length - 5) : fileName;
            AddMapChoice(label, "Saved map", fileName, onPick);
        }
    }

    void AddSpeedStepper()
    {
        int index = 0;
        float bestGap = float.MaxValue;
        for (int i = 0; i < TestSpeeds.Length; i++)
        {
            float gap = Mathf.Abs(TestSpeeds[i] - MapSession.testSpeed);
            if (gap < bestGap)
            {
                bestGap = gap;
                index = i;
            }
        }

        GameObject row = AddRow();
        AddTextOn(row.transform, "Speed", 18);
        AddButton(row.transform, "-", () =>
        {
            MapSession.testSpeed = TestSpeeds[Mathf.Max(0, index - 1)];
            ShowTestSetup();
        });
        AddTextOn(row.transform, TestSpeeds[index].ToString("0") + "x", 20);
        AddButton(row.transform, "+", () =>
        {
            MapSession.testSpeed = TestSpeeds[Mathf.Min(TestSpeeds.Length - 1, index + 1)];
            ShowTestSetup();
        });
    }

    void CommitName()
    {
        if (nameInput == null || MapSession.workingCopy == null) return;
        string next = nameInput.text == null ? "" : nameInput.text.Trim();
        if (string.IsNullOrEmpty(next)) return;
        if (next == MapSession.workingCopy.mapName) return;
        MapSession.workingCopy.mapName = next;
        MapSession.workingCopyDirty = true;
    }

    void OpenOutsideScene(string sceneName)
    {
        Time.timeScale = 1f;
        MapSession.phase = MapSession.Phase.Menu;
        MapSession.allowLookAround = false;
        SceneManager.LoadScene(sceneName);
    }

    void CreateCanvas()
    {
        canvasObject = new GameObject("MapWorkshopCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        backdrop = new GameObject("Menu Backdrop");
        backdrop.transform.SetParent(canvasObject.transform, false);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = true;
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        sidePanel = new GameObject("Menu Rows");
        sidePanel.transform.SetParent(canvasObject.transform, false);
        Image image = sidePanel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);
        RectTransform rect = sidePanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(420f, -24f);
        rect.anchoredPosition = new Vector2(12f, 0f);

        VerticalLayoutGroup layout = sidePanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        sidePanel.AddComponent<ContentSizeFitter>();
        ApplyMenuLayout();
    }

    void HideSidePanel()
    {
        if (sidePanel != null) sidePanel.SetActive(false);
        ApplyMenuLayout();
    }

    void ClearSidePanel()
    {
        if (sidePanel == null) return;
        sidePanel.SetActive(true);
        for (int i = sidePanel.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(sidePanel.transform.GetChild(i).gameObject);
        }

        nameInput = null;
        statusText = null;
        paletteChoices.Clear();
        ApplyMenuLayout();
    }

    void ApplyMenuLayout()
    {
        bool menu = screen == Screen.Menu;
        if (backdrop != null) backdrop.SetActive(menu);
        if (sidePanel == null) return;

        Image image = sidePanel.GetComponent<Image>();
        RectTransform rect = sidePanel.GetComponent<RectTransform>();
        ContentSizeFitter fitter = sidePanel.GetComponent<ContentSizeFitter>();
        if (menu)
        {
            if (image != null) image.color = new Color(0f, 0f, 0f, 0f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 0f);
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
        else
        {
            if (image != null) image.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(420f, -24f);
            rect.anchoredPosition = new Vector2(12f, 0f);
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }
    }

    void AddTitle(string text)
    {
        TextAnchor anchor = screen == Screen.Menu ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        Text label = AddText(text, 26, FontStyle.Bold, anchor);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 40f;
        element.preferredHeight = 40f;
    }

    Text AddBody(string text)
    {
        TextAnchor anchor = screen == Screen.Menu ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
        Text label = AddText(text, 16, FontStyle.Normal, anchor);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 64f;
        element.preferredHeight = 72f;
        return label;
    }

    Text AddText(string value, int size, FontStyle style, TextAnchor anchor)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(sidePanel.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    void AddMapChoice(string label, string caption, string id, UnityEngine.Events.UnityAction onPick)
    {
        Button button = AddButton(sidePanel.transform, label, () =>
        {
            MapSession.selectedId = id;
            if (onPick != null) onPick.Invoke();
        });
        ColorBlock colors = button.colors;
        Color baseColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor * 1.1f;
        colors.selectedColor = baseColor;
        button.colors = colors;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = baseColor;
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
        }

        if (!string.IsNullOrEmpty(caption))
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label + "\n<size=14>" + caption + "</size>";
        }
    }

    void AddPalette()
    {
        paletteChoices.Clear();
        Text heading = AddTextOn(sidePanel.transform, "Pieces", 18);
        heading.alignment = TextAnchor.MiddleLeft;

        GameObject scrollObject = new GameObject("Palette");
        scrollObject.transform.SetParent(sidePanel.transform, false);
        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.minHeight = 168f;
        scrollLayout.preferredHeight = 168f;
        scrollLayout.flexibleHeight = 0f;
        Image background = scrollObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.12f, 0.15f, 1f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(6f, 6f);
        viewportRect.offsetMax = new Vector2(-6f, -6f);
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = viewportRect;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRect;

        string lastGroup = null;
        for (int i = 0; i < DecorationCatalog.Entries.Length; i++)
        {
            DecorationCatalog.Entry entry = DecorationCatalog.Entries[i];
            if (entry.groupName != lastGroup)
            {
                lastGroup = entry.groupName;
                Text header = AddTextOn(content.transform, entry.groupName, 15);
                header.alignment = TextAnchor.MiddleLeft;
                LayoutElement headerLayout = header.GetComponent<LayoutElement>();
                if (headerLayout != null) headerLayout.minHeight = 22f;
            }

            AddPaletteButton(content.transform, entry);
        }
    }

    void AddPaletteButton(Transform parent, DecorationCatalog.Entry entry)
    {
        GameObject buttonObject = new GameObject(entry.displayName);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = PaletteColor(entry.id);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 32f;
        layout.preferredHeight = 32f;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.text = entry.displayName;
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        Stretch(textObject, 10f);

        string id = entry.id;
        EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => palettePressId = id);
        trigger.triggers.Add(down);
        paletteChoices.Add(new PaletteChoice { id = id, image = image });
    }

    void RefreshPaletteHighlight()
    {
        for (int i = 0; i < paletteChoices.Count; i++)
        {
            if (paletteChoices[i].image != null)
                paletteChoices[i].image.color = PaletteColor(paletteChoices[i].id);
        }
    }

    Color PaletteColor(string id)
    {
        if (id == armedCatalogId) return new Color(0.55f, 0.48f, 0.22f, 1f);
        return new Color(0.18f, 0.22f, 0.28f, 1f);
    }

    class PaletteChoice
    {
        public string id;
        public Image image;
    }

    void AddDensitySlider(int value)
    {
        GameObject row = AddRow();
        Text readout = AddTextOn(row.transform, "Population " + value, 18);
        if (readout != null)
        {
            LayoutElement readoutLayout = readout.GetComponent<LayoutElement>();
            if (readoutLayout != null)
            {
                readoutLayout.minWidth = 128f;
                readoutLayout.preferredWidth = 140f;
                readoutLayout.flexibleWidth = 0f;
            }
        }

        GameObject sliderObject = new GameObject("Population");
        sliderObject.transform.SetParent(row.transform, false);
        LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
        layout.minWidth = 140f;
        layout.flexibleWidth = 1f;
        layout.minHeight = 28f;
        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.14f, 0.18f, 1f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.45f, 0.55f, 0.32f, 1f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(sliderObject.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 24f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.95f, 0.85f, 0.4f, 1f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = PlayableMapDefinition.MinDecorationDensity;
        slider.maxValue = PlayableMapDefinition.MaxDecorationDensity;
        slider.wholeNumbers = true;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
        slider.onValueChanged.AddListener(newValue =>
        {
            int density = Mathf.RoundToInt(newValue);
            if (readout != null) readout.text = "Population " + density;
            ApplyDecorationDensity(density);
        });
    }

    void AddStepper(string label, int value, int min, int max, System.Action<int> changed)
    {
        GameObject row = AddRow();
        AddTextOn(row.transform, label, 18);
        AddButton(row.transform, "-", () => changed(Mathf.Max(min, value - 1)));
        AddTextOn(row.transform, value.ToString(), 20);
        AddButton(row.transform, "+", () => changed(Mathf.Min(max, value + 1)));
    }

    GameObject AddRow()
    {
        GameObject row = new GameObject("Row");
        row.transform.SetParent(sidePanel.transform, false);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        LayoutElement element = row.AddComponent<LayoutElement>();
        element.minHeight = 40f;
        element.preferredHeight = 40f;
        return row;
    }

    void AddSpacer()
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(sidePanel.transform, false);
        LayoutElement element = spacer.AddComponent<LayoutElement>();
        element.minHeight = 8f;
        element.preferredHeight = 8f;
    }

    InputField AddInput(string value)
    {
        GameObject inputObject = new GameObject("Name Input");
        inputObject.transform.SetParent(sidePanel.transform, false);
        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        LayoutElement element = inputObject.AddComponent<LayoutElement>();
        element.minHeight = 40f;
        element.preferredHeight = 40f;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(inputObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.color = Color.white;
        text.fontSize = 18;
        text.supportRichText = false;
        text.alignment = TextAnchor.MiddleLeft;
        Stretch(textObject, 10f);

        InputField input = inputObject.AddComponent<InputField>();
        input.textComponent = text;
        input.targetGraphic = image;
        input.text = value;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        LayoutElement element = buttonObject.AddComponent<LayoutElement>();
        element.minHeight = 40f;
        element.preferredHeight = 40f;
        element.minWidth = 72f;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.text = label;
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(textObject, 6f);
        return button;
    }

    Text AddTextOn(Transform parent, string value, int size)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        LayoutElement element = textObject.AddComponent<LayoutElement>();
        element.minWidth = 36f;
        element.preferredWidth = 120f;
        return text;
    }

    static void Stretch(GameObject target, float padding)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, 4f);
        rect.offsetMax = new Vector2(-padding, -4f);
    }

    static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
