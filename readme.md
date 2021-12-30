Get your planet ready for a shiny new factory. Cover it in foundation in seconds or remove all existing factory buildings.

Coming soon, support for non-cheaty mode where foundation & soil pile from player's inventory are used (see 'Cheatiness' config properties).

Checkbox next to the action button controls whether guide lines will be added. Config controls types painted (equator, meridian, tropic)
Raising/lowering veins is now a separate operation that takes around a minute to complete. It can be enabled using the checkbox under the guide lines control

Use DeleteFactoryTrash config property to automatically delete items that would be littered

Non-cheaty options are not quite complete. For now, foundation is deducted properly but soil pile limits are not honored. For this mode,
foundation will be taken first from the user's inventory and then from storage containers and logistics stations on the current planet.

Check the "Environment Modification" menu to find the action button
![Example2](https://github.com/mattsemar/dsp-bulldozer/blob/master/Examples/example2.png?raw=true)

![Example1](https://github.com/mattsemar/dsp-bulldozer/blob/master/Examples/example1.png?raw=true)

![Example3](https://github.com/mattsemar/dsp-bulldozer/blob/master/Examples/example3.png?raw=true)

## How to install

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

Extract the archive file and drag `Bulldozer.dll` into the `BepInEx/plugins` directory.

#### Mod manager

Click the `Install with Mod Manager` link above.

## Acknowledgements

Thanks to runeranger, Madac, Veretragna, Narceen & Bem on Discord for helpful suggestions and bug reports.

## Changelog

#### v1.0.25

Bugfix: resolved issue where veins were not being raised (thanks Valoneu)

#### v1.0.24

Skip over landing capsule when removing planet vegetation

#### v1.0.23

Added option for skipping logistics stations when destroying factory components

#### v1.0.22

Fix bug in veins alteration for planets where no foundation was placed

#### v1.0.21

Added ability to customize colors for all guide mark types

#### v1.0.20

Added option to paint poles  

#### v1.0.19

Added support for minor meridian lines. Set MinorMeridianInterval > 0 in config window to add  

#### v1.0.18

Added config window (thanks runeranger for very helpful feedback)
Improved predicated soil pile usage calculation

#### v1.0.17

Fix issue with config property being out of sync with UI

#### v1.0.16

Speed up operation when veins don't need to be altered

#### v1.0.15

Fixed tropic painting for non-default radius planets

#### v1.0.14

Add option to automatically delete items that would have been littered

#### v1.0.13

Add Universe Exploration 3 as a prerequisite, fix issue with meridian line being too thin at pole. Add tropic line painting

#### v1.0.12

Fix issue where checkbox wasn't being hidden

#### v1.0.11
Add confirmation prompt
Enable destruction of factory machines (assemblers, etc). By default will not do foundation when enabled

#### v1 - v1.0.10
Added support for BepInEx Configuration
Add config option for overriding raise/bury veins, defaults to using same settings as game ui
Add config option for overriding foundation decoration style, default is to follow game ui setting
Add config option to enable skipping equator or meridian painting (individually)
Add config option for number of update actions executed on each frame
Added better support for larger radius planets
Fix issue with hover text position
Add option to skip repaving already paved locations
Fix equator & meridian lines for planets with non-default diameter
Added hover text to action button and checkbox and added ability to cancel execution
Fixed issue where action button icon was showing in other categories
More points added to try and catch missed veins in raise/lower on subsequent executions

## Contact
Bugs? Contact me on discord: mattersnot#1983 or create an issue in the github repository.

Icons made by [Freepik](https://www.freepik.com)