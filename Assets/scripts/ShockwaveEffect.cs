using UnityEngine;

public class ShockwaveEffect : MonoBehaviour
{
    public float expandSpeed = 15f;
    public float maxScale = 10f;

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        // Expand
        transform.localScale += Vector3.one * expandSpeed * Time.deltaTime;

        // Fade out
        if (rend != null)
        {
            Color c = rend.material.color;
            c.a -= Time.deltaTime * 1.5f;
            rend.material.color = c;
        }

        // Destroy when done
        if (transform.localScale.x >= maxScale)
        {
            Destroy(gameObject);
        }
    }
}