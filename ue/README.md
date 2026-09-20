# Unreal clothing preview

`RollerSkate.uproject` is a content-only UE **5.4** project (no C++ module; VS 2022 14.44 cannot compile UE 5.4's editor PCH).

- Garments you will cook must live under `/Game/MainFolder/...` so overlay paths match the game.
- The look-dev map is `/Game/ModPreview/Preview`. Do not pack that folder.
- Open it with `.\ue\open_preview.ps1` (add `-Setup` after a new Blender export).
