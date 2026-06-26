import re

changelog_path = 'CHANGELOG.md'
with open(changelog_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Add a fix entry under [Unreleased] section.
# The Unreleased section starts at ## [Unreleased]
# It should go under ### Fixed

unreleased_pattern = r'(## \[Unreleased\]\n)([\s\S]*?)(?=\n## \[)'
match = re.search(unreleased_pattern, content)

if match:
    unreleased_content = match.group(2)
    if '### Fixed' in unreleased_content:
        unreleased_content = unreleased_content.replace('### Fixed\n', '### Fixed\n- Fix sync conflict dialog falsely appearing on page refresh.\n- Prevent URI Too Long error in Supabase activity event synchronization.\n')
    else:
        unreleased_content += '\n### Fixed\n- Fix sync conflict dialog falsely appearing on page refresh.\n- Prevent URI Too Long error in Supabase activity event synchronization.\n'

    new_content = content[:match.start(2)] + unreleased_content + content[match.end(2):]
    with open(changelog_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Updated changelog")
else:
    print("Could not find Unreleased section")
