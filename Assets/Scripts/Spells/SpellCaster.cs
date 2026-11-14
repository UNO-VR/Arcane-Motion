using System;
using System.Collections.Generic;
using ArcaneMotion.Gestures;
using UnityEngine;
using UnityEngine.Events;

namespace ArcaneMotion.Spells
{
    // Everything a listener needs to know about one cast.
    public readonly struct SpellCastInfo
    {
        public SpellCastInfo(SpellGesture spell, RecognitionState state, Vector3 origin, Quaternion rotation)
        {
            Spell = spell;
            State = state;
            Origin = origin;
            Rotation = rotation;
        }

        // The spell asset the player drew (for example PH Star). Map this to behavior.
        public SpellGesture Spell { get; }

        // The recognition state at release: shape, score, size.
        public RecognitionState State { get; }

        // The controller's world position and rotation at release. Rotation * forward is the aim.
        public Vector3 Origin { get; }
        public Quaternion Rotation { get; }
    }

    [Serializable]
    public class SpellCastEvent : UnityEvent<SpellCastInfo> { }

    // Turns a completed gesture into a cast. Put it on the same object as a GestureCapture (the
    // Gesture Hand prefab). Gesture code never references this; this listens to GestureCapture.Completed.
    //
    // A cast happens only when the gesture is released while the status is Spell. To react to casts
    // from other scripts, subscribe to Cast (C#) or add a listener to On Cast (Inspector).
    // One spell's projectile.
    [Serializable]
    public class SpellEffect
    {
        [Tooltip("The spell this entry is for, for example PH Star or PH Small Circle.")]
        [SerializeField] private SpellGesture spell;

        [Tooltip("The prefab fired for this spell. It needs a Collider.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Colors the prefab's particle systems and trails with the spell's ink color. Leave off " +
                 "if the prefab's own colors are already right.")]
        [SerializeField] private bool tintWithInk;

        [Tooltip("This spell's speed in meters per second, or 0 to use the Spell Caster's Projectile Speed.")]
        [SerializeField, Min(0f)] private float speed;

        public SpellGesture Spell => spell;
        public GameObject Prefab => prefab;
        public bool TintWithInk => tintWithInk;
        public float Speed => speed;
    }

    [RequireComponent(typeof(GestureCapture))]
    public class SpellCaster : MonoBehaviour
    {
        [Header("Cast event")]
        [Tooltip("Raised once per successful cast. Hook up spell effects here.")]
        [SerializeField] private SpellCastEvent onCast = new SpellCastEvent();

        [Header("Default test spell: a glowing orb fired along the controller")]
        [Tooltip("Fires the orb on every cast, so recognition can be tested end to end. Turn off once real " +
                 "spells listen to the event.")]
        [SerializeField] private bool fireTestProjectile = true;

        [Tooltip("One entry per spell: which prefab it fires. A spell with no entry, or an entry with no " +
                 "prefab, uses the Fallback Prefab, or the plain orb if that is empty too.")]
        [SerializeField] private List<SpellEffect> spellEffects = new List<SpellEffect>();

        [Tooltip("Optional. Used for any spell without its own entry above. A prefab with a Collider. If " +
                 "empty, a plain orb is built at runtime and tinted with the spell's ink color.")]
        [SerializeField] private GameObject projectilePrefab;

        [SerializeField, Min(0f)] private float projectileSpeed = 6f;
        [SerializeField, Min(0.01f)] private float projectileSize = 0.12f;

        [Tooltip("How far in front of the controller the orb appears, in meters.")]
        [SerializeField, Min(0f)] private float spawnOffset = 0.15f;

        [Header("Testing")]
        [Tooltip("Logs every recognition change and every release to the Console.")]
        [SerializeField] private bool logToConsole = true;

        private GestureCapture capture;

        public event Action<SpellCastInfo> Cast;

        private void Awake()
        {
            capture = GetComponent<GestureCapture>();
        }

        private void OnEnable()
        {
            capture.StateChanged += OnStateChanged;
            capture.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            capture.StateChanged -= OnStateChanged;
            capture.Completed -= OnCompleted;
        }

        private void OnStateChanged(GestureCapture sender, RecognitionState state)
        {
            if (!logToConsole)
                return;
            Debug.Log($"[Spell] {name}: {Describe(state)}", this);
        }

        private void OnCompleted(GestureCapture sender, RecognitionState shown)
        {
            if (shown.Status != RecognitionStatus.Spell || shown.Spell == null)
            {
                if (logToConsole)
                    Debug.Log($"[Spell] {name}: released, NO CAST ({Describe(shown)})", this);
                return;
            }

            Transform controller = sender.TrackedController;
            var info = new SpellCastInfo(shown.Spell, shown, controller.position, controller.rotation);

            if (logToConsole)
                Debug.Log($"[Spell] {name}: released, CAST {shown.Spell.name} ({Describe(shown)})", this);

            Cast?.Invoke(info);
            onCast.Invoke(info);

            if (fireTestProjectile)
                FireProjectile(info);
        }

        private static string Describe(RecognitionState s)
        {
            string shape = s.Shape != null ? s.Shape.name : "none";
            string spell = s.Spell != null ? s.Spell.name : "-";
            return $"status={s.Status}, shape={shape}, spell={spell}, size={s.Size:0.000}m, score={s.Score:0.000}";
        }

        private SpellEffect FindEffect(SpellGesture spell)
        {
            foreach (SpellEffect effect in spellEffects)
                if (effect != null && effect.Spell == spell)
                    return effect;
            return null;
        }

        // Colors every particle system and trail in the instance.
        private static void Tint(GameObject instance, Color color)
        {
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = color;
            }
            foreach (TrailRenderer trail in instance.GetComponentsInChildren<TrailRenderer>())
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < spellEffects.Count; i++)
            {
                if (spellEffects[i] == null || spellEffects[i].Spell == null)
                    continue;
                for (int j = 0; j < i; j++)
                    if (spellEffects[j] != null && spellEffects[j].Spell == spellEffects[i].Spell)
                        Debug.LogWarning($"[Spell] {name}: {spellEffects[i].Spell.name} has more than one entry; " +
                                         "only the first is used.", this);
            }
        }

        private void FireProjectile(SpellCastInfo info)
        {
            Vector3 aim = info.Rotation * Vector3.forward;
            Vector3 position = info.Origin + aim * spawnOffset;

            SpellEffect effect = FindEffect(info.Spell);
            GameObject prefab = effect != null && effect.Prefab != null ? effect.Prefab : projectilePrefab;
            float speed = effect != null && effect.Speed > 0f ? effect.Speed : projectileSpeed;

            GameObject orb;
            if (prefab != null)
            {
                orb = Instantiate(prefab, position, info.Rotation);
                if (effect != null && effect.Prefab != null && effect.TintWithInk)
                    Tint(orb, info.Spell.InkColor);
            }
            else
            {
                orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = $"Spell Orb ({info.Spell.name})";
                orb.transform.SetPositionAndRotation(position, info.Rotation);
                orb.transform.localScale = Vector3.one * projectileSize;
                orb.GetComponent<Renderer>().material.color = info.Spell.InkColor;
            }

            Rigidbody body = orb.GetComponent<Rigidbody>();
            if (body == null)
                body = orb.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = aim * speed;

            if (orb.GetComponent<SpellProjectile>() == null)
                orb.AddComponent<SpellProjectile>();

            // The orb starts at the player's hand, so it must not hit the player's own rig.
            Collider[] orbColliders = orb.GetComponentsInChildren<Collider>();
            Collider[] rigColliders = capture.TrackingSpace.root.GetComponentsInChildren<Collider>();
            foreach (Collider a in orbColliders)
                foreach (Collider b in rigColliders)
                    Physics.IgnoreCollision(a, b);
        }
    }
}
