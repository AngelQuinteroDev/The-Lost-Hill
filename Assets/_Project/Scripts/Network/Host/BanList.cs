using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLostHill.Network.Host
{
    /// <summary>
    /// Lista de IPs baneadas con persistencia en JSON.
    /// Se guarda en Application.persistentDataPath para sobrevivir reinicios.
    /// </summary>
    public class BanList
    {
        private HashSet<string> _bannedIPs = new HashSet<string>();
        private string _filePath;

        [Serializable]
        private class BanListData
        {
            public List<string> bannedIPs = new List<string>();
        }

        public BanList()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "banlist.json");
            Load();
        }

        // ═════════════════════════════════════════════════════════
        //  OPERACIONES
        // ═════════════════════════════════════════════════════════

        /// <summary>Banea una dirección IP y guarda la lista.</summary>
        public void Ban(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;

            string normalized = NormalizeIP(ip);
            if (_bannedIPs.Add(normalized))
            {
                Save();
                Debug.Log($"[BanList] IP baneada: {normalized}");
            }
        }

        /// <summary>Desbanea una dirección IP y guarda la lista.</summary>
        public void Unban(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;

            string normalized = NormalizeIP(ip);
            if (_bannedIPs.Remove(normalized))
            {
                Save();
                Debug.Log($"[BanList] IP desbaneada: {normalized}");
            }
        }

        /// <summary>Verifica si una IP está baneada.</summary>
        public bool IsBanned(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            return _bannedIPs.Contains(NormalizeIP(ip));
        }

        /// <summary>Obtiene todas las IPs baneadas.</summary>
        public IReadOnlyCollection<string> GetAll()
        {
            return _bannedIPs;
        }

        /// <summary>Número de IPs baneadas.</summary>
        public int Count => _bannedIPs.Count;

        // ═════════════════════════════════════════════════════════
        //  PERSISTENCIA
        // ═════════════════════════════════════════════════════════

        /// <summary>Carga la lista desde disco.</summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonUtility.FromJson<BanListData>(json);
                    _bannedIPs = new HashSet<string>(data.bannedIPs);
                    Debug.Log($"[BanList] Cargada: {_bannedIPs.Count} IPs baneadas.");
                }
                else
                {
                    _bannedIPs = new HashSet<string>();
                    Debug.Log("[BanList] No se encontró archivo, creando lista vacía.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BanList] Error al cargar: {e.Message}");
                _bannedIPs = new HashSet<string>();
            }
        }

        /// <summary>Guarda la lista a disco.</summary>
        public void Save()
        {
            try
            {
                var data = new BanListData { bannedIPs = new List<string>(_bannedIPs) };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BanList] Error al guardar: {e.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════
        //  UTILIDADES
        // ═════════════════════════════════════════════════════════

        private static string NormalizeIP(string ip)
        {
            // Eliminar espacios y mapeo IPv6 de IPv4
            ip = ip.Trim();
            if (ip.StartsWith("::ffff:"))
                ip = ip.Substring(7);
            return ip;
        }
    }
}
