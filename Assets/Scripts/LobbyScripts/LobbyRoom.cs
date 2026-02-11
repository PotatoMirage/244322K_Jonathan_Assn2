using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UIElements;

public class LobbyRoom : NetworkBehaviour
{
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>();

    public NetworkVariable<FixedString64Bytes> LobbyCode = new NetworkVariable<FixedString64Bytes>();

    private VisualElement roomContainer;
    private ScrollView playerList;
    private Button startButton;
    private Button readyButton;
    private Label codeLabel;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentSession != null)
            {
                string code = LobbyManager.Instance.CurrentSession.Code;
                LobbyCode.Value = code; 
                Debug.Log($"Host set Lobby Code to: {code}");
            }
        }

        LobbyCode.OnValueChanged += (oldVal, newVal) => {
            UpdateCodeUI(newVal.ToString());
        };

        if (IsOwner)
        {
            SetNameServerRpc(AuthenticationService.Instance.PlayerName);
            var uiDoc = FindAnyObjectByType<UIDocument>();
            if (uiDoc != null) CreateRoomUI(uiDoc.rootVisualElement);

            UpdateCodeUI(LobbyCode.Value.ToString());
        }
    }

    private void UpdateCodeUI(string code)
    {
        if (codeLabel != null)
        {
            if (string.IsNullOrEmpty(code) || code == "0")
                codeLabel.text = "Code: Loading...";
            else
                codeLabel.text = $"Lobby Code: {code}";
        }
    }

    [ServerRpc] void SetNameServerRpc(string name) => PlayerName.Value = name;
    [ServerRpc] void ToggleReadyServerRpc() => IsReady.Value = !IsReady.Value;

    void CreateRoomUI(VisualElement root)
    {
        roomContainer = new VisualElement();
        roomContainer.style.position = Position.Absolute;
        roomContainer.style.right = 20; roomContainer.style.top = 20; roomContainer.style.bottom = 20;
        roomContainer.style.width = 250;
        roomContainer.style.backgroundColor = new Color(0, 0, 0, 0.9f);
        roomContainer.style.paddingTop = 10;
        roomContainer.style.paddingBottom = 10;
        roomContainer.style.paddingLeft = 10;
        roomContainer.style.paddingRight = 10;
        root.Add(roomContainer);

        codeLabel = new Label("Code: Loading...");
        codeLabel.style.fontSize = 20;
        codeLabel.style.color = Color.yellow;
        codeLabel.style.marginBottom = 10;
        roomContainer.Add(codeLabel);

        playerList = new ScrollView();
        playerList.style.flexGrow = 1;
        roomContainer.Add(playerList);

        readyButton = new Button(() => ToggleReadyServerRpc())
        {
            text = "READY", style = { height = 40 }
        };
        roomContainer.Add(readyButton);

        roomContainer.Add(new Button(() => {
            if (LobbyManager.Instance != null) LobbyManager.Instance.LeaveGame();
            else NetworkManager.Singleton.Shutdown();
        })
        { text = "LEAVE", style = { height = 30, backgroundColor = Color.red, marginTop = 5 } });

        if (IsServer)
        {
            startButton = new Button(() => {
                NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            })
            {
                text = "START GAME", style ={ height = 40, backgroundColor = Color.green, display = DisplayStyle.None, marginTop = 10 }
            };
            roomContainer.Add(startButton);
        }
    }

    private float refreshTimer = 0;
    void Update()
    {
        if (!IsOwner || roomContainer == null) return;

        if (IsServer)
        {
            if (LobbyCode.Value.IsEmpty && LobbyManager.Instance != null && LobbyManager.Instance.CurrentSession != null)
            {
                LobbyCode.Value = LobbyManager.Instance.CurrentSession.Code;
            }
        }

        if (!LobbyCode.Value.IsEmpty)
        {
            UpdateCodeUI(LobbyCode.Value.ToString());
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer > 0.5f)
        {
            refreshTimer = 0;
            RefreshPlayers();
        }
    }

    void RefreshPlayers()
    {
        playerList.Clear();
        bool allReady = true;
        int count = 0;

        var players = FindObjectsByType<LobbyRoom>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            count++;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            row.Add(new Label(p.PlayerName.Value.ToString()) { style = { color = Color.white } });
            row.Add(new Label(p.IsReady.Value ? "READY" : "...") { style = { color = p.IsReady.Value ? Color.green : Color.red } });
            playerList.Add(row);

            if (p.IsOwner) readyButton.style.backgroundColor = p.IsReady.Value ? Color.green : Color.gray;
            if (!p.IsReady.Value) allReady = false;
        }

        if (IsServer && startButton != null)
        {
            startButton.style.display = (allReady && count > 0) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (roomContainer != null) roomContainer.RemoveFromHierarchy();
    }
}