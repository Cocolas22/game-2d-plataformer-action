using UnityEngine;

public class HitboxAtaque : MonoBehaviour
{
    private Prota jugador;
    private bool yaGolpeo;

    public float fuerzaRetroceso = 8f;

    void Start()
    {
        jugador = GetComponentInParent<Prota>();
    }

    // Resetear el flag cada vez que se activa la hitbox
    void OnEnable()
    {
        yaGolpeo = false;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (yaGolpeo)
            return;

        if (!collision.CompareTag("Enemigo"))
            return;

        VidaEnemigo vida = collision.GetComponent<VidaEnemigo>();
        EnemigoBasico enemigoBasico = collision.GetComponent<EnemigoBasico>();

        // ── FIX: limpiar launcherActivo ANTES del return,
        //    para que no quede activo si el enemigo no tiene VidaEnemigo ──
        if (vida == null)
        {
            jugador.launcherActivo = false;
            return;
        }

        // ════════════════════════════════════════
        // 1. LAUNCHER
        // ════════════════════════════════════════
        if (jugador.launcherActivo)
        {
            vida.RecibirDaño(jugador.dañoActual);

            // Lanzar() aplica la fuerza física Y marca enAire = true internamente.
            // EjecutarLauncher() solo marcaba enAire pero nunca aplicaba fuerza,
            // por eso el enemigo no se movía.
            if (enemigoBasico != null)
                enemigoBasico.Lanzar(20f);

            jugador.launcherActivo = false;
            yaGolpeo = true;
            return;
        }

        // ════════════════════════════════════════
        // 2. DAÑO BASE
        // ════════════════════════════════════════
        vida.RecibirDaño(jugador.dañoActual);

        // ── Remate de Martillo (tercer golpe aéreo) ──
        if (jugador.martilloActivo)
        {
            enemigoBasico.Estrellar(25f);
            jugador.martilloActivo = false;
        }

        // ── Recuperar energía según combo ──
        EnergiaJugador energia = jugador.GetComponent<EnergiaJugador>();
        if (energia != null)
        {
            int energiaGanada = jugador.comboPaso switch
            {
                1 => 2,
                2 => 3,
                3 => 5,
                _ => 2
            };
            energia.RecuperarEnergia(energiaGanada);
        }

        // ── Empuje en el 3er golpe del combo ──
        if (jugador.comboPaso == 3)
        {
            float dir = jugador.GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
            vida.RecibirEmpuje(new Vector2(dir * fuerzaRetroceso, 2f));
        }

        // ── Remate de Ráfaga ──
        if (jugador.remateRafagaActivo)
        {
            float dir = jugador.GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
            vida.RecibirEmpuje(new Vector2(dir * jugador.fuerzaRemate, 3f));
            jugador.remateRafagaActivo = false;
        }

        yaGolpeo = true;
    }
}