using UnityEngine;
using UnityEngine.UI; // Required for Slider
using Photon.Pun;
using System.Collections;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider loadingBar;

    [Header("Loading Screen Settings")]
    [Tooltip("How many seconds the loading screen should stay visible.")]
    [SerializeField] private float waitDuration = 3.0f;

    void Start()
    {
        // Reset the bar at start
        if (loadingBar != null) loadingBar.value = 0;

        // Everyone (Master and Clients) starts the visual animation
        StartCoroutine(AnimateLoadingBar());

        // Only the Master Client (or Offline) triggers the actual scene transition
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(LoadNextLevelDelayed());
        }
    }

    IEnumerator AnimateLoadingBar()
    {
        if (loadingBar == null) yield break;

        float elapsed = 0f;
        float currentProgress = 0f;

        while (elapsed < waitDuration)
        {
            elapsed += Time.deltaTime;
            
            // Calculate a "fake" target based on time, but add some randomness
            // This makes it jump to a random percentage (e.g., 30%, 65%, 90%) and pause
            float timePercent = elapsed / waitDuration;
            
            // Generate a target that is slightly ahead of time but capped at 0.98
            // (We save the final 100% for the actual scene load)
            float randomTarget = Mathf.Min(0.98f, timePercent + Random.Range(0.05f, 0.15f));
            
            // Smoothly move the bar towards the random target
            currentProgress = Mathf.MoveTowards(currentProgress, randomTarget, Time.deltaTime * 0.5f);
            loadingBar.value = currentProgress;

            // Randomly "pause" the bar for a few frames to simulate loading data
            if (Random.value > 0.95f) 
            {
                yield return new WaitForSeconds(Random.Range(0.1f, 0.3f));
            }

            yield return null;
        }

        loadingBar.value = 1f; // Snap to full right before transition
    }

    IEnumerator LoadNextLevelDelayed()
    {
        // Wait for the specified duration
        yield return new WaitForSeconds(waitDuration);

        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.nextSceneName))
        {
            string targetScene = GameManager.instance.nextSceneName;

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LoadLevel(targetScene);
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            Debug.LogWarning("[LoadingSceneController] Next scene name is missing in GameManager!");
        }
    }
}