using Unity.Entities;

namespace ECS_Sound.Components
{
    public struct ActiveSoundSourceComponent : IComponentData 
    {
        // Contains ids of interaction which CollisionSoundHybridAudioSystem should play
        internal uint interactionsIds;

        private const uint INTERACTION_ID_MASK = 1;

        internal bool IsPlaySound(int interactionId)
        {
            return ((interactionsIds >> interactionId) & INTERACTION_ID_MASK) == 1;
        }
        
        internal void SetPlaySoundActive(int interactionId, bool isActive)
        {
            interactionsIds = isActive 
                ? (interactionsIds | (uint) (1 << interactionId))
                : (interactionsIds & (uint) ~(1 << interactionId));
        }
    }
}
