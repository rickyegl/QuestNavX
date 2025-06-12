using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.PlayerLoop;
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

    [SerializeField]
    private GameObject anchorsLocation;

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

    private List<OVRSpatialAnchor> _anchorInstances = new List<OVRSpatialAnchor>();
    private List<Guid> _anchorUuids = new List<Guid>();

    private async Task<Guid> SetupAnchorAsync(OVRSpatialAnchor anchor, bool saveAnchor)
    {
        // Keep checking for a valid and localized anchor state
        if (!await anchor.WhenLocalizedAsync())
        {
            Debug.LogError($"Unable to create anchor.");
            Destroy(anchor.gameObject);
            throw new InvalidOperationException("Anchor could not be localized.");
        }

        // Add the anchor to the list of all instances
        _anchorInstances.Add(anchor);

        // save the savable (green) anchors only
        if (saveAnchor && (await anchor.SaveAnchorAsync()).Success)
        {
            // Remember UUID so you can load the anchor later
            _anchorUuids.Add(anchor.Uuid);
            return anchor.Uuid;
        }
        throw new InvalidOperationException("Anchor could not be saved.");
    }

    public async void LoadAllAnchors()
    {
        // Load and localize
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        _anchorUuids = new List<Guid>();
        activeField.tags.ForEach(tag =>
        {
            if (tag.anchorUuid != Guid.Empty)
            {
                _anchorUuids.Add(tag.anchorUuid);
                GameObject newTag = Instantiate(debugAprilTag);
                newTag.AddComponent<OVRSpatialAnchor>();
                //unboundAnchors.Add(newTag.GetComponent<OVRSpatialAnchor>());dsadsadasdsa
            }
            else
            {
                Debug.LogWarning($"Tag {tag.ID} does not have a valid anchor UUID.");
            }
        });
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(_anchorUuids, unboundAnchors);

        if (result.Success)
        {
            foreach (var anchor in unboundAnchors)
            {
                await anchor.LocalizeAsync();//.ContinueWith(_onLocalized, anchor);
            }
        }
        else
        {
            Debug.LogError($"Load anchors failed with {result.Status}.");
        }
    }

    private void Awake()
    {

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

    async void saveTagPosition()
    {

        // 1. Determine the measured world pose of the AprilTag
        Transform definiteTransform = new GameObject().transform; // Temporary for calculation
        definiteTransform.position = (indicatorDown.transform.position + indicatorUp.transform.position) / 2f;

        // This orients definiteTransform's local Z along (indicatorUp - indicatorDown)
        // and its local Y along world up.
        definiteTransform.rotation = Quaternion.LookRotation(indicatorUp.transform.position - indicatorDown.transform.position, Vector3.up);

        // This +90 degree rotation implies that the (indicatorUp - indicatorDown) direction
        // corresponds to the tag's local X-axis (or -X), and you're rotating it
        // so that the tag's conceptual "forward" (what JSON considers Z-forward) aligns.
        // Ensure this correctly reflects your tag's physical orientation vs. JSON definition.
        definiteTransform.rotation = Quaternion.Euler(definiteTransform.rotation.eulerAngles.x, definiteTransform.rotation.eulerAngles.y + 0, 0f);

        // These are the *actual measured* world coordinates of the tag
        Vector3 tagWorldPosition = definiteTransform.position;
        Quaternion tagWorldRotation = definiteTransform.rotation;

        Destroy(definiteTransform.gameObject); // Clean up the temporary GameObject

        // 2. Update the debug visualizer (optional, but good for verification)


        // DO NOT DO THIS - IT USES JSON Z AS WORLD Y, WHICH IS WRONG HERE.
        // debugAprilTag.transform.position = new Vector3(debugAprilTag.transform.position.x, (float)selectedTag.pose.translation.z, debugAprilTag.transform.position.z);


        // 3. Get the tag's pose in Field Coordinates (from JSON, converted to Unity's system)
        // JSON X -> Unity X
        // JSON Z (elevation in JSON) -> Unity Y
        // JSON Y (depth/forward in JSON's XY plane) -> Unity Z
        Vector3 tagPositionInFieldCoords = new Vector3(
            (float)selectedTag.pose.translation.x,
            (float)selectedTag.pose.translation.z,
            (float)selectedTag.pose.translation.y
        );

        definiteTransform.transform.position = new Vector3(debugAprilTag.transform.position.x, tagPositionInFieldCoords.y, debugAprilTag.transform.position.z);
        debugAprilTag.transform.position = tagWorldPosition;
        debugAprilTag.transform.rotation = tagWorldRotation * Quaternion.Euler(0, 0, 0);

        GameObject anchorObject = Instantiate(debugAprilTag);
        anchorObject.transform.parent = anchorsLocation.transform;
        OVRSpatialAnchor anchor = anchorObject.GetComponent<OVRSpatialAnchor>();
        anchor.enabled = true;
        Guid guid = await SetupAnchorAsync(new GameObject("TagAnchor").AddComponent<OVRSpatialAnchor>(), true);
        Debug.Log("Created anchor with UUID: " + guid);

        // Convert JSON quaternion to Unity's coordinate system (Y-up) and negate yaw.
        // JSON X,Y,Z,W -> Unity Quaternion (X_json, Z_json, Y_json, W_json) to account for axis remapping
        Quaternion initialJsonOrientationInUnityAxes = new Quaternion(
            (float)selectedTag.pose.rotation.quaternion.X,
            (float)selectedTag.pose.rotation.quaternion.Z, // JSON Z-axis (up for JSON) part maps to Unity Y-axis
            (float)selectedTag.pose.rotation.quaternion.Y, // JSON Y-axis (f orward for JSON) part maps to Unity Z-axis
            (float)selectedTag.pose.rotation.quaternion.W
        );

        Vector3 eulerAngles = initialJsonOrientationInUnityAxes.eulerAngles;
        eulerAngles.y = -eulerAngles.y; // Negate yaw
        Quaternion tagRotationInFieldCoords = Quaternion.Euler(eulerAngles);


        // 4. Calculate the Field's origin pose in World coordinates (W_T_field)
        // W_T_field = W_T_tag * Inverse(F_T_tag)
        // Inverse(F_T_tag) transforms from Field's origin to Tag's origin, expressed in Tag's local frame.
        // More direct: FieldOrigin_World = Tag_WorldPose * Tag_Pose_In_Field_Inverse

        // Rotation of Field in World: R_world_field = R_world_tag * R_tag_field_inverse
        // R_tag_field_inverse = Quaternion.Inverse(tagRotationInFieldCoords)
        Quaternion fieldOriginWorldRotation = tagWorldRotation * Quaternion.Inverse(tagRotationInFieldCoords);

        // Position of Field in World: P_world_field = P_world_tag - (R_world_field * P_field_tag)
        // P_field_tag is tagPositionInFieldCoords (vector from field origin to tag, in field coords)
        Vector3 fieldOriginWorldPosition = tagWorldPosition - (fieldOriginWorldRotation * tagPositionInFieldCoords);


        // 5. Apply to the fieldObject
        fieldObject.transform.position = fieldOriginWorldPosition;
        fieldObject.transform.rotation = fieldOriginWorldRotation;

        if (activeField.tags.Find(t => t.ID == selectedTag.ID) != null)
        {
            activeField.tags.Remove(activeField.tags.Find(t => t.ID == selectedTag.ID));
        }
        activeField.tags.Add(new TagData
        {
            ID = selectedTag.ID,
            anchorUuid = guid,
            pose = new PoseData
            {
                translation = new TranslationData
                {
                    x = fieldOriginWorldPosition.x,
                    y = fieldOriginWorldPosition.y,
                    z = fieldOriginWorldPosition.z
                },
                rotation = new RotationData
                {
                    quaternion = new QuaternionData
                    {
                        W = fieldOriginWorldRotation.w,
                        X = fieldOriginWorldRotation.x,
                        Y = fieldOriginWorldRotation.y,
                        Z = fieldOriginWorldRotation.z
                    }
                }
            }
        });



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
    public Guid anchorUuid;
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