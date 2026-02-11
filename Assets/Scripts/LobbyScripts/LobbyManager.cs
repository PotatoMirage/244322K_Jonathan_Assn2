using System;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    public ISession CurrentSession { get; private set; }

    public UIDocument uiDocument;

    private VisualElement root;
    private VisualElement mainMenuContainer;
    private VisualElement browserContainer;

    private TextField nameInput;
    private TextField joinCodeInput;
    private Label statusLabel;
    private ScrollView sessionScrollView;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null) root = uiDocument.rootVisualElement;

        CreateMenuUI();

        SceneManager.sceneLoaded += OnSceneLoaded;

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            UpdateStatus($"Signed in as {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            UpdateStatus("Init Error: " + e.Message);
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientStopped += OnNetworkShutdown;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientStopped -= OnNetworkShutdown;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (root == null) return;

        if (scene.name == "Lobby")
        {
            root.style.display = DisplayStyle.Flex;
            CloseBrowser();
        }
        else
        {
            root.style.display = DisplayStyle.None;
        }
    }

    public void OpenBrowser()
    {
        mainMenuContainer.style.display = DisplayStyle.None;
        browserContainer.style.display = DisplayStyle.Flex;
        RefreshLobbies();
    }
    public void CloseBrowser()
    {
        browserContainer.style.display = DisplayStyle.None;
        mainMenuContainer.style.display = DisplayStyle.Flex;
    }

    public async void CreateGame()
    {
        if (string.IsNullOrEmpty(nameInput.value)) return;
        UpdateStatus("Creating...");
        await AuthenticationService.Instance.UpdatePlayerNameAsync(nameInput.value);

        SessionOptions options = new SessionOptions { Name = $"{nameInput.value}'s Lobby", MaxPlayers = 4 }.WithRelayNetwork();

        try { CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options); }
        catch (Exception e) { UpdateStatus("Create Failed: " + e.Message); }
    }

    public async void QuickJoin()
    {
        UpdateStatus("Quick Joining...");
        await AuthenticationService.Instance.UpdatePlayerNameAsync(nameInput.value);

        QuickJoinOptions quickOptions = new();
        SessionOptions sessionOptions = new SessionOptions { Name = "QuickMatch", MaxPlayers = 4 }.WithRelayNetwork();

        try { CurrentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickOptions, sessionOptions); }
        catch (Exception e) { UpdateStatus("Quick Join Failed: " + e.Message); }
    }

    public async void RefreshLobbies()
    {
        UpdateStatus("Searching...");
        sessionScrollView.Clear();
        try
        {
            var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
            foreach (ISessionInfo session in results.Sessions)
            {
                sessionScrollView.Add(new Button(async () => {
                    UpdateStatus($"Joining {session.Name}...");
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(nameInput.value);
                    CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);
                })
                { text = $"{session.Name} ({session.AvailableSlots} Slots)", style = { height = 40, marginBottom = 5 } });
            }
        }
        catch (Exception e) { UpdateStatus("Error: " + e.Message); }
    }

    public async void JoinByCode()
    {
        if (string.IsNullOrEmpty(joinCodeInput.value)) return;
        UpdateStatus("Joining...");
        await AuthenticationService.Instance.UpdatePlayerNameAsync(nameInput.value);
        try
        {
            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCodeInput.value);
        }
        catch (Exception e)
        {
            UpdateStatus("Error: " + e.Message);
        }
    }

    public async void LeaveGame()
    {
        NetworkManager.Singleton.Shutdown();
        if (CurrentSession != null)
        {
            await CurrentSession.LeaveAsync();
            CurrentSession = null;
        }
        if (SceneManager.GetActiveScene().name != "Lobby") SceneManager.LoadScene("Lobby");
    }

    private void OnNetworkShutdown(bool wasHost) { UpdateStatus("Disconnected."); }

    void CreateMenuUI()
    {
        if (root == null) return;
        root.Clear();
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        root.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        mainMenuContainer = new VisualElement();
        mainMenuContainer.style.width = 350;
        root.Add(mainMenuContainer);

        statusLabel = new Label("Initializing...")
        {
            style = { color = Color.white, marginBottom = 10, alignSelf = Align.Center}
        };
        mainMenuContainer.Add(statusLabel);

        nameInput = new TextField("Player Name")
        {
            value = "Player" + UnityEngine.Random.Range(100, 999)
        };
        mainMenuContainer.Add(nameInput);

        mainMenuContainer.Add(new Button(CreateGame)
        {
            text = "Create Lobby (Host)", style = { height = 45, marginTop = 15, backgroundColor = new Color(0.2f, 0.6f, 0.2f) }
        });

        mainMenuContainer.Add(new Label("--- JOIN ---")
        {
            style = { marginTop = 20, color = Color.yellow, alignSelf = Align.Center }
        });

        joinCodeInput = new TextField("Lobby Code"); mainMenuContainer.Add(joinCodeInput);
        mainMenuContainer.Add(new Button(JoinByCode)
        {
            text = "Join by Code", style = { height = 30 }
        });
        mainMenuContainer.Add(new Button(QuickJoin)
        {
            text = "Quick Join (Random)", style = { height = 40, marginTop = 10, backgroundColor = new Color(0.2f, 0.4f, 0.8f) }
        });
        mainMenuContainer.Add(new Button(OpenBrowser)
        {
            text = "Open Server Browser", style = { height = 40, marginTop = 5 }
        });

        browserContainer = new VisualElement();
        browserContainer.style.width = 400;
        browserContainer.style.height = 500;
        browserContainer.style.backgroundColor = new Color(0, 0, 0, 0.5f);
        browserContainer.style.paddingTop = 20;
        browserContainer.style.paddingBottom = 20;
        browserContainer.style.paddingLeft = 20;
        browserContainer.style.paddingRight = 20;
        browserContainer.style.display = DisplayStyle.None;
        root.Add(browserContainer);
        browserContainer.Add(new Label("SERVER BROWSER")
        { style = { fontSize = 24, color = Color.white, marginBottom = 10, alignSelf = Align.Center }
        });

        sessionScrollView = new ScrollView();
        sessionScrollView.style.flexGrow = 1;
        sessionScrollView.style.backgroundColor = new Color(0, 0, 0, 0.3f);
        browserContainer.Add(sessionScrollView);

        var row = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 10, justifyContent = Justify.SpaceBetween }
        };
        row.Add(new Button(RefreshLobbies)
        {
            text = "Refresh", style = { width = 100, height = 40 }
        });

        row.Add(new Button(CloseBrowser)
        { text = "Back", style = { width = 100, height = 40, backgroundColor = Color.red }
        });
        browserContainer.Add(row);
    }
    private void UpdateStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }
}