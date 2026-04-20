using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLostHill.Network.Host
{

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


        public bool IsBanned(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            return _bannedIPs.Contains(NormalizeIP(ip));
        }


        public IReadOnlyCollection<string> GetAll()
        {
            return _bannedIPs;
        }


        public int Count => _bannedIPs.Count;


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



        private static string NormalizeIP(string ip)
        {

            ip = ip.Trim();
            if (ip.StartsWith("::ffff:"))
                ip = ip.Substring(7);
            return ip;
        }
    }
}
