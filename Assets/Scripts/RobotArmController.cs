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

    [Header("Joints (6 axes: base, shoulder, elbow, wrist pitch, wrist roll, gripper)")]
    public Transform baseJoint;
    public Transform shoulderJoint;
    public Transform elbowJoint;
    public Transform wristPitchJoint;
    public Transform wristRollJoint;
    public Transform gripperAttach;
    public Transform leftFinger;
    public Transform rightFinger;

    [Header("Targets")]
    public Transform pickupPoint;
    public StorageBin storageBin;

    [Header("Arm geometry (must match the visual segment lengths)")]
    public float upperArmLength = 1.2f;
    public float foreArmLength = 1.2f; // elbow to wrist pitch joint only
    public float wristExtension = 0.3f; // wrist pitch joint to gripper attach (hangs straight down once wrist compensates)

    [Header("Retracted / transit pose (deg)")]
    public float safeShoulderAngle = 20f;
    public float safeElbowAngle = 30f;

    [Header("Wrist roll (deg) - fixed for now, will map to a real servo later")]
    public float wristRollAngle = 0f;

    [Header("Gripper fingers - center offset from gripper axis (finger is 0.08 thick, so add half that to reach the box surface)")]
    public float fingerOpenOffset = 0.29f;   // inner face right at the box's side - open gap = box width
    public float fingerClosedOffset = 0.25f; // inner face presses flush against the box's side

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
            ComputeIK(WristAdjustedTarget(pickupPoint.position), out curBaseAngle, out _, out _);
        }

        baseJoint.localEulerAngles = new Vector3(0, curBaseAngle, 0);
        shoulderJoint.localEulerAngles = new Vector3(curShoulderAngle, 0, 0);
        elbowJoint.localEulerAngles = new Vector3(curElbowAngle, 0, 0);
        wristRollJoint.localEulerAngles = new Vector3(0, wristRollAngle, 0);
        UpdateWristPitch();
        SetFingers(true);

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
        SetFingers(true); // open, ready to grab
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
                    ComputeIK(WristAdjustedTarget(pickupPoint.position), out float b, out float s, out float e);
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
                    ComputeIK(WristAdjustedTarget(storageBin.GetNextPlacementPosition()), out float b, out _, out _);
                    if (RotateBase(b))
                        SetState(State.LowerToPlace);
                    break;
                }

            case State.LowerToPlace:
                {
                    ComputeIK(WristAdjustedTarget(storageBin.GetNextPlacementPosition()), out float b, out float s, out float e);
                    bool baseDone = RotateBase(b);
                    bool armDone = RotateJoints(s, e);
                    if (baseDone && armDone)
                        SetState(State.Releasing);
                    break;
                }

            case State.Releasing:
                ReleaseBox(); // opens the fingers, still down - mirrors "close, still down" during grab
                SetState(State.LiftAfterPlace);
                break;

            case State.LiftAfterPlace:
                if (RotateJoints(safeShoulderAngle, safeElbowAngle))
                    SetState(State.RotateHome);
                break;

            case State.RotateHome:
                {
                    ComputeIK(WristAdjustedTarget(pickupPoint.position), out float b, out _, out _);
                    if (RotateBase(b))
                        SetState(State.Idle);
                    break;
                }
        }

        UpdateWristPitch();

        // Keep the carried box world-upright every frame (not just at the grab
        // instant), so it stays correctly gripped between the fingers even while
        // the base/shoulder/elbow keep moving during the carry.
        if (currentBox != null)
            currentBox.transform.rotation = Quaternion.identity;
    }

    void SetState(State next)
    {
        Debug.Log("[Arm] " + state + " -> " + next);
        state = next;
    }

    // The wrist always compensates to point straight down, so the gripper hangs
    // exactly `wristExtension` below the wrist pitch joint. The 2-link IK below
    // solves for the wrist pitch joint's position, so it needs the target raised
    // by that same amount.
    Vector3 WristAdjustedTarget(Vector3 gripperTarget) => gripperTarget + Vector3.up * wristExtension;

    // Computes the base yaw, shoulder angle, and elbow angle needed so the
    // wrist pitch joint (end of the 2-segment planar arm) reaches worldTarget exactly.
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

    // Keeps the gripper facing straight down regardless of how the shoulder/elbow
    // are currently bent - mirrors how a real arm's wrist compensates.
    void UpdateWristPitch()
    {
        float wristPitch = 180f - curShoulderAngle - curElbowAngle;
        wristPitchJoint.localEulerAngles = new Vector3(wristPitch, 0, 0);
    }

    // Fingers are a fixed-length claw permanently attached to the wrist (like the
    // real gripper - it doesn't slide up and down, only its two halves open/close).
    // A positive local Y here reliably points "down toward the box" in world space,
    // because the wrist always holds a net 180-degree pitch (see UpdateWristPitch),
    // and staying in local space keeps the fingers rigidly attached to the wrist
    // instead of floating off to a fixed world-space spot.
    void SetFingers(bool open)
    {
        float offset = open ? fingerOpenOffset : fingerClosedOffset;

        if (leftFinger == null || rightFinger == null)
        {
            Debug.LogError("[Arm] SetFingers - leftFinger or rightFinger reference is NULL! open=" + open);
            return;
        }

        leftFinger.localPosition = new Vector3(-offset, gripOffset, 0f);
        rightFinger.localPosition = new Vector3(offset, gripOffset, 0f);
        leftFinger.localRotation = Quaternion.identity;
        rightFinger.localRotation = Quaternion.identity;
    }

    void AttachBox()
    {
        if (currentBox == null) return;

        float gap = Vector3.Distance(gripperAttach.position, pickupPoint.position);
        Debug.Log("[Arm] AttachBox - pickupPoint=" + pickupPoint.position +
                   " gripperAttach=" + gripperAttach.position +
                   " wristPitchJoint=" + wristPitchJoint.position +
                   " gap=" + gap);

        BoxMover mover = currentBox.GetComponent<BoxMover>();
        if (mover != null) mover.enabled = false;
        currentBox.transform.SetParent(gripperAttach);

        // Vacuum-style top pick: the box hangs straight down (world space) below the
        // gripper, and stays world-upright (not tilted with the gripper) so the box's
        // top face - not a side - is what stays against the gripper.
        currentBox.transform.position = gripperAttach.position + Vector3.down * gripOffset;
        currentBox.transform.rotation = Quaternion.identity;

        SetFingers(false); // close around the box
    }

    void ReleaseBox()
    {
        if (currentBox == null) return;
        currentBox.transform.SetParent(null);
        currentBox.transform.position = storageBin.GetNextPlacementPosition();
        currentBox.transform.rotation = Quaternion.identity;
        storageBin.ConfirmPlaced(currentBox);
        currentBox = null;

        SetFingers(true); // open, release the box
    }
}
