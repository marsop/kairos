import os

files_to_update = {
    'src/Kairos.Shared/Resources/Strings.de.resx': 'Konto',
    'src/Kairos.Shared/Resources/Strings.es.resx': 'Cuenta',
    'src/Kairos.Shared/Resources/Strings.gl.resx': 'Conta',
    'src/Kairos.Shared/Resources/Strings.gsw.resx': 'Konto',
}

for file, translation in files_to_update.items():
    if os.path.exists(file):
        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()

        # Replace the value for SettingsAccount
        old_str = '<data name="SettingsAccount" xml:space="preserve">\n    <value>Account</value>\n  </data>'
        new_str = f'<data name="SettingsAccount" xml:space="preserve">\n    <value>{translation}</value>\n  </data>'

        if old_str in content:
            content = content.replace(old_str, new_str)
            with open(file, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Updated {file}")
        else:
            print(f"Pattern not found in {file}")
