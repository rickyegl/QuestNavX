using System;
using System.Collections.Generic;
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

    private TagData selectedTag;

    [SerializeField]
    private LineRenderer lineRenderer;
    private FieldLayoutData activeFieldLayoutData;

    [SerializeField]
    private Button createFieldButton;

    [SerializeField]
    private TMP_InputField fieldNameText;

    private List<Field> fields;

    private Field activeField;

    [SerializeField]
    private TMP_Dropdown fieldSelector;

    [SerializeField]
    private GameObject debugAprilTag;

    [SerializeField]
    private GameObject fieldObject;

    void Start()
    {
        layoutSelector.ClearOptions();

        for (int i = 0; i < jsons.Length; i++)
        {
            layoutSelector.options.Add(new TMP_Dropdown.OptionData(jsons[i].name));
        }

        loadFields();

        createFieldButton.onClick.AddListener(createFieldButtonClicked);
        layoutSelector.onValueChanged.AddListener(updateTagSelection);
        updateTagSelection(0);

        indicatorDown.SetActive(false);
        indicatorUp.SetActive(false);
        lineRenderer.enabled = false;

    }

    void loadFields()
    {
        Debug.Log("Loading fields from " + Application.persistentDataPath + "/userTagLayouts");
        fields = new List<Field>();
        if (!System.IO.Directory.Exists(Application.persistentDataPath + "/userTagLayouts"))
        {
            System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/userTagLayouts");
        }
        string[] files = System.IO.Directory.GetFiles(Application.persistentDataPath + "/userTagLayouts", "*.json");
        foreach (string file in files)
        {
            string json = System.IO.File.ReadAllText(file);
            Field field = JsonUtility.FromJson<Field>(json);
            fields.Add(field);
        }
        fieldSelector.ClearOptions();
        //fieldNameText.text = "vTest";
        for (int i = 0; i < fields.Count; i++)
        {
            fieldSelector.options.Add(new TMP_Dropdown.OptionData(fields[i].fieldName));
        }
        fieldSelector.onValueChanged.AddListener(setActiveField);
        Debug.Log("Loaded " + fields.Count + " fields");
    }

    void setActiveField(int index)
    {
        string fieldName = fieldSelector.options[index].text;
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].fieldName == fieldName)
            {
                activeField = fields[i];
                break;
            }
        }
    }

    void createFieldButtonClicked()
    {
        Debug.Log("Creating field");
        //fieldSelector.ClearOptions();
        if (fieldNameText.text != "")
        {
            Boolean isFieldNameValid = true;
            String inputText = fieldNameText.text;
            foreach (Field iField in fields)
            {
                if (iField.fieldName == inputText)
                {
                    isFieldNameValid = false;
                    break;
                }
            }
            if (isFieldNameValid)
            {
                activeField = new Field(inputText);
                saveActiveField();
            }
            else
            {
                Debug.LogWarning("Didn't create field, name " + fieldNameText.text + " already exists");
            }
        }
        else
        {
            Debug.LogWarning("Didn't create field, name is empty");
        }

    }

    void saveActiveField()
    {
        Debug.Log("Saving field to " + Application.persistentDataPath + "/userTagLayouts");
        if (activeField != null)
        {
            string json = JsonUtility.ToJson(activeField);
            System.IO.File.WriteAllText(Application.persistentDataPath + "/userTagLayouts/" + activeField.fieldName + ".json", json);
            print("Saved field to " + Application.persistentDataPath + "/userTagLayouts/" + activeField.fieldName + ".json");

            //fieldSelector.ClearOptions();
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].fieldName == activeField.fieldName)
                {
                    fields.RemoveAt(i);
                }
            }
            fields.Add(activeField);
            fieldSelector.options.Add(new TMP_Dropdown.OptionData(activeField.fieldName));
        }
        else { Debug.LogWarning("No active field to save"); }


    }

    void checkForRays()
    {
        SurfaceHit hit;
        floor.Raycast(rightInteractor.Ray, out hit, rightInteractor.MaxRayLength);

        Vector3 rayPose = hit.Point;

        testSphere.transform.position = rayPose;
        
        OVRInput.Button button = OVRInput.Button.PrimaryIndexTrigger;

        if (OVRInput.Get(button))
        {
            indicatorUp.transform.position = rayPose;
        }
        
        if (OVRInput.GetDown(button))
        {
            indicatorDown.SetActive(true);
            indicatorUp.SetActive(false);
            indicatorDown.transform.position = rayPose;
            lineRenderer.SetPosition(0, indicatorDown.transform.position);
        }
        else if (OVRInput.GetUp(button))
        {
            indicatorUp.SetActive(true);
            lineRenderer.SetPosition(1, indicatorUp.transform.position);
            lineRenderer.enabled = true;
            saveTagPosition();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (selectedTag != null)
        {
            checkForRays();
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
        selectedTag = tagData;

    }

    void saveTagPosition()
    {

        Transform definiteTransform = new GameObject().transform;
        definiteTransform.position = (indicatorDown.transform.position + indicatorUp.transform.position) / 2f;
        definiteTransform.rotation = Quaternion.LookRotation(indicatorUp.transform.position - indicatorDown.transform.position, Vector3.up);
        definiteTransform.rotation = Quaternion.Euler(definiteTransform.rotation.eulerAngles.x, definiteTransform.rotation.eulerAngles.y + 90, 0f);

        Vector3 tagWorldPosition = definiteTransform.position;
        Quaternion tagWorldRotation = definiteTransform.rotation;

        debugAprilTag.transform.position = tagWorldPosition;
        debugAprilTag.transform.rotation = tagWorldRotation;

        debugAprilTag.transform.position = new Vector3(debugAprilTag.transform.position.x, (float)selectedTag.pose.translation.z, debugAprilTag.transform.position.z);

        Vector3 tagPositionInFieldCoords = new Vector3(
            (float)selectedTag.pose.translation.x,
            (float)selectedTag.pose.translation.z, // Field Y is from selectedTag's Z component
            (float)selectedTag.pose.translation.y  // Field Z is from selectedTag's Y component
        );

        Quaternion tagRotationInFieldCoords = new Quaternion(
            (float)selectedTag.pose.rotation.quaternion.X,
            (float)selectedTag.pose.rotation.quaternion.Z,
            (float)selectedTag.pose.rotation.quaternion.Y,
            (float)selectedTag.pose.rotation.quaternion.W
        );

        //Calculate the Field's origin pose in World coordinates (W_T_field)
        // W_T_field = W_T_tag * Inverse(F_T_tag)

        // Inverse of F_T_tag:
        // Inverse rotation:
        Quaternion inv_tagRotationInFieldCoords = Quaternion.Inverse(tagRotationInFieldCoords);
        // Inverse translation (must be rotated by the inverse rotation):
        Vector3 inv_tagPositionInFieldCoords = inv_tagRotationInFieldCoords * (-tagPositionInFieldCoords);

        // Now combine: W_T_field = W_T_tag * (Tag_T_field)
        // where Tag_T_field is Inverse(F_T_tag)

        // Rotation of Field in World:
        // R_world_field = R_world_tag * R_tag_field (where R_tag_field is inv_tagRotationInFieldCoords)
        Quaternion fieldOriginWorldRotation = tagWorldRotation * inv_tagRotationInFieldCoords;

        // Position of Field in World:
        // P_world_field = P_world_tag + R_world_tag * P_tag_field (where P_tag_field is inv_tagPositionInFieldCoords)
        Vector3 fieldOriginWorldPosition = tagWorldPosition + (tagWorldRotation * inv_tagPositionInFieldCoords);

        // Alternative calculation (often more intuitive for matrix math people):
        // fieldOriginWorldRotation = tagWorldRotation * Quaternion.Inverse(tagRotationInFieldCoords);
        // fieldOriginWorldPosition = tagWorldPosition - (fieldOriginWorldRotation * tagPositionInFieldCoords);


        //Apply to the fieldObject
        fieldObject.transform.position = fieldOriginWorldPosition;
        fieldObject.transform.rotation = fieldOriginWorldRotation;
        
        

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

[System.Serializable] // This attribute is crucial for JsonUtility
public class Field
{
    public string fieldName;
    public List<TagData> tags;
    public Field(string fieldName)
    {
        this.fieldName = fieldName;
        tags = new List<TagData>();
    }

}