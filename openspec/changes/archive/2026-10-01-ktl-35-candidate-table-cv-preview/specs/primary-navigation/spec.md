## MODIFIED Requirements

### Requirement: Shell content width

Every page inside the application shell SHALL center its content area and cap it at 1440 CSS
pixels, including the side padding. It SHALL widen only while a CV is shown beside other content:

- the candidate CV preview beside the sections (`candidate-profile-pages`);
- a candidate's CV shown in a panel beside a table (`data-tables`).

It SHALL return to the default cap when that CV closes.

#### Scenario: Default content width on a wide screen

- **WHEN** a page without a CV preview is shown at 1920×1080
- **THEN** its content area is at most 1440 CSS pixels wide and horizontally centered

#### Scenario: Table CV panel widens the shell

- **WHEN** a user opens a CV beside the candidate list at 1920×1080 and then hides it
- **THEN** the content area is wider than 1440 CSS pixels while the panel is shown, and at most
  1440 CSS pixels once it is hidden
