using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class OffsetCalibration : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject startCalibrationButton;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private GameObject yesButton;
    [SerializeField] private GameObject noButton;
    [SerializeField] private GameObject timerTextObject;
    [SerializeField] private TMP_Text calibrationOutputText;

    private TMP_Text timerTMPText; // Cached in InitializeUI
    private Button _startCalibrationButtonComponent;
    private Button _yesButtonComponent;
    private Button _noButtonComponent;

    [Header("Tracking Setup")]
    [SerializeField] private OVRCameraRig cameraRig;
    private Transform trackingAnchor;

    [Header("Calibration Parameters")]
    [SerializeField] private float confirmationTimeout = 5.0f;
    [SerializeField] private float preSpinCountdown = 5.0f;
    [SerializeField] private float spinDuration = 10.0f;
    [SerializeField] private int minSamplesForCalibration = 50;
    [Tooltip("Estimated samples to pre-allocate list capacity. Helps reduce re-allocations during collection.")]
    [SerializeField] private int estimatedSamplesCapacity = 1200; // e.g., 10s * 90fps + buffer

    [Header("Output (Relative to Initial Orientation)")]
    public Vector2 CalculatedOffset_InitialRobotFrame { get; private set; }

    private enum CalibrationState
    {
        Idle,
        Confirming,
        WaitingForSpinStart,
        SpinningAndCollecting,
        Calculating
    }
    private CalibrationState state = CalibrationState.Idle;

    private float timer;
    private List<Vector2> anchorWorldPositionsXZ;
    private Quaternion initialWorldRotationOfAnchor;
    private Vector3 initialAnchorWorldPosition_AtSpinStart;

    // Constants for PlayerPrefs keys to avoid magic strings
    private const string OFFSET_X_KEY = "InitialFrameOffset_LocalX_Right";
    private const string OFFSET_Z_KEY = "InitialFrameOffset_LocalZ_Forward";

    void Start()
    {
        if (!InitializeDependencies()) return;
        InitializeUI();
        LoadOffset();
        UpdateOutputText(); // Display loaded or default offset
    }

    bool InitializeDependencies()
    {
        if (cameraRig == null)
        {
            Debug.LogError("OVRCameraRig is not assigned! Disabling script.");
            enabled = false;
            UpdateUIForError("Error: Rig Missing");
            return false;
        }
        if (cameraRig.centerEyeAnchor == null)
        {
            Debug.LogError("OVRCameraRig's centerEyeAnchor is not assigned! Disabling script.");
            enabled = false;
            UpdateUIForError("Error: Anchor Missing");
            return false;
        }
        trackingAnchor = cameraRig.centerEyeAnchor;

        // Pre-allocate list capacity to reduce re-allocations during collection
        anchorWorldPositionsXZ = new List<Vector2>(estimatedSamplesCapacity);
        return true;
    }

    void InitializeUI()
    {
        // Ensure timerTMPText is fetched before use
        if (timerTextObject != null)
        {
            timerTMPText = timerTextObject.GetComponent<TMP_Text>();
            if (timerTMPText == null)
            {
                Debug.LogError("TimerTextObject does not have a TMP_Text component!");
            }
        }
        else
        {
            Debug.LogError("TimerTextObject is not assigned!");
        }

        // Cache Button components and add listeners
        _startCalibrationButtonComponent = CacheAndSetupButton(startCalibrationButton, StartCalibrationRequest);
        // Assuming Button component is on the GameObject itself or a direct child.
        // If structure is more complex, GetComponentInChildren might be needed but is less performant if overused.
        _yesButtonComponent = CacheAndSetupButton(yesButton, ConfirmCalibration, true);
        _noButtonComponent = CacheAndSetupButton(noButton, DenyCalibration, true);

        // Initial UI state
        SetConfirmationButtonsActive(false);
        SetTimerActive(false);
    }

    Button CacheAndSetupButton(GameObject buttonObject, UnityEngine.Events.UnityAction action, bool findInChildren = false)
    {
        if (buttonObject == null) return null;

        Button buttonComponent = findInChildren ? buttonObject.GetComponentInChildren<Button>() : buttonObject.GetComponent<Button>();
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(action);
        }
        else
        {
            Debug.LogWarning($"Button component not found on {buttonObject.name} or its children (if findInChildren was true).", buttonObject);
        }
        return buttonComponent;
    }

    void UpdateUIForError(string errorMessage)
    {
        if (buttonText != null) buttonText.SetText(errorMessage); // Use SetText for TMP
        if (_startCalibrationButtonComponent != null) _startCalibrationButtonComponent.interactable = false;
        SetConfirmationButtonsActive(false);
        SetTimerActive(false);
    }

    void Update()
    {
        if (state == CalibrationState.Idle || !enabled || trackingAnchor == null) return;

        timer -= Time.deltaTime;
        UpdateTimerDisplay();

        if (timer <= 0f)
        {
            HandleTimerExpiry();
        }
        // Optimization: Only call CollectSample if timer hasn't expired and we are in the correct state.
        // The HandleTimerExpiry will transition state, so this check is somewhat redundant if timer hits 0,
        // but harmless and explicit.
        else if (state == CalibrationState.SpinningAndCollecting)
        {
            CollectSample();
        }
    }

    void UpdateTimerDisplay()
    {
        if (timerTMPText != null && timerTextObject.activeSelf)
        {
            // Use SetText with formatting for better performance (reduces string garbage)
            timerTMPText.SetText("{0:0.0}", Mathf.Max(0f, timer)); // Ensure timer doesn't display negative
        }
    }

    void HandleTimerExpiry()
    {
        SetTimerActive(false);
        switch (state)
        {
            case CalibrationState.WaitingForSpinStart:
                StartSpinningPhase();
                break;
            case CalibrationState.SpinningAndCollecting:
                CompleteCollectionAndCalculate();
                break;
            case CalibrationState.Confirming:
                DenyCalibration(); // Timeout on confirmation
                break;
        }
    }

    void StartCalibrationRequest()
    {
        if (_startCalibrationButtonComponent != null) _startCalibrationButtonComponent.interactable = false;
        if (buttonText != null) buttonText.SetText("Calibrate Offset?");
        SetConfirmationButtonsActive(true);
        SetTimerActive(true, confirmationTimeout);
        state = CalibrationState.Confirming;
    }

    void DenyCalibration()
    {
        if (buttonText != null) buttonText.SetText("Start Offset Calibration");
        if (_startCalibrationButtonComponent != null) _startCalibrationButtonComponent.interactable = true;
        SetConfirmationButtonsActive(false);
        SetTimerActive(false);
        state = CalibrationState.Idle;
    }

    void ConfirmCalibration()
    {
        if (buttonText != null) buttonText.SetText("Align Robot to desired FORWARD.\nReady to Spin in...");
        SetConfirmationButtonsActive(false);
        SetTimerActive(true, preSpinCountdown);
        state = CalibrationState.WaitingForSpinStart;
        anchorWorldPositionsXZ.Clear(); // Clear before collecting new samples
    }

    void StartSpinningPhase()
    {
        if (trackingAnchor == null) // Should have been caught in Start/InitializeDependencies
        {
            Debug.LogError("Tracking anchor is null. Cannot start spin phase.");
            DenyCalibration();
            return;
        }

        initialWorldRotationOfAnchor = trackingAnchor.rotation;
        initialAnchorWorldPosition_AtSpinStart = trackingAnchor.position;

        state = CalibrationState.SpinningAndCollecting;
        if (buttonText != null) buttonText.SetText("Collecting Data... Spin Robot!");
        SetTimerActive(true, spinDuration);
    }

    void CompleteCollectionAndCalculate()
    {
        state = CalibrationState.Calculating;
        if (buttonText != null) buttonText.SetText("Calculating...");
        Debug.Log($"Collected {anchorWorldPositionsXZ.Count} samples.");

        PerformCalibrationCalculation();

        if (buttonText != null) buttonText.SetText("Start Offset Calibration");
        if (_startCalibrationButtonComponent != null) _startCalibrationButtonComponent.interactable = true;
        state = CalibrationState.Idle;
        // Using string interpolation here is fine as it's not in a hot path.
        Debug.Log($"Calibration Complete. Offset (Initial Robot Frame): Local_X(Right)={CalculatedOffset_InitialRobotFrame.x:F4}, Local_Z(Forward)={CalculatedOffset_InitialRobotFrame.y:F4}");
        UpdateOutputText();
        SaveOffset();
    }

    void CollectSample()
    {
        // trackingAnchor null check is implicitly handled by the initial check in Update
        // and the fact that InitializeDependencies would have disabled the script or StartSpinningPhase would have aborted.
        // However, an explicit check here is safer if states could be entered unexpectedly.
        // For performance, if we are sure trackingAnchor is valid at this point, this check can be omitted.
        // if (trackingAnchor == null) return; // Redundant if logic flow is guaranteed

        Vector3 currentPositionWorld = trackingAnchor.position;
        anchorWorldPositionsXZ.Add(new Vector2(currentPositionWorld.x, currentPositionWorld.z));
    }

    void PerformCalibrationCalculation()
    {
        if (anchorWorldPositionsXZ.Count < minSamplesForCalibration)
        {
            Debug.LogError($"Not enough samples: {anchorWorldPositionsXZ.Count}. Need {minSamplesForCalibration}. Setting offset to zero.");
            CalculatedOffset_InitialRobotFrame = Vector2.zero;
            return;
        }

        Vector2 sumTrackedPos_World_XZ = Vector2.zero;
        // Using a for loop can be slightly more performant than foreach for List<T> in some older .NET versions,
        // but foreach is generally optimized well by modern compilers. Readability often wins.
        for (int i = 0; i < anchorWorldPositionsXZ.Count; i++)
        {
            sumTrackedPos_World_XZ += anchorWorldPositionsXZ[i];
        }
        Vector2 estimatedRobotCenter_World_XZ = sumTrackedPos_World_XZ / anchorWorldPositionsXZ.Count;

        // Re-use initialAnchorWorldPosition_AtSpinStart directly for XZ
        Vector2 initialAnchorPos_World_XZ = new Vector2(initialAnchorWorldPosition_AtSpinStart.x, initialAnchorWorldPosition_AtSpinStart.z);
        Vector2 offset_World_XZ = initialAnchorPos_World_XZ - estimatedRobotCenter_World_XZ;

        // Convert to 3D for rotation. No new Vector3 needed here if we are careful
        // Vector3 offset_World_3D = new Vector3(offset_World_XZ.x, 0, offset_World_XZ.y);
        // Vector3 offset_InInitialAnchorFrame_3D = Quaternion.Inverse(initialWorldRotationOfAnchor) * offset_World_3D;

        // Optimized: Perform rotation on components directly if possible, or use existing Vector3.
        // The original way is clear and the allocation is minor here (once per calculation).
        // Let's stick to clarity unless this becomes a super hot path, which it isn't.
        Vector3 offset_InInitialAnchorFrame_3D = Quaternion.Inverse(initialWorldRotationOfAnchor) * new Vector3(offset_World_XZ.x, 0f, offset_World_XZ.y);

        CalculatedOffset_InitialRobotFrame = new Vector2(offset_InInitialAnchorFrame_3D.x, offset_InInitialAnchorFrame_3D.z);
    }

    void UpdateOutputText()
    {
        if (calibrationOutputText != null)
        {
            // Use SetText with formatting for better performance (reduces string garbage)
            calibrationOutputText.SetText("Offset (Initial Robot Frame):\nLocal_X(Right)={0:F4}\nLocal_Z(Forward)={1:F4}",
                                          CalculatedOffset_InitialRobotFrame.x,
                                          CalculatedOffset_InitialRobotFrame.y);
        }
    }

    void SaveOffset()
    {
        PlayerPrefs.SetFloat(OFFSET_X_KEY, CalculatedOffset_InitialRobotFrame.x);
        PlayerPrefs.SetFloat(OFFSET_Z_KEY, CalculatedOffset_InitialRobotFrame.y);
        PlayerPrefs.Save(); // Consider if Save() is needed immediately or can be deferred. For critical data, immediate is fine.
        Debug.Log("Offset Saved.");
    }

    void LoadOffset()
    {
        if (PlayerPrefs.HasKey(OFFSET_X_KEY) && PlayerPrefs.HasKey(OFFSET_Z_KEY))
        {
            float ox = PlayerPrefs.GetFloat(OFFSET_X_KEY);
            float oz = PlayerPrefs.GetFloat(OFFSET_Z_KEY);
            CalculatedOffset_InitialRobotFrame = new Vector2(ox, oz);
            Debug.Log($"Offset Loaded: X={ox:F4}, Z={oz:F4}");
        }
        else
        {
            CalculatedOffset_InitialRobotFrame = Vector2.zero;
            Debug.Log("No saved offset found, defaulting to zero.");
        }
    }

    // Helper methods for UI state
    void SetConfirmationButtonsActive(bool isActive)
    {
        if (yesButton != null) yesButton.SetActive(isActive);
        if (noButton != null) noButton.SetActive(isActive);
    }

    void SetTimerActive(bool isActive, float duration = 0f)
    {
        if (timerTextObject != null) timerTextObject.SetActive(isActive);
        if (isActive)
        {
            timer = duration;
            UpdateTimerDisplay(); // Update display immediately when activated
        }
    }


    // This function is NOT needed if you manually input the offset to the robot.
    // It's here for completeness if you were to use it in Unity.
    // The allocations within (new Vector3, new Vector2) are acceptable if this isn't called per frame.
    public Vector2 GetRobotCenterPosition_World_BasedOnInitialFrame(Transform currentAnchorTransform)
    {
        if (currentAnchorTransform == null) return Vector2.zero;

        // The CalculatedOffset_InitialRobotFrame is (LocalX_Right, LocalZ_Forward)
        // in the *initial* robot frame.
        // To find the robot center from the *current* anchor pose, we assume this
        // offset is a fixed geometric property of how the HMD is mounted relative to the robot's true center.
        // This offset vector, when expressed in the robot's *current* local space,
        // still points from the robot center to the HMD.
        Vector3 offsetInCurrentRobotLocalSpace = new Vector3(CalculatedOffset_InitialRobotFrame.x, 0, CalculatedOffset_InitialRobotFrame.y);

        // Transform this local space offset to world space using the *current* anchor/robot rotation
        Vector3 offsetInWorldSpace = currentAnchorTransform.rotation * offsetInCurrentRobotLocalSpace;

        // RobotCenter_World = AnchorPosition_World - OffsetVector_World
        Vector3 robotCenterWorldPos = currentAnchorTransform.position - offsetInWorldSpace;

        return new Vector2(robotCenterWorldPos.x, robotCenterWorldPos.z);
    }
}