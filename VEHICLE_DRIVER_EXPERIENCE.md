# Vehicle driver experience

This branch contains the first review version of the driver flow.

## Current implementation

- The Car Rapide starts stationary with its engine off.
- **E** opens the actual `Porte_avant_gauche` object already contained in the Car Rapide FBX. No fake procedural door is created.
- **E** again moves the driver to the seat while the camera approaches the cabin.
- **R** starts the engine; controls stay disabled before that.
- The camera then moves progressively behind the vehicle.
- A receiver/apprentice is attached to the rear platform.
- Start and driving engine sounds are generated at runtime.
- The driver/receiver layout is derived from the actual vehicle bounds instead of fixed world positions.

## Fix character appearance

The VALID FBX files embed their textures/materials. Unity can import them as plain white if they are not extracted.

In Unity use:

`Car Rapide > Vehicle > Download / Fix Driver & Receiver`

or, if the FBX files already exist:

`Car Rapide > Vehicle > Fix Existing Character Materials`

Then reopen `SampleScene` and press Play.

## Controls

- **E**: open real driver door / enter
- **R**: start engine
- **W / Up**: accelerate after start
- **S / Down**: brake then reverse
- **A / D**: steer
- **Space**: handbrake
