using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using TheLostHill.Core;
using TheLostHill.Network.Shared;
using TheLostHill.Network.Host;
using TheLostHill.Network.Client;

public class ItemCounter : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI counterText;

    private Dictionary<int, CollectibleItem> itemsMap = new Dictionary<int, CollectibleItem>();

    public int TotalItems { get; private set; }
    public int CollectedCount { get; private set; }

    public static ItemCounter Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Encontrar y ordenar por posicin de forma determinista
        var foundItems = FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None)
                            .OrderBy(i => i.transform.position.sqrMagnitude)
                            .ToArray();
        TotalItems = foundItems.Length;

        for (int i = 0; i < foundItems.Length; i++)
        {
            foundItems[i].InitializeNetworkId(i);
            itemsMap[i] = foundItems[i];
        }

        UpdateUI();

        // Suscribirse a los mensajes del GameManager activo
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsHost && GameManager.Instance.HostManager != null)
            {
                GameManager.Instance.HostManager.OnMessageReceived += OnHostMessageReceived;
            }
            if (GameManager.Instance.ClientHandler != null)
            {
                GameManager.Instance.ClientHandler.OnMessageReceived += OnClientMessageReceived;
            }
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsHost && GameManager.Instance.HostManager != null)
                GameManager.Instance.HostManager.OnMessageReceived -= OnHostMessageReceived;

            if (GameManager.Instance.ClientHandler != null)
                GameManager.Instance.ClientHandler.OnMessageReceived -= OnClientMessageReceived;
        }
        if (Instance == this) Instance = null;
    }

    public void HandleLocalPickupAttempt(int itemId)
    {
        if (!itemsMap.ContainsKey(itemId)) return;

        // El Host lo procesa inmediatamente
        if (GameManager.Instance.IsHost)
        {
            ProcessHostCollection(itemId, GameManager.Instance.LocalPlayerId);
        }
        // Los clientes mandan el mensaje al Host
        else if (GameManager.Instance.ClientHandler != null && GameManager.Instance.ClientHandler.IsConnected)
        {
            var msg = new ItemCollectedMessage
            {
                ItemId = itemId,
                CollectorPlayerId = GameManager.Instance.LocalPlayerId,
                Timestamp = Time.unscaledTime
            };
            GameManager.Instance.ClientHandler.Send(msg);
        }
    }

    private void OnHostMessageReceived(NetworkMessage msg)
    {
        // El Host aprueba los request de recoleccin de los clientes
        if (msg is ItemCollectedMessage pickupReq)
        {
            ProcessHostCollection(pickupReq.ItemId, pickupReq.CollectorPlayerId);
        }
    }

    private void ProcessHostCollection(int itemId, int collectorId)
    {
        if (itemsMap.ContainsKey(itemId))
        {
            // Lo eliminamos localmente (para el host)
            ConfirmCollection(itemId);

            // Avisamos a toda la sala de red (clientes) que ha sido recogido
            if (GameManager.Instance.HostManager != null)
            {
                var broadcastMsg = new ItemCollectedMessage
                {
                    SenderId = 0, // Sender id 0 es el Host Server
                    ItemId = itemId,
                    CollectorPlayerId = collectorId,
                    Timestamp = Time.unscaledTime
                };
                GameManager.Instance.HostManager.Broadcast(broadcastMsg);
            }
        }
    }

    private void OnClientMessageReceived(NetworkMessage msg)
    {
        // Ignora si somos Host, pues ya se recogi por proceso directo  
        if (GameManager.Instance != null && GameManager.Instance.IsHost) return;

        switch (msg)
        {
            case ItemCollectedMessage confirmMsg:
                ConfirmCollection(confirmMsg.ItemId);
                break;
            
            case WorldSnapshotMessage snap:
                if (snap.CollectedItemIds != null)
                {
                    foreach (int cid in snap.CollectedItemIds)
                    {
                        ConfirmCollection(cid);
                    }
                }
                break;
        }
    }

    private void ConfirmCollection(int itemId)
    {
        if (itemsMap.TryGetValue(itemId, out CollectibleItem item) && item != null)
        {
            item.ConfirmCollection();
            itemsMap.Remove(itemId);
            CollectedCount++;
            UpdateUI();
            
            // Check win condition?
            // if (CollectedCount == TotalItems) TriggerWin();
        }
    }

    public int[] GetCollectedItemIds()
    {
        var collectedIds = new List<int>();
        for (int i = 0; i < TotalItems; i++)
        {
            if (!itemsMap.ContainsKey(i)) collectedIds.Add(i);
        }
        return collectedIds.ToArray();
    }

    public void OnItemCollected()
    {
        // Legacy Support para la versin antigua de CollectibleItem, por si acaso algun script la llama
        CollectedCount++;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (counterText != null)
            counterText.text = CollectedCount + " / " + TotalItems;
    }
}