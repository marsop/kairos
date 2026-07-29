---
description: "Use when adding, renaming, editing, or removing localization keys in resource files. Ensures every key change is applied consistently across all language variants."
name: "Translation Key Parity"
applyTo: "src/Kairos.Application/Resources/Strings*.resx"
---
# Translation Key Parity

- Keep key sets identical across all `Strings*.resx` files.
- When a key is added, add it in every language file in the same change.
- When a key name changes, rename it in every language file in the same change.
- When a key is removed, remove it from every language file in the same change.
- When base-language text changes due to semantic meaning updates, review and update all translations in the same change.
- Do not leave temporary gaps with missing keys, stale key names, or placeholder mismatches across languages.