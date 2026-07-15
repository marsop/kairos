import os
import re

files_to_fix = [
    'src/Kairos.Shared/Resources/Strings.resx',
    'src/Kairos.Shared/Resources/Strings.de.resx',
    'src/Kairos.Shared/Resources/Strings.es.resx',
    'src/Kairos.Shared/Resources/Strings.gl.resx',
    'src/Kairos.Shared/Resources/Strings.gsw.resx'
]

for filepath in files_to_fix:
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Simple regex to replace the conflict markers while keeping both sides
    content = re.sub(r'<<<<<<< HEAD\n(.*?)\n=======\n(.*?)\n>>>>>>> origin/master', r'\1\n\2', content, flags=re.DOTALL)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

    print(f"Fixed {filepath}")
