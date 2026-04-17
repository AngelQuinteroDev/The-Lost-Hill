using UnityEngine;

namespace TheLostHill.Gameplay.Player
{
    /// <summary>
    /// Maneja el cambio de aspecto, animaciones y colores de la unidad para 
    /// que cada jugador se vea distinto, acorde con su ColorIndex.
    /// </summary>
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("References")]
        public Renderer[] MeshRenderers; // Las partes del modelo que cambian de color
        
        [Header("Colors Mapping")]
        public Color[] PlayerColors;     // Preset de 8 colores en el inspector
        
        private int _currentColorIndex = -1;

        public void SetColorIndex(int colorIndex)
        {
            if (colorIndex == _currentColorIndex) return;

            if (colorIndex >= 0 && colorIndex < PlayerColors.Length)
            {
                _currentColorIndex = colorIndex;
                Color designatedColor = PlayerColors[colorIndex];

                foreach (Renderer r in MeshRenderers)
                {
                    if (r != null && r.material != null)
                    {
                        // En URP generalmente BaseColor es _BaseColor
                        r.material.SetColor("_BaseColor", designatedColor);
                    }
                }
            }
        }

        // TODO: Agregar animador para sincronizar bools como IsWalking, IsSprinting
        // SetAnimationState()
    }
}
