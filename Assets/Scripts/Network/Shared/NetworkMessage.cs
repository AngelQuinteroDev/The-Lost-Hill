using UnityEngine;

namespace TheLostHill.Network.Shared
{
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


        public bool IsMoving;
        public bool IsRunning;
        public bool IsPickingUp;
        public bool IsAlive = true;

        public PlayerStateMessage() { Code = OpCode.PlayerState; }
    }


    public struct PlayerSnapshot
    {
        public int PlayerId;
        public float PosX, PosY, PosZ;
        public float RotY;
        public int ColorIndex;
        public bool IsAlive;


        public bool IsMoving;
        public bool IsRunning;
        public bool IsPickingUp;
    }


    public struct MonsterSnapshot
    {
        public float PosX, PosY, PosZ;
        public float RotY;
        public byte State; 
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

        public PlayerSnapshot[] Players;
        public MonsterSnapshot Monster;
        public int[] CollectedItemIds;
        public int TotalItems;
        public byte CurrentGameState;

        public WorldSnapshotMessage() { Code = OpCode.WorldSnapshot; }
    }


    public class MonsterStateMessage : NetworkMessage
    {
        public float PosX, PosY, PosZ;
        public float RotY;
        public byte State;

        public MonsterStateMessage() { Code = OpCode.MonsterState; }
    }


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


    public class PingRequestMessage : NetworkMessage
    {
        public float ClientTime;

        public PingRequestMessage() { Code = OpCode.PingRequest; }
    }

    public class PingResponseMessage : NetworkMessage
    {
        public float ClientTime; 
        public int TargetPlayerId;

        public PingResponseMessage() { Code = OpCode.PingResponse; }
    }



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

 

    public class KeepAliveMessage : NetworkMessage
    {
        public KeepAliveMessage() { Code = OpCode.KeepAlive; }
    }
}
