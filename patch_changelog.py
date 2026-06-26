import re

with open('CHANGELOG.md', 'r') as f:
    content = f.read()

# Replace Unreleased section to be 1.3.0
new_content = re.sub(
    r'## \[Unreleased\]',
    '## [1.3.0]',
    content
)

with open('CHANGELOG.md', 'w') as f:
    f.write(new_content)

print("Updated CHANGELOG.md version")
