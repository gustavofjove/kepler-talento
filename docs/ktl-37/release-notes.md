# KTL-37 release notes: candidate summary and dates

The candidate page pairs Datos principales with Disponibilidad in its first row. On narrow screens, or while Datos principales is being edited, the panels stack in that order. The availability controls retain their immediate save, reconfirm and undo behavior. The former candidate Auditoría panel has been removed; the administration Auditoría section is unchanged.

The availability form uses «Registrar» for its submit action. Its date fields and buttons share one row when space permits and wrap on narrow screens.
The panel heading supplies «Disponibilidad» once; the value below it shows only the bold state. When space permits, regular-weight «Comprobado el …» information appears to its right on the same row. The summary, hints and actions have comfortable vertical spacing and wrap on narrow screens.

A logically removed candidate shows «(Inactivo)» beside the name. Every candidate reader sees a relative «Actualizado …» line near the status action and can open its information control to read the exact creation and update times. The breadcrumb keeps the plain name.

Datos principales labels the retention review date «Revisión LOPD». A past review date shows «(vencida)»; a date from today through 30 calendar days ahead shows «(próxima)». These markers are display hints only and do not trigger a server workflow. The form explains that this date is for reviewing whether candidate data may still be retained.

Recepción is hidden in read mode when its calendar day matches the record's local creation day. It remains visible when empty or different, and the field is always available in the form. Stored values, API contracts, permissions and document access are unchanged.
