# Mobile User Experience

## Navigation

Use a bottom navigation bar on phones: **Projects**, **Items**, and **More**. A persistent floating or bottom-docked **New Item** action is visible within a project. Avoid desktop-style dense grids.

## Capture screen

Present the capture controls in this order:

1. Project (locked to current project, with Change action)
2. Area (searchable recent-first picker)
3. Trade (large chips for recent trades plus View All)
4. Type (two large segmented buttons: Observation and Punch List)
5. Camera button (dominant action)

The camera action is disabled until Area, Trade and Type are selected. Show the current selections immediately above the camera button so mistakes are visible before capture. After Trade selection, show its configured responsible company as read-only context; do not require another subcontractor selection.

## Trade partner view

Trade partners land on **My Company's Items**, filtered to projects and trades mapped to their company. Each card shows item type, number, status, area, photo thumbnail and due date when present. They can open an item, review English/Spanish descriptions, comment, attach completion photos and change status. During MVP they can also close or reopen items, with a confirmation and required reopen reason.

## Review screen states

| State | User experience |
| --- | --- |
| Local draft | Photo thumbnail visible; message says it is stored on this device |
| Uploading | Determinate progress when available; Cancel preserves local draft |
| Analyzing | Skeleton fields and **Enter manually now** action |
| AI ready | Editable title/description, marked AI suggestion |
| AI failed | Photo retained; Retry and Enter Manually buttons |
| Saving | Disable duplicate submission but keep screen content |
| Saved | Item number and Capture Another/Edit/View Items actions |

## Description editor

- Title: maximum 80 characters.
- English description: plain text, recommended 1-3 concise sentences, maximum 2,000 characters.
- **Translate to Spanish** appears beneath English.
- Spanish appears in a separate expandable section labeled **Spanish translation**.
- When English changes after translation, show a nonblocking stale indicator and Regenerate action.
- Never concatenate English and Spanish into one database field.

## Fast repeat capture

After save, **Capture Another** starts a new draft and carries forward Project, Area, Trade and Item Type. Each carried value is visibly selected and individually changeable. This is essential when walking one area and documenting several items for the same trade.

## Area picker

Show recent areas first, then a searchable hierarchical list. Display full paths such as `Building A / Level 2 / Room 214`. Do not use free text for normal capture. PM/Admin users manage the list elsewhere.

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
