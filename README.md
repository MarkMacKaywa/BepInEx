﻿<p align="center">
    <img src="https://avatars2.githubusercontent.com/u/39589027?s=256">
</p>

# BepInEx
![Github All Releases](https://img.shields.io/github/downloads/bepinex/bepinex/total.svg)
![GitHub release](https://img.shields.io/github/release/bepinex/bepinex.svg)
[![BepInEx Discord](https://user-images.githubusercontent.com/7288322/34429117-c74dbd12-ecb8-11e7-896d-46369cd0de5b.png)](https://discord.gg/MpFEDAg)

Bepis Injector Extensible

---

BepInEx is a plugin / modding framework for Unity Mono, IL2CPP and .NET framework games (XNA, FNA, MonoGame, etc.)

(Currently only Unity Mono has stable releases)

#### Platform compatibility chart

|              | Windows | OSX  | Linux | ARM |
|--------------|---------|------|-------|-----|
| Unity Mono   | ✔️       | ✔️    | ✔️     | N/A |
| Unity IL2CPP | ✔️       | ❌    | ❌ (Wine only)  | ❌   |
| .NET / XNA   | ✔️       | Mono | Mono  | N/A |

A more comprehensive comparison list of features and compatibility is available at https://bepis.io/unity.html


## Resources

**[Latest releases](https://github.com/BepInEx/BepInEx/releases)**

**[Bleeding Edge builds](https://builds.bepis.io/projects/bepinex_be)**

**[How to install (latest releases)](https://docs.bepinex.dev/articles/user_guide/installation/index.html)**

**[How to install (Bleeding Edge, BepInEx 6)](https://docs.bepinex.dev/master/articles/user_guide/installation/index.html)**

**[User and developer guides](https://docs.bepinex.dev/master/)**

**[Discord server](https://discord.gg/MpFEDAg)**

### Available plugin loaders

| Name              | Link to project                                                                           |
|-------------------|-------------------------------------------------------------------------------------------|
| BSIPA             | [BepInEx.BSIPA.Loader](https://github.com/BepInEx/BepInEx.BSIPA.Loader)                   |
| IPA               | [IPALoaderX](https://github.com/BepInEx/IPALoaderX)                                       |
| MelonLoader       | [BepInEx.MelonLoader.Loader](https://github.com/BepInEx/BepInEx.MelonLoader.Loader)       |
| MonoMod           | [BepInEx.MonoMod.Loader](https://github.com/BepInEx/BepInEx.MonoMod.Loader)               |
| MuseDashModLoader | [BepInEx.MDML.Loader](https://github.com/BepInEx/BepInEx.MDML.Loader)                     |
| Partiality        | [BepInEx-Partiality-Wrapper](https://github.com/sinai-dev/BepInEx-Partiality-Wrapper)     |
| Sybaris           | [BepInEx.SybarisLoader.Patcher](https://github.com/BepInEx/BepInEx.SybarisLoader.Patcher) |
| UnityInjector     | [BepInEx.UnityInjector.Loader](https://github.com/BepInEx/BepInEx.UnityInjectorLoader)    |
| Unity Mod Manager | [Yan.UMMLoader](https://github.com/hacknet-bar/Yan.UMMLoader)                             |
| uMod              | [BepInEx.uMod.Loader](https://github.com/BepInEx/BepInEx.uMod.Loader)                     |

## Used libraries
- [NeighTools/UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) -
  3.4.0.0 ([fad307f](https://github.com/NeighTools/UnityDoorstop/commit/fad307fda5c968d05675f17a49af7e790966fec3))
- [NeighTools/UnityDoorstop.Unix](https://github.com/NeighTools/UnityDoorstop.Unix) -
  1.5.1.0 ([06e9790](https://github.com/NeighTools/UnityDoorstop.Unix/commit/06e979008730cf89c6bcf8806f2c18c80b0a7b21))
- [BepInEx/HarmonyX](https://github.com/BepInEx/HarmonyX) -
  2.7.0 ([2537257](https://github.com/BepInEx/HarmonyX/commit/253725768e59b0e1ea90105cdbcc4a0a477422c7))
- [0x0ade/MonoMod](https://github.com/0x0ade/MonoMod) -
  v21.11.11.1 ([ede81f4](https://github.com/MonoMod/MonoMod/commit/ede81f48924d58abf05359409fad740fe2b0dfb5))
- [jbevain/cecil](https://github.com/jbevain/cecil) -
  0.10.4 ([98ec890](https://github.com/jbevain/cecil/commit/98ec890d44643ad88d573e97be0e120435eda732))

#### IL2CPP libraries

- [Perfare/Il2CppDumper](https://github.com/Perfare/Il2CppDumper) - v6.6.3
  fork ([112e2e8](https://github.com/BepInEx/Il2CppDumper/commit/112e2e8c369dfcb6d5718fd4ad7e3838d7ddabbf))
- [SamboyCoding/Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)  -
  2021.6.1 ([03a18c8](https://github.com/SamboyCoding/Cpp2IL/commit/03a18c89555d6ce3e8863307928fb0237ada39e3))
- [BepInEx/Il2CppAssemblyUnhollower](https://github.com/BepInEx/Il2CppAssemblyUnhollower) - v0.4.21.0 (fork of
  knah/Il2CppAssemblyUnhollower)
  fork ([12f4224](https://github.com/BepInEx/Il2CppAssemblyUnhollower/commit/12f4224bec3bd6bb1748b6982e0f62d1cab4e0b0))
- [mono/mono](https://github.com/mono/mono) - 6.12.0.93
  fork ([7328415](https://github.com/BepInEx/mono/commit/7328415ac575399a71f32487e97bce9d5fe7f6ca))

## Credits
- [Usagirei](https://github.com/Usagirei) - Code for using the console and for assisting with technical support
- [essu](https://github.com/exdownloader) - Project logo, moral support and lots of misc. help
- [denikson](https://github.com/denikson) - [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) for the patchless loader
- [nn@](https://twitter.com/NnAone2cmg) - Japanese translation of the wiki
