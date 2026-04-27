using UnityEngine;
using TMPro;
using Photon.Pun;

public class PlayerNameTag : MonoBehaviourPun
{
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

    private Transform _mainCamTransform;

    void Start()
    {
        // If this is the local player, hide the name tag so it doesn't block the view
        if (photonView.IsMine)
        {
            gameObject.SetActive(false);
            return;
        }

        // Apply the missing offset
        transform.localPosition = offset;

        // Set the text to the Photon NickName initially
        UpdateNameText();
        
        // Cache the main camera transform for billboarding
        if (Camera.main != null)
        {
            _mainCamTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        // Ensure the name is set if it was missing during Start
        if (nameText != null && string.IsNullOrEmpty(nameText.text))
        {
            UpdateNameText();
        }

        // Billboarding: Make the name tag face the camera
        if (_mainCamTransform != null)
        {
            // Simple rotation match often works best for TMP billboarding
            transform.rotation = _mainCamTransform.rotation;
        }
        else if (Camera.main != null)
        {
            _mainCamTransform = Camera.main.transform;
        }
    }

    private void UpdateNameText()
    {
        if (nameText != null && photonView.Owner != null && !string.IsNullOrEmpty(photonView.Owner.NickName))
        {
            nameText.text = photonView.Owner.NickName;
        }
    }
}
