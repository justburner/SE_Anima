# Space Engineers - Anima Script #

## Parts animation script for Space Engineers ##

This script allow SE Blocks to playback an animation, the animation is restricted to object's tranformations along a timeline.

Requires Blender 2.70 or up for exporting animations. 

GitHub available at: https://github.com/JustBurn/SE_Anima

## Features ##

* Export animations in Blender and include the exported sequence into your mod.
* Animation is per part allowing mixing multiple different sequences together.
* Ability to play any keyframe lengths and frame rate with any part.
* Can play any sequence with different speed at any time during playback.
* Negative speed will play animation backwards.
* By default part colors will be taken from root block but is possible to assign a custom color per part.
* Any sequence can be played once, looped, ping-pong or only a specific single keyframe.
* Is possible to manually set custom transformations on a part for procedual animation.
* Can hide parts and disable animation when player is far away to minimize performance impact.
* Emissive material can be changed on parts.

## Limitations ##

* Games doesn't have `System.IO.Directory` and `VRage.FileSystem.MyFileSystem` whitelisted, your mod either have to be in workshop using the exact name as modName or a folder in /Mods/ with exact name as altModName.
* Created pars won't have physics, make sure the root block has a collision box big enough to cover the whole animation area.
* No "bone weight" support, all animation elements will be transformed on object's local matrix only.
* Hiding a specific part will also hide all child parts, if Anima is disabled the Visible property of any part will always report false, keep this in mind if you use part.Visible.
* Animation Export script is only available for Blender 2.7x and up.

## Documentation ##

Documentation is available by opening `Anima.chm`.

HTML documentation may be available if requested.

## How to install it ##

Install `Anima_Blender.zip` into Blender.

Include `Anima.cs` and your own game-logic inside a script folder on your mod.

For more info how to do this please check in documentation.

### Thanks to ###

Digi - For telling me about the exposed MyEntity on Mod API and helping me a lot.

Harag - For his excellent work on the "SE Block Tools for Blender"
        http://harag-on-steam.github.io/se-blender/
