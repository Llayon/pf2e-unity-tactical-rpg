# UI Style Guide — Worn Slate

**Reference:** Solasta Crown of the Magister (primary), Pathfinder WotR panel construction.
**Genre:** Medieval dark fantasy tactical RPG (PF2e rules).

## Core Principle

UI should feel like **crafted medieval objects** — dark stone surfaces held by forged bronze frames. Tactile, physical, grounded. The gameplay grid is the star; UI supports with atmosphere, never competes for attention.

## Visual Identity

- **Surface material:** Dark slate stone with visible grain/texture — never flat solid color
- **Frame material:** Aged forged bronze with subtle hammered patina — not clean/digital
- **Construction feel:** Rivets, brackets, metal joints at corners — the panel looks "built"
- **Overall mood:** An adventurer's equipment, worn but well-crafted

## Color Palette

| Role | Hex | Usage |
|------|-----|-------|
| Background (stone) | `#1A1A22` to `#20202C` | Panel fills — dark slate with visible texture |
| Surface | `#242430` | Buttons, slots — slightly lighter stone |
| Frame (bronze) | `#6B5A3E` to `#8B7340` | Panel border frames, aged bronze metal |
| Accent | `#8B7340` | Active/selected elements, bronze highlight |
| Accent Light | `#C9A84C` | Hover highlights, important indicators, gold tone |

## Text Colors

| Role | Hex | Usage |
|------|-----|-------|
| Primary | `#E8E8EC` | Main readable text |
| Secondary | `#8888A0` | Hotkey hints, labels, less important info |
| Negative | `#CC4444` | Damage numbers, errors, failures |
| Positive | `#44AA66` | Healing, success, buffs |
| Warning | `#DDAA33` | Cover/concealment risk hints |

## Borders and Frames

- **Panel outer frame:** 8-10px, bronze metal texture (`Frame` color), slightly weathered
- **Corner accents:** small metal rivets or bracket pieces at each corner (~24×24px)
- **Inner edge shadow:** 2px dark line between frame and stone fill for depth
- **Internal dividers:** 1-2px, `Frame` color at 30-40% opacity
- **Style:** textured metal — not solid flat lines, not gradient glow

## Surfaces and Textures

- **Panel fill:** dark slate stone with visible grain — organic, not digital noise
- **Opacity:** panels fully opaque (the stone surface is a real material, not a transparent overlay)
- **Frame texture:** hammered/aged bronze with slight color variation — not perfectly uniform
- **Materials should feel REAL:** stone, metal, leather — not flat UI rectangles

## Visual Weight Hierarchy

| Weight | Elements | Notes |
|--------|----------|-------|
| Invisible | Grid, 3D scene | UI never obscures gameplay |
| Light | Tooltips, targeting hints | Appear/disappear contextually, thinner frame |
| Medium | TurnHUD, ActionBar, InitiativeBar, CombatLog | Bronze-framed stone panels |
| Heavy (rare) | Modal dialogs (EncounterEnd, ReactionPrompt) | Can have slightly more ornate corners |

## Decoration Rules

- **Functional, not ornamental.** Decoration serves construction (rivets hold the frame together).
- Corner rivets/brackets on all framed panels — they unify the style.
- No filigree, no vine patterns, no scrollwork. This is forged metalwork, not calligraphy.
- Bronze accent used for: frame, active mode highlight, selected state.
- More ornate corners allowed only on modal dialogs.

## Button States

| State | Background | Border | Text |
|-------|-----------|--------|------|
| Normal | `Surface` (stone) | `Frame` (bronze) thin | `Primary` |
| Hover | `Surface` lightened 8% | `Accent Light` | `Primary` |
| Pressed | `Surface` darkened 8% | `Accent` | `Primary` |
| Active/Selected | `Surface` | `Accent Light` glow 2px | `Accent Light` |
| Disabled | `Surface` at 50% opacity | `Frame` at 40% | `Secondary` at 50% |

## Typography

- **Primary font:** TBD — target a readable serif or semi-serif with fantasy character (Cinzel for headers, Noto Sans for body)
- **Headers:** 20-24pt, `Primary` color, medium weight
- **Body/buttons:** 16-18pt, `Primary` color, regular weight
- **Hints/hotkeys:** 12-14pt, `Secondary` color, regular weight

## Sprite Generation Prompt Prefix

Use this block at the start of every AI image generation prompt for consistency:

> Style: "Worn Slate" — dark slate stone surface (#1A1A22) held by an aged bronze metal frame (#6B5A3E to #8B7340) with small corner rivets. Tactile, medieval, crafted feel. Semi-realistic painted texture with visible stone grain and hammered metal patina. No filigree, no scrollwork, no glow effects. Matte finish. Reference: Solasta Crown of the Magister UI panels.

## 9-Slice Sprite Inventory

| Sprite | Size | Used For | Status |
|--------|------|----------|--------|
| Action Bar | 1024×256 | ActionBar (bottom-center) | In progress |
| Panel Background | 512×512 | TurnHUD, CombatLog, EncounterEndPanel, ReactionPrompt | TODO |
| Tooltip | 256×256 | TargetingHintPanel, future tooltips | TODO |
| Initiative Slot | 128×128 | InitiativeBar slots | TODO |
| Button | 256×64 (×4 states) | All clickable buttons | TODO |
| Icon Frame | 96×96 | Action icon containers | TODO |
