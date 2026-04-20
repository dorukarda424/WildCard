using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class AuthManager : MonoBehaviour
{
 
    private string baseUrl = "https://doruk-rogue-api-hcd2ckbyavdhgbdh.swedencentral-01.azurewebsites.net/";

    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TextMeshProUGUI feedbackText;

    public void OnRegisterClick()
    {
        StartCoroutine(Auth(usernameField.text, passwordField.text, "register.php"));
    }

    public void OnLoginClick()
    {
        StartCoroutine(Auth(usernameField.text, passwordField.text, "login.php"));
    }

    IEnumerator Auth(string username, string password, string phpFile)
    {
        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (UnityWebRequest www = UnityWebRequest.Post(baseUrl + phpFile, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Network Error: " + www.error);
                if (feedbackText) feedbackText.text = "Error connecting to server.";
            }
            else
            {
                string response = www.downloadHandler.text;
                Debug.Log("Server Response: " + response);

                if (response.Contains("SUCCESS") || response.Contains("Login Successful"))
                {
                    if (feedbackText) feedbackText.text = "Success!";
                    Debug.Log("Operation Successful!");

                    if (phpFile == "login.php")
                    {

                        string[] parts = response.Split(':');


                        int money = 0;
                        if (parts.Length > 1)
                        {

                            int.TryParse(parts[1].Trim(), out money);
                        }

                        Debug.Log("Player Rank: " + money);

                        PhotonNetwork.NickName = username;
                        GameManager.instance.loggedInPlayerName = username;
                        GameManager.instance.playerRank = money;

                        // Sync rank to Photon so other players can see it
                        Hashtable props = new Hashtable { { "rank", money } };
                        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

                        SceneManager.LoadScene("LobbyScene");
                    }
                }
                else
                {
                    if (feedbackText) feedbackText.text = response;
                    Debug.LogError("Server Error: " + response);
                }

            }
        }
    }
}