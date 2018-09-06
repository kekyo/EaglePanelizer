# EAGLE CAD artwork panelizer

## What is this?

* This is simple tool for panelize EAGLE CAD artwork.
* [Motivates from this tweets](https://twitter.com/kekyo2/status/852850454740353024) "Specify X, Y on the EAGLE just make faces with Gerber in 1 shot."

## Download binary

* [Released 1.1 (for .NET 4.5 binary)](https://github.com/kekyo/EaglePanelizer/releases/download/1.1/EaglePanelizer-1.1.zip)

## Example

* Original artwork (*.brd file)

![Original artwork](OriginalArtwork.png)

* Apply this tool then:

![Panelized artwork](PanelizedArtwork.png)

## How to use

* You can see the help without some arguments:
  * Width and height units are milli-meter.

```
D:\>EaglePanelizer.exe
EaglePanelizer - EAGLE CAD artwork panelizer 1.1
Copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)

usage: EaglePanelizer <width> <height> <from-path> <panelized-path>
ex: EaglePanelizer 100 100 pga44dip44.brd panelized.brd
```

## License

* Source code copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)
* Under Apache v2 http://www.apache.org/licenses/LICENSE-2.0

## History

* 1.1:
  * Calculate contour with package's.
  * Added usage help.
  * Support .NET Core 2.0.
  * Upgraded to MSBuild 2.0.
* 1.0:
  * First public release.
