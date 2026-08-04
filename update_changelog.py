import re

with open("CHANGELOG.md", "r") as f:
    content = f.read()

# We need to insert a new entry under ### Changed or ### Removed in the most recent unreleased block if there is one, or just in the latest version block
# Actually, since we're making a change in the current development cycle, we should add it to the latest version section (1.6.0) under ### Changed or ### Removed
# But let's check if there's an Unreleased section, there isn't. So we append it to 1.6.0.
# The latest version block is `## [1.6.0] - ...`

# Let's add it under `### Changed` of `## [1.6.0]`
changed_marker = "### Changed"

new_entry = "- **Settings UI**: Removed the inline trash-icon button for deleting Activity Groups. Deletion is now only available from within the group editing dialog.\n"

if changed_marker in content:
    content = content.replace(changed_marker, f"{changed_marker}\n{new_entry}", 1)
else:
    # If not found, just put it after the version line
    # (In this case we know ### Changed is there)
    pass

with open("CHANGELOG.md", "w") as f:
    f.write(content)
