using UnityEngine;

namespace ProjectS1.Gameplay
{
    /// <summary>
    /// Fixed-angle top-down camera that smooth-follows a target on the XZ plane.
    /// Pitch/yaw stay constant so screen-space directions never drift.
    /// </summary>
    [ExecuteAlways]
    public class TopDownCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);

        [Header("Framing")]
        [Range(20f, 89f)]
        [SerializeField] private float _pitch = 55f;
        [Range(-180f, 180f)]
        [SerializeField] private float _yaw = 0f;
        [SerializeField] private float _distance = 22f;

        [Header("Follow")]
        [SerializeField] private float _smoothTime = 0.18f;
        [Tooltip("Snap instantly while not in Play Mode so scene framing is always accurate.")]
        [SerializeField] private bool _snapInEditMode = true;

        private Vector3 _velocity;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void OnEnable()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = _target.position + _targetOffset - rotation * Vector3.forward * _distance;

            bool snap = !Application.isPlaying && _snapInEditMode;
            transform.position = snap
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);
            transform.rotation = rotation;
        }

        /// <summary>Places the camera at its framing position immediately, skipping the damping.</summary>
        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = _target.position + _targetOffset - rotation * Vector3.forward * _distance;
            transform.SetPositionAndRotation(position, rotation);
            _velocity = Vector3.zero;
        }
    }
}
