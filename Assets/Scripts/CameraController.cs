using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    public GameObject ReferenceObject;

    private Vector3 offset;

    // Start is called before the first frame update
    void Start()
    {
        offset = transform.position - ReferenceObject.transform.position;
    }


    void FixedUpdate()
    {
        if (!Physics.Raycast(transform.position, - Input.GetAxisRaw("Mouse X") * Vector3.Cross(offset, Vector3.up), 2))
        {
            offset = Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse X"), Vector3.up) * offset;
        }
        if (!Physics.Raycast(transform.position, Input.GetAxisRaw("Mouse Y") * Vector3.Cross(offset, Vector3.right), 10))
        {
            Vector3 offsetHoriz = new Vector3(offset[0], 0, offset[2]);
            if (Vector3.Angle(Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse Y"), Vector3.Cross(Vector3.up, offsetHoriz)) * offset, offsetHoriz) < 80)
            {
                offset = Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse Y"), Vector3.Cross(Vector3.up, offset)) * offset;
            }
        }
        transform.position = ReferenceObject.transform.position + offset;
        transform.LookAt(ReferenceObject.transform.position);

        float minFOV = 60;
        float maxFOV = 110;
        //uses player velocity to widen field of view, with max FOV at 75+30 = 105 and max velocity 200
        GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, minFOV + (ReferenceObject.GetComponent<Rigidbody>().velocity.magnitude / 200) * (maxFOV - minFOV), Time.deltaTime);
    }
}