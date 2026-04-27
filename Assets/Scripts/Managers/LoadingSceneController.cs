using UnityEngine;
using Photon.Pun;
using System.Collections;

public class LoadingSceneController : MonoBehaviour
{
    void Start()
    {
        // Only the Master Client should trigger the next level load
        // because AutomaticallySyncScene is true.
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(LoadNextLevelDelayed());
        }
    }

    IEnumerator LoadNextLevelDelayed()
    {
        // Optional: Add a small delay if the scene loads too fast 
        // to let the loading animation play for at least a second.
        yield return new WaitForSeconds(1.0f);

        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.nextSceneName))
        {
            PhotonNetwork.LoadLevel(GameManager.instance.nextSceneName);
        }
    }
}