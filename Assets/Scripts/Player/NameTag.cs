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

        // Set the text to the Photon NickName
        if (nameText != null && photonView.Owner != null)
        {
            nameText.text = photonView.Owner.NickName;
        }
        
        // Cache the main camera transform for billboarding
        if (Camera.main != null)
        {
            _mainCamTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        // Billboarding: Make the name tag face the camera
        if (_mainCamTransform != null)
        {
            transform.LookAt(transform.position + _mainCamTransform.forward);
        }
        else if (Camera.main != null)
        {
            _mainCamTransform = Camera.main.transform;
        }
    }
}
