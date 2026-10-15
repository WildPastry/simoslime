# SimoSlime

The most basic Slay the Spire-style card game: one fight against a Slime, with a deck of five
Strikes and five Defends. It's made in Unity 6.6 (the editor version is in
`ProjectSettings/ProjectVersion.txt`) and ships as a per-user Windows installer. To build the
installer, run `tools/build.ps1` from the repo root in Windows PowerShell; it writes
`Release/SimoSlime-Setup-<version>.exe`. That script needs the Unity editor named in
`ProjectVersion.txt`, installed through Unity Hub, and Inno Setup 7.
