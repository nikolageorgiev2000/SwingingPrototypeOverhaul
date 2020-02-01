using System;
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

    private bool colliding;

    bool jump;
    bool[] newRope;
    bool[] isHooked;
    RopeConnector[] ropes;
    Vector3[] tempAngularVelocity;

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
        jump = false;

        //intitialize booleans
        newRope[0] = false;
        newRope[1] = false;
        isHooked[0] = false;
        isHooked[1] = false;

        colliding = false;

        tempAngularVelocity = new Vector3[2];

        //remove control of cursor and hide it to remove unwanted clicks outside game
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        GetComponent<Animator>().SetBool("IsGrounded", isGrounded());
        GetComponent<Animator>().SetFloat("InputHorizontal", horizDir);
        GetComponent<Animator>().SetFloat("InputVertical", vertDir);
        GetComponent<Animator>().SetFloat("InputMagnitude", GetComponent<Rigidbody>().velocity.magnitude);
        GetComponent<Animator>().SetBool("IsStrafing", false);
        GetComponent<Animator>().SetBool("IsSprinting", true);
        GetComponent<Animator>().SetFloat("GroundDistance", transform.position.y);



        //if player presses escape allow control of cursor
        if (Input.GetKey(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }

        //obtain WASD input info every frame
        horizDir = Input.GetAxisRaw("Horizontal");
        vertDir = Input.GetAxisRaw("Vertical");

        //if space pressed gain height
        if (Input.GetButtonDown("Jump") && Physics.Raycast(transform.position, -Vector3.up, 5))
        {
            jump = true;
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
        //-1+2*hookIndex leads to handDir = -1; +1 for hookIndex input into ropeGen, or left or right handed rope swing
        int handDir = 1 - hookIndex * 2;

        Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, 0, 1)).normalized;
        //the XZ direction of player input
        //((vertDir+horizDir != 0) ? vertDir : 1) assumes attaching rope in direction of camera if no direction of XZ motion is given
        Vector3 inputDir = ((vertDir + horizDir != 0) ? vertDir : 1) * camDirXZ + -((vertDir + horizDir != 0) ? horizDir : 0) * Vector3.Cross(camDirXZ, Vector3.up);
        //move rope spawning direction to highest angle in direction of player's movement input
        Vector3 controlDir = Quaternion.AngleAxis(85, Vector3.Cross(inputDir, Vector3.up)) * inputDir;

        Ray controlRay = new Ray(transform.position, controlDir);
        RaycastHit hit;
        float maxRopeLength = 300;
        //angle in degrees to search for a rope to the sides
        float searchWidth = 60;

        //total number of rays cast searching for hook point: vertRays * horizRays
        float vertRays = 25;
        float horizRays = 25;

        Vector3 optimalHitPoint = Vector3.zero;
        float maxRopeFitness = -Mathf.Infinity;

        //when dir is -1 check to the left, when dir is 1 check to the right
        for (int i = 0; i < vertRays; i++)
        {
            //decrement vertical angle to a lower one if no hook point is found above
            controlDir = Quaternion.AngleAxis(-90 / vertRays, Vector3.Cross(controlDir, Vector3.up)) * controlDir;
            for (int j = 0; j * handDir < horizRays; j += handDir)
            {
                //if hook point found, connect rope to it
                if (Physics.Raycast(controlRay, out hit, maxRopeLength))
                {
                    if (ropeFitness(controlDir, hit.point, inputDir, maxRopeFitness) > maxRopeFitness)
                    {
                        optimalHitPoint = hit.point;
                        maxRopeFitness = ropeFitness(controlDir, hit.point, inputDir, maxRopeFitness);
                    }
                }
                //if no hook point found, move rope direction further to the side
                controlRay = new Ray(transform.position, Quaternion.Euler(0, (searchWidth / horizRays) * j, 0) * controlDir);
            }
        }
        if (optimalHitPoint != Vector3.zero)
        {
            return new RopeConnector(gameObject, optimalHitPoint);
        }
        else
        {
            //if no hook point found return null
            return null;
        }
    }

    float ropeFitness(Vector3 ropeDir, Vector3 hookPoint, Vector3 inputDir, float maxRopeFitness)
    {
        inputDir = inputDir.normalized;
        ropeDir = ropeDir.normalized;
        Vector3 vDir = GetComponent<Rigidbody>().velocity.normalized;
        //percent of speed in the XZ plane (checks that if the player is moving horizontally quickly or if the player is falling flat webs can be used, but not if moving slowly horizontally)
        float horizSpeedPercent = Vector3.Scale(GetComponent<Rigidbody>().velocity, new Vector3(1, 0, 1)).magnitude / GetComponent<Rigidbody>().velocity.magnitude;
        //preferred length of the rope vs hook height off the ground (when hanging will be (1-ropeHeightRatio) of the way from the ground to the hook
        float ropeHeightRatio = 0.75f;
        float preferredHangHeight = ropeHeightRatio * hookPoint.y;
        //prefer higher points when the player is closer to being underneath them (avoids selecting points right next to player which negate forward motion)
        //old *Math.Max(0, (1 - (Math.Abs(preferredHangHeight - (hookPoint - transform.position).magnitude) / preferredHangHeight)))
        float heightPref = ((hookPoint.y - transform.position.y) / hookPoint.y) * (Math.Abs(hookPoint.y - transform.position.y) / (transform.position - hookPoint).magnitude);
        //prefer rope in direction perpendicular to player motion (used for horizontal ropes while falling)
        //not used if the player's horizontal speed is slow but a large part of the velocity because then higher hook points should be used
        //not used if it would result in a swing where the player falls lower than the preferred hang height
        float velocityPref = (((transform.position - hookPoint).magnitude > preferredHangHeight && GetComponent<Rigidbody>().velocity.magnitude > 60 * horizSpeedPercent) ? 1 : 0) * Math.Abs(Vector3.Dot(vDir, Vector3.up)) * Vector3.Dot(vDir, ropeDir.normalized);
        //prefer rope in XZ direction of player movement input
        float inputPref = Mathf.Sqrt(Vector3.Dot(new Vector3(ropeDir.x, 0, ropeDir.z).normalized, inputDir));

        //Debug.Log(GetComponent<Rigidbody>().velocity.magnitude + " ?>? " + 10 * horizSpeedPercent);
        if (heightPref * velocityPref * inputPref > maxRopeFitness)
        {
            //Debug.Log(heightPref + " " + velocityPref + " " + inputPref);
        }
        return heightPref * velocityPref * inputPref;
    }

    private void OnCollisionEnter(Collision collision)
    {
        colliding = true;
        Vector3 normal = Vector3.zero;
        foreach (ContactPoint c in collision)
        {
            normal += c.normal;
        }
        normal /= collision.contactCount;
        GetComponent<Rigidbody>().velocity = Vector3.Reflect(GetComponent<Rigidbody>().velocity, normal);
        Debug.Log("hi");
    }

    private void OnCollisionExit(Collision collision)
    {
        colliding = false;
    }

    void FixedUpdate()
    {
        //velocity-cap at max velocity
        float maxV = 200;
        GetComponent<Rigidbody>().velocity = (GetComponent<Rigidbody>().velocity.magnitude > maxV) ? maxV * GetComponent<Rigidbody>().velocity.normalized : GetComponent<Rigidbody>().velocity;

        if (jump)
        {
            GetComponent<Rigidbody>().velocity += new Vector3(0, 300, 0);
            jump = false;
        }

        //player XZ movement control scale. more control if in a swing (more speed)
        controlScale = (isHooked[0] || isHooked[1]) ? maxV/5 : maxV/2;

        //camera direction on XZ plane
        //old
        //Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, (isHooked[0] || isHooked[1]) ? 1 : 0, 1)).normalized;
        Vector3 camDirXZ = Vector3.Scale((transform.position - playerCamera.transform.position), new Vector3(1, 0, 1)).normalized;

        //using user WASD input and camera direction on XZ plane add movement force
        //left and right are flipped so horizDir is negative
        //need to check velocity isnt 0 or infinite force will be applied;
        Vector3 controlForce = controlScale / Mathf.Sqrt(Mathf.Max(GetComponent<Rigidbody>().velocity.magnitude, 1)) * (vertDir * camDirXZ + -horizDir * Vector3.Cross(camDirXZ, Vector3.up)).normalized;
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

        Vector3 combinedRopeDir = Vector3.zero;


        for (int i = 0; i < ropes.Length; i++)
        {
            //since ropes are destroyed within update, need to check it is not destroyed (using .destroyed)
            //because fixed update loop could occur between destroying the RopeConnector and setting ropes[i]=null;
            if (ropes[i] != null && !ropes[i].destroyed)
            {
                //swinging physics called in RopeConnector
                ropes[i].swing();

                //get direction if holding both ropes or not
                combinedRopeDir += (ropes[i].getCurrentHook().transform.position - transform.position).normalized;

                //calculate angular velocity if ropes are released at this point in time
                //Vector3 ropeLen = ropes[i].getCurrentHook().transform.position - transform.position;
                ////Vector3 playerHeight = GetComponent<MeshRenderer>().bounds.size.y * ropeLen.normalized;
                //Vector3 playerHeight = ropeLen.normalized * 2;
                //Vector3 TRPY = 2 * ropeLen + playerHeight;
                //Vector3 coeff = Vector3.Scale(playerHeight, new Vector3(1 / TRPY.x, 1 / TRPY.y, 1 / TRPY.z) - Vector3.Scale(2 * ropeLen, new Vector3(1 / playerHeight.x, 1 / playerHeight.y, 1 / playerHeight.z)));
                //tempAngularVelocity[i] = Vector3.Scale(coeff, GetComponent<Rigidbody>().velocity);


                //Vector3 denom = (2 * (ropes[i].getCurrentHook().transform.position - transform.position) + ((ropes[i].getCurrentHook().transform.position - transform.position)).normalized * GetComponent<MeshRenderer>().bounds.size.y);
                //tempAngularVelocity[i] = Vector3.Scale(GetComponent<Rigidbody>().velocity, new Vector3(1/denom.x, 1/denom.y, 1/denom.z));
            }
        }

        if (combinedRopeDir.Equals(Vector3.zero))
        {
            combinedRopeDir = transform.up;
        }

        //direction rule states
        //on ground: perpendicular, facing direction on XZ plane
        //within H distance from ground 
        //
        //

        float rotationSmoother = 0.5f;
        float rotRate = Time.fixedDeltaTime / rotationSmoother;

        //transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward, GetComponent<Rigidbody>().velocity, rotRate),transform.up);

        //TODO create Slerp-like function but with springiness rather than being linear (ala Overgrowth dev)

        bool isUpsideDown = (Vector3.Dot(transform.up, Vector3.up) < 0);

        if (isHooked[0] || isHooked[1] && !isGrounded())
        {
            transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward, GetComponent<Rigidbody>().velocity, rotRate), Vector3.Slerp(transform.up, combinedRopeDir, rotRate));
        }
        else if (!isGrounded())
        {
            Vector3 tempTransformUp = Vector3.Slerp(transform.up, Vector3.up, rotRate);
            if (isUpsideDown)
            {
                transform.rotation = Quaternion.LookRotation(Vector3.Slerp(Vector3.Cross(transform.right, tempTransformUp), GetComponent<Rigidbody>().velocity.normalized, rotRate), tempTransformUp);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward, GetComponent<Rigidbody>().velocity, rotRate), tempTransformUp);
            }
        }

        Debug.Log(GetComponent<Rigidbody>().velocity);
        //landing (not colliding, about 1 second from hitting surface below, moving downwards)
        if (colliding == false && Physics.Raycast(transform.position, -Vector3.up, -GetComponent<Rigidbody>().velocity.y) && GetComponent<Rigidbody>().velocity.y<0)
        {
            //transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward.normalized, new Vector3(transform.forward.x, 0, transform.forward.z).normalized, Time.fixedDeltaTime), Vector3.Slerp(transform.up, Vector3.up, rotRate));
            transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward, new Vector3(transform.forward.x, 0, transform.forward.z), rotRate * 2), Vector3.Slerp(transform.up, Vector3.up, rotRate * 2));
        }

        //moving on ground
        if (isGrounded())
        {
            transform.rotation = Quaternion.LookRotation(Vector3.Slerp(new Vector3(transform.forward.x, 0, transform.forward.z), GetComponent<Rigidbody>().velocity.normalized, rotRate), Vector3.Slerp(transform.up, Vector3.up, rotRate));
            //if (!(isHooked[0] || isHooked[1]))
            //{
            //    Debug.Log("5");
            //    GetComponent<Rigidbody>().velocity = Vector3.Scale(GetComponent<Rigidbody>().velocity, Vector3.up);
            //}
        }


        //rotate in direction of motion
        //else
        //{
        //    if(tempAngularVelocity[0].Equals(Vector3.zero) && tempAngularVelocity[1].Equals(Vector3.zero))
        //    {
        //        //GetComponent<Rigidbody>().angularVelocity *= 0.95f * Time.fixedDeltaTime;
        //        ////less than one degree per second
        //        //if (GetComponent<Rigidbody>().angularVelocity.magnitude < 1)
        //        //{
        //        //    transform.up = Vector3.Slerp(transform.up, (combinedRopeDir.Equals(Vector3.zero) ? Vector3.up : combinedRopeDir), rotRate);
        //        //}
        //    } else
        //    {
        //        GetComponent<Rigidbody>().angularVelocity = tempAngularVelocity[0] + tempAngularVelocity[1];
        //    }
        //}

    }

    public bool isGrounded()
    {
        return colliding && Physics.Raycast(transform.position, -Vector3.up, 10);
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
            //if the player distance from the hook exceeds that of the rope length remove velocity component that would cause it to increase
            //and add centripetal force to swing player realistically (F = mv^2/r)
            if (tempDist > hookDist)
            {
                if (v.magnitude != 0)
                {
                    float vLostPercent = 1 - (v - Vector3.Project(v, dir)).magnitude / v.magnitude;
                    player.GetComponent<Rigidbody>().velocity = v - Vector3.Project(v, dir);

                    //make up for lost velocity in direction of rope by redirecting 1/10 of lost velocity
                    player.GetComponent<Rigidbody>().velocity *= 1 + vLostPercent / 10;
                }

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
            line.GetComponent<LineRenderer>().startWidth = 0.15f;

            return line;
        }

        //draw lines between the hooks and player
        public void drawLines()
        {
            for (int i = 0; i < lines.Count - 1; i++)
            {
                lines[i].GetComponent<LineRenderer>().SetPositions(new Vector3[] { hooks[i].transform.position, hooks[i + 1].transform.position });
                lines[i].GetComponent<LineRenderer>().startColor = Color.gray;
                lines[i].GetComponent<LineRenderer>().endColor = Color.gray;
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
