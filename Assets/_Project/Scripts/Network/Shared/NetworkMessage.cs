using UnityEngine;

namespace TheLostHill.Network.Shared
{
    // ═══════════════════════════════════════════════════════════════
    //  BASE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Clase base para todos los mensajes de red.
    /// Cada mensaje lleva un OpCode, el ID del emisor y un timestamp.
    /// </summary>
    public class NetworkMessage
    {
        public OpCode Code;
        public int SenderId;
        public float Timestamp;

        public NetworkMessage() { }

        public NetworkMessage(OpCode code, int senderId)
        {
            Code = code;
            SenderId = senderId;
            Timestamp = Time.time;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CONEXIÓN
    // ═══════════════════════════════════════════════════════════════

    public class ConnectRequestMessage : NetworkMessage
    {
        public string PlayerName;
        public string GameVersion;

        public ConnectRequestMessage() { Code = OpCode.ConnectRequest; }
    }

    public class ConnectAcceptMessage : NetworkMessage
    {
        public int AssignedPlayerId;
        public int AssignedColorIndex;

        public ConnectAcceptMessage() { Code = OpCode.ConnectAccept; }
    }

    public class ConnectRejectMessage : NetworkMessage
    {
        public RejectReason Reason;

        public ConnectRejectMessage() { Code = OpCode.ConnectReject; }
    }

    public class DisconnectMessage : NetworkMessage
    {
        public DisconnectMessage() { Code = OpCode.Disconnect; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  JUGADORES
    // ═══════════════════════════════════════════════════════════════

    public class PlayerJoinedMessage : NetworkMessage
    {
        public int PlayerId;
        public string PlayerName;
        public int ColorIndex;

        public PlayerJoinedMessage() { Code = OpCode.PlayerJoined; }
    }

    public class PlayerLeftMessage : NetworkMessage
    {
        public int PlayerId;

        public PlayerLeftMessage() { Code = OpCode.PlayerLeft; }
    }

    public class PlayerInputMessage : NetworkMessage
    {
        public int SequenceNumber;
        public float InputX;
        public float InputZ;
        public bool Sprint;

        public PlayerInputMessage() { Code = OpCode.PlayerInput; }
    }

    public class PlayerStateMessage : NetworkMessage
    {
        public int PlayerId;
        public int LastProcessedInput;
        public float PosX, PosY, PosZ;
        public float RotY;

        public PlayerStateMessage() { Code = OpCode.PlayerState; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ESTADO DEL MUNDO
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Datos de un jugador individual dentro de un WorldState.
    /// </summary>
    public struct PlayerSnapshot
    {
        public int PlayerId;
        public float PosX, PosY, PosZ;
        public float RotY;
        public bool IsAlive;
    }

    /// <summary>
    /// Datos del monstruo dentro de un WorldState.
    /// </summary>
    public struct MonsterSnapshot
    {
        public float PosX, PosY, PosZ;
        public float RotY;
        public byte State; // 0=idle, 1=patrol, 2=chase
    }

    public class WorldStateMessage : NetworkMessage
    {
        public int Tick;
        public PlayerSnapshot[] Players;
        public MonsterSnapshot Monster;

        public WorldStateMessage() { Code = OpCode.WorldState; }
    }

    public class WorldSnapshotMessage : NetworkMessage
    {
        /// <summary>Snapshot completo para sincronizar un jugador que se acaba de conectar.</summary>
        public PlayerSnapshot[] Players;
        public MonsterSnapshot Monster;
        public int[] CollectedItemIds;
        public int TotalItems;
        public byte CurrentGameState;

        public WorldSnapshotMessage() { Code = OpCode.WorldSnapshot; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  MONSTRUO
    // ═══════════════════════════════════════════════════════════════

    public class MonsterStateMessage : NetworkMessage
    {
        public float PosX, PosY, PosZ;
        public float RotY;
        public byte State;

        public MonsterStateMessage() { Code = OpCode.MonsterState; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ÍTEMS
    // ═══════════════════════════════════════════════════════════════

    public class ItemCollectedMessage : NetworkMessage
    {
        public int ItemId;
        public int CollectorPlayerId;

        public ItemCollectedMessage() { Code = OpCode.ItemCollected; }
    }

    public class ItemSpawnedMessage : NetworkMessage
    {
        public int ItemId;
        public float PosX, PosY, PosZ;

        public ItemSpawnedMessage() { Code = OpCode.ItemSpawned; }
    }

    public class ItemsSyncMessage : NetworkMessage
    {
        public int[] ItemIds;
        public float[] PosX, PosY, PosZ;
        public bool[] Collected;

        public ItemsSyncMessage() { Code = OpCode.ItemsSync; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ESTADO DEL JUEGO
    // ═══════════════════════════════════════════════════════════════

    public class GameStateChangeMessage : NetworkMessage
    {
        public byte NewState;

        public GameStateChangeMessage() { Code = OpCode.GameStateChange; }
    }

    public class GameRulesUpdateMessage : NetworkMessage
    {
        public int MaxPlayers;
        public float MonsterSpeed;
        public int TotalItems;
        public float TimeLimit;
        public float MonsterSpawnDelay;

        public GameRulesUpdateMessage() { Code = OpCode.GameRulesUpdate; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PING
    // ═══════════════════════════════════════════════════════════════

    public class PingRequestMessage : NetworkMessage
    {
        public float ClientTime;

        public PingRequestMessage() { Code = OpCode.PingRequest; }
    }

    public class PingResponseMessage : NetworkMessage
    {
        public float ClientTime; // eco del tiempo original
        public int TargetPlayerId;

        public PingResponseMessage() { Code = OpCode.PingResponse; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ADMIN
    // ═══════════════════════════════════════════════════════════════

    public class KickPlayerMessage : NetworkMessage
    {
        public int TargetPlayerId;
        public string Reason;

        public KickPlayerMessage() { Code = OpCode.KickPlayer; }
    }

    public class BanIPMessage : NetworkMessage
    {
        public int TargetPlayerId;
        public string Reason;

        public BanIPMessage() { Code = OpCode.BanIP; }
    }

    public class PauseGameMessage : NetworkMessage
    {
        public PauseGameMessage() { Code = OpCode.PauseGame; }
    }

    public class ResumeGameMessage : NetworkMessage
    {
        public ResumeGameMessage() { Code = OpCode.ResumeGame; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEEP ALIVE
    // ═══════════════════════════════════════════════════════════════

    public class KeepAliveMessage : NetworkMessage
    {
        public KeepAliveMessage() { Code = OpCode.KeepAlive; }
    }
}
