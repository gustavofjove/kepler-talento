# KTL-26: Search form basic-filter design

This is the layout and interaction record for [the KTL-26 ticket](../../openspec/KTL-26.md).
It describes the shared criteria editor used by advanced search, saved presets and position
requirements. The interaction was delivered in the shared criteria form.

## Closed by default

```text
Texto [ Buscar candidatos...       ]   CV [x] Con CV [x] Sin CV   Estados [ Todos los estados v ]
```

Text takes the available width. CV and status stay compact. The three fields share one row when
there is room and wrap naturally on narrower screens. The status control remains closed when its
default, unrestricted selection is active.

## Status options when opened

The option panel opens as a vertical popover anchored to the status control. It overlays the content
without moving the fields below it. The example uses seven options to show that the list can scroll
without depending on the current count of five; it does not propose two new status values.

```text
Estados [ Todos los estados ^ ]
        +-----------------------------+
        | [x] Nuevo               ( ) |
        | [x] Disponible          ( ) |
        | [x] En proceso          ( ) |
        | [x] Contratado          ( ) |
        | [x] Descartado          ( ) |
        | [x] Opción 6            ( ) |
        | [x] Opción 7            ( ) |
        +-----------------------------+
```

The checkbox changes just its own status. The circular button selects only its row's status; it
looks like a radio control but is a button with a name such as `Seleccionar solo Disponible`.
Selecting only `Disponible` makes the closed control read `Disponible`. A partial selection shows
`Seleccionar todos` at the end so the default can be restored in one action:

Clicking outside the options closes them without changing the selection. Escape also closes them
and returns focus to the status control.

```text
Estados [ Disponible ^ ]
        +-----------------------------+
        | [ ] Nuevo               ( ) |
        | [x] Disponible          (.) |
        | [ ] En proceso          ( ) |
        | [ ] Contratado          ( ) |
        | [ ] Descartado          ( ) |
        |-----------------------------|
        |          Seleccionar todos  |
        +-----------------------------+
```

For several selected statuses, the closed control summarizes their count rather than listing a
long sequence. The popover stays inside the viewport at narrow widths and scrolls internally when
needed. Each checkbox and button needs a distinct accessible name, visible focus indication and
keyboard operation.

## Selection rules

| Control | Default                      | One selected                      | All selected | Last-choice rule            |
| ------- | ---------------------------- | --------------------------------- | ------------ | --------------------------- |
| CV      | Both checked                 | `Con CV` = `yes`; `Sin CV` = `no` | `hasCv=''`   | Do not allow both unchecked |
| Status  | Every current status checked | Restrict to that status           | Unrestricted | Do not allow all unchecked  |

The backend currently treats an empty status list as unrestricted, so an empty-looking UI state
would contradict the results. The closed status disclosure is a presentation state only; opening
and closing it does not change filters. The status vocabulary and its possible relationship to
positions need separate domain analysis.
