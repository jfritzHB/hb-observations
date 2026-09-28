# Mobile User Experience

## Navigation

Use a bottom navigation bar on phones: **Projects**, **Items**, and **More**. A persistent floating or bottom-docked **New Item** action is visible within a project. Avoid desktop-style dense grids.

## Capture screen

The capture screen is a fast field tool, not a multi-step form. Refined in Slice 1.1 after product-owner testing:

- **Project:** a compact context bar (project name and number, with a **Change** action), always visible but taking little space.
- **Where?** one location control (see *Location control* below) with recent locations as one-tap chips.
- **Trade:** large chips for recently/commonly used trades plus **More** (all trades). After selection, show **Responsible: <company>** as compact read-only context. There is no company/subcontractor picker; Project Trade is authoritative.
- **Type:** two large, equally prominent controls, **Observation** and **Punch List**, whose selected state is obvious at a glance.
- **Take Photo:** the dominant action, docked where the thumb reaches it above the bottom navigation. It becomes visually prominent once location, trade and type are set.

Order on screen is Project, location, Trade, Type, Take Photo. Do not use numbered steps, and do not repeat a second summary of the selections before the camera: the controls themselves show the current choices. Keep explanatory copy out of the normal path, and keep the path to Take Photo within about one screen at 360px. The bottom navigation must never obscure capture controls or focused elements.

## Trade partner view

Trade partners land on **My Company's Items**, filtered to projects and trades mapped to their company. Each card shows item type, number, status, area, photo thumbnail and due date when present. They can open an item, review English/Spanish descriptions, comment, attach completion photos and change status. During MVP they can also close or reopen items, with a confirmation and required reopen reason.

## Review screen states

| State | User experience |
| --- | --- |
| Local draft | Photo thumbnail visible; message says it is stored on this device |
| Uploading | Determinate progress when available; Cancel preserves local draft |
| Describe | Editable English description ready for typing, plus **Generate description with AI** |
| Analyzing | Only after the user requests AI: skeleton fields and **Enter manually now** action |
| AI ready | Editable title/description, marked AI suggestion |
| AI failed | Photo retained; Retry and Enter Manually buttons |
| Saving | Disable duplicate submission but keep screen content |
| Saved | Item number and Capture Another/Edit/View Items actions |

## Description editor

- After the photo, the English description is immediately editable so the user can type their own text (manual path). AI is never required to save.
- A separate explicit action, **Generate description with AI**, drafts an editable suggestion (Slice 3). Generated text is labelled **AI suggestion** until accepted or edited.
- Title: maximum 80 characters.
- English description: plain text, recommended 1-3 concise sentences, maximum 2,000 characters.
- **Translate to Spanish** appears beneath English once any English text exists, whether typed, generated, or generated then edited (Slice 4).
- Spanish appears in a separate expandable section labeled **Spanish translation**.
- When English changes after translation, show a nonblocking stale indicator and Regenerate action.
- Never concatenate English and Spanish into one database field.

## Fast repeat capture

After save, **Capture Another** starts a new draft and carries forward Project, structured Area, Location Detail (where appropriate), Trade and Item Type. Each carried value is visibly selected and individually changeable. This is essential when a superintendent walks into, say, Unit 214 and documents several Electrical or Drywall items in a row: the next item should need only the photo.

## Location control

One control answers "Where?". The user taps it and can immediately type, for example `214`, `Office`, `Level 2` or `East Corridor`.

- As the user types, matching structured Areas appear with full paths (for example `Building A / Level 2 / Office 201`), recent matches first, as large tappable results.
- Tapping a result selects that structured Area (its `AreaId`), shows it as a removable chip, and clears the search text.
- Typed text is always **Location Detail** (maximum 120 characters) unless the user explicitly taps a suggestion. Never convert text to a structured Area automatically, even on an exact name match: a superintendent typing `Lobby` may mean it only as field detail and must not unknowingly attach master data.
- Once an Area is selected, the same field is for detail such as `North wall`, with the placeholder **Add location detail (optional)**. Project-wide Area results may still appear while typing, clearly labelled **Replace area**; tapping one replaces the selected Area (a quick correction without backing out). Suggestions are not limited to children of the selected Area. Without an Area, the text alone (such as `Unit 214`) is the location.
- Typed text never creates Area master data. PM/Admin users manage structured Areas elsewhere.
- Recent locations (Area and/or detail combinations used on this device) appear as one-tap chips.
- A **Browse** action opens the full hierarchical Area list for choosing without typing.
- Any active Area may be selected, including buildings and levels, not only rooms.
- Accessibility: an ARIA combobox with a listbox of results, arrow-key and Enter selection, Escape to close, result counts announced, and a visible focus indicator.

## Error behavior

- Never clear entered text after an error.
- Explain whether the draft exists only on the device or is saved to the server.
- Provide retry at the failed boundary: Upload, Analyze, Translate, or Save.
- If the photo cannot be restored from local storage, say so plainly and require a new photo.

## Accessibility and device behavior

- Support camera permission denial with file-picker fallback.
- Respect device safe areas and on-screen keyboard.
- Compress large images in a Web Worker before upload while preserving enough detail for inspection.
- Correct image orientation from EXIF and strip unnecessary location metadata by default.
- Do not rely on color alone for status.
