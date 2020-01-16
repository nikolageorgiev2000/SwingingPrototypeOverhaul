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


    void LateUpdate()
    {
        offset = Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse X"), Vector3.up) * offset;
        Vector3 offsetHoriz = new Vector3(offset[0], 0, offset[2]);
        if (Vector3.Angle(Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse Y"), Vector3.Cross(Vector3.up, offsetHoriz)) * offset, offsetHoriz) < 80)
        {
            offset = Quaternion.AngleAxis(100 * Time.deltaTime * Input.GetAxisRaw("Mouse Y"), Vector3.Cross(Vector3.up, offset)) * offset;
        }
        transform.position = ReferenceObject.transform.position + offset;
        transform.LookAt(ReferenceObject.transform.position);
    }

    public Vector3 getXYDirection()
    {
        return -offset.normalized;
    }
}