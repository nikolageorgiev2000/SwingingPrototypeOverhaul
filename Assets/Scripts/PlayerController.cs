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
        //quality settings for editor (^ perf on Mac)
        Screen.SetResolution(1280, 720, false);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 24;

        //intitialize arrays
        newRope = new bool[2];
        isHooked = new bool[2];
        ropes = new RopeConnector[2];

        //intitialize booleans
        newRope[0] = false;
        newRope[1] = false;
        isHooked[0] = false;
        isHooked[1] = false;

        //remove control of cursor and hide it to remove unwanted clicks outside game
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        //if player presses escape allow control of cursor
        if (Input.GetKey(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }

        //obtain WASD input info every frame
        horizDir = Input.GetAxisRaw("Horizontal");
        vertDir = Input.GetAxisRaw("Vertical");

        //if space pressed gain height
        if (Input.GetButtonDown("Jump"))
        {
            GetComponent<Rigidbody>().velocity += new Vector3(0, 30, 0);
        }

        //if rope spawn inputs are given record that a new rope is required
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
        //move rope spawning direction to highest angle
        controlDir = Quaternion.AngleAxis(85, Vector3.Cross(controlDir, Vector3.up)) * controlDir;

        Ray controlRay = new Ray(transform.position, controlDir);
        RaycastHit hit;
        float maxRopeLength = 400;
        //angle in degrees to search for a rope to the sides
        float searchWidth = 60;

        //total number of rays cast searching for hook point: vertRays * horizRays
        float vertRays = 15;
        float horizRays = 15;

        //when dir is -1 check to the left, when dir is 1 check to the right
        for (int i = 0; i < vertRays; i++)
        {
            //decrement vertical angle to a lower one if no hook point is found above
            controlDir = Quaternion.AngleAxis(-90 / vertRays, Vector3.Cross(controlDir, Vector3.up)) * controlDir;
            for (int j = 0; j * dir < horizRays; j += dir)
            {
                //if hook point found, connect rope to it
                if (Physics.Raycast(controlRay, out hit, maxRopeLength))
                {
                    return new RopeConnector(gameObject, hit.point);
                }
                //if no hook point found, move rope direction further to the side
                controlRay = new Ray(transform.position, Quaternion.Euler(0, (searchWidth / horizRays) * j, 0) * controlDir);
            }
        }
        //if no hook point found return null
        return null;
    }

    void FixedUpdate()
    {
        //player XZ movement control scale
        controlScale = 4;
        //camera direction on XZ plane
        Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, 0, 1)).normalized;
        //using user WASD input and camera direction on XZ plane add movement force
        //left and right are flipped so horizDir is negative
        Vector3 controlForce = controlScale * (vertDir * camDirXZ + -horizDir * Vector3.Cross(camDirXZ, Vector3.up)).normalized;
        GetComponent<Rigidbody>().AddForce(controlForce);

        for (int i = 0; i < newRope.Length; i++)
        {
            //if a new rope is requested try generating one. if unsuccessful, ropes[i] will be null and not hooked, otherwise hooked
            if (newRope[i])
            {
                ropes[i] = ropeGen(i);
                if (ropes[i] == null)
                {
                    isHooked[i] = false;

                }
                else
                {
                    isHooked[i] = true;
                }
                //IMPORTANT! a new rope is no longer requested
                newRope[i] = false;
            }
        }

        for (int i = 0; i < ropes.Length; i++)
        {
            //since ropes are destroyed within update, need to check it is not destroyed (using .destroyed)
            //because fixed update loop could occur between destroying the RopeConnector and setting ropes[i]=null;
            if (ropes[i] != null && !ropes[i].destroyed)
            {
                //swinging physics called in RopeConnector
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

        //overloads previous constructor and adds a hook point
        public RopeConnector(GameObject p, Vector3 h) : this(p)
        {
            GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hook.GetComponent<Collider>().enabled = false;
            hook.name = "hook-placeholder";
            hook.transform.position = h;

            hooks.Add(hook);

            hookDist = (player.transform.position - h).magnitude;
        }

        //overloads previous constructor, creating a hook point using a GameObject
        public RopeConnector(GameObject p, GameObject h) : this(p, h.transform.position)
        {

        }

        //swinging physics
        public void swing()
        {
            Vector3 v = player.GetComponent<Rigidbody>().velocity;
            Vector3 dir = (getCurrentHook().transform.position - player.transform.position).normalized;
            float tempDist = (player.transform.position - getCurrentHook().transform.position).magnitude;
            float m = player.GetComponent<Rigidbody>().mass;
            Debug.Log(tempDist - hookDist);
            //if the player distance from the hook exceeds that of the rope length remove velocity component that would cause it to increase
            //and add centripetal force to swing player realistically (F = mv^2/r)
            if (tempDist >= hookDist)
            {
                player.GetComponent<Rigidbody>().velocity = v - Vector3.Project(v, dir);
                Vector3 cForce = dir * m * v.magnitude * v.magnitude / hookDist;
                player.GetComponent<Rigidbody>().AddForce(cForce - Vector3.Project(Physics.gravity, dir) * m);
            }

            //if a collider intersects the rope at a distance less than 1, cut rope, else change hook point to intersection (bending rope)
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

        //creates a gameObject with a LineRenderer for consistent drawing of lines
        public GameObject createLine()
        {
            GameObject line = new GameObject();
            line.name = "linerenderer";
            line.AddComponent<LineRenderer>();
            line.GetComponent<LineRenderer>().startWidth = 0.1f;
            return line;
        }

        //draw lines between the hooks and player
        public void drawLines()
        {
            for (int i = 0; i < lines.Count - 1; i++)
            {
                lines[i].GetComponent<LineRenderer>().SetPositions(new Vector3[] { hooks[i].transform.position, hooks[i + 1].transform.position });
            }
            lines[lines.Count - 1].GetComponent<LineRenderer>().SetPositions(new Vector3[] { player.transform.position, getCurrentHook().transform.position });
        }

        //destroys gameObjects to save memory (^ perf)
        public void destroyRope()
        {
            //marks the ropeConnector object as destroyed due to lack of sync between FixedUpdate (physics) and Update (controls)
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

        //changes the hook point and updates the rope length (swinging around new hook)
        public void changeHook(GameObject h)
        {
            hooks.Add(h);
            hookDist = (player.transform.position - getCurrentHook().transform.position).magnitude;

        }

        //uses the previous changeHook method but creates a gameObject to hook to a given point
        public void changeHook(Vector3 h)
        {
            lines.Add(createLine());

            GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prim.transform.position = h;
            prim.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            prim.GetComponent<Collider>().enabled = false;
            changeHook(prim);
        }

        //retreived the hook currently swinging around
        public GameObject getCurrentHook()
        {
            return hooks[hooks.Count - 1];
        }

    }

}
