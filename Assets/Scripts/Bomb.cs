using System.Collections;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(ExplodeAfterDelay(5f));
    }

    IEnumerator ExplodeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        Destroy(gameObject);
    }
}