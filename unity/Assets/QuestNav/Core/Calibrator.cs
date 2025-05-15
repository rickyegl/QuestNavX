using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

public class Calibrator : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField]
    private OVRHand leftHand;

    [SerializeField]
    private GameObject testSphere;

    [SerializeField]
    private RayInteractor rightInteractor;

    [SerializeField]
    private PlaneSurface floor;
    void Start()
    {   

    }

    // Update is called once per frame
    void Update()
    {
        SurfaceHit hit;
        floor.Raycast(rightInteractor.Ray, out hit, rightInteractor.MaxRayLength);

        Vector3 rayPose = hit.Point;

        testSphere.transform.position = rayPose;

        
    }
}
