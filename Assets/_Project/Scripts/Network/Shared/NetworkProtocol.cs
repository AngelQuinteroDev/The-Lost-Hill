namespace TheLostHill.Network.Shared
{
    /// <summary>
    /// Códigos de operación para todos los mensajes de red.
    /// Cada mensaje enviado por TCP o UDP lleva un OpCode que identifica su tipo.
    /// </summary>
    public enum OpCode : byte
    {
        // ── Conexión ─────────────────────────────────────────────
        ConnectRequest      = 0x01,
        ConnectAccept       = 0x02,
        ConnectReject       = 0x03,
        Disconnect          = 0x04,

        // ── Jugadores ────────────────────────────────────────────
        PlayerJoined        = 0x10,
        PlayerLeft          = 0x11,
        PlayerInput         = 0x12,
        PlayerState         = 0x13,

        // ── Estado del mundo ─────────────────────────────────────
        WorldState          = 0x20,
        MonsterState        = 0x21,
        WorldSnapshot       = 0x22,    // Snapshot completo para nuevos jugadores

        // ── Ítems / Coleccionables ───────────────────────────────
        ItemCollected       = 0x30,
        ItemSpawned         = 0x31,
        ItemsSync           = 0x32,    // Sync completo de ítems

        // ── Estado del juego ─────────────────────────────────────
        GameStateChange     = 0x40,
        GameRulesUpdate     = 0x41,

        // ── Ping ─────────────────────────────────────────────────
        PingRequest         = 0x50,
        PingResponse        = 0x51,

        // ── Admin ────────────────────────────────────────────────
        KickPlayer          = 0x60,
        BanIP               = 0x61,
        PauseGame           = 0x62,
        ResumeGame          = 0x63,

        // ── Chat (opcional, extensión futura) ────────────────────
        ChatMessage         = 0x70,

        // ── Keep Alive ───────────────────────────────────────────
        KeepAlive           = 0xFE,

        // ── Desconocido ──────────────────────────────────────────
        Unknown             = 0xFF
    }

    /// <summary>
    /// Razones de rechazo de conexión.
    /// </summary>
    public enum RejectReason : byte
    {
        None            = 0,
        ServerFull      = 1,
        Banned          = 2,
        VersionMismatch = 3,
        GameInProgress  = 4
    }
}
