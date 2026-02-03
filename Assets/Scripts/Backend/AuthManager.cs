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
                Debug.LogError("Hata: " + www.error);
                if (feedbackText) feedbackText.text = "Error connecting to server.";
            }
            else
            {
                string response = www.downloadHandler.text;
                Debug.Log(response);

                if (response.Contains("SUCCESS"))
                {
                    if (feedbackText) feedbackText.text = "Success!";


                    if (phpFile == "login.php")
                    {
                        GameManager.instance.loggedInPlayerName = username;
                        SceneManager.LoadScene("SampleScene");
                    }
                }
                else
                {
                    if (feedbackText) feedbackText.text = "Error: " + response;
                }
            }
        }
    }
}