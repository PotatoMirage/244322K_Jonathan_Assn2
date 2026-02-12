using UnityEngine;

public class FloatingEmote : MonoBehaviour
{
    public float lifeTime = 2.0f;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        Destroy(gameObject, lifeTime);
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // Face the camera
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}