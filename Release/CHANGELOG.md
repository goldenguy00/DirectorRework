## 1.2.0

- Added linear scaling
- More defaults updated.
- Horde of many specific cfg options
- Couple additional spawn algorithm tweaks
- Small fixes
- Renamed a cfg section, so all Director Tweaks settings will be default. sorry.

## 1.1.3

- Adjusted defaults

## 1.1.2

- Added UseRecommendedValues setting for DirectorTweaks. Disable to apply your own changes to this section.
- Tweaked things to work better with the previous update
- Changed the target of some hooks

## 1.1.1

- Adjusted the monsterCard selection algorithm to match vanilla a bit better
    - This should help with compat
- Fixed some faulty math with the mountain shrine settings
- Changed credit refund to go to the combat director that spawned the enemy

## 1.1.0

- Added Elite Reworks as a dependency.
    - Nothing will break if it's removed, but the intended experience is to have this installed.
- Added Risk of Options as a dependency.
    - Once again, nothing will break without it, but the customizability of the director is one of the key points of this mod.
- Affixes given to a void infestor now are given to their host
- Void infestors will regain their affixes after the host has died
- Added even more configs relating to elite affix stacking
- Minor improvements to codebase

## 1.0.5

- Adjusted default configs
- Affix stacking defaults to false
- Various combat director values default to non-values to work with enemy variety better.

## 1.0.4

- All config options will be applied instantly
- Moved director credit refund to apply on death instead of on spawn
- Seperated enemy diversity into three independent catagories (boss, normal spawn, void fields)

## 1.0.3

- Made all config options apply either on stage start or instantly
- Added config for enabling and disabling bosses entirely (world spawn or event)
- Fixed bug with affixes getting applied to non-elites even with the config option enabled
- Adjusted some defaults
 
## 1.0.2

- Added config option to turn on enemy diversity in void fields (default false)

## 1.0.1

- Various config updates and some bugfixes

- ==**DELETE YOUR OLD CONFIG CUZ NOTHING FROM THAT WILL WORK ANYMORE SORRY**==

- Fixed Director Tweaks (various things just not working or working poorly).  Defaults are now the vanilla values.

- Internally separated Director Tweaks from Director Main (just enemy variety and credit refund atm).

- Made Affix stacking slightly cheaper and credit refund actually logical and not bad.

## 1.0.0

- First release