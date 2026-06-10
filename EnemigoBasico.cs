using UnityEngine;

public class EnemigoBasico : MonoBehaviour
{
    [Header("Detección")]
    public Transform jugador;
    public float distanciaDeteccion = 8f;
    public float distanciaAtaque = 1.5f;

    [Header("Movimiento")]
    public float velocidad = 3f;

    [Header("Ataque")]
    public float cooldownAtaque = 1.5f;
    public GameObject hitboxAtaque;

    [Header("Suelo")]
    public Transform puntoSuelo;
    public float radioSuelo = 0.2f;
    public LayerMask capaSuelo;

    private float ultimoAtaque;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;

    public bool enAire;
    public bool congeladoEnAire;

    // ══════════════════════════════════════════════
    // NUEVO: Control de suspensión aérea sin física
    // ══════════════════════════════════════════════
    private float tiempoSuspension = 0f;
    private float duracionSuspension = 0f;
    private bool suspendidoEnAire = false;

    enum Estado { Idle, Perseguir, Atacar, Lanzado }
    private Estado estadoActual;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        estadoActual = Estado.Idle;

        if (jugador == null)
        {
            GameObject objJugador = GameObject.FindGameObjectWithTag("Jugador");
            if (objJugador != null)
                jugador = objJugador.transform;
        }
    }

    void Update()
    {
        // ══════════════════════════════════════════════
        // NUEVO: Gestionar suspensión aérea
        // ══════════════════════════════════════════════
        if (suspendidoEnAire)
        {
            tiempoSuspension += Time.deltaTime;

            // Mantener velocidad Y en 0 (flotando)
            rb.linearVelocity = new Vector2(0, 0);
            rb.gravityScale = 0f;

            // Si se acabó el tiempo de suspensión, volver a caer
            if (tiempoSuspension >= duracionSuspension)
            {
                suspendidoEnAire = false;
                rb.gravityScale = 1f;
                rb.linearVelocity = new Vector2(0, -0.5f); // Caída suave
            }

            anim.SetBool("Caminar", false);
            return;
        }

        // ── Fase 1: congelar en el aire al inicio de la caída ──
        if (enAire && !congeladoEnAire && rb.linearVelocity.y < -0.5f)
        {
            congeladoEnAire = true;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        if (congeladoEnAire)
        {
            anim.SetBool("Caminar", false);
            return;
        }

        // ── FIX: detectar aterrizaje con OverlapCircle igual que el jugador ──
        if (enAire && EstaEnSuelo())
        {
            enAire = false;
            congeladoEnAire = false;
            suspendidoEnAire = false;
            estadoActual = Estado.Idle;
            rb.gravityScale = 1f;
        }

        // Si sigue en el aire (sin congelar) detenemos la IA
        if (enAire || estadoActual == Estado.Lanzado)
        {
            anim.SetBool("Caminar", false);
            return;
        }

        if (jugador == null)
            return;

        ActualizarEstado();
        EjecutarEstado();
        GirarHaciaJugador();
    }

    // ─────────────────────────────────────────
    // Estado de la IA
    // ─────────────────────────────────────────

    void ActualizarEstado()
    {
        float distancia = Vector2.Distance(transform.position, jugador.position);

        if (distancia > distanciaDeteccion)
            estadoActual = Estado.Idle;
        else if (distancia > distanciaAtaque)
            estadoActual = Estado.Perseguir;
        else
            estadoActual = Estado.Atacar;
    }

    void EjecutarEstado()
    {
        switch (estadoActual)
        {
            case Estado.Idle: Idle(); break;
            case Estado.Perseguir: Perseguir(); break;
            case Estado.Atacar: Atacar(); break;
        }
    }

    void Idle()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        anim.SetBool("Caminar", false);
    }

    void Perseguir()
    {
        float direccion = jugador.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(direccion * velocidad, rb.linearVelocity.y);
        anim.SetBool("Caminar", true);
    }

    void Atacar()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        anim.SetBool("Caminar", false);

        if (Time.time < ultimoAtaque + cooldownAtaque)
            return;

        ultimoAtaque = Time.time;
        anim.SetTrigger("Ataque");
    }

    void GirarHaciaJugador()
    {
        if (jugador == null)
            return;

        sprite.flipX = jugador.position.x < transform.position.x;
    }

    // ─────────────────────────────────────────
    // Detección de suelo (igual que el jugador)
    // ─────────────────────────────────────────

    bool EstaEnSuelo()
    {
        Vector2 origen = puntoSuelo != null
            ? (Vector2)puntoSuelo.position
            : (Vector2)transform.position + Vector2.down * 0.5f;

        return Physics2D.OverlapCircle(origen, radioSuelo, capaSuelo);
    }

    // ─────────────────────────────────────────
    // Hitbox del ataque del enemigo
    // ─────────────────────────────────────────

    public void ActivarHitbox() => hitboxAtaque.SetActive(true);
    public void DesactivarHitbox() => hitboxAtaque.SetActive(false);

    // ─────────────────────────────────────────
    // Lanzado por el jugador (Launcher)
    // ─────────────────────────────────────────

    /// <summary>
    /// Lanza al enemigo con velocidad inicial y lo suspende en el aire.
    /// </summary>
    /// <param name="fuerza">Velocidad inicial (m/s)</param>
    /// <param name="tiempoFlote">Cuánto tiempo permanece suspendido (segundos)</param>
    public void Lanzar(float fuerza, float tiempoFlote = 1.2f)
    {
        enAire = true;
        congeladoEnAire = false;
        suspendidoEnAire = false;
        estadoActual = Estado.Lanzado;

        // Sin gravedad durante el lanzamiento
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(0, fuerza);

        // Después de 0.3 segundos de caída, congelar en el aire
        Invoke(nameof(CongelarEnAire), 0.3f);
    }

    /// <summary>
    /// Congela al enemigo en el aire para el combo aéreo.
    /// </summary>
    void CongelarEnAire()
    {
        if (!enAire)
            return;

        suspendidoEnAire = true;
        tiempoSuspension = 0f;
        duracionSuspension = 1.2f; // Tiempo que flota
        congeladoEnAire = false;
    }

    // Estrellar: reactivar gravedad y empujar al suelo (remate de martillo)
    public void Estrellar(float fuerza)
    {
        congeladoEnAire = false;
        suspendidoEnAire = false;
        rb.gravityScale = 6f;
        rb.linearVelocity = new Vector2(0, -fuerza);
    }

    // ─────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 origen = puntoSuelo != null
            ? (Vector2)puntoSuelo.position
            : (Vector2)transform.position + Vector2.down * 0.5f;

        Gizmos.DrawWireSphere(origen, radioSuelo);
    }
}