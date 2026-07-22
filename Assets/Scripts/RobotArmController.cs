using UnityEngine;

public class RobotArmController : MonoBehaviour
{
    public static RobotArmController Instance { get; private set; }

    enum State
    {
        Idle,
        LowerToPick,
        Grabbing,
        LiftAfterPick,
        RotateToPlace,
        LowerToPlace,
        Releasing,
        LiftAfterPlace,
        RotateHome
    }

    [Header("Joints")]
    public Transform baseJoint;
    public Transform shoulderJoint;
    public Transform elbowJoint;
    public Transform gripperAttach;

    [Header("Targets")]
    public Transform pickupPoint;
    public StorageBin storageBin;

    [Header("Arm geometry (must match the visual segment lengths)")]
    public float upperArmLength = 1.2f;
    public float foreArmLength = 1.2f;

    [Header("Retracted / transit pose (deg)")]
    public float safeShoulderAngle = 20f;
    public float safeElbowAngle = 30f;

    [Header("Grip offset - how far below the gripper (vacuum-style, top-face pick) the box hangs")]
    public float gripOffset = 0.35f;

    [Header("Speed")]
    public float rotateSpeed = 90f; // degrees per second

    State state = State.Idle;
    GameObject currentBox;

    float curBaseAngle;
    float curShoulderAngle;
    float curElbowAngle;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        curShoulderAngle = safeShoulderAngle;
        curElbowAngle = safeElbowAngle;

        if (pickupPoint != null)
        {
            ComputeIK(pickupPoint.position, out curBaseAngle, out _, out _);
        }

        baseJoint.localEulerAngles = new Vector3(0, curBaseAngle, 0);
        shoulderJoint.localEulerAngles = new Vector3(curShoulderAngle, 0, 0);
        elbowJoint.localEulerAngles = new Vector3(curElbowAngle, 0, 0);
        Debug.Log("[Arm] Start - state=" + state);
    }

    public void NotifyBoxReady(GameObject box)
    {
        if (storageBin != null && storageBin.IsFull)
        {
            Debug.Log("[Arm] Storage bin is full, leaving box at pickup point.");
            return;
        }

        if (state != State.Idle)
        {
            Debug.Log("[Arm] NotifyBoxReady ignored, arm busy in state=" + state);
            return;
        }
        currentBox = box;
        state = State.LowerToPick;
        Debug.Log("[Arm] Box accepted, -> LowerToPick");
    }

    void Update()
    {
        switch (state)
        {
            case State.Idle:
                break;

            case State.LowerToPick:
                {
                    ComputeIK(pickupPoint.position, out float b, out float s, out float e);
                    bool baseDone = RotateBase(b);
                    bool armDone = RotateJoints(s, e);
                    if (baseDone && armDone)
                        SetState(State.Grabbing);
                    break;
                }

            case State.Grabbing:
                AttachBox();
                SetState(State.LiftAfterPick);
                break;

            case State.LiftAfterPick:
                if (RotateJoints(safeShoulderAngle, safeElbowAngle))
                    SetState(State.RotateToPlace);
                break;

            case State.RotateToPlace:
                {
                    ComputeIK(storageBin.GetNextPlacementPosition(), out float b, out _, out _);
                    if (RotateBase(b))
                        SetState(State.LowerToPlace);
                    break;
                }

            case State.LowerToPlace:
                {
                    ComputeIK(storageBin.GetNextPlacementPosition(), out float b, out float s, out float e);
                    bool baseDone = RotateBase(b);
                    bool armDone = RotateJoints(s, e);
                    if (baseDone && armDone)
                        SetState(State.Releasing);
                    break;
                }

            case State.Releasing:
                ReleaseBox();
                SetState(State.LiftAfterPlace);
                break;

            case State.LiftAfterPlace:
                if (RotateJoints(safeShoulderAngle, safeElbowAngle))
                    SetState(State.RotateHome);
                break;

            case State.RotateHome:
                {
                    ComputeIK(pickupPoint.position, out float b, out _, out _);
                    if (RotateBase(b))
                        SetState(State.Idle);
                    break;
                }
        }
    }

    void SetState(State next)
    {
        Debug.Log("[Arm] " + state + " -> " + next);
        state = next;
    }

    // Computes the base yaw, shoulder angle, and elbow angle needed so the
    // gripper (end of a 2-segment planar arm) reaches worldTarget exactly.
    void ComputeIK(Vector3 worldTarget, out float baseAngle, out float shoulderAngle, out float elbowAngle)
    {
        Vector3 origin = baseJoint.position;
        Vector3 toTarget = worldTarget - origin;

        float horizontalDist = new Vector2(toTarget.x, toTarget.z).magnitude;
        float verticalDist = toTarget.y;

        baseAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;

        float r = Mathf.Sqrt(horizontalDist * horizontalDist + verticalDist * verticalDist);
        float maxReach = upperArmLength + foreArmLength - 0.01f;
        float minReach = Mathf.Abs(upperArmLength - foreArmLength) + 0.01f;
        r = Mathf.Clamp(r, minReach, maxReach);

        float cosElbowInterior = (upperArmLength * upperArmLength + foreArmLength * foreArmLength - r * r) / (2f * upperArmLength * foreArmLength);
        cosElbowInterior = Mathf.Clamp(cosElbowInterior, -1f, 1f);
        float elbowInterior = Mathf.Acos(cosElbowInterior) * Mathf.Rad2Deg;
        elbowAngle = 180f - elbowInterior;

        float beta = Mathf.Atan2(horizontalDist, verticalDist) * Mathf.Rad2Deg;
        float cosAlpha2 = (upperArmLength * upperArmLength + r * r - foreArmLength * foreArmLength) / (2f * upperArmLength * r);
        cosAlpha2 = Mathf.Clamp(cosAlpha2, -1f, 1f);
        float alpha2 = Mathf.Acos(cosAlpha2) * Mathf.Rad2Deg;

        shoulderAngle = beta - alpha2;
    }

    bool AngleReached(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b)) < 0.5f;

    bool RotateBase(float targetAngle)
    {
        curBaseAngle = Mathf.MoveTowardsAngle(curBaseAngle, targetAngle, rotateSpeed * Time.deltaTime);
        baseJoint.localEulerAngles = new Vector3(0, curBaseAngle, 0);
        return AngleReached(curBaseAngle, targetAngle);
    }

    bool RotateJoints(float shoulderTarget, float elbowTarget)
    {
        curShoulderAngle = Mathf.MoveTowardsAngle(curShoulderAngle, shoulderTarget, rotateSpeed * Time.deltaTime);
        curElbowAngle = Mathf.MoveTowardsAngle(curElbowAngle, elbowTarget, rotateSpeed * Time.deltaTime);

        shoulderJoint.localEulerAngles = new Vector3(curShoulderAngle, 0, 0);
        elbowJoint.localEulerAngles = new Vector3(curElbowAngle, 0, 0);

        return AngleReached(curShoulderAngle, shoulderTarget) && AngleReached(curElbowAngle, elbowTarget);
    }

    void AttachBox()
    {
        if (currentBox == null) return;
        BoxMover mover = currentBox.GetComponent<BoxMover>();
        if (mover != null) mover.enabled = false;
        currentBox.transform.SetParent(gripperAttach);

        // Vacuum-style top pick: the box hangs straight down (world space) below the
        // gripper, and stays world-upright (not tilted with the gripper) so the box's
        // top face - not a side - is what stays against the gripper.
        currentBox.transform.position = gripperAttach.position + Vector3.down * gripOffset;
        currentBox.transform.rotation = Quaternion.identity;
    }

    void ReleaseBox()
    {
        if (currentBox == null) return;
        currentBox.transform.SetParent(null);
        currentBox.transform.position = storageBin.GetNextPlacementPosition();
        currentBox.transform.rotation = Quaternion.identity;
        storageBin.ConfirmPlaced(currentBox);
        currentBox = null;
    }
}
