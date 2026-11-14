using UnityEngine;

namespace ArcaneMotion.Spells
{
    // A fired test spell. It needs a Collider and a non-kinematic Rigidbody so that anything it touches,
    // such as the DummyTarget, gets a normal collision or trigger event. It removes itself on impact
    // or after its lifetime.
    [RequireComponent(typeof(Rigidbody))]
    public class SpellProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        [SerializeField] private bool destroyOnHit = true;

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (destroyOnHit)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (destroyOnHit && !other.isTrigger)
                Destroy(gameObject);
        }
    }
}
