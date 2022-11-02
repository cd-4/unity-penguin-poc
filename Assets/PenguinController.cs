using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Physics;
using UnityEngine.InputSystem;

public class PenguinController : MonoBehaviour
{
    [Header("Basics")]
    public float penguinWeight = 1.0f;
    public float waddleSpeed = 2.0f;
    public float slideSpeed = 5.0f;
    public float slideTurnRate = 90.0f; // degrees / sec
    public float penguinPushForce = 2.0f;
    public float flapSlowEffect = 0.05f;
    public float gravityAmount = 5.0f;
    public float waterGravityModifier = 0.2f;
    public float amountOfAir = 45.0f;
    public float jumpHeight = 0.7f;
    public float timeToDive = 0.2f;
    public float maxSpeed = 30f; // at high speeds game gets ridiculous and buggy

    [Header("Ground Detection")]
    public int groundRayAmount = 10;
    public float groundRayOffset = 0.4f;
    public float groundRayLength = 0.1f;
    public float airtimeDelay = 0.01f;
    private float currentAirtime = 0f;
    private bool jumpPressed = false;
    private Vector3 lastGroundPosition;

    public LayerMask groundLayer;

    private bool onGround = false;
    private bool inWater = false;
    private bool isSliding = false;
    private bool flapHeld = false;


    // Low value = higher friction
    public float snowFrictionAmount = 0.5f;

    private Vector3 groundNormal;
    private Vector3 lastGroundPos;

    [Header("Acrobatics")] // Mid Air
    private float airRotationSpeed = 180f;


    [Header("Flight")]
    public float flightRotationSlowFactor = 0.2f;
    public float liftFactor = 0.05f;
    public float airResistance = 0.2f;

    private CapsuleCollider collider;
    private Rigidbody body;
    [Header("Camera")]
    public Transform camera;

    private Vector3 velocity;
    private bool aboutToDive = false;
    private bool isDiving = false;

    private int groundRayFrameCheckAmount = 5;
    private int groundRayFrameChecks = 5;

    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    public float angleSmoothTime = 0.1f;
    float angleSmoothVelocity;
    float yawAngleSmoothVelocity;

    private Vector2 movementInput;
    private Vector2 cameraInput;

    private PenguinControls penguinInput;
    private InputAction movement;
    private InputAction flapAction;
    private InputAction flapBrakeAction;
    private float flapBrakeAmount;
    private float flapAmount;

    private bool[] groundCache;
    public int groundCacheCount = 5;
    private int groundCacheIndex = 0;

    [Header("Transforms")]
    public Transform feetLocation;
    public Transform slideRotator;
    public Transform slideYawRotator;

    [Header("Wing Tips")]
    public int trailLength = 20;
    public int trailSkipFrames = 2;
    private int trailSkips = 0;
    public Transform leftWingTip;
    public Transform rightWingTip;
    private Vector3[] leftTipTrail;
    private Vector3[] rightTipTrail;

    private LineRenderer leftTrail;
    private LineRenderer rightTrail;

    /*
    JumpPressed
    DivePressed
    OnGround
    IsSliding
    ForwardVector
    HorizontalVector
     */
    public Animator animator;


    // GIZMOS

    Vector3 movementDirectionGizmo;
    Vector3 checkGizmo;
    Vector3 desiredDirectionGizmo;
    Vector3 verticalAdjustGizmo;
    
    // Start is called before the first frame update

    void Awake()
    {
        penguinInput = new PenguinControls();
        groundCache = new bool[groundCacheCount];
        leftTipTrail = new Vector3[trailLength];
        rightTipTrail = new Vector3[trailLength];
    }

    void Start()
    {

        collider = GetComponent<CapsuleCollider>();
        body = GetComponent<Rigidbody>();
        velocity = new Vector3(0.0f, -gravityAmount, 0.0f);
    }

    void OnEnable()
    {
        // Do Input intialize stuff here
        CreateActions();
        CreateLineTips();
        EnableActions();
    }

    void CreateLineTips()
    {
        GameObject leftLine = new GameObject();
        leftLine.transform.position = leftWingTip.position;
        leftLine.AddComponent<LineRenderer>();
        leftTrail = leftLine.GetComponent<LineRenderer>();
        leftTrail.positionCount = trailLength;
        leftTrail.SetWidth(0.1f, 0.1f);
        leftTrail.SetPosition(0, leftWingTip.position);
        leftTrail.SetPosition(1, rightWingTip.position);

        GameObject rightLine = new GameObject();
        rightLine.transform.position = rightWingTip.position;
        rightLine.AddComponent<LineRenderer>();
        rightTrail = rightLine.GetComponent<LineRenderer>();
        rightTrail.positionCount = trailLength;
        rightTrail.SetWidth(0.1f, 0.1f);
        rightTrail.SetPosition(0, rightWingTip.position);
        rightTrail.SetPosition(1, leftWingTip.position);
    }

    void CreateActions()
    {
        movement = penguinInput.Default.Movement;
        flapAction = penguinInput.Default.Flap;
        flapBrakeAction = penguinInput.Default.FlapBrake;
        penguinInput.Default.Jump.performed += JumpPressed;
        penguinInput.Default.Dive.performed += e => Dive();

    }

    void EnableActions()
    {
        movement.Enable();
        penguinInput.Default.Jump.Enable();
        penguinInput.Default.Dive.Enable();
        flapAction.Enable();
        flapBrakeAction.Enable();
    }


    // Update is called once per frame
    void Update()
    {
        CheckGround();
        if (!onGround)
        {
            ApplyGravity();
        }
        DoMovement();
        DrawLineTips();

        movementDirectionGizmo = velocity.normalized;
        transform.Translate(velocity * Time.deltaTime, Space.World);
        CheckUnderground();
    }

    void CheckUnderground()
    {
        RaycastHit hit;
        Vector3 feet = feetLocation.position;
        if (Raycast(feet, Vector3.up, out hit, groundRayLength, groundLayer))
        {
            var groundPos = hit.point;
            transform.Translate(groundPos - feet);
        }
    }

    void JumpPressed(InputAction.CallbackContext obj)
    {
        if (onGround)//currentAirtime < airtimeDelay)
        {
            Jump();
        }
        if (isSliding && currentAirtime < airtimeDelay)
        {
            Jump();
        }
    }

    IEnumerator EndJump()
    {
        yield return new WaitForSeconds(0.6f);
        animator.SetBool("JumpPressed", false);
    }

    void DoJump()
    {
        CheckUnderground();
        float verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravityAmount);
        velocity.y += verticalVelocity;
        SetLeftGround();
        groundRayFrameChecks = 7;
    }

    void Jump()
    {
        //h = (init velocity) * t - (1/2) g t^2)
        //(init velocity) = h / t - (1/2) g t^2)
        animator.SetBool("JumpPressed", true);
        DoJump();
        StartCoroutine(EndJump());
    }

    /*
        Dive Nonsense
     */

    IEnumerator EndDive() {
        yield return new WaitForSeconds(0.6f);
        animator.SetBool("DivePressed", false);
    }

    IEnumerator DiveWithDelay() {
        yield return new WaitForSeconds(0.05f);
        DoDive();
    }

    void DoDive()
    {
        if (onGround)
        {
            float verticalVelocity = Mathf.Sqrt(jumpHeight * 0.5f * 2f * gravityAmount);
            velocity.y = verticalVelocity;
        }
        isSliding = true;
    }

    void Dive()
    {
        if (!isSliding)
        {
            animator.SetBool("DivePressed", true);
            animator.SetBool("IsSliding", true);
            StartCoroutine(DiveWithDelay());
            StartCoroutine(EndDive());
        } else {
            if (onGround)
            {
                animator.SetBool("IsSliding", false);
                isSliding = false;
            }
        }
    }

    // Turns a value from [-1, 1] to [0, 1]
    float InputToAnimAxis(float value)
    {
        return (value + 1f) / 2f;

    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, movementDirectionGizmo* 1);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, desiredDirectionGizmo * 1);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, checkGizmo* 2);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, verticalAdjustGizmo);
    }

    float AngleBetween(Vector2 vec1, Vector2 vec2)
    {
        Vector2 vec1Rotated90 = new Vector2(-vec1.y, vec1.x);
        float sign = (Vector2.Dot(vec1Rotated90, vec2) < 0) ? -1.0f : 1.0f;
        return Vector2.Angle(vec1, vec2) * sign;
    }

    void DoMovement()
    {
        // Movement
        var mIn = movement.ReadValue<Vector2>();
        float xInput = mIn.x;
        float yInput = mIn.y;

        Vector3 inputVector = new Vector3(xInput, 0f, yInput);
        Vector3 inputDirection = inputVector.normalized;

        // Camera and character rotation
        float camAngle = camera.eulerAngles.y;
        float currentDirection = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
        if (!isSliding) {
            // Turn walking penguin in direction of movement
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + camAngle;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        } else {
            // Turn sliding penguin towards slide movement direction
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, currentDirection, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // Character Movement
        Vector3 moveVector = Quaternion.Euler(0f, camAngle, 0f) * inputDirection;

        Vector2 desiredDir = new Vector2(moveVector.x, moveVector.z);
        var difference = AngleBetween(desiredDir, new Vector2(velocity.x, velocity.z));
        var visualDiff = difference;

        if (onGround && !isSliding) {
            animator.SetFloat("ForwardVector", Mathf.Abs(moveVector.magnitude));
            animator.SetFloat("HorizontalVector", InputToAnimAxis(xInput));
        } else if (isSliding) {
            if (visualDiff > 90f) { visualDiff = 90f - (visualDiff - 90f) * 2; }
            if (visualDiff < -90f) { visualDiff = -90f + (visualDiff - 90f) * 2; }
            animator.SetFloat("ForwardVector", InputToAnimAxis(yInput));
            animator.SetFloat("HorizontalVector", InputToAnimAxis(visualDiff / 90f));
        }

        flapBrakeAmount = flapBrakeAction.ReadValue<float>();
        flapAmount = flapAction.ReadValue<float>();

        bool doPush = false;
        if (isSliding)
        {
            animator.SetFloat("FlapAmount", flapAmount);
            var isFlapNowHeld = flapAmount > 0.05f;
            if (isFlapNowHeld && currentAirtime < airtimeDelay)
            {
                velocity -= velocity * flapSlowEffect * Time.deltaTime;
            }
            if (flapHeld && !isFlapNowHeld)
            {
                doPush = true;
            }
            flapHeld = isFlapNowHeld;

        }

        Vector3 directionProj = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 movementProj = Vector3.ProjectOnPlane(velocity, groundNormal);

        // Rotate Model
        if (isSliding && velocity.magnitude > 2f)
        {
            // Sliding On Hill
            if (onGround)
            {
                // Rotates model vertically around X axis
                var angle = Vector3.Angle(directionProj, movementProj);

                // Flip if going uphill somereason donesn't work right off the bat
                if (movementProj.y > directionProj.y) { angle *= -1f; }
                var rot = slideRotator.eulerAngles;
                var newRot = Mathf.SmoothDampAngle(rot.x, angle, ref angleSmoothVelocity, angleSmoothTime);
                slideRotator.rotation = Quaternion.Euler(newRot, currentDirection, 0f);


                // Rotates model around Z axis (yaw I believe)
                Vector3 rightDirection = (Quaternion.Euler(0f, 90f, 0f) * directionProj).normalized;
                Vector3 rightSlope = Vector3.ProjectOnPlane(rightDirection, groundNormal);
                var yawAngle = Vector3.Angle(rightDirection, rightSlope);
                // Flip if one situation
                if (rightDirection.y > rightSlope.y) { yawAngle *= -1f; }
                var yawRot = slideYawRotator.eulerAngles;
                var newYawRot = Mathf.SmoothDampAngle(yawRot.z, yawAngle, ref yawAngleSmoothVelocity, angleSmoothTime);
                slideYawRotator.rotation = Quaternion.Euler(newRot, currentDirection, newYawRot);
            } else // Doing Badass acrobatics
            {
                // moveVector = direction the user is pushing towards;
                // inputDirection = up / right
                var mod = airRotationSpeed * Time.deltaTime;

                if (flapAmount > 0.05f)
                {
                    mod *= flightRotationSlowFactor;
                }

                var rotateX = inputDirection.z * mod;
                var rotateY = inputDirection.x * mod;
                var rotateZ = -flapBrakeAmount * mod;

                slideRotator.Rotate(rotateX, rotateY, rotateZ);
                //slideYawRotator.rotation = Quaternion.Euler(newRot, currentDirection, 0f);
                var normalMove = moveVector.normalized;

            }
        } else {
            slideRotator.rotation = Quaternion.Euler(0f, currentDirection, 0f);
            slideYawRotator.rotation = Quaternion.Euler(0f, currentDirection, 0f);
        }


        if (onGround)
        {
            if (isSliding)
            {
                var downForce = Vector3.down * (penguinWeight * gravityAmount);
                var slideForce = Vector3.ProjectOnPlane(downForce, groundNormal) * snowFrictionAmount;

                // Desired movement direction
                var desiredMovement = Quaternion.Euler(0f, difference, 0f) * velocity;
                var angleDifference = Vector3.Angle(velocity, desiredMovement);
                // degrees / sec = slideTurnRate
                // sec
                // desired degrees
                // Time.deltaTime
                var degreeChange = slideTurnRate * Time.deltaTime;
                if (degreeChange < Mathf.Abs(angleDifference))
                {
                    if (difference < 0f) { degreeChange *= -1f; }
                    velocity = Quaternion.Euler(0f, degreeChange, 0f) * velocity;
                } else
                {
                    velocity = desiredMovement;
                }
                //velocity = desiredMovement;

                if (doPush)
                {
                    velocity = velocity + velocity.normalized * penguinPushForce;
                }

                Vector3 newVelocity = velocity + slideForce * Time.deltaTime;
                velocity = newVelocity;
            } else {
                var movementVector = moveVector * waddleSpeed + velocity * 0.5f;
                velocity = Vector3.ProjectOnPlane(movementVector, groundNormal);
            }
        } else
        {
            // In the air
            if (flapAmount > 0.5f)
            {
                //var diminishingFactor = 1f - (Time.deltaTime / 4f);
                //velocity.x *= diminishingFactor;
                //velocity.z *= diminishingFactor;
                CalculateLift();
            }
        }
        if (velocity.magnitude > maxSpeed)
        {
            velocity *= maxSpeed / velocity.magnitude;

        }
        Debug.Log(velocity.magnitude);
    }

    void ApplyGravity()
    {
        velocity += Vector3.down * gravityAmount * Time.deltaTime;
    }

    void CheckLanding()
    {
        var angleBetween = Vector3.Angle(groundNormal, slideRotator.up);
        if (angleBetween < 30f)
        {
            Debug.Log("Nice Landing!");
        }
    }

    void SetOnGround()
    {
        if (!onGround)
        {
            CheckLanding();
            onGround = true;
            var verticalVelocity = velocity.y;
            //velocity.y = 0f;

            // This should be better, needs to somewhat "bounce" off the ground to give more forward velocity

            velocity = Vector3.ProjectOnPlane(velocity, groundNormal);
            animator.SetBool("OnGround", true);
            // Do other stuff here;
        }
    }

    void SetLeftGround()
    {
        if (onGround) {
            onGround = false;
            animator.SetBool("OnGround", false);
        }
    }

    bool GroundCacheFullOf(bool checkValue)
    {
        bool foundAll = true;
        for(int i=0; i<groundCacheCount; i++)
        {
            if (groundCache[i] != checkValue)
            {
                foundAll = false;
            }
        }
        return foundAll;
    }

    void AddGroundCache(bool value)
    {

        groundCache[groundCacheIndex] = value;
        groundCacheIndex++;
        if (groundCacheIndex == groundCacheCount)
        {
            groundCacheIndex = 0;
        }
    }

    void UpdateGroundStatus(bool currentValue)
    {
        AddGroundCache(currentValue);
        if (GroundCacheFullOf(true))
        {
            SetOnGround();
        } else if (GroundCacheFullOf(false))
        {
            SetLeftGround();
        }
    }

    bool CheckDown(Vector3 position)
    {
        RaycastHit hit;
        if (Raycast(position, Vector3.down, out hit, groundRayLength))
        {
            groundNormal = hit.normal;
            lastGroundPos = hit.point;
            return true;
        }
        return false;
    }

    void CheckGround()
    {
        bool foundGround = false;

        if (groundRayFrameChecks > 0)
        {
            groundRayFrameChecks--;
            return;
        }
        var twoPi = 2 * Mathf.PI;
        var verticalOffset = transform.position - Vector3.up * groundRayOffset;
        if (CheckDown(verticalOffset)) { foundGround = true; }
        var groundRayRadius = collider.radius * 0.8f;

        int rayCount = 0;
        var raysFound = new Vector3[3];
        Vector3 lastNormal = groundNormal;
        for (int i = 0; i < groundRayAmount; i++)
        {
            float angle = twoPi * ((float)i) / ((float) groundRayAmount);
            var xChange = groundRayRadius * Mathf.Sin(angle);
            var yChange = groundRayRadius * Mathf.Cos(angle);
            Vector3 position = verticalOffset + new Vector3(xChange, 0f, yChange);
            if (CheckDown(position)) { foundGround = true; break; }
        }

        if (foundGround)
        {
            currentAirtime = 0f;

            var desiredBottomPosition = lastGroundPos.y;
            var change = desiredBottomPosition - feetLocation.position.y;
            transform.Translate(new Vector3(0f, change, 0f));
        } else
        {
            currentAirtime += Time.deltaTime;
        }

        UpdateGroundStatus(foundGround);
    }


    // Wing Tips
    void BumpTips(Vector3[] wingTips, Vector3 newPoint)
    {
        if (trailSkips != trailSkipFrames)
        {
            trailSkips++;
            return;
        } else
        {
            trailSkips = 0;
        }
        for (int i=trailLength-2; i>-1; i--)
        {
            wingTips[i + 1] = wingTips[i];
        }
        wingTips[0] = newPoint;
    }

    void AddLeftTipPoint() { BumpTips(leftTipTrail, leftWingTip.position); }
    void AddRightTipPoint() { BumpTips(rightTipTrail, rightWingTip.position); }

    void ClearTips()
    {
        for(int i=0; i<trailLength; i++)
        {
            leftTipTrail[i] = leftWingTip.position;
            rightTipTrail[i] = rightWingTip.position;
        }
    }

    void UpdateTips()
    {
        leftTrail.SetPositions(leftTipTrail);
        rightTrail.SetPositions(rightTipTrail);
    }

    void DrawLineTips()
    {
        if (isSliding && !onGround && flapAmount > 0.05f)
        {
            AddLeftTipPoint();
            AddRightTipPoint();
            UpdateTips();
        } else
        {
            ClearTips();
            UpdateTips();
        }
    }
    // Calculate the lift factor for a penguin's flight lol
    void CalculateLift()
    {
        var penguinNormal = slideRotator.rotation * Vector3.down;
        var reflected = Vector3.Reflect(velocity, penguinNormal) * airResistance;

        var currentDirectionVelocity = Vector3.ProjectOnPlane(-velocity, penguinNormal).magnitude;
        var liftMagnitude = currentDirectionVelocity * liftFactor;
        //var liftForce = slideRotator.rotation * (Vector3.up) * liftMagnitude;
        var liftForce = slideRotator.rotation * (new Vector3(1f, 0f, 1f).normalized) * liftMagnitude;
        desiredDirectionGizmo = liftForce;
        checkGizmo = reflected;

        var oldMag = velocity.magnitude;

        //var addedForce = reflected * Time.deltaTime;
        var addedForce = liftForce + reflected * Time.deltaTime;
        if (velocity.y > 0 && addedForce.y > 0)
        {
            addedForce.y = 0;
        }
        velocity += addedForce * Time.deltaTime;

    }

}
