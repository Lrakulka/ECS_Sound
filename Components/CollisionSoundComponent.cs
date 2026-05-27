using Unity.Entities;

namespace ECS_Sound.Components
{
    /// <summary>
    /// Identifies which <see cref="ECS_Sound.AudioConfiguration.CollisionSoundConfiguration"/>
    /// this entity uses. Clip selection happens at play time (one random clip per impact from the
    /// configuration's touch/slide arrays), so clip IDs are no longer cached here.
    /// </summary>
    public readonly struct CollisionSoundComponent : IComponentData
    {
        public readonly int ConfigurationId;

        public CollisionSoundComponent(int configurationId)
        {
            ConfigurationId = configurationId;
        }
    }
}
