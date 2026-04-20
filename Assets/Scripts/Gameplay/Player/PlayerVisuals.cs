using UnityEngine;

namespace TheLostHill.Gameplay.Player
{

    public class PlayerVisuals : MonoBehaviour
    {
        [Header("References")]
        public Renderer[] MeshRenderers; 
        
        [Header("Colors Mapping")]
        public Color[] PlayerColors;     
        
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

        
    }
}
