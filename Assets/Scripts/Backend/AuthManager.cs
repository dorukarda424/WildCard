using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

                        Debug.Log("Player Money: " + money);

                        // GameManager.instance.playerMoney = money;
                        Photon.Pun.PhotonNetwork.NickName = username; 
                        GameManager.instance.loggedInPlayerName = username;
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