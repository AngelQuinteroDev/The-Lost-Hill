using System;

namespace TheLostHill.Gameplay
{
    /// <summary>
    /// Reglas globales modificables por el Host en cualquier momento.
    /// Serializable a JSON.
    /// </summary>
    [Serializable]
    public class GameRules
    {
        // Límites
        public int MaxPlayers = 8;
        
        // Dificultad general
        public float MonsterSpeed = 5.0f;
        public float MonsterSpawnDelay = 30f; // Tiempo antes de que empiece a cazar
        
        // Objetivos
        public int TotalItemsToFind = 10;
        public float TimeLimitMinutes = 15f; // 0 = sin límite

        // Funciones utilitarias
        public static GameRules Default()
        {
            return new GameRules();
        }

        public void LoadFromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}
