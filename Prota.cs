using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

class Prota : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 8f;
    public float velocidadCorrer = 14f;
    public float fuerzaSalto = 16f;

    [Header("Doble Tap")]
    public float tiempoDobleTap = 0.3f;

    [Header("Suelo")]
    public Transform puntoSuelo;
    public float radioSuelo = 0.2f;
    public LayerMask capaSuelo;

    [Header("Combo")]
    public float tiempoCombo = 0.7f;

    [Header("Daño Combo")]
    public int dañoCombo1 = 10;
    public int dañoCombo2 = 15;
    public int dañoCombo3 = 25;

    [HideInInspector]
    public int dañoActual;

    [Header("Ataque")]
    public GameObject hitboxAtaque;

    [Header("Camera Shake")]
    public float shakeCombo1 = 0.3f;
    public float shakeCombo2 = 0.7f;
    public float shakeCombo3 = 1.2f;

    private HabilidadesJugador habilidades;

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Animator anim;
    private CinemachineImpulseSource impulso;
    private VidaJugador vidaJugador;
    private EnergiaJugador energiaJugador;

    public Transform puntosAtaque;

    private bool enSuelo;
    private float movimiento;
    private bool corriendo;

    private float ultimoTapDerecha;
    private float ultimoTapIzquierda;

    [HideInInspector]
    public int comboPaso = 0;
    private float ultimoAtaque;

    [Header("Dash")]
    public float velocidadDash = 20f;
    public float duracionDash = 0.5f;
    public float cooldownDash = 1f;

    private bool haciendoDash = false;
    private float tiempoFinDash;
    private float direccionDash;
    private float ultimoDash = -999f;

    [Header("Parry")]
    public float duracionParry = 0.2f;
    public float cooldownParry = 1f;

    public bool haciendoParry;
    private float tiempoFinParry;
    private float ultimoParry;

    [Header("Contraataque")]
    public float tiempoContraataque = 0.2f;

    private bool puedeContraatacar;
    private float finContraataque;

    [Header("Proyectil")]
    public GameObject prefabProyectil;
    public Transform puntoDisparo;
    public float distanciaDisparo = 1f;
    public int costoEnergia = 10;

    [Header("Rafaga")]
    public int costoEnergiaRafaga = 2;
    public float intervaloRafaga = 0.1f;
    public int dañoRafaga = 4;
    public float shakeGolpeRafaga = 0.1f;

    private bool haciendoRafaga;
    private float siguienteGolpeRafaga;

    [Header("Remate Rafaga")]
    public int dañoRemateRafaga = 20;
    public float fuerzaRemate = 12f;
    public float shakeRemate = 3f;

    [HideInInspector] public bool remateRafagaActivo;
    [HideInInspector] public bool launcherActivo;
    [HideInInspector] public bool martilloActivo;

    private int comboAereoPaso;
    public bool comboAereoActivo;

    public float alturaComboAereo = 6f;

    private float alturaObjetivo;

    public EnemigoBasico enemigoAereo;

    [Header("Golpe Area")]
    public GameObject hitboxArea;
    public GameObject prefabExplosionArea;
    public int dañoArea = 30;
    public int costoEnergiaArea = 20;
    public float shakeArea = 4f;
    public float fuerzaSaltoArea = 12f;
    public float velocidadPicada = 30f;

    private bool haciendoArea;

    // ─────────────────────────────────────────
    // FIX: Ventana de gracia post-Launcher
    // Evita que enSuelo=true en los primeros
    // frames bloquee el combo aéreo.
    // ─────────────────────────────────────────
    private float tiempoUltimoLauncher = -999f;
    public float graciaSueloLauncher = 0.15f;

    // Propiedad para no repetir sprite.flipX ? -1f : 1f en cada método
    private float direccionSprite => sprite.flipX ? -1f : 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        vidaJugador = GetComponent<VidaJugador>();
        energiaJugador = GetComponent<EnergiaJugador>();
        impulso = GetComponent<CinemachineImpulseSource>();
        habilidades = GetComponent<HabilidadesJugador>();
    }

    void Update()
    {
        // No leer input de movimiento mientras se hace dash, parry o ráfaga
        if (!haciendoDash && !haciendoParry && !haciendoRafaga)
            movimiento = Input.GetAxisRaw("Horizontal");

        enSuelo = Physics2D.OverlapCircle(puntoSuelo.position, radioSuelo, capaSuelo);

        // Durante la ventana de gracia post-Launcher, tratamos al jugador
        // como si estuviera en el aire aunque enSuelo sea true por un frame
        bool enVentanaLauncher = Time.time - tiempoUltimoLauncher < graciaSueloLauncher;
        bool efectivamenteEnSuelo = enSuelo && !enVentanaLauncher;

        if (efectivamenteEnSuelo)
        {
            comboAereoPaso = 0;
            martilloActivo = false;
        }

        if (haciendoArea && efectivamenteEnSuelo)
            FinalizarArea();

        DetectarDobleTap();
        ActualizarAnimator();
        ActualizarDireccion();
        LeerInputSalto(efectivamenteEnSuelo);
        LeerInputAtaque(efectivamenteEnSuelo, enVentanaLauncher);
        LeerInputDash();
        LeerInputHabilidades();
        ActualizarTimers();
    }

    // ─────────────────────────────────────────
    // Update dividido en métodos claros
    // ─────────────────────────────────────────

    void ActualizarAnimator()
    {
        anim.SetFloat("Movimiento", Mathf.Abs(movimiento));
        anim.SetFloat("VelocidadY", rb.linearVelocity.y);
        anim.SetBool("EnSuelo", enSuelo);
        anim.SetBool("Corriendo", corriendo);
    }

    void ActualizarDireccion()
    {
        if (movimiento > 0)
        {
            sprite.flipX = false;
            if (puntosAtaque != null)
                puntosAtaque.localScale = new Vector3(1, 1, 1);
        }
        else if (movimiento < 0)
        {
            sprite.flipX = true;
            if (puntosAtaque != null)
                puntosAtaque.localScale = new Vector3(-1, 1, 1);
        }
    }

    void LeerInputSalto(bool efectivamenteEnSuelo)
    {
        if (Input.GetButtonDown("Jump") && efectivamenteEnSuelo && !haciendoDash)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, fuerzaSalto);
    }

    void LeerInputAtaque(bool efectivamenteEnSuelo, bool enVentanaLauncher)
    {
        if (!Input.GetKeyDown(KeyCode.X))
            return;

        if (puedeContraatacar)
        {
            Contraataque();
        }
        else if (efectivamenteEnSuelo && Input.GetKey(KeyCode.W))
        {
            // Solo lanzar si está firmemente en el suelo (fuera de la ventana de gracia)
            Launcher();
        }
        else if (!efectivamenteEnSuelo || enVentanaLauncher)
        {
            // En el aire O recién salido del Launcher → combo aéreo
            AtaqueAereo();
        }
        else
        {
            Ataque();
        }
    }

    void LeerInputDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
            IntentarDash();
    }

    void LeerInputHabilidades()
    {
        // ── Slot 1 (tecla C) ──
        if (Input.GetKeyDown(KeyCode.C))
            UsarSlot(habilidades.slot1);

        if (Input.GetKey(KeyCode.C) && habilidades.slot1 == Habilidad.Rafaga)
            Rafaga();

        if (Input.GetKeyUp(KeyCode.C) && haciendoRafaga)
        {
            FinalizarRafaga();
            movimiento = 0;
        }

        // ── Slot 2 (tecla Z) ──
        if (Input.GetKeyDown(KeyCode.Z))
            UsarSlot(habilidades.slot2);

        if (Input.GetKey(KeyCode.Z) && habilidades.slot2 == Habilidad.Rafaga)
            Rafaga();

        if (Input.GetKeyUp(KeyCode.Z) && haciendoRafaga)
        {
            FinalizarRafaga();
            movimiento = 0;
        }
    }

    // Un único método para ejecutar la habilidad del slot.
    // Ráfaga se maneja aparte con GetKey en LeerInputHabilidades.
    void UsarSlot(Habilidad habilidad)
    {
        switch (habilidad)
        {
            case Habilidad.Parry:
                IntentarParry();
                break;
            case Habilidad.Proyectil:
                Disparar();
                break;
            case Habilidad.Area:
                GolpeArea();
                break;
        }
    }

    void ActualizarTimers()
    {
        if (haciendoParry && Time.time >= tiempoFinParry)
        {
            haciendoParry = false;
            vidaJugador.invulnerable = false;
        }

        if (puedeContraatacar && Time.time >= finContraataque)
            puedeContraatacar = false;
    }

    void FixedUpdate()
    {
        if (haciendoDash)
        {
            rb.linearVelocity = new Vector2(direccionDash * velocidadDash, 0);

            if (Time.time >= tiempoFinDash)
            {
                haciendoDash = false;
                vidaJugador.invulnerable = false;

                Physics2D.IgnoreLayerCollision(
                    LayerMask.NameToLayer("Jugador"),
                    LayerMask.NameToLayer("Enemigo"),
                    false
                );

                anim.SetBool("Dash", false);
            }

            return;
        }

        float velocidadActual = corriendo ? velocidadCorrer : velocidad;
        rb.linearVelocity = new Vector2(movimiento * velocidadActual, rb.linearVelocity.y);
    }

    // ─────────────────────────────────────────
    // Detección de doble tap para correr
    // ─────────────────────────────────────────

    void DetectarDobleTap()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (Time.time - ultimoTapDerecha < tiempoDobleTap)
                corriendo = true;

            ultimoTapDerecha = Time.time;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (Time.time - ultimoTapIzquierda < tiempoDobleTap)
                corriendo = true;

            ultimoTapIzquierda = Time.time;
        }

        if (!Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            corriendo = false;
    }

    // ─────────────────────────────────────────
    // Combos de suelo
    // ─────────────────────────────────────────

    void Ataque()
    {
        if (Time.time - ultimoAtaque > tiempoCombo)
            comboPaso = 0;

        comboPaso++;

        if (comboPaso > 3)
            comboPaso = 1;

        ultimoAtaque = Time.time;

        switch (comboPaso)
        {
            case 1:
                dañoActual = dañoCombo1;
                anim.SetTrigger("ataque1");
                Shake(shakeCombo1);
                break;
            case 2:
                dañoActual = dañoCombo2;
                anim.SetTrigger("ataque2");
                Shake(shakeCombo2);
                break;
            case 3:
                dañoActual = dañoCombo3;
                anim.SetTrigger("ataque3");
                Shake(shakeCombo3);
                break;
        }
    }

    // ─────────────────────────────────────────
    // Combos aéreos
    // ─────────────────────────────────────────

    void AtaqueAereo()
    {
        anim.ResetTrigger("launcher");
        anim.ResetTrigger("PatadaAerea1");
        anim.ResetTrigger("PatadaAerea2");
        anim.ResetTrigger("martillo");

        comboAereoPaso++;

        if (comboAereoPaso > 3)
            return;

        switch (comboAereoPaso)
        {
            case 1:
                // launcherActivo NO se limpia aquí — lo limpia HitboxAtaque
                // al confirmar el golpe, o FinalizarLauncher() al terminar la animación.
                // Si lo limpiamos aquí el Launcher pierde su efecto de lanzar al enemigo.
                martilloActivo = false;
                dañoActual = 12;
                anim.SetTrigger("PatadaAerea1");
                break;
            case 2:
                martilloActivo = false;
                dañoActual = 15;
                anim.SetTrigger("PatadaAerea2");
                break;
            case 3:
                martilloActivo = true;
                dañoActual = 30;
                anim.SetTrigger("martillo");
                break;
        }
    }

    // ─────────────────────────────────────────
    // Launcher
    // ─────────────────────────────────────────

    void Launcher()
    {
        comboAereoPaso = 0;
        dañoActual = 15;
        launcherActivo = true;

        // FIX: registrar el momento del Launcher para la ventana de gracia
        tiempoUltimoLauncher = Time.time;
        alturaObjetivo = transform.position.y + alturaComboAereo;
        comboAereoActivo = false;
        enemigoAereo = null;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 22f);

        // NUEVO: Congelar al jugador en el aire
        Invoke(nameof(CongelarEnAireJugador), 0.3f);

        anim.SetTrigger("launcher");
    }

    // NUEVO: Método para congelar al jugador sin gravedad
    void CongelarEnAireJugador()
    {
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0, 0);
    }

    // Llamar desde Animation Event al ÚLTIMO FRAME de la animación del Launcher.
    // Si el Launcher no golpeó a ningún enemigo, limpia launcherActivo de forma segura.
    public void FinalizarLauncher()
    {
        launcherActivo = false;
    }

    // ─────────────────────────────────────────
    // Contraataque
    // ─────────────────────────────────────────

    void Contraataque()
    {
        puedeContraatacar = false;
        anim.SetTrigger("Contraataque");
        Shake(3f);
        Debug.Log("CONTRAATAQUE");
    }

    // ─────────────────────────────────────────
    // Dash
    // ─────────────────────────────────────────

    void IntentarDash()
    {
        if (haciendoDash)
            return;

        if (Time.time < ultimoDash + cooldownDash)
            return;

        ultimoDash = Time.time;
        haciendoDash = true;
        direccionDash = direccionSprite;

        vidaJugador.invulnerable = true;

        Physics2D.IgnoreLayerCollision(
            LayerMask.NameToLayer("Jugador"),
            LayerMask.NameToLayer("Enemigo"),
            true
        );

        anim.SetBool("Dash", true);
        tiempoFinDash = Time.time + duracionDash;
    }

    // ─────────────────────────────────────────
    // Parry
    // ─────────────────────────────────────────

    void IntentarParry()
    {
        if (habilidades.slot1 != Habilidad.Parry && habilidades.slot2 != Habilidad.Parry)
            return;

        if (Time.time < ultimoParry + cooldownParry)
            return;

        ultimoParry = Time.time;
        haciendoParry = true;

        rb.linearVelocity = Vector2.zero;
        movimiento = 0;

        vidaJugador.invulnerable = true;
        tiempoFinParry = Time.time + duracionParry;

        anim.SetTrigger("Parry");
    }

    IEnumerator HitStop(float duracion)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duracion);
        Time.timeScale = 1f;
    }

    public void ParryExitoso()
    {
        Shake(2f);
        StartCoroutine(HitStop(0.08f));
        puedeContraatacar = true;
        finContraataque = Time.time + tiempoContraataque;
    }

    // ─────────────────────────────────────────
    // Proyectil
    // ─────────────────────────────────────────

    void Disparar()
    {
        if (habilidades.slot1 != Habilidad.Proyectil && habilidades.slot2 != Habilidad.Proyectil)
            return;

        if (!energiaJugador.GastarEnergia(costoEnergia))
            return;

        anim.SetTrigger("Disparo");

        GameObject nuevoProyectil = Instantiate(prefabProyectil, puntoDisparo.position, Quaternion.identity);
        nuevoProyectil.GetComponent<Proyectil>().Configurar(direccionSprite);
    }

    // ─────────────────────────────────────────
    // Ráfaga
    // ─────────────────────────────────────────

    void Rafaga()
    {
        haciendoRafaga = true;
        rb.linearVelocity = Vector2.zero;
        movimiento = 0;

        if (Time.time < siguienteGolpeRafaga)
            return;

        if (!energiaJugador.GastarEnergia(costoEnergiaRafaga))
        {
            FinalizarRafaga();
            return;
        }

        siguienteGolpeRafaga = Time.time + intervaloRafaga;
        dañoActual = dañoRafaga;

        Shake(shakeGolpeRafaga);
        anim.SetTrigger("Rafaga");

        // Desactivar y reactivar para forzar OnEnable → resetear yaGolpeo
        hitboxAtaque.SetActive(false);
        hitboxAtaque.SetActive(true);

        Invoke(nameof(DesactivarHitbox), 0.05f);
    }

    void FinalizarRafaga()
    {
        if (!haciendoRafaga)
            return;

        haciendoRafaga = false;
        anim.SetTrigger("RafagaRemate");
    }

    public void GolpeRemateRafaga()
    {
        remateRafagaActivo = true;
        dañoActual = dañoRemateRafaga;

        ActivarHitbox();
        Invoke(nameof(DesactivarHitbox), 0.15f);
        Shake(shakeRemate);
    }

    // ─────────────────────────────────────────
    // Golpe de Área
    // ─────────────────────────────────────────

    void GolpeArea()
    {
        if (haciendoArea)
            return;

        if (!energiaJugador.GastarEnergia(costoEnergiaArea))
            return;

        haciendoArea = true;
        dañoActual = dañoArea;

        rb.linearVelocity = new Vector2(0, fuerzaSaltoArea);
        anim.SetTrigger("Area");
    }

    public void IniciarPicada()
    {
        rb.linearVelocity = new Vector2(0, -velocidadPicada);
    }

    void FinalizarArea()
    {
        haciendoArea = false;

        ActivarHitboxArea();
        Invoke(nameof(DesactivarHitboxArea), 0.2f);
        Shake(shakeArea);
    }

    // ─────────────────────────────────────────
    // Hitbox helpers
    // ─────────────────────────────────────────

    public void ActivarHitbox() => hitboxAtaque.SetActive(true);
    public void DesactivarHitbox() => hitboxAtaque.SetActive(false);
    public void ActivarHitboxArea() => hitboxArea.SetActive(true);
    public void DesactivarHitboxArea() => hitboxArea.SetActive(false);

    public void CrearExplosionArea()
    {
        Instantiate(prefabExplosionArea, transform.position, Quaternion.identity);
    }

    // ─────────────────────────────────────────
    // Camera shake
    // ─────────────────────────────────────────

    void Shake(float intensidad)
    {
        if (impulso != null)
            impulso.GenerateImpulse(intensidad);
    }

    // ─────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (puntoSuelo == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(puntoSuelo.position, radioSuelo);
    }
}