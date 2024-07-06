using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECS_Sound.Components
{
    public struct CollisionSoundInteractionsComponent : IComponentData
    {
        // AudioSource PlayOneShot has limitation in 10 clips. It can be extended.
        internal int Length => 10;

        public CollisionInteraction Interaction0;
        public CollisionInteraction Interaction1;
        public CollisionInteraction Interaction2;
        public CollisionInteraction Interaction3;
        public CollisionInteraction Interaction4;
        public CollisionInteraction Interaction5;
        public CollisionInteraction Interaction6;
        public CollisionInteraction Interaction7;
        public CollisionInteraction Interaction8;
        public CollisionInteraction Interaction9;

        public CollisionInteraction this[int interactionId]
        {
            get => GetInteraction(interactionId);
            set => SetInteraction(interactionId, value);
        }
        
        private CollisionInteraction GetInteraction(int interactionId)
        {
            switch (interactionId)
            {
                case 0: return Interaction0;
                case 1: return Interaction1;
                case 2: return Interaction2;
                case 3: return Interaction3;
                case 4: return Interaction4;
                case 5: return Interaction5;
                case 6: return Interaction6;
                case 7: return Interaction7;
                case 8: return Interaction8;
                case 9: return Interaction9;
            }
            return CollisionInteraction.Null;
        }

        private void SetInteraction(int interactionId, in CollisionInteraction interaction)
        {
            switch (interactionId)
            {
                case 0: Interaction0 = interaction; break;
                case 1: Interaction1 = interaction; break;
                case 2: Interaction2 = interaction; break;
                case 3: Interaction3 = interaction; break;
                case 4: Interaction4 = interaction; break;
                case 5: Interaction5 = interaction; break;
                case 6: Interaction6 = interaction; break;
                case 7: Interaction7 = interaction; break;
                case 8: Interaction8 = interaction; break;
                case 9: Interaction9 = interaction; break;
            }
        }

        public int GetFirstCleanInteractionId()
        {
            for (var interactionId = 0; interactionId < Length; interactionId++)
            {
                var interaction = GetInteraction(interactionId);
                if (interaction.Equals(CollisionInteraction.Null))
                {
                    return interactionId;
                }
            }
            return -1;
        }

        public int GetInteractionId(Entity collisionEntity)
        {
            for (var interactionId = 0; interactionId < Length; interactionId++)
            {
                var interaction = GetInteraction(interactionId);
                if (interaction.CollisionEntity == collisionEntity)
                {
                    return interactionId;
                }
            }
            return -1;
        }

        private const float CLEANING_PERCENT = 0.8f;
        public void Clean()
        {
            var oldestInteraction = double.MaxValue;
            var newestInteraction = double.MinValue;
            for (var interactionId = 0; interactionId < Length; interactionId++)
            {
                var interaction = GetInteraction(interactionId);
                if (interaction.Equals(CollisionInteraction.Null)) continue;
                oldestInteraction = math.min(oldestInteraction, interaction.UpdatedTime);
                newestInteraction = math.max(newestInteraction, interaction.UpdatedTime);
            }

            var cleaningThreshold = oldestInteraction + (newestInteraction - oldestInteraction) * CLEANING_PERCENT;
            for (var interactionId = 0; interactionId < Length; interactionId++)
            {
                var interaction = GetInteraction(interactionId);
                if (cleaningThreshold > interaction.UpdatedTime)
                {
                    SetInteraction(interactionId, CollisionInteraction.Null);
                }
            }
        }
    }

    public struct CollisionInteraction
    {
        public int MainClipId;
        public int SecondaryClipId;
        public int ConfigurationId; // Contains Id with configurations for clip play
        public float VolumeScale;
        public double UpdatedTime;
        public double PlayClipEndTime;
        public float3 AverageContactPoint;
        public Entity CollisionEntity;
        public LocalToWorld EntityToWorld;

        public static CollisionInteraction Null => default;
        
        public bool Equals(CollisionInteraction other)
        {
            return CollisionEntity == other.CollisionEntity
                   && ConfigurationId == other.ConfigurationId
                   && MainClipId == other.MainClipId
                   && SecondaryClipId == other.SecondaryClipId;
        }
    }
}
