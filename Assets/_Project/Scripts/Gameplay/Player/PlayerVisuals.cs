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
        
        [Header("Animation Sync")]
        public Animator CharacterAnimator;
        public string WalkParam = "isWalking";
        public string RunParam = "isRunning";
        public string IdleParam = "isIdle";
        public string PickupParam = "isPickingUp";

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

        public void SetAnimationState(bool isMoving, bool isRunning, bool isPickingUp, bool isAlive = true)
        {
            if (CharacterAnimator == null)
                CharacterAnimator = GetComponentInChildren<Animator>();
            if (CharacterAnimator == null) return;

            bool walking = isMoving && !isRunning && !isPickingUp;
            bool idle = !isMoving && !isPickingUp && isAlive;

            SetBoolIfExists(RunParam, isRunning && isAlive);
            SetBoolIfExists(WalkParam, walking && isAlive);
            SetBoolIfExists(PickupParam, isPickingUp && isAlive);
            SetBoolIfExists(IdleParam, idle);
        }

        private void SetBoolIfExists(string param, bool value)
        {
            if (string.IsNullOrEmpty(param) || CharacterAnimator == null) return;

            foreach (var p in CharacterAnimator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                {
                    CharacterAnimator.SetBool(param, value);
                    return;
                }
            }
        }

        // TODO: Agregar animador para sincronizar bools como IsWalking, IsSprinting
        // SetAnimationState()
    }
}
