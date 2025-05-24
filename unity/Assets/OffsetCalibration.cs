using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OffsetCalibration : MonoBehaviour
{

    [SerializeField]
    private GameObject startCalibrationButton;

    [SerializeField]
    private TMP_Text buttonText;
    [SerializeField]
    private GameObject yesButton;
    [SerializeField]
    private GameObject noButton;
    [SerializeField]
    private GameObject timerText;
    private float timer = 5.0f;

    private CalibrationState state = CalibrationState.Idle;
    enum CalibrationState
    {
        Idle,
        Confirming,
        Calibrating,
        Waiting
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        yesButton.SetActive(false);
        noButton.SetActive(false);
        timerText.SetActive(false);
        startCalibrationButton.GetComponent<Button>().onClick.AddListener(StartCalibration);
        yesButton.GetComponentInChildren<Button>().onClick.AddListener(ConfirmCalibration);
        noButton.GetComponentInChildren<Button>().onClick.AddListener(DenyCalibration);
    }

    // Update is called once per frame
    void Update()
    {
        timer -= Time.deltaTime;
        timerText.GetComponent<TMP_Text>().text = timer.ToString("0.0");

        if (timer <= 0)
        {
            timerText.SetActive(false);
            if (state == CalibrationState.Waiting)
            {
                state = CalibrationState.Calibrating;
                timer = 10.0f;
                buttonText.text = "Spin";
                timerText.SetActive(true);
            }
            if (state == CalibrationState.Calibrating)
            {
                // Perform calibration logic here
                // For example, send the calibration data to the robot
                Debug.Log("Calibration complete");
                startCalibrationButton.GetComponent<Button>().interactable = true;
                buttonText.text = "Start Offset Calibration";
                state = CalibrationState.Idle;
            }
        }
    }

    void StartCalibration()
    {
        buttonText.text = "Sure?";
        yesButton.SetActive(true);
        noButton.SetActive(true);
        state = CalibrationState.Confirming;
        timerText.SetActive(true);
        timer = 5.0f;
    }

    void DenyCalibration()
    {
        buttonText.text = "Start Offset Calibration";
        //startCalibrationButton.GetComponent<Button>().interactable = true;
        yesButton.SetActive(false);
        noButton.SetActive(false);
        timerText.SetActive(false);
        state = CalibrationState.Idle;
    }

    void ConfirmCalibration()
    {
        buttonText.text = "Spin Robot For 10 Seconds After Timer";
        startCalibrationButton.GetComponent<Button>().interactable = false;
        yesButton.SetActive(false);
        noButton.SetActive(false);
        timerText.SetActive(true);
        timer = 9.0f;
        state = CalibrationState.Waiting;
        
    }
}
