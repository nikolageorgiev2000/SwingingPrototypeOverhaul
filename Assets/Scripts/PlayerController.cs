using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    public GameObject hookL;
    public GameObject hookR;
    public GameObject playerCamera;

    private float horizDir;
    private float vertDir;
    private float controlScale;

    bool[] newRope;
    bool[] isHooked;
    RopeConnector[] ropes;

    // Start is called before the first frame update
    void Start()
    {
        Screen.SetResolution(1280, 720, false);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 24;

        newRope = new bool[2];
        isHooked = new bool[2];
        ropes = new RopeConnector[2];
        isHooked[0] = false; isHooked[1] = false;

        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }

        horizDir = Input.GetAxisRaw("Horizontal");
        vertDir = Input.GetAxisRaw("Vertical");

        if (Input.GetButtonDown("Jump"))
        {
            GetComponent<Rigidbody>().velocity += new Vector3(0,30,0);
        }

        //Connect ropes
        if (!isHooked[0] && Input.GetButtonDown("Fire2"))
        {
            newRope[0] = true;
        }

        if (!isHooked[1] && Input.GetButtonDown("Fire1"))
        {
            newRope[1] = true;
        }

        //Disconnect ropes
        if (isHooked[0] && !Input.GetButton("Fire2"))
        {
            isHooked[0] = false;
            ropes[0].destroyRope();
            ropes[0] = null;
        }

        if (isHooked[1] && !Input.GetButton("Fire1"))
        {
            isHooked[1] = false;
            ropes[1].destroyRope();
            ropes[1] = null;
        }

        //need to draw lines in sync with frame rate rather than physics calculations or else there is flickering
        for (int i = 0; i < isHooked.Length; i++)
        {
            if (ropes[i] != null && !ropes[i].destroyed)
            {
                ropes[i].drawLines();
            }
        }
    }

    public RopeConnector ropeGen(int hookIndex)
    {
        //-1+2*hookIndex leads to dir = -1; +1 for hookIndex input into ropeGen
        int dir = 1 - hookIndex * 2;

        Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, 0, 1)).normalized;
        //((vertDir+horizDir != 0) ? vertDir : 1) assumes attaching rope in direction of camera if no direction of XZ motion is given
        Vector3 controlDir = ((vertDir + horizDir != 0) ? vertDir : 1) * camDirXZ + -((vertDir + horizDir != 0) ? horizDir : 0) * Vector3.Cross(camDirXZ, Vector3.up);
        controlDir = Quaternion.AngleAxis(85, Vector3.Cross(controlDir, Vector3.up)) * controlDir;

        Ray controlRay = new Ray(transform.position, controlDir);
        RaycastHit hit;
        float maxRopeLength = 400;
        float searchWidth = 60;
        float vertRays = 10;
        float horizRays = 10;

        //when dir is -1 check to the left, when dir is 1 check to the right
        for (int i = 0; i < vertRays; i++)
        {
            controlDir = Quaternion.AngleAxis(-90/vertRays, Vector3.Cross(controlDir, Vector3.up)) * controlDir;
            for (int j = 0; j * dir < horizRays; j += dir)
            {
                if (Physics.Raycast(controlRay, out hit, maxRopeLength))
                {
                    return new RopeConnector(gameObject, hit.point);
                }
                controlRay = new Ray(transform.position, Quaternion.Euler(0, (searchWidth / horizRays) * j, 0) * controlDir);
                Debug.Log(controlRay);
            }
        }
        return null;
    }

    void FixedUpdate()
    {
        //player XZ control
        controlScale = 4;
        Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, 0, 1)).normalized;
        //left and right are flipped so horizDir is negative
        Vector3 controlForce = controlScale * (vertDir * camDirXZ + -horizDir * Vector3.Cross(camDirXZ, Vector3.up)).normalized;
        GetComponent<Rigidbody>().AddForce(controlForce);

        for (int i = 0; i < newRope.Length; i++)
        {
            if (newRope[i])
            {
                ropes[i] = ropeGen(i);
                if (ropes[i] == null)
                {
                    isHooked[i] = false;

                } else
                {
                    isHooked[i] = true;
                }
                newRope[i] = false;
            }
        }

        for (int i = 0; i < ropes.Length; i++)
        {
            //since ropes are destroyed within update, need to check it is not destroyed (using .destroyed)
            //because fixed update loop could occur between destroying the RopeConnector and setting ropes[i]=null;
            if (ropes[i] != null && !ropes[i].destroyed)
            {
                ropes[i].swing();
            }
        }
    }

    public class RopeConnector
    {
        public bool destroyed;
        public float hookDist;
        private GameObject player;
        private List<GameObject> lines;
        private List<GameObject> hooks;

        RopeConnector(GameObject p)
        {
            destroyed = false;
            player = p;
            lines = new List<GameObject>();
            lines.Add(createLine());

            hooks = new List<GameObject>();
        }

        public RopeConnector(GameObject p, Vector3 h) : this(p)
        {
            GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hook.GetComponent<Collider>().enabled = false;
            hook.name = "hook-placeholder";
            hook.transform.position = h;

            hooks.Add(hook);

            hookDist = (player.transform.position - h).magnitude;
        }

        public RopeConnector(GameObject p, GameObject h) : this(p, h.transform.position)
        {

        }

        public void swing()
        {
            Vector3 v = player.GetComponent<Rigidbody>().velocity;
            Vector3 dir = (getCurrentHook().transform.position - player.transform.position).normalized;
            float tempDist = (player.transform.position - getCurrentHook().transform.position).magnitude;
            float m = player.GetComponent<Rigidbody>().mass;
            Debug.Log(tempDist - hookDist);
            if (tempDist >= hookDist)
            {
                player.GetComponent<Rigidbody>().velocity = v - Vector3.Project(v, dir);
                Vector3 cForce = dir * m * v.magnitude * v.magnitude / hookDist;
                player.GetComponent<Rigidbody>().AddForce(cForce - Vector3.Project(Physics.gravity, dir) * m);
            }

            RaycastHit hit;
            if (Physics.Raycast(player.transform.position, (getCurrentHook().transform.position - player.transform.position).normalized, out hit, (getCurrentHook().transform.position - player.transform.position).magnitude - 0.2f))
            {
                if ((hit.point - player.transform.position).magnitude < 1)
                {
                    destroyRope();
                }
                else
                {
                    changeHook(hit.point);
                }
            }

        }

        public GameObject createLine()
        {
            GameObject line = new GameObject();
            line.name = "linerenderer";
            line.AddComponent<LineRenderer>();
            line.GetComponent<LineRenderer>().startWidth = 0.1f;
            return line;
        }

        public void drawLines()
        {
            for (int i = 0; i < lines.Count - 1; i++)
            {
                lines[i].GetComponent<LineRenderer>().SetPositions(new Vector3[] {hooks[i].transform.position, hooks[i + 1].transform.position });
            }
            lines[lines.Count - 1].GetComponent<LineRenderer>().SetPositions(new Vector3[] { player.transform.position, getCurrentHook().transform.position });
        }

        public void destroyRope()
        {

            destroyed = true;

            foreach (GameObject h in hooks)
            {
                Destroy(h.gameObject);
            }
            foreach (GameObject l in lines)
            {
                Destroy(l.gameObject);
            }
        }

        public void changeHook(GameObject h)
        {
            hooks.Add(h);
            hookDist = (player.transform.position - getCurrentHook().transform.position).magnitude;

        }

        public GameObject getCurrentHook()
        {
            return hooks[hooks.Count - 1];
        }

        public void changeHook(Vector3 h)
        {
            lines.Add(createLine());

            GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prim.transform.position = h;
            prim.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            prim.GetComponent<Collider>().enabled = false;
            changeHook(prim);
        }

    }


}
