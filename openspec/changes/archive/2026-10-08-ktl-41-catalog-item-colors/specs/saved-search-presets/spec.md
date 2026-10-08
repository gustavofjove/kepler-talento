## ADDED Requirements

### Requirement: Criteria summary chips carry catalog colours

In the shared read-only criteria summary, wherever it is rendered (the advanced search page, the
criteria dialog, the preset list and the position page), each chip of the skills, languages,
programs and tags groups SHALL be drawn in the colour its value holds in its catalog family. A
value that no longer resolves SHALL fall back to the default orange. Chips of the other groups
(text, availability, «Comprobado desde», CV presence) SHALL keep the default orange. The summary's
content SHALL be unchanged.

#### Scenario: Catalog criterion is coloured in the summary

- **WHEN** the criteria summary is rendered for filters that contain the `blue` skill «Análisis»
- **THEN** that chip is drawn blue on every screen that renders the summary

#### Scenario: Non-catalog criterion keeps the default

- **WHEN** the criteria summary is rendered for filters with a text criterion
- **THEN** the text chip is drawn in the default orange

#### Scenario: Unresolved criterion falls back to orange

- **WHEN** a preset's criteria name a tag that no longer matches any tag in the catalog
- **THEN** its chip in the summary is drawn in the default orange
