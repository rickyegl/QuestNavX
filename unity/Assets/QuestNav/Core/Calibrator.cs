using System.Collections.Generic;
using Meta.XR.Editor.Tags;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Calibrator : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField]
    private GameObject testSphere;

    [SerializeField]
    private RayInteractor rightInteractor;

    [SerializeField]
    private PlaneSurface floor;

    [SerializeField]
    private GameObject indicatorDown;

    [SerializeField]
    private GameObject indicatorUp;

    [SerializeField]
    private TMP_Dropdown layoutSelector;

    [SerializeField]
    private GameObject buttonsList;

    [SerializeField]
    private GameObject buttonPrefab;

    [SerializeField]
    private TextAsset[] jsons;



    private FieldLayoutData activeFieldLayoutData;

    void Start()
    {
        layoutSelector.ClearOptions();

        for (int i = 0; i < jsons.Length; i++)
        {
            layoutSelector.options.Add(new TMP_Dropdown.OptionData(jsons[i].name));
        }

        layoutSelector.onValueChanged.AddListener(updateTagSelection);
        updateTagSelection(0);

        indicatorDown.SetActive(false);
        indicatorUp.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        SurfaceHit hit;
        floor.Raycast(rightInteractor.Ray, out hit, rightInteractor.MaxRayLength);

        Vector3 rayPose = hit.Point;

        testSphere.transform.position = rayPose;

        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
        {
            indicatorDown.SetActive(true);
            indicatorUp.SetActive(false);
            indicatorDown.transform.position = rayPose;
        }
        else if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger))
        {
            indicatorUp.SetActive(true);
            indicatorUp.transform.position = rayPose;
        }

    }

    void updateTagSelection(int index)
    {
        foreach (Transform child in buttonsList.transform)
            {
                Destroy(child.gameObject);
            }
        activeFieldLayoutData = JsonUtility.FromJson<FieldLayoutData>(jsons[index].text);
        print("Showing " + activeFieldLayoutData.tags.Count + " tags");
        for (int i = 0; i < activeFieldLayoutData.tags.Count; i++)
        {
            GameObject button = Instantiate(buttonPrefab, buttonsList.transform);
            button.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = "Apriltag " + activeFieldLayoutData.tags[i].ID.ToString();
            TagData tagData = activeFieldLayoutData.tags[i];
            button.GetComponentInChildren<Button>().onClick.AddListener(() => OnTagButtonClicked(tagData));
        }
    }
    
    void OnTagButtonClicked(TagData tagData)
    {
        print("Tag ID: " + tagData.ID);
    }
        
}








[System.Serializable]
public class QuaternionData
{
    public double W;
    public double X;
    public double Y;
    public double Z;
}

[System.Serializable]
public class RotationData
{
    public QuaternionData quaternion;
}

[System.Serializable]
public class TranslationData
{
    public double x;
    public double y;
    public double z;
}

[System.Serializable]
public class PoseData
{
    public TranslationData translation;
    public RotationData rotation;
}

[System.Serializable]
public class TagData
{
    public int ID;
    public PoseData pose;
}

[System.Serializable]
public class FieldData
{
    public double length;
    public double width;
}

[System.Serializable]
public class FieldLayoutData
{
    public List<TagData> tags; // Use List for JSON arrays
    public FieldData field;
}