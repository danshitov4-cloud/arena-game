using UnityEngine;

namespace ArenaGame
{
    public class CameraController : MonoBehaviour
    {
        // All constants — nothing serialized, nothing that can be lost
        private const float OffsetY     = 10f;
        private const float OffsetZ     = -8f;
        private const float PitchAngle  = 55f;
        private const float SmoothSpeed = 10f;

        private Transform _target;

        private void Awake()  => FindPlayer();
        private void Start()  => FindPlayer();

        private void LateUpdate()
        {
            if (_target == null) FindPlayer();
            if (_target == null) return;

            Vector3 desired = _target.position + new Vector3(0f, OffsetY, OffsetZ);
            transform.position = Vector3.Lerp(transform.position, desired, SmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(PitchAngle, 0f, 0f);
        }

        private void FindPlayer()
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) _target = go.transform;
        }
    }
}
