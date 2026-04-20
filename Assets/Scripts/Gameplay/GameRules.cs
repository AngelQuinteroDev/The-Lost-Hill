using System;
using UnityEngine;

namespace TheLostHill.Gameplay
{

    [Serializable]
    public class GameRules
    {
  
        public int MaxPlayers = 4;
        
    
        public float MonsterSpeed = 5.0f;
        public float MonsterSpawnDelay = 30f; 
        
      
        public int TotalItemsToFind = 10;
        public float TimeLimitMinutes = 15f; 

       
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
