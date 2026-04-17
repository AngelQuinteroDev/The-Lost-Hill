using System;

namespace TheLostHill.Gameplay.Player
{
    /// <summary>
    /// Datos básicos del jugador retenidos en memoria durante toda la sesión.
    /// Serializable para enviar el estado en resúmenes.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int PlayerId;
        public string Name;
        public int ColorIndex;
        public int Score;
        public bool IsAlive = true;

        public PlayerData(int id, string name, int color)
        {
            PlayerId = id;
            Name = name;
            ColorIndex = color;
            Score = 0;
            IsAlive = true;
        }
    }
}
