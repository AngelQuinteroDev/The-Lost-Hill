namespace TheLostHill.Network.Shared
{
    public enum OpCode : byte
    {
        ConnectRequest = 0x01,
        ConnectAccept = 0x02,
        ConnectReject = 0x03,
        Disconnect = 0x04,

        PlayerJoined = 0x10,
        PlayerLeft = 0x11,
        PlayerInput = 0x12,
        PlayerState = 0x13,

        WorldState = 0x20,
        MonsterState = 0x21,
        WorldSnapshot = 0x22,

        ItemCollected = 0x30,
        ItemSpawned = 0x31,
        ItemsSync = 0x32,

        GameStateChange = 0x40,
        GameRulesUpdate = 0x41,

        PingRequest = 0x50,
        PingResponse = 0x51,

        KickPlayer = 0x60,
        BanIP = 0x61,
        PauseGame = 0x62,
        ResumeGame = 0x63,

        ChatMessage = 0x70,

        KeepAlive = 0xFE,

        Unknown = 0xFF
    }

    public enum RejectReason : byte
    {
        None = 0,
        ServerFull = 1,
        Banned = 2,
        VersionMismatch = 3,
        GameInProgress = 4
    }
}