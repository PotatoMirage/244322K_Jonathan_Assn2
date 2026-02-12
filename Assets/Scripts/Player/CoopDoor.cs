using Unity.Netcode;
using UnityEngine;

public class CoopDoor : NetworkBehaviour
{
    [Header("References")]
    public CoopButton button1;
    public CoopButton button2;
    public Transform doorModel;

    [Header("Settings")]
    public float rotateSpeed = 2.0f;
    public Vector3 openRotation = new Vector3(0, -100, 0);

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private NetworkVariable<bool> isDoorOpen = new NetworkVariable<bool>(false);

    private void Awake()
    {
        if (doorModel != null)
        {
            closedRotation = doorModel.localRotation;
            targetRotation = closedRotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (button1 != null) button1.IsPressed.OnValueChanged += CheckDoorCondition;
            if (button2 != null) button2.IsPressed.OnValueChanged += CheckDoorCondition;
        }
        isDoorOpen.OnValueChanged += OnDoorStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (button1 != null) button1.IsPressed.OnValueChanged -= CheckDoorCondition;
            if (button2 != null) button2.IsPressed.OnValueChanged -= CheckDoorCondition;
        }
        isDoorOpen.OnValueChanged -= OnDoorStateChanged;
    }

    private void CheckDoorCondition(bool prev, bool current)
    {
        if (!IsServer) return;
        bool b1 = button1 != null && button1.IsPressed.Value;
        bool b2 = button2 != null && button2.IsPressed.Value;
        isDoorOpen.Value = (b1 && b2);
    }

    private void OnDoorStateChanged(bool prev, bool isOpen)
    {
        if (doorModel == null) return;
        targetRotation = isOpen ? closedRotation * Quaternion.Euler(openRotation) : closedRotation;
    }

    private void Update()
    {
        if (doorModel != null)
        {
            doorModel.localRotation = Quaternion.Slerp(doorModel.localRotation, targetRotation, Time.deltaTime * rotateSpeed);
        }
    }
}