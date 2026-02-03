using UnityEngine;
using Photon.Pun;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 7f;

    PhotonView view;
    Rigidbody rb;

    void Start()
    {
        view = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!view.IsMine)
            return;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(x, 0, z) * speed * Time.deltaTime;
        transform.Translate(move);


        if (Input.GetButtonDown("Jump") && Mathf.Abs(rb.linearVelocity.y) < 0.01f)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}