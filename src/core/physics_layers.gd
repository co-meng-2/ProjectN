class_name PhysicsLayers
extends RefCounted
## 2D physics layer bitmasks. Names must match [layer_names] in project settings.

const WORLD: int = 1 << 0
const ALLY_BODY: int = 1 << 1
const ENEMY_BODY: int = 1 << 2
const ALLY_HURT: int = 1 << 3
const ENEMY_HURT: int = 1 << 4
const PICKUP: int = 1 << 5
