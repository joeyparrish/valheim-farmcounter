# FarmCounter - A Valheim Mod by Joey Parrish

Source: https://github.com/joeyparrish/valheim-farmcounter

[FarmCounter Preview Video<br>
![FarmCounter Preview Video](https://img.youtube.com/vi/DPXoDrkEHrg/hq2.jpg)](
https://youtu.be/DPXoDrkEHrg "FarmCounter Preview Video")


## About

FarmCounter lets you add special tags to a sign that will be replaced with
counts of nearby creatures.

For example, `$tame_boar` would be replaced with a count of "nearby" tamed
boars, while `$wild_deer` would be replaced with a count of "nearby" wild deer.

See [the definition of "nearby"](#defining-nearby) below.


## Installation

FarmCounter is published on both ThunderStore and Nexus Mods.  Install using
your favorite mod manager.


## Tags for vanilla tameable creatures

| Creature      | Tag               |
| ------------- | ----------------- |
| Tame Boar     | `$tame_boar`      |
| Tame Piggy    | `$tame_boarpiggy` |
| Tame Wolf     | `$tame_wolf`      |
| Tame Wolf Cub | `$tame_wolfcub`   |
| Tame Lox      | `$tame_lox`       |
| Tame Lox Calf | `$tame_loxcalf`   |


## Tag construction

A tag has a prefix and a suffix.

The prefix is either `$tame_` to count tame creatures, `$wild_` to count wild
creatures, or `$all_` to count both.

The suffix is the untranslated name of a creature.  A full list can be found
in [Jotunn's docs on translation data](https://valheim-modding.github.io/Jotunn/data/localization/translations/English.html).
If the name is `$enemy_boar`, the suffix is simply `boar`.

So, for example, `$all_goblin` would count all nearby Fulings.  If you're using
a mod like [AllTameable](https://www.nexusmods.com/valheim/mods/478) or
[Pokéheim](https://github.com/joeyparrish/pokeheim#readme), then any creature
could be considered tame.


## Defining "nearby"

Valheim defines your "base" as anything in range of your workbenches, so this
mod uses a similar definition.

A creature is in range of your sign if it is range of any workbench in range of
your sign.

In vanilla Valheim, workbenches have a 20 meter range.  If you're using a mod
such as [Valheim Plus](https://github.com/valheimPlus/ValheimPlus), FarmCounter
will detect the overridden distance from the other mod and use that instead.


## Dependencies

Just [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)


## Incompatibilities

None that we know of!


## Multiplayer

If someone is not using the mod, they will simply see the raw tag on the sign.


## Credits

FarmCounter was created by [Joey Parrish](https://joeyparrish.github.io/).
