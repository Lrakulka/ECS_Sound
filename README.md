 ## DOTS Collision Sound

Implements two feature: sound between Objects Collision and RayCast collision with object.

* RayCast - attach `RayCollider` to object abd it will start to cast a small ray which 
  in case of intersection with something will trigger the sound. (PLayer Steps)
* Object Collision - those object that rises events(`Physics Shape`) during collision 
  will trigger the sound
  
Both methods require `CollisionSound` component that contains configuration for `SoundSource` 
and Audio Clips to play (Touch Clip and Slide Clip). 
`CollisionSound` contains parameter `isMakingSound`, if this parameter - true 
then a `SoundSource` will be attached to the Entity and logic will use it for clip play.
Also, `CollisionSound` has parameter `configuration` this field contains configuration, 
it will be used for setting configuration for `SoundSource` that would play audio clip.

Currently supported two types of clips:
* Touch Clip - logic plays this sound when Entities touches each other 
  (Ball falling on the ground, bullet hits the wall)
* Slide Clip - logic plays this sound when Entities is in touch, but in motion 
  (Sphere rolling from the hill, box sliding from another box) 

`SoundSource` - is attached to active Entity as `HybridComponent`, 
logic uses `SoundSource.PlayOneShot` to play clips.

`CollisionSoundHub` - DOTS component can't store class objects that is why we are storing
audio clips and `SoundSource` configuration in static list and DOTS component
`ActiveSoundSourceComponent` stores ids to this data in those lists.

`CollisionSoundInteractionsComponent` - DOTS component contains array of Entity tracking interactions. 
The total number of simultaneously tracking interaction for entity is 10. Each of interaction 
contains data about interaction that would be used for clip play.

`ActiveSoundSourceComponent` - DOTS component which is responsible for holding status(Active/NotActive) 
for interactions in `CollisionSoundInteractionsComponent`. We are kipping this data separate from
`CollisionSoundInteractionsComponent` to trigger System`CollisionSoundHybridAudioSystem` only when we need.

`RayColliderInfoComponent` - DOTS component that contains data for RayCast.

`CollisionSoundHybridAudioSystem` - System that set prepared `SoundSource` configuration into `SoundSource`
and invokes audio clips play. The systems triggers on changes in `ActiveSoundSourceComponent`.

`CollisionSoundRayCastSystem` and `CollisionSoundSystem` - systems shares almost the same logic.
The difference is that `CollisionSoundRayCastSystem` cast RayCast and `CollisionSoundSystem` listens 
for collision events that rises. The Systems determinants how to process interaction between two entities:
* both Entities are active (has attached HybridComponent `SoundSource`) - Entity1 uses `SoundSource` 
  configuration of Entity2 to play it's audio clip, Entity2 use configuration of Entity1 to play it's clip.
* Entity1 is active and Entity2 is not active - Entity1 uses `SoundSource` configuration of Entity2 
  to play it's audio clip, Entity1 use configuration of Entity1 and audio clip of Entity2 to play it.
* Entity1 is active, Entity2 is not active and do not have configuration - Entity1 uses `SoundSource` 
  configuration of Entity1 to play it's audio clip.
If the limit of Entity interactions is exhausted, systems will call logic to try to clean old interactions,
  if a clean interaction is not found then the systems will use the last 
  interaction (data about prev active interaction will be lost).
  
Note: Dynamic Sound Source position is static itself (we do not move sound source component to collision 
interaction) that is why it recommended not to use Dynamic Sound Source on large object since it will look like
sound coming not from the collision location.
