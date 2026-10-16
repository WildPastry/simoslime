# SimoSlime: agent guide

A Unity 6.6 card game (uGUI, Input System, Mono, x64) that ships as a per-user Inno Setup
installer. Fight rules are plain C# with no Unity references; thin MonoBehaviours show them.

The workflow is machine-wide, in `~/.claude/CLAUDE.md`; this file holds only what is true of
SimoSlime.

## Addresses

- **Plan and ledger:** `~/.claude/plans/simoslime.md`, which holds the decisions, the chunk list
  with commit SHAs, and the design picks.
- **Tracker:** GitHub Issues in `WildPastry/simoslime`, via the `gh` CLI.
- **Green:** `tools/unity.ps1 -Task tests` passes and `tools/unity.ps1 -Task build` succeeds.
  A chunk that touches the installer also needs `tools/build.ps1` to succeed.
- **Proof of UI:** a Development build run windowed with `-autoplay <script>`, then reading its
  screenshots and log.
- **PR:** into `main` from [.github/pull_request_template.md](.github/pull_request_template.md).
- **Skills:** [.claude/skills-manifest.json](.claude/skills-manifest.json) lists what the
  workflow calls; `/setup-skills` installs them.

## Loop mapping

- **One look.** The game has a single dark look, so "both themes" means that one look.
- **Four resolutions instead of phone and laptop width:** 1280x720, 1920x1080, 2560x1440 and
  2560x1080 (21:9). The phone-width checklist runs at 1280x720 and the laptop checklist at
  1920x1080.

## Concessions

- **Mouse only, plus Esc and Enter.** There's no arrow-key navigation and no screen reader, which
  fails WCAG 2.1.1 Keyboard. To reverse it, add EventSystem navigation and a Unity Accessibility
  module hierarchy for Narrator.
- **Unsigned installer.** Other PCs see SmartScreen's "More info, Run anyway". To reverse it,
  sign `SimoSlime.exe` and the setup with a code-signing certificate.

## Project rules, counter-default only

1. **Never run Unity on the repo folder.** The human's editor may have it open. Use
   `tools/unity.ps1`, which mirrors the project to `%LOCALAPPDATA%\SimoSlime-mirror` and runs
   batchmode there.
2. **Hand-write a `.meta` for every new file or folder you add under `Assets/`**
   (`fileFormatVersion: 2` plus a fresh 32-hex `guid`), in the same commit. Otherwise the mirror
   and the editor mint different GUIDs and break script references. Assets that `generate`
   creates (scenes, prefabs, TextMesh Pro resources) are the exception: their Unity-minted
   `.meta` comes back from the mirror with them, so both sides share one GUID.
3. **Scenes, the Card prefab and project settings are generated** by
   `SimoSlime.Editor.SceneGenerator`. Change the generator, never the scene; hand edits are
   overwritten on the next generate.
4. **The version string lives only in `ProjectConfig.Version`.**
5. **Development-only code sits inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR`.** A release build
   has no autoplay.
6. **The agent runs the game windowed** (`-screen-fullscreen 0`), never fullscreen over the
   human's screen.
7. **Hard gate, this repo:** any download (assets, sounds, packages outside Unity's registry)
   needs the human's yes first.
