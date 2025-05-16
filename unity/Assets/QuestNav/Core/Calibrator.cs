using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

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
    void Start()
    {
        //indicatorDown.SetActive(false);
        //indicatorUp.SetActive(false);

        
        
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
}
