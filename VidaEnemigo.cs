using UnityEngine;

public class VidaEnemigo : MonoBehaviour
{
    public int vida = 30;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void RecibirDaño(int daño)
    {
        vida -= daño;

        if (vida <= 0)
            Destroy(gameObject);
    }

    public void RecibirEmpuje(Vector2 fuerza)
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(fuerza, ForceMode2D.Impulse);
    }

    public void GolpeSuelo()
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(Vector2.down * 20f, ForceMode2D.Impulse);
    }
}