using UnityEngine;

public class FloatingEmote : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float lifeTime = 2.0f;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += moveSpeed * Time.deltaTime * Vector3.up;

        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}