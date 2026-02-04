using UnityEngine;

public class CameraInertiaFollow : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;          // player or camera rig
    [SerializeField] private Vector3 positionOffset;    // local offset relative to target

    [Header("Inertia")]
    [Tooltip("How quickly the camera catches up to target position. Higher = snappier.")]
    [SerializeField] private float positionLag = 10f;
    [Tooltip("How quickly the camera matches the target rotation.")]
    [SerializeField] private float rotationLag = 12f;

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Target position + offset
        Vector3 desiredPos = target.position + target.TransformVector(positionOffset);

        // Exponential smoothing parameters
        float posAlpha = 1f - Mathf.Exp(-positionLag * dt);
        float rotAlpha = 1f - Mathf.Exp(-rotationLag * dt);

        // Smooth position
        transform.position = Vector3.Lerp(transform.position, desiredPos, posAlpha);

        // Smooth rotation
        Quaternion desiredRot = target.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotAlpha);
    }
}
