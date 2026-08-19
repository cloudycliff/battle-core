# V1 projectiles are linear with optional pierce; no homing

V1 projectiles move in a straight line at constant speed, use a collision circle, and despawn on lifetime, map bounds, or when pierce count is exhausted (zero means destroy on first valid hit). Target validity uses the same Hit Filter as skill effects. Homing and bounce are extension points, not V1—they couple turning to fixed-point motion and terrain in ways we are not taking on yet.
