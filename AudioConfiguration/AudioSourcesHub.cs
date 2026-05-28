using ECS_Common.Utils;
using ECS_Sound.Components;
using Unity.Entities;
using UnityEngine;

namespace ECS_Sound.Systems
{
    /// <summary>
    /// Pool of <see cref="AudioSource"/>s with score-based stealing and slide management.
    ///
    /// Acquisition policy:
    ///   1. If an idle source exists, claim it.
    ///   2. Otherwise, steal the source whose currently-playing sound has the lowest score
    ///      iff the incoming score is strictly higher.
    ///   3. Otherwise, drop the new sound.
    ///
    /// Slide management: each frame, expired slide slots (their interaction hasn't been updated
    /// recently → the slide stopped) are released. Hard-stop unless the clip has only a small
    /// amount of time left (grace), in which case it's allowed to finish naturally.
    /// </summary>
    public class AudioSourcesHub
    {
        private readonly AudioSource[] sources;
        private readonly Slot[] slots;

        private struct Slot
        {
            public bool InUse;
            public double PlayEndTime;
            public float Score;

            // Slide tracking
            public bool IsSlide;
            public Entity ConsumerEntity;
            public int InteractionId;
        }

        public AudioSourcesHub(int size)
        {
            sources = new AudioSource[size];
            slots = new Slot[size];

            var template = new GameObject("AudioSource for active CollisionSound 0");
            Object.DontDestroyOnLoad(template);
            sources[0] = template.AddComponent<AudioSource>();
            template.AddComponent<AutoDisablePlayGameObject>();
            template.SetActive(false);

            for (var i = 1; i < sources.Length; i++)
            {
                var instance = Object.Instantiate(template);
                Object.DontDestroyOnLoad(instance);
                instance.name = $"AudioSource for active CollisionSound {i}";
                sources[i] = instance.GetComponent<AudioSource>();
            }
        }

        public int Capacity => sources.Length;

        /// <summary>Refresh natural-completion state. Call once per frame before everything else.</summary>
        public void Tick(double now)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].InUse && now >= slots[i].PlayEndTime)
                {
                    ReleaseSlot(i, hardStop: false); // Already done naturally.
                }
            }
        }

        /// <summary>
        /// Stop slide sources whose interaction is no longer being updated (the slide ended).
        /// Hard-stop unless the clip has &lt; <paramref name="graceTime"/> seconds remaining.
        /// </summary>
        public void StopExpiredSlides(double now, double staleThreshold, double graceTime,
            ComponentLookup<CollisionSoundInteractionsComponent> interactionsLookup)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].InUse || !slots[i].IsSlide) continue;
                if (!interactionsLookup.HasComponent(slots[i].ConsumerEntity))
                {
                    // The consumer entity was destroyed; let the source finish naturally.
                    // (It'll be released by Tick when PlayEndTime passes.)
                    continue;
                }

                var interactions = interactionsLookup[slots[i].ConsumerEntity];
                var interaction = interactions[slots[i].InteractionId];
                var staleness = now - interaction.UpdatedTime;
                if (staleness <= staleThreshold) continue;

                // Slide has stopped. Hard-stop unless within grace window.
                var remaining = slots[i].PlayEndTime - now;
                if (remaining > graceTime)
                {
                    ReleaseSlot(i, hardStop: true);
                }
                // Otherwise: let it finish; Tick will release it.
            }
        }

        /// <summary>
        /// Try to acquire a source. Returns false if pool is full AND incoming score doesn't
        /// outrank any playing sound.
        /// </summary>
        public bool TryAcquire(
            float incomingScore, double playEndTime,
            bool isSlide, Entity consumerEntity, int interactionId,
            out AudioSource audioSource, out int slotIndex)
        {
            int idle = -1;
            int worstPlaying = -1;
            float worstPlayingScore = float.MaxValue;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].InUse)
                {
                    idle = i;
                    break;
                }
                if (slots[i].Score < worstPlayingScore)
                {
                    worstPlayingScore = slots[i].Score;
                    worstPlaying = i;
                }
            }

            int chosen;
            if (idle >= 0)
            {
                chosen = idle;
            }
            else if (incomingScore > worstPlayingScore)
            {
                // Steal. Stop the previous sound abruptly (no grace) — the new sound is more important.
                if (sources[worstPlaying] != null && sources[worstPlaying].isPlaying)
                    sources[worstPlaying].Stop();
                chosen = worstPlaying;
            }
            else
            {
                audioSource = null;
                slotIndex = -1;
                return false;
            }

            slots[chosen] = new Slot
            {
                InUse = true,
                Score = incomingScore,
                PlayEndTime = playEndTime,
                IsSlide = isSlide,
                ConsumerEntity = consumerEntity,
                InteractionId = interactionId,
            };
            audioSource = sources[chosen];
            slotIndex = chosen;
            return true;
        }

        private void ReleaseSlot(int i, bool hardStop)
        {
            if (hardStop && sources[i] != null && sources[i].isPlaying)
            {
                sources[i].Stop();
                // Ensure the GameObject is deactivated; AutoDisablePlayGameObject only fires on natural end.
                if (sources[i].gameObject.activeSelf) sources[i].gameObject.SetActive(false);
            }
            slots[i] = default; // Resets InUse to false and clears Entity/IDs.
        }

        public void Dispose()
        {
            foreach (var src in sources)
            {
#if UNITY_EDITOR
                if (src != null) Object.DestroyImmediate(src.gameObject);
#else
                if (src != null) Object.Destroy(src.gameObject);
#endif
            }
        }
    }
}
