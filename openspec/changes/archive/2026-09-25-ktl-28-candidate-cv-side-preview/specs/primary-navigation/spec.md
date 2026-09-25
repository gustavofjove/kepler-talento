## ADDED Requirements

### Requirement: Shell content width

Every page inside the application shell SHALL center its content area and cap it at 1440 CSS
pixels, including the side padding. The only case that widens it is the candidate CV preview
beside the sections (`candidate-profile-pages`).

#### Scenario: Default content width on a wide screen

- **WHEN** a page without a CV preview is shown at 1920×1080
- **THEN** its content area is at most 1440 CSS pixels wide and horizontally centered
