using Unity.Entities;

namespace ECS_Sound.Components
{
    public readonly struct CollisionSoundComponent : IComponentData
    {
        public readonly int TouchClipId;
        public readonly int SlideClipId;
        public readonly int ConfigurationId; // Contains id of configurations

        public CollisionSoundComponent(int touchClipId, int slideClipId, int configurationId)
        {
            TouchClipId = touchClipId;
            SlideClipId = slideClipId;
            ConfigurationId = configurationId;
        }
    }
}
