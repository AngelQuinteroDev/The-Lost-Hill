namespace TheLostHill.Core
{
    /// <summary>
    /// Constantes globales del proyecto. Puertos, buffers, timeouts y límites.
    /// </summary>
    public static class Constants
    {
        // ── Red (un solo puerto UDP) ─────────────────────────────
        public const int DefaultNetworkPort = 7777;
        public const int DefaultTcpPort = DefaultNetworkPort;
        public const int DefaultUdpPort = DefaultNetworkPort;

        // ── Buffers ──────────────────────────────────────────────
        public const int TcpBufferSize = 4096;
        public const int UdpBufferSize = 1024;
        public const int MaxPacketSize = 2048;

        // ── Timeouts (segundos) ──────────────────────────────────
        public const float ConnectionTimeout = 15f;
        public const float TcpKeepAliveInterval = 10f;
        // Más margen para pruebas con ventanas en background (Multiplayer Center)
        public const float DisconnectTimeout = 90f;

        // ── Ping ─────────────────────────────────────────────────
        public const float PingInterval = 1f;
        public const int PingSmoothingWindow = 5;

        // ── Jugadores ────────────────────────────────────────────
        public const int MaxPlayers = 8;
        public const int MinPlayers = 2;

        // ── Sincronización ───────────────────────────────────────
        public const float InterpolationDelay = 0.1f;        // 100ms
        public const float ExtrapolationMaxTime = 0.5f;      // máximo de extrapolación
        public const float ReconciliationThreshold = 0.1f;   // distancia para reconciliar
        public const float NetworkSendRate = 1f / 20f;       // 20 updates/segundo (UDP)

        // ── Reconexión ───────────────────────────────────────────
        public const int MaxReconnectAttempts = 5;
        public const float ReconnectBaseDelay = 1f;
        public const float ReconnectMaxDelay = 16f;

        // ── Snapshot Buffer ──────────────────────────────────────
        public const int SnapshotBufferSize = 30;

        // ── Header ───────────────────────────────────────────────
        /// <summary>Tamaño del header de longitud para TCP framing (4 bytes = int32).</summary>
        public const int LengthHeaderSize = 4;

        // ── Escenas ──────────────────────────────────────────────
        public const string MainMenuScene = "MainScene";
    }
}
