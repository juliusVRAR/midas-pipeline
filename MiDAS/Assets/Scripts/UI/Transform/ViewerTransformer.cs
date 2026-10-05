using UnityEngine;

namespace Oculus.Interaction
{
    public class ViewerTransformer : MonoBehaviour, ITransformer
    {
        [Header("References")]
        [SerializeField] private OneGrabTranslateTransformer transformer;
        [SerializeField] private Transform head;

        [Header("Offsets")]
        [SerializeField] private float initialForwardOffset = 1.5f;
        [SerializeField] private float initialHorizontalOffset = 1f;
        private IGrabbable _grabbable;
        private Transform _target;


        /// <summary>
        /// Implementation of <see cref="ITransformer.Initialize"/>; for details, please refer to the related documentation
        /// provided for that interface.
        /// </summary>
        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            _target = _grabbable.Transform;
            transformer.Initialize(grabbable);
        }

        /// <summary>
        /// Implementation of <see cref="ITransformer.BeginTransform"/>; for details, please refer to the related documentation
        /// provided for that interface.
        /// </summary>
        public void BeginTransform()
        {
            transformer.BeginTransform();
        }

        /// <summary>
        /// Implementation of <see cref="ITransformer.UpdateTransform"/>; for details, please refer to the related documentation
        /// provided for that interface.
        /// </summary>
        public void UpdateTransform()
        {
            transformer.UpdateTransform();
            UpdateLookRotation();

        }

        void UpdateLookRotation()
        {
            Vector3 dir = head.position - _target.position;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targerRot = Quaternion.LookRotation(-dir.normalized, Vector3.up);
                _target.rotation = Quaternion.Slerp(_target.rotation, targerRot, Time.deltaTime * 15f);
            }
        }

        /// <summary>
        /// Implementation of <see cref="ITransformer.EndTransform"/>; for details, please refer to the related documentation
        /// provided for that interface.
        /// </summary>
        public void EndTransform()
        {
            transformer.EndTransform();
        }

        void OnEnable()
        {
            DefaultTransform();
        }

        public void DefaultTransform()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            
            Vector3 targetPos = head.position + head.forward * initialForwardOffset + head.right * initialHorizontalOffset;
            Vector3 lookDir = targetPos - head.position;

            transform.SetPositionAndRotation(targetPos, Quaternion.LookRotation(lookDir.normalized));
        }

    }
}
