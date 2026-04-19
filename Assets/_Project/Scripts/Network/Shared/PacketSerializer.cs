using System;
using System.IO;
using System.Collections.Generic;
using TheLostHill.Core;

namespace TheLostHill.Network.Shared
{
    /// <summary>
    /// Serialización binaria. En wire: [1 byte OpCode][senderId int32][timestamp float][payload…].
    /// Todo el tráfico usa datagramas UDP (un datagrama = un mensaje).
    /// </summary>
    public static class PacketSerializer
    {
        // ═════════════════════════════════════════════════════════
        //  SERIALIZACIÓN
        // ═════════════════════════════════════════════════════════

        /// <summary>Alias de <see cref="SerializeUDP"/> por compatibilidad; el framing TCP ya no se usa.</summary>
        public static byte[] SerializeTCP(NetworkMessage msg) => SerializeUDP(msg);

        /// <summary>
        /// Serializa un mensaje para envío UDP (sin length-prefix).
        /// </summary>
        public static byte[] SerializeUDP(NetworkMessage msg)
        {
            return SerializePayload(msg);
        }

        /// <summary>
        /// Deserializa un payload (sin length-prefix) en un NetworkMessage.
        /// </summary>
        public static NetworkMessage Deserialize(byte[] data, int offset = 0, int length = -1)
        {
            if (length < 0) length = data.Length - offset;
            if (length < 1) return null;

            using (var ms = new MemoryStream(data, offset, length))
            using (var reader = new BinaryReader(ms))
            {
                OpCode code = (OpCode)reader.ReadByte();
                int senderId = reader.ReadInt32();
                float timestamp = reader.ReadSingle();

                return code switch
                {
                    OpCode.ConnectRequest   => ReadConnectRequest(reader, senderId, timestamp),
                    OpCode.ConnectAccept    => ReadConnectAccept(reader, senderId, timestamp),
                    OpCode.ConnectReject    => ReadConnectReject(reader, senderId, timestamp),
                    OpCode.Disconnect       => new DisconnectMessage { SenderId = senderId, Timestamp = timestamp },

                    OpCode.PlayerJoined     => ReadPlayerJoined(reader, senderId, timestamp),
                    OpCode.PlayerLeft       => ReadPlayerLeft(reader, senderId, timestamp),
                    OpCode.PlayerInput      => ReadPlayerInput(reader, senderId, timestamp),
                    OpCode.PlayerState      => ReadPlayerState(reader, senderId, timestamp),

                    OpCode.WorldState       => ReadWorldState(reader, senderId, timestamp),
                    OpCode.WorldSnapshot    => ReadWorldSnapshot(reader, senderId, timestamp),
                    OpCode.MonsterState     => ReadMonsterState(reader, senderId, timestamp),

                    OpCode.ItemCollected    => ReadItemCollected(reader, senderId, timestamp),
                    OpCode.ItemSpawned      => ReadItemSpawned(reader, senderId, timestamp),

                    OpCode.GameStateChange  => ReadGameStateChange(reader, senderId, timestamp),
                    OpCode.GameRulesUpdate  => ReadGameRulesUpdate(reader, senderId, timestamp),

                    OpCode.PingRequest      => ReadPingRequest(reader, senderId, timestamp),
                    OpCode.PingResponse     => ReadPingResponse(reader, senderId, timestamp),

                    OpCode.KickPlayer       => ReadKickPlayer(reader, senderId, timestamp),
                    OpCode.BanIP            => ReadBanIP(reader, senderId, timestamp),
                    OpCode.PauseGame        => new PauseGameMessage { SenderId = senderId, Timestamp = timestamp },
                    OpCode.ResumeGame       => new ResumeGameMessage { SenderId = senderId, Timestamp = timestamp },

                    OpCode.KeepAlive        => new KeepAliveMessage { SenderId = senderId, Timestamp = timestamp },

                    _ => new NetworkMessage { Code = code, SenderId = senderId, Timestamp = timestamp }
                };
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAYLOAD SERIALIZATION (internal)
        // ═════════════════════════════════════════════════════════

        private static byte[] SerializePayload(NetworkMessage msg)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Header común: OpCode + SenderId + Timestamp
                writer.Write((byte)msg.Code);
                writer.Write(msg.SenderId);
                writer.Write(msg.Timestamp);

                // Payload específico por tipo
                switch (msg)
                {
                    case ConnectRequestMessage m:
                        writer.Write(m.PlayerName ?? "");
                        writer.Write(m.GameVersion ?? "");
                        break;

                    case ConnectAcceptMessage m:
                        writer.Write(m.AssignedPlayerId);
                        writer.Write(m.AssignedColorIndex);
                        break;

                    case ConnectRejectMessage m:
                        writer.Write((byte)m.Reason);
                        break;

                    case PlayerJoinedMessage m:
                        writer.Write(m.PlayerId);
                        writer.Write(m.PlayerName ?? "");
                        writer.Write(m.ColorIndex);
                        break;

                    case PlayerLeftMessage m:
                        writer.Write(m.PlayerId);
                        break;

                    case PlayerInputMessage m:
                        writer.Write(m.SequenceNumber);
                        writer.Write(m.InputX);
                        writer.Write(m.InputZ);
                        writer.Write(m.Sprint);
                        break;

                    case PlayerStateMessage m:
                        writer.Write(m.PlayerId);
                        writer.Write(m.LastProcessedInput);
                        writer.Write(m.PosX);
                        writer.Write(m.PosY);
                        writer.Write(m.PosZ);
                        writer.Write(m.RotY);
                        writer.Write(m.IsMoving);
                        writer.Write(m.IsRunning);
                        writer.Write(m.IsPickingUp);
                        writer.Write(m.IsAlive);
                        break;

                    case WorldStateMessage m:
                        writer.Write(m.Tick);
                        WritePlayerSnapshots(writer, m.Players);
                        WriteMonsterSnapshot(writer, m.Monster);
                        break;

                    case WorldSnapshotMessage m:
                        WritePlayerSnapshots(writer, m.Players);
                        WriteMonsterSnapshot(writer, m.Monster);
                        int collectedCount = m.CollectedItemIds?.Length ?? 0;
                        writer.Write(collectedCount);
                        for (int i = 0; i < collectedCount; i++)
                            writer.Write(m.CollectedItemIds[i]);
                        writer.Write(m.TotalItems);
                        writer.Write(m.CurrentGameState);
                        break;

                    case MonsterStateMessage m:
                        writer.Write(m.PosX);
                        writer.Write(m.PosY);
                        writer.Write(m.PosZ);
                        writer.Write(m.RotY);
                        writer.Write(m.State);
                        break;

                    case ItemCollectedMessage m:
                        writer.Write(m.ItemId);
                        writer.Write(m.CollectorPlayerId);
                        break;

                    case ItemSpawnedMessage m:
                        writer.Write(m.ItemId);
                        writer.Write(m.PosX);
                        writer.Write(m.PosY);
                        writer.Write(m.PosZ);
                        break;

                    case GameStateChangeMessage m:
                        writer.Write(m.NewState);
                        break;

                    case GameRulesUpdateMessage m:
                        writer.Write(m.MaxPlayers);
                        writer.Write(m.MonsterSpeed);
                        writer.Write(m.TotalItems);
                        writer.Write(m.TimeLimit);
                        writer.Write(m.MonsterSpawnDelay);
                        break;

                    case PingRequestMessage m:
                        writer.Write(m.ClientTime);
                        break;

                    case PingResponseMessage m:
                        writer.Write(m.ClientTime);
                        writer.Write(m.TargetPlayerId);
                        break;

                    case KickPlayerMessage m:
                        writer.Write(m.TargetPlayerId);
                        writer.Write(m.Reason ?? "");
                        break;

                    case BanIPMessage m:
                        writer.Write(m.TargetPlayerId);
                        writer.Write(m.Reason ?? "");
                        break;

                    // PauseGameMessage, ResumeGameMessage, KeepAliveMessage, DisconnectMessage
                    // → no tienen payload adicional, solo el header común
                }

                return ms.ToArray();
            }
        }

        // ═════════════════════════════════════════════════════════
        //  HELPERS — WRITE
        // ═════════════════════════════════════════════════════════

        private static void WritePlayerSnapshots(BinaryWriter writer, PlayerSnapshot[] snapshots)
        {
            int count = snapshots?.Length ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                writer.Write(snapshots[i].PlayerId);
                writer.Write(snapshots[i].PosX);
                writer.Write(snapshots[i].PosY);
                writer.Write(snapshots[i].PosZ);
                writer.Write(snapshots[i].RotY);
                writer.Write(snapshots[i].ColorIndex);
                writer.Write(snapshots[i].IsAlive);
                writer.Write(snapshots[i].IsMoving);
                writer.Write(snapshots[i].IsRunning);
                writer.Write(snapshots[i].IsPickingUp);
            }
        }

        private static bool TryReadBool(BinaryReader reader, bool defaultValue = false)
        {
            return reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadBoolean() : defaultValue;
        }

        private static void WriteMonsterSnapshot(BinaryWriter writer, MonsterSnapshot monster)
        {
            writer.Write(monster.PosX);
            writer.Write(monster.PosY);
            writer.Write(monster.PosZ);
            writer.Write(monster.RotY);
            writer.Write(monster.State);
        }

        // ═════════════════════════════════════════════════════════
        //  HELPERS — READ
        // ═════════════════════════════════════════════════════════

        private static PlayerSnapshot[] ReadPlayerSnapshots(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var snapshots = new PlayerSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                snapshots[i].PlayerId = reader.ReadInt32();
                snapshots[i].PosX = reader.ReadSingle();
                snapshots[i].PosY = reader.ReadSingle();
                snapshots[i].PosZ = reader.ReadSingle();
                snapshots[i].RotY = reader.ReadSingle();
                snapshots[i].ColorIndex = reader.ReadInt32();
                snapshots[i].IsAlive = TryReadBool(reader, true);
                snapshots[i].IsMoving = TryReadBool(reader);
                snapshots[i].IsRunning = TryReadBool(reader);
                snapshots[i].IsPickingUp = TryReadBool(reader);
            }
            return snapshots;
        }

        private static MonsterSnapshot ReadMonsterSnapshot(BinaryReader reader)
        {
            return new MonsterSnapshot
            {
                PosX = reader.ReadSingle(),
                PosY = reader.ReadSingle(),
                PosZ = reader.ReadSingle(),
                RotY = reader.ReadSingle(),
                State = reader.ReadByte()
            };
        }

        // ── Read por tipo de mensaje ─────────────────────────────

        private static ConnectRequestMessage ReadConnectRequest(BinaryReader r, int sid, float ts)
        {
            return new ConnectRequestMessage
            {
                SenderId = sid, Timestamp = ts,
                PlayerName = r.ReadString(),
                GameVersion = r.ReadString()
            };
        }

        private static ConnectAcceptMessage ReadConnectAccept(BinaryReader r, int sid, float ts)
        {
            return new ConnectAcceptMessage
            {
                SenderId = sid, Timestamp = ts,
                AssignedPlayerId = r.ReadInt32(),
                AssignedColorIndex = r.ReadInt32()
            };
        }

        private static ConnectRejectMessage ReadConnectReject(BinaryReader r, int sid, float ts)
        {
            return new ConnectRejectMessage
            {
                SenderId = sid, Timestamp = ts,
                Reason = (RejectReason)r.ReadByte()
            };
        }

        private static PlayerJoinedMessage ReadPlayerJoined(BinaryReader r, int sid, float ts)
        {
            return new PlayerJoinedMessage
            {
                SenderId = sid, Timestamp = ts,
                PlayerId = r.ReadInt32(),
                PlayerName = r.ReadString(),
                ColorIndex = r.ReadInt32()
            };
        }

        private static PlayerLeftMessage ReadPlayerLeft(BinaryReader r, int sid, float ts)
        {
            return new PlayerLeftMessage
            {
                SenderId = sid, Timestamp = ts,
                PlayerId = r.ReadInt32()
            };
        }

        private static PlayerInputMessage ReadPlayerInput(BinaryReader r, int sid, float ts)
        {
            return new PlayerInputMessage
            {
                SenderId = sid, Timestamp = ts,
                SequenceNumber = r.ReadInt32(),
                InputX = r.ReadSingle(),
                InputZ = r.ReadSingle(),
                Sprint = r.ReadBoolean()
            };
        }

        private static PlayerStateMessage ReadPlayerState(BinaryReader r, int sid, float ts)
        {
            return new PlayerStateMessage
            {
                SenderId = sid, Timestamp = ts,
                PlayerId = r.ReadInt32(),
                LastProcessedInput = r.ReadInt32(),
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                PosZ = r.ReadSingle(),
                RotY = r.ReadSingle(),
                IsMoving = TryReadBool(r),
                IsRunning = TryReadBool(r),
                IsPickingUp = TryReadBool(r),
                IsAlive = TryReadBool(r, true)
            };
        }

        private static WorldStateMessage ReadWorldState(BinaryReader r, int sid, float ts)
        {
            return new WorldStateMessage
            {
                SenderId = sid, Timestamp = ts,
                Tick = r.ReadInt32(),
                Players = ReadPlayerSnapshots(r),
                Monster = ReadMonsterSnapshot(r)
            };
        }

        private static WorldSnapshotMessage ReadWorldSnapshot(BinaryReader r, int sid, float ts)
        {
            var msg = new WorldSnapshotMessage
            {
                SenderId = sid, Timestamp = ts,
                Players = ReadPlayerSnapshots(r),
                Monster = ReadMonsterSnapshot(r)
            };
            int collectedCount = r.ReadInt32();
            msg.CollectedItemIds = new int[collectedCount];
            for (int i = 0; i < collectedCount; i++)
                msg.CollectedItemIds[i] = r.ReadInt32();
            msg.TotalItems = r.ReadInt32();
            msg.CurrentGameState = r.ReadByte();
            return msg;
        }

        private static MonsterStateMessage ReadMonsterState(BinaryReader r, int sid, float ts)
        {
            return new MonsterStateMessage
            {
                SenderId = sid, Timestamp = ts,
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                PosZ = r.ReadSingle(),
                RotY = r.ReadSingle(),
                State = r.ReadByte()
            };
        }

        private static ItemCollectedMessage ReadItemCollected(BinaryReader r, int sid, float ts)
        {
            return new ItemCollectedMessage
            {
                SenderId = sid, Timestamp = ts,
                ItemId = r.ReadInt32(),
                CollectorPlayerId = r.ReadInt32()
            };
        }

        private static ItemSpawnedMessage ReadItemSpawned(BinaryReader r, int sid, float ts)
        {
            return new ItemSpawnedMessage
            {
                SenderId = sid, Timestamp = ts,
                ItemId = r.ReadInt32(),
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                PosZ = r.ReadSingle()
            };
        }

        private static GameStateChangeMessage ReadGameStateChange(BinaryReader r, int sid, float ts)
        {
            return new GameStateChangeMessage
            {
                SenderId = sid, Timestamp = ts,
                NewState = r.ReadByte()
            };
        }

        private static GameRulesUpdateMessage ReadGameRulesUpdate(BinaryReader r, int sid, float ts)
        {
            return new GameRulesUpdateMessage
            {
                SenderId = sid, Timestamp = ts,
                MaxPlayers = r.ReadInt32(),
                MonsterSpeed = r.ReadSingle(),
                TotalItems = r.ReadInt32(),
                TimeLimit = r.ReadSingle(),
                MonsterSpawnDelay = r.ReadSingle()
            };
        }

        private static PingRequestMessage ReadPingRequest(BinaryReader r, int sid, float ts)
        {
            return new PingRequestMessage
            {
                SenderId = sid, Timestamp = ts,
                ClientTime = r.ReadSingle()
            };
        }

        private static PingResponseMessage ReadPingResponse(BinaryReader r, int sid, float ts)
        {
            return new PingResponseMessage
            {
                SenderId = sid, Timestamp = ts,
                ClientTime = r.ReadSingle(),
                TargetPlayerId = r.ReadInt32()
            };
        }

        private static KickPlayerMessage ReadKickPlayer(BinaryReader r, int sid, float ts)
        {
            return new KickPlayerMessage
            {
                SenderId = sid, Timestamp = ts,
                TargetPlayerId = r.ReadInt32(),
                Reason = r.ReadString()
            };
        }

        private static BanIPMessage ReadBanIP(BinaryReader r, int sid, float ts)
        {
            return new BanIPMessage
            {
                SenderId = sid, Timestamp = ts,
                TargetPlayerId = r.ReadInt32(),
                Reason = r.ReadString()
            };
        }
    }
}
