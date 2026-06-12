using UnityEngine;

/// <summary>
/// LateUpdate에서 플레이어를 부드럽게 따라가는 간단한 2D 카메라입니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SimpleCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.6f, -10f);
    [SerializeField] private float smoothTime = 0.18f;
    [SerializeField] private float minY = -0.2f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.y = Mathf.Max(minY, desiredPosition.y);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0.01f, smoothTime);
    }
}
