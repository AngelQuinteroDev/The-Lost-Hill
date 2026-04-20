using System.Collections;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLostHill.Core
{
    /// <summary>
    /// Utilidad para cargar escenas asincrónicamente evitando bloqueos en el main thread
    /// que podrían desconectar sockets TCP/UDP.
    /// Muestra opcionalmente una pantalla de carga y expone los callbacks.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public event Action<string> OnSceneLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Inicia la carga asíncrona de una escena por su nombre.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            Debug.Log($"[SceneLoader] Iniciando carga de: {sceneName}");
            // Permitir lógica de animación de fade-out aquí

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            
            if (asyncLoad == null)
            {
                Debug.LogError($"[SceneLoader] ERROR: No se puede cargar la escena '{sceneName}'. ¿Está añadida en Build Settings?");
                yield break;
            }

            // Impide que la escena se active inmediatamente si se desea esperar sinc auto
            // asyncLoad.allowSceneActivation = false; 

            while (!asyncLoad.isDone)
            {
                // UI update the loading bar
                yield return null;
            }

            Debug.Log($"[SceneLoader] Escena {sceneName} cargada exitosamente.");
            
            OnSceneLoaded?.Invoke(sceneName);
            // Animación fade-in aquí
        }
    }
}
